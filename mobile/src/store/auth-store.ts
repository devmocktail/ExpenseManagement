import { create } from 'zustand';
import { setSessionExpiredHandler } from '@/api/client';
import { isRefreshTokenUsable, tokenStorage } from '@/api/token-storage';
import type { AuthResponse, UserProfile, UserSettings } from '@/types/api';
import { toThemeMode, usePreferencesStore } from './preferences-store';

/**
 * Session state.
 *
 * Holds only what the UI needs to decide *which* screens to show. Tokens
 * themselves never enter this store — they live in SecureStore and are read by
 * the axios interceptor — so a state snapshot in a debugger or a crash report
 * can never leak a credential.
 */

export type AuthStatus =
  /** Still reading the persisted session; show the splash. */
  | 'restoring'
  | 'authenticated'
  | 'unauthenticated';

type AuthState = {
  status: AuthStatus;
  user: UserProfile | null;
  settings: UserSettings | null;

  /** Set when a session ends because the server rejected it rather than by user action. */
  sessionExpiredMessage: string | null;

  signIn: (response: AuthResponse) => Promise<void>;
  signOut: () => Promise<void>;
  restore: () => Promise<void>;
  setUser: (user: UserProfile) => void;
  setSettings: (settings: UserSettings) => void;
  clearSessionExpiredMessage: () => void;
};

export const useAuthStore = create<AuthState>()((set, get) => ({
  status: 'restoring',
  user: null,
  settings: null,
  sessionExpiredMessage: null,

  signIn: async (response) => {
    await tokenStorage.save(response);
    set({
      status: 'authenticated',
      user: response.user,
      sessionExpiredMessage: null,
    });
  },

  signOut: async () => {
    await tokenStorage.clear();
    set({ status: 'unauthenticated', user: null, settings: null });
  },

  /**
   * Decides at launch whether there is a session worth resuming.
   *
   * The access token is intentionally NOT validated here. It is short-lived and
   * usually already expired on a cold start; the refresh token is what
   * determines whether the session is alive, and the interceptor will exchange
   * it on the first real request. Blocking startup on a network round trip
   * would make the app unusable offline.
   */
  restore: async () => {
    const tokens = await tokenStorage.load();

    if (!isRefreshTokenUsable(tokens)) {
      await tokenStorage.clear();
      set({ status: 'unauthenticated', user: null, settings: null });
      return;
    }

    set({ status: 'authenticated' });
  },

  setUser: (user) => set({ user }),

  setSettings: (settings) => {
    set({ settings });
    // The server is authoritative for theme and currency once signed in, so a
    // preference changed on another device follows the user here.
    const prefs = usePreferencesStore.getState();
    prefs.setTheme(toThemeMode(settings.theme));
    prefs.setCurrency(settings.currencyCode);
  },

  clearSessionExpiredMessage: () => set({ sessionExpiredMessage: null }),
}));

/**
 * Connects the axios interceptor's "refresh failed" path to the store.
 *
 * Registered here rather than imported by client.ts because client.ts is
 * imported by this module — going the other way would be a require cycle that
 * Metro resolves to `undefined` at runtime.
 */
setSessionExpiredHandler(() => {
  const { status } = useAuthStore.getState();

  // Only surface the message to someone who believed they were signed in.
  useAuthStore.setState({
    status: 'unauthenticated',
    user: null,
    settings: null,
    sessionExpiredMessage:
      status === 'authenticated' ? 'Your session has expired. Please sign in again.' : null,
  });
});

/** Convenience selector used by screens that must not render without a user. */
export const useCurrentUser = (): UserProfile | null => useAuthStore((s) => s.user);

export const useIsAuthenticated = (): boolean =>
  useAuthStore((s) => s.status === 'authenticated');

/** The currency every amount on screen is formatted in. */
export const useCurrencyCode = (): string =>
  useAuthStore((s) => s.settings?.currencyCode) ?? usePreferencesStore.getState().currencyCode;
