import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Image } from 'expo-image';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { CategoryIcon } from '@/components/CategoryIcon';
import { CurrencyText } from '@/components/CurrencyText';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { AppErrorState, AppLoader } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { PAYMENT_METHOD_ICONS, PAYMENT_METHOD_LABELS } from '@/constants/icons';
import { useAuthenticatedImageSource } from '@/features/receipts/use-authenticated-image';
import { useDeleteTransaction, useTransaction } from '@/features/transactions/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ReceiptSummary } from '@/types/api';
import { formatDateTime } from '@/utils/date';

export default function TransactionDetailScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: transaction, isPending, isError, error, refetch } = useTransaction(id);
  const remove = useDeleteTransaction();

  const [confirmDelete, setConfirmDelete] = useState(false);

  if (isPending) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Transaction" onBack={() => router.back()} />
        <AppLoader />
      </View>
    );
  }

  if (isError || !transaction) {
    return (
      <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
        <ScreenHeader title="Transaction" onBack={() => router.back()} />
        <AppErrorState
          title="Could not load this transaction"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      </View>
    );
  }

  const details: { icon: React.ComponentProps<typeof MaterialCommunityIcons>['name']; label: string; value: string }[] = [
    {
      icon: 'calendar-outline',
      label: 'Date',
      value: formatDateTime(transaction.transactionDate),
    },
    {
      icon: PAYMENT_METHOD_ICONS[transaction.paymentMethod] ?? 'wallet-outline',
      label: 'Paid with',
      value: PAYMENT_METHOD_LABELS[transaction.paymentMethod] ?? transaction.paymentMethod,
    },
  ];

  if (transaction.merchant) {
    details.push({ icon: 'store-outline', label: 'Merchant', value: transaction.merchant });
  }
  if (transaction.description) {
    details.push({ icon: 'text-short', label: 'Description', value: transaction.description });
  }
  if (transaction.notes) {
    details.push({ icon: 'note-text-outline', label: 'Notes', value: transaction.notes });
  }
  if (transaction.recurringTransactionId) {
    details.push({ icon: 'autorenew', label: 'Source', value: 'Created by a recurring rule' });
  }

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title="Transaction"
        onBack={() => router.back()}
        actions={[
          {
            icon: 'pencil-outline',
            label: 'Edit transaction',
            onPress: () => router.push(`/transaction/${transaction.id}/edit`),
          },
          {
            icon: 'trash-can-outline',
            label: 'Delete transaction',
            onPress: () => setConfirmDelete(true),
            destructive: true,
          },
        ]}
      />

      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          gap: theme.spacing.base,
          paddingBottom: theme.spacing.xxxl,
        }}
        showsVerticalScrollIndicator={false}
      >
        <AppCard elevation="medium">
          <View style={styles.hero}>
            <CategoryIcon
              icon={transaction.categoryIcon}
              color={transaction.categoryColor}
              size="large"
            />

            <CurrencyText
              amount={transaction.amount}
              currencyCode={transaction.currencyCode}
              type={transaction.type}
              variant="display"
              style={{ marginTop: theme.spacing.md }}
              numberOfLines={1}
            />

            <View
              style={[
                styles.typeBadge,
                {
                  backgroundColor:
                    transaction.type === 'Income' ? theme.c.incomeMuted : theme.c.surfaceSunken,
                  borderRadius: theme.radius.pill,
                  paddingHorizontal: theme.spacing.md,
                  paddingVertical: 4,
                  marginTop: theme.spacing.sm,
                  gap: 6,
                },
              ]}
            >
              <MaterialCommunityIcons
                name={transaction.type === 'Income' ? 'arrow-down-left' : 'arrow-up-right'}
                size={13}
                color={transaction.type === 'Income' ? theme.c.income : theme.c.textSecondary}
              />
              <AppText
                variant="caption"
                color={transaction.type === 'Income' ? 'income' : 'textSecondary'}
              >
                {transaction.type} · {transaction.categoryName}
              </AppText>
            </View>
          </View>
        </AppCard>

        <AppCard padding="none">
          {details.map((detail, index) => (
            <View
              key={detail.label}
              style={[
                styles.detailRow,
                {
                  padding: theme.spacing.base,
                  gap: theme.spacing.md,
                  borderTopWidth: index === 0 ? 0 : 1,
                  borderTopColor: theme.c.border,
                },
              ]}
            >
              <MaterialCommunityIcons
                name={detail.icon}
                size={18}
                color={theme.c.textTertiary}
                accessibilityElementsHidden
              />
              <AppText variant="bodySmall" color="textSecondary" style={{ width: 92 }}>
                {detail.label}
              </AppText>
              <AppText variant="bodySmall" style={styles.flex}>
                {detail.value}
              </AppText>
            </View>
          ))}
        </AppCard>

        {transaction.receipts.length > 0 ? (
          <AppCard>
            <AppText variant="heading3">Receipts</AppText>

            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={{ gap: theme.spacing.sm, marginTop: theme.spacing.md }}
            >
              {transaction.receipts.map((receipt) => (
                <ReceiptPreview key={receipt.id} receipt={receipt} />
              ))}
            </ScrollView>
          </AppCard>
        ) : null}

        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Edit this transaction"
          onPress={() => router.push(`/transaction/${transaction.id}/edit`)}
          style={{ alignSelf: 'center', paddingVertical: theme.spacing.sm }}
          hitSlop={8}
        >
          <AppText variant="bodySmallStrong" color="primary">
            Edit transaction
          </AppText>
        </Pressable>
      </ScrollView>

      <ConfirmDialog
        visible={confirmDelete}
        title="Delete transaction?"
        message="This transaction will be permanently removed from your records."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(transaction.id);
            setConfirmDelete(false);
            toast.show({ title: 'Transaction deleted', tone: 'success' });
            router.back();
          } catch (error) {
            setConfirmDelete(false);
            toast.show({
              title: 'Could not delete',
              description: error instanceof Error ? error.message : undefined,
              tone: 'error',
            });
          }
        }}
      />
    </View>
  );
}

function ReceiptPreview({ receipt }: { receipt: ReceiptSummary }) {
  const theme = useAppTheme();
  const source = useAuthenticatedImageSource(receipt.url);

  return (
    <Image
      source={source}
      style={{
        width: 120,
        height: 160,
        borderRadius: theme.radius.medium,
        backgroundColor: theme.c.surfaceSunken,
      }}
      contentFit="cover"
      transition={150}
      accessibilityLabel={`Receipt ${receipt.fileName}`}
    />
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  hero: {
    alignItems: 'center',
    paddingVertical: 8,
  },
  typeBadge: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  detailRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
