import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function RecurringStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="index" />
      {/* Creating a schedule is a self-contained task, so it presents modally
          and can be swiped away. Editing an existing one is a place you go, so
          it pushes and keeps the list behind it. */}
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]" />
    </Stack>
  );
}
