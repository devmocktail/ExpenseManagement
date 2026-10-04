import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AmountInput } from '@/components/AmountInput';
import { DateTimeField } from '@/components/DateTimeField';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { OptionPicker } from '@/components/ui/OptionPicker';
import { useAccounts } from '@/features/accounts/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Transfer } from '@/types/api';
import { formatCurrency, parseAmountInput } from '@/utils/currency';
import { toServerDate } from '@/utils/date';
import { transferSchema, type TransferFormValues } from '@/validation/schemas';

export type TransferFormSubmit = {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  transferDate: string;
  notes: string | null;
};

export type TransferFormProps = {
  /** Present when editing. Pre-populates every field. */
  initial?: Transfer;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: TransferFormSubmit) => Promise<void>;
};

/**
 * The move-money form.
 *
 * Deliberately short — two accounts, an amount, a date. A transfer has no
 * category, no payment method and no receipt, because none of those mean
 * anything for money that never left the user's own control.
 */
export function TransferForm({ initial, submitLabel, submitting, onSubmit }: TransferFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const [formError, setFormError] = useState<string | null>(null);

  const { data: accounts = [], isPending: accountsLoading } = useAccounts();

  const {
    control,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<TransferFormValues>({
    resolver: zodResolver(transferSchema),
    defaultValues: {
      fromAccountId: initial?.fromAccountId ?? '',
      toAccountId: initial?.toAccountId ?? '',
      amount: initial ? String(initial.amount) : '',
      transferDate: initial ? new Date(initial.transferDate) : new Date(),
      notes: initial?.notes ?? '',
    },
  });

  const fromId = watch('fromAccountId');
  const toId = watch('toAccountId');

  const from = accounts.find((a) => a.id === fromId);
  const to = accounts.find((a) => a.id === toId);

  const options = useMemo(
    () =>
      accounts.map((account) => ({
        value: account.id,
        label: account.name,
        // The balance is the number that decides whether this transfer is even
        // possible, so it belongs in the picker rather than a screen away.
        description: formatCurrency(account.balance, account.currencyCode),
        icon: account.icon as React.ComponentProps<typeof MaterialCommunityIcons>['name'],
      })),
    [accounts],
  );

  /**
   * Destination options exclude the source.
   *
   * The schema rejects a same-account transfer anyway, but letting the user
   * pick it and then telling them off is worse than not offering it. Only
   * accounts sharing the source's currency are offered, for the same reason:
   * the server refuses a cross-currency transfer, and an option that can only
   * fail is not an option.
   */
  const destinationOptions = useMemo(
    () =>
      options.filter((option) => {
        if (option.value === fromId) return false;
        if (!from) return true;
        const account = accounts.find((a) => a.id === option.value);
        return account?.currencyCode === from.currencyCode;
      }),
    [options, fromId, from, accounts],
  );

  const swap = () => {
    setValue('fromAccountId', toId, { shouldValidate: true });
    setValue('toAccountId', fromId, { shouldValidate: true });
  };

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await onSubmit({
        fromAccountId: values.fromAccountId,
        toAccountId: values.toAccountId,
        amount: parseAmountInput(values.amount) ?? 0,
        transferDate: toServerDate(values.transferDate),
        notes: values.notes?.trim() ? values.notes.trim() : null,
      });
    } catch (error) {
      // Surfaced in the form rather than as a toast: the rules the server
      // enforces here — archived account, currency mismatch — are about the
      // fields on screen, and a toast disappears before it can be acted on.
      setFormError(
        error instanceof ApiError
          ? error.message
          : 'That could not be saved. Check your connection and try again.',
      );
    }
  });

  if (!accountsLoading && accounts.length < 2) {
    return (
      <View style={[styles.empty, { padding: theme.spacing.xl, gap: theme.spacing.md }]}>
        <MaterialCommunityIcons name="bank-outline" size={40} color={theme.c.textTertiary} />
        <AppText variant="heading3" align="center">
          You need two accounts
        </AppText>
        <AppText variant="bodySmall" color="textSecondary" align="center">
          A transfer moves money between two of your own accounts. Add another one — a bank account
          or a wallet — and you can move money into it.
        </AppText>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.xxl,
          gap: theme.spacing.lg,
        }}
        keyboardShouldPersistTaps="handled"
      >
        <Controller
          control={control}
          name="amount"
          render={({ field }) => (
            <AmountInput
              value={field.value}
              onChangeText={field.onChange}
              // The control needs a direction to colour itself. A transfer has
              // none, so it borrows the expense styling; nothing downstream
              // reads this, and the row and totals both treat it as neither.
              type="Expense"
              currencyCode={from?.currencyCode}
              error={errors.amount?.message}
            />
          )}
        />

        <View style={{ gap: theme.spacing.sm }}>
          <Controller
            control={control}
            name="fromAccountId"
            render={({ field }) => (
              <OptionPicker
                label="From"
                required
                value={field.value || undefined}
                options={options}
                onChange={(value) => {
                  field.onChange(value);
                  // Clearing a now-invalid destination is better than leaving a
                  // stale one that the form will reject on submit.
                  if (value === toId) setValue('toAccountId', '');
                }}
                placeholder="Which account is the money leaving?"
                error={errors.fromAccountId?.message}
              />
            )}
          />

          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Swap the two accounts"
            onPress={swap}
            hitSlop={8}
            style={[styles.swap, { alignSelf: 'center' }]}
          >
            <MaterialCommunityIcons name="swap-vertical" size={22} color={theme.c.primary} />
          </Pressable>

          <Controller
            control={control}
            name="toAccountId"
            render={({ field }) => (
              <OptionPicker
                label="To"
                required
                value={field.value || undefined}
                options={destinationOptions}
                onChange={field.onChange}
                placeholder="Which account is it going into?"
                error={errors.toAccountId?.message}
              />
            )}
          />
        </View>

        {from && to ? (
          <View
            style={{
              padding: theme.spacing.md,
              borderRadius: theme.radius.medium,
              backgroundColor: theme.c.surfaceSunken,
              gap: theme.spacing.xs,
            }}
          >
            <AppText variant="caption" color="textSecondary">
              This does not count as spending — it only moves money between your accounts.
            </AppText>
          </View>
        ) : null}

        <Controller
          control={control}
          name="transferDate"
          render={({ field }) => (
            <DateTimeField
              label="Date and time"
              value={field.value}
              onChange={field.onChange}
              error={errors.transferDate?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="notes"
          render={({ field }) => (
            <AppInput
              label="Note"
              value={field.value ?? ''}
              onChangeText={field.onChange}
              placeholder="ATM withdrawal, card bill…"
              multiline
              error={errors.notes?.message}
            />
          )}
        />

        {formError ? (
          <AppText variant="bodySmall" color="error">
            {formError}
          </AppText>
        ) : null}

        <AppButton label={submitLabel} onPress={submit} loading={submitting} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  swap: {
    width: 36,
    height: 36,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
