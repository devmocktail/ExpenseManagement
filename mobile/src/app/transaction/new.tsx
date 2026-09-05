import { useLocalSearchParams, useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { TransactionForm } from '@/features/transactions/TransactionForm';
import { useCreateTransaction } from '@/features/transactions/hooks';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TransactionType } from '@/types/api';
import { formatCurrency } from '@/utils/currency';

/**
 * Add a transaction.
 *
 * On success the user goes straight back to where they came from with a toast,
 * rather than landing on a detail screen they did not ask for. The confirmation
 * repeats the amount and category so they can see at a glance that what was
 * saved is what they meant — which is the only check most people will do.
 */
export default function NewTransactionScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();
  const currencyCode = useCurrencyCode();

  const params = useLocalSearchParams<{ type?: string }>();
  const defaultType: TransactionType = params.type === 'Income' ? 'Income' : 'Expense';

  const create = useCreateTransaction();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="New transaction" onBack={() => router.back()} />

      <TransactionForm
        defaultType={defaultType}
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

          // `back()` rather than a replace, so the user returns to the list or
          // dashboard they launched from instead of a fixed destination.
          if (router.canGoBack()) {
            router.back();
          } else {
            router.replace('/(tabs)');
          }
        }}
      />
    </View>
  );
}
