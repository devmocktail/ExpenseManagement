import { Pressable, StyleSheet, View } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';

export type SegmentedOption = {
  value: string;
  label: string;
};

export type SegmentedControlProps = {
  value: string;
  onChange: (value: string) => void;
  options: SegmentedOption[];
  /** Smaller variant for use inside a card. */
  dense?: boolean;
};

/**
 * A two-to-four-way switch.
 *
 * Uses the `tab` accessibility role rather than `button` so a screen reader
 * announces "1 of 3, selected" — a row of plain buttons gives no sense of
 * position or of what is currently active.
 */
export function SegmentedControl({ value, onChange, options, dense }: SegmentedControlProps) {
  const theme = useAppTheme();
  const height = dense ? 34 : 40;

  return (
    <View
      accessibilityRole="tablist"
      style={[
        styles.container,
        {
          backgroundColor: theme.c.surfaceSunken,
          borderRadius: theme.radius.medium,
          padding: 3,
        },
      ]}
    >
      {options.map((option) => {
        const selected = option.value === value;

        return (
          <Pressable
            key={option.value}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            accessibilityLabel={option.label}
            onPress={() => onChange(option.value)}
            style={[
              styles.segment,
              {
                height,
                borderRadius: theme.radius.small,
                backgroundColor: selected ? theme.c.surface : 'transparent',
              },
              selected && !theme.isDark ? theme.elevation.low : null,
            ]}
          >
            <AppText
              variant={dense ? 'caption' : 'bodySmallStrong'}
              color={selected ? 'textPrimary' : 'textSecondary'}
              numberOfLines={1}
            >
              {option.label}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
  },
  segment: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
