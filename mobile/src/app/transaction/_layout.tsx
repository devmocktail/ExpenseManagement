import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function TransactionStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      {/* Adding is a task, so it presents modally and dismisses downward —
          which is what a user expects to be able to swipe away. Detail and edit
          are places, so they push. */}
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]/index" />
      <Stack.Screen name="[id]/edit" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
    </Stack>
  );
}
