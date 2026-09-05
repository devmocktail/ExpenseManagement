import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { categoryApi } from '@/api/category-api';
import { queryKeys, TRANSACTION_DEPENDENT_KEYS } from '@/api/query-client';
import type {
  CreateCategoryRequest,
  DeleteCategoryRequest,
  TransactionType,
  UpdateCategoryRequest,
} from '@/types/api';

export function useCategories(type?: TransactionType) {
  return useQuery({
    queryKey: queryKeys.categories.list(type),
    queryFn: () => categoryApi.list(type),
    // Categories change rarely and are needed by every form, so a longer stale
    // window avoids refetching the same list on each screen open.
    staleTime: 5 * 60_000,
  });
}

export function useCreateCategory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateCategoryRequest) => categoryApi.create(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.categories.all }),
  });
}

export function useUpdateCategory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCategoryRequest }) =>
      categoryApi.update(id, body),
    // A rename or recolour shows up on every transaction row, so the whole
    // dependent set is refreshed rather than just the category list.
    onSuccess: async () => {
      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useDeleteCategory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: DeleteCategoryRequest }) =>
      categoryApi.remove(id, body ?? {}),
    onSuccess: async () => {
      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}
