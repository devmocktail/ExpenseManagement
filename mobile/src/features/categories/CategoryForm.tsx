import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState, type ReactNode } from 'react';
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
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppButton } from '@/components/ui/AppButton';
import { AppInput } from '@/components/ui/AppInput';
import { AppText } from '@/components/ui/AppText';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import {
  resolveCategoryIcon,
  SELECTABLE_CATEGORY_COLORS,
  SELECTABLE_CATEGORY_ICONS,
} from '@/constants/icons';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Category, TransactionType } from '@/types/api';
import { categorySchema, type CategoryFormValues } from '@/validation/schemas';

export type CategoryFormSubmit = {
  name: string;
  type: TransactionType;
  icon: string;
  color: string;
};

export type CategoryFormProps = {
  /** Present when editing. Pre-populates every field and locks the type. */
  initial?: Category;
  defaultType?: TransactionType;
  submitLabel: string;
  submitting: boolean;
  onSubmit: (values: CategoryFormSubmit) => Promise<void>;
  /** Rendered at the end of the form — the delete section on the edit screen. */
  footer?: ReactNode;
};

/** The fields this form renders. Anything else the server flags becomes a banner. */
const FORM_FIELDS: readonly (keyof CategoryFormValues)[] = ['name', 'type', 'icon', 'color'];

/**
 * The create/edit category form.
 *
 * A category is three decisions — what it is called, which side of the ledger
 * it belongs to, and how it looks — so the icon and colour are laid out as open
 * grids rather than hidden behind pickers. The preview at the top shows the
 * exact chip that will appear on every transaction row, because that pairing,
 * not the two values separately, is what the user is really choosing.
 */
