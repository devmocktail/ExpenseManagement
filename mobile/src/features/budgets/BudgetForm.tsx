import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { addMonths } from 'date-fns';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AmountInput } from '@/components/AmountInput';
import { CategoryPicker } from '@/components/CategoryPicker';
import { DateTimeField } from '@/components/DateTimeField';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { OptionPicker } from '@/components/ui/OptionPicker';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { useAppTheme } from '@/theme/ThemeProvider';
import { BUDGET_PERIODS, type Budget, type BudgetPeriod } from '@/types/api';
import { parseAmountInput } from '@/utils/currency';
import { toServerDate } from '@/utils/date';
import { budgetSchema, type BudgetFormValues } from '@/validation/schemas';

type IconName = React.ComponentProps<typeof MaterialCommunityIcons>['name'];

/** Every budget scope reduces to all of my spending, or one expense category. */
type BudgetScope = 'overall' | 'category';

export type BudgetFormSubmit = {
  name: string;
  /** Null is the overall budget covering every category. */
  categoryId: string | null;
  amount: number;
  period: BudgetPeriod;
  startDate: string;
  /** Only sent for a custom window; otherwise the server derives it. */
  endDate?: string;
};

export type BudgetFormProps = {
  /** Present when editing. Pre-populates every field. */
  initial?: Budget;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: BudgetFormSubmit) => Promise<void>;
};

const PERIOD_ICONS: Record<BudgetPeriod, IconName> = {
  Weekly: 'calendar-week',
  Monthly: 'calendar-month',
  Quarterly: 'calendar-range',
  Yearly: 'calendar-blank',
  Custom: 'calendar-edit',
};

const PERIOD_DESCRIPTIONS: Record<BudgetPeriod, string> = {
  Weekly: 'Starts again every week',
  Monthly: 'Starts again every month',
  Quarterly: 'Starts again every three months',
  Yearly: 'Starts again every year',
  Custom: 'A one-off window you choose',
};

/**
 * The create/edit budget form.
 *
 * The one decision that shapes everything else is scope: an overall budget and
 * a per-category budget are different tools, and asking which category of
 * someone who wants a single monthly ceiling is the fastest way to make them
 * pick one at random. So scope is an explicit switch, and the category field
 * only exists on the branch that needs it.
 */
