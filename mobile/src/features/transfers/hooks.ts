import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { transferApi } from '@/api/transfer-api';
import { queryKeys, TRANSFER_DEPENDENT_KEYS } from '@/api/query-client';
import type { CreateTransferRequest, TransferQuery, UpdateTransferRequest } from '@/types/api';

const PAGE_SIZE = 20;

/**
 * The transfers list, paged for infinite scroll.
 *
 * Mirrors the transactions list so the two tabs behave identically under the
 * thumb — same page size, same "stop when the server says there is no next
 * page" rule rather than inferring it from a short page.
 */
export function useTransfers(filters: Omit<TransferQuery, 'page' | 'pageSize'> = {}) {
  return useInfiniteQuery({
    queryKey: queryKeys.transfers.list(filters),
    queryFn: ({ pageParam }) =>
      transferApi.list({ ...filters, page: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.hasNextPage ? lastPage.page + 1 : undefined),
    staleTime: 30_000,
  });
}

export function useTransfer(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.transfers.detail(id ?? ''),
    queryFn: () => transferApi.getById(id!),
    enabled: Boolean(id),
  });
}

export function useCreateTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTransferRequest) => transferApi.create(body),
    onSuccess: async () => {
      await Promise.all(
        TRANSFER_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useUpdateTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTransferRequest }) =>
      transferApi.update(id, body),
    onSuccess: async () => {
      await Promise.all(
        TRANSFER_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useDeleteTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => transferApi.remove(id),
    onSuccess: async () => {
      await Promise.all(
        TRANSFER_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}
