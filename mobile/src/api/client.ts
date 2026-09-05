import axios, {
  AxiosError,
  AxiosHeaders,
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import { apiRoot, config } from '@/constants/config';
import type { ApiEnvelope, ApiFieldError, ApiFailure, AuthTokens } from '@/types/api';
import { tokenStorage } from './token-storage';

/**
 * The single HTTP client.
 *
 * Two responsibilities live here and nowhere else:
 *
 *  1. Attach the access token to every outbound request.
 *  2. On a 401, refresh once and replay — with all concurrent 401s queued
 *     behind that single refresh, so ten screens loading at once produce one
 *     refresh call rather than ten racing rotations that invalidate each other.
 *
 * Refresh-token rotation makes the second point a correctness issue, not an
 * optimisation: the server issues a new refresh token and revokes the old one
 * on every use, so two parallel refreshes would have the loser present an
 * already-rotated token, which the server treats as theft and responds to by
 * revoking the entire session family.
 */

export type ApiErrorShape = {
  status: number;
  errorCode: string;
  message: string;
  fieldErrors: ApiFieldError[];
  traceId?: string;
  isNetworkError: boolean;
  isTimeout: boolean;
};

/** Normalised error every caller in the app sees, regardless of what went wrong. */
export class ApiError extends Error implements ApiErrorShape {
  readonly status: number;
  readonly errorCode: string;
  readonly fieldErrors: ApiFieldError[];
  readonly traceId?: string;
  readonly isNetworkError: boolean;
  readonly isTimeout: boolean;

  constructor(shape: ApiErrorShape) {
    super(shape.message);
    this.name = 'ApiError';
    this.status = shape.status;
    this.errorCode = shape.errorCode;
    this.fieldErrors = shape.fieldErrors;
    this.traceId = shape.traceId;
    this.isNetworkError = shape.isNetworkError;
    this.isTimeout = shape.isTimeout;
  }

  /** True when retrying the identical request could plausibly succeed. */
  get isRetryable(): boolean {
    return this.isNetworkError || this.isTimeout || this.status >= 500 || this.status === 429;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isValidation(): boolean {
    return this.status === 400 || this.status === 422;
  }
}

/**
 * Called when the session is unrecoverable. Wired to the auth store at startup
 * rather than imported from it, because the store imports this module — a
 * direct import would be a cycle and would blow up at bundle time.
 */
type SessionExpiredHandler = () => void | Promise<void>;

let onSessionExpired: SessionExpiredHandler | null = null;

export function setSessionExpiredHandler(handler: SessionExpiredHandler | null): void {
  onSessionExpired = handler;
}

/** Marks a request as exempt from the Authorization header (login, register, refresh). */
export const SKIP_AUTH = 'x-skip-auth';

type RetriableConfig = InternalAxiosRequestConfig & { _retriedAfterRefresh?: boolean };

export const apiClient: AxiosInstance = axios.create({
  baseURL: apiRoot,
  timeout: config.api.timeoutMs,
  headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
});

// --- Request: attach the bearer token -------------------------------------

apiClient.interceptors.request.use(async (request) => {
  const headers = AxiosHeaders.from(request.headers);

  if (headers.has(SKIP_AUTH)) {
    headers.delete(SKIP_AUTH);
    request.headers = headers;
    return request;
  }

  const token = await tokenStorage.getAccessToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  request.headers = headers;
  return request;
});

// --- Response: unwrap the envelope, refresh on 401 -------------------------

/**
 * The in-flight refresh, shared by every request that 401s while it runs.
 * Null when no refresh is happening.
 */
let refreshInFlight: Promise<string | null> | null = null;

async function performRefresh(): Promise<string | null> {
  const refreshToken = await tokenStorage.getRefreshToken();
  if (!refreshToken) return null;

  try {
    // A bare axios call, not `apiClient`: routing this through the instance
    // would re-enter these interceptors and a failing refresh would try to
    // refresh itself forever.
    const response = await axios.post<ApiEnvelope<AuthTokens>>(
      `${apiRoot}/auth/refresh`,
      { refreshToken },
      {
        timeout: config.api.timeoutMs,
        headers: { 'Content-Type': 'application/json' },
      },
    );

    const body = response.data;
    if (!body?.success) return null;

    await tokenStorage.save(body.data);
    return body.data.accessToken;
  } catch {
    // Any failure here — expired token, revoked family, server down — is
    // treated the same way: the session cannot be recovered automatically.
    return null;
  }
}

async function refreshAccessToken(): Promise<string | null> {
  refreshInFlight ??= performRefresh().finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetriableConfig | undefined;
    const status = error.response?.status;

    const isRefreshCall = original?.url?.includes('/auth/refresh') ?? false;

    // Exactly one refresh attempt per request. `_retriedAfterRefresh` is the
    // loop guard: a second 401 after a successful refresh means the token is
    // valid but the caller genuinely lacks access, and retrying again would
    // spin forever.
    if (status === 401 && original && !original._retriedAfterRefresh && !isRefreshCall) {
      original._retriedAfterRefresh = true;

      const newToken = await refreshAccessToken();

      if (newToken) {
        const headers = AxiosHeaders.from(original.headers);
        headers.set('Authorization', `Bearer ${newToken}`);
        original.headers = headers;
        return apiClient.request(original);
      }

      await tokenStorage.clear();
      await onSessionExpired?.();
    }

    return Promise.reject(toApiError(error));
  },
);

/** Maps anything axios can throw onto the one error shape the app handles. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  if (axios.isAxiosError(error)) {
    const response = error.response as AxiosResponse<ApiFailure> | undefined;

    if (!response) {
      const isTimeout = error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT';
      return new ApiError({
        status: 0,
        errorCode: isTimeout ? 'timeout' : 'network_error',
        message: isTimeout
          ? 'The request took too long. Check your connection and try again.'
          : 'Cannot reach the server. Check your internet connection.',
        fieldErrors: [],
        isNetworkError: !isTimeout,
        isTimeout,
      });
    }

    const body = response.data;
    const hasEnvelope = body && typeof body === 'object' && 'success' in body;

    return new ApiError({
      status: response.status,
      errorCode: hasEnvelope ? (body.errorCode ?? 'error') : String(response.status),
      // Never surface a raw server exception. The envelope's message is written
      // for users; anything else falls back to a generic line.
      message: hasEnvelope && body.message ? body.message : defaultMessageFor(response.status),
      fieldErrors: hasEnvelope && Array.isArray(body.errors) ? body.errors : [],
      traceId: hasEnvelope ? body.traceId : undefined,
      isNetworkError: false,
      isTimeout: false,
    });
  }

  return new ApiError({
    status: 0,
    errorCode: 'unknown',
    message: 'Something went wrong. Please try again.',
    fieldErrors: [],
    isNetworkError: false,
    isTimeout: false,
  });
}

function defaultMessageFor(status: number): string {
  switch (status) {
    case 400:
      return 'Some of the details are not valid.';
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return 'You do not have permission to do that.';
    case 404:
      return 'We could not find what you were looking for.';
    case 409:
      return 'That conflicts with something that already exists.';
    case 422:
      return 'Some of the details are not valid.';
    case 429:
      return 'Too many attempts. Please wait a moment and try again.';
    default:
      return status >= 500
        ? 'The server had a problem. Please try again shortly.'
        : 'Something went wrong. Please try again.';
  }
}

/**
 * Unwraps the success envelope and returns just the payload.
 *
 * Every API function goes through this, so no caller ever touches
 * `response.data.data` or has to remember to check `success`.
 */
export async function unwrap<T>(promise: Promise<AxiosResponse<ApiEnvelope<T>>>): Promise<T> {
  const response = await promise;
  const body = response.data;

  if (!body || typeof body !== 'object' || !('success' in body)) {
    throw new ApiError({
      status: response.status,
      errorCode: 'malformed_response',
      message: 'The server sent an unexpected response.',
      fieldErrors: [],
      isNetworkError: false,
      isTimeout: false,
    });
  }

  if (!body.success) {
    throw new ApiError({
      status: response.status,
      errorCode: body.errorCode ?? 'error',
      message: body.message,
      fieldErrors: body.errors ?? [],
      traceId: body.traceId,
      isNetworkError: false,
      isTimeout: false,
    });
  }

  return body.data;
}
