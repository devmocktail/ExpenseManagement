import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import * as Haptics from 'expo-haptics';
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Platform, Pressable, StyleSheet, View } from 'react-native';
import Animated, { FadeInDown, FadeOutUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ColorTokens } from '@/theme/tokens';
import { AppText } from './AppText';

export type ToastTone = 'success' | 'error' | 'info' | 'warning';

export type ToastOptions = {
  title: string;
  /** Second line — e.g. the amount and category of a saved expense. */
  description?: string;
  tone?: ToastTone;
  durationMs?: number;
  action?: { label: string; onPress: () => void };
};

type ToastContextValue = {
  show: (options: ToastOptions) => void;
  hide: () => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

const TONE: Record<ToastTone, { icon: 'check-circle' | 'alert-circle' | 'information' | 'alert'; color: keyof ColorTokens; background: keyof ColorTokens }> = {
  success: { icon: 'check-circle', color: 'success', background: 'successMuted' },
  error: { icon: 'alert-circle', color: 'error', background: 'errorMuted' },
  warning: { icon: 'alert', color: 'warning', background: 'warningMuted' },
  info: { icon: 'information', color: 'info', background: 'infoMuted' },
};

/**
 * Lightweight confirmations.
 *
 * Deliberately a top banner rather than a modal: the brief calls for feedback
 * that does not interrupt, and after saving an expense the user is usually
 * already navigating away. A modal would block that; this rides along above it.
 *
 * Only one toast is shown at a time — a stack of them competing for attention
 * is worse than dropping the older message.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();
  const [toast, setToast] = useState<ToastOptions | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const hide = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = null;
    setToast(null);
  }, []);

  const show = useCallback(
    (options: ToastOptions) => {
      if (timerRef.current) clearTimeout(timerRef.current);

      if (Platform.OS !== 'web') {
        const style =
          options.tone === 'error'
            ? Haptics.NotificationFeedbackType.Error
            : options.tone === 'warning'
              ? Haptics.NotificationFeedbackType.Warning
              : Haptics.NotificationFeedbackType.Success;

        Haptics.notificationAsync(style).catch(() => {});
      }

      setToast(options);

      // An action needs longer to notice and reach; without one, a short
      // confirmation is enough.
      const duration = options.durationMs ?? (options.action ? 6000 : 3000);
      timerRef.current = setTimeout(() => setToast(null), duration);
    },
    [],
  );

  const value = useMemo(() => ({ show, hide }), [show, hide]);

  const tone = TONE[toast?.tone ?? 'success'];

  return (
    <ToastContext.Provider value={value}>
      {children}

      {toast ? (
        <Animated.View
          entering={FadeInDown.duration(theme.motion.normal)}
          exiting={FadeOutUp.duration(theme.motion.fast)}
          pointerEvents="box-none"
          style={[
            styles.container,
            { top: insets.top + theme.spacing.sm, paddingHorizontal: theme.spacing.base },
          ]}
        >
          <Pressable
            onPress={hide}
            accessibilityRole="alert"
            // Announced immediately: a confirmation the user cannot see is not
            // a confirmation.
            accessibilityLiveRegion="assertive"
            accessibilityLabel={`${toast.title}${toast.description ? `. ${toast.description}` : ''}`}
            style={[
              styles.toast,
              theme.elevation.medium,
              {
                backgroundColor: theme.c.surfaceElevated,
                borderRadius: theme.radius.medium,
                padding: theme.spacing.md,
                gap: theme.spacing.md,
                borderWidth: 1,
                borderColor: theme.c.border,
              },
            ]}
          >
            <View
              style={{
                width: 32,
                height: 32,
                borderRadius: theme.radius.small,
                backgroundColor: theme.c[tone.background],
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <MaterialCommunityIcons name={tone.icon} size={18} color={theme.c[tone.color]} />
            </View>

            <View style={styles.body}>
              <AppText variant="bodySmallStrong" numberOfLines={1}>
                {toast.title}
              </AppText>
              {toast.description ? (
                <AppText variant="caption" color="textSecondary" numberOfLines={2}>
                  {toast.description}
                </AppText>
              ) : null}
            </View>

            {toast.action ? (
              <Pressable
                accessibilityRole="button"
                onPress={() => {
                  hide();
                  toast.action?.onPress();
                }}
                hitSlop={8}
              >
                <AppText variant="bodySmallStrong" color="primary">
                  {toast.action.label}
                </AppText>
              </Pressable>
            ) : null}
          </Pressable>
        </Animated.View>
      ) : null}
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);

  if (!context) {
    throw new Error('useToast must be used inside <ToastProvider>. Check the root layout.');
  }

  return context;
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    left: 0,
    right: 0,
    zIndex: 1000,
  },
  toast: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  body: {
    flex: 1,
    gap: 2,
  },
});
