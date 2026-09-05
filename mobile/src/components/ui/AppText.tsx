import { Text as RNText, type TextProps as RNTextProps, type TextStyle } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ColorTokens, TypographyToken } from '@/theme/tokens';

export type AppTextProps = RNTextProps & {
  /** Typography scale entry. Defaults to `body`. */
  variant?: TypographyToken;
  /** Semantic colour token. Defaults to `textPrimary`. */
  color?: keyof ColorTokens;
  align?: TextStyle['textAlign'];
  /** Overrides the weight from the variant, for one-off emphasis. */
  weight?: TextStyle['fontWeight'];
};

/**
 * Every piece of text in the app.
 *
 * Going through one component means no screen can invent a font size or a hex
 * colour, which is what keeps the type scale coherent and dark mode complete.
 *
 * `allowFontScaling` is deliberately left at its default (on) so the OS
 * dynamic-type setting is respected. `maxFontSizeMultiplier` caps it at 1.6:
 * without a cap, a user at the largest accessibility size gets text that
 * overflows every card and truncates the amounts they came here to read.
 */
export function AppText({
  variant = 'body',
  color = 'textPrimary',
  align,
  weight,
  style,
  maxFontSizeMultiplier = 1.6,
  ...rest
}: AppTextProps) {
  const theme = useAppTheme();
  const scale = theme.typography[variant];

  return (
    <RNText
      maxFontSizeMultiplier={maxFontSizeMultiplier}
      style={[
        {
          fontSize: scale.fontSize,
          lineHeight: scale.lineHeight,
          fontWeight: weight ?? (scale.fontWeight as TextStyle['fontWeight']),
          letterSpacing: 'letterSpacing' in scale ? scale.letterSpacing : undefined,
          color: theme.c[color],
          textAlign: align,
        },
        style,
      ]}
      {...rest}
    />
  );
}
