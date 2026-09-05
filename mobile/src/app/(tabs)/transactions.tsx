import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { TransactionRow } from '@/components/TransactionRow';
import { AppText } from '@/components/ui/AppText';
import { SearchBar } from '@/components/ui/SearchBar';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { FilterSheet, type TransactionFilters } from '@/features/transactions/FilterSheet';
import { flattenPages, useTransactionsInfinite } from '@/features/transactions/hooks';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Transaction, TransactionType } from '@/types/api';
import { formatRelativeDayHeading, toDayKey } from '@/utils/date';

type TabValue = 'all' | 'Expense' | 'Income';

/**
 * The transaction ledger.
 *
 * Rendered as a single flat list of interleaved headers and rows rather than a
 * SectionList. FlatList's windowing works on one array, so a flattened list
 * recycles views across section boundaries too — with a SectionList, scrolling
 * past a header re-mounts the rows underneath it, which is visible as a stutter
 * once the list is a few hundred rows long.
 */
export default function TransactionsScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  const [tab, setTab] = useState<TabValue>('all');
  const [search, setSearch] = useState('');
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [filters, setFilters] = useState<TransactionFilters>({});
  const [refreshing, setRefreshing] = useState(false);

  // 350ms: long enough that typing a word does not fire six requests, short
  // enough that the list feels like it is keeping up.
  const debouncedSearch = useDebouncedValue(search, 350);

  const query = useMemo(
    () => ({
      search: debouncedSearch.trim() || undefined,
      type: tab === 'all' ? undefined : (tab as TransactionType),
      categoryId: filters.categoryId,
      from: filters.from,
      to: filters.to,
      minAmount: filters.minAmount,
      maxAmount: filters.maxAmount,
      paymentMethod: filters.paymentMethod,
      sortBy: filters.sortBy ?? 'date',
      sortDirection: filters.sortDirection ?? 'desc',
    }),
    [debouncedSearch, tab, filters],
  );

  const {
    data,
    isPending,
    isError,
    error,
    refetch,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useTransactionsInfinite(query);

  const transactions = flattenPages(data);
  const rows = useMemo(() => groupByDay(transactions), [transactions]);

  const totalCount = data?.pages[0]?.totalCount ?? 0;
  const activeFilterCount = countActiveFilters(filters);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

  const renderItem = useCallback(
    ({ item }: { item: ListRow }) => {
      if (item.kind === 'header') {
        return (
          <View
            style={{
              paddingTop: theme.spacing.lg,
              paddingBottom: theme.spacing.xs,
              backgroundColor: theme.c.background,
            }}
          >
            <AppText variant="overline" color="textSecondary">
              {item.label.toUpperCase()}
            </AppText>
          </View>
        );
      }

      return (
        <TransactionRow
          transaction={item.transaction}
          hideDate
          onPress={(t) => router.push(`/transaction/${t.id}`)}
        />
      );
    },
    [router, theme],
  );

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background, paddingTop: insets.top }]}>
      <View style={{ paddingHorizontal: theme.spacing.base, gap: theme.spacing.md }}>
        <View style={styles.titleRow}>
          <AppText variant="heading2">Transactions</AppText>

          <Pressable
            accessibilityRole="button"
            accessibilityLabel={
              activeFilterCount > 0 ? `Filters, ${activeFilterCount} active` : 'Filters'
            }
            onPress={() => setFiltersOpen(true)}
            hitSlop={8}
            style={{
              width: 40,
              height: 40,
              borderRadius: theme.radius.medium,
              backgroundColor: activeFilterCount > 0 ? theme.c.primaryMuted : theme.c.surface,
              alignItems: 'center',
              justifyContent: 'center',
              borderWidth: 1,
              borderColor: activeFilterCount > 0 ? theme.c.primary : theme.c.border,
            }}
          >
            <MaterialCommunityIcons
              name="tune-variant"
              size={20}
              color={activeFilterCount > 0 ? theme.c.primary : theme.c.textSecondary}
            />
            {activeFilterCount > 0 ? (
              <View
                style={[
                  styles.badge,
                  { backgroundColor: theme.c.primary, borderColor: theme.c.background },
                ]}
              >
                <AppText variant="caption" color="onPrimary" style={styles.badgeText}>
                  {activeFilterCount}
                </AppText>
              </View>
            ) : null}
          </Pressable>
        </View>

        <SearchBar
          value={search}
          onChangeText={setSearch}
          placeholder="Search merchant, note or category"
        />

        <SegmentedControl
          value={tab}
          onChange={(value) => setTab(value as TabValue)}
          options={[
            { value: 'all', label: 'All' },
            { value: 'Expense', label: 'Expenses' },
            { value: 'Income', label: 'Income' },
          ]}
        />
      </View>

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ paddingHorizontal: theme.spacing.base }}>
          <ListSkeleton count={8} />
        </View>
      ) : rows.length === 0 ? (
        <AppEmptyState
          title={
            debouncedSearch || activeFilterCount > 0
              ? 'Nothing matches that'
              : 'No transactions yet'
          }
          description={
            debouncedSearch || activeFilterCount > 0
              ? 'Try a different search, or clear your filters.'
              : 'Record your first expense and it will show up here.'
          }
          actionLabel={
            debouncedSearch || activeFilterCount > 0 ? 'Clear filters' : 'Add your first expense'
          }
          onAction={() => {
            if (debouncedSearch || activeFilterCount > 0) {
              setSearch('');
              setFilters({});
              setTab('all');
            } else {
              router.push('/transaction/new');
            }
          }}
          icon={
            <MaterialCommunityIcons
              name={debouncedSearch ? 'magnify' : 'receipt-text-outline'}
              size={44}
              color={theme.c.textTertiary}
            />
          }
        />
      ) : (
        <FlatList
          data={rows}
          keyExtractor={(item) => item.key}
          renderItem={renderItem}
          contentContainerStyle={{
            paddingHorizontal: theme.spacing.base,
            paddingBottom: theme.spacing.xxxl,
          }}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={onRefresh}
              tintColor={theme.c.primary}
              colors={[theme.c.primary]}
            />
          }
          // Fetch the next page well before the user reaches the bottom, so an
          // infinite list never visibly stalls.
          onEndReachedThreshold={0.4}
          onEndReached={() => {
            if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
          }}
          ListHeaderComponent={
            <AppText
              variant="caption"
              color="textTertiary"
              style={{ paddingTop: theme.spacing.sm }}
            >
              {totalCount} transaction{totalCount === 1 ? '' : 's'}
            </AppText>
          }
          ListFooterComponent={
            isFetchingNextPage ? (
              <View style={{ paddingVertical: theme.spacing.lg }}>
                <ActivityIndicator color={theme.c.primary} />
              </View>
            ) : null
          }
          showsVerticalScrollIndicator={false}
          removeClippedSubviews
          maxToRenderPerBatch={12}
          windowSize={11}
        />
      )}

      <FilterSheet
        visible={filtersOpen}
        value={filters}
        onClose={() => setFiltersOpen(false)}
        onApply={(next) => {
          setFilters(next);
          setFiltersOpen(false);
        }}
      />
    </View>
  );
}

