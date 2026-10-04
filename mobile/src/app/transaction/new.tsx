import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { useToast } from '@/components/ui/Toast';
import { TransactionForm } from '@/features/transactions/TransactionForm';
import { useCreateTransaction } from '@/features/transactions/hooks';
import { TransferForm } from '@/features/transfers/TransferForm';
import { useCreateTransfer } from '@/features/transfers/hooks';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TransactionType } from '@/types/api';
import { formatCurrency } from '@/utils/currency';

type Mode = TransactionType | 'Transfer';

/**
 * Add an entry: an expense, some income, or a transfer between accounts.
 *
 * All three share one screen and one toggle because that is where people look
 * for them — a transfer tucked away on another tab is a transfer nobody finds.
 *
 * They do not share a data model. Expense and Income post to /transactions;
 * Transfer posts to /transfers and is excluded from every income and expense
 * total in the app, because moving money between your own pots is not
 * spending. The toggle is presentation; the separation underneath it is the
 * thing that keeps the totals honest.
 */
export default function NewTransactionScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();
  const currencyCode = useCurrencyCode();

  const params = useLocalSearchParams<{ type?: string }>();
  const [mode, setMode] = useState<Mode>(
    params.type === 'Income' ? 'Income' : params.type === 'Transfer' ? 'Transfer' : 'Expense',
  );

  const create = useCreateTransaction();
  const createTransfer = useCreateTransfer();

  const goBack = () => {
    // `back()` rather than a replace, so the user returns to the list or
    // dashboard they launched from instead of a fixed destination.
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/(tabs)');
    }
  };

  const typeToggle = (
    <SegmentedControl
      value={mode}
      onChange={(next) => setMode(next as Mode)}
      options={[
        { value: 'Expense', label: 'Expense' },
        { value: 'Income', label: 'Income' },
        { value: 'Transfer', label: 'Transfer' },
      ]}
    />
  );

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader
        title={mode === 'Transfer' ? 'Move money' : 'New transaction'}
        onBack={() => router.back()}
      />

      {mode === 'Transfer' ? (
        <TransferForm
          header={typeToggle}
          submitLabel="Save transfer"
          submitting={createTransfer.isPending}
          onSubmit={async (values) => {
            const saved = await createTransfer.mutateAsync(values);

            toast.show({
              title: 'Transfer recorded',
              // Names both ends: "₹10,000" alone is ambiguous on a screen whose
              // whole job is saying which way the money went.
              description: `${formatCurrency(saved.amount, saved.currencyCode)} · ${saved.fromAccountName} → ${saved.toAccountName}`,
              tone: 'success',
            });

            goBack();
          }}
        />
      ) : (
        <TransactionForm
          defaultType={mode}
          onSelectTransfer={() => setMode('Transfer')}
          submitLabel="Save transaction"
          submitting={create.isPending}
          onSubmit={async (values) => {
            const saved = await create.mutateAsync(values);

            toast.show({
              title: saved.type === 'Income' ? 'Income added' : 'Expense added',
              description: `${formatCurrency(saved.amount, saved.currencyCode ?? currencyCode)} · ${saved.categoryName}`,
              tone: 'success',
              action: {
                label: 'View',
                onPress: () => router.push(`/transaction/${saved.id}`),
              },
            });

            goBack();
          }}
        />
      )}
    </View>
  );
}
