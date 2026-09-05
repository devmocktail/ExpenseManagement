import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { z } from 'zod';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppCard } from '@/components/ui/AppCard';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { useDeleteAccount } from '@/features/profile/hooks';
import { useAuthStore } from '@/store/auth-store';
import { useAppTheme } from '@/theme/ThemeProvider';

/** The word the user has to type. Case-sensitive on purpose — this is the gate. */
const CONFIRM_WORD = 'DELETE';

/**
 * Lives here rather than in validation/schemas.ts because it mirrors no server
 * contract: it is purely the friction that stops an accidental tap, and the API
 * only ever receives the password.
 */
const deleteAccountSchema = z.object({
  confirmation: z
    .string()
    .refine((value) => value.trim() === CONFIRM_WORD, `Type ${CONFIRM_WORD} exactly to confirm`),
  password: z.string().min(1, 'Enter your password'),
});

type DeleteAccountFormValues = z.infer<typeof deleteAccountSchema>;

const DELETED_ITEMS = [
  'Every transaction, including notes and merchants',
  'Receipt images attached to them',
  'Your categories, budgets and recurring rules',
  'Settings, notification preferences and registered devices',
  'The account itself and the email address it is tied to',
] as const;

/**
 * Delete account.
 *
 * A one-way door, so it is gated three times: the user reads what goes, types a
 * word that cannot be produced by a mis-tap, and re-enters their password
 * (which the server verifies) before a destructive dialog asks once more.
 */
export default function DeleteAccountScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();
  const queryClient = useQueryClient();

  const signOut = useAuthStore((s) => s.signOut);
  const deleteAccount = useDeleteAccount();

  const [confirming, setConfirming] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    getValues,
    setError,
    formState: { errors },
  } = useForm<DeleteAccountFormValues>({
    resolver: zodResolver(deleteAccountSchema),
    defaultValues: { confirmation: '', password: '' },
    mode: 'onBlur',
  });

  // Validation runs before the dialog opens, so the last prompt is never shown
  // for a request that was going to be rejected anyway.
  const openConfirm = handleSubmit(() => {
    setFormError(null);
    setConfirming(true);
  });

  const performDelete = async () => {
    try {
      await deleteAccount.mutateAsync(getValues('password'));

      setConfirming(false);

      // Cache first: it still holds the deleted account's transactions, and
      // whoever signs in next on this device must not see them.
      queryClient.clear();
      await signOut();

      toast.show({ title: 'Your account has been deleted', tone: 'info' });
      router.replace('/(auth)/login');
    } catch (error) {
      setConfirming(false);

      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof DeleteAccountFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else if (error instanceof ApiError && (error.status === 400 || error.status === 401)) {
        setError('password', { type: 'server', message: 'That password is not correct' });
      } else {
        setFormError(
          error instanceof ApiError
            ? error.message
            : 'Could not delete your account. Please try again.',
        );
      }
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Delete account" onBack={() => router.back()} />

      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? insets.top + theme.hitTarget.min : 0}
      >
        <ScrollView
          contentContainerStyle={{
            padding: theme.spacing.base,
            paddingBottom: insets.bottom + theme.spacing.xxl,
            gap: theme.spacing.base,
          }}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View
            style={[
              styles.note,
              {
                gap: theme.spacing.sm,
                backgroundColor: theme.c.errorMuted,
                borderRadius: theme.radius.medium,
                padding: theme.spacing.md,
              },
            ]}
          >
            <MaterialCommunityIcons name="alert-outline" size={18} color={theme.c.error} />
            <AppText variant="bodySmallStrong" color="error" style={styles.flex}>
              This is permanent. There is no undo, and support cannot restore a deleted account.
            </AppText>
          </View>

          <AppCard>
            <AppText variant="heading3">What gets deleted</AppText>

            <View style={{ marginTop: theme.spacing.md, gap: theme.spacing.sm }}>
              {DELETED_ITEMS.map((item) => (
                <View key={item} style={[styles.bullet, { gap: theme.spacing.sm }]}>
                  <MaterialCommunityIcons name="close" size={14} color={theme.c.error} />
                  <AppText variant="bodySmall" color="textSecondary" style={styles.flex}>
                    {item}
                  </AppText>
                </View>
              ))}
            </View>

            <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.md }}>
              Deletion is immediate. Backups age out on their own retention schedule, so a copy may
              persist there for a short period before it is overwritten.
            </AppText>
          </AppCard>

          <AppButton
            label="Export my data first"
            variant="outline"
            fullWidth
            leadingIcon={
              <MaterialCommunityIcons
                name="download-outline"
                size={18}
                color={theme.c.textPrimary}
              />
            }
            onPress={() => router.push('/settings/export')}
          />

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
            name="confirmation"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label={`Type ${CONFIRM_WORD} to confirm`}
                placeholder={CONFIRM_WORD}
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.confirmation?.message}
                autoCapitalize="characters"
                autoCorrect={false}
                autoComplete="off"
                required
              />
            )}
          />

          <Controller
            control={control}
            name="password"
            render={({ field: { onChange, onBlur, value } }) => (
              <AppInput
                label="Your password"
                placeholder="Confirm it is you"
                value={value}
                onChangeText={onChange}
                onBlur={onBlur}
                error={errors.password?.message}
                secureTextEntry
                autoCapitalize="none"
                autoComplete="current-password"
                textContentType="password"
                returnKeyType="done"
                required
              />
            )}
          />

          <AppButton
            label="Delete my account"
            variant="danger"
            size="large"
            fullWidth
            loading={deleteAccount.isPending}
            onPress={() => void openConfirm()}
          />
        </ScrollView>
      </KeyboardAvoidingView>

      <ConfirmDialog
        visible={confirming}
        destructive
        title="Delete your account?"
        message="Your transactions, budgets and receipts will be erased. This cannot be undone."
        confirmLabel="Delete forever"
        cancelLabel="Keep my account"
        loading={deleteAccount.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => void performDelete()}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  bullet: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
