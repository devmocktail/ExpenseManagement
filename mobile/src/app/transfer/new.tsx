import { useRouter } from 'expo-router';
import { View } from 'react-native';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useToast } from '@/components/ui/Toast';
import { TransferForm } from '@/features/transfers/TransferForm';
import { useCreateTransfer } from '@/features/transfers/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatCurrency } from '@/utils/currency';

/**
 * Record a movement between two accounts.
 *
 * The confirmation names both ends rather than just the amount: "₹10,000" is
 * ambiguous on a screen whose whole job is saying which way the money went.
 */
export default function NewTransferScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const create = useCreateTransfer();

  return (
    <View style={{ flex: 1, backgroundColor: theme.c.background }}>
      <ScreenHeader title="Move money" onBack={() => router.back()} />

      <TransferForm
        submitLabel="Save transfer"
        submitting={create.isPending}
        onSubmit={async (values) => {
          const saved = await create.mutateAsync(values);

          toast.show({
            title: 'Transfer recorded',
            description: `${formatCurrency(saved.amount, saved.currencyCode)} · ${saved.fromAccountName} → ${saved.toAccountName}`,
            tone: 'success',
          });

          if (router.canGoBack()) {
            router.back();
          } else {
            router.replace('/(tabs)/transactions');
          }
        }}
      />
    </View>
  );
}
