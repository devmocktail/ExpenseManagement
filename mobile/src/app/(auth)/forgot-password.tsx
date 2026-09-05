import { zodResolver } from '@hookform/resolvers/zod';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { useToast } from '@/components/ui/Toast';
import { useForgotPassword } from '@/features/auth/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { forgotPasswordSchema, type ForgotPasswordFormValues } from '@/validation/schemas';

/** Seconds a user must wait before asking for another email. */
const RESEND_COOLDOWN_SECONDS = 30;

export default function ForgotPasswordScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const [sentTo, setSentTo] = useState<string | null>(null);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const [formError, setFormError] = useState<string | null>(null);

  const forgotPassword = useForgotPassword();

  const {
    control,
    handleSubmit,
    setError,
    getValues,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
    mode: 'onBlur',
  });

  // One timer chained off the current value rather than a long-lived interval,
  // so the countdown cannot keep ticking after the screen unmounts or drift out
  // of step with re-renders.
  useEffect(() => {
    if (secondsLeft <= 0) return;

    const timer = setTimeout(() => setSecondsLeft((s) => s - 1), 1000);
    return () => clearTimeout(timer);
  }, [secondsLeft]);

  async function requestReset(email: string): Promise<boolean> {
    setFormError(null);

    try {
      await forgotPassword.mutateAsync({ email });
      setSecondsLeft(RESEND_COOLDOWN_SECONDS);
      return true;
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof ForgotPasswordFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else {
        setFormError(
          error instanceof ApiError ? error.message : 'Something went wrong. Please try again.',
        );
      }

      return false;
    }
  }

  const onSubmit = handleSubmit(async (values) => {
    const email = values.email.trim().toLowerCase();

    if (await requestReset(email)) {
      setSentTo(email);
    }
  });

  async function onResend() {
    const email = sentTo ?? getValues('email').trim().toLowerCase();

    if (await requestReset(email)) {
      toast.show({ title: 'Sent again', description: `Check ${email}`, tone: 'success' });
    }
  }

  return (
    <KeyboardAvoidingView
      style={[styles.flex, { backgroundColor: theme.c.background }]}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={{
          flexGrow: 1,
          paddingTop: insets.top + theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.xl,
          paddingHorizontal: theme.spacing.lg,
        }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Go back"
          onPress={() => router.back()}
          hitSlop={12}
          style={[styles.back, { marginBottom: theme.spacing.base }]}
        >
          <MaterialCommunityIcons name="arrow-left" size={24} color={theme.c.textPrimary} />
        </Pressable>

        {formError ? (
          <View
            accessibilityRole="alert"
            style={{
              backgroundColor: theme.c.errorMuted,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
              marginBottom: theme.spacing.base,
              flexDirection: 'row',
              alignItems: 'center',
              gap: theme.spacing.sm,
            }}
          >
            <MaterialCommunityIcons name="alert-circle" size={18} color={theme.c.error} />
            <AppText variant="bodySmall" color="error" style={styles.flex}>
              {formError}
            </AppText>
          </View>
        ) : null}

        {sentTo ? (
          <SentPanel
            email={sentTo}
            secondsLeft={secondsLeft}
            resending={forgotPassword.isPending}
            onResend={() => void onResend()}
            onEnterCode={() =>
              router.push({ pathname: '/(auth)/reset-password', params: { email: sentTo } })
            }
            onBackToSignIn={() => router.replace('/(auth)/login')}
          />
        ) : (
          <>
            <View
              style={{
                width: 56,
                height: 56,
                borderRadius: theme.radius.large,
                backgroundColor: theme.c.primaryMuted,
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: theme.spacing.base,
              }}
            >
              <MaterialCommunityIcons name="lock-reset" size={28} color={theme.c.primary} />
            </View>

            <AppText variant="heading1">Reset your password</AppText>
            <AppText variant="bodySmall" color="textSecondary" style={{ marginTop: 4 }}>
              Enter the email you signed up with and we will send you a link to set a new password.
            </AppText>

            <View style={{ gap: theme.spacing.base, marginTop: theme.spacing.xl }}>
              <Controller
                control={control}
                name="email"
                render={({ field: { onChange, onBlur, value } }) => (
                  <AppInput
                    label="Email"
                    placeholder="you@example.com"
                    value={value}
                    onChangeText={onChange}
                    onBlur={onBlur}
                    error={errors.email?.message}
                    keyboardType="email-address"
                    autoCapitalize="none"
                    autoComplete="email"
                    textContentType="emailAddress"
                    autoCorrect={false}
                    returnKeyType="send"
                    onSubmitEditing={() => void onSubmit()}
                    leadingIcon={
                      <MaterialCommunityIcons
                        name="email-outline"
                        size={18}
                        color={theme.c.textTertiary}
                      />
                    }
                  />
                )}
              />

              <AppButton
                label="Send reset link"
                size="large"
                fullWidth
                loading={forgotPassword.isPending}
                onPress={() => void onSubmit()}
              />
            </View>

            <View style={[styles.footer, { marginTop: theme.spacing.xl }]}>
              <AppText variant="bodySmall" color="textSecondary">
                Remembered it?{' '}
              </AppText>
              <Pressable
                accessibilityRole="link"
                accessibilityLabel="Back to sign in"
                hitSlop={8}
                onPress={() => router.replace('/(auth)/login')}
              >
                <AppText variant="bodySmallStrong" color="primary">
                  Back to sign in
                </AppText>
              </Pressable>
            </View>
          </>
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/**
 * Confirmation state, shown in place of the form.
 *
 * The wording never confirms whether the address has an account, because the
 * endpoint responds identically either way — saying "no account with that
 * email" here would turn this screen into an oracle for enumerating who is
 * registered. Pointing at the spam folder covers the far more common real cause
 * of "I got nothing".
 */
function SentPanel({
  email,
  secondsLeft,
  resending,
  onResend,
  onEnterCode,
  onBackToSignIn,
}: {
  email: string;
  secondsLeft: number;
  resending: boolean;
  onResend: () => void;
  onEnterCode: () => void;
  onBackToSignIn: () => void;
}) {
  const theme = useAppTheme();
  const cooling = secondsLeft > 0;

  return (
    <View style={[styles.sent, { paddingTop: theme.spacing.xxl }]}>
      <View
        style={{
          width: 72,
          height: 72,
          borderRadius: theme.radius.pill,
          backgroundColor: theme.c.successMuted,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <MaterialCommunityIcons name="email-check-outline" size={34} color={theme.c.success} />
      </View>

      <AppText variant="heading2" align="center" style={{ marginTop: theme.spacing.lg }}>
        Check your inbox
      </AppText>

      <AppText
        variant="body"
        color="textSecondary"
        align="center"
        style={{ marginTop: theme.spacing.sm }}
      >
        If {email} is registered, a link to reset your password is on its way.
      </AppText>

      <AppText
        variant="bodySmall"
        color="textTertiary"
        align="center"
        style={{ marginTop: theme.spacing.md }}
      >
        It can take a minute to arrive. Check your spam folder before trying again.
      </AppText>

      <View style={[styles.actions, { gap: theme.spacing.md, marginTop: theme.spacing.xxl }]}>
        <AppButton label="I have a reset code" size="large" fullWidth onPress={onEnterCode} />

        <AppButton
          label={cooling ? `Resend in ${secondsLeft}s` : 'Resend email'}
          variant="outline"
          size="large"
          fullWidth
          disabled={cooling}
          loading={resending}
          onPress={onResend}
        />
      </View>

      <Pressable
        accessibilityRole="link"
        accessibilityLabel="Back to sign in"
        onPress={onBackToSignIn}
        hitSlop={8}
        style={{
          marginTop: theme.spacing.xl,
          minHeight: theme.hitTarget.min,
          justifyContent: 'center',
        }}
      >
        <AppText variant="bodySmallStrong" color="primary">
          Back to sign in
        </AppText>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  back: { alignSelf: 'flex-start' },
  sent: { alignItems: 'center' },
  actions: { alignSelf: 'stretch' },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },
});
