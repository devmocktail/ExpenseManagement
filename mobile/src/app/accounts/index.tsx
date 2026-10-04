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

  /**
   * Summed only across accounts sharing one currency.
   *
   * Adding a USD balance to an INR one would produce a number that means
   * nothing, so a mixed set shows no total rather than a confident wrong one.
   */
  const total = useMemo(() => {
    if (active.length === 0) return null;
    const currencies = new Set(active.map((a) => a.currencyCode));
    if (currencies.size !== 1) return null;
    return {
      amount: active.reduce((sum, a) => sum + a.balance, 0),
      currencyCode: active[0].currencyCode,
    };
  }, [active]);

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
          {total ? (
            <AppCard>
              <AppText variant="caption" color="textSecondary">
                Across all accounts
              </AppText>
              <AppText variant="heading1" style={{ marginTop: 4 }}>
                {formatCurrency(total.amount, total.currencyCode)}
              </AppText>
            </AppCard>
          ) : null}

          <View style={{ gap: theme.spacing.sm }}>
            {active.map((account) => (
              <AccountCard
                key={account.id}
                account={account}
                onPress={() => router.push(`/accounts/${account.id}`)}
              />
            ))}
          </View>

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
