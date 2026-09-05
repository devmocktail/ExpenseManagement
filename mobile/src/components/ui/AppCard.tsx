import { Pressable, View, type StyleProp, type ViewStyle } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ElevationToken, RadiusToken, SpacingToken } from '@/theme/tokens';

export type AppCardProps = {
  children: React.ReactNode;
  padding?: SpacingToken;
  radius?: RadiusToken;
  elevation?: ElevationToken;
  /** Draws a hairline border instead of a shadow. Reads better in dark mode. */
  bordered?: boolean;
  onPress?: () => void;
  accessibilityLabel?: string;
  style?: StyleProp<ViewStyle>;
};

/**
 * A surface.
 *
 * The brief warns against putting a card around everything, so this is
 * intentionally plain: one background, one radius, optional single-level
 * elevation. Nesting cards is a design smell — use spacing to group instead.
 *
 * In dark mode a shadow is nearly invisible against a dark ground, so elevation
 * silently degrades to a border there. That keeps the component's contract
 * ("this is a distinct surface") true in both themes.
 */
export function AppCard({
  children,
  padding = 'base',
  radius = 'large',
  elevation = 'low',
  bordered,
  onPress,
  accessibilityLabel,
  style,
}: AppCardProps) {
  const theme = useAppTheme();

  const useBorder = bordered ?? theme.isDark;

  const surface: ViewStyle = {
    backgroundColor: theme.c.surface,
    borderRadius: theme.radius[radius],
    padding: theme.spacing[padding],
    borderWidth: useBorder ? 1 : 0,
    borderColor: theme.c.border,
  };

  const shadow = useBorder ? theme.elevation.none : theme.elevation[elevation];

  if (!onPress) {
    return <View style={[surface, shadow, style]}>{children}</View>;
  }

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      onPress={onPress}
      style={({ pressed }) => [
        surface,
        shadow,
        pressed ? { opacity: theme.opacity.pressed } : null,
        style,
      ]}
    >
      {children}
    </Pressable>
  );
}
