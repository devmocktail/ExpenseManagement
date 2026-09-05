import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Modal, Pressable, StyleSheet, View } from 'react-native';
import Animated, { FadeIn, FadeOut, ZoomIn } from 'react-native-reanimated';
import { AppButton } from './AppButton';
import { AppText } from './AppText';
import { useAppTheme } from '@/theme/ThemeProvider';

export type ConfirmDialogProps = {
  visible: boolean;
  title: string;
  message?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Styles the confirm action as destructive and shows a warning glyph. */
  destructive?: boolean;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

/**
 * A confirmation dialog.
 *
 * Used in place of `Alert.alert` because Alert cannot show a loading state,
 * cannot be themed, and orders its buttons differently on each platform — so a
 * user who has learned that "the right-hand button deletes" on Android is wrong
 * on iOS. Here the destructive action is always on the right, always styled as
 * destructive, and always disabled while the request is in flight so a double
 * tap cannot delete twice.
 */
export function ConfirmDialog({
  visible,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const theme = useAppTheme();

  return (
    <Modal
      visible={visible}
      transparent
      animationType="none"
      // Android's back button must cancel, never confirm.
      onRequestClose={loading ? () => {} : onCancel}
      statusBarTranslucent
    >
      <Animated.View
        entering={FadeIn.duration(theme.motion.fast)}
        exiting={FadeOut.duration(theme.motion.fast)}
        style={[styles.backdrop, { backgroundColor: theme.c.backdrop }]}
      >
        <Pressable
          style={StyleSheet.absoluteFill}
          accessibilityRole="button"
          accessibilityLabel={cancelLabel}
          // Tapping outside must not be able to dismiss mid-request, which would
          // leave the user unsure whether the delete went through.
          onPress={loading ? undefined : onCancel}
        />

        <Animated.View
          entering={ZoomIn.duration(theme.motion.normal).springify().damping(18)}
          style={[
            styles.dialog,
            theme.elevation.high,
            {
              backgroundColor: theme.c.surfaceElevated,
              borderRadius: theme.radius.large,
              padding: theme.spacing.lg,
            },
          ]}
        >
          {destructive ? (
            <View
              style={{
                width: 48,
                height: 48,
                borderRadius: 24,
                backgroundColor: theme.c.errorMuted,
                alignItems: 'center',
                justifyContent: 'center',
                alignSelf: 'center',
                marginBottom: theme.spacing.base,
              }}
            >
              <MaterialCommunityIcons name="trash-can-outline" size={24} color={theme.c.error} />
            </View>
          ) : null}

          <AppText variant="heading3" align="center" accessibilityRole="header">
            {title}
          </AppText>

          {message ? (
            <AppText
              variant="bodySmall"
              color="textSecondary"
              align="center"
              style={{ marginTop: theme.spacing.sm }}
            >
              {message}
            </AppText>
          ) : null}

          <View style={[styles.actions, { marginTop: theme.spacing.xl, gap: theme.spacing.md }]}>
            <AppButton
              label={cancelLabel}
              variant="outline"
              onPress={onCancel}
              disabled={loading}
              style={styles.flex}
              haptic={false}
            />
            <AppButton
              label={confirmLabel}
              variant={destructive ? 'danger' : 'primary'}
              onPress={onConfirm}
              loading={loading}
              style={styles.flex}
            />
          </View>
        </Animated.View>
      </Animated.View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
  },
  dialog: {
    width: '100%',
    maxWidth: 360,
  },
  actions: {
    flexDirection: 'row',
  },
  flex: {
    flex: 1,
  },
});
