import { zodResolver } from '@hookform/resolvers/zod';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
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
import { useResetPassword } from '@/features/auth/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { passwordRules, resetPasswordSchema, type ResetPasswordFormValues } from '@/validation/schemas';

export default function ResetPasswordScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  // A reset link opens the app straight here with both values attached. They
  // stay editable because the same email also carries a plain code, and a user
  // whose mail client mangled the link needs to be able to type it.
  const params = useLocalSearchParams<{ email?: string; token?: string }>();

  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const resetPassword = useResetPassword();

  const {
    control,
    handleSubmit,
    setError,
    setValue,
    getValues,
    formState: { errors },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      email: params.email ?? '',
      token: params.token ?? '',
      newPassword: '',
      confirmPassword: '',
    },
    mode: 'onBlur',
  });

  const newPassword = useWatch({ control, name: 'newPassword' });

  // On a cold start the deep link can resolve a beat after the first render, so
  // defaultValues alone would miss it. Only empty fields are filled, so a value
  // the user has already typed is never overwritten.
  useEffect(() => {
    if (params.email && !getValues('email')) {
      setValue('email', params.email);
    }

    if (params.token && !getValues('token')) {
      setValue('token', params.token);
    }
  }, [params.email, params.token, getValues, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await resetPassword.mutateAsync({
        email: values.email.trim().toLowerCase(),
        token: values.token.trim(),
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      });

      toast.show({
        title: 'Password updated',
        description: 'Sign in with your new password.',
        tone: 'success',
      });

      // Replace rather than push: the reset flow is spent, and letting the user
      // swipe back into a form whose token has just been consumed would only
      // produce a confusing failure.
      router.replace('/(auth)/login');
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof ResetPasswordFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else {
        setFormError(
          error instanceof ApiError ? error.message : 'Something went wrong. Please try again.',
        );
      }
    }
  });

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

        <AppText variant="heading1">Set a new password</AppText>
        <AppText variant="bodySmall" color="textSecondary" style={{ marginTop: 4 }}>
          Enter the code from your email and choose a password you have not used here before.
        </AppText>

        {formError ? (
          <View
            accessibilityRole="alert"
            style={{
              backgroundColor: theme.c.errorMuted,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
              marginTop: theme.spacing.base,
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
                required
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

          <Controller
            control={control}
            name="token"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Reset code"
                placeholder="Paste the code from your email"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.token?.message}
                hint="Opening the link from the email fills this in for you."
                autoCapitalize="none"
                autoCorrect={false}
                autoComplete="one-time-code"
                textContentType="oneTimeCode"
                required
                leadingIcon={
                  <MaterialCommunityIcons
                    name="key-outline"
                    size={18}
                    color={theme.c.textTertiary}
                  />
                }
              />
            )}
          />

          <Controller
            control={control}
            name="newPassword"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="New password"
                placeholder="Create a password"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.newPassword?.message}
                secureTextEntry={!showPassword}
                autoCapitalize="none"
                autoComplete="new-password"
                textContentType="newPassword"
                required
                trailingIcon={
                  <MaterialCommunityIcons
                    name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                    size={20}
                    color={theme.c.textSecondary}
                  />
                }
                onTrailingIconPress={() => setShowPassword((v) => !v)}
                trailingIconLabel={showPassword ? 'Hide password' : 'Show password'}
              />
            )}
          />

          <PasswordChecklist password={newPassword ?? ''} />

          <Controller
            control={control}
            name="confirmPassword"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Confirm password"
                placeholder="Type it again"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.confirmPassword?.message}
                secureTextEntry={!showPassword}
                autoCapitalize="none"
                autoComplete="new-password"
                textContentType="newPassword"
                returnKeyType="go"
                onSubmitEditing={() => void onSubmit()}
                required
              />
            )}
          />

          <AppButton
            label="Update password"
            size="large"
            fullWidth
            loading={resetPassword.isPending}
            onPress={() => void onSubmit()}
          />
        </View>

        <View style={[styles.footer, { marginTop: theme.spacing.xl }]}>
          <AppText variant="bodySmall" color="textSecondary">
            Code expired?{' '}
          </AppText>
          <Pressable
            accessibilityRole="link"
            accessibilityLabel="Request a new reset link"
            hitSlop={8}
            onPress={() => router.replace('/(auth)/forgot-password')}
          >
            <AppText variant="bodySmallStrong" color="primary">
              Request a new one
            </AppText>
          </Pressable>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/**
 * Live password requirements, matching the register screen.
 *
 * A checklist rather than a strength bar, because "weak" tells the user nothing
 * about what to change. Each rule reports its own state through both an icon
 * and a text colour, so the state is never conveyed by colour alone.
 */
function PasswordChecklist({ password }: { password: string }) {
  const theme = useAppTheme();

  if (!password) return null;

  return (
    <View style={{ gap: 6 }}>
      {passwordRules.map((rule) => {
        const met = rule.test(password);

        return (
          <View key={rule.id} style={[styles.rule, { gap: theme.spacing.sm }]}>
            <MaterialCommunityIcons
              name={met ? 'check-circle' : 'circle-outline'}
              size={14}
              color={met ? theme.c.success : theme.c.textTertiary}
            />
            <AppText
              variant="caption"
              color={met ? 'success' : 'textTertiary'}
              accessibilityLabel={`${rule.label}: ${met ? 'met' : 'not met'}`}
            >
              {rule.label}
            </AppText>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  back: { alignSelf: 'flex-start' },
  rule: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },
});
