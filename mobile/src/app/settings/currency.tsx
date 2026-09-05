import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { useSettings, useUpdateSettings } from '@/features/profile/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import { SUPPORTED_CURRENCIES, formatCurrency, getCurrencyMeta } from '@/utils/currency';

/** A representative amount, used only to preview how a currency will render. */
const SAMPLE_AMOUNT = 123456.78;

/**
 * Currency picker.
 *
 * Selecting saves immediately rather than collecting a change behind a Save
 * button — there is exactly one field here, so a second confirmation step would
 * be ceremony. The row that is saving shows its own spinner so the user knows
 * which choice is in flight.
 */
export default function CurrencyScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const { data, isPending, isError, error, refetch } = useSettings();
  const updateSettings = useUpdateSettings();

  const [savingCode, setSavingCode] = useState<string | null>(null);

  const selectedCode = data?.currencyCode;

  const select = async (code: string) => {
    if (code === selectedCode || savingCode) return;

    setSavingCode(code);

    try {
      await updateSettings.mutateAsync({ currencyCode: code });

      const meta = getCurrencyMeta(code);
      toast.show({
        title: `Now showing ${meta.name}`,
        description: `Amounts will look like ${formatCurrency(SAMPLE_AMOUNT, code)}`,
        tone: 'success',
      });
    } catch {
      toast.show({
        title: 'Could not change your currency',
        description: 'Check your connection and try again.',
        tone: 'error',
      });
    } finally {
      setSavingCode(null);
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Currency" onBack={() => router.back()} />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ padding: theme.spacing.base }}>
          <ListSkeleton count={6} />
        </View>
      ) : (
        <ScrollView
          contentContainerStyle={{
            padding: theme.spacing.base,
            paddingBottom: insets.bottom + theme.spacing.xxl,
            gap: theme.spacing.base,
          }}
          showsVerticalScrollIndicator={false}
        >
          <View
            style={[
              styles.note,
              {
                gap: theme.spacing.sm,
                backgroundColor: theme.c.infoMuted,
                borderRadius: theme.radius.medium,
                padding: theme.spacing.md,
              },
            ]}
          >
            <MaterialCommunityIcons name="information-outline" size={16} color={theme.c.info} />
            <AppText variant="caption" color="textSecondary" style={styles.flex}>
              This changes how amounts are displayed. Nothing is converted: a transaction saved as
              50 stays 50, it is simply shown with the new symbol. Change this only if you were
              recording in the new currency all along.
            </AppText>
          </View>

          {/* The list is a build-time constant in utils/currency, so it cannot
              arrive empty — the only failure mode worth a state is the settings
              request above. */}
          <AppCard padding="none">
            {SUPPORTED_CURRENCIES.map((currency, index) => {
              const selected = currency.code === selectedCode;
              const saving = savingCode === currency.code;

              return (
                <Pressable
                  key={currency.code}
                  accessibilityRole="button"
                  accessibilityState={{ selected, busy: saving, disabled: savingCode !== null }}
                  accessibilityLabel={`${currency.name}, ${currency.code}${
                    selected ? ', currently selected' : ''
                  }`}
                  disabled={savingCode !== null}
                  onPress={() => void select(currency.code)}
                  style={({ pressed }) => [
                    styles.row,
                    {
                      padding: theme.spacing.base,
                      gap: theme.spacing.md,
                      minHeight: theme.hitTarget.comfortable,
                      borderTopWidth: index === 0 ? 0 : 1,
                      borderTopColor: theme.c.border,
                      backgroundColor: pressed ? theme.c.surfaceSunken : 'transparent',
                    },
                  ]}
                >
                  <View
                    style={{
                      width: theme.hitTarget.min,
                      height: theme.hitTarget.min,
                      borderRadius: theme.hitTarget.min / 2,
                      backgroundColor: selected ? theme.c.primaryMuted : theme.c.surfaceSunken,
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <AppText
                      variant="bodyStrong"
                      color={selected ? 'primary' : 'textSecondary'}
                      numberOfLines={1}
                    >
                      {currency.symbol}
                    </AppText>
                  </View>

                  <View style={styles.flex}>
                    <AppText variant="bodyStrong" numberOfLines={1}>
                      {currency.code}
                    </AppText>
                    <AppText variant="caption" color="textSecondary" numberOfLines={1}>
                      {currency.name} · {formatCurrency(SAMPLE_AMOUNT, currency.code)}
                    </AppText>
                  </View>

                  {saving ? (
                    <ActivityIndicator size="small" color={theme.c.primary} />
                  ) : selected ? (
                    <MaterialCommunityIcons name="check" size={20} color={theme.c.primary} />
                  ) : null}
                </Pressable>
              );
            })}
          </AppCard>

          <AppText variant="caption" color="textTertiary" style={{ paddingHorizontal: 4 }}>
            Missing a currency? Tell us which one and we will add it.
          </AppText>
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
