import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';
import { ApiError } from '@/api/client';
import { CategoryIcon } from '@/components/CategoryIcon';
import { CurrencyText } from '@/components/CurrencyText';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppEmptyState, AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import { describeRecurrence } from '@/features/recurring/RecurringForm';
import { useRecurringList, useSetRecurringPaused } from '@/features/recurring/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { RecurringTransaction } from '@/types/api';
import { formatCurrency } from '@/utils/currency';
import { formatDate } from '@/utils/date';

/**
 * The list of recurring schedules.
 *
 * Every row answers the only two questions anyone opens this screen with: what
 * is going to happen, and when. Pausing is on the row itself rather than behind
 * a detail screen, because "stop taking this out" is the action people come
 * here in a hurry to perform.
 */
export default function RecurringListScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const [refreshing, setRefreshing] = useState(false);
  const { data, isPending, isError, error, refetch } = useRecurringList();
  const setPaused = useSetRecurringPaused();

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refetch();
    } finally {
      setRefreshing(false);
    }
  }, [refetch]);

  const togglePaused = async (schedule: RecurringTransaction) => {
    const nextPaused = !schedule.isPaused;

    try {
      await setPaused.mutateAsync({ id: schedule.id, isPaused: nextPaused });

      toast.show({
        title: nextPaused ? `${schedule.name} paused` : `${schedule.name} resumed`,
        description: nextPaused
          ? 'Nothing will be added until you resume it.'
          : `Next on ${formatDate(schedule.nextRunDate)}`,
        tone: 'success',
      });
    } catch (caught) {
      toast.show({
        title: nextPaused ? 'Could not pause that' : 'Could not resume that',
        description: caught instanceof ApiError ? caught.message : undefined,
        tone: 'error',
      });
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title="Recurring"
        onBack={() => router.back()}
        actions={[
          {
            icon: 'plus',
            label: 'Add a recurring transaction',
            onPress: () => router.push('/recurring/new'),
          },
        ]}
      />

      {isError ? (
        <AppErrorState
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending ? (
        <View style={{ padding: theme.spacing.base }}>
          <ListSkeleton />
        </View>
      ) : !data || data.length === 0 ? (
        <AppEmptyState
          title="Nothing on repeat yet"
          description="Rent, salary, subscriptions — set one up once and it is added on schedule, so you stop having to remember it."
          actionLabel="Add your first one"
          onAction={() => router.push('/recurring/new')}
          icon={<MaterialCommunityIcons name="autorenew" size={44} color={theme.c.textTertiary} />}
        />
      ) : (
        <FlatList
          data={data}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{
            padding: theme.spacing.base,
            paddingBottom: theme.spacing.xxxl,
            gap: theme.spacing.md,
          }}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={onRefresh}
              tintColor={theme.c.primary}
              colors={[theme.c.primary]}
            />
          }
          showsVerticalScrollIndicator={false}
          renderItem={({ item }) => (
            <ScheduleRow
              schedule={item}
              onPress={() => router.push(`/recurring/${item.id}`)}
              onTogglePaused={() => void togglePaused(item)}
              // Only the row being changed shows a spinner; the mutation is
              // shared, so keying off `isPending` alone would spin all of them.
              busy={setPaused.isPending && setPaused.variables?.id === item.id}
            />
          )}
        />
      )}
    </View>
  );
}

function ScheduleRow({
  schedule,
  onPress,
  onTogglePaused,
  busy,
}: {
  schedule: RecurringTransaction;
  onPress: () => void;
  onTogglePaused: () => void;
  busy: boolean;
}) {
  const theme = useAppTheme();

  const cadence = describeRecurrence(schedule.frequency, schedule.interval);
  const meta = `${cadence} · Next ${formatDate(schedule.nextRunDate)}`;

  // A paused schedule is dimmed, but dimming is never the only signal — a low
  // contrast difference is invisible to plenty of people and says nothing at all
  // to a screen reader. The chip and this label carry the actual meaning.
  const muted = schedule.isPaused ? theme.opacity.muted : 1;

  const rowLabel = [
    schedule.name,
    schedule.type === 'Income' ? 'income' : 'expense',
    formatCurrency(schedule.amount, schedule.currencyCode),
    cadence,
    schedule.isPaused ? 'Paused' : `next on ${formatDate(schedule.nextRunDate)}`,
  ].join(', ');

  return (
    <AppCard padding="none">
      <View style={styles.row}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={rowLabel}
          accessibilityHint="Opens this schedule"
          onPress={onPress}
          style={({ pressed }) => [
            styles.body,
            {
              padding: theme.spacing.base,
              paddingRight: theme.spacing.sm,
              gap: theme.spacing.md,
              minHeight: theme.hitTarget.comfortable,
              opacity: pressed ? theme.opacity.pressed : 1,
            },
          ]}
        >
          <CategoryIcon
            icon={schedule.categoryIcon}
            color={schedule.categoryColor}
            style={{ opacity: muted }}
          />

          <View style={styles.flex}>
            <View style={[styles.titleRow, { gap: theme.spacing.sm }]}>
              <AppText
                variant="bodyStrong"
                color={schedule.isPaused ? 'textSecondary' : 'textPrimary'}
                numberOfLines={1}
                style={styles.shrink}
              >
                {schedule.name}
              </AppText>

              {schedule.isPaused ? (
                <View
                  style={{
                    backgroundColor: theme.c.warningMuted,
                    borderRadius: theme.radius.pill,
                    paddingHorizontal: theme.spacing.sm,
                    paddingVertical: theme.spacing.xxs,
                  }}
                >
                  <AppText variant="caption" color="warning">
                    Paused
                  </AppText>
                </View>
              ) : null}
            </View>

            <AppText
              variant="caption"
              color="textSecondary"
              numberOfLines={1}
              style={{ marginTop: theme.spacing.xxs, opacity: muted }}
            >
              {meta}
            </AppText>
          </View>

          <CurrencyText
            amount={schedule.amount}
            currencyCode={schedule.currencyCode}
            type={schedule.type}
            variant="bodySmallStrong"
            numberOfLines={1}
            style={{ opacity: muted }}
          />
        </Pressable>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel={
            schedule.isPaused ? `Resume ${schedule.name}` : `Pause ${schedule.name}`
          }
          accessibilityState={{ disabled: busy }}
          disabled={busy}
          onPress={onTogglePaused}
          hitSlop={4}
          style={({ pressed }) => [
            styles.toggle,
            {
              width: theme.hitTarget.min,
              minHeight: theme.hitTarget.min,
              marginRight: theme.spacing.sm,
              borderRadius: theme.radius.pill,
              backgroundColor: pressed ? theme.c.surfaceSunken : 'transparent',
            },
          ]}
        >
          {busy ? (
            <ActivityIndicator size="small" color={theme.c.primary} />
          ) : (
            <MaterialCommunityIcons
              name={schedule.isPaused ? 'play' : 'pause'}
              size={22}
              color={schedule.isPaused ? theme.c.primary : theme.c.textSecondary}
            />
          )}
        </Pressable>
      </View>
    </AppCard>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  shrink: { flexShrink: 1 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  body: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  toggle: {
    alignItems: 'center',
    justifyContent: 'center',
  },
});
