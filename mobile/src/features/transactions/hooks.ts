import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
} from '@tanstack/react-query';
import { transactionApi } from '@/api/transaction-api';
import { queryKeys, TRANSACTION_DEPENDENT_KEYS } from '@/api/query-client';
import type {
  CreateTransactionRequest,
  Paged,
  Transaction,
  TransactionQuery,
  UpdateTransactionRequest,
} from '@/types/api';

/**
 * Transaction data hooks.
 *
 * Every mutation invalidates the same dependent set, because a single
 * transaction changes the list, the dashboard totals, budget usage and the
 * analytics aggregates at once. Missing one of those is how a UI ends up
 * showing a balance that disagrees with the list underneath it.
 */

const PAGE_SIZE = 20;

export function useTransactionsInfinite(filters: Omit<TransactionQuery, 'page' | 'pageSize'>) {
  return useInfiniteQuery<
    Paged<Transaction>,
    Error,
    InfiniteData<Paged<Transaction>>,
    ReturnType<typeof queryKeys.transactions.list>,
    number
  >({
    queryKey: queryKeys.transactions.list(filters as Record<string, unknown>),
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      transactionApi.list({ ...filters, page: pageParam, pageSize: PAGE_SIZE }),
    getNextPageParam: (lastPage) =>
      lastPage.hasNextPage ? lastPage.page + 1 : undefined,
  });
}

export function useTransaction(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.transactions.detail(id ?? ''),
    queryFn: () => transactionApi.getById(id!),
    enabled: Boolean(id),
  });
}

export function useCreateTransaction() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTransactionRequest) => transactionApi.create(body),
    onSuccess: async () => {
      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useUpdateTransaction() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTransactionRequest }) =>
      transactionApi.update(id, body),
    onSuccess: async (updated) => {
      // Seed the detail cache from the response so navigating back to the detail
      // screen shows the new values immediately rather than refetching.
      queryClient.setQueryData(queryKeys.transactions.detail(updated.id), updated);

      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useDeleteTransaction() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => transactionApi.remove(id),
    onSuccess: async (_data, id) => {
      queryClient.removeQueries({ queryKey: queryKeys.transactions.detail(id) });

      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

/** Flattens the paged result for a FlatList, which wants one array. */
export function flattenPages(data: InfiniteData<Paged<Transaction>> | undefined): Transaction[] {
  return data?.pages.flatMap((page) => page.items) ?? [];
}
