import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { memo } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { CurrencyText } from '@/components/CurrencyText';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Transaction } from '@/types/api';
import { formatCurrency } from '@/utils/currency';
import { formatTimeOfDay } from '@/utils/date';

export type TransactionRowProps = {
  transaction: Transaction;
  onPress?: (transaction: Transaction) => void;
  /** Hides the date line inside a date-grouped list, where it would be redundant. */
  hideDate?: boolean;
};

/**
 * One row in the transaction list.
 *
 * Memoised because this renders hundreds of times in a long list and its props
 * are stable object references from the query cache — without it, every
 * re-render of the list re-renders every row.
 */
export const TransactionRow = memo(function TransactionRow({
  transaction,
  onPress,
  hideDate = false,
}: TransactionRowProps) {
  const theme = useAppTheme();

  const title = transaction.merchant?.trim() || transaction.description?.trim() || transaction.categoryName;

  const subtitleParts = [transaction.categoryName];
  if (!hideDate) subtitleParts.push(formatTimeOfDay(transaction.transactionDate));

  // One label for the whole row rather than four separate announcements, and it
  // states the direction in words because a leading minus sign is not reliably
  // read as "expense".
  const accessibilityLabel = [
    transaction.type === 'Income' ? 'Income' : 'Expense',
    formatCurrency(transaction.amount, transaction.currencyCode),
    title,
    transaction.categoryName,
  ].join(', ');

  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : 'text'}
      accessibilityLabel={accessibilityLabel}
      onPress={onPress ? () => onPress(transaction) : undefined}
      style={({ pressed }) => [
        styles.row,
        {
          paddingVertical: theme.spacing.md,
          gap: theme.spacing.md,
          minHeight: theme.hitTarget.comfortable,
          backgroundColor: pressed ? theme.c.surfaceSunken : 'transparent',
        },
      ]}
    >
      <CategoryIcon icon={transaction.categoryIcon} color={transaction.categoryColor} />

      <View style={styles.body}>
        <AppText variant="bodyStrong" numberOfLines={1}>
          {title}
        </AppText>

        <View style={[styles.subtitle, { gap: theme.spacing.xs }]}>
          <AppText variant="caption" color="textSecondary" numberOfLines={1}>
            {subtitleParts.join(' · ')}
          </AppText>

          {transaction.receipts.length > 0 ? (
            <MaterialCommunityIcons
              name="paperclip"
              size={12}
              color={theme.c.textTertiary}
              // Decorative: the count is in the detail screen, and announcing
              // "paperclip" per row is noise.
              accessibilityElementsHidden
            />
          ) : null}

          {transaction.recurringTransactionId ? (
            <MaterialCommunityIcons
              name="autorenew"
              size={12}
              color={theme.c.textTertiary}
              accessibilityElementsHidden
            />
          ) : null}
        </View>
      </View>

      <CurrencyText
        amount={transaction.amount}
        currencyCode={transaction.currencyCode}
        type={transaction.type}
        variant="bodyStrong"
        // The row's own accessibilityLabel already covers this, so the amount
        // must not be announced a second time.
        style={styles.amount}
      />
    </Pressable>
  );
});

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  body: {
    flex: 1,
    justifyContent: 'center',
  },
  subtitle: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 2,
  },
  amount: {
    textAlign: 'right',
  },
});
