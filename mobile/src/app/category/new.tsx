import { useLocalSearchParams, useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { CategoryForm } from '@/features/categories/CategoryForm';
import { useCreateCategory } from '@/features/categories/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { TransactionType } from '@/types/api';

/**
 * Create a category.
 *
 * The type is seeded from whichever tab the user was looking at, because they
 * almost always tapped "add" while already on the side of the ledger they meant
 * — and it is the one field they cannot change afterwards.
 */
export default function NewCategoryScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const params = useLocalSearchParams<{ type?: string }>();
  const defaultType: TransactionType = params.type === 'Income' ? 'Income' : 'Expense';

  const create = useCreateCategory();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="New category" onBack={() => router.back()} />

      <CategoryForm
        defaultType={defaultType}
        submitLabel="Create category"
        submitting={create.isPending}
        onSubmit={async (values) => {
          const saved = await create.mutateAsync(values);

          toast.show({
            title: `${saved.name} added`,
            description:
              saved.type === 'Income'
                ? 'It will show up when you record income.'
                : 'It will show up when you record an expense.',
            tone: 'success',
          });

          // Back rather than a replace, so a user who opened this from the
          // transaction form or the list returns where they started.
          if (router.canGoBack()) {
            router.back();
          } else {
            router.replace('/category');
          }
        }}
      />
    </View>
  );
}
