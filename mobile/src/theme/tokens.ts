/**
 * Design tokens.
 *
 * This is the only file in the app that contains a raw colour, a font size or a
 * pixel gap. Everything else consumes these by name, which is what makes dark
 * mode a palette swap rather than an audit of every screen.
 *
 * Colours are named for their ROLE (`surface`, `danger`, `expense`), never for
 * their appearance (`lightGray`, `red`). A role can be re-pointed for dark mode
 * or rebranded without any component knowing.
 */

/** Raw ramps. Referenced only by the semantic palettes below — never by a component. */
const palette = {
  indigo: {
    50: '#EEF2FF',
    100: '#E0E7FF',
    200: '#C7D2FE',
    300: '#A5B4FC',
    400: '#818CF8',
    500: '#6366F1',
    600: '#4F46E5',
    700: '#4338CA',
    800: '#3730A3',
    900: '#312E81',
    950: '#1E1B4B',
  },
  slate: {
    0: '#FFFFFF',
    50: '#F8FAFC',
    100: '#F1F5F9',
    200: '#E2E8F0',
    300: '#CBD5E1',
    400: '#94A3B8',
    500: '#64748B',
    600: '#475569',
    700: '#334155',
    800: '#1E293B',
    900: '#0F172A',
    950: '#020617',
  },
  green: {
    100: '#DCFCE7',
    300: '#86EFAC',
    500: '#22C55E',
    600: '#16A34A',
    700: '#15803D',
    900: '#14532D',
  },
  amber: {
    100: '#FEF3C7',
    300: '#FCD34D',
    500: '#F59E0B',
    600: '#D97706',
    700: '#B45309',
    900: '#78350F',
  },
  red: {
    100: '#FEE2E2',
    300: '#FCA5A5',
    500: '#EF4444',
    600: '#DC2626',
    700: '#B91C1C',
    900: '#7F1D1D',
  },
  sky: {
    100: '#E0F2FE',
    500: '#0EA5E9',
    600: '#0284C7',
    900: '#0C4A6E',
  },
} as const;

export type ColorTokens = {
  /** Brand colour for primary actions and active states. */
  primary: string;
  primaryDark: string;
  /** Tinted background for selected chips, active tabs, subtle emphasis. */
  primaryMuted: string;
  /** Text/icon colour that is legible ON primary. */
  onPrimary: string;

  /** The page behind everything. */
  background: string;
  /** Cards, sheets, inputs — one step forward from the background. */
  surface: string;
  /** A second elevation for menus and sheets stacked over a surface. */
  surfaceElevated: string;
  /** Inset wells: search bars, disabled inputs, chart backdrops. */
  surfaceSunken: string;

  textPrimary: string;
  textSecondary: string;
  /** Placeholders and disabled labels. Never use for meaningful content. */
  textTertiary: string;
  textInverse: string;

  border: string;
  /** A stronger divider for focused inputs and card outlines. */
  borderStrong: string;

  success: string;
  successMuted: string;
  warning: string;
  warningMuted: string;
  error: string;
  errorMuted: string;
  info: string;
  infoMuted: string;

  /**
   * Ledger direction. Deliberately distinct tokens from success/error: money
   * coming in is not "success", and an expense is not an "error". Keeping them
   * separate means restyling alerts never accidentally restyles the ledger.
   */
  income: string;
  incomeMuted: string;
  expense: string;
  expenseMuted: string;

  /** Scrim behind modals and bottom sheets. */
  backdrop: string;
  /** Track colour for progress bars and skeletons. */
  track: string;
  /** Skeleton shimmer highlight. */
  skeleton: string;
};

export const lightColors: ColorTokens = {
  primary: palette.indigo[600],
  primaryDark: palette.indigo[700],
  primaryMuted: palette.indigo[50],
  onPrimary: palette.slate[0],

  background: palette.slate[50],
  surface: palette.slate[0],
  surfaceElevated: palette.slate[0],
  surfaceSunken: palette.slate[100],

  textPrimary: palette.slate[900],
  textSecondary: palette.slate[500],
  textTertiary: palette.slate[400],
  textInverse: palette.slate[0],

  border: palette.slate[200],
  borderStrong: palette.slate[300],

  success: palette.green[600],
  successMuted: palette.green[100],
  warning: palette.amber[600],
  warningMuted: palette.amber[100],
  error: palette.red[600],
  errorMuted: palette.red[100],
  info: palette.sky[600],
  infoMuted: palette.sky[100],

  income: palette.green[600],
  incomeMuted: palette.green[100],
  expense: palette.slate[900],
  expenseMuted: palette.slate[100],

  backdrop: 'rgba(15, 23, 42, 0.45)',
  track: palette.slate[200],
  skeleton: palette.slate[200],
};

/**
 * Dark mode is a true dark (not an inverted light theme): surfaces step UP in
 * lightness as they come forward, matching how elevation reads on OLED, and
 * accents shift lighter so they keep 4.5:1 contrast against a dark ground.
 */
