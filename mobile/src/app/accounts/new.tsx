import { useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { AccountForm } from '@/features/accounts/AccountForm';
import { useCreateAccount } from '@/features/accounts/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';

export default function NewAccountScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const create = useCreateAccount();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="New account" onBack={() => router.back()} />

      <AccountForm
        submitLabel="Add account"
        submitting={create.isPending}
        onSubmit={async (values) => {
          const saved = await create.mutateAsync(values);
          toast.show({ title: `${saved.name} added`, tone: 'success' });
          router.back();
        }}
      />
    </View>
  );
}
