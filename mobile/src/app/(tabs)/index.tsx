import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryDonut } from '@/components/charts/CategoryDonut';
import { TrendChart } from '@/components/charts/TrendChart';
import { CurrencyText } from '@/components/CurrencyText';
import { BudgetProgress } from '@/components/BudgetProgress';
import { TransactionRow } from '@/components/TransactionRow';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { AppErrorState } from '@/components/ui/StateViews';
import { ListSkeleton, Skeleton, SummaryCardSkeleton } from '@/components/ui/Skeleton';
import { useDashboard } from '@/features/dashboard/hooks';
import { useAuthStore } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatMonthYear } from '@/utils/date';

/**
 * The home screen.
 *
 * It answers, in order and without scrolling past the fold: how much do I have,
 * what came in, what went out, and how much of my budget is left. Everything
 * below that is supporting detail.
 *
 * The whole screen is one request — the server assembles it — so there is a
 * single loading state and a single error state rather than six independently
 * flickering cards.
 */
export default function DashboardScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  const user = useAuthStore((s) => s.user);
  const [refreshing, setRefreshing] = useState(false);

  const { data, isPending, isError, error, refetch } = useDashboard();

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

  if (isError) {
    return (
      <View style={[styles.flex, { paddingTop: insets.top, backgroundColor: theme.c.background }]}>
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      </View>
    );
  }

  return (
    <ScrollView
      style={{ backgroundColor: theme.c.background }}
      contentContainerStyle={{
        paddingTop: insets.top + theme.spacing.md,
        paddingHorizontal: theme.spacing.base,
        paddingBottom: theme.spacing.xxxl,
        gap: theme.spacing.base,
      }}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={onRefresh}
          tintColor={theme.c.primary}
          colors={[theme.c.primary]}
        />
      }
      showsVerticalScrollIndicator={false}
    >
      <Header
        name={user?.fullName ?? ''}
        periodLabel={data ? formatMonthYear(data.periodStart) : ''}
        loading={isPending}
        onProfilePress={() => router.push('/(tabs)/profile')}
      />

      {isPending ? (
        <>
          <SummaryCardSkeleton />
          <SummaryCardSkeleton />
        </>
      ) : data ? (
        <>
          <BalanceCard
            balance={data.balance}
            income={data.income}
            expenses={data.expenses}
            currencyCode={data.currencyCode}
          />

          <QuickActions />

          {data.budget ? (
            <AppCard
              onPress={() => router.push('/budget')}
              accessibilityLabel="Monthly budget. Opens budgets."
            >
              <View style={styles.sectionHeader}>
                <AppText variant="heading3">Monthly budget</AppText>
                <MaterialCommunityIcons
                  name="chevron-right"
                  size={20}
                  color={theme.c.textTertiary}
                />
              </View>

              <BudgetProgress
                spent={data.budget.spent}
                budget={data.budget.amount}
                percentage={data.budget.percentage}
                status={data.budget.status}
                currencyCode={data.currencyCode}
                style={{ marginTop: theme.spacing.md }}
              />
            </AppCard>
          ) : (
            <AppCard onPress={() => router.push('/budget/new')} accessibilityLabel="Create a budget">
              <View style={[styles.emptyBudget, { gap: theme.spacing.md }]}>
                <View
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: theme.radius.medium,
                    backgroundColor: theme.c.primaryMuted,
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <MaterialCommunityIcons name="target" size={20} color={theme.c.primary} />
                </View>

                <View style={styles.flex}>
                  <AppText variant="bodyStrong">Set a monthly budget</AppText>
                  <AppText variant="caption" color="textSecondary">
                    Know where you stand before the month ends
                  </AppText>
                </View>

                <MaterialCommunityIcons name="chevron-right" size={20} color={theme.c.textTertiary} />
              </View>
            </AppCard>
          )}

          <AppCard>
            <AppText variant="heading3">This week</AppText>
            <AppText variant="caption" color="textSecondary" style={{ marginTop: 2 }}>
              Daily spending
            </AppText>

            <View style={{ marginTop: theme.spacing.base }}>
              <TrendChart data={data.weeklyTrend} variant="bar" height={160} />
            </View>
          </AppCard>

          <AppCard>
            <View style={styles.sectionHeader}>
              <AppText variant="heading3">Where it went</AppText>
              <Pressable
                accessibilityRole="button"
                onPress={() => router.push('/(tabs)/analytics')}
                hitSlop={8}
              >
                <AppText variant="bodySmallStrong" color="primary">
                  Details
                </AppText>
              </Pressable>
            </View>

            <View style={{ marginTop: theme.spacing.base }}>
              <CategoryDonut
                data={data.categoryBreakdown}
                currencyCode={data.currencyCode}
                total={data.expenses}
              />
            </View>
          </AppCard>

          <AppCard padding="none">
            <View
              style={[
                styles.sectionHeader,
                { padding: theme.spacing.base, paddingBottom: theme.spacing.sm },
              ]}
            >
              <AppText variant="heading3">Recent</AppText>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="View all transactions"
                onPress={() => router.push('/(tabs)/transactions')}
                hitSlop={8}
              >
                <AppText variant="bodySmallStrong" color="primary">
                  View all
                </AppText>
              </Pressable>
            </View>

            {data.recentTransactions.length === 0 ? (
              <View style={{ padding: theme.spacing.xl, alignItems: 'center' }}>
                <AppText variant="bodySmall" color="textSecondary" align="center">
                  No transactions yet this period
                </AppText>
                <Pressable
                  accessibilityRole="button"
                  onPress={() => router.push('/transaction/new')}
                  style={{ marginTop: theme.spacing.md }}
                  hitSlop={8}
                >
                  <AppText variant="bodySmallStrong" color="primary">
                    Add your first expense
                  </AppText>
                </Pressable>
              </View>
            ) : (
              <View style={{ paddingHorizontal: theme.spacing.base }}>
                {data.recentTransactions.map((transaction) => (
                  <TransactionRow
                    key={transaction.id}
                    transaction={transaction}
                    onPress={(t) => router.push(`/transaction/${t.id}`)}
                  />
                ))}
              </View>
            )}
          </AppCard>
        </>
      ) : (
        <ListSkeleton />
      )}
    </ScrollView>
  );
}

