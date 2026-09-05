import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { useCategories } from '@/features/categories/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Category, TransactionType } from '@/types/api';

/**
 * Category management.
 *
 * Expense and income categories are two separate lists, not one list with a
 * badge on each row: a category only ever applies to one side of the ledger, so
 * interleaving them would put twenty rows between the user and the one they
 * came to edit.
 */
export default function CategoryListScreen() {
  const theme = useAppTheme();
  const router = useRouter();

  const [type, setType] = useState<TransactionType>('Expense');
  const [refreshing, setRefreshing] = useState(false);

  const { data, isPending, isError, error, refetch } = useCategories(type);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title="Categories"
        onBack={() => router.back()}
        actions={[
          {
            icon: 'plus',
            label: 'Add a category',
            onPress: () => router.push({ pathname: '/category/new', params: { type } }),
          },
        ]}
      />

      <View style={{ padding: theme.spacing.base, paddingBottom: theme.spacing.sm }}>
        <SegmentedControl
          value={type}
          onChange={(next) => setType(next as TransactionType)}
          options={[
            { value: 'Expense', label: 'Expense' },
            { value: 'Income', label: 'Income' },
          ]}
        />
      </View>

      {isError ? (
        <AppErrorState
          title="Could not load your categories"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ paddingHorizontal: theme.spacing.base }}>
          <ListSkeleton count={7} />
        </View>
      ) : !data || data.length === 0 ? (
        <AppEmptyState
          title={type === 'Income' ? 'No income categories' : 'No expense categories'}
          description={
            type === 'Income'
              ? 'Add one so you can tell a salary apart from a refund.'
              : 'Add one so your spending lands somewhere more useful than “Other”.'
          }
          actionLabel="Create a category"
          onAction={() => router.push({ pathname: '/category/new', params: { type } })}
          icon={
            <MaterialCommunityIcons name="shape-outline" size={44} color={theme.c.textTertiary} />
          }
        />
      ) : (
        <ScrollView
          contentContainerStyle={{
            padding: theme.spacing.base,
            paddingTop: theme.spacing.sm,
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
          <AppCard padding="none">
            {data.map((category, index) => (
              <CategoryRow
                key={category.id}
                category={category}
                first={index === 0}
                onPress={() => router.push(`/category/${category.id}`)}
              />
            ))}
          </AppCard>

          <AppText variant="caption" color="textTertiary" align="center">
            Categories marked Default come with the app and cannot be deleted, but you can rename
            and restyle them.
          </AppText>
        </ScrollView>
      )}
    </View>
  );
}

function CategoryRow({
  category,
  first,
  onPress,
}: {
  category: Category;
  first: boolean;
  onPress: () => void;
}) {
  const theme = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      // The "Default" chip is invisible to a screen reader, so the constraint it
      // communicates is spelled out in the row's label instead.
      accessibilityLabel={`${category.name}, ${countLabel(category.transactionCount)}${
        category.isSystem ? ', a default category that cannot be deleted' : ''
      }`}
      accessibilityHint="Opens this category to edit it"
      onPress={onPress}
      style={({ pressed }) => (pressed ? { backgroundColor: theme.c.surfaceSunken } : null)}
    >
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
        <CategoryIcon icon={category.icon} color={category.color} />

        <View style={styles.flex}>
          <View style={[styles.nameRow, { gap: theme.spacing.sm }]}>
            <AppText variant="bodyStrong" numberOfLines={1} style={styles.shrink}>
              {category.name}
            </AppText>

            {category.isSystem ? <DefaultChip /> : null}
          </View>

          <AppText variant="caption" color="textSecondary">
            {countLabel(category.transactionCount)}
          </AppText>
        </View>

        <MaterialCommunityIcons name="chevron-right" size={20} color={theme.c.textTertiary} />
      </View>
    </Pressable>
  );
}

/**
 * Marks a category the user did not create.
 *
 * Shown on the list rather than only on the detail screen so the rule is
 * legible before a tap — discovering that a delete is unavailable only after
 * navigating in is how a constraint reads as a bug.
 */
function DefaultChip() {
  const theme = useAppTheme();

  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.spacing.xs,
        paddingHorizontal: theme.spacing.sm,
        paddingVertical: theme.spacing.xxs,
        borderRadius: theme.radius.pill,
        backgroundColor: theme.c.surfaceSunken,
        borderWidth: 1,
        borderColor: theme.c.border,
      }}
    >
      <MaterialCommunityIcons name="lock-outline" size={11} color={theme.c.textTertiary} />
      <AppText variant="caption" color="textTertiary">
        Default
      </AppText>
    </View>
  );
}

function countLabel(count: number): string {
  if (count === 0) return 'No transactions';
  return `${count} ${count === 1 ? 'transaction' : 'transactions'}`;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  shrink: { flexShrink: 1 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
