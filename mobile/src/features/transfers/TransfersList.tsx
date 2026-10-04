import { useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { TransferRow } from '@/components/TransferRow';
import { AppText } from '@/components/ui/AppText';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { useTransfers } from '@/features/transfers/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Transfer } from '@/types/api';
import { formatRelativeDayHeading, toDayKey } from '@/utils/date';

export type TransfersListProps = {
  /** Restricts the list to transfers touching this account on either end. */
  accountId?: string;
  /** Extra bottom padding so the last row clears the tab bar. */
  bottomInset?: number;
};

/**
 * The transfers tab.
 *
 * Structurally a twin of the transaction ledger — one flat list of interleaved
 * headers and rows, so FlatList's windowing recycles views across day
 * boundaries instead of re-mounting them, which a SectionList cannot do.
 */
export function TransfersList({ accountId, bottomInset = 0 }: TransfersListProps) {
  const theme = useAppTheme();
  const router = useRouter();
  const [refreshing, setRefreshing] = useState(false);

  const filters = useMemo(() => (accountId ? { accountId } : {}), [accountId]);

  const {
    data,
    isPending,
    isError,
    error,
    refetch,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useTransfers(filters);

  const transfers = useMemo(() => data?.pages.flatMap((page) => page.items) ?? [], [data]);
  const rows = useMemo(() => groupByDay(transfers), [transfers]);
  const totalCount = data?.pages[0]?.totalCount ?? 0;

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
        <TransferRow
          transfer={item.transfer}
          hideDate
          onPress={(transfer) => router.push(`/transfer/${transfer.id}`)}
        />
      );
    },
    [router, theme],
  );

  if (isError) {
    return (
      <AppErrorState
        description={error instanceof Error ? error.message : undefined}
        onRetry={() => void refetch()}
      />
    );
  }

  if (isPending) {
    return (
      <View style={{ paddingHorizontal: theme.spacing.base }}>
        <ListSkeleton count={6} />
      </View>
    );
  }

  if (rows.length === 0) {
    return (
      <AppEmptyState
        title="No transfers yet"
        description="Moving money between your own accounts — a cash withdrawal, paying a card bill — goes here. It never counts as spending."
        actionLabel="Record a transfer"
        onAction={() => router.push('/transfer/new')}
      />
    );
  }

  return (
    <FlatList
      data={rows}
      keyExtractor={(item) => item.key}
      renderItem={renderItem}
      contentContainerStyle={{
        paddingHorizontal: theme.spacing.base,
        paddingBottom: bottomInset + theme.spacing.xxl,
      }}
      refreshControl={
        <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={theme.c.primary} />
      }
      onEndReachedThreshold={0.4}
      onEndReached={() => {
        if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
      }}
      ListHeaderComponent={
        <AppText variant="caption" color="textSecondary" style={{ paddingTop: theme.spacing.sm }}>
          {totalCount} transfer{totalCount === 1 ? '' : 's'}
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
  );
}

type ListRow =
  | { kind: 'header'; key: string; label: string }
  | { kind: 'row'; key: string; transfer: Transfer };

/**
 * Interleaves date headers into the flat row list. The API already returns
 * transfers newest first, so this only detects boundaries — sorting here would
 * fight the server's ordering and break pagination across page joins.
 */
function groupByDay(transfers: Transfer[]): ListRow[] {
  const rows: ListRow[] = [];
  let currentDay: string | null = null;

  for (const transfer of transfers) {
    const day = toDayKey(transfer.transferDate);

    if (day !== currentDay) {
      currentDay = day;
      rows.push({
        kind: 'header',
        key: `header-${day}`,
        label: formatRelativeDayHeading(transfer.transferDate),
      });
    }

    rows.push({ kind: 'row', key: transfer.id, transfer });
  }

  return rows;
}

const styles = StyleSheet.create({});
