import { useMemo } from 'react';
import { StyleSheet, View } from 'react-native';
import { PieChart } from 'react-native-gifted-charts';
import { CurrencyText } from '@/components/CurrencyText';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import { chartCategoryColors } from '@/theme/tokens';
import type { CategoryBreakdownItem } from '@/types/api';

export type CategoryDonutProps = {
  data: CategoryBreakdownItem[];
  currencyCode?: string;
  /** Total rendered in the hole. Defaults to the sum of the slices. */
  total?: number;
  size?: number;
};

/**
 * Spending by category.
 *
 * A donut is only readable up to about six slices, so the caller is expected to
 * have folded the tail into an "Other" bucket server-side. The legend beside it
 * is not decoration: it carries the name and the amount for every slice, which
 * is what makes the chart usable without relying on colour.
 */
export function CategoryDonut({ data, currencyCode, total, size = 180 }: CategoryDonutProps) {
  const theme = useAppTheme();

  const slices = useMemo(
    () =>
      data.map((item, index) => ({
        value: item.amount,
        // The server sends the category's own colour; the palette is the
        // fallback so a category created before colours existed still renders
        // distinctly rather than as a run of identical grey wedges.
        color: item.categoryColor || chartCategoryColors[index % chartCategoryColors.length],
        text: item.categoryName,
      })),
    [data],
  );

  const sum = total ?? data.reduce((acc, item) => acc + item.amount, 0);

  if (data.length === 0) {
    return (
      <View style={[styles.empty, { height: size }]}>
        <AppText variant="bodySmall" color="textSecondary" align="center">
          No spending to break down yet
        </AppText>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View
        // The chart itself is decorative — every value it encodes is repeated in
        // the legend, which screen readers can actually navigate.
        accessibilityElementsHidden
        importantForAccessibility="no-hide-descendants"
      >
        <PieChart
          data={slices}
          donut
          radius={size / 2}
          innerRadius={size / 2 - 26}
          innerCircleColor={theme.c.surface}
          strokeWidth={2}
          strokeColor={theme.c.surface}
          centerLabelComponent={() => (
            <View style={styles.center}>
              <AppText variant="caption" color="textSecondary">
                Total
              </AppText>
              <CurrencyText
                amount={sum}
                currencyCode={currencyCode}
                variant="heading3"
                compactDecimals
                abbreviate={sum >= 100_000}
              />
            </View>
          )}
        />
      </View>

      <View style={[styles.legend, { gap: theme.spacing.sm }]}>
        {data.map((item, index) => (
          <View
            key={item.categoryId}
            style={styles.legendRow}
            accessibilityLabel={`${item.categoryName}, ${Math.round(item.percentage)} percent`}
          >
            <View
              style={{
                width: 10,
                height: 10,
                borderRadius: 3,
                backgroundColor:
                  item.categoryColor || chartCategoryColors[index % chartCategoryColors.length],
              }}
            />

            <AppText variant="bodySmall" numberOfLines={1} style={styles.legendName}>
              {item.categoryName}
            </AppText>

            <AppText variant="caption" color="textSecondary">
              {Math.round(item.percentage)}%
            </AppText>
          </View>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
  },
  center: {
    alignItems: 'center',
  },
  legend: {
    marginTop: 20,
    alignSelf: 'stretch',
  },
  legendRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  legendName: {
    flex: 1,
  },
  empty: {
    alignItems: 'center',
    justifyContent: 'center',
  },
});
