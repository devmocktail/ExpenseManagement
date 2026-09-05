import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { addYears } from 'date-fns';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AmountInput } from '@/components/AmountInput';
import { CategoryPicker } from '@/components/CategoryPicker';
import { DateTimeField } from '@/components/DateTimeField';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { OptionPicker, type PickerOption } from '@/components/ui/OptionPicker';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import {
  PAYMENT_METHOD_ICONS,
  PAYMENT_METHOD_LABELS,
  type IconName,
} from '@/constants/icons';
import { useAppTheme } from '@/theme/ThemeProvider';
import {
  PAYMENT_METHODS,
  RECURRENCE_FREQUENCIES,
  type PaymentMethod,
  type RecurrenceFrequency,
  type RecurringTransaction,
  type TransactionType,
} from '@/types/api';
import { parseAmountInput } from '@/utils/currency';
import { toServerDate } from '@/utils/date';
import { recurringSchema, type RecurringFormValues } from '@/validation/schemas';

const FREQUENCY_NOUNS: Record<RecurrenceFrequency, string> = {
  Daily: 'day',
  Weekly: 'week',
  Monthly: 'month',
  Yearly: 'year',
};

const FREQUENCY_ICONS: Record<RecurrenceFrequency, IconName> = {
  Daily: 'calendar-today',
  Weekly: 'calendar-week',
  Monthly: 'calendar-month',
  Yearly: 'calendar-blank-outline',
};

/**
 * "Every month", "Every 2 weeks".
 *
 * On the wire a schedule is a frequency plus an interval, which is precise and
 * unreadable — nobody thinks in `{Weekly, 2}`, they think "every other week".
 * The pair is only ever shown to a user through this.
 */
export function describeRecurrence(
  frequency: RecurrenceFrequency,
  interval: number | null | undefined,
): string {
  const noun = FREQUENCY_NOUNS[frequency];
  if (!noun) return '';

  const count = Number(interval);
  if (!Number.isFinite(count) || count <= 1) return `Every ${noun}`;

  return `Every ${count} ${noun}s`;
}

/** "On the day", "2 days before" — the reminder offset in the same plain register. */
export function describeReminder(days: number): string {
  if (days <= 0) return 'On the day';
  if (days === 1) return '1 day before';
  if (days === 7) return '1 week before';
  return `${days} days before`;
}

// Fixed offsets rather than a free number field: a reminder is a nudge, and the
// difference between four days and five is not a decision worth asking for.
const REMINDER_DAYS = [0, 1, 2, 3, 7] as const;

export type RecurringFormSubmit = {
  name: string;
  type: TransactionType;
  amount: number;
  categoryId: string;
  paymentMethod: PaymentMethod;
  frequency: RecurrenceFrequency;
  interval: number;
  startDate: string;
  endDate: string | null;
  merchant: string | null;
  description: string | null;
  reminderDaysBefore: number;
};

export type RecurringFormProps = {
  /** Present when editing. Pre-populates every field. */
  initial?: RecurringTransaction;
  defaultType?: TransactionType;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: RecurringFormSubmit) => Promise<void>;
  /** Screen-specific actions under the fields — pause and delete when editing. */
  footer?: React.ReactNode;
};

/**
 * The add/edit form for a recurring transaction.
 *
 * It leans the opposite way from the transaction form. That one is optimised
 * for speed because it is used many times a day; this one is filled in once and
 * then trusted for months, so it spells out in words what the machine-readable
 * fields will actually do. A wrong interval here is not one bad row — it is a
 * wrong row every fortnight until somebody notices.
 */
