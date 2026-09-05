import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useEffect } from 'react';
import {
  BackHandler,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  useWindowDimensions,
  View,
} from 'react-native';
import Animated, { FadeIn, FadeOut, SlideInDown, SlideOutDown } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';

export type BottomSheetProps = {
  visible: boolean;
  onClose: () => void;
  title?: string;
  children: React.ReactNode;
  /** Height as a fraction of the screen. Defaults to 0.6. */
  snapPercent?: number;
  /** Hides the header row entirely, for a sheet that supplies its own. */
  hideHeader?: boolean;
};

/**
 * A bottom sheet.
 *
 * Built on the platform Modal rather than a gesture-driven sheet library
 * because everything this app needs a sheet for — pick a category, set filters,
 * confirm a delete — is a modal decision, not a draggable surface. The result
 * has no native dependency, works identically on both platforms, and cannot be
 * left half-open in an ambiguous state.
 *
 * The Android back button closes it, which `Modal`'s `onRequestClose` handles;
 * the extra BackHandler guard covers the case where a nested handler would
 * otherwise swallow the event and leave the sheet stuck open.
 */
export function BottomSheet({
  visible,
  onClose,
  title,
  children,
  snapPercent = 0.6,
  hideHeader = false,
}: BottomSheetProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const { height: windowHeight } = useWindowDimensions();

  useEffect(() => {
    if (!visible || Platform.OS !== 'android') return;

    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      onClose();
      return true;
    });

    return () => subscription.remove();
  }, [visible, onClose]);

  const sheetHeight = Math.min(windowHeight * snapPercent, windowHeight - insets.top - 24);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="none"
      onRequestClose={onClose}
      statusBarTranslucent
    >
      <View style={styles.root}>
        <Animated.View
          entering={FadeIn.duration(theme.motion.fast)}
          exiting={FadeOut.duration(theme.motion.fast)}
          style={StyleSheet.absoluteFill}
        >
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Close"
            onPress={onClose}
            style={[StyleSheet.absoluteFill, { backgroundColor: theme.c.backdrop }]}
          />
        </Animated.View>

        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          style={styles.keyboardWrapper}
          pointerEvents="box-none"
        >
          <Animated.View
            entering={SlideInDown.duration(theme.motion.normal)}
            exiting={SlideOutDown.duration(theme.motion.fast)}
            style={[
              styles.sheet,
              {
                height: sheetHeight,
                backgroundColor: theme.c.surfaceElevated,
                borderTopLeftRadius: theme.radius.xlarge,
                borderTopRightRadius: theme.radius.xlarge,
                paddingBottom: insets.bottom + theme.spacing.base,
              },
            ]}
          >
            <View style={styles.grabberArea}>
              <View
                style={{
                  width: 40,
                  height: 4,
                  borderRadius: 2,
                  backgroundColor: theme.c.borderStrong,
                }}
              />
            </View>

            {!hideHeader ? (
              <View
                style={[
                  styles.header,
                  {
                    paddingHorizontal: theme.spacing.base,
                    paddingBottom: theme.spacing.md,
                    borderBottomWidth: 1,
                    borderBottomColor: theme.c.border,
                  },
                ]}
              >
                <AppText variant="heading3" numberOfLines={1} style={styles.flex}>
                  {title}
                </AppText>

                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel="Close"
                  onPress={onClose}
                  hitSlop={12}
                >
                  <MaterialCommunityIcons name="close" size={22} color={theme.c.textSecondary} />
                </Pressable>
              </View>
            ) : null}

            <View style={[styles.body, { padding: theme.spacing.base }]}>{children}</View>
          </Animated.View>
        </KeyboardAvoidingView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    justifyContent: 'flex-end',
  },
  keyboardWrapper: {
    justifyContent: 'flex-end',
  },
  sheet: {
    overflow: 'hidden',
  },
  grabberArea: {
    alignItems: 'center',
    paddingVertical: 10,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  body: {
    flex: 1,
  },
  flex: {
    flex: 1,
  },
});
