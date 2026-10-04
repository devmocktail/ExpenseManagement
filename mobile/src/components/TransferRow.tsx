import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { memo } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Transfer } from '@/types/api';
import { formatCurrency } from '@/utils/currency';
import { formatTimeOfDay } from '@/utils/date';

export type TransferRowProps = {
  transfer: Transfer;
  onPress?: (transfer: Transfer) => void;
  /** Hides the time inside a date-grouped list, where it would be redundant. */
  hideDate?: boolean;
};

/**
 * One row in the transfers list.
 *
 * The amount is shown without a sign and in the ordinary text colour, unlike
 * a transaction's red or green. That is the point of the row: money moved
 * between two of your own pots, so it was neither spent nor earned, and
 * colouring it like spending would undo the distinction the whole feature
 * exists to make.
 */
export const TransferRow = memo(function TransferRow({
  transfer,
  onPress,
  hideDate = false,
}: TransferRowProps) {
  const theme = useAppTheme();

  const subtitleParts = [`${transfer.fromAccountName} → ${transfer.toAccountName}`];
  if (!hideDate) subtitleParts.push(formatTimeOfDay(transfer.transferDate));

  // One label for the whole row, saying "to" and "from" in words: an arrow
  // glyph is announced inconsistently across screen readers, and direction is
  // the only thing that distinguishes two otherwise identical transfers.
  const accessibilityLabel = [
    'Transfer',
    formatCurrency(transfer.amount, transfer.currencyCode),
    `from ${transfer.fromAccountName}`,
    `to ${transfer.toAccountName}`,
  ].join(', ');

  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : 'text'}
      accessibilityLabel={accessibilityLabel}
      onPress={onPress ? () => onPress(transfer) : undefined}
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
      <View
        style={[
          styles.icon,
          { backgroundColor: theme.c.primaryMuted, borderRadius: theme.radius.medium },
        ]}
      >
        <MaterialCommunityIcons name="swap-horizontal" size={20} color={theme.c.primary} />
      </View>

      <View style={styles.body}>
        <AppText variant="bodyStrong" numberOfLines={1}>
          {transfer.notes?.trim() || 'Transfer'}
        </AppText>

        <AppText
          variant="caption"
          color="textSecondary"
          numberOfLines={1}
          style={{ marginTop: 2 }}
        >
          {subtitleParts.join(' · ')}
        </AppText>
      </View>

      <AppText variant="bodyStrong" style={styles.amount}>
        {formatCurrency(transfer.amount, transfer.currencyCode)}
      </AppText>
    </Pressable>
  );
});

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  icon: {
    width: 40,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  body: {
    flex: 1,
    justifyContent: 'center',
  },
  amount: {
    textAlign: 'right',
  },
});
