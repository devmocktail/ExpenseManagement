import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppText } from '@/components/ui/AppText';
import { BottomSheet } from '@/components/ui/BottomSheet';
import { SearchBar } from '@/components/ui/SearchBar';
import { AppLoader } from '@/components/ui/StateViews';
import { useCategories } from '@/features/categories/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Category, TransactionType } from '@/types/api';

export type CategoryPickerProps = {
  type: TransactionType;
  value: string | undefined;
  onChange: (categoryId: string) => void;
  error?: string;
  label?: string;
};

/**
 * Category selection.
 *
 * Shown inline as a horizontal strip of the most-used categories with a "More"
 * affordance, rather than a dropdown. Choosing a category is on the critical
 * path of the add-expense flow, and the brief's target is amount → category →
 * save; a picker that costs a tap to open, a scroll and a tap to close makes
 * that three steps instead of one for the common case.
 */
export function CategoryPicker({ type, value, onChange, error, label = 'Category' }: CategoryPickerProps) {
  const theme = useAppTheme();
  const [sheetOpen, setSheetOpen] = useState(false);
  const [search, setSearch] = useState('');

  const { data: categories, isPending } = useCategories(type);

  const selected = categories?.find((category) => category.id === value);

  // The strip shows the first eight by sort order, plus the current selection if
  // it happens to fall outside them — so an already-chosen category is never
  // invisible on the screen that shows it as chosen.
  const quickPicks = useMemo(() => {
    if (!categories) return [];

    const head = categories.slice(0, 8);
    if (selected && !head.some((c) => c.id === selected.id)) {
      return [selected, ...head.slice(0, 7)];
    }
    return head;
  }, [categories, selected]);

  const filtered = useMemo(() => {
    if (!categories) return [];
    const term = search.trim().toLowerCase();
    if (!term) return categories;
    return categories.filter((category) => category.name.toLowerCase().includes(term));
  }, [categories, search]);

  return (
    <View>
      <View style={styles.labelRow}>
        <AppText variant="bodySmallStrong" color="textSecondary">
          {label}
          <AppText variant="bodySmallStrong" color="error">
            {' *'}
          </AppText>
        </AppText>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="See all categories"
          onPress={() => setSheetOpen(true)}
          hitSlop={8}
        >
          <AppText variant="caption" color="primary">
            See all
          </AppText>
        </Pressable>
      </View>

      {isPending ? (
        <AppLoader fullscreen={false} label="" />
      ) : (
        <FlatList
          data={quickPicks}
          keyExtractor={(item) => item.id}
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={{ gap: theme.spacing.sm, paddingVertical: theme.spacing.xs }}
          renderItem={({ item }) => (
            <CategoryChip
              category={item}
              selected={item.id === value}
              onPress={() => onChange(item.id)}
            />
          )}
          ListFooterComponent={
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="More categories"
              onPress={() => setSheetOpen(true)}
              style={[
                styles.chip,
                {
                  backgroundColor: theme.c.surfaceSunken,
                  borderRadius: theme.radius.medium,
                  borderColor: theme.c.border,
                  minWidth: 74,
                },
              ]}
            >
              <View
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: 12,
                  backgroundColor: theme.c.surface,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <MaterialCommunityIcons
                  name="dots-horizontal"
                  size={22}
                  color={theme.c.textSecondary}
                />
              </View>
              <AppText variant="caption" color="textSecondary" numberOfLines={1}>
                More
              </AppText>
            </Pressable>
          }
        />
      )}

      {error ? (
        <AppText variant="caption" color="error" style={{ marginTop: 6 }}>
          {error}
        </AppText>
      ) : null}

      <BottomSheet
        visible={sheetOpen}
        onClose={() => {
          setSheetOpen(false);
          setSearch('');
        }}
        title="Choose a category"
        snapPercent={0.85}
      >
        <View style={{ gap: theme.spacing.base, flex: 1 }}>
          <SearchBar value={search} onChangeText={setSearch} placeholder="Search categories" />

          <FlatList
            data={filtered}
            keyExtractor={(item) => item.id}
            numColumns={4}
            columnWrapperStyle={{ gap: theme.spacing.sm }}
            contentContainerStyle={{ gap: theme.spacing.sm, paddingBottom: theme.spacing.xl }}
            keyboardShouldPersistTaps="handled"
            renderItem={({ item }) => (
              <CategoryChip
                category={item}
                selected={item.id === value}
                grow
                onPress={() => {
                  onChange(item.id);
                  setSheetOpen(false);
                  setSearch('');
                }}
              />
            )}
            ListEmptyComponent={
              <View style={{ padding: theme.spacing.xl, alignItems: 'center' }}>
                <AppText variant="bodySmall" color="textSecondary">
                  No categories match “{search}”
                </AppText>
              </View>
            }
          />
        </View>
      </BottomSheet>
    </View>
  );
}

function CategoryChip({
  category,
  selected,
  onPress,
  grow,
}: {
  category: Category;
  selected: boolean;
  onPress: () => void;
  grow?: boolean;
}) {
  const theme = useAppTheme();

  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ selected }}
      accessibilityLabel={category.name}
      onPress={onPress}
      style={[
        styles.chip,
        {
          flex: grow ? 1 : undefined,
          minWidth: grow ? undefined : 74,
          borderRadius: theme.radius.medium,
          borderWidth: selected ? 2 : 1,
          borderColor: selected ? theme.c.primary : theme.c.border,
          backgroundColor: selected ? theme.c.primaryMuted : theme.c.surface,
          padding: selected ? theme.spacing.sm - 1 : theme.spacing.sm,
        },
      ]}
    >
      <CategoryIcon icon={category.icon} color={category.color} />
      <AppText
        variant="caption"
        color={selected ? 'primary' : 'textSecondary'}
        numberOfLines={1}
        align="center"
      >
        {category.name}
      </AppText>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  labelRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 6,
  },
  chip: {
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    padding: 8,
    borderWidth: 1,
  },
});
