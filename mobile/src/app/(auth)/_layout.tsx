import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * The unauthenticated stack.
 *
 * Headers are off because every screen in this group draws its own back
 * affordance and hero copy — a native header would push that copy below the
 * fold on small devices and duplicate the title.
 */
export default function AuthStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="login" />
      <Stack.Screen name="register" />
      <Stack.Screen name="forgot-password" />
      <Stack.Screen name="reset-password" />
    </Stack>
  );
}