export const darkColors: ColorTokens = {
  primary: palette.indigo[400],
  primaryDark: palette.indigo[300],
  primaryMuted: 'rgba(99, 102, 241, 0.16)',
  onPrimary: palette.indigo[950],

  background: palette.slate[950],
  surface: palette.slate[900],
  surfaceElevated: palette.slate[800],
  surfaceSunken: '#0B1220',

  textPrimary: palette.slate[50],
  textSecondary: palette.slate[400],
  textTertiary: palette.slate[500],
  textInverse: palette.slate[950],

  border: palette.slate[800],
  borderStrong: palette.slate[700],

  success: palette.green[300],
  successMuted: 'rgba(34, 197, 94, 0.18)',
  warning: palette.amber[300],
  warningMuted: 'rgba(245, 158, 11, 0.18)',
  error: palette.red[300],
  errorMuted: 'rgba(239, 68, 68, 0.18)',
  info: '#7DD3FC',
  infoMuted: 'rgba(14, 165, 233, 0.18)',

  income: palette.green[300],
  incomeMuted: 'rgba(34, 197, 94, 0.18)',
  expense: palette.slate[100],
  expenseMuted: 'rgba(148, 163, 184, 0.16)',

  backdrop: 'rgba(2, 6, 23, 0.7)',
  track: palette.slate[800],
  skeleton: palette.slate[800],
};

/**
 * Categorical palette for charts. Ordered so that neighbouring slices in a
 * donut stay distinguishable, and chosen to remain separable for the most
 * common colour-vision deficiencies — a pie chart is the one place in this app
 * where colour genuinely is the encoding, so every chart also labels its data.
 */
export const chartCategoryColors = [
  '#6366F1',
  '#F59E0B',
  '#22C55E',
  '#EC4899',
  '#0EA5E9',
  '#8B5CF6',
  '#F97316',
  '#14B8A6',
  '#EF4444',
  '#84CC16',
  '#06B6D4',
  '#A855F7',
] as const;

/**
 * A 4pt base scale. Every margin, padding and gap in the app is one of these —
 * arbitrary values are how layouts drift out of alignment over time.
 */
export const spacing = {
  none: 0,
  xxs: 2,
  xs: 4,
  sm: 8,
  md: 12,
  base: 16,
  lg: 20,
  xl: 24,
  xxl: 32,
  xxxl: 48,
  huge: 64,
} as const;

export type SpacingToken = keyof typeof spacing;

export const radius = {
  none: 0,
  small: 8,
  medium: 12,
  large: 16,
  xlarge: 24,
  /** Fully rounded. Large enough to round any control height we ship. */
  pill: 999,
} as const;

export type RadiusToken = keyof typeof radius;

export const typography = {
  /** Hero numbers only — the balance on the dashboard, the amount being entered. */
  display: { fontSize: 40, lineHeight: 46, fontWeight: '700' },
  heading1: { fontSize: 28, lineHeight: 34, fontWeight: '700' },
  heading2: { fontSize: 22, lineHeight: 28, fontWeight: '700' },
  heading3: { fontSize: 18, lineHeight: 24, fontWeight: '600' },
  /** Default reading size. 16pt so iOS never zooms an input on focus. */
  body: { fontSize: 16, lineHeight: 24, fontWeight: '400' },
  bodyStrong: { fontSize: 16, lineHeight: 24, fontWeight: '600' },
  bodySmall: { fontSize: 14, lineHeight: 20, fontWeight: '400' },
  bodySmallStrong: { fontSize: 14, lineHeight: 20, fontWeight: '600' },
  /** Metadata and timestamps. The smallest size we allow — nothing below 12pt. */
  caption: { fontSize: 12, lineHeight: 16, fontWeight: '500' },
  /** All-caps section headers in lists. */
  overline: { fontSize: 12, lineHeight: 16, fontWeight: '700', letterSpacing: 0.6 },
  button: { fontSize: 16, lineHeight: 20, fontWeight: '600' },
} as const;

export type TypographyToken = keyof typeof typography;

/**
 * Elevation. iOS gets a soft shadow, Android an elevation value — expressing
 * both here keeps every card looking native on its own platform instead of
 * picking one and living with the other looking wrong.
 */
export const elevation = {
  none: {
    shadowColor: 'transparent',
    shadowOpacity: 0,
    shadowRadius: 0,
    shadowOffset: { width: 0, height: 0 },
    elevation: 0,
  },
  low: {
    shadowColor: '#0F172A',
    shadowOpacity: 0.06,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
  },
  medium: {
    shadowColor: '#0F172A',
    shadowOpacity: 0.1,
    shadowRadius: 14,
    shadowOffset: { width: 0, height: 6 },
    elevation: 6,
  },
  high: {
    shadowColor: '#0F172A',
    shadowOpacity: 0.16,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: 12 },
    elevation: 12,
  },
} as const;

export type ElevationToken = keyof typeof elevation;

/**
 * Minimum interactive sizes. 44pt is Apple's HIG floor and comfortably clears
 * Android's 48dp guidance once padding is included; nothing tappable in this
 * app is allowed to be smaller.
 */
export const hitTarget = {
  min: 44,
  comfortable: 48,
} as const;

/** Durations in ms. Short enough that the UI never feels like it is waiting on itself. */
export const motion = {
  instant: 90,
  fast: 150,
  normal: 220,
  slow: 320,
} as const;

export const opacity = {
  disabled: 0.4,
  pressed: 0.7,
  muted: 0.6,
} as const;
