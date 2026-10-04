import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function TransferStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      {/* Recording a transfer is a task, so it presents modally and can be
          swiped away. Opening an existing one is a place, so it pushes. */}
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]" />
    </Stack>
  );
}
