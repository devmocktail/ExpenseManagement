import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppLoader } from '@/components/ui/StateViews';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * The "Add" tab route — a safety net, not a screen.
 *
 * If you are here to change how adding a transaction looks or behaves, this is
 * the wrong file: you want `src/app/transaction/new.tsx`.
 *
 * This file exists only because `(tabs)/_layout.tsx` declares
 * `<Tabs.Screen name="add" />` and expo-router throws when a declared tab has
 * no matching route file. Users never see it: the tab renders a custom raised
 * button that pushes `/transaction/new`, and the `tabPress` listener calls
 * `preventDefault()` and pushes the same route for anyone who reaches the tab
 * another way (hardware keyboard, screen-reader activation).
 *
 * The redirect below covers the one remaining path — navigating directly to
 * `/(tabs)/add`, e.g. from a stale deep link — so that route still lands on the
 * form instead of an empty tab.
 */
export default function AddTabRedirectScreen() {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();

  useEffect(() => {
    // `replace`, not `push`: this route must not survive in the history, or
    // going back from the form would bounce the user straight through it again.
    router.replace('/transaction/new');
  }, [router]);

  return (
    <View
      style={{
        flex: 1,
        backgroundColor: theme.c.background,
        paddingTop: insets.top,
        paddingBottom: insets.bottom,
      }}
    >
      <AppLoader label="Opening…" />
    </View>
  );
}
