import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Tabs, useRouter } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { Platform, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * The five-tab shell.
 *
 * The middle "Add" tab is not a tab at all: it renders as a raised circular
 * button and pushes the add-transaction screen as a modal instead of switching
 * tabs. Adding an expense is a task with a start and an end, not a place in the
 * app you dwell in — routing it as a tab would leave the user stranded on a
 * blank form when they came back later.
 */
export default function TabsLayout() {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: theme.c.primary,
        tabBarInactiveTintColor: theme.c.textTertiary,
        tabBarStyle: {
          backgroundColor: theme.c.surface,
          borderTopColor: theme.c.border,
          borderTopWidth: StyleSheet.hairlineWidth,
          // Gesture-navigation devices need the inset; a 3-button bar does not,
          // and hardcoding either leaves the bar floating or clipped.
          height: 58 + insets.bottom,
          paddingTop: 6,
          paddingBottom: Math.max(insets.bottom, 8),
        },
        tabBarLabelStyle: {
          fontSize: 11,
          fontWeight: '600',
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Home',
          tabBarIcon: ({ color, focused }) => (
            <MaterialCommunityIcons
              name={focused ? 'view-dashboard' : 'view-dashboard-outline'}
              size={24}
              color={color}
            />
          ),
        }}
      />

      <Tabs.Screen
        name="transactions"
        options={{
          title: 'Transactions',
          tabBarIcon: ({ color, focused }) => (
            <MaterialCommunityIcons
              name={focused ? 'format-list-bulleted' : 'format-list-bulleted'}
              size={24}
              color={color}
            />
          ),
        }}
      />

      <Tabs.Screen
        name="add"
        options={{
          title: '',
          tabBarButton: (props) => (
            <AddTabButton
              onPress={() => {
                if (Platform.OS !== 'web') {
                  Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium).catch(() => {});
                }
                router.push('/transaction/new');
              }}
              accessibilityState={props.accessibilityState}
            />
          ),
        }}
        listeners={{
          // Belt and braces: even if the custom button is bypassed (hardware
          // keyboard, accessibility action), navigating INTO this route is
          // intercepted and redirected to the modal.
          tabPress: (event) => {
            event.preventDefault();
            router.push('/transaction/new');
          },
        }}
      />

      <Tabs.Screen
        name="analytics"
        options={{
          title: 'Analytics',
          tabBarIcon: ({ color, focused }) => (
            <MaterialCommunityIcons
              name={focused ? 'chart-donut' : 'chart-donut-variant'}
              size={24}
              color={color}
            />
          ),
        }}
      />

      <Tabs.Screen
        name="profile"
        options={{
          title: 'Profile',
          tabBarIcon: ({ color, focused }) => (
            <MaterialCommunityIcons
              name={focused ? 'account' : 'account-outline'}
              size={24}
              color={color}
            />
          ),
        }}
      />
    </Tabs>
  );
}

function AddTabButton({
  onPress,
  accessibilityState,
}: {
  onPress: () => void;
  accessibilityState?: { selected?: boolean };
}) {
  const theme = useAppTheme();

  return (
    <View style={styles.addContainer}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Add a transaction"
        accessibilityHint="Opens the form to record a new expense or income"
        accessibilityState={accessibilityState}
        onPress={onPress}
        style={({ pressed }) => [
          styles.addButton,
          theme.elevation.medium,
          {
            backgroundColor: theme.c.primary,
            transform: [{ scale: pressed ? 0.94 : 1 }],
          },
        ]}
      >
        <MaterialCommunityIcons name="plus" size={28} color={theme.c.onPrimary} />
      </Pressable>

      <AppText variant="caption" color="textTertiary" style={styles.addLabel}>
        Add
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  addContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'flex-start',
  },
  addButton: {
    width: 52,
    height: 52,
    borderRadius: 26,
    alignItems: 'center',
    justifyContent: 'center',
    // Lifted above the bar so it reads as the primary action rather than one of
    // five equals.
    marginTop: -18,
  },
  addLabel: {
    marginTop: 2,
  },
});
