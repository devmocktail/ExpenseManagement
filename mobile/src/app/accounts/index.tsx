import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { useAccounts } from '@/features/accounts/hooks';
import { computeNetWorth, groupAccounts } from '@/features/accounts/summary';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Account } from '@/types/api';
import { formatCurrency } from '@/utils/currency';

/**
 * The accounts screen: every pot, its balance, and the total across them.
 *
 * Archived accounts are shown in their own collapsed section rather than mixed
 * in. They exist to keep history intact, and listing them alongside live
 * accounts would make the balances above look wrong.
 */
export default function AccountsScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const [refreshing, setRefreshing] = useState(false);

  const { data: accounts = [], isPending, isError, error, refetch } = useAccounts(true);

  const active = useMemo(() => accounts.filter((a) => !a.isArchived), [accounts]);
  const archived = useMemo(() => accounts.filter((a) => a.isArchived), [accounts]);

  // Archived accounts are excluded from both: they are kept so history stays
  // intact, not because the user still holds that money.
  const netWorth = useMemo(() => computeNetWorth(active), [active]);
  const groups = useMemo(() => groupAccounts(active), [active]);

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title="Accounts"
        onBack={() => router.back()}
        actions={[
          {
            icon: 'plus',
            label: 'Add an account',
            onPress: () => router.push('/accounts/new'),
          },
        ]}
      />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ paddingHorizontal: theme.spacing.base }}>
          <ListSkeleton count={4} />
        </View>
      ) : accounts.length === 0 ? (
        <AppEmptyState
          title="No accounts yet"
          description="Add the places your money sits — a bank account, a card, a wallet — and you can track what each one holds."
          actionLabel="Add an account"
          onAction={() => router.push('/accounts/new')}
        />
      ) : (
        <ScrollView
          contentContainerStyle={{ padding: theme.spacing.base, gap: theme.spacing.base }}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={async () => {
                setRefreshing(true);
                try {
                  await refetch();
                } finally {
                  setRefreshing(false);
                }
              }}
              tintColor={theme.c.primary}
            />
          }
        >
          {netWorth ? (
            <AppCard>
              <View style={styles.netWorthRow}>
                <NetWorthFigure
                  label="Assets"
                  amount={netWorth.assets}
                  currencyCode={netWorth.currencyCode}
                  color={theme.c.income}
                />
                <NetWorthFigure
                  label="Liabilities"
                  amount={netWorth.liabilities}
                  currencyCode={netWorth.currencyCode}
                  color={theme.c.error}
                />
                <NetWorthFigure
                  label="Total"
                  amount={netWorth.total}
                  currencyCode={netWorth.currencyCode}
                  // Neutral unless the user is actually underwater, where the
                  // number is the one thing on the screen worth noticing.
                  color={netWorth.total < 0 ? theme.c.error : theme.c.textPrimary}
                  emphasis
                />
              </View>
            </AppCard>
          ) : null}

          {groups.map((group) => (
            <View key={group.type} style={{ gap: theme.spacing.sm }}>
              <View style={styles.groupHeader}>
                <AppText variant="overline" color="textSecondary">
                  {group.label.toUpperCase()}
                </AppText>
                {group.subtotal ? (
                  <AppText variant="bodySmallStrong" color="textSecondary">
                    {formatCurrency(group.subtotal.amount, group.subtotal.currencyCode)}
                  </AppText>
                ) : null}
              </View>

              {group.accounts.map((account) => (
                <AccountCard
                  key={account.id}
                  account={account}
                  onPress={() => router.push(`/accounts/${account.id}`)}
                />
              ))}
            </View>
          ))}

          {archived.length > 0 ? (
            <View style={{ gap: theme.spacing.sm }}>
              <AppText variant="overline" color="textSecondary">
                ARCHIVED
              </AppText>
              {archived.map((account) => (
                <AccountCard
                  key={account.id}
                  account={account}
                  onPress={() => router.push(`/accounts/${account.id}`)}
                />
              ))}
            </View>
          ) : null}
        </ScrollView>
      )}
    </View>
  );
}

function NetWorthFigure({
  label,
  amount,
  currencyCode,
  color,
  emphasis = false,
}: {
  label: string;
  amount: number;
  currencyCode: string;
  color: string;
  emphasis?: boolean;
}) {
  const theme = useAppTheme();

  return (
    <View style={styles.netWorthCell}>
      <AppText variant="caption" color="textSecondary" numberOfLines={1}>
        {label}
      </AppText>
      <AppText
        variant={emphasis ? 'bodyStrong' : 'body'}
        numberOfLines={1}
        // Shrinks rather than wraps or clips: six-figure balances are ordinary
        // here, and three of them share one row on a 360pt phone.
        adjustsFontSizeToFit
        minimumFontScale={0.7}
        style={{ color, marginTop: 2 }}
      >
        {formatCurrency(amount, currencyCode)}
      </AppText>
    </View>
  );
}

function AccountCard({ account, onPress }: { account: Account; onPress: () => void }) {
  const theme = useAppTheme();

  const subtitle = [account.institution, account.last4 ? `•••• ${account.last4}` : null]
    .filter(Boolean)
    .join(' · ');

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${account.name}, ${formatCurrency(account.balance, account.currencyCode)}${account.isDefault ? ', default account' : ''}`}
      onPress={onPress}
      style={({ pressed }) => [
        styles.card,
        {
          padding: theme.spacing.base,
          borderRadius: theme.radius.large,
          backgroundColor: pressed ? theme.c.surfaceSunken : theme.c.surface,
          borderColor: theme.c.border,
          gap: theme.spacing.md,
          opacity: account.isArchived ? 0.6 : 1,
        },
      ]}
    >
      <View
        style={[
          styles.icon,
          { backgroundColor: account.color + '22', borderRadius: theme.radius.medium },
        ]}
      >
        <MaterialCommunityIcons
          name={account.icon as React.ComponentProps<typeof MaterialCommunityIcons>['name']}
          size={22}
          color={account.color}
        />
      </View>

      <View style={styles.body}>
        <View style={[styles.titleRow, { gap: theme.spacing.xs }]}>
          <AppText variant="bodyStrong" numberOfLines={1}>
            {account.name}
          </AppText>
          {account.isDefault ? (
            <View
              style={{
                paddingHorizontal: 6,
                paddingVertical: 1,
                borderRadius: theme.radius.small,
                backgroundColor: theme.c.primaryMuted,
              }}
            >
              <AppText variant="caption" style={{ color: theme.c.primary, fontSize: 10 }}>
                DEFAULT
              </AppText>
            </View>
          ) : null}
        </View>

        {subtitle ? (
          <AppText variant="caption" color="textSecondary" numberOfLines={1}>
            {subtitle}
          </AppText>
        ) : null}
      </View>

      <AppText
        variant="bodyStrong"
        // A negative balance is normal on a credit card, so it is coloured only
        // when it is actually negative rather than by account type.
        style={{ color: account.balance < 0 ? theme.c.error : theme.c.textPrimary }}
      >
        {formatCurrency(account.balance, account.currencyCode)}
      </AppText>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  netWorthRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  netWorthCell: {
    flex: 1,
    // flexShrink lets a long figure compress instead of pushing its neighbour
    // off the card, which is what happens when three currency strings share a
    // fixed row.
    flexShrink: 1,
    paddingRight: 8,
  },
  groupHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
  },
  icon: {
    width: 42,
    height: 42,
    alignItems: 'center',
    justifyContent: 'center',
  },
  body: { flex: 1 },
  titleRow: { flexDirection: 'row', alignItems: 'center' },
});
