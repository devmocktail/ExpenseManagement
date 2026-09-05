import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Switch, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { useToast } from '@/components/ui/Toast';
import { config } from '@/constants/config';
import { useLogout } from '@/features/auth/hooks';
import { useUpdateSettings } from '@/features/profile/hooks';
import { useAuthStore } from '@/store/auth-store';
import { usePreferencesStore, toThemePreference, type ThemeMode } from '@/store/preferences-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import { getCurrencyMeta } from '@/utils/currency';

type Row = {
  icon: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  label: string;
  value?: string;
  onPress?: () => void;
  destructive?: boolean;
  /** Renders a switch instead of a chevron. */
  toggle?: { value: boolean; onChange: (next: boolean) => void };
};

export default function ProfileScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const user = useAuthStore((s) => s.user);
  const settings = useAuthStore((s) => s.settings);

  const themeMode = usePreferencesStore((s) => s.theme);
  const setTheme = usePreferencesStore((s) => s.setTheme);

  const updateSettings = useUpdateSettings();
  const logout = useLogout();

  const [confirmLogout, setConfirmLogout] = useState(false);

  /**
   * Applies a preference locally first, then persists it.
   *
   * The theme has to flip instantly — waiting for a round trip to redraw the UI
   * makes the toggle feel broken. If the save fails the local value is left
   * alone rather than snapped back, because reverting a change the user can
   * plainly see is more confusing than a toast saying it did not sync.
   */
  const changeTheme = async (mode: ThemeMode) => {
    setTheme(mode);

    try {
      await updateSettings.mutateAsync({ theme: toThemePreference(mode) });
    } catch {
      toast.show({
        title: 'Theme saved on this device only',
        description: 'We could not sync it to your account.',
        tone: 'warning',
      });
    }
  };

  const toggleNotification = async (
    key: 'budgetAlertsEnabled' | 'recurringRemindersEnabled' | 'monthlySummaryEnabled',
    value: boolean,
  ) => {
    try {
      await updateSettings.mutateAsync({ [key]: value });
    } catch {
      toast.show({ title: 'Could not save that setting', tone: 'error' });
    }
  };

  const currency = getCurrencyMeta(settings?.currencyCode);

  const sections: { title: string; rows: Row[] }[] = [
    {
      title: 'Account',
      rows: [
        {
          icon: 'account-edit-outline',
          label: 'Edit profile',
          onPress: () => router.push('/settings/profile'),
        },
        {
          icon: 'lock-outline',
          label: 'Change password',
          onPress: () => router.push('/settings/password'),
        },
      ],
    },
    {
      title: 'Preferences',
      rows: [
        {
          icon: 'cash',
          label: 'Currency',
          value: `${currency.code} ${currency.symbol}`,
          onPress: () => router.push('/settings/currency'),
        },
        {
          icon: 'shape-outline',
          label: 'Categories',
          onPress: () => router.push('/category'),
        },
        {
          icon: 'autorenew',
          label: 'Recurring transactions',
          onPress: () => router.push('/recurring'),
        },
        {
          icon: 'target',
          label: 'Budgets',
          onPress: () => router.push('/budget'),
        },
      ],
    },
    {
      title: 'Notifications',
      rows: [
        {
          icon: 'bell-alert-outline',
          label: 'Budget alerts',
          toggle: {
            value: settings?.budgetAlertsEnabled ?? true,
            onChange: (v) => void toggleNotification('budgetAlertsEnabled', v),
          },
        },
        {
          icon: 'calendar-clock',
          label: 'Recurring reminders',
          toggle: {
            value: settings?.recurringRemindersEnabled ?? true,
            onChange: (v) => void toggleNotification('recurringRemindersEnabled', v),
          },
        },
        {
          icon: 'chart-box-outline',
          label: 'Monthly summary',
          toggle: {
            value: settings?.monthlySummaryEnabled ?? true,
            onChange: (v) => void toggleNotification('monthlySummaryEnabled', v),
          },
        },
      ],
    },
    {
      title: 'Data',
      rows: [
        {
          icon: 'download-outline',
          label: 'Export transactions',
          onPress: () => router.push('/settings/export'),
        },
        {
          icon: 'delete-forever-outline',
          label: 'Delete account',
          onPress: () => router.push('/settings/delete-account'),
          destructive: true,
        },
      ],
    },
    {
      title: 'About',
      rows: [
        {
          icon: 'shield-check-outline',
          label: 'Privacy policy',
          onPress: () => router.push('/settings/privacy'),
        },
        {
          icon: 'file-document-outline',
          label: 'Terms of service',
          onPress: () => router.push('/settings/terms'),
        },
        {
          icon: 'information-outline',
          label: 'Version',
          value: config.appVersion,
        },
      ],
    },
  ];

  return (
    <ScrollView
      style={{ backgroundColor: theme.c.background }}
      contentContainerStyle={{
        paddingTop: insets.top + theme.spacing.md,
        paddingHorizontal: theme.spacing.base,
        paddingBottom: theme.spacing.xxxl,
        gap: theme.spacing.base,
      }}
      showsVerticalScrollIndicator={false}
    >
      <AppText variant="heading2">Profile</AppText>

      <AppCard>
        <View style={[styles.identity, { gap: theme.spacing.base }]}>
          <View
            style={{
              width: 60,
              height: 60,
              borderRadius: 30,
              backgroundColor: theme.c.primaryMuted,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <AppText variant="heading2" color="primary">
              {initials(user?.fullName ?? '')}
            </AppText>
          </View>

          <View style={styles.flex}>
            <AppText variant="heading3" numberOfLines={1}>
              {user?.fullName ?? '—'}
            </AppText>
            <AppText variant="bodySmall" color="textSecondary" numberOfLines={1}>
              {user?.email ?? ''}
            </AppText>
          </View>
        </View>
      </AppCard>

      <AppCard>
        <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: theme.spacing.md }}>
          Appearance
        </AppText>

        <SegmentedControl
          value={themeMode}
          onChange={(next) => void changeTheme(next as ThemeMode)}
          options={[
            { value: 'system', label: 'System' },
            { value: 'light', label: 'Light' },
            { value: 'dark', label: 'Dark' },
          ]}
        />
      </AppCard>

      {sections.map((section) => (
        <View key={section.title} style={{ gap: theme.spacing.sm }}>
          <AppText variant="overline" color="textSecondary" style={{ paddingHorizontal: 4 }}>
            {section.title.toUpperCase()}
          </AppText>

          <AppCard padding="none">
            {section.rows.map((row, index) => (
              <SettingRow key={row.label} row={row} first={index === 0} />
            ))}
          </AppCard>
        </View>
      ))}

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Sign out"
        onPress={() => setConfirmLogout(true)}
        style={({ pressed }) => [
          styles.logout,
          {
            backgroundColor: theme.c.surface,
            borderRadius: theme.radius.medium,
            borderWidth: 1,
            borderColor: theme.c.border,
            paddingVertical: theme.spacing.base,
            gap: theme.spacing.sm,
            opacity: pressed ? theme.opacity.pressed : 1,
          },
        ]}
      >
        <MaterialCommunityIcons name="logout" size={18} color={theme.c.error} />
        <AppText variant="bodyStrong" color="error">
          Sign out
        </AppText>
      </Pressable>

      <ConfirmDialog
        visible={confirmLogout}
        title="Sign out?"
        message="You will need to sign in again to see your transactions."
        confirmLabel="Sign out"
        loading={logout.isPending}
        onCancel={() => setConfirmLogout(false)}
        onConfirm={async () => {
          await logout.mutateAsync();
          setConfirmLogout(false);
          router.replace('/(auth)/login');
        }}
      />
    </ScrollView>
  );
}

