import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { config } from '@/constants/config';
import type { ThemePreference } from '@/types/api';

/**
 * Device-local preferences.
 *
 * These are deliberately kept out of the auth/session state: theme and
 * onboarding status must survive a logout, and must be readable before the
 * first network call so the app never flashes the wrong theme at launch.
 *
 * AsyncStorage (not SecureStore) is correct here — nothing in this store is a
 * secret, and SecureStore's per-key encryption would add latency to a read on
 * the critical path of the very first render.
 */

export type ThemeMode = 'system' | 'light' | 'dark';

type PreferencesState = {
  theme: ThemeMode;
  /** Locally chosen currency. Overwritten by the server's value once signed in. */
  currencyCode: string;
  hasCompletedOnboarding: boolean;
  /** True once the persisted state has been read from disk. */
  hydrated: boolean;

  setTheme: (theme: ThemeMode) => void;
  setCurrency: (code: string) => void;
  completeOnboarding: () => void;
  resetOnboarding: () => void;
  setHydrated: (value: boolean) => void;
};

/** Maps the server's PascalCase preference onto the local lowercase union. */
export function toThemeMode(preference: ThemePreference | undefined | null): ThemeMode {
  switch (preference) {
    case 'Light':
      return 'light';
    case 'Dark':
      return 'dark';
    default:
      return 'system';
  }
}

export function toThemePreference(mode: ThemeMode): ThemePreference {
  switch (mode) {
    case 'light':
      return 'Light';
    case 'dark':
      return 'Dark';
    default:
      return 'System';
  }
}

export const usePreferencesStore = create<PreferencesState>()(
  persist(
    (set) => ({
      theme: 'system',
      currencyCode: config.defaultCurrency,
      hasCompletedOnboarding: false,
      hydrated: false,

      setTheme: (theme) => set({ theme }),
      setCurrency: (currencyCode) => set({ currencyCode }),
      completeOnboarding: () => set({ hasCompletedOnboarding: true }),
      resetOnboarding: () => set({ hasCompletedOnboarding: false }),
      setHydrated: (hydrated) => set({ hydrated }),
    }),
    {
      name: 'em.preferences',
      storage: createJSONStorage(() => AsyncStorage),
      // `hydrated` is runtime-only: persisting it would make the app believe it
      // had already loaded before it actually had.
      partialize: ({ theme, currencyCode, hasCompletedOnboarding }) => ({
        theme,
        currencyCode,
        hasCompletedOnboarding,
      }),
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
    },
  ),
);
