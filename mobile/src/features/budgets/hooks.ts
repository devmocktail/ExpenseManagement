import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { budgetApi } from '@/api/budget-api';
import { queryKeys } from '@/api/query-client';
import type { CreateBudgetRequest, UpdateBudgetRequest } from '@/types/api';

export function useBudgets(at?: string) {
  return useQuery({
    queryKey: queryKeys.budgets.list(at),
    queryFn: () => budgetApi.list(at),
  });
}

export function useBudget(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.budgets.detail(id ?? ''),
    queryFn: () => budgetApi.getById(id!),
    enabled: Boolean(id),
  });
}

function invalidateBudgets(queryClient: ReturnType<typeof useQueryClient>) {
  // The dashboard embeds the overall budget summary, so it goes stale too.
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.budgets.all }),
    queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
  ]);
}

export function useCreateBudget() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateBudgetRequest) => budgetApi.create(body),
    onSuccess: () => invalidateBudgets(queryClient),
  });
}

export function useUpdateBudget() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateBudgetRequest }) =>
      budgetApi.update(id, body),
    onSuccess: () => invalidateBudgets(queryClient),
  });
}

export function useDeleteBudget() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => budgetApi.remove(id),
    onSuccess: () => invalidateBudgets(queryClient),
  });
}
