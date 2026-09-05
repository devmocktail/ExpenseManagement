import { useEffect } from 'react';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withTiming,
} from 'react-native-reanimated';
import { CurrencyText } from '@/components/CurrencyText';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ColorTokens } from '@/theme/tokens';
import type { BudgetStatus } from '@/types/api';

export type BudgetProgressProps = {
  spent: number;
  budget: number;
  percentage: number;
  status: BudgetStatus;
  currencyCode?: string;
  label?: string;
  /** Trailing metadata, e.g. "4 days left". */
  meta?: string;
  /** Compact form for a dense list. */
  dense?: boolean;
  style?: StyleProp<ViewStyle>;
};

/**
 * A budget's usage bar.
 *
 * Status is never communicated by the bar colour alone — a text badge
 * ("Over budget", "Nearly there") sits beside the percentage, which is what
 * makes it readable for anyone who cannot distinguish amber from green, and
 * what a screen reader announces.
 *
 * The bar is clamped to 100% width while the *number* is allowed past it, so an
 * over-budget user sees "142%" rather than a bar that has silently run off the
 * edge of the card.
 */
export function BudgetProgress({
  spent,
  budget,
  percentage,
  status,
  currencyCode,
  label,
  meta,
  dense = false,
  style,
}: BudgetProgressProps) {
  const theme = useAppTheme();

  const statusStyles: Record<BudgetStatus, { color: keyof ColorTokens; text: string }> = {
    ok: { color: 'success', text: 'On track' },
    warning: { color: 'warning', text: 'Approaching limit' },
    critical: { color: 'warning', text: 'Nearly there' },
    exceeded: { color: 'error', text: 'Over budget' },
  };

  const { color, text } = statusStyles[status] ?? statusStyles.ok;

  const width = useSharedValue(0);
  const clamped = Math.min(Math.max(percentage, 0), 100);

  useEffect(() => {
    width.value = withTiming(clamped, { duration: theme.motion.slow });
  }, [clamped, theme.motion.slow, width]);

  const barStyle = useAnimatedStyle(() => ({ width: `${width.value}%` }));

  const remaining = budget - spent;

  return (
    <View style={style}>
      {label || meta ? (
        <View style={[styles.header, { marginBottom: theme.spacing.sm }]}>
          {label ? (
            <AppText variant={dense ? 'bodySmallStrong' : 'bodyStrong'} numberOfLines={1} style={styles.flex}>
              {label}
            </AppText>
          ) : (
            <View style={styles.flex} />
          )}
          {meta ? (
            <AppText variant="caption" color="textSecondary">
              {meta}
            </AppText>
          ) : null}
        </View>
      ) : null}

      <View style={[styles.amounts, { marginBottom: theme.spacing.sm }]}>
        <View style={styles.row}>
          <CurrencyText
            amount={spent}
            currencyCode={currencyCode}
            variant={dense ? 'bodySmallStrong' : 'bodyStrong'}
          />
          <AppText variant={dense ? 'bodySmall' : 'body'} color="textSecondary">
            {' / '}
          </AppText>
          <CurrencyText
            amount={budget}
            currencyCode={currencyCode}
            variant={dense ? 'bodySmall' : 'body'}
            color="textSecondary"
          />
        </View>

        <AppText variant={dense ? 'caption' : 'bodySmallStrong'} color={color}>
          {Math.round(percentage)}%
        </AppText>
      </View>

      <View
        accessibilityRole="progressbar"
        accessibilityValue={{ min: 0, max: 100, now: Math.round(percentage) }}
        accessibilityLabel={`${label ? `${label}: ` : ''}${Math.round(percentage)}% of budget used. ${text}.`}
        style={{
          height: dense ? 6 : 8,
          borderRadius: theme.radius.pill,
          backgroundColor: theme.c.track,
          overflow: 'hidden',
        }}
      >
        <Animated.View
          style={[
            barStyle,
            {
              height: '100%',
              borderRadius: theme.radius.pill,
              backgroundColor: theme.c[color],
            },
          ]}
        />
      </View>

      <View style={[styles.footer, { marginTop: theme.spacing.sm }]}>
        <AppText variant="caption" color={color}>
          {text}
        </AppText>

        <View style={styles.row}>
          <AppText variant="caption" color="textSecondary">
            {remaining >= 0 ? 'Remaining ' : 'Over by '}
          </AppText>
          <CurrencyText
            amount={Math.abs(remaining)}
            currencyCode={currencyCode}
            variant="caption"
            color={remaining >= 0 ? 'textSecondary' : 'error'}
          />
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  amounts: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    justifyContent: 'space-between',
  },
  footer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  flex: {
    flex: 1,
  },
});