type ListRow =
  | { kind: 'header'; key: string; label: string }
  | { kind: 'row'; key: string; transaction: Transaction };

/**
 * Interleaves date headers into the flat row list.
 *
 * The API already returns transactions ordered by date descending, so this only
 * has to detect boundaries — it never sorts, which would fight the server's
 * ordering and break pagination across page joins.
 */
function groupByDay(transactions: Transaction[]): ListRow[] {
  const rows: ListRow[] = [];
  let currentDay: string | null = null;

  for (const transaction of transactions) {
    const day = toDayKey(transaction.transactionDate);

    if (day !== currentDay) {
      currentDay = day;
      rows.push({
        kind: 'header',
        key: `header-${day}`,
        label: formatRelativeDayHeading(transaction.transactionDate),
      });
    }

    rows.push({ kind: 'row', key: transaction.id, transaction });
  }

  return rows;
}

function countActiveFilters(filters: TransactionFilters): number {
  return [
    filters.categoryId,
    filters.from,
    filters.to,
    filters.minAmount,
    filters.maxAmount,
    filters.paymentMethod,
  ].filter((value) => value !== undefined && value !== null && value !== '').length;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  badge: {
    position: 'absolute',
    top: -4,
    right: -4,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 4,
  },
  badgeText: {
    fontSize: 10,
    lineHeight: 12,
  },
});
