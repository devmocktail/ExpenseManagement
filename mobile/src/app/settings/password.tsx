import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { useChangePassword } from '@/features/profile/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import {
  changePasswordSchema,
  passwordRules,
  type ChangePasswordFormValues,
} from '@/validation/schemas';

/**
 * Change password.
 *
 * The policy checklist updates as the user types rather than waiting for a
 * blur: a rejected password with one opaque message is the most common reason
 * people abandon this screen.
 */
export default function ChangePasswordScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const changePassword = useChangePassword();

  const {
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
    mode: 'onBlur',
  });

  const newPassword = useWatch({ control, name: 'newPassword' }) ?? '';

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await changePassword.mutateAsync({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      });

      toast.show({
        title: 'Password changed',
        description: 'Your other devices have been signed out.',
        tone: 'success',
      });

      if (router.canGoBack()) {
        router.back();
      } else {
        router.replace('/(tabs)/profile');
      }
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof ChangePasswordFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else {
        setFormError(
          error instanceof ApiError
            ? error.message
            : 'Could not change your password. Please try again.',
        );
      }
    }
  });

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Change password" onBack={() => router.back()} />

      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? insets.top + theme.hitTarget.min : 0}
      >
        <ScrollView
          contentContainerStyle={{
            padding: theme.spacing.base,
            paddingBottom: insets.bottom + theme.spacing.xxl,
            gap: theme.spacing.lg,
          }}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {formError ? (
            <View
              accessibilityRole="alert"
              style={{
                backgroundColor: theme.c.errorMuted,
                borderRadius: theme.radius.medium,
                padding: theme.spacing.md,
              }}
            >
              <AppText variant="bodySmall" color="error">
                {formError}
              </AppText>
            </View>
          ) : null}

          <Controller
            control={control}
            name="currentPassword"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Current password"
                placeholder="Your current password"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.currentPassword?.message}
                secureTextEntry={!showCurrent}
                autoCapitalize="none"
                autoComplete="current-password"
                textContentType="password"
                required
                trailingIcon={
                  <MaterialCommunityIcons
                    name={showCurrent ? 'eye-off-outline' : 'eye-outline'}
                    size={20}
                    color={theme.c.textSecondary}
                  />
                }
                onTrailingIconPress={() => setShowCurrent((v) => !v)}
                trailingIconLabel={showCurrent ? 'Hide current password' : 'Show current password'}
              />
            )}
          />

          <Controller
            control={control}
            name="newPassword"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="New password"
                placeholder="Choose a new password"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.newPassword?.message}
                secureTextEntry={!showNew}
                autoCapitalize="none"
                autoComplete="new-password"
                textContentType="newPassword"
                required
                trailingIcon={
                  <MaterialCommunityIcons
                    name={showNew ? 'eye-off-outline' : 'eye-outline'}
                    size={20}
                    color={theme.c.textSecondary}
                  />
                }
                onTrailingIconPress={() => setShowNew((v) => !v)}
                trailingIconLabel={showNew ? 'Hide new password' : 'Show new password'}
              />
            )}
          />

          <PasswordChecklist value={newPassword} />

          <Controller
            control={control}
            name="confirmPassword"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Confirm new password"
                placeholder="Type it again"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.confirmPassword?.message}
                // Deliberately follows the new-password toggle: two separate
                // reveal states for the same secret is friction with no payoff.
                secureTextEntry={!showNew}
                autoCapitalize="none"
                autoComplete="new-password"
                textContentType="newPassword"
                returnKeyType="done"
                onSubmitEditing={() => void submit()}
                required
              />
            )}
          />

          <View
            style={[
              styles.note,
              {
                gap: theme.spacing.sm,
                backgroundColor: theme.c.infoMuted,
                borderRadius: theme.radius.medium,
                padding: theme.spacing.md,
              },
            ]}
          >
            <MaterialCommunityIcons name="shield-key-outline" size={16} color={theme.c.info} />
            <AppText variant="caption" color="textSecondary" style={styles.flex}>
              Changing your password signs you out of every other device. The server revokes the
              refresh tokens issued to them, so they will each need to sign in again. This device
              stays signed in.
            </AppText>
          </View>

          <AppButton
            label="Change password"
            size="large"
            fullWidth
            loading={changePassword.isPending}
            onPress={() => void submit()}
          />
        </ScrollView>
      </KeyboardAvoidingView>
    </View>
  );
}

/**
 * The live policy checklist.
 *
 * Each rule is announced with its state, because a green tick next to grey text
 * carries no meaning to a screen reader or to anyone who cannot separate the
 * two colours.
 */
function PasswordChecklist({ value }: { value: string }) {
  const theme = useAppTheme();

  return (
    <View style={{ gap: theme.spacing.xs }}>
      {passwordRules.map((rule) => {
        const met = rule.test(value);

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
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  rule: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