function Header({
  name,
  periodLabel,
  loading,
  onProfilePress,
}: {
  name: string;
  periodLabel: string;
  loading: boolean;
  onProfilePress: () => void;
}) {
  const theme = useAppTheme();
  const firstName = name.trim().split(' ')[0] || 'there';

  return (
    <View style={styles.header}>
      <View style={styles.flex}>
        <AppText variant="caption" color="textSecondary">
          {greeting()}
        </AppText>
        <AppText variant="heading2" numberOfLines={1}>
          {firstName}
        </AppText>
        {loading ? (
          <Skeleton width={110} height={12} style={{ marginTop: 4 }} />
        ) : (
          <AppText variant="caption" color="textTertiary" style={{ marginTop: 2 }}>
            {periodLabel}
          </AppText>
        )}
      </View>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Open your profile"
        onPress={onProfilePress}
        style={{
          width: 44,
          height: 44,
          borderRadius: 22,
          backgroundColor: theme.c.primaryMuted,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <AppText variant="bodyStrong" color="primary">
          {initials(name)}
        </AppText>
      </Pressable>
    </View>
  );
}

function BalanceCard({
  balance,
  income,
  expenses,
  currencyCode,
}: {
  balance: number;
  income: number;
  expenses: number;
  currencyCode: string;
}) {
  const theme = useAppTheme();

  return (
    <AppCard elevation="medium">
      <AppText variant="caption" color="textSecondary">
        Balance this period
      </AppText>

      <CurrencyText
        amount={balance}
        currencyCode={currencyCode}
        variant="display"
        // Negative balances are already shown with a minus sign; colouring them
        // red as well would read as an error rather than a fact.
        color={balance < 0 ? 'error' : 'textPrimary'}
        style={{ marginTop: 4 }}
        numberOfLines={1}
      />

      <View
        style={[
          styles.splitRow,
          {
            marginTop: theme.spacing.base,
            paddingTop: theme.spacing.base,
            borderTopWidth: 1,
            borderTopColor: theme.c.border,
          },
        ]}
      >
        <Stat label="Income" amount={income} currencyCode={currencyCode} tone="income" />
        <View style={{ width: 1, backgroundColor: theme.c.border }} />
        <Stat label="Expenses" amount={expenses} currencyCode={currencyCode} tone="expense" />
      </View>
    </AppCard>
  );
}

function Stat({
  label,
  amount,
  currencyCode,
  tone,
}: {
  label: string;
  amount: number;
  currencyCode: string;
  tone: 'income' | 'expense';
}) {
  const theme = useAppTheme();

  return (
    <View style={[styles.flex, { gap: 4 }]}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
        <MaterialCommunityIcons
          name={tone === 'income' ? 'arrow-down-left' : 'arrow-up-right'}
          size={14}
          color={tone === 'income' ? theme.c.income : theme.c.textSecondary}
        />
        <AppText variant="caption" color="textSecondary">
          {label}
        </AppText>
      </View>

      <CurrencyText
        amount={amount}
        currencyCode={currencyCode}
        variant="heading3"
        color={tone === 'income' ? 'income' : 'textPrimary'}
        numberOfLines={1}
      />
    </View>
  );
}

function QuickActions() {
  const theme = useAppTheme();
  const router = useRouter();

  const actions = [
    { icon: 'minus-circle-outline', label: 'Expense', to: '/transaction/new?type=Expense' },
    { icon: 'plus-circle-outline', label: 'Income', to: '/transaction/new?type=Income' },
    { icon: 'camera-outline', label: 'Scan', to: '/transaction/new?scan=1' },
    { icon: 'target', label: 'Budget', to: '/budget/new' },
  ] as const;

  return (
    <View style={[styles.quickActions, { gap: theme.spacing.sm }]}>
      {actions.map((action) => (
        <Pressable
          key={action.label}
          accessibilityRole="button"
          accessibilityLabel={action.label}
          onPress={() => router.push(action.to as never)}
          style={({ pressed }) => [
            styles.quickAction,
            {
              backgroundColor: theme.c.surface,
              borderRadius: theme.radius.medium,
              paddingVertical: theme.spacing.md,
              borderWidth: theme.isDark ? 1 : 0,
              borderColor: theme.c.border,
              opacity: pressed ? theme.opacity.pressed : 1,
            },
            theme.isDark ? null : theme.elevation.low,
          ]}
        >
          <MaterialCommunityIcons name={action.icon} size={22} color={theme.c.primary} />
          <AppText variant="caption" color="textSecondary" style={{ marginTop: 6 }}>
            {action.label}
          </AppText>
        </Pressable>
      ))}
    </View>
  );
}

function greeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  splitRow: {
    flexDirection: 'row',
    alignItems: 'stretch',
    gap: 16,
  },
  quickActions: {
    flexDirection: 'row',
  },
  quickAction: {
    flex: 1,
    alignItems: 'center',
  },
  emptyBudget: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
