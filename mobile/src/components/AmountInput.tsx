import { useRef } from 'react';
import { Pressable, StyleSheet, TextInput, View } from 'react-native';
import { AppText } from '@/components/ui/AppText';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TransactionType } from '@/types/api';
import { getCurrencySymbol } from '@/utils/currency';

export type AmountInputProps = {
  value: string;
  onChangeText: (value: string) => void;
  type: TransactionType;
  currencyCode?: string;
  error?: string;
  autoFocus?: boolean;
};

/**
 * The hero amount field on the add/edit screen.
 *
 * Everything about it is tuned for one-handed speed, because entering an amount
 * is the single most repeated action in the product:
 *
 *  - Tapping anywhere in the large area focuses the field, not just the caret.
 *  - `decimal-pad` gives the numeric keypad with a decimal separator and no
 *    letters, on both platforms.
 *  - Input is sanitised as it is typed — a second decimal point or a third
 *    decimal place is simply not accepted, so the field can never hold a value
 *    the server would reject.
 */
export function AmountInput({
  value,
  onChangeText,
  type,
  currencyCode,
  error,
  autoFocus = true,
}: AmountInputProps) {
  const theme = useAppTheme();
  const inputRef = useRef<TextInput>(null);
  const userCurrency = useCurrencyCode();

  const symbol = getCurrencySymbol(currencyCode ?? userCurrency);
  const accent = type === 'Income' ? theme.c.income : theme.c.textPrimary;

  const handleChange = (raw: string) => {
    // Strip everything that is not a digit or a separator, and normalise a
    // comma decimal (some locales' keypads emit one) to a period.
    let next = raw.replace(/[^0-9.,]/g, '').replace(/,/g, '.');

    // Keep only the first decimal point: "12.3.4" becomes "12.34".
    const firstDot = next.indexOf('.');
    if (firstDot !== -1) {
      next = next.slice(0, firstDot + 1) + next.slice(firstDot + 1).replace(/\./g, '');
    }

    // Money has at most two decimal places, so a third keystroke is ignored
    // rather than accepted and rounded away later.
    const [whole, fraction] = next.split('.');
    if (fraction !== undefined && fraction.length > 2) {
      next = `${whole}.${fraction.slice(0, 2)}`;
    }

    // Trim a leading zero unless the user is typing "0." — otherwise "05"
    // becomes possible and looks like a bug.
    if (whole && whole.length > 1 && whole.startsWith('0')) {
      next = whole.replace(/^0+/, '') + (fraction !== undefined ? `.${fraction}` : '');
      if (next.startsWith('.')) next = `0${next}`;
    }

    onChangeText(next);
  };

  return (
    <Pressable
      onPress={() => inputRef.current?.focus()}
      accessibilityLabel={`Amount in ${currencyCode ?? userCurrency}`}
      accessibilityHint="Enter the amount for this transaction"
      style={styles.container}
    >
      <AppText variant="caption" color="textSecondary">
        {type === 'Income' ? 'Income amount' : 'Expense amount'}
      </AppText>

      <View style={[styles.row, { marginTop: theme.spacing.sm }]}>
        <AppText
          variant="heading1"
          style={{ color: accent, opacity: value ? 1 : 0.45 }}
        >
          {symbol}
        </AppText>

        <TextInput
          ref={inputRef}
          value={value}
          onChangeText={handleChange}
          autoFocus={autoFocus}
          keyboardType="decimal-pad"
          inputMode="decimal"
          placeholder="0"
          placeholderTextColor={theme.c.textTertiary}
          selectionColor={theme.c.primary}
          maxLength={15}
          style={[
            styles.input,
            {
              color: accent,
              fontSize: theme.typography.display.fontSize,
              lineHeight: theme.typography.display.lineHeight,
              fontWeight: '700',
            },
          ]}
        />
      </View>

      {error ? (
        <AppText
          variant="caption"
          color="error"
          style={{ marginTop: theme.spacing.xs }}
          accessibilityLiveRegion="polite"
        >
          {error}
        </AppText>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    paddingVertical: 12,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'center',
    gap: 6,
    // Stretched so the row is bounded by the screen rather than sized to its
    // content. Left to size itself, the row can grow past the viewport and,
    // because it is centre-aligned, the overflow spills off BOTH edges — which
    // pushed the currency symbol off-screen entirely while the number still
    // looked fine.
    alignSelf: 'stretch',
    paddingHorizontal: 16,
  },
  input: {
    // flexShrink is the load-bearing part. On web, react-native-web renders a
    // real <input>, which carries the HTML intrinsic width of roughly 20
    // characters (~520px) regardless of its content. Without permission to
    // shrink it refuses to fit a phone-width row.
    flexShrink: 1,
    minWidth: 60,
    textAlign: 'center',
    padding: 0,
  },
});
