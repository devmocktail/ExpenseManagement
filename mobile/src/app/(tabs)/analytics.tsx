import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useCallback, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryDonut } from '@/components/charts/CategoryDonut';
import { TrendChart } from '@/components/charts/TrendChart';
import { CategoryIcon } from '@/components/CategoryIcon';
import { CurrencyText } from '@/components/CurrencyText';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { AppErrorState, AppEmptyState } from '@/components/ui/StateViews';
import { SummaryCardSkeleton } from '@/components/ui/Skeleton';
import { useAnalytics } from '@/features/analytics/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { AnalyticsPeriod, CategoryBreakdownItem, SpendingInsight } from '@/types/api';

/**
 * Analytics.
 *
 * Everything here is computed server-side in SQL and arrives as a compact
 * payload — the app never downloads a transaction list to add it up. That is
 * what keeps this screen fast for someone with three years of history.
 */
export default function AnalyticsScreen() {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  const [period, setPeriod] = useState<AnalyticsPeriod>('month');
  const [refreshing, setRefreshing] = useState(false);

  const { data, isPending, isError, error, refetch } = useAnalytics(period);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

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
      <AppText variant="heading2">Analytics</AppText>

      <SegmentedControl
        value={period}
        onChange={(next) => setPeriod(next as AnalyticsPeriod)}
        options={[
          { value: 'week', label: 'Week' },
          { value: 'month', label: 'Month' },
          { value: 'year', label: 'Year' },
        ]}
      />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <>
          <SummaryCardSkeleton />
          <SummaryCardSkeleton />
        </>
      ) : data ? (
        <>
          <AppCard elevation="medium">
            <AppText variant="caption" color="textSecondary">
              {data.summary.periodLabel}
            </AppText>

            <View style={[styles.summaryGrid, { marginTop: theme.spacing.md, gap: theme.spacing.base }]}>
              <SummaryStat
                label="Income"
                amount={data.summary.totalIncome}
                previous={data.summary.previousTotalIncome}
                currencyCode={data.summary.currencyCode}
                tone="income"
                // For income, more is better — an increase is a positive signal.
                higherIsBetter
              />
              <SummaryStat
                label="Expenses"
                amount={data.summary.totalExpenses}
                previous={data.summary.previousTotalExpenses}
                currencyCode={data.summary.currencyCode}
                tone="expense"
              />
            </View>

            <View
              style={{
                marginTop: theme.spacing.base,
                paddingTop: theme.spacing.base,
                borderTopWidth: 1,
                borderTopColor: theme.c.border,
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <View>
                <AppText variant="caption" color="textSecondary">
                  Saved
                </AppText>
                <CurrencyText
                  amount={data.summary.savings}
                  currencyCode={data.summary.currencyCode}
                  variant="heading2"
                  color={data.summary.savings >= 0 ? 'success' : 'error'}
                />
              </View>

              <View style={{ alignItems: 'flex-end' }}>
                <AppText variant="caption" color="textSecondary">
                  Savings rate
                </AppText>
                <AppText
                  variant="heading3"
                  color={data.summary.savingsRate >= 0 ? 'success' : 'error'}
                >
                  {Math.round(data.summary.savingsRate)}%
                </AppText>
              </View>
            </View>
          </AppCard>

          {data.insights.length > 0 ? (
            <View style={{ gap: theme.spacing.sm }}>
              {data.insights.map((insight) => (
                <InsightCard key={insight.id} insight={insight} />
              ))}
            </View>
          ) : null}

          <AppCard>
            <AppText variant="heading3">Spending trend</AppText>
            <View style={{ marginTop: theme.spacing.base }}>
              <TrendChart data={data.trend} variant="line" height={190} />
            </View>
          </AppCard>

          <AppCard>
            <AppText variant="heading3">Income vs expenses</AppText>
            <View style={{ marginTop: theme.spacing.base }}>
              <TrendChart data={data.trend} mode="comparison" variant="bar" height={190} />
            </View>
          </AppCard>

          <AppCard>
            <AppText variant="heading3">By category</AppText>
            <View style={{ marginTop: theme.spacing.base }}>
              <CategoryDonut
                data={data.categoryBreakdown}
                currencyCode={data.summary.currencyCode}
                total={data.summary.totalExpenses}
              />
            </View>
          </AppCard>

          {data.topCategories.length > 0 ? (
            <AppCard>
              <AppText variant="heading3">Top spending</AppText>

              <View style={{ marginTop: theme.spacing.base, gap: theme.spacing.md }}>
                {data.topCategories.map((item, index) => (
                  <TopCategoryRow
                    key={item.categoryId}
                    rank={index + 1}
                    item={item}
                    currencyCode={data.summary.currencyCode}
                  />
                ))}
              </View>
            </AppCard>
          ) : (
            <AppEmptyState
              title="Nothing to analyse yet"
              description="Once you have recorded a few transactions, your patterns will show up here."
            />
          )}
        </>
      ) : null}
    </ScrollView>
  );
}

