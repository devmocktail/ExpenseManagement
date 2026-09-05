import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useAppTheme } from '@/theme/ThemeProvider';

export type SearchBarProps = {
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  autoFocus?: boolean;
  onSubmitEditing?: () => void;
};

/**
 * A search field.
 *
 * Kept separate from AppInput because it is a control, not a form field: it has
 * no label, no error state, and a clear button that must be reachable without
 * dismissing the keyboard.
 */
export function SearchBar({
  value,
  onChangeText,
  placeholder = 'Search',
  autoFocus,
  onSubmitEditing,
}: SearchBarProps) {
  const theme = useAppTheme();

  return (
    <View
      style={[
        styles.container,
        {
          backgroundColor: theme.c.surfaceSunken,
          borderRadius: theme.radius.medium,
          paddingHorizontal: theme.spacing.md,
          height: theme.hitTarget.comfortable,
          gap: theme.spacing.sm,
        },
      ]}
    >
      <MaterialCommunityIcons name="magnify" size={20} color={theme.c.textTertiary} />

      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={theme.c.textTertiary}
        selectionColor={theme.c.primary}
        autoFocus={autoFocus}
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="search"
        onSubmitEditing={onSubmitEditing}
        accessibilityLabel={placeholder}
        // "search" gives iOS the magnifier key and enables the clear affordance
        // consistently across platforms.
        clearButtonMode="never"
        style={[
          styles.input,
          { color: theme.c.textPrimary, fontSize: theme.typography.body.fontSize },
        ]}
      />

      {value.length > 0 ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Clear search"
          onPress={() => onChangeText('')}
          hitSlop={10}
        >
          <MaterialCommunityIcons name="close-circle" size={18} color={theme.c.textTertiary} />
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  input: {
    flex: 1,
    paddingVertical: 0,
  },
});
