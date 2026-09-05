import { zodResolver } from '@hookform/resolvers/zod';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Link, useRouter } from 'expo-router';
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
import { useLogin } from '@/features/auth/hooks';
import { useAuthStore } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';
import { loginSchema, type LoginFormValues } from '@/validation/schemas';

export default function LoginScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const [showPassword, setShowPassword] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const login = useLogin();

  const sessionExpiredMessage = useAuthStore((s) => s.sessionExpiredMessage);
  const clearSessionExpiredMessage = useAuthStore((s) => s.clearSessionExpiredMessage);

  const {
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
    mode: 'onBlur',
  });

  // Surfaced once, on arrival, so a user bounced here by an expired token knows
  // why rather than assuming the app logged them out at random.
  useEffect(() => {
    if (sessionExpiredMessage) {
      toast.show({ title: 'Signed out', description: sessionExpiredMessage, tone: 'info' });
      clearSessionExpiredMessage();
    }
  }, [sessionExpiredMessage, clearSessionExpiredMessage, toast]);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await login.mutateAsync({
        email: values.email.trim().toLowerCase(),
        password: values.password,
      });

      router.replace('/(tabs)');
    } catch (error) {
      if (error instanceof ApiError) {
        // Field-level messages go on the fields; anything else becomes a banner
        // above the form, where it cannot be missed.
        if (error.fieldErrors.length > 0) {
          for (const fieldError of error.fieldErrors) {
            setError(fieldError.field as keyof LoginFormValues, {
              type: 'server',
              message: fieldError.message,
            });
          }
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('Something went wrong. Please try again.');
      }
    }
  });

  return (
    <KeyboardAvoidingView
      style={[styles.flex, { backgroundColor: theme.c.background }]}
      // On iOS the keyboard overlays the view, so padding is needed; on Android
      // the window resizes already and adding padding double-counts it.
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={{
          flexGrow: 1,
          paddingTop: insets.top + theme.spacing.xxl,
          paddingBottom: insets.bottom + theme.spacing.xl,
          paddingHorizontal: theme.spacing.lg,
        }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <View style={styles.brand}>
          <View
            style={{
              width: 64,
              height: 64,
              borderRadius: theme.radius.large,
              backgroundColor: theme.c.primary,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <MaterialCommunityIcons name="wallet-outline" size={32} color={theme.c.onPrimary} />
          </View>

          <AppText variant="heading1" style={{ marginTop: theme.spacing.base }}>
            Welcome back
          </AppText>
          <AppText variant="bodySmall" color="textSecondary" style={{ marginTop: 4 }}>
            Sign in to keep track of your money
          </AppText>
        </View>

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

        <View style={{ gap: theme.spacing.base }}>
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
                returnKeyType="next"
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
            name="password"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Password"
                placeholder="Your password"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.password?.message}
                secureTextEntry={!showPassword}
                autoCapitalize="none"
                autoComplete="current-password"
                textContentType="password"
                returnKeyType="go"
                onSubmitEditing={() => void onSubmit()}
                leadingIcon={
                  <MaterialCommunityIcons
                    name="lock-outline"
                    size={18}
                    color={theme.c.textTertiary}
                  />
                }
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

          <Pressable
            accessibilityRole="link"
            onPress={() => router.push('/(auth)/forgot-password')}
            hitSlop={8}
            style={styles.forgot}
          >
            <AppText variant="bodySmallStrong" color="primary">
              Forgot password?
            </AppText>
          </Pressable>

          <AppButton
            label="Sign in"
            size="large"
            fullWidth
            loading={login.isPending}
            onPress={() => void onSubmit()}
          />
        </View>

        <View style={[styles.footer, { marginTop: theme.spacing.xxl }]}>
          <AppText variant="bodySmall" color="textSecondary">
            New here?{' '}
          </AppText>
          <Link href="/(auth)/register" asChild>
            <Pressable accessibilityRole="link" hitSlop={8}>
              <AppText variant="bodySmallStrong" color="primary">
                Create an account
              </AppText>
            </Pressable>
          </Link>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  brand: {
    alignItems: 'center',
    marginBottom: 32,
  },
  forgot: {
    alignSelf: 'flex-end',
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },
});
