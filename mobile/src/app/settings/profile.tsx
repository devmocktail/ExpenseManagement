import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { Skeleton } from '@/components/ui/Skeleton';
import { AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { useProfile, useUpdateProfile } from '@/features/profile/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { UserProfile } from '@/types/api';
import { formatFullDate } from '@/utils/date';
import { profileSchema, type ProfileFormValues } from '@/validation/schemas';

/**
 * Edit profile.
 *
 * Only the name is editable. Email is the account identifier on the server and
 * changing it needs a re-verification flow that does not exist yet, so it is
 * shown read-only with an honest explanation rather than hidden — people open
 * this screen specifically to check which address the account uses.
 */
export default function EditProfileScreen() {
  const theme = useAppTheme();
  const router = useRouter();

  const { data, isPending, isError, error, refetch } = useProfile();

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Edit profile" onBack={() => router.back()} />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <FormSkeleton />
      ) : (
        // No fourth branch for "loaded but no data": TanStack Query's result is a
        // discriminated union, so ruling out error and pending already proves
        // `data` is defined. A defensive else here narrows to `never` and stops
        // compiling — the type is telling us the state cannot occur.
        //
        // Keyed on the profile so the form's defaultValues are the real ones.
        // Remounting once is less bug-prone than a reset() effect that has to
        // avoid clobbering what the user has already typed.
        <ProfileForm key={data.id} profile={data} />
      )}
    </View>
  );
}

function ProfileForm({ profile }: { profile: UserProfile }) {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const update = useUpdateProfile();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: profile.fullName },
    mode: 'onBlur',
  });

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await update.mutateAsync({ fullName: values.fullName.trim() });

      toast.show({ title: 'Profile updated', tone: 'success' });

      if (router.canGoBack()) {
        router.back();
      } else {
        router.replace('/(tabs)/profile');
      }
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        for (const fieldError of error.fieldErrors) {
          setError(fieldError.field as keyof ProfileFormValues, {
            type: 'server',
            message: fieldError.message,
          });
        }
      } else {
        setFormError(
          error instanceof ApiError ? error.message : 'Could not save. Please try again.',
        );
      }
    }
  });

  return (
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
          name="fullName"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Full name"
              placeholder="Your name"
              value={value}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.fullName?.message}
              autoCapitalize="words"
              autoComplete="name"
              textContentType="name"
              returnKeyType="done"
              onSubmitEditing={() => void submit()}
              required
            />
          )}
        />

        <AppInput
          label="Email"
          value={profile.email}
          editable={false}
          hint="Changing your email address is not supported yet."
          leadingIcon={
            <MaterialCommunityIcons name="email-outline" size={18} color={theme.c.textTertiary} />
          }
        />

        <View
          style={[
            styles.note,
            {
              gap: theme.spacing.sm,
              backgroundColor: theme.c.surfaceSunken,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
            },
          ]}
        >
          <MaterialCommunityIcons
            name="information-outline"
            size={16}
            color={theme.c.textTertiary}
          />
          <AppText variant="caption" color="textTertiary" style={styles.flex}>
            Your email is how you sign in, so moving an account to a new address needs a
            verification step we have not built yet. Member since{' '}
            {formatFullDate(profile.createdAt)}.
          </AppText>
        </View>

        <AppButton
          label="Save changes"
          size="large"
          fullWidth
          // Nothing to save is clearer than a button that succeeds silently
          // without having sent anything.
          disabled={!isDirty}
          loading={update.isPending}
          onPress={() => void submit()}
        />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function FormSkeleton() {
  const theme = useAppTheme();

  return (
    <View style={{ padding: theme.spacing.base, gap: theme.spacing.lg }}>
      <View style={{ gap: theme.spacing.sm }}>
        <Skeleton width={80} height={12} />
        <Skeleton width="100%" height={theme.hitTarget.comfortable} radius={theme.radius.medium} />
      </View>

      <View style={{ gap: theme.spacing.sm }}>
        <Skeleton width={60} height={12} />
        <Skeleton width="100%" height={theme.hitTarget.comfortable} radius={theme.radius.medium} />
      </View>

      <Skeleton width="100%" height={56} radius={theme.radius.medium} />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
