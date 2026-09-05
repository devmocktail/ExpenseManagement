import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { AppButton } from '@/components/ui/AppButton';
import { AppText } from '@/components/ui/AppText';
import { BottomSheet } from '@/components/ui/BottomSheet';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { AppErrorState, AppLoader } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { CategoryForm } from '@/features/categories/CategoryForm';
import { useCategories, useDeleteCategory, useUpdateCategory } from '@/features/categories/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { Category } from '@/types/api';

/**
 * Edit a category.
 *
 * Deleting is the whole reason this screen is more than a form. The server will
 * not orphan history, so a category that is in use can only be removed together
 * with a destination for its transactions — the sheet below makes that the
 * user's decision up front instead of a rejected request after the fact.
 */
export default function CategoryDetailScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data, isPending, isError, error, refetch } = useCategories();

  const update = useUpdateCategory();
  const remove = useDeleteCategory();

  const [confirmOpen, setConfirmOpen] = useState(false);
  const [reassignOpen, setReassignOpen] = useState(false);
  const [reassignTo, setReassignTo] = useState<string | null>(null);
  const [deleted, setDeleted] = useState(false);

  const category = data?.find((item) => item.id === id);

  // Only categories on the same side of the ledger can take these transactions;
  // moving an expense into an income category would flip its sign.
  const candidates = useMemo(
    () =>
      data && category
        ? data.filter((item) => item.type === category.type && item.id !== category.id)
        : [],
    [data, category],
  );

  // Deleting invalidates the list this screen reads from, so for one frame the
  // category is gone while the navigation back has not landed yet. Without this
  // flag that frame renders "could not load", which looks like a failure.
  if (isPending || deleted) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Category" onBack={() => router.back()} />
        <AppLoader />
      </View>
    );
  }

  if (isError || !category) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Category" onBack={() => router.back()} />
        <AppErrorState
          title="Could not load this category"
          description={
            isError && error instanceof Error ? error.message : 'It may have been deleted already.'
          }
          onRetry={() => void refetch()}
        />
      </View>
    );
  }

  const inUse = category.transactionCount > 0;

  const closeDelete = () => {
    setConfirmOpen(false);
    setReassignOpen(false);
    setReassignTo(null);
  };

  const performDelete = async (reassignToCategoryId?: string) => {
    try {
      await remove.mutateAsync({
        id: category.id,
        body: reassignToCategoryId ? { reassignToCategoryId } : undefined,
      });

      const movedTo = candidates.find((item) => item.id === reassignToCategoryId);

      setDeleted(true);
      closeDelete();

      toast.show({
        title: `${category.name} deleted`,
        description: movedTo
          ? `${countLabel(category.transactionCount)} moved to ${movedTo.name}`
          : undefined,
        tone: 'success',
      });

      if (router.canGoBack()) {
        router.back();
      } else {
        router.replace('/category');
      }
    } catch (deleteError) {
      closeDelete();
      toast.show({
        title: 'Could not delete this category',
        description: deleteError instanceof Error ? deleteError.message : undefined,
        tone: 'error',
      });
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Edit category" subtitle={category.name} onBack={() => router.back()} />

      <CategoryForm
        initial={category}
        submitLabel="Save changes"
        submitting={update.isPending}
        onSubmit={async (values) => {
          await update.mutateAsync({
            id: category.id,
            body: {
              name: values.name,
              icon: values.icon,
              color: values.color,
              // Sent back unchanged so a rename cannot quietly reset where this
              // category sits in every picker in the app.
              sortOrder: category.sortOrder,
            },
          });

          toast.show({ title: 'Changes saved', tone: 'success' });
          router.back();
        }}
        footer={
          <DangerZone
            category={category}
            onDelete={() => (inUse ? setReassignOpen(true) : setConfirmOpen(true))}
          />
        }
      />

      <ConfirmDialog
        visible={confirmOpen}
        title={`Delete ${category.name}?`}
        message="Nothing is filed under it, so this only removes the category itself."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={closeDelete}
        onConfirm={() => void performDelete()}
      />

      <BottomSheet
        visible={reassignOpen}
        onClose={closeDelete}
        title={`Move ${countLabel(category.transactionCount)}`}
        snapPercent={0.85}
      >
        <View style={[styles.flex, { gap: theme.spacing.base }]}>
          <AppText variant="bodySmall" color="textSecondary">
            {category.transactionCount === 1
              ? `One transaction is filed under ${category.name}. Pick where it should go — deleting cannot be undone, but the transaction itself is kept.`
              : `${category.transactionCount} transactions are filed under ${category.name}. Pick where they should go — deleting cannot be undone, but the transactions themselves are kept.`}
          </AppText>

          {candidates.length === 0 ? (
            <View style={[styles.centered, { gap: theme.spacing.base }]}>
              <MaterialCommunityIcons
                name="folder-alert-outline"
                size={44}
                color={theme.c.textTertiary}
              />

              <AppText variant="bodySmall" color="textSecondary" align="center">
                This is your only {category.type === 'Income' ? 'income' : 'expense'} category, so
                there is nowhere to move these transactions. Create another one first.
              </AppText>

              <AppButton
                label="Create a category"
                variant="outline"
                onPress={() => {
                  closeDelete();
                  router.push({ pathname: '/category/new', params: { type: category.type } });
                }}
              />
            </View>
          ) : (
            <>
              <FlatList
                data={candidates}
                keyExtractor={(item) => item.id}
                contentContainerStyle={{ paddingBottom: theme.spacing.base }}
                showsVerticalScrollIndicator={false}
                renderItem={({ item }) => (
                  <ReassignRow
                    category={item}
                    selected={item.id === reassignTo}
                    onPress={() => setReassignTo(item.id)}
                  />
                )}
              />

              <AppButton
                label="Move and delete"
                variant="danger"
                size="large"
                fullWidth
                // Disabled until a destination exists: the request would be
                // rejected by the server anyway, and a failure the user could
                // have been warned about is worse than a disabled button.
                disabled={!reassignTo}
                loading={remove.isPending}
                onPress={() => void performDelete(reassignTo ?? undefined)}
              />
            </>
          )}
        </View>
      </BottomSheet>
    </View>
  );
}

