import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { recurringApi } from '@/api/recurring-api';
import { queryKeys, TRANSACTION_DEPENDENT_KEYS } from '@/api/query-client';
import type { CreateRecurringRequest, UpdateRecurringRequest } from '@/types/api';

export function useRecurringList() {
  return useQuery({
    queryKey: queryKeys.recurring.list(),
    queryFn: () => recurringApi.list(),
  });
}

export function useRecurring(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.recurring.detail(id ?? ''),
    queryFn: () => recurringApi.getById(id!),
    enabled: Boolean(id),
  });
}

function invalidate(queryClient: ReturnType<typeof useQueryClient>) {
  // A schedule can generate a transaction immediately, so the ledger views are
  // invalidated alongside the schedule list.
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.recurring.all }),
    ...TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
  ]);
}

export function useCreateRecurring() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateRecurringRequest) => recurringApi.create(body),
    onSuccess: () => invalidate(queryClient),
  });
}

export function useUpdateRecurring() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateRecurringRequest }) =>
      recurringApi.update(id, body),
    onSuccess: () => invalidate(queryClient),
  });
}

export function useSetRecurringPaused() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isPaused }: { id: string; isPaused: boolean }) =>
      recurringApi.setPaused(id, isPaused),
    onSuccess: () => invalidate(queryClient),
  });
}

export function useDeleteRecurring() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => recurringApi.remove(id),
    onSuccess: () => invalidate(queryClient),
  });
}
