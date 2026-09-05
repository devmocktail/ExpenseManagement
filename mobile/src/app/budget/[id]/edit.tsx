import { useLocalSearchParams, useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { AppErrorState, AppLoader } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { BudgetForm } from '@/features/budgets/BudgetForm';
import { useBudget, useUpdateBudget } from '@/features/budgets/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * Edit a budget.
 *
 * Reuses BudgetForm rather than duplicating it, so a field added to the create
 * flow can never be missing here — the classic way an edit screen quietly drops
 * data the create screen collected.
 */
export default function EditBudgetScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: budget, isPending, isError, error, refetch } = useBudget(id);
  const update = useUpdateBudget();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="Edit budget" onBack={() => router.back()} />

      {isPending ? (
        <AppLoader />
      ) : isError || !budget ? (
        <AppErrorState
          title="Could not load this budget"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : (
        <BudgetForm
          initial={budget}
          submitLabel="Save changes"
          submitting={update.isPending}
          onSubmit={async (values) => {
            await update.mutateAsync({
              id: budget.id,
              body: {
                ...values,
                // The update is a full replace, so flags the form does not
                // collect are carried over explicitly. Omitting them would let
                // an edit silently pause a budget or stop it rolling over.
                isRecurring: budget.isRecurring,
                isActive: budget.isActive,
              },
            });

            toast.show({ title: 'Changes saved', tone: 'success' });
            router.back();
          }}
        />
      )}
    </View>
  );
}
