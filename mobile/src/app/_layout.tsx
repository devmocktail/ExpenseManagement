import { QueryClientProvider } from '@tanstack/react-query';
import { Stack, useRouter, useSegments } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useRef, useState } from 'react';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { createQueryClient } from '@/api/query-client';
import { OfflineBanner } from '@/components/ui/StateViews';
import { ToastProvider } from '@/components/ui/Toast';
import { useNetworkStatus } from '@/hooks/use-network-status';
import { useAuthStore } from '@/store/auth-store';
import { usePreferencesStore } from '@/store/preferences-store';
import { AppThemeProvider, useAppTheme } from '@/theme/ThemeProvider';

// Held until we know where to send the user, so they never see a flash of the
// wrong screen before being redirected.
SplashScreen.preventAutoHideAsync().catch(() => {
  // Already hidden (fast refresh); nothing to do.
});

export default function RootLayout() {
  // Created once and kept in a ref: a client rebuilt on re-render would discard
  // the entire cache every time the theme or auth state changed.
  const queryClientRef = useRef(createQueryClient());

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <QueryClientProvider client={queryClientRef.current}>
          <AppThemeProvider>
            <ToastProvider>
              <RootNavigator />
            </ToastProvider>
          </AppThemeProvider>
        </QueryClientProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}

/**
 * Decides which route group the user belongs in and keeps them there.
 *
 * Doing this with a redirect effect rather than by conditionally rendering two
 * different navigators means the router keeps one consistent history — swapping
 * navigators mid-session drops the back stack and breaks deep links.
 */
function RootNavigator() {
  const theme = useAppTheme();
  const router = useRouter();
  const segments = useSegments();

  const authStatus = useAuthStore((s) => s.status);
  const restore = useAuthStore((s) => s.restore);

  const preferencesHydrated = usePreferencesStore((s) => s.hydrated);
  const hasCompletedOnboarding = usePreferencesStore((s) => s.hasCompletedOnboarding);

  const { isOffline } = useNetworkStatus();
  const [splashHidden, setSplashHidden] = useState(false);

  useEffect(() => {
    void restore();
  }, [restore]);

  // Both the persisted preferences and the stored session have to be read
  // before we can route; acting on either alone would send a returning user to
  // onboarding, or a first-time user straight to login.
  const ready = preferencesHydrated && authStatus !== 'restoring';

  useEffect(() => {
    if (!ready) return;

    const group = segments[0];
    const inAuthGroup = group === '(auth)';
    const inAppGroup = group === '(tabs)' || group === 'transaction' || group === 'budget'
      || group === 'category' || group === 'recurring' || group === 'settings';

    if (authStatus === 'authenticated' && (inAuthGroup || group === undefined)) {
      router.replace('/(tabs)');
      return;
    }

    if (authStatus === 'unauthenticated') {
      if (!hasCompletedOnboarding && group !== 'onboarding') {
        router.replace('/onboarding');
        return;
      }

      if (hasCompletedOnboarding && (inAppGroup || group === undefined)) {
        router.replace('/(auth)/login');
      }
    }
  }, [ready, authStatus, hasCompletedOnboarding, segments, router]);

  useEffect(() => {
    if (ready && !splashHidden) {
      SplashScreen.hideAsync()
        .catch(() => {})
        .finally(() => setSplashHidden(true));
    }
  }, [ready, splashHidden]);

  if (!ready) {
    // Returning null keeps the native splash on screen rather than flashing an
    // empty view behind it.
    return null;
  }

  return (
    <>
      <StatusBar style={theme.isDark ? 'light' : 'dark'} />
      <OfflineBanner visible={isOffline} />

      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: { backgroundColor: theme.c.background },
          animation: 'slide_from_right',
        }}
      >
        <Stack.Screen name="onboarding" />
        <Stack.Screen name="(auth)" />
        <Stack.Screen name="(tabs)" />

        {/* Detail and form routes live outside the tab bar so they present as a
            full-screen push, which is what a task-focused flow should feel like. */}
        <Stack.Screen name="transaction" />
        <Stack.Screen name="budget" />
        <Stack.Screen name="category" />
        <Stack.Screen name="recurring" />
        <Stack.Screen name="settings" />
      </Stack>
    </>
  );
}
