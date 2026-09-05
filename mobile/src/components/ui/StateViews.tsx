import { ActivityIndicator, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppButton } from './AppButton';
import { AppText } from './AppText';

/**
 * The four states every data-backed screen must handle.
 *
 * They live together because they share a layout and because keeping them in one
 * file makes it obvious when a screen has implemented three of the four — the
 * missing one is almost always the error state.
 */

type BaseProps = {
  style?: StyleProp<ViewStyle>;
};

export function AppLoader({
  label = 'Loading…',
  fullscreen = true,
  style,
}: BaseProps & { label?: string; fullscreen?: boolean }) {
  const theme = useAppTheme();

  return (
    <View
      style={[fullscreen ? styles.fullscreen : styles.inline, style]}
      accessibilityRole="progressbar"
      accessibilityLabel={label}
      // Announced by the screen reader; the spinner alone is silent.
      accessibilityLiveRegion="polite"
    >
      <ActivityIndicator size="large" color={theme.c.primary} />
      {label ? (
        <AppText variant="bodySmall" color="textSecondary" style={{ marginTop: theme.spacing.md }}>
          {label}
        </AppText>
      ) : null}
    </View>
  );
}

export function AppEmptyState({
  title,
  description,
  actionLabel,
  onAction,
  icon,
  style,
}: BaseProps & {
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
  icon?: React.ReactNode;
}) {
  const theme = useAppTheme();

  return (
    <View style={[styles.fullscreen, { padding: theme.spacing.xl }, style]}>
      {icon ? <View style={{ marginBottom: theme.spacing.base }}>{icon}</View> : null}

      <AppText variant="heading3" align="center">
        {title}
      </AppText>

      {description ? (
        <AppText
          variant="bodySmall"
          color="textSecondary"
          align="center"
          style={{ marginTop: theme.spacing.sm, maxWidth: 320 }}
        >
          {description}
        </AppText>
      ) : null}

      {actionLabel && onAction ? (
        <AppButton
          label={actionLabel}
          onPress={onAction}
          style={{ marginTop: theme.spacing.xl }}
        />
      ) : null}
    </View>
  );
}

export function AppErrorState({
  title = 'Something went wrong',
  description,
  onRetry,
  retryLabel = 'Try again',
  style,
}: BaseProps & {
  title?: string;
  description?: string;
  onRetry?: () => void;
  retryLabel?: string;
}) {
  const theme = useAppTheme();

  return (
    <View style={[styles.fullscreen, { padding: theme.spacing.xl }, style]}>
      <View
        style={{
          width: 56,
          height: 56,
          borderRadius: theme.radius.pill,
          backgroundColor: theme.c.errorMuted,
          alignItems: 'center',
          justifyContent: 'center',
          marginBottom: theme.spacing.base,
        }}
      >
        {/* A glyph rather than an icon font, so this state can never itself
            fail to render because an icon set did not load. */}
        <AppText variant="heading2" color="error">
          !
        </AppText>
      </View>

      <AppText variant="heading3" align="center">
        {title}
      </AppText>

      {description ? (
        <AppText
          variant="bodySmall"
          color="textSecondary"
          align="center"
          style={{ marginTop: theme.spacing.sm, maxWidth: 320 }}
        >
          {description}
        </AppText>
      ) : null}

      {onRetry ? (
        <AppButton
          label={retryLabel}
          variant="outline"
          onPress={onRetry}
          style={{ marginTop: theme.spacing.xl }}
        />
      ) : null}
    </View>
  );
}

/**
 * A persistent banner, not a toast: being offline is a state the user stays in,
 * so a message that disappears after three seconds is worse than useless.
 */
export function OfflineBanner({ visible }: { visible: boolean }) {
  const theme = useAppTheme();

  if (!visible) return null;

  return (
    <View
      accessibilityRole="alert"
      style={{
        backgroundColor: theme.c.warningMuted,
        paddingVertical: theme.spacing.sm,
        paddingHorizontal: theme.spacing.base,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.spacing.sm,
      }}
    >
      <View
        style={{
          width: 8,
          height: 8,
          borderRadius: 4,
          backgroundColor: theme.c.warning,
        }}
      />
      <AppText variant="caption" color="warning">
        You&apos;re offline. Some features may be unavailable.
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  fullscreen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  inline: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 32,
  },
});
