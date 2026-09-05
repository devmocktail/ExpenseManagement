import { useLocalSearchParams, useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { AppErrorState, AppLoader } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { TransactionForm } from '@/features/transactions/TransactionForm';
import { useTransaction, useUpdateTransaction } from '@/features/transactions/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * Edit a transaction.
 *
 * Reuses TransactionForm rather than duplicating it, so a field added to the
 * add flow can never be missing here — the classic way an edit screen quietly
 * drops data the create screen collected.
 */
export default function EditTransactionScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: transaction, isPending, isError, error, refetch } = useTransaction(id);
  const update = useUpdateTransaction();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="Edit transaction" onBack={() => router.back()} />

      {isPending ? (
        <AppLoader />
      ) : isError || !transaction ? (
        <AppErrorState
          title="Could not load this transaction"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : (
        <TransactionForm
          initial={transaction}
          submitLabel="Save changes"
          submitting={update.isPending}
          transactionIdForReceipts={transaction.id}
          onSubmit={async (values) => {
            await update.mutateAsync({ id: transaction.id, body: values });
            toast.show({ title: 'Changes saved', tone: 'success' });
            router.back();
          }}
        />
      )}
    </View>
  );
}
