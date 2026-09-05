import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { BudgetProgress } from '@/components/BudgetProgress';
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { SummaryCardSkeleton } from '@/components/ui/Skeleton';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { useBudgets } from '@/features/budgets/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatDaysRemaining } from '@/utils/date';

/**
 * Budgets.
 *
 * The overall budget is separated out and shown first: it is the one number
 * people check, and burying it among six category budgets makes it just another
 * row.
 */
export default function BudgetListScreen() {
  const theme = useAppTheme();
  const router = useRouter();

  const [refreshing, setRefreshing] = useState(false);
  const { data, isPending, isError, error, refetch } = useBudgets();

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

  const overall = data?.find((budget) => budget.categoryId === null);
  const categoryBudgets = data?.filter((budget) => budget.categoryId !== null) ?? [];

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title="Budgets"
        onBack={() => router.back()}
        actions={[
          {
            icon: 'plus',
            label: 'Create a budget',
            onPress: () => router.push('/budget/new'),
          },
        ]}
      />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ padding: theme.spacing.base, gap: theme.spacing.base }}>
          <SummaryCardSkeleton />
          <SummaryCardSkeleton />
        </View>
      ) : !data || data.length === 0 ? (
        <AppEmptyState
          title="No budgets yet"
          description="Set a monthly limit and we will tell you before you go over it, not after."
          actionLabel="Create your first budget"
          onAction={() => router.push('/budget/new')}
          icon={<MaterialCommunityIcons name="target" size={44} color={theme.c.textTertiary} />}
        />
      ) : (
        <ScrollView
          contentContainerStyle={{
            padding: theme.spacing.base,
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
          {overall ? (
            <AppCard
              elevation="medium"
              onPress={() => router.push(`/budget/${overall.id}`)}
              accessibilityLabel={`Overall budget, ${Math.round(overall.percentageUsed)} percent used`}
            >
              <View style={styles.cardHeader}>
                <View style={styles.flex}>
                  <AppText variant="heading3" numberOfLines={1}>
                    {overall.name}
                  </AppText>
                  <AppText variant="caption" color="textSecondary">
                    All categories · {overall.period}
                  </AppText>
                </View>

                <MaterialCommunityIcons
                  name="chevron-right"
                  size={20}
                  color={theme.c.textTertiary}
                />
              </View>

              <BudgetProgress
                spent={overall.spent}
                budget={overall.amount}
                percentage={overall.percentageUsed}
                status={overall.status}
                currencyCode={overall.currencyCode}
                meta={formatDaysRemaining(overall.endDate)}
                style={{ marginTop: theme.spacing.base }}
              />
            </AppCard>
          ) : (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Create an overall budget"
              onPress={() => router.push('/budget/new')}
              style={{
                borderWidth: 1,
                borderStyle: 'dashed',
                borderColor: theme.c.borderStrong,
                borderRadius: theme.radius.large,
                padding: theme.spacing.base,
                flexDirection: 'row',
                alignItems: 'center',
                gap: theme.spacing.md,
              }}
            >
              <MaterialCommunityIcons name="plus-circle-outline" size={22} color={theme.c.primary} />
              <AppText variant="bodySmall" color="textSecondary" style={styles.flex}>
                Add an overall monthly budget
              </AppText>
            </Pressable>
          )}

          {categoryBudgets.length > 0 ? (
            <>
              <AppText variant="overline" color="textSecondary" style={{ paddingHorizontal: 4 }}>
                BY CATEGORY
              </AppText>

              {categoryBudgets.map((budget) => (
                <AppCard
                  key={budget.id}
                  onPress={() => router.push(`/budget/${budget.id}`)}
                  accessibilityLabel={`${budget.categoryName} budget, ${Math.round(budget.percentageUsed)} percent used`}
                >
                  <View style={[styles.cardHeader, { gap: theme.spacing.md }]}>
                    <CategoryIcon
                      icon={budget.categoryIcon}
                      color={budget.categoryColor}
                      size="small"
                    />

                    <View style={styles.flex}>
                      <AppText variant="bodyStrong" numberOfLines={1}>
                        {budget.categoryName ?? budget.name}
                      </AppText>
                      <AppText variant="caption" color="textTertiary">
                        {formatDaysRemaining(budget.endDate)}
                      </AppText>
                    </View>

                    <MaterialCommunityIcons
                      name="chevron-right"
                      size={20}
                      color={theme.c.textTertiary}
                    />
                  </View>

                  <BudgetProgress
                    spent={budget.spent}
                    budget={budget.amount}
                    percentage={budget.percentageUsed}
                    status={budget.status}
                    currencyCode={budget.currencyCode}
                    dense
                    style={{ marginTop: theme.spacing.md }}
                  />
                </AppCard>
              ))}
            </>
          ) : null}
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
