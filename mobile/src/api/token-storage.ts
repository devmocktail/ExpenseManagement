import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import type { AuthTokens } from '@/types/api';

/**
 * Token persistence.
 *
 * Tokens go in Expo SecureStore — Keychain on iOS, EncryptedSharedPreferences
 * on Android — never AsyncStorage, which is a plaintext file that any process
 * with filesystem access on a rooted/jailbroken device can read.
 *
 * `WHEN_UNLOCKED_THIS_DEVICE_ONLY` keeps the tokens off encrypted iCloud
 * backups, so restoring a backup onto a second phone does not clone a live
 * session.
 */

const ACCESS_TOKEN_KEY = 'em.auth.accessToken';
const REFRESH_TOKEN_KEY = 'em.auth.refreshToken';
const ACCESS_EXPIRY_KEY = 'em.auth.accessExpiresAt';
const REFRESH_EXPIRY_KEY = 'em.auth.refreshExpiresAt';

const OPTIONS: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

/**
 * SecureStore has no web implementation. Web is not a shipping target for this
 * app, but Metro still bundles for it during development, so the calls degrade
 * to an in-memory map instead of throwing on import.
 */
const memoryFallback = new Map<string, string>();
const isWeb = Platform.OS === 'web';

async function setItem(key: string, value: string): Promise<void> {
  if (isWeb) {
    memoryFallback.set(key, value);
    return;
  }
  await SecureStore.setItemAsync(key, value, OPTIONS);
}

async function getItem(key: string): Promise<string | null> {
  if (isWeb) return memoryFallback.get(key) ?? null;
  try {
    return await SecureStore.getItemAsync(key, OPTIONS);
  } catch {
    // A corrupted keychain entry (seen after some OS upgrades) throws rather
    // than returning null. Treat it as "no session" so the app recovers by
    // asking the user to sign in again instead of crashing on launch.
    return null;
  }
}

async function deleteItem(key: string): Promise<void> {
  if (isWeb) {
    memoryFallback.delete(key);
    return;
  }
  try {
    await SecureStore.deleteItemAsync(key, OPTIONS);
  } catch {
    // Deleting a key that is not there is not an error worth surfacing.
  }
}

export type StoredTokens = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export const tokenStorage = {
  async save(tokens: AuthTokens): Promise<void> {
    await Promise.all([
      setItem(ACCESS_TOKEN_KEY, tokens.accessToken),
      setItem(REFRESH_TOKEN_KEY, tokens.refreshToken),
      setItem(ACCESS_EXPIRY_KEY, tokens.accessTokenExpiresAt),
      setItem(REFRESH_EXPIRY_KEY, tokens.refreshTokenExpiresAt),
    ]);
  },

  async load(): Promise<StoredTokens | null> {
    const [accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt] =
      await Promise.all([
        getItem(ACCESS_TOKEN_KEY),
        getItem(REFRESH_TOKEN_KEY),
        getItem(ACCESS_EXPIRY_KEY),
        getItem(REFRESH_EXPIRY_KEY),
      ]);

    if (!accessToken || !refreshToken) return null;

    return {
      accessToken,
      refreshToken,
      accessTokenExpiresAt: accessTokenExpiresAt ?? '',
      refreshTokenExpiresAt: refreshTokenExpiresAt ?? '',
    };
  },

  async getAccessToken(): Promise<string | null> {
    return getItem(ACCESS_TOKEN_KEY);
  },

  async getRefreshToken(): Promise<string | null> {
    return getItem(REFRESH_TOKEN_KEY);
  },

  async clear(): Promise<void> {
    await Promise.all([
      deleteItem(ACCESS_TOKEN_KEY),
      deleteItem(REFRESH_TOKEN_KEY),
      deleteItem(ACCESS_EXPIRY_KEY),
      deleteItem(REFRESH_EXPIRY_KEY),
    ]);
  },
};

/**
 * True when the refresh token is expired or absent, i.e. the session cannot be
 * recovered and the user must sign in again.
 *
 * The 30-second skew allows for a slow request and a phone clock that is a
 * little ahead of the server's.
 */
export function isRefreshTokenUsable(tokens: StoredTokens | null): boolean {
  if (!tokens?.refreshToken) return false;
  if (!tokens.refreshTokenExpiresAt) return true;

  const expiry = Date.parse(tokens.refreshTokenExpiresAt);
  if (Number.isNaN(expiry)) return true;

  return expiry - 30_000 > Date.now();
}
