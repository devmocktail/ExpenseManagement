import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { AppErrorState } from '@/components/ui/StateViews';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { useToast } from '@/components/ui/Toast';
import { TransferForm } from '@/features/transfers/TransferForm';
import { useDeleteTransfer, useTransfer, useUpdateTransfer } from '@/features/transfers/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatCurrency } from '@/utils/currency';

/**
 * Open an existing transfer to edit or remove it.
 *
 * There is no separate read-only detail screen, unlike a transaction. A
 * transfer has five fields and no receipts or history to show, so a view that
 * only displayed them would be a screen whose single purpose is a button that
 * opens this one.
 */
export default function TransferDetailScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: transfer, isPending, isError, error, refetch } = useTransfer(id);

  const update = useUpdateTransfer();
  const remove = useDeleteTransfer();
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader
        title="Transfer"
        onBack={() => router.back()}
        actions={
          transfer
            ? [
                {
                  icon: 'trash-can-outline',
                  label: 'Delete transfer',
                  destructive: true,
                  onPress: () => setConfirmDelete(true),
                },
              ]
            : undefined
        }
      />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending || !transfer ? (
        <ListSkeleton count={5} />
      ) : (
        <TransferForm
          initial={transfer}
          submitLabel="Save changes"
          submitting={update.isPending}
          onSubmit={async (values) => {
            const saved = await update.mutateAsync({ id: transfer.id, body: values });

            toast.show({
              title: 'Transfer updated',
              description: `${formatCurrency(saved.amount, saved.currencyCode)} · ${saved.fromAccountName} → ${saved.toAccountName}`,
              tone: 'success',
            });

            router.back();
          }}
        />
      )}

      <ConfirmDialog
        visible={confirmDelete}
        title="Delete this transfer?"
        // Says what actually happens rather than a generic warning: both
        // balances move back, and nothing the user thinks of as spending
        // changes, because a transfer was never counted as any.
        message="Both account balances will go back to what they were. Your income and expense totals are not affected."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          await remove.mutateAsync(transfer!.id);
          setConfirmDelete(false);
          toast.show({ title: 'Transfer deleted', tone: 'success' });
          router.back();
        }}
      />
    </View>
  );
}
