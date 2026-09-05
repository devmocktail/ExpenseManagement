import { useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { BudgetForm } from '@/features/budgets/BudgetForm';
import { useCreateBudget } from '@/features/budgets/hooks';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatCurrency } from '@/utils/currency';

/**
 * Create a budget.
 *
 * On success the user goes back where they came from — the budget list, or the
 * dashboard prompt that sent them here — with a toast that repeats the limit,
 * so they can confirm the number without opening the budget again.
 */
export default function NewBudgetScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();
  const currencyCode = useCurrencyCode();

  const create = useCreateBudget();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="New budget" onBack={() => router.back()} />

      <BudgetForm
        submitLabel="Create budget"
        submitting={create.isPending}
        onSubmit={async (values) => {
          const saved = await create.mutateAsync({
            ...values,
            // The form has no recurrence switch: a repeating period is meant to
            // roll over, and a custom window is by definition a one-off.
            isRecurring: values.period !== 'Custom',
          });

          toast.show({
            title: 'Budget created',
            description: `${formatCurrency(saved.amount, saved.currencyCode ?? currencyCode)} · ${saved.period}`,
            tone: 'success',
            action: {
              label: 'View',
              onPress: () => router.push(`/budget/${saved.id}`),
            },
          });

          if (router.canGoBack()) {
            router.back();
          } else {
            router.replace('/budget');
          }
        }}
      />
    </View>
  );
}
