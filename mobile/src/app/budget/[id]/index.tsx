import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { subDays } from 'date-fns';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { BudgetProgress } from '@/components/BudgetProgress';
import { CategoryIcon } from '@/components/CategoryIcon';
import { CurrencyText } from '@/components/CurrencyText';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { SummaryCardSkeleton } from '@/components/ui/Skeleton';
import { AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { useBudget, useDeleteBudget } from '@/features/budgets/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatDate, formatDaysRemaining, parseServerDate } from '@/utils/date';

/**
 * A single budget.
 *
 * The progress bar is the whole point of the screen, so it gets the top of the
 * page at full size; the window, the period and the days left are the details
 * that explain the bar, and they sit underneath it in that order.
 */
export default function BudgetDetailScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: budget, isPending, isError, error, refetch } = useBudget(id);
  const remove = useDeleteBudget();

  const [confirmDelete, setConfirmDelete] = useState(false);

  if (isPending) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Budget" onBack={() => router.back()} />
        <View style={{ padding: theme.spacing.base, gap: theme.spacing.base }}>
          <SummaryCardSkeleton />
          <SummaryCardSkeleton />
        </View>
      </View>
    );
  }

  if (isError || !budget) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Budget" onBack={() => router.back()} />
        <AppErrorState
          title="Could not load this budget"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      </View>
    );
  }

  const end = parseServerDate(budget.endDate);
  // endDate is exclusive, so the last day a purchase still counts is the day
  // before it. Showing the raw value reads as a window one day too long.
  const lastDay = end ? subDays(end, 1) : null;
  const window = lastDay ? `${formatDate(budget.startDate)} – ${formatDate(lastDay)}` : '';

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title={budget.name}
        subtitle={budget.categoryName ?? 'All categories'}
        onBack={() => router.back()}
        actions={[
          {
            icon: 'pencil-outline',
            label: 'Edit budget',
            onPress: () => router.push(`/budget/${budget.id}/edit`),
          },
          {
            icon: 'trash-can-outline',
            label: 'Delete budget',
            onPress: () => setConfirmDelete(true),
            destructive: true,
          },
        ]}
      />

      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          gap: theme.spacing.base,
          paddingBottom: theme.spacing.xxxl,
        }}
        showsVerticalScrollIndicator={false}
      >
        <AppCard elevation="medium">
          <View style={[styles.cardHeader, { gap: theme.spacing.md }]}>
            {budget.categoryId ? (
              <CategoryIcon icon={budget.categoryIcon} color={budget.categoryColor} size="small" />
            ) : (
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
            )}

            <View style={styles.flex}>
              <AppText variant="heading3" numberOfLines={1}>
                {budget.name}
              </AppText>
              <AppText variant="caption" color="textSecondary" numberOfLines={1}>
                {budget.categoryName ?? 'All categories'} · {budget.period}
              </AppText>
            </View>
          </View>

          <BudgetProgress
            spent={budget.spent}
            budget={budget.amount}
            percentage={budget.percentageUsed}
            status={budget.status}
            currencyCode={budget.currencyCode}
            meta={formatDaysRemaining(budget.endDate)}
            style={{ marginTop: theme.spacing.base }}
          />
        </AppCard>

        <AppCard>
          <AppText variant="caption" color="textSecondary">
            {budget.remaining < 0 ? 'Over budget by' : 'Left to spend'}
          </AppText>

          <CurrencyText
            amount={Math.abs(budget.remaining)}
            currencyCode={budget.currencyCode}
            variant="heading1"
            color={budget.remaining < 0 ? 'error' : 'textPrimary'}
            style={{ marginTop: 2 }}
            numberOfLines={1}
          />

          <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.xs }}>
            {formatDaysRemaining(budget.endDate)}
          </AppText>
        </AppCard>

        <AppCard padding="none">
          <DetailRow icon="calendar-range" label="Window" index={0}>
            <AppText variant="bodySmall">{window}</AppText>
          </DetailRow>

          <DetailRow icon="autorenew" label="Period" index={1}>
            <AppText variant="bodySmall">
              {budget.period}
              {budget.isRecurring ? ' · repeats' : ''}
            </AppText>
          </DetailRow>

          <DetailRow icon="clock-outline" label="Time left" index={2}>
            <AppText variant="bodySmall">{formatDaysRemaining(budget.endDate)}</AppText>
          </DetailRow>

          <DetailRow icon="cash-multiple" label="Spent" index={3}>
            <CurrencyText
              amount={budget.spent}
              currencyCode={budget.currencyCode}
              variant="bodySmall"
            />
          </DetailRow>

          <DetailRow icon="target" label="Limit" index={4}>
            <CurrencyText
              amount={budget.amount}
              currencyCode={budget.currencyCode}
              variant="bodySmall"
            />
          </DetailRow>
        </AppCard>

        {!budget.isActive ? (
          <View
            style={{
              backgroundColor: theme.c.surfaceSunken,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
              flexDirection: 'row',
              alignItems: 'center',
              gap: theme.spacing.sm,
            }}
          >
            <MaterialCommunityIcons
              name="pause-circle-outline"
              size={16}
              color={theme.c.textTertiary}
            />
            <AppText variant="caption" color="textTertiary" style={styles.flex}>
              This budget is paused, so it is not tracking new spending.
            </AppText>
          </View>
        ) : null}

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Edit this budget"
          onPress={() => router.push(`/budget/${budget.id}/edit`)}
          style={{ alignSelf: 'center', paddingVertical: theme.spacing.sm }}
          hitSlop={8}
        >
          <AppText variant="bodySmallStrong" color="primary">
            Edit budget
          </AppText>
        </Pressable>
      </ScrollView>

      <ConfirmDialog
        visible={confirmDelete}
        title="Delete budget?"
        message="Your transactions stay exactly as they are — only this limit and its tracking are removed."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(budget.id);
            setConfirmDelete(false);
            toast.show({ title: 'Budget deleted', tone: 'success' });
            router.back();
          } catch (deleteError) {
            setConfirmDelete(false);
            toast.show({
              title: 'Could not delete',
              description: deleteError instanceof Error ? deleteError.message : undefined,
              tone: 'error',
            });
          }
        }}
      />
    </View>
  );
}

function DetailRow({
  icon,
  label,
  index,
  children,
}: {
  icon: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  label: string;
  index: number;
  children: React.ReactNode;
}) {
  const theme = useAppTheme();

  return (
    <View
      style={[
        styles.detailRow,
        {
          padding: theme.spacing.base,
          gap: theme.spacing.md,
          borderTopWidth: index === 0 ? 0 : 1,
          borderTopColor: theme.c.border,
        },
      ]}
    >
      <MaterialCommunityIcons
        name={icon}
        size={18}
        color={theme.c.textTertiary}
        accessibilityElementsHidden
      />
      <AppText variant="bodySmall" color="textSecondary" style={{ width: 92 }}>
        {label}
      </AppText>
      <View style={styles.flex}>{children}</View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  detailRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