function SummaryStat({
  label,
  amount,
  previous,
  currencyCode,
  tone,
  higherIsBetter = false,
}: {
  label: string;
  amount: number;
  previous: number;
  currencyCode: string;
  tone: 'income' | 'expense';
  higherIsBetter?: boolean;
}) {
  const theme = useAppTheme();

  // A change from a zero baseline is not a percentage, it is a new thing —
  // rendering "+∞%" or "+100%" there would be noise, so no delta is shown.
  const hasBaseline = previous > 0;
  const delta = hasBaseline ? ((amount - previous) / previous) * 100 : 0;
  const rising = delta > 0;
  const meaningful = hasBaseline && Math.abs(delta) >= 1;

  const good = higherIsBetter ? rising : !rising;

  return (
    <View style={styles.flex}>
      <AppText variant="caption" color="textSecondary">
        {label}
      </AppText>

      <CurrencyText
        amount={amount}
        currencyCode={currencyCode}
        variant="heading3"
        color={tone === 'income' ? 'income' : 'textPrimary'}
        numberOfLines={1}
      />

      {meaningful ? (
        <View style={[styles.delta, { gap: 2 }]}>
          <MaterialCommunityIcons
            name={rising ? 'trending-up' : 'trending-down'}
            size={13}
            color={good ? theme.c.success : theme.c.warning}
          />
          <AppText variant="caption" color={good ? 'success' : 'warning'}>
            {Math.abs(Math.round(delta))}% vs last {label === 'Income' ? 'period' : 'period'}
          </AppText>
        </View>
      ) : (
        <AppText variant="caption" color="textTertiary">
          {hasBaseline ? 'About the same' : 'No comparison yet'}
        </AppText>
      )}
    </View>
  );
}

function InsightCard({ insight }: { insight: SpendingInsight }) {
  const theme = useAppTheme();

  const tone =
    insight.severity === 'positive'
      ? { color: 'success' as const, background: theme.c.successMuted, icon: 'trending-up' as const }
      : insight.severity === 'warning'
        ? { color: 'warning' as const, background: theme.c.warningMuted, icon: 'alert-outline' as const }
        : { color: 'info' as const, background: theme.c.infoMuted, icon: 'information-outline' as const };

  return (
    <View
      style={{
        flexDirection: 'row',
        gap: theme.spacing.md,
        backgroundColor: theme.c.surface,
        borderRadius: theme.radius.medium,
        padding: theme.spacing.md,
        borderWidth: 1,
        borderColor: theme.c.border,
      }}
    >
      <View
        style={{
          width: 32,
          height: 32,
          borderRadius: theme.radius.small,
          backgroundColor: tone.background,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <MaterialCommunityIcons name={tone.icon} size={17} color={theme.c[tone.color]} />
      </View>

      <View style={styles.flex}>
        <AppText variant="bodySmallStrong">{insight.title}</AppText>
        <AppText variant="caption" color="textSecondary" style={{ marginTop: 2 }}>
          {insight.detail}
        </AppText>
      </View>
    </View>
  );
}

function TopCategoryRow({
  rank,
  item,
  currencyCode,
}: {
  rank: number;
  item: CategoryBreakdownItem;
  currencyCode: string;
}) {
  const theme = useAppTheme();

  return (
    <View style={[styles.topRow, { gap: theme.spacing.md }]}>
      <AppText variant="bodySmallStrong" color="textTertiary" style={{ width: 18 }}>
        {rank}
      </AppText>

      <CategoryIcon icon={item.categoryIcon} color={item.categoryColor} size="small" />

      <View style={styles.flex}>
        <AppText variant="bodySmall" numberOfLines={1}>
          {item.categoryName}
        </AppText>

        <View
          style={{
            height: 4,
            borderRadius: 2,
            backgroundColor: theme.c.track,
            marginTop: 6,
            overflow: 'hidden',
          }}
        >
          <View
            style={{
              width: `${Math.min(item.percentage, 100)}%`,
              height: '100%',
              borderRadius: 2,
              backgroundColor: item.categoryColor || theme.c.primary,
            }}
          />
        </View>
      </View>

      <View style={{ alignItems: 'flex-end' }}>
        <CurrencyText amount={item.amount} currencyCode={currencyCode} variant="bodySmallStrong" />
        <AppText variant="caption" color="textTertiary">
          {Math.round(item.percentage)}%
        </AppText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  summaryGrid: {
    flexDirection: 'row',
  },
  delta: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 2,
  },
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
