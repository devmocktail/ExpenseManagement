import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function BudgetStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="index" />
      {/* Creating and editing are tasks, so they present modally and can be
          swiped away. The list and the detail are places, so they push. */}
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]/index" />
      <Stack.Screen
        name="[id]/edit"
        options={{ presentation: 'modal', animation: 'slide_from_bottom' }}
      />
    </Stack>
  );
}
