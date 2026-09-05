import { apiClient, SKIP_AUTH, unwrap } from './client';
import type {
  ApiEnvelope,
  AuthResponse,
  AuthTokens,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
} from '@/types/api';

/**
 * Authentication endpoints.
 *
 * Login, register, refresh and the password-reset pair carry the `SKIP_AUTH`
 * marker: they must not send a stale bearer token, because a 401 on one of them
 * would trip the interceptor's refresh path and turn a simple "wrong password"
 * into a logout.
 */

const anonymous = { headers: { [SKIP_AUTH]: '1' } };

export const authApi = {
  register: (body: RegisterRequest): Promise<AuthResponse> =>
    unwrap(apiClient.post<ApiEnvelope<AuthResponse>>('/auth/register', body, anonymous)),

  login: (body: LoginRequest): Promise<AuthResponse> =>
    unwrap(apiClient.post<ApiEnvelope<AuthResponse>>('/auth/login', body, anonymous)),

  refresh: (refreshToken: string): Promise<AuthTokens> =>
    unwrap(apiClient.post<ApiEnvelope<AuthTokens>>('/auth/refresh', { refreshToken }, anonymous)),

  /**
   * Revokes the refresh-token family server-side. Fire-and-forget from the UI's
   * point of view: local tokens are cleared regardless, so a logout still works
   * with no connection.
   */
  logout: (refreshToken: string): Promise<void> =>
    unwrap(apiClient.post<ApiEnvelope<void>>('/auth/logout', { refreshToken })),

  forgotPassword: (body: ForgotPasswordRequest): Promise<void> =>
    unwrap(apiClient.post<ApiEnvelope<void>>('/auth/forgot-password', body, anonymous)),

  resetPassword: (body: ResetPasswordRequest): Promise<void> =>
    unwrap(apiClient.post<ApiEnvelope<void>>('/auth/reset-password', body, anonymous)),

  changePassword: (body: ChangePasswordRequest): Promise<void> =>
    unwrap(apiClient.put<ApiEnvelope<void>>('/profile/password', body)),
};