/**
 * The delete affordance, or the reason there isn't one.
 *
 * System categories get an explanation rather than a disabled button: a control
 * that is visible but never usable invites repeated taps, while the sentence
 * answers the question the button would have raised.
 */
function DangerZone({ category, onDelete }: { category: Category; onDelete: () => void }) {
  const theme = useAppTheme();

  if (category.isSystem) {
    return (
      <View
        style={[
          styles.note,
          {
            backgroundColor: theme.c.surfaceSunken,
            borderRadius: theme.radius.medium,
            padding: theme.spacing.md,
            gap: theme.spacing.sm,
          },
        ]}
      >
        <MaterialCommunityIcons name="lock-outline" size={16} color={theme.c.textTertiary} />
        <AppText variant="caption" color="textTertiary" style={styles.flex}>
          {category.name} is a default category, so it cannot be deleted. Renaming it or changing
          its icon and colour works as usual.
        </AppText>
      </View>
    );
  }

  return (
    <View style={{ gap: theme.spacing.sm }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Delete ${category.name}`}
        accessibilityHint={
          category.transactionCount > 0
            ? 'Asks where to move the transactions in this category first'
            : 'Asks you to confirm'
        }
        onPress={onDelete}
        style={({ pressed }) => [
          styles.destructive,
          {
            backgroundColor: theme.c.surface,
            borderRadius: theme.radius.medium,
            borderWidth: 1,
            borderColor: theme.c.border,
            paddingVertical: theme.spacing.base,
            minHeight: theme.hitTarget.min,
            gap: theme.spacing.sm,
            opacity: pressed ? theme.opacity.pressed : 1,
          },
        ]}
      >
        <MaterialCommunityIcons name="trash-can-outline" size={18} color={theme.c.error} />
        <AppText variant="bodyStrong" color="error">
          Delete category
        </AppText>
      </Pressable>

      <AppText variant="caption" color="textTertiary" align="center">
        {category.transactionCount > 0
          ? `${countLabel(category.transactionCount)} will move to a category you choose.`
          : 'Nothing is filed under this category yet.'}
      </AppText>
    </View>
  );
}

function ReassignRow({
  category,
  selected,
  onPress,
}: {
  category: Category;
  selected: boolean;
  onPress: () => void;
}) {
  const theme = useAppTheme();

  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ selected }}
      accessibilityLabel={`Move them to ${category.name}, ${countLabel(category.transactionCount)}`}
      onPress={onPress}
      style={({ pressed }) => [
        styles.reassignRow,
        {
          paddingVertical: theme.spacing.md,
          paddingHorizontal: theme.spacing.sm,
          gap: theme.spacing.md,
          minHeight: theme.hitTarget.comfortable,
          borderRadius: theme.radius.medium,
          backgroundColor: selected
            ? theme.c.primaryMuted
            : pressed
              ? theme.c.surfaceSunken
              : 'transparent',
        },
      ]}
    >
      <CategoryIcon icon={category.icon} color={category.color} size="small" />

      <View style={styles.flex}>
        <AppText variant="bodySmallStrong" numberOfLines={1}>
          {category.name}
        </AppText>
        <AppText variant="caption" color="textTertiary">
          {countLabel(category.transactionCount)}
        </AppText>
      </View>

      <MaterialCommunityIcons
        name={selected ? 'radiobox-marked' : 'radiobox-blank'}
        size={22}
        color={selected ? theme.c.primary : theme.c.textTertiary}
      />
    </Pressable>
  );
}

function countLabel(count: number): string {
  if (count === 0) return 'No transactions';
  return `${count} ${count === 1 ? 'transaction' : 'transactions'}`;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  destructive: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  reassignRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