export function RecurringForm({
  initial,
  defaultType = 'Expense',
  submitLabel,
  submitting,
  onSubmit,
  footer,
}: RecurringFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  const [showAdvanced, setShowAdvanced] = useState(
    // Opened when editing something that already uses these fields, otherwise
    // the user cannot see values that exist.
    Boolean(initial?.merchant || initial?.description),
  );
  const [hasEndDate, setHasEndDate] = useState(Boolean(initial?.endDate));
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors },
  } = useForm<RecurringFormValues>({
    resolver: zodResolver(recurringSchema),
    defaultValues: {
      name: initial?.name ?? '',
      type: initial?.type ?? defaultType,
      amount: initial ? String(initial.amount) : '',
      categoryId: initial?.categoryId ?? '',
      paymentMethod: initial?.paymentMethod ?? 'Cash',
      frequency: initial?.frequency ?? 'Monthly',
      interval: initial?.interval ?? 1,
      startDate: initial ? new Date(initial.startDate) : new Date(),
      endDate: initial?.endDate ? new Date(initial.endDate) : null,
      merchant: initial?.merchant ?? '',
      description: initial?.description ?? '',
      reminderDaysBefore: initial?.reminderDaysBefore ?? 1,
    },
    mode: 'onBlur',
  });

  const type = watch('type');
  const frequency = watch('frequency');
  const interval = watch('interval');
  const startDate = watch('startDate');

  const toggleEndDate = (enabled: boolean) => {
    setHasEndDate(enabled);
    // The picker needs a date to render, and a year out is the least surprising
    // starting point for something that repeats.
    setValue('endDate', enabled ? addYears(startDate ?? new Date(), 1) : null, {
      shouldValidate: false,
    });
  };

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    const amount = parseAmountInput(values.amount);
    if (amount === null) {
      setError('amount', { type: 'manual', message: 'Enter a valid amount' });
      return;
    }

    try {
      await onSubmit({
        name: values.name.trim(),
        type: values.type,
        amount,
        categoryId: values.categoryId,
        paymentMethod: values.paymentMethod,
        frequency: values.frequency,
        interval: values.interval,
        startDate: toServerDate(values.startDate),
        // The switch, not the stored value, decides whether an end date is sent —
        // turning it off has to clear a date the server already knows about.
        endDate: hasEndDate && values.endDate ? toServerDate(values.endDate) : null,
        merchant: values.merchant?.trim() || null,
        description: values.description?.trim() || null,
        reminderDaysBefore: values.reminderDaysBefore,
      });
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof RecurringFormValues, {
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

  const reminderOptions: PickerOption<string>[] = REMINDER_DAYS.map((days) => ({
    value: String(days),
    label: describeReminder(days),
    icon: days === 0 ? 'bell-ring-outline' : 'bell-outline',
  }));

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
              required
              placeholder="Rent, Netflix, Salary…"
              value={value}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.name?.message}
              hint="What this is called every time it lands in your ledger"
              autoCapitalize="words"
              autoFocus={!initial}
              returnKeyType="next"
            />
          )}
        />

        <Controller
          control={control}
          name="type"
          render={({ field: { onChange, value } }) => (
            <SegmentedControl
              value={value}
              onChange={(next) => {
                onChange(next);
                // Categories are scoped to a direction, so one chosen for an
                // expense is not valid for income. Clearing it here prevents a
                // confusing server-side rejection at save time.
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
              currencyCode={initial?.currencyCode}
              error={errors.amount?.message}
              autoFocus={false}
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

        <View style={{ gap: theme.spacing.base }}>
          <AppText variant="overline" color="textSecondary">
            SCHEDULE
          </AppText>

          <Controller
            control={control}
            name="frequency"
            render={({ field: { onChange, value } }) => (
              <OptionPicker
                label="Repeats"
                value={value}
                onChange={onChange}
                options={RECURRENCE_FREQUENCIES.map((option) => ({
                  value: option,
                  label: option,
                  icon: FREQUENCY_ICONS[option],
                  // Each option previews itself against the interval already
                  // entered, so the sheet answers "what would this mean?".
                  description: describeRecurrence(option, interval),
                }))}
                error={errors.frequency?.message}
              />
            )}
          />

          <Controller
            control={control}
            name="interval"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Repeat every"
                // Held as a number so the schema validates it directly. An empty
                // field becomes NaN, which zod reports as "Enter a number"
                // instead of silently passing as zero.
                value={Number.isFinite(value) ? String(value) : ''}
                onChangeText={(text) => {
                  const digits = text.replace(/[^0-9]/g, '');
                  onChange(digits === '' ? Number.NaN : Number(digits));
                }}
                onBlur={onBlur}
                keyboardType="number-pad"
                error={errors.interval?.message}
                hint={describeRecurrence(frequency, interval)}
                placeholder="1"
                maxLength={3}
                returnKeyType="done"
              />
            )}
          />

          <Controller
            control={control}
            name="startDate"
            render={({ field: { onChange, value } }) => (
              <DateTimeField
                label="Starts on"
                value={value}
                onChange={onChange}
                mode="date"
                error={errors.startDate?.message}
              />
            )}
          />

          <View>
            <View
              style={[
                styles.switchRow,
                { minHeight: theme.hitTarget.min, gap: theme.spacing.md },
              ]}
            >
              <View style={styles.flex}>
                <AppText variant="bodySmallStrong" color="textSecondary">
                  End date
                </AppText>
                <AppText variant="caption" color="textTertiary">
                  Without one it runs until you pause or delete it
                </AppText>
              </View>

              <Switch
                value={hasEndDate}
                onValueChange={toggleEndDate}
                accessibilityLabel="Set an end date"
                trackColor={{ false: theme.c.track, true: theme.c.primary }}
                thumbColor={theme.c.surface}
              />
            </View>

            {hasEndDate ? (
              <Controller
                control={control}
                name="endDate"
                render={({ field: { onChange, value } }) => (
                  <DateTimeField
                    label="Ends on"
                    value={value ?? addYears(startDate ?? new Date(), 1)}
                    onChange={onChange}
                    mode="date"
                    minimumDate={startDate}
                    error={errors.endDate?.message}
                  />
                )}
              />
            ) : null}
          </View>

          <Controller
            control={control}
            name="reminderDaysBefore"
            render={({ field: { onChange, value } }) => (
              <OptionPicker
                label="Remind me"
                value={String(value)}
                onChange={(next) => onChange(Number(next))}
                options={reminderOptions}
                error={errors.reminderDaysBefore?.message}
              />
            )}
          />
        </View>

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
              name="merchant"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Merchant"
                  placeholder={type === 'Income' ? 'Who pays you?' : 'Who gets paid?'}
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  error={errors.merchant?.message}
                  autoCapitalize="words"
                />
              )}
            />

            <Controller
              control={control}
              name="description"
              render={({ field: { onChange, onBlur, value } }) => (
                <AppInput
                  label="Description"
                  placeholder="Copied onto every transaction this creates"
                  value={value ?? ''}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  error={errors.description?.message}
                />
              )}
            />
          </View>
        ) : null}

        {footer}

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

      {/* Pinned so the primary action stays under a thumb however far the form
          has been scrolled. */}
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
  switchRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  disclosure: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
  },
});
