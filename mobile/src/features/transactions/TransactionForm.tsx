import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
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
import { PAYMENT_METHOD_ICONS, PAYMENT_METHOD_LABELS } from '@/constants/icons';
import { ReceiptPicker } from '@/features/receipts/ReceiptPicker';
import { useAppTheme } from '@/theme/ThemeProvider';
import { PAYMENT_METHODS, type PaymentMethod, type Transaction, type TransactionType } from '@/types/api';
import { parseAmountInput } from '@/utils/currency';
import { toServerDate } from '@/utils/date';
import { transactionSchema, type TransactionFormValues } from '@/validation/schemas';

export type TransactionFormSubmit = {
  type: TransactionType;
  amount: number;
  categoryId: string;
  transactionDate: string;
  paymentMethod: PaymentMethod;
  merchant: string | null;
  description: string | null;
  notes: string | null;
};

export type TransactionFormProps = {
  /** Present when editing. Pre-populates every field. */
  initial?: Transaction;
  defaultType?: TransactionType;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: TransactionFormSubmit) => Promise<void>;
  /** Only offered when editing — a receipt needs a transaction to attach to. */
  transactionIdForReceipts?: string;
};

/**
 * The add/edit transaction form.
 *
 * The brief's target flow is amount → category → save, so those two are above
 * the fold and everything else is collapsed behind "More details". A form that
 * demands ten fields for a ₹40 chai is a form people stop using by the second
 * week.
 */
export function TransactionForm({
  initial,
  defaultType = 'Expense',
  submitLabel,
  submitting,
  onSubmit,
  transactionIdForReceipts,
}: TransactionFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  const [showAdvanced, setShowAdvanced] = useState(
    // Open automatically when editing something that already uses these fields,
    // otherwise the user cannot see values that exist.
    Boolean(initial?.notes || initial?.description),
  );
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors },
  } = useForm<TransactionFormValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: {
      type: initial?.type ?? defaultType,
      amount: initial ? String(initial.amount) : '',
      categoryId: initial?.categoryId ?? '',
      transactionDate: initial ? new Date(initial.transactionDate) : new Date(),
      paymentMethod: initial?.paymentMethod ?? 'Cash',
      merchant: initial?.merchant ?? '',
      description: initial?.description ?? '',
      notes: initial?.notes ?? '',
    },
    mode: 'onBlur',
  });

  const type = watch('type');

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    const amount = parseAmountInput(values.amount);
    if (amount === null) {
      setError('amount', { type: 'manual', message: 'Enter a valid amount' });
      return;
    }

    try {
      await onSubmit({
        type: values.type,
        amount,
        categoryId: values.categoryId,
        transactionDate: toServerDate(values.transactionDate),
        paymentMethod: values.paymentMethod,
        // Empty strings become null so the server stores an absent value rather
        // than an empty one, which would show as a blank line in the detail view.
        merchant: values.merchant?.trim() || null,
        description: values.description?.trim() || null,
        notes: values.notes?.trim() || null,
      });
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof TransactionFormValues, {
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
          name="type"
          render={({ field: { onChange, value } }) => (
            <SegmentedControl
              value={value}
              onChange={(next) => {
                onChange(next);
                // Categories are scoped to a direction, so a category chosen for
                // an expense is not valid for income. Clearing it here prevents
                // a confusing server-side rejection at save time.
                setValue('categoryId', '', { shouldValidate: false });
              }}
              options={[
                { value: 'Expense', label: 'Expense' },
                { value: 'Income', label: 'Income' },
              ]}
            />
          )}
        />

        <Controller
          control={control}
          name="amount"
          render={({ field: { onChange, value } }) => (
            <AmountInput
              value={value}
              onChangeText={onChange}
              type={type}
              error={errors.amount?.message}
              autoFocus={!initial}
            />
          )}
        />

        <Controller
          control={control}
          name="categoryId"
          render={({ field: { onChange, value } }) => (
            <CategoryPicker
              type={type}
              value={value || undefined}
              onChange={onChange}
              error={errors.categoryId?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="transactionDate"
          render={({ field: { onChange, value } }) => (
            <DateTimeField
              label="Date and time"
              value={value}
              onChange={onChange}
              mode="datetime"
              maximumDate={new Date()}
              error={errors.transactionDate?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="paymentMethod"
          render={({ field: { onChange, value } }) => (
            <OptionPicker
              label="Paid with"
              value={value}
              onChange={onChange}
              options={PAYMENT_METHODS.map((method) => ({
                value: method,
                label: PAYMENT_METHOD_LABELS[method] ?? method,
                icon: PAYMENT_METHOD_ICONS[method],
              }))}
              error={errors.paymentMethod?.message}
            />
          )}
        />

        <Controller
          control={control}
          name="merchant"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Merchant"
              placeholder={type === 'Income' ? 'Who paid you?' : 'Where did you spend?'}
              value={value ?? ''}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.merchant?.message}
              autoCapitalize="words"
              returnKeyType="done"
            />
          )}
        />

        <Pressable
          accessibilityRole="button"
          accessibilityState={{ expanded: showAdvanced }}
          accessibilityLabel={showAdvanced ? 'Hide more details' : 'Show more details'}
          onPress={() => setShowAdvanced((v) => !v)}
          style={[styles.disclosure, { gap: theme.spacing.xs }]}
        >
          <AppText variant="bodySmallStrong" color="primary">
            {showAdvanced ? 'Fewer details' : 'More details'}
          </AppText>
          <MaterialCommunityIcons
            name={showAdvanced ? 'chevron-up' : 'chevron-down'}
            size={18}
            color={theme.c.primary}
          />
        </Pressable>

        {showAdvanced ? (
          <View style={{ gap: theme.spacing.base }}>
            <Controller
              control={control}
              name="description"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Description"
                  placeholder="What was it for?"
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  error={errors.description?.message}
                />
              )}
            />

            <Controller
              control={control}
              name="notes"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Notes"
                  placeholder="Anything else worth remembering"
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  error={errors.notes?.message}
                  multiline
                  numberOfLines={3}
                  inputStyle={{ minHeight: 84, textAlignVertical: 'top' }}
                />
              )}
            />
          </View>
        ) : null}

        {transactionIdForReceipts ? (
          <ReceiptPicker
            transactionId={transactionIdForReceipts}
            receipts={initial?.receipts ?? []}
          />
        ) : (
          <View
            style={{
              backgroundColor: theme.c.surfaceSunken,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
              flexDirection: 'row',
              alignItems: 'center',
              gap: theme.spacing.sm,
            }}
          >
            <MaterialCommunityIcons
              name="information-outline"
              size={16}
              color={theme.c.textTertiary}
            />
            <AppText variant="caption" color="textTertiary" style={styles.flex}>
              Save first, then you can attach a receipt.
            </AppText>
          </View>
        )}

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
  disclosure: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
  },
});
