import {
  MD3DarkTheme,
  MD3LightTheme,
  configureFonts,
  type MD3Theme,
} from 'react-native-paper';
import {
  darkColors,
  elevation,
  hitTarget,
  lightColors,
  motion,
  opacity,
  radius,
  spacing,
  typography,
  type ColorTokens,
} from './tokens';

/**
 * The theme object every component reads through `useAppTheme()`.
 *
 * It extends React Native Paper's MD3 theme rather than replacing it, so Paper's
 * own components (TextInput, Snackbar, Dialog) pick up the palette automatically
 * and never need to be individually restyled.
 */
export type AppTheme = MD3Theme & {
  /** Our semantic tokens. Prefer these over `theme.colors` from MD3. */
  c: ColorTokens;
  spacing: typeof spacing;
  radius: typeof radius;
  typography: typeof typography;
  elevation: typeof elevation;
  hitTarget: typeof hitTarget;
  motion: typeof motion;
  opacity: typeof opacity;
  isDark: boolean;
};

/**
 * Maps our tokens onto the MD3 colour slots Paper expects. Paper has far more
 * slots than we design against; the ones we do not use meaningfully are still
 * pointed at a sensible token so a stray Paper component never renders an
 * off-palette colour.
 */
function toPaperColors(c: ColorTokens, base: MD3Theme['colors']): MD3Theme['colors'] {
  return {
    ...base,
    primary: c.primary,
    onPrimary: c.onPrimary,
    primaryContainer: c.primaryMuted,
    onPrimaryContainer: c.primaryDark,

    secondary: c.info,
    onSecondary: c.onPrimary,
    secondaryContainer: c.infoMuted,
    onSecondaryContainer: c.info,

    tertiary: c.success,
    onTertiary: c.onPrimary,
    tertiaryContainer: c.successMuted,
    onTertiaryContainer: c.success,

    error: c.error,
    onError: c.onPrimary,
    errorContainer: c.errorMuted,
    onErrorContainer: c.error,

    background: c.background,
    onBackground: c.textPrimary,
    surface: c.surface,
    onSurface: c.textPrimary,
    surfaceVariant: c.surfaceSunken,
    onSurfaceVariant: c.textSecondary,
    surfaceDisabled: c.surfaceSunken,
    onSurfaceDisabled: c.textTertiary,

    outline: c.border,
    outlineVariant: c.borderStrong,
    backdrop: c.backdrop,

    inverseSurface: c.textPrimary,
    inverseOnSurface: c.background,
    inversePrimary: c.primaryDark,

    elevation: {
      level0: 'transparent',
      level1: c.surface,
      level2: c.surfaceElevated,
      level3: c.surfaceElevated,
      level4: c.surfaceElevated,
      level5: c.surfaceElevated,
    },
  };
}

/**
 * Paper sizes its own text from this config. We keep it aligned with our
 * typography scale so a Paper `Button` label and our `AppText` never sit at
 * subtly different sizes next to each other.
 */
const fontConfig = configureFonts({
  config: {
    // System font on both platforms: it ships with the OS, respects the user's
    // dynamic-type setting, and costs nothing to load.
    fontFamily: undefined,
  },
});

const shared = {
  spacing,
  radius,
  typography,
  elevation,
  hitTarget,
  motion,
  opacity,
  roundness: radius.medium,
  fonts: fontConfig,
} as const;

export const appLightTheme: AppTheme = {
  ...MD3LightTheme,
  ...shared,
  dark: false,
  isDark: false,
  c: lightColors,
  colors: toPaperColors(lightColors, MD3LightTheme.colors),
};

export const appDarkTheme: AppTheme = {
  ...MD3DarkTheme,
  ...shared,
  dark: true,
  isDark: true,
  c: darkColors,
  colors: toPaperColors(darkColors, MD3DarkTheme.colors),
};
