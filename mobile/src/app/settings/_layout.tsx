import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * The settings stack.
 *
 * Every screen in here draws its own <ScreenHeader>, so the navigator's header
 * is off — leaving it on is how you end up with two stacked titles that use
 * different type scales.
 */
export default function SettingsStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="profile" />
      <Stack.Screen name="password" />
      <Stack.Screen name="currency" />
      <Stack.Screen name="export" />
      <Stack.Screen name="delete-account" />
      <Stack.Screen name="privacy" />
      <Stack.Screen name="terms" />
    </Stack>
  );
}