export function CategoryForm({
  initial,
  defaultType = 'Expense',
  submitLabel,
  submitting,
  onSubmit,
  footer,
}: CategoryFormProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  const isEditing = Boolean(initial);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    watch,
    setError,
    formState: { errors },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: initial?.name ?? '',
      type: initial?.type ?? defaultType,
      icon: initial?.icon ?? 'category',
      color: initial?.color ?? SELECTABLE_CATEGORY_COLORS[0],
    },
    mode: 'onBlur',
  });

  const name = watch('name');
  const icon = watch('icon');
  const color = watch('color');

  const submit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await onSubmit({
        name: values.name.trim(),
        type: values.type,
        icon: values.icon,
        color: values.color,
      });
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors.length > 0) {
        // A server error naming a field this form does not render would vanish
        // if it were handed straight to setError, so those are collected and
        // shown in the banner instead of being swallowed.
        const unmapped: string[] = [];

        for (const fieldError of error.fieldErrors) {
          const field = FORM_FIELDS.find((candidate) => candidate === fieldError.field);

          if (field) {
            setError(field, { type: 'server', message: fieldError.message });
          } else {
            unmapped.push(fieldError.message);
          }
        }

        if (unmapped.length > 0) setFormError(unmapped.join(' '));
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
      keyboardVerticalOffset={Platform.OS === 'ios' ? insets.top + 44 : 0}
    >
      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.huge + theme.spacing.xxxl,
          gap: theme.spacing.lg,
        }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <View style={styles.preview}>
          <CategoryIcon icon={icon} color={color} size="large" />

          <AppText
            variant="bodyStrong"
            align="center"
            numberOfLines={1}
            style={{ marginTop: theme.spacing.sm }}
          >
            {name.trim() || 'Your category'}
          </AppText>

          <AppText variant="caption" color="textTertiary">
            Preview
          </AppText>
        </View>

        <Controller
          control={control}
          name="name"
          render={({ field: { onChange, onBlur, value } }) => (
            <AppInput
              label="Name"
              required
              placeholder="Groceries, Rent, Side project"
              value={value}
              onChangeText={onChange}
              onBlur={onBlur}
              error={errors.name?.message}
              autoCapitalize="words"
              autoFocus={!isEditing}
              maxLength={100}
              returnKeyType="done"
            />
          )}
        />

        <View>
          <AppText
            variant="bodySmallStrong"
            color="textSecondary"
            style={{ marginBottom: theme.spacing.xs }}
          >
            Type
          </AppText>

          <Controller
            control={control}
            name="type"
            render={({ field: { onChange, value } }) => (
              // Locked while editing. The type decides which side of the ledger
              // a category sits on, so flipping it would move every transaction
              // already filed under it from expense to income (or back) and
              // silently rewrite months of totals from a single tap.
              <View
                pointerEvents={isEditing ? 'none' : 'auto'}
                accessibilityState={{ disabled: isEditing }}
                style={{ opacity: isEditing ? theme.opacity.disabled : 1 }}
              >
                <SegmentedControl
                  value={value}
                  onChange={onChange}
                  options={[
                    { value: 'Expense', label: 'Expense' },
                    { value: 'Income', label: 'Income' },
                  ]}
                />
              </View>
            )}
          />

          <AppText
            variant="caption"
            color={errors.type ? 'error' : 'textTertiary'}
            style={{ marginTop: theme.spacing.xs }}
          >
            {errors.type?.message ??
              (isEditing
                ? 'A category keeps the type it was created with. Changing it would move every transaction in it to the other side of your ledger.'
                : 'Expense categories appear when you record spending, income categories when money comes in.')}
          </AppText>
        </View>

        <Controller
          control={control}
          name="icon"
          render={({ field: { onChange, value } }) => (
            <View>
              <AppText
                variant="bodySmallStrong"
                color="textSecondary"
                style={{ marginBottom: theme.spacing.xs }}
              >
                Icon
              </AppText>

              <View style={[styles.grid, { gap: theme.spacing.sm }]}>
                {SELECTABLE_CATEGORY_ICONS.map((slug) => {
                  const selected = slug === value;

                  return (
                    <Pressable
                      key={slug}
                      accessibilityRole="radio"
                      accessibilityState={{ selected }}
                      accessibilityLabel={`${iconLabel(slug)} icon`}
                      onPress={() => onChange(slug)}
                      style={{
                        width: theme.hitTarget.comfortable + theme.spacing.sm,
                        height: theme.hitTarget.comfortable + theme.spacing.sm,
                        alignItems: 'center',
                        justifyContent: 'center',
                        borderRadius: theme.radius.medium,
                        borderWidth: selected ? 2 : 1,
                        borderColor: selected ? theme.c.primary : theme.c.border,
                        backgroundColor: selected ? theme.c.primaryMuted : theme.c.surface,
                      }}
                    >
                      <MaterialCommunityIcons
                        name={resolveCategoryIcon(slug)}
                        size={22}
                        // The chosen icon is tinted with the chosen colour, so
                        // the grid previews the actual pairing instead of
                        // showing the selection in a second, unrelated accent.
                        color={selected ? color : theme.c.textSecondary}
                      />
                    </Pressable>
                  );
                })}
              </View>

              {errors.icon ? (
                <AppText variant="caption" color="error" style={{ marginTop: theme.spacing.xs }}>
                  {errors.icon.message}
                </AppText>
              ) : null}
            </View>
          )}
        />

        <Controller
          control={control}
          name="color"
          render={({ field: { onChange, value } }) => (
            <View>
              <AppText
                variant="bodySmallStrong"
                color="textSecondary"
                style={{ marginBottom: theme.spacing.xs }}
              >
                Colour
              </AppText>

              <View style={[styles.grid, { gap: theme.spacing.sm }]}>
                {SELECTABLE_CATEGORY_COLORS.map((swatch) => {
                  const selected = swatch === value;
                  const dot = selected
                    ? theme.spacing.xxl - theme.spacing.xs
                    : theme.hitTarget.min - theme.spacing.sm;

                  return (
                    <Pressable
                      key={swatch}
                      accessibilityRole="radio"
                      accessibilityState={{ selected }}
                      accessibilityLabel={`Colour ${swatch}`}
                      onPress={() => onChange(swatch)}
                      style={{
                        width: theme.hitTarget.min,
                        height: theme.hitTarget.min,
                        borderRadius: theme.radius.pill,
                        alignItems: 'center',
                        justifyContent: 'center',
                        // Selection reads as a ring rather than a tick, because
                        // a glyph drawn on top of an arbitrary swatch is
                        // illegible against half of this palette.
                        borderWidth: selected ? 2 : 0,
                        borderColor: theme.c.textPrimary,
                      }}
                    >
                      <View
                        style={{
                          width: dot,
                          height: dot,
                          borderRadius: theme.radius.pill,
                          backgroundColor: swatch,
                        }}
                      />
                    </Pressable>
                  );
                })}
              </View>

              {errors.color ? (
                <AppText variant="caption" color="error" style={{ marginTop: theme.spacing.xs }}>
                  {errors.color.message}
                </AppText>
              ) : null}
            </View>
          )}
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

        {footer}
      </ScrollView>

      {/* Pinned so saving stays a thumb away however far the grids scroll. */}
      <View
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.md,
          backgroundColor: theme.c.surface,
          borderTopWidth: 1,
          borderTopColor: theme.c.border,
        }}
      >
        <AppButton
          label={submitLabel}
          size="large"
          fullWidth
          loading={submitting}
          onPress={() => void submit()}
        />
      </View>
    </KeyboardAvoidingView>
  );
}

/** "personal-care" becomes "Personal care", for the tile's screen-reader label. */
function iconLabel(slug: string): string {
  const spaced = slug.replace(/-/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  preview: {
    alignItems: 'center',
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
});
