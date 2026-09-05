import { forwardRef, useState } from 'react';
import {
  Pressable,
  StyleSheet,
  TextInput,
  View,
  type StyleProp,
  type TextInputProps,
  type TextStyle,
  type ViewStyle,
} from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';

export type AppInputProps = Omit<TextInputProps, 'style'> & {
  label?: string;
  /** Validation message. Its presence switches the field to the error style. */
  error?: string;
  /** Guidance shown when there is no error. */
  hint?: string;
  leadingIcon?: React.ReactNode;
  trailingIcon?: React.ReactNode;
  onTrailingIconPress?: () => void;
  trailingIconLabel?: string;
  containerStyle?: StyleProp<ViewStyle>;
  inputStyle?: StyleProp<TextStyle>;
  required?: boolean;
};

/**
 * A labelled text field.
 *
 * Two details that are easy to get wrong and expensive to debug:
 *
 *  - The font size is never below 16pt. Safari and some Android keyboards zoom
 *    the viewport when focusing a smaller input, and the page never zooms back.
 *  - The error is rendered as text, not just a red border. Colour alone fails
 *    both screen readers and anyone who cannot distinguish it, and
 *    `accessibilityState.invalid` announces the state properly.
 */
export const AppInput = forwardRef<TextInput, AppInputProps>(function AppInput(
  {
    label,
    error,
    hint,
    leadingIcon,
    trailingIcon,
    onTrailingIconPress,
    trailingIconLabel,
    containerStyle,
    inputStyle,
    required,
    editable = true,
    onFocus,
    onBlur,
    ...rest
  },
  ref,
) {
  const theme = useAppTheme();
  const [focused, setFocused] = useState(false);

  const borderColor = error
    ? theme.c.error
    : focused
      ? theme.c.primary
      : theme.c.border;

  return (
    <View style={containerStyle}>
      {label ? (
        <AppText variant="bodySmallStrong" color="textSecondary" style={styles.label}>
          {label}
          {required ? (
            <AppText variant="bodySmallStrong" color="error">
              {' *'}
            </AppText>
          ) : null}
        </AppText>
      ) : null}

      <View
        style={[
          styles.field,
          {
            backgroundColor: editable ? theme.c.surface : theme.c.surfaceSunken,
            borderColor,
            borderRadius: theme.radius.medium,
            // Grows on focus rather than switching width, so the field does not
            // shift its contents by a pixel when the user taps it.
            borderWidth: focused || error ? 2 : 1,
            paddingHorizontal: theme.spacing.md - (focused || error ? 1 : 0),
            minHeight: theme.hitTarget.comfortable,
          },
        ]}
      >
        {leadingIcon ? <View style={styles.adornment}>{leadingIcon}</View> : null}

        <TextInput
          ref={ref}
          editable={editable}
          placeholderTextColor={theme.c.textTertiary}
          selectionColor={theme.c.primary}
          accessibilityLabel={label ?? rest.placeholder}
          accessibilityHint={error ?? hint}
          accessibilityState={{ disabled: !editable }}
          onFocus={(e) => {
            setFocused(true);
            onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            onBlur?.(e);
          }}
          style={[
            styles.input,
            {
              color: theme.c.textPrimary,
              // Never below 16 — see the note above.
              fontSize: theme.typography.body.fontSize,
              paddingVertical: theme.spacing.md,
            },
            inputStyle,
          ]}
          {...rest}
        />

        {trailingIcon ? (
          onTrailingIconPress ? (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={trailingIconLabel}
              onPress={onTrailingIconPress}
              hitSlop={12}
              style={styles.adornment}
            >
              {trailingIcon}
            </Pressable>
          ) : (
            <View style={styles.adornment}>{trailingIcon}</View>
          )
        ) : null}
      </View>

      {error ? (
        <AppText variant="caption" color="error" style={styles.helper} accessibilityLiveRegion="polite">
          {error}
        </AppText>
      ) : hint ? (
        <AppText variant="caption" color="textTertiary" style={styles.helper}>
          {hint}
        </AppText>
      ) : null}
    </View>
  );
});

const styles = StyleSheet.create({
  label: {
    marginBottom: 6,
  },
  field: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  input: {
    flex: 1,
  },
  adornment: {
    justifyContent: 'center',
    alignItems: 'center',
    minWidth: 24,
  },
  helper: {
    marginTop: 6,
  },
});
