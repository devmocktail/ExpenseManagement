import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { accountApi } from '@/api/account-api';
import { queryKeys, TRANSACTION_DEPENDENT_KEYS } from '@/api/query-client';
import type { CreateAccountRequest, UpdateAccountRequest } from '@/types/api';

export function useAccounts(includeArchived = false) {
  return useQuery({
    queryKey: queryKeys.accounts.list(includeArchived),
    queryFn: () => accountApi.list(includeArchived),
    // Shorter than categories: the list carries balances, which change every
    // time anything is spent, so holding it for five minutes would show a
    // figure the user can see is wrong on the screen they just came from.
    staleTime: 30_000,
  });
}

export function useAccount(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.accounts.detail(id ?? ''),
    queryFn: () => accountApi.getById(id!),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}

/**
 * The account a new transaction should start on.
 *
 * Returns undefined rather than guessing when there is no default — leaving
 * the picker empty is honest, whereas silently choosing the first account
 * would file spending somewhere the user never looked at.
 */
export function useDefaultAccount() {
  const { data } = useAccounts();
  return data?.find((account) => account.isDefault && !account.isArchived);
}

export function useCreateAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateAccountRequest) => accountApi.create(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all }),
  });
}

export function useUpdateAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateAccountRequest }) =>
      accountApi.update(id, body),
    // A rename or recolour shows on every transaction row that names the
    // account, and editing the opening balance moves its balance, so the whole
    // dependent set is refreshed rather than just the account list.
    onSuccess: async () => {
      await Promise.all(
        TRANSACTION_DEPENDENT_KEYS.map((key) => queryClient.invalidateQueries({ queryKey: key })),
      );
    },
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => accountApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all }),
  });
}

export function useSetDefaultAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => accountApi.setDefault(id),
    // Two rows change — the new default and the one it displaced — so the list
    // is invalidated rather than the single detail entry patched.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts.all }),
  });
}
