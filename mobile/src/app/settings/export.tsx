import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
// Expo SDK 54+ file API. Verified against the installed expo-file-system
// (57.0.6), which exports File/Paths from the package root. If a downgrade ever
// removes them, the fallback is `import * as FileSystem from
// 'expo-file-system/legacy'` and writeAsStringAsync.
import { File, Paths } from 'expo-file-system';
import { useRouter } from 'expo-router';
import * as Sharing from 'expo-sharing';
import { useState } from 'react';
import { ScrollView, StyleSheet, Switch, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ApiError } from '@/api/client';
import { profileApi } from '@/api/profile-api';
import { DateTimeField } from '@/components/DateTimeField';
import { AppButton } from '@/components/ui/AppButton';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { SegmentedControl } from '@/components/ui/SegmentedControl';
import { useToast } from '@/components/ui/Toast';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ExportFormat } from '@/types/api';
import { toServerDate } from '@/utils/date';

const FORMAT_META: Record<ExportFormat, { mimeType: string; uti: string; blurb: string }> = {
  csv: {
    mimeType: 'text/csv',
    uti: 'public.comma-separated-values-text',
    blurb: 'One row per transaction. Opens in Excel, Numbers or Google Sheets.',
  },
  json: {
    mimeType: 'application/json',
    uti: 'public.json',
    blurb: 'Full structured records, including anything a spreadsheet would flatten.',
  },
};

/**
 * Export transactions.
 *
 * The download is a three-step chain — fetch text, write a cache file, hand the
 * URI to the share sheet — and each step fails differently, so the button
 * reports which one went wrong rather than a single generic error.
 */
export default function ExportDataScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const toast = useToast();

  const [format, setFormat] = useState<ExportFormat>('csv');
  const [limitRange, setLimitRange] = useState(false);
  const [from, setFrom] = useState(startOfCurrentMonth);
  const [to, setTo] = useState(() => new Date());

  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const rangeInvalid = limitRange && from > to;

  const runExport = async () => {
    if (busy) return;

    if (rangeInvalid) {
      setFormError('The start date must be on or before the end date.');
      return;
    }

    setFormError(null);
    setBusy(true);

    try {
      const text = await profileApi.exportTransactions({
        format,
        from: limitRange ? toServerDate(startOfDay(from)) : undefined,
        // Inclusive of the end date: sending midnight would silently drop
        // everything recorded on the last day the user picked.
        to: limitRange ? toServerDate(endOfDay(to)) : undefined,
      });

      if (!text || text.trim().length === 0) {
        toast.show({
          title: 'Nothing to export',
          description: limitRange
            ? 'No transactions fall inside that date range.'
            : 'Add a transaction first, then export.',
          tone: 'info',
        });
        return;
      }

      const name = fileName(format);
      const file = new File(Paths.cache, name);

      // The cache directory is the right home for this: it is a hand-off to the
      // share sheet, not a document the app is responsible for keeping, and the
      // OS may reclaim it whenever it needs the space.
      file.create({ overwrite: true, intermediates: true });
      file.write(text);

      if (!(await Sharing.isAvailableAsync())) {
        toast.show({
          title: 'Sharing is not available on this device',
          description: `The file was saved as ${name} in the app cache.`,
          tone: 'warning',
        });
        return;
      }

      await Sharing.shareAsync(file.uri, {
        mimeType: FORMAT_META[format].mimeType,
        UTI: FORMAT_META[format].uti,
        dialogTitle: 'Export transactions',
      });

      toast.show({ title: 'Export ready', description: name, tone: 'success' });
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? error.message
          : 'Could not create the export file. Please try again.',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Export transactions" onBack={() => router.back()} />

      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.xxl,
          gap: theme.spacing.base,
        }}
        showsVerticalScrollIndicator={false}
      >
        {formError ? (
          <View
            accessibilityRole="alert"
            style={{
              backgroundColor: theme.c.errorMuted,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
            }}
          >
            <AppText variant="bodySmall" color="error">
              {formError}
            </AppText>
          </View>
        ) : null}

        <AppCard>
          <AppText variant="bodySmallStrong" color="textSecondary">
            Format
          </AppText>

          <View style={{ marginTop: theme.spacing.md }}>
            <SegmentedControl
              value={format}
              onChange={(next) => setFormat(next as ExportFormat)}
              options={[
                { value: 'csv', label: 'CSV' },
                { value: 'json', label: 'JSON' },
              ]}
            />
          </View>

          <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.md }}>
            {FORMAT_META[format].blurb}
          </AppText>
        </AppCard>

        <AppCard>
          <View style={[styles.row, { gap: theme.spacing.md }]}>
            <View style={styles.flex}>
              <AppText variant="bodyStrong">Limit to a date range</AppText>
              <AppText variant="caption" color="textSecondary">
                Off exports every transaction on your account
              </AppText>
            </View>

            <Switch
              value={limitRange}
              onValueChange={setLimitRange}
              accessibilityLabel="Limit the export to a date range"
              trackColor={{ false: theme.c.track, true: theme.c.primary }}
              thumbColor={theme.c.surface}
            />
          </View>

          {limitRange ? (
            <View style={{ marginTop: theme.spacing.base, gap: theme.spacing.base }}>
              <DateTimeField
                label="From"
                value={from}
                onChange={setFrom}
                mode="date"
                maximumDate={to}
              />

              <DateTimeField
                label="To"
                value={to}
                onChange={setTo}
                mode="date"
                minimumDate={from}
                maximumDate={new Date()}
                error={rangeInvalid ? 'The end date must be on or after the start date' : undefined}
              />
            </View>
          ) : null}
        </AppCard>

        <View
          style={[
            styles.note,
            {
              gap: theme.spacing.sm,
              backgroundColor: theme.c.surfaceSunken,
              borderRadius: theme.radius.medium,
              padding: theme.spacing.md,
            },
          ]}
        >
          <MaterialCommunityIcons
            name="information-outline"
            size={16}
            color={theme.c.textTertiary}
          />
          <AppText variant="caption" color="textTertiary" style={styles.flex}>
            The file contains your own transaction records in plain text. Once you share it, it is
            outside this app — think about where it lands.
          </AppText>
        </View>

        <AppButton
          label="Export and share"
          size="large"
          fullWidth
          loading={busy}
          disabled={rangeInvalid}
          leadingIcon={
            <MaterialCommunityIcons name="download-outline" size={18} color={theme.c.onPrimary} />
          }
          onPress={() => void runExport()}
        />
      </ScrollView>
    </View>
  );
}

function fileName(format: ExportFormat): string {
  // Colons are illegal in filenames on some targets the share sheet can reach,
  // so the ISO timestamp is flattened to digits.
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
  return `transactions-${stamp}.${format}`;
}

function startOfCurrentMonth(): Date {
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0);
}

function startOfDay(date: Date): Date {
  const copy = new Date(date);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

function endOfDay(date: Date): Date {
  const copy = new Date(date);
  copy.setHours(23, 59, 59, 999);
  return copy;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  note: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
