import { zodResolver } from '@hookform/resolvers/zod';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import * as Localization from 'expo-localization';
import { useRouter } from 'expo-router';
import { useState } from 'react';
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
import { useRegister } from '@/features/auth/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { passwordRules, registerSchema, type RegisterFormValues } from '@/validation/schemas';

export default function RegisterScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const register = useRegister();

  const {
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: '',
      email: '',
      password: '',
      confirmPassword: '',
      acceptedTerms: false as unknown as true,
    },
    mode: 'onBlur',
  });

  const password = useWatch({ control, name: 'password' });
  const acceptedTerms = useWatch({ control, name: 'acceptedTerms' });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await register.mutateAsync({
        fullName: values.fullName.trim(),
        email: values.email.trim().toLowerCase(),
        password: values.password,
        confirmPassword: values.confirmPassword,
        acceptedTerms: true,
        // Sent so the very first dashboard uses the right currency and month
        // boundaries, instead of defaulting and then correcting itself once the
        // user finds settings.
        timeZoneId: Localization.getCalendars()[0]?.timeZone ?? undefined,
        currencyCode: Localization.getLocales()[0]?.currencyCode ?? undefined,
      });

      router.replace('/(tabs)');
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof RegisterFormValues, {
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
          style={{ alignSelf: 'flex-start', marginBottom: theme.spacing.base }}
        >
          <MaterialCommunityIcons name="arrow-left" size={24} color={theme.c.textPrimary} />
        </Pressable>

        <AppText variant="heading1">Create your account</AppText>
        <AppText variant="bodySmall" color="textSecondary" style={{ marginTop: 4 }}>
          Start tracking in under a minute
        </AppText>

        {formError ? (
          <View
            accessibilityRole="alert"
            style={{
              backgroundColor: theme.c.errorMuted,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
              marginTop: theme.spacing.base,
            }}
          >
            <AppText variant="bodySmall" color="error">
              {formError}
            </AppText>
          </View>
        ) : null}

        <View style={{ gap: theme.spacing.base, marginTop: theme.spacing.xl }}>
          <Controller
            control={control}
            name="fullName"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Full name"
                placeholder="Priya Sharma"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.fullName?.message}
                autoCapitalize="words"
                autoComplete="name"
                textContentType="name"
                required
              />
            )}
          />

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
              />
            )}
          />

          <Controller
            control={control}
            name="password"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Password"
                placeholder="Create a password"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.password?.message}
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

          <PasswordChecklist password={password ?? ''} />

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
                required
              />
            )}
          />

          <Controller
            control={control}
            name="acceptedTerms"
            render={({ field: { onChange } }) => (
              <Pressable
                accessibilityRole="checkbox"
                accessibilityState={{ checked: Boolean(acceptedTerms) }}
                accessibilityLabel="Accept the terms of service and privacy policy"
                onPress={() => onChange(!acceptedTerms)}
                style={[styles.terms, { gap: theme.spacing.md }]}
              >
                <View
                  style={{
                    width: 22,
                    height: 22,
                    borderRadius: 6,
                    borderWidth: 2,
                    borderColor: acceptedTerms ? theme.c.primary : theme.c.borderStrong,
                    backgroundColor: acceptedTerms ? theme.c.primary : 'transparent',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  {acceptedTerms ? (
                    <MaterialCommunityIcons name="check" size={14} color={theme.c.onPrimary} />
                  ) : null}
                </View>

                <AppText variant="bodySmall" color="textSecondary" style={styles.flex}>
                  I agree to the Terms of Service and Privacy Policy
                </AppText>
              </Pressable>
            )}
          />

          {errors.acceptedTerms ? (
            <AppText variant="caption" color="error">
              {errors.acceptedTerms.message}
            </AppText>
          ) : null}

          <AppButton
            label="Create account"
            size="large"
            fullWidth
            loading={register.isPending}
            onPress={() => void onSubmit()}
          />
        </View>

        <View style={[styles.footer, { marginTop: theme.spacing.xl }]}>
          <AppText variant="bodySmall" color="textSecondary">
            Already have an account?{' '}
          </AppText>
          <Pressable accessibilityRole="link" hitSlop={8} onPress={() => router.replace('/(auth)/login')}>
            <AppText variant="bodySmallStrong" color="primary">
              Sign in
            </AppText>
          </Pressable>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/**
 * Live password requirements.
 *
 * Shown as a checklist rather than a single strength bar because "weak" tells
 * the user nothing about what to change. Each rule reports its own state, and
 * the icon plus the text colour both change so the state is never conveyed by
 * colour alone.
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
  terms: {
    flexDirection: 'row',
    alignItems: 'center',
  },
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
