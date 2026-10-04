import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function AccountsStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="index" />
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
    </Stack>
  );
}
