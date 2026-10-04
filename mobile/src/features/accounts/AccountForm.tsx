import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { OptionPicker } from '@/components/ui/OptionPicker';
import { useAppTheme } from '@/theme/ThemeProvider';
import { ACCOUNT_TYPES, type Account, type AccountType } from '@/types/api';
import { accountSchema, type AccountFormValues } from '@/validation/schemas';

export type AccountFormSubmit = {
  name: string;
  type: AccountType;
  openingBalance: number;
  institution: string | null;
  last4: string | null;
};

export type AccountFormProps = {
  initial?: Account;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: AccountFormSubmit) => Promise<void>;
};

const TYPE_LABELS: Record<AccountType, string> = {
  Cash: 'Cash in hand',
  Bank: 'Bank account',
  CreditCard: 'Credit card',
  Wallet: 'Wallet',
  Savings: 'Savings',
  Other: 'Other',
};

const TYPE_ICONS: Record<AccountType, React.ComponentProps<typeof MaterialCommunityIcons>['name']> = {
  Cash: 'wallet-outline',
  Bank: 'bank-outline',
  CreditCard: 'credit-card-outline',
  Wallet: 'cellphone',
  Savings: 'piggy-bank-outline',
  Other: 'dots-horizontal',
};

/**
 * The add/edit account form.
 *
 * Currency is absent by design. Changing it would reinterpret every amount
 * already recorded against the account — ₹50,000 of history silently becoming
 * $50,000 — without touching a single row, so a different currency is a
 * different account. New accounts inherit the profile currency.
 */
export function AccountForm({ initial, submitLabel, submitting, onSubmit }: AccountFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<AccountFormValues>({
    resolver: zodResolver(accountSchema),
    defaultValues: {
      name: initial?.name ?? '',
      type: initial?.type ?? 'Bank',
      openingBalance: initial ? String(initial.openingBalance) : '',
      institution: initial?.institution ?? '',
      last4: initial?.last4 ?? '',
    },
    mode: 'onBlur',
  });

  const type = watch('type');

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await onSubmit({
        name: values.name.trim(),
        type: values.type,
        openingBalance: values.openingBalance ? Number(values.openingBalance) : 0,
        institution: values.institution?.trim() || null,
        last4: values.last4?.trim() || null,
      });
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? error.message
          : 'That could not be saved. Check your connection and try again.',
      );
    }
  });

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
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
          name="name"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Name"
              placeholder="HDFC Savings, Cash in hand…"
              value={value}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.name?.message}
              autoCapitalize="words"
            />
          )}
        />

        <Controller
          control={control}
          name="type"
          render={({ field: { onChange, value } }) => (
            <OptionPicker
              label="Type"
              required
              value={value}
              onChange={onChange}
              options={ACCOUNT_TYPES.map((accountType) => ({
                value: accountType,
                label: TYPE_LABELS[accountType],
                icon: TYPE_ICONS[accountType],
              }))}
              error={errors.type?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="openingBalance"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Opening balance"
              placeholder="0"
              value={value ?? ''}
              onChangeText={onChange}
              onBlur={onBlur}
              keyboardType="numbers-and-punctuation"
              error={errors.openingBalance?.message}
              hint={
                type === 'CreditCard'
                  ? 'What you already owe, as a negative number — e.g. -12500.'
                  : 'What the account holds today, before anything you record here.'
              }
            />
          )}
        />

        {type === 'Cash' ? null : (
          <>
            <Controller
              control={control}
              name="institution"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Bank or provider"
                  placeholder="HDFC Bank, Paytm…"
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  error={errors.institution?.message}
                  autoCapitalize="words"
                />
              )}
            />

            <Controller
              control={control}
              name="last4"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Last 4 digits"
                  placeholder="4321"
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  keyboardType="number-pad"
                  maxLength={4}
                  error={errors.last4?.message}
                  // Said plainly, because a field next to a card name invites
                  // the full number. Four digits is all that is stored, and all
                  // that is needed to tell two cards apart.
                  hint="Only the last four — never the full card number."
                />
              )}
            />
          </>
        )}

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
});
