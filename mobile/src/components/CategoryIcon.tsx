import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { View, type StyleProp, type ViewStyle } from 'react-native';
import { resolveCategoryIcon } from '@/constants/icons';
import { useAppTheme } from '@/theme/ThemeProvider';

export type CategoryIconProps = {
  icon: string | null | undefined;
  /** Category colour from the server. Falls back to the theme's primary. */
  color?: string | null;
  size?: 'small' | 'medium' | 'large';
  style?: StyleProp<ViewStyle>;
};

const SIZES = {
  small: { box: 32, glyph: 16, radius: 10 },
  medium: { box: 44, glyph: 22, radius: 12 },
  large: { box: 56, glyph: 28, radius: 16 },
} as const;

/**
 * A category's glyph on its own tinted chip.
 *
 * The tint is derived from the category colour at low opacity rather than being
 * a second stored colour, so a user picking a new colour restyles both at once
 * and the pair can never drift out of harmony.
 *
 * Decorative by definition: the category name is always rendered beside it, so
 * announcing the icon would make a screen reader say everything twice.
 */
export function CategoryIcon({ icon, color, size = 'medium', style }: CategoryIconProps) {
  const theme = useAppTheme();
  const dimensions = SIZES[size];

  const tint = color ?? theme.c.primary;

  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[
        {
          width: dimensions.box,
          height: dimensions.box,
          borderRadius: dimensions.radius,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: withAlpha(tint, theme.isDark ? 0.22 : 0.12),
        },
        style,
      ]}
    >
      <MaterialCommunityIcons
        name={resolveCategoryIcon(icon)}
        size={dimensions.glyph}
        color={tint}
      />
    </View>
  );
}

/**
 * Adds an alpha channel to a #RRGGBB string.
 *
 * React Native does not accept 8-digit hex on Android below API 29, so this
 * produces an rgba() string, which every version parses.
 */
export function withAlpha(hex: string, alpha: number): string {
  const normalized = hex.replace('#', '');

  if (normalized.length !== 6) {
    // A malformed colour must not crash a list row; fall back to a neutral tint.
    return `rgba(100, 116, 139, ${alpha})`;
  }

  const r = parseInt(normalized.slice(0, 2), 16);
  const g = parseInt(normalized.slice(2, 4), 16);
  const b = parseInt(normalized.slice(4, 6), 16);

  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
