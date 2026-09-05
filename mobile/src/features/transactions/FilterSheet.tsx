import { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { BottomSheet } from '@/components/ui/BottomSheet';
import { OptionPicker } from '@/components/ui/OptionPicker';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { PAYMENT_METHOD_ICONS, PAYMENT_METHOD_LABELS } from '@/constants/icons';
import { useCategories } from '@/features/categories/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { PAYMENT_METHODS, type PaymentMethod } from '@/types/api';
import { parseAmountInput } from '@/utils/currency';
import { Pressable } from 'react-native';

export type TransactionFilters = {
  categoryId?: string;
  from?: string;
  to?: string;
  minAmount?: number;
  maxAmount?: number;
  paymentMethod?: PaymentMethod;
  sortBy?: 'date' | 'amount';
  sortDirection?: 'asc' | 'desc';
};

export type FilterSheetProps = {
  visible: boolean;
  value: TransactionFilters;
  onClose: () => void;
  onApply: (filters: TransactionFilters) => void;
};

type RangePreset = 'all' | '7d' | '30d' | 'thisMonth' | 'lastMonth';

/**
 * Transaction filters.
 *
 * Edits a local draft and only commits it on "Apply". Filtering live as the
 * user types would fire a request per keystroke on the amount fields and make
 * the list flicker under their thumb while they are still deciding.
 *
 * Date ranges are offered as presets rather than two date pickers, because
 * "last 30 days" is what people actually want and picking two dates to express
 * it is four taps and a mistake waiting to happen.
 */
export function FilterSheet({ visible, value, onClose, onApply }: FilterSheetProps) {
  const theme = useAppTheme();
  const { data: categories } = useCategories();

  const [draft, setDraft] = useState<TransactionFilters>(value);
  const [minText, setMinText] = useState(value.minAmount?.toString() ?? '');
  const [maxText, setMaxText] = useState(value.maxAmount?.toString() ?? '');
  const [preset, setPreset] = useState<RangePreset>('all');

  // Re-sync whenever the sheet opens: the parent may have cleared the filters
  // from the empty state while this component stayed mounted.
  useEffect(() => {
    if (visible) {
      setDraft(value);
      setMinText(value.minAmount?.toString() ?? '');
      setMaxText(value.maxAmount?.toString() ?? '');
    }
  }, [visible, value]);

  const applyPreset = (next: RangePreset) => {
    setPreset(next);

    const now = new Date();

    if (next === 'all') {
      setDraft((d) => ({ ...d, from: undefined, to: undefined }));
      return;
    }

    if (next === '7d' || next === '30d') {
      const days = next === '7d' ? 7 : 30;
      const from = new Date(now);
      from.setDate(from.getDate() - days);
      from.setHours(0, 0, 0, 0);
      setDraft((d) => ({ ...d, from: from.toISOString(), to: undefined }));
      return;
    }

    const monthOffset = next === 'thisMonth' ? 0 : -1;
    const from = new Date(now.getFullYear(), now.getMonth() + monthOffset, 1);
    // Exclusive upper bound, matching the API's half-open range — an inclusive
    // end date would drop anything recorded later on the last day.
    const to = new Date(now.getFullYear(), now.getMonth() + monthOffset + 1, 1);

    setDraft((d) => ({ ...d, from: from.toISOString(), to: to.toISOString() }));
  };

  const commit = () => {
    onApply({
      ...draft,
      minAmount: parseAmountInput(minText) ?? undefined,
      maxAmount: parseAmountInput(maxText) ?? undefined,
    });
  };

  const clear = () => {
    setDraft({});
    setMinText('');
    setMaxText('');
    setPreset('all');
    onApply({});
  };

  return (
    <BottomSheet visible={visible} onClose={onClose} title="Filters" snapPercent={0.85}>
      <View style={styles.flex}>
        <ScrollView
          showsVerticalScrollIndicator={false}
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={{ gap: theme.spacing.lg, paddingBottom: theme.spacing.base }}
        >
          <View style={{ gap: theme.spacing.sm }}>
            <AppText variant="bodySmallStrong" color="textSecondary">
              Date range
            </AppText>

            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={{ gap: theme.spacing.sm }}
            >
              {(
                [
                  { value: 'all', label: 'All time' },
                  { value: '7d', label: 'Last 7 days' },
                  { value: '30d', label: 'Last 30 days' },
                  { value: 'thisMonth', label: 'This month' },
                  { value: 'lastMonth', label: 'Last month' },
                ] as const
              ).map((option) => {
                const selected = preset === option.value;

                return (
                  <Pressable
                    key={option.value}
                    accessibilityRole="radio"
                    accessibilityState={{ selected }}
                    accessibilityLabel={option.label}
                    onPress={() => applyPreset(option.value)}
                    style={{
                      paddingHorizontal: theme.spacing.md,
                      paddingVertical: theme.spacing.sm,
                      borderRadius: theme.radius.pill,
                      borderWidth: 1,
                      borderColor: selected ? theme.c.primary : theme.c.border,
                      backgroundColor: selected ? theme.c.primaryMuted : theme.c.surface,
                    }}
                  >
                    <AppText variant="caption" color={selected ? 'primary' : 'textSecondary'}>
                      {option.label}
                    </AppText>
                  </Pressable>
                );
              })}
            </ScrollView>
          </View>

          <View style={{ gap: theme.spacing.sm }}>
            <AppText variant="bodySmallStrong" color="textSecondary">
              Category
            </AppText>

            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={{ gap: theme.spacing.sm }}
            >
              <Pressable
                accessibilityRole="radio"
                accessibilityState={{ selected: !draft.categoryId }}
                accessibilityLabel="All categories"
                onPress={() => setDraft((d) => ({ ...d, categoryId: undefined }))}
                style={{
                  paddingHorizontal: theme.spacing.md,
                  paddingVertical: theme.spacing.sm,
                  borderRadius: theme.radius.pill,
                  borderWidth: 1,
                  borderColor: !draft.categoryId ? theme.c.primary : theme.c.border,
                  backgroundColor: !draft.categoryId ? theme.c.primaryMuted : theme.c.surface,
                  justifyContent: 'center',
                }}
              >
                <AppText variant="caption" color={!draft.categoryId ? 'primary' : 'textSecondary'}>
                  All
                </AppText>
              </Pressable>

              {categories?.map((category) => {
                const selected = draft.categoryId === category.id;

                return (
                  <Pressable
                    key={category.id}
                    accessibilityRole="radio"
                    accessibilityState={{ selected }}
                    accessibilityLabel={category.name}
                    onPress={() =>
                      setDraft((d) => ({
                        ...d,
                        categoryId: selected ? undefined : category.id,
                      }))
                    }
                    style={{
                      flexDirection: 'row',
                      alignItems: 'center',
                      gap: 6,
                      paddingHorizontal: theme.spacing.sm,
                      paddingVertical: 6,
                      borderRadius: theme.radius.pill,
                      borderWidth: 1,
                      borderColor: selected ? theme.c.primary : theme.c.border,
                      backgroundColor: selected ? theme.c.primaryMuted : theme.c.surface,
                    }}
                  >
                    <CategoryIcon icon={category.icon} color={category.color} size="small" />
                    <AppText variant="caption" color={selected ? 'primary' : 'textSecondary'}>
                      {category.name}
                    </AppText>
                  </Pressable>
                );
              })}
            </ScrollView>
          </View>

          <View style={{ gap: theme.spacing.sm }}>
            <AppText variant="bodySmallStrong" color="textSecondary">
              Amount range
            </AppText>

            <View style={[styles.row, { gap: theme.spacing.md }]}>
              <AppInput
                placeholder="Min"
                value={minText}
                onChangeText={setMinText}
                keyboardType="decimal-pad"
                containerStyle={styles.flex}
              />
              <AppInput
                placeholder="Max"
                value={maxText}
                onChangeText={setMaxText}
                keyboardType="decimal-pad"
                containerStyle={styles.flex}
              />
            </View>
          </View>

          <OptionPicker
            label="Payment method"
            value={draft.paymentMethod}
            placeholder="Any method"
            onChange={(method) => setDraft((d) => ({ ...d, paymentMethod: method }))}
            options={PAYMENT_METHODS.map((method) => ({
              value: method,
              label: PAYMENT_METHOD_LABELS[method] ?? method,
              icon: PAYMENT_METHOD_ICONS[method],
            }))}
          />

          <View style={{ gap: theme.spacing.sm }}>
            <AppText variant="bodySmallStrong" color="textSecondary">
              Sort by
            </AppText>

            <SegmentedControl
              value={draft.sortBy ?? 'date'}
              onChange={(next) => setDraft((d) => ({ ...d, sortBy: next as 'date' | 'amount' }))}
              options={[
                { value: 'date', label: 'Date' },
                { value: 'amount', label: 'Amount' },
              ]}
              dense
            />

            <SegmentedControl
              value={draft.sortDirection ?? 'desc'}
              onChange={(next) =>
                setDraft((d) => ({ ...d, sortDirection: next as 'asc' | 'desc' }))
              }
              options={[
                { value: 'desc', label: 'Highest first' },
                { value: 'asc', label: 'Lowest first' },
              ]}
              dense
            />
          </View>
        </ScrollView>

        <View
          style={[
            styles.row,
            {
              gap: theme.spacing.md,
              paddingTop: theme.spacing.md,
              borderTopWidth: 1,
              borderTopColor: theme.c.border,
            },
          ]}
        >
          <AppButton label="Clear all" variant="outline" onPress={clear} style={styles.flex} />
          <AppButton label="Apply" onPress={commit} style={styles.flex} />
        </View>
      </View>
    </BottomSheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: { flexDirection: 'row', alignItems: 'center' },
});
