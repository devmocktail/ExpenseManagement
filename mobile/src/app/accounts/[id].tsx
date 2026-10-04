import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';
import { AppButton } from '@/components/ui/AppButton';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { AccountForm } from '@/features/accounts/AccountForm';
import {
  useAccount,
  useDeleteAccount,
  useSetDefaultAccount,
  useUpdateAccount,
} from '@/features/accounts/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function AccountDetailScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: account, isPending, isError, error, refetch } = useAccount(id);

  const update = useUpdateAccount();
  const remove = useDeleteAccount();
  const setDefault = useSetDefaultAccount();
  const [confirmDelete, setConfirmDelete] = useState(false);

  const inUse = (account?.transactionCount ?? 0) + (account?.transferCount ?? 0) > 0;

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title={account?.name ?? 'Account'} onBack={() => router.back()} />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending || !account ? (
        <ListSkeleton count={5} />
      ) : (
        <ScrollView keyboardShouldPersistTaps="handled">
          <AccountForm
            initial={account}
            submitLabel="Save changes"
            submitting={update.isPending}
            onSubmit={async (values) => {
              await update.mutateAsync({
                id: account.id,
                body: { ...values, isArchived: account.isArchived },
              });
              toast.show({ title: 'Account updated', tone: 'success' });
              router.back();
            }}
          />

          <View style={{ padding: theme.spacing.base, gap: theme.spacing.md }}>
            {account.isDefault ? null : (
              <AppButton
                label="Make this the default"
                variant="secondary"
                loading={setDefault.isPending}
                onPress={async () => {
                  await setDefault.mutateAsync(account.id);
                  toast.show({ title: `${account.name} is now the default`, tone: 'success' });
                }}
              />
            )}

            <AppButton
              label={account.isArchived ? 'Unarchive' : 'Archive'}
              variant="secondary"
              loading={update.isPending}
              onPress={async () => {
                await update.mutateAsync({
                  id: account.id,
                  body: {
                    name: account.name,
                    type: account.type,
                    openingBalance: account.openingBalance,
                    institution: account.institution,
                    last4: account.last4,
                    isArchived: !account.isArchived,
                  },
                });
                toast.show({
                  title: account.isArchived ? 'Account unarchived' : 'Account archived',
                  tone: 'success',
                });
              }}
            />

            <AppText variant="caption" color="textSecondary">
              {/* Said before they try it, not after it is refused. */}
              {inUse
                ? `This account has ${account.transactionCount} transaction${account.transactionCount === 1 ? '' : 's'} and ${account.transferCount} transfer${account.transferCount === 1 ? '' : 's'}, so it cannot be deleted. Archiving hides it from the pickers and keeps its history.`
                : 'Archiving hides an account from the pickers but keeps its history. Deleting is only possible while nothing references it.'}
            </AppText>

            {inUse || account.isDefault ? null : (
              <AppButton
                label="Delete account"
                variant="danger"
                loading={remove.isPending}
                onPress={() => setConfirmDelete(true)}
              />
            )}
          </View>
        </ScrollView>
      )}

      <ConfirmDialog
        visible={confirmDelete}
        title="Delete this account?"
        message="Nothing references it, so nothing else changes."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          await remove.mutateAsync(account!.id);
          setConfirmDelete(false);
          toast.show({ title: 'Account deleted', tone: 'success' });
          router.back();
        }}
      />
    </View>
  );
}
