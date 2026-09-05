import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';
import { BottomSheet } from './BottomSheet';

export type PickerOption<T extends string> = {
  value: T;
  label: string;
  icon?: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  description?: string;
};

export type OptionPickerProps<T extends string> = {
  label: string;
  value: T | undefined;
  options: PickerOption<T>[];
  onChange: (value: T) => void;
  placeholder?: string;
  error?: string;
  required?: boolean;
};

/**
 * A single-select field that opens a sheet.
 *
 * Used for payment method, frequency, period — anything with a handful of
 * mutually exclusive options. A native picker was rejected because iOS renders
 * it as a wheel and Android as a dialog, so the two platforms end up looking
 * and behaving nothing alike inside the same form.
 */
export function OptionPicker<T extends string>({
  label,
  value,
  options,
  onChange,
  placeholder = 'Select',
  error,
  required,
}: OptionPickerProps<T>) {
  const theme = useAppTheme();
  const [open, setOpen] = useState(false);

  const selected = options.find((option) => option.value === value);

  return (
    <View>
      <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: 6 }}>
        {label}
        {required ? (
          <AppText variant="bodySmallStrong" color="error">
            {' *'}
          </AppText>
        ) : null}
      </AppText>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${label}. ${selected?.label ?? placeholder}`}
        accessibilityHint="Opens a list of options"
        onPress={() => setOpen(true)}
        style={[
          styles.field,
          {
            backgroundColor: theme.c.surface,
            borderColor: error ? theme.c.error : theme.c.border,
            borderWidth: error ? 2 : 1,
            borderRadius: theme.radius.medium,
            paddingHorizontal: theme.spacing.md,
            minHeight: theme.hitTarget.comfortable,
            gap: theme.spacing.sm,
          },
        ]}
      >
        {selected?.icon ? (
          <MaterialCommunityIcons name={selected.icon} size={18} color={theme.c.textSecondary} />
        ) : null}

        <AppText
          variant="body"
          color={selected ? 'textPrimary' : 'textTertiary'}
          numberOfLines={1}
          style={styles.flex}
        >
          {selected?.label ?? placeholder}
        </AppText>

        <MaterialCommunityIcons name="chevron-down" size={20} color={theme.c.textTertiary} />
      </Pressable>

      {error ? (
        <AppText variant="caption" color="error" style={{ marginTop: 6 }}>
          {error}
        </AppText>
      ) : null}

      <BottomSheet
        visible={open}
        onClose={() => setOpen(false)}
        title={label}
        snapPercent={Math.min(0.3 + options.length * 0.08, 0.8)}
      >
        <ScrollView showsVerticalScrollIndicator={false}>
          {options.map((option) => {
            const isSelected = option.value === value;

            return (
              <Pressable
                key={option.value}
                accessibilityRole="radio"
                accessibilityState={{ selected: isSelected }}
                accessibilityLabel={option.label}
                onPress={() => {
                  onChange(option.value);
                  setOpen(false);
                }}
                style={({ pressed }) => [
                  styles.option,
                  {
                    paddingVertical: theme.spacing.md,
                    gap: theme.spacing.md,
                    minHeight: theme.hitTarget.comfortable,
                    backgroundColor: pressed ? theme.c.surfaceSunken : 'transparent',
                    borderRadius: theme.radius.small,
                    paddingHorizontal: theme.spacing.sm,
                  },
                ]}
              >
                {option.icon ? (
                  <View
                    style={{
                      width: 36,
                      height: 36,
                      borderRadius: theme.radius.small,
                      backgroundColor: isSelected ? theme.c.primaryMuted : theme.c.surfaceSunken,
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <MaterialCommunityIcons
                      name={option.icon}
                      size={18}
                      color={isSelected ? theme.c.primary : theme.c.textSecondary}
                    />
                  </View>
                ) : null}

                <View style={styles.flex}>
                  <AppText variant="body" color={isSelected ? 'primary' : 'textPrimary'}>
                    {option.label}
                  </AppText>
                  {option.description ? (
                    <AppText variant="caption" color="textTertiary">
                      {option.description}
                    </AppText>
                  ) : null}
                </View>

                {isSelected ? (
                  <MaterialCommunityIcons name="check" size={20} color={theme.c.primary} />
                ) : null}
              </Pressable>
            );
          })}
        </ScrollView>
      </BottomSheet>
    </View>
  );
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  flex: {
    flex: 1,
  },
});
