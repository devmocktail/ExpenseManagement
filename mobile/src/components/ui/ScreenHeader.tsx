import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppTheme } from '@/theme/ThemeProvider';
import { AppText } from './AppText';

export type HeaderAction = {
  icon: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  label: string;
  onPress: () => void;
  destructive?: boolean;
};

export type ScreenHeaderProps = {
  title: string;
  subtitle?: string;
  onBack?: () => void;
  actions?: HeaderAction[];
  /** Draws a divider. Off for headers that sit above a coloured hero section. */
  bordered?: boolean;
};

/**
 * The header for pushed screens.
 *
 * A custom component rather than the navigator's built-in header so the title,
 * spacing and typography come from the same tokens as the rest of the app —
 * native headers use platform metrics and end up visibly different from every
 * other screen title.
 */
export function ScreenHeader({
  title,
  subtitle,
  onBack,
  actions = [],
  bordered = true,
}: ScreenHeaderProps) {
  const theme = useAppTheme();
  const insets = useSafeAreaInsets();

  return (
    <View
      style={[
        styles.container,
        {
          paddingTop: insets.top + theme.spacing.sm,
          paddingBottom: theme.spacing.md,
          paddingHorizontal: theme.spacing.base,
          backgroundColor: theme.c.background,
          borderBottomWidth: bordered ? 1 : 0,
          borderBottomColor: theme.c.border,
          gap: theme.spacing.sm,
        },
      ]}
    >
      {onBack ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Go back"
          onPress={onBack}
          hitSlop={12}
          style={styles.iconButton}
        >
          <MaterialCommunityIcons name="arrow-left" size={24} color={theme.c.textPrimary} />
        </Pressable>
      ) : null}

      <View style={styles.titleBlock}>
        <AppText variant="heading3" numberOfLines={1} accessibilityRole="header">
          {title}
        </AppText>
        {subtitle ? (
          <AppText variant="caption" color="textSecondary" numberOfLines={1}>
            {subtitle}
          </AppText>
        ) : null}
      </View>

      {actions.map((action) => (
        <Pressable
          key={action.label}
          accessibilityRole="button"
          accessibilityLabel={action.label}
          onPress={action.onPress}
          hitSlop={12}
          style={styles.iconButton}
        >
          <MaterialCommunityIcons
            name={action.icon}
            size={22}
            color={action.destructive ? theme.c.error : theme.c.textPrimary}
          />
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  titleBlock: {
    flex: 1,
    justifyContent: 'center',
  },
  iconButton: {
    // Never smaller than the minimum comfortable touch target, even though the
    // glyph inside is only 24pt.
    width: 40,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
