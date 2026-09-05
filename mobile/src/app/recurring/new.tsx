import { useLocalSearchParams, useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { RecurringForm, describeRecurrence } from '@/features/recurring/RecurringForm';
import { useCreateRecurring } from '@/features/recurring/hooks';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TransactionType } from '@/types/api';
import { formatCurrency } from '@/utils/currency';
import { formatDate } from '@/utils/date';

/**
 * Set up a recurring transaction.
 *
 * The confirmation repeats the cadence and the first run date rather than just
 * saying "saved": this is a promise the app is making about future money, and
 * the moment to catch a wrong interval is now, not next month.
 */
export default function NewRecurringScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();
  const currencyCode = useCurrencyCode();

  const params = useLocalSearchParams<{ type?: string }>();
  const defaultType: TransactionType = params.type === 'Income' ? 'Income' : 'Expense';

  const create = useCreateRecurring();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="New recurring" onBack={() => router.back()} />

      <RecurringForm
        defaultType={defaultType}
        submitLabel="Save schedule"
        submitting={create.isPending}
        onSubmit={async (values) => {
          const saved = await create.mutateAsync(values);

          toast.show({
            title: `${saved.name} scheduled`,
            description: `${formatCurrency(saved.amount, saved.currencyCode ?? currencyCode)} · ${describeRecurrence(saved.frequency, saved.interval)} · First on ${formatDate(saved.nextRunDate)}`,
            tone: 'success',
          });

          // Back rather than a replace, so the user returns to wherever they
          // launched this from instead of a fixed destination.
          if (router.canGoBack()) {
            router.back();
          } else {
            router.replace('/recurring');
          }
        }}
      />
    </View>
  );
}