function SettingRow({ row, first }: { row: Row; first: boolean }) {
  const theme = useAppTheme();

  const content = (
    <View
      style={[
        styles.row,
        {
          padding: theme.spacing.base,
          gap: theme.spacing.md,
          minHeight: theme.hitTarget.comfortable,
          borderTopWidth: first ? 0 : 1,
          borderTopColor: theme.c.border,
        },
      ]}
    >
      <MaterialCommunityIcons
        name={row.icon}
        size={20}
        color={row.destructive ? theme.c.error : theme.c.textSecondary}
      />

      <AppText variant="body" color={row.destructive ? 'error' : 'textPrimary'} style={styles.flex}>
        {row.label}
      </AppText>

      {row.value ? (
        <AppText variant="bodySmall" color="textSecondary">
          {row.value}
        </AppText>
      ) : null}

      {row.toggle ? (
        <Switch
          value={row.toggle.value}
          onValueChange={row.toggle.onChange}
          accessibilityLabel={row.label}
          trackColor={{ false: theme.c.track, true: theme.c.primary }}
          thumbColor={theme.c.surface}
        />
      ) : row.onPress ? (
        <MaterialCommunityIcons name="chevron-right" size={20} color={theme.c.textTertiary} />
      ) : null}
    </View>
  );

  // A row with a switch is not itself pressable — wrapping it would make the
  // whole row toggle, which fires on an accidental brush while scrolling.
  if (!row.onPress || row.toggle) return content;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={row.label}
      onPress={row.onPress}
      style={({ pressed }) => (pressed ? { backgroundColor: theme.c.surfaceSunken } : null)}
    >
      {content}
    </Pressable>
  );
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  identity: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  logout: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
