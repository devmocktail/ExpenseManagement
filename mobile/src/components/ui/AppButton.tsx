import * as Haptics from 'expo-haptics';
import { useCallback } from 'react';
import {
  ActivityIndicator,
  Platform,
  Pressable,
  StyleSheet,
  View,
  type PressableProps,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';

export type AppButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
export type AppButtonSize = 'small' | 'medium' | 'large';

export type AppButtonProps = Omit<PressableProps, 'style' | 'children'> & {
  label: string;
  variant?: AppButtonVariant;
  size?: AppButtonSize;
  loading?: boolean;
  /** Stretches to the width of the parent. */
  fullWidth?: boolean;
  leadingIcon?: React.ReactNode;
  trailingIcon?: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  /** Fires a light impact on press. Off for buttons pressed repeatedly. */
  haptic?: boolean;
};

/**
 * The app's button.
 *
 * Notable behaviours:
 *  - While `loading`, the label stays mounted but is hidden behind the spinner
 *    so the button does not change width mid-press, which reads as a layout jump.
 *  - `disabled` covers loading too, so a double tap cannot fire a mutation twice.
 *  - The touch target is never smaller than 44pt regardless of `size`.
 */
export function AppButton({
  label,
  variant = 'primary',
  size = 'medium',
  loading = false,
  fullWidth = false,
  leadingIcon,
  trailingIcon,
  disabled,
  style,
  haptic = true,
  onPress,
  ...rest
}: AppButtonProps) {
  const theme = useAppTheme();
  const isDisabled = disabled || loading;

  const handlePress = useCallback<NonNullable<PressableProps['onPress']>>(
    (event) => {
      // Haptics are a no-op on unsupported hardware, but the promise still
      // rejects on web — swallow it rather than producing an unhandled rejection.
      if (haptic && Platform.OS !== 'web') {
        Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});
      }
      onPress?.(event);
    },
    [haptic, onPress],
  );

  const sizing = {
    small: { height: 40, paddingHorizontal: theme.spacing.base, textVariant: 'bodySmallStrong' as const },
    medium: { height: 48, paddingHorizontal: theme.spacing.lg, textVariant: 'button' as const },
    large: { height: 56, paddingHorizontal: theme.spacing.xl, textVariant: 'button' as const },
  }[size];

  const palette = {
    primary: {
      background: theme.c.primary,
      border: 'transparent',
      text: 'onPrimary' as const,
    },
    secondary: {
      background: theme.c.primaryMuted,
      border: 'transparent',
      text: 'primary' as const,
    },
    outline: {
      background: 'transparent',
      border: theme.c.borderStrong,
      text: 'textPrimary' as const,
    },
    ghost: {
      background: 'transparent',
      border: 'transparent',
      text: 'primary' as const,
    },
    danger: {
      background: theme.c.error,
      border: 'transparent',
      text: 'onPrimary' as const,
    },
  }[variant];

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      // Announces the disabled and busy states to screen readers, which colour
      // and a spinner alone do not convey.
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      disabled={isDisabled}
      onPress={handlePress}
      hitSlop={size === 'small' ? 6 : 0}
      style={({ pressed }) => [
        styles.base,
        {
          height: Math.max(sizing.height, theme.hitTarget.min),
          paddingHorizontal: sizing.paddingHorizontal,
          backgroundColor: palette.background,
          borderColor: palette.border,
          borderWidth: variant === 'outline' ? StyleSheet.hairlineWidth * 2 : 0,
          borderRadius: theme.radius.medium,
          opacity: isDisabled ? theme.opacity.disabled : pressed ? theme.opacity.pressed : 1,
          alignSelf: fullWidth ? 'stretch' : 'flex-start',
          width: fullWidth ? '100%' : undefined,
        },
        variant === 'primary' && !isDisabled ? theme.elevation.low : null,
        style,
      ]}
      {...rest}
    >
      <View style={styles.content}>
        {leadingIcon ? <View style={{ marginRight: theme.spacing.sm }}>{leadingIcon}</View> : null}

        <AppText
          variant={sizing.textVariant}
          color={palette.text}
          numberOfLines={1}
          style={{ opacity: loading ? 0 : 1 }}
        >
          {label}
        </AppText>

        {trailingIcon ? <View style={{ marginLeft: theme.spacing.sm }}>{trailingIcon}</View> : null}
      </View>

      {loading ? (
        <View style={StyleSheet.absoluteFill} pointerEvents="none">
          <View style={styles.content}>
            <ActivityIndicator size="small" color={theme.c[palette.text]} />
          </View>
        </View>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    justifyContent: 'center',
    alignItems: 'center',
    overflow: 'hidden',
  },
  content: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
