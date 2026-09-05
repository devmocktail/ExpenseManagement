import { Stack } from 'expo-router';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function CategoryStackLayout() {
  const theme = useAppTheme();

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.c.background },
      }}
    >
      <Stack.Screen name="index" />

      {/* Creating is a short task the user backs out of, so it presents modally
          and can be swiped away. Editing is a place they navigate to from a row,
          so it pushes and keeps the list behind it in the back stack. */}
      <Stack.Screen name="new" options={{ presentation: 'modal', animation: 'slide_from_bottom' }} />
      <Stack.Screen name="[id]" />
    </Stack>
  );
}
