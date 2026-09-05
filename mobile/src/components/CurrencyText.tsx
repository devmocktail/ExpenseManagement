import type { StyleProp, TextStyle } from 'react-native';
import { AppText } from '@/components/ui/AppText';
import { useCurrencyCode } from '@/store/auth-store';
import type { ColorTokens, TypographyToken } from '@/theme/tokens';
import type { TransactionType } from '@/types/api';
import { formatCurrency, formatSignedAmount } from '@/utils/currency';

export type CurrencyTextProps = {
  amount: number;
  /** Overrides the signed-in user's currency. Rarely needed. */
  currencyCode?: string;
  /** When set, renders a ledger amount with a leading + or − and a direction colour. */
  type?: TransactionType;
  variant?: TypographyToken;
  /** Overrides the colour that `type` would otherwise choose. */
  color?: keyof ColorTokens;
  compactDecimals?: boolean;
  abbreviate?: boolean;
  style?: StyleProp<TextStyle>;
  numberOfLines?: number;
};

/**
 * Renders money.
 *
 * Two things it guarantees that scattered `formatCurrency` calls would not:
 *
 *  1. The currency comes from the signed-in user's settings, so no screen can
 *     hardcode a symbol (Rule 12).
 *  2. Income and expense are distinguished by an explicit + / − sign as well as
 *     colour, and the accessibility label spells the direction out in words.
 *     Colour alone fails anyone who cannot distinguish green from the default
 *     text colour, which is a hard accessibility requirement here.
 */
export function CurrencyText({
  amount,
  currencyCode,
  type,
  variant = 'body',
  color,
  compactDecimals,
  abbreviate,
  style,
  numberOfLines,
}: CurrencyTextProps) {
  const userCurrency = useCurrencyCode();
  const code = currencyCode ?? userCurrency;

  const text = type
    ? formatSignedAmount(amount, type, code, { compactDecimals, abbreviate })
    : formatCurrency(amount, code, { compactDecimals, abbreviate });

  const resolvedColor: keyof ColorTokens =
    color ?? (type === 'Income' ? 'income' : type === 'Expense' ? 'expense' : 'textPrimary');

  // Screen readers read "-450" as "minus four fifty", which is ambiguous in a
  // ledger. Saying the direction in words removes the ambiguity entirely.
  const accessibilityLabel = type
    ? `${type === 'Income' ? 'Income' : 'Expense'} ${formatCurrency(Math.abs(amount), code)}`
    : undefined;

  return (
    <AppText
      variant={variant}
      color={resolvedColor}
      style={style}
      numberOfLines={numberOfLines}
      accessibilityLabel={accessibilityLabel}
    >
      {text}
    </AppText>
  );
}
