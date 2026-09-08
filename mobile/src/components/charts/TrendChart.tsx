import { useMemo } from 'react';
import { useWindowDimensions, View } from 'react-native';
import { BarChart, LineChart } from 'react-native-gifted-charts';
import { AppText } from '@/components/ui/AppText';
import { useCurrencyCode } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TrendPoint } from '@/types/api';
import { formatCurrency } from '@/utils/currency';

const INITIAL_SPACING = 8;
const END_SPACING = 8;
/** Keeps bars from touching when a period has many points. */
const MIN_SPACING = 2;
/** Below this a bar stops reading as a bar and becomes a hairline. */
const MIN_BAR_WIDTH = 3;

export type TrendChartProps = {
  data: TrendPoint[];
  /** `expense` plots one series; `comparison` plots income against expense. */
  mode?: 'expense' | 'comparison';
  variant?: 'line' | 'bar';
  height?: number;
};

/**
 * Spending over time.
 *
 * Sizing is computed from the window width rather than fixed, because a chart
 * tuned for a 390pt iPhone overflows a 360pt Android phone and looks lost on a
 * 430pt Pro Max. The label stride thins the x-axis on narrow screens so ticks
 * never collide into unreadable mush — which is the single most common way a
 * mobile chart becomes useless.
 */
export function TrendChart({
  data,
  mode = 'expense',
  variant = 'line',
  height = 180,
}: TrendChartProps) {
  const theme = useAppTheme();
  const currencyCode = useCurrencyCode();
  const { width: windowWidth } = useWindowDimensions();

  // Card padding either side, plus room for the y-axis labels.
  const chartWidth = Math.max(windowWidth - theme.spacing.base * 2 - theme.spacing.base * 2 - 40, 220);

  const { primarySeries, secondarySeries, maxValue, hasData, barWidth } = useMemo(() => {
    // gifted-charts treats `spacing` as the gap BETWEEN points, so a bar
    // occupies barWidth + spacing. Dividing the width by the point count alone
    // therefore overshoots by one bar width per point: a seven-day bar chart
    // ran 106px past its card, which put the most recent day — the one that
    // usually has today's data — off the right edge entirely, drawn but never
    // visible.
    //
    // Comparison mode interleaves two series, so twice as many bars share the
    // width. At a month's worth of points the preferred bar width alone
    // overruns the card, and no amount of shrinking the gaps recovers it — so
    // the bars themselves have to narrow. Below MIN_BAR_WIDTH they would stop
    // reading as bars, and the period selector is the real answer at that
    // density.
    const slots = (mode === 'comparison' ? data.length * 2 : data.length) || 1;
    const usable = Math.max(chartWidth - INITIAL_SPACING - END_SPACING, 0);
    const perSlot = usable / slots;

    const preferredBar = mode === 'comparison' ? 8 : 14;
    const resolvedBar = Math.max(Math.min(preferredBar, perSlot - MIN_SPACING), MIN_BAR_WIDTH);

    const spacingPerPoint =
      variant === 'bar'
        ? Math.max(perSlot - resolvedBar, MIN_SPACING)
        : // A line's points have no width of their own; n points make n-1 gaps.
          Math.max(usable / Math.max(slots - 1, 1), MIN_SPACING);

    // Show roughly six labels regardless of how many points there are.
    const stride = Math.max(1, Math.ceil(data.length / 6));

    const expense = data.map((point, index) => ({
      value: point.expense,
      label: index % stride === 0 ? point.label : '',
      labelTextStyle: { color: theme.c.textTertiary, fontSize: 10 },
      frontColor: theme.c.primary,
      spacing: spacingPerPoint,
    }));

    const income = data.map((point, index) => ({
      value: point.income,
      label: index % stride === 0 ? point.label : '',
      labelTextStyle: { color: theme.c.textTertiary, fontSize: 10 },
      frontColor: theme.c.success,
      spacing: spacingPerPoint,
    }));

    const peak = Math.max(
      ...data.map((p) => (mode === 'comparison' ? Math.max(p.income, p.expense) : p.expense)),
      0,
    );

    return {
      primarySeries: expense,
      secondarySeries: income,
      // A flat-zero series would collapse the y-axis to nothing and render an
      // empty box; a nominal ceiling keeps the baseline visible.
      maxValue: peak > 0 ? peak * 1.15 : 100,
      hasData: data.some((p) => p.income > 0 || p.expense > 0),
      barWidth: resolvedBar,
    };
  }, [data, chartWidth, mode, variant, theme]);

  if (data.length === 0) {
    return (
      <View style={{ height, alignItems: 'center', justifyContent: 'center' }}>
        <AppText variant="bodySmall" color="textSecondary">
          Not enough data to chart yet
        </AppText>
      </View>
    );
  }

  const axisLabelFormatter = (value: string) => {
    const numeric = Number(value);
    if (!Number.isFinite(numeric)) return value;
    return formatCurrency(numeric, currencyCode, {
      abbreviate: true,
      compactDecimals: true,
      hideSymbol: false,
    });
  };

  const shared = {
    height,
    width: chartWidth,
    maxValue,
    noOfSections: 4,
    yAxisTextStyle: { color: theme.c.textTertiary, fontSize: 10 },
    yAxisColor: 'transparent',
    xAxisColor: theme.c.border,
    rulesColor: theme.c.border,
    rulesType: 'dashed' as const,
    yAxisLabelWidth: 44,
    formatYLabel: axisLabelFormatter,
    initialSpacing: INITIAL_SPACING,
    endSpacing: END_SPACING,
  };

  return (
    <View>
      <View
        // Every value here is also available as a number elsewhere on the
        // screen (totals, category list), so the chart is presentational.
        accessibilityElementsHidden
        importantForAccessibility="no-hide-descendants"
      >
        {variant === 'bar' ? (
          <BarChart
            {...shared}
            data={mode === 'comparison' ? interleave(secondarySeries, primarySeries) : primarySeries}
            barWidth={barWidth}
            barBorderRadius={4}
            // No chart-level `spacing`: every datum carries its own, computed
            // above from the available width. A default here would be a second
            // source of truth for the same number.
            frontColor={theme.c.primary}
          />
        ) : (
          <LineChart
            {...shared}
            data={primarySeries}
            data2={mode === 'comparison' ? secondarySeries : undefined}
            color={theme.c.primary}
            color2={theme.c.success}
            thickness={2.5}
            thickness2={2.5}
            curved
            hideDataPoints={data.length > 14}
            dataPointsColor={theme.c.primary}
            dataPointsColor2={theme.c.success}
            areaChart={mode !== 'comparison'}
            startFillColor={theme.c.primary}
            startOpacity={0.18}
            endOpacity={0.01}
          />
        )}
      </View>

      {mode === 'comparison' ? (
        <View
          style={{
            flexDirection: 'row',
            justifyContent: 'center',
            gap: theme.spacing.lg,
            marginTop: theme.spacing.sm,
          }}
        >
          <Legend color={theme.c.success} label="Income" />
          <Legend color={theme.c.primary} label="Expenses" />
        </View>
      ) : null}

      {!hasData ? (
        <AppText variant="caption" color="textTertiary" align="center" style={{ marginTop: 8 }}>
          No activity in this period
        </AppText>
      ) : null}
    </View>
  );
}

function Legend({ color, label }: { color: string; label: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
      <View style={{ width: 10, height: 10, borderRadius: 3, backgroundColor: color }} />
      <AppText variant="caption" color="textSecondary">
        {label}
      </AppText>
    </View>
  );
}

/** Pairs the two series into the alternating layout gifted-charts uses for grouped bars. */
function interleave<T>(a: T[], b: T[]): T[] {
  const result: T[] = [];
  for (let i = 0; i < Math.max(a.length, b.length); i += 1) {
    if (a[i]) result.push(a[i]);
    if (b[i]) result.push(b[i]);
  }
  return result;
}
