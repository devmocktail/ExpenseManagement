import { useMutation, useQueryClient } from '@tanstack/react-query';
import { receiptApi } from '@/api/receipt-api';
import { queryKeys, TRANSACTION_DEPENDENT_KEYS } from '@/api/query-client';

export function useUploadReceipt() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (params: {
      transactionId: string;
      uri: string;
      fileName: string;
      mimeType: string;
      onProgress?: (percent: number) => void;
    }) => receiptApi.upload(params),
    onSuccess: async (_data, variables) => {
      // The transaction detail carries the receipt list, so it is the one that
      // must refresh; the list rows show only an attachment indicator.
      await queryClient.invalidateQueries({
        queryKey: queryKeys.transactions.detail(variables.transactionId),
      });
      await queryClient.invalidateQueries({ queryKey: queryKeys.transactions.all });
    },
  });
}

export function useDeleteReceipt() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id }: { id: string; transactionId: string }) => receiptApi.remove(id),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.transactions.detail(variables.transactionId),
      });
      await queryClient.invalidateQueries({ queryKey: queryKeys.transactions.all });
    },
  });
}