export function BudgetForm({ initial, submitLabel, submitting, onSubmit }: BudgetFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  // Scope is its own state rather than derived from categoryId, because
  // "by category, nothing picked yet" is a real state the user passes through.
  const [scope, setScope] = useState<BudgetScope>(initial?.categoryId ? 'category' : 'overall');
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    getValues,
    setValue,
    setError,
    formState: { errors },
  } = useForm<BudgetFormValues>({
    resolver: zodResolver(budgetSchema),
    defaultValues: {
      name: initial?.name ?? '',
      categoryId: initial?.categoryId ?? '',
      amount: initial ? String(initial.amount) : '',
      period: initial?.period ?? 'Monthly',
      startDate: initial ? new Date(initial.startDate) : new Date(),
      endDate: initial?.period === 'Custom' ? new Date(initial.endDate) : undefined,
    },
    mode: 'onBlur',
  });

  const period = watch('period');
  const startDate = watch('startDate');

  const handlePeriodChange = (next: BudgetPeriod, onChange: (value: BudgetPeriod) => void) => {
    onChange(next);

    if (next === 'Custom') {
      // Seed a plausible window so the field shows a real date the user can
      // adjust, rather than an empty control that fails validation on submit.
      if (!getValues('endDate')) {
        setValue('endDate', addMonths(getValues('startDate'), 1), { shouldValidate: false });
      }
    } else {
      // A stale end date would otherwise be sent with, or invalidate, a period
      // whose window the server computes for itself.
      setValue('endDate', undefined, { shouldValidate: false });
    }
  };

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    const amount = parseAmountInput(values.amount);
    if (amount === null) {
      setError('amount', { type: 'manual', message: 'Enter a valid amount' });
      return;
    }

    // The schema allows an absent categoryId — that is the overall budget — so
    // the "switched to By category but picked nothing" case is caught here.
    if (scope === 'category' && !values.categoryId) {
      setError('categoryId', { type: 'manual', message: 'Pick a category' });
      return;
    }

    try {
      await onSubmit({
        name: values.name.trim(),
        categoryId: scope === 'category' ? (values.categoryId ?? null) : null,
        amount,
        period: values.period,
        startDate: toServerDate(values.startDate),
        endDate:
          values.period === 'Custom' && values.endDate ? toServerDate(values.endDate) : undefined,
      });
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof BudgetFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else {
        setFormError(
          error instanceof ApiError ? error.message : 'Could not save. Please try again.',
        );
      }
    }
  });

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? insets.top + 44 : 0}
    >
      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + 120,
          gap: theme.spacing.lg,
        }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <Controller
          control={control}
          name="name"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Name"
              placeholder="Groceries, everyday spending"
              value={value}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.name?.message}
              autoCapitalize="sentences"
              returnKeyType="next"
              required
              autoFocus={!initial}
            />
          )}
        />

        <View>
          <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: 6 }}>
            Applies to
          </AppText>

          <SegmentedControl
            value={scope}
            onChange={(next) => {
              const nextScope = next as BudgetScope;
              setScope(nextScope);
              // An overall budget must not carry a category, or the server would
              // store a category budget behind an everything-shaped label.
              if (nextScope === 'overall') {
                setValue('categoryId', '', { shouldValidate: false });
              }
            }}
            options={[
              { value: 'overall', label: 'Overall' },
              { value: 'category', label: 'By category' },
            ]}
          />

          {scope === 'overall' ? (
            <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.sm }}>
              One ceiling across every expense category.
            </AppText>
          ) : null}
        </View>

        {scope === 'category' ? (
          <Controller
            control={control}
            name="categoryId"
            render={({ field: { onChange, value } }) => (
              // Income has no budget, so the picker is pinned to expenses.
              <CategoryPicker
                type="Expense"
                value={value || undefined}
                onChange={onChange}
                error={errors.categoryId?.message}
              />
            )}
          />
        ) : null}

        <Controller
          control={control}
          name="amount"
          render={({ field: { onChange, value } }) => (
            <AmountInput
              value={value}
              onChangeText={onChange}
              type="Expense"
              currencyCode={initial?.currencyCode}
              error={errors.amount?.message}
              autoFocus={false}
            />
          )}
        />

        <Controller
          control={control}
          name="period"
          render={({ field: { onChange, value } }) => (
            <OptionPicker
              label="Repeats"
              value={value}
              onChange={(next) => handlePeriodChange(next, onChange)}
              options={BUDGET_PERIODS.map((option) => ({
                value: option,
                label: option,
                description: PERIOD_DESCRIPTIONS[option],
                icon: PERIOD_ICONS[option],
              }))}
              error={errors.period?.message}
              required
            />
          )}
        />

        <Controller
          control={control}
          name="startDate"
          render={({ field: { onChange, value } }) => (
            <DateTimeField
              label="Starts"
              value={value}
              onChange={onChange}
              mode="date"
              error={errors.startDate?.message}
            />
          )}
        />

        {period === 'Custom' ? (
          <Controller
            control={control}
            name="endDate"
            render={({ field: { onChange, value } }) => (
              <DateTimeField
                label="Ends"
                value={value ?? addMonths(startDate, 1)}
                onChange={onChange}
                mode="date"
                minimumDate={startDate}
                error={errors.endDate?.message}
              />
            )}
          />
        ) : null}

        {formError ? (
          <View
            accessibilityRole="alert"
            style={{
              backgroundColor: theme.c.errorMuted,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
            }}
          >
            <AppText variant="bodySmall" color="error">
              {formError}
            </AppText>
          </View>
        ) : null}
      </ScrollView>

      {/* Pinned so the primary action is always reachable with a thumb, no
          matter how far the form has been scrolled. */}
      <View
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.md,
          backgroundColor: theme.c.surface,
          borderTopWidth: 1,
          borderTopColor: theme.c.border,
        }}
      >
        <AppButton
          label={submitLabel}
          size="large"
          fullWidth
          loading={submitting}
          onPress={() => void submit()}
        />
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
});
