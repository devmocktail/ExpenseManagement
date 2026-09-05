import Constants from 'expo-constants';

/**
 * Runtime configuration.
 *
 * Nothing in the app reads `process.env` or a literal URL directly — everything
 * comes through here, so pointing a build at staging is an env-file change
 * rather than a code change (Rule 11).
 *
 * Expo inlines `EXPO_PUBLIC_*` variables into the bundle at build time. That
 * makes them readable by anyone with the .ipa/.apk, so this file may hold
 * endpoints and feature flags but MUST NEVER hold a secret. API keys that
 * matter live on the server.
 */

export type Environment = 'development' | 'staging' | 'production';

function readEnv(key: string): string | undefined {
  // Indexing process.env dynamically defeats Expo's build-time inlining, so the
  // values are read from an explicit literal map instead.
  const map: Record<string, string | undefined> = {
    EXPO_PUBLIC_API_BASE_URL: process.env.EXPO_PUBLIC_API_BASE_URL,
    EXPO_PUBLIC_ENV: process.env.EXPO_PUBLIC_ENV,
    EXPO_PUBLIC_API_TIMEOUT_MS: process.env.EXPO_PUBLIC_API_TIMEOUT_MS,
    EXPO_PUBLIC_DEFAULT_CURRENCY: process.env.EXPO_PUBLIC_DEFAULT_CURRENCY,
    EXPO_PUBLIC_SENTRY_DSN: process.env.EXPO_PUBLIC_SENTRY_DSN,
    EXPO_PUBLIC_ENABLE_DEV_TOOLS: process.env.EXPO_PUBLIC_ENABLE_DEV_TOOLS,
  };
  const value = map[key];
  return value === undefined || value === '' ? undefined : value;
}

/**
 * `extra` from app.config.ts is the fallback for values baked at build time by
 * EAS, which does not always surface process.env to the runtime.
 */
const extra = (Constants.expoConfig?.extra ?? {}) as Record<string, unknown>;

function resolve(envKey: string, extraKey: string, fallback: string): string {
  return readEnv(envKey) ?? (extra[extraKey] as string | undefined) ?? fallback;
}

const environment = resolve('EXPO_PUBLIC_ENV', 'environment', 'development') as Environment;

/**
 * Where the API lives when no env file is present.
 *
 * `10.0.2.2` is the Android emulator's alias for the host machine's loopback —
 * `localhost` inside the emulator is the emulator itself, which is the single
 * most common reason a new developer sees "Network Error" on first run. iOS
 * simulators share the host's network stack, so `localhost` is correct there.
 * A physical device needs the machine's LAN IP and must be set explicitly.
 */
const devFallbackBaseUrl = 'http://10.0.2.2:5165';

export const config = {
  environment,
  isDevelopment: environment === 'development',
  isProduction: environment === 'production',

  api: {
    baseUrl: resolve('EXPO_PUBLIC_API_BASE_URL', 'apiBaseUrl', devFallbackBaseUrl).replace(/\/+$/, ''),
    /** Version segment; kept separate so a future /api/v2 is a one-line change. */
    version: 'v1',
    timeoutMs: Number(resolve('EXPO_PUBLIC_API_TIMEOUT_MS', 'apiTimeoutMs', '20000')),
  },

  /**
   * Used only until the server tells us the user's real preference. The app
   * never assumes INR beyond this default (Rule 12).
   */
  defaultCurrency: resolve('EXPO_PUBLIC_DEFAULT_CURRENCY', 'defaultCurrency', 'INR'),

  sentryDsn: readEnv('EXPO_PUBLIC_SENTRY_DSN') ?? (extra.sentryDsn as string | undefined),

  enableDevTools:
    resolve('EXPO_PUBLIC_ENABLE_DEV_TOOLS', 'enableDevTools', environment === 'development' ? 'true' : 'false') ===
    'true',

  /** EAS project id, needed to obtain an Expo push token. */
  easProjectId:
    (Constants.expoConfig?.extra?.eas as { projectId?: string } | undefined)?.projectId ??
    (extra.easProjectId as string | undefined),

  appVersion: Constants.expoConfig?.version ?? '0.0.0',
} as const;

/** Fully-qualified API root, e.g. `https://api.example.com/api/v1`. */
export const apiRoot = `${config.api.baseUrl}/api/${config.api.version}`;
