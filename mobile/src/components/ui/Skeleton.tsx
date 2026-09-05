import { useEffect } from 'react';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withTiming,
  type WithTimingConfig,
} from 'react-native-reanimated';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * Skeleton placeholders.
 *
 * Preferred over a spinner for content whose shape is known — a list of
 * transactions, a summary card — because the layout does not jump when the data
 * lands and the wait feels shorter.
 *
 * The pulse runs on the UI thread via Reanimated, so it keeps animating while
 * JavaScript is busy parsing the response that will replace it.
 */

const TIMING: WithTimingConfig = { duration: 850 };

export function Skeleton({
  width,
  height = 16,
  radius,
  style,
}: {
  width?: number | `${number}%`;
  height?: number;
  radius?: number;
  style?: StyleProp<ViewStyle>;
}) {
  const theme = useAppTheme();
  const progress = useSharedValue(0.5);

  useEffect(() => {
    progress.value = withRepeat(withTiming(1, TIMING), -1, true);
  }, [progress]);

  const animatedStyle = useAnimatedStyle(() => ({ opacity: progress.value }));

  return (
    <Animated.View
      // Hidden from screen readers: announcing a row of empty grey boxes is
      // noise. The surrounding container reports the loading state instead.
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[
        {
          width: width ?? '100%',
          height,
          borderRadius: radius ?? theme.radius.small,
          backgroundColor: theme.c.skeleton,
        },
        animatedStyle,
        style,
      ]}
    />
  );
}

/** Matches the geometry of a TransactionRow so the swap is seamless. */
export function TransactionRowSkeleton() {
  const theme = useAppTheme();

  return (
    <View style={[styles.row, { paddingVertical: theme.spacing.md, gap: theme.spacing.md }]}>
      <Skeleton width={44} height={44} radius={theme.radius.medium} />
      <View style={{ flex: 1, gap: 8 }}>
        <Skeleton width="55%" height={15} />
        <Skeleton width="32%" height={12} />
      </View>
      <Skeleton width={72} height={18} />
    </View>
  );
}

export function SummaryCardSkeleton() {
  const theme = useAppTheme();

  return (
    <View
      style={{
        backgroundColor: theme.c.surface,
        borderRadius: theme.radius.large,
        padding: theme.spacing.base,
        gap: theme.spacing.md,
        borderWidth: theme.isDark ? 1 : 0,
        borderColor: theme.c.border,
      }}
    >
      <Skeleton width="38%" height={13} />
      <Skeleton width="62%" height={32} radius={theme.radius.small} />
      <View style={{ flexDirection: 'row', gap: theme.spacing.base }}>
        <Skeleton width="45%" height={16} />
        <Skeleton width="45%" height={16} />
      </View>
    </View>
  );
}

export function ListSkeleton({ count = 6 }: { count?: number }) {
  return (
    <View>
      {Array.from({ length: count }, (_, index) => (
        <TransactionRowSkeleton key={index} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
