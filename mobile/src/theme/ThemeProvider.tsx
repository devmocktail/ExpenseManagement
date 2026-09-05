// Expo SDK 57 vendors React Navigation inside expo-router and re-exports its
// theming surface, so `@react-navigation/native` is no longer a direct
// dependency. Importing from it here would resolve to a nested copy with a
// different context instance and silently fail to theme anything.
import {
  DarkTheme as NavDarkTheme,
  DefaultTheme as NavLightTheme,
  ThemeProvider as NavigationThemeProvider,
  type Theme as NavigationTheme,
} from 'expo-router';
import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';
import { PaperProvider } from 'react-native-paper';
import { usePreferencesStore } from '@/store/preferences-store';
import { appDarkTheme, appLightTheme, type AppTheme } from './theme';

const AppThemeContext = createContext<AppTheme>(appLightTheme);

/**
 * The single source of truth for "is the app dark right now".
 *
 * Three providers have to agree or the app tears visually on navigation:
 * Paper (component colours), React Navigation (screen background and header),
 * and our own token context. They are all fed from one computed value here.
 */
export function AppThemeProvider({ children }: { children: ReactNode }) {
  const systemScheme = useColorScheme();
  const preference = usePreferencesStore((s) => s.theme);

  const isDark =
    preference === 'dark' || (preference === 'system' && systemScheme === 'dark');

  const theme = isDark ? appDarkTheme : appLightTheme;

  // React Navigation's own theme controls the colour behind a screen during a
  // transition. Left at its default it flashes white when pushing a screen in
  // dark mode — a small thing that reads as a bug.
  const navigationTheme = useMemo<NavigationTheme>(() => {
    const base = isDark ? NavDarkTheme : NavLightTheme;
    return {
      ...base,
      dark: isDark,
      colors: {
        ...base.colors,
        primary: theme.c.primary,
        background: theme.c.background,
        card: theme.c.surface,
        text: theme.c.textPrimary,
        border: theme.c.border,
        notification: theme.c.error,
      },
    };
  }, [isDark, theme]);

  return (
    <AppThemeContext.Provider value={theme}>
      <PaperProvider theme={theme}>
        <NavigationThemeProvider value={navigationTheme}>
          {children}
        </NavigationThemeProvider>
      </PaperProvider>
    </AppThemeContext.Provider>
  );
}

/** Access design tokens. Every component that renders a colour uses this. */
export function useAppTheme(): AppTheme {
  return useContext(AppThemeContext);
}
