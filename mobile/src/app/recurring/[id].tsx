import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { ApiError } from '@/api/client';
import { AppButton } from '@/components/ui/AppButton';
import { AppCard } from '@/components/ui/AppCard';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { ListSkeleton } from '@/components/ui/Skeleton';
import { AppErrorState } from '@/components/ui/StateViews';
import { useToast } from '@/components/ui/Toast';
import {
  RecurringForm,
  describeRecurrence,
  describeReminder,
} from '@/features/recurring/RecurringForm';
import {
  useDeleteRecurring,
  useRecurring,
  useSetRecurringPaused,
  useUpdateRecurring,
} from '@/features/recurring/hooks';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { RecurringTransaction } from '@/types/api';
import { formatDate } from '@/utils/date';

/**
 * Edit a recurring transaction.
 *
 * Pause is the action offered first and delete is the one that costs a
 * confirmation, because they are not interchangeable: pausing keeps the
 * schedule and the transactions it has already generated, deleting throws away
 * a setup the user is unlikely to remember well enough to rebuild.
 */
export default function EditRecurringScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const toast = useToast();

  const { id } = useLocalSearchParams<{ id: string }>();
  const { data, isPending, isError, error, refetch } = useRecurring(id);

  const update = useUpdateRecurring();
  const setPaused = useSetRecurringPaused();
  const remove = useDeleteRecurring();

  const [confirmDelete, setConfirmDelete] = useState(false);
  // Deleting invalidates this record, so the detail query refetches and 404s
  // while the screen is still mounted. This keeps the error state suppressed
  // for the frame or two before the navigation lands.
  const [deleted, setDeleted] = useState(false);

  const goBack = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/recurring');
    }
  };

  const togglePaused = async (schedule: RecurringTransaction) => {
    const nextPaused = !schedule.isPaused;

    try {
      await setPaused.mutateAsync({ id: schedule.id, isPaused: nextPaused });

      toast.show({
        title: nextPaused ? 'Schedule paused' : 'Schedule resumed',
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

  const deleteSchedule = async () => {
    if (!id) return;

    try {
      await remove.mutateAsync(id);
      setDeleted(true);
      setConfirmDelete(false);

      toast.show({
        title: 'Schedule deleted',
        description: 'Transactions it already created are untouched.',
        tone: 'success',
      });

      goBack();
    } catch (caught) {
      setConfirmDelete(false);
      toast.show({
        title: 'Could not delete that schedule',
        description: caught instanceof ApiError ? caught.message : undefined,
        tone: 'error',
      });
    }
  };

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader
        title={data?.name ?? 'Schedule'}
        subtitle={data ? describeRecurrence(data.frequency, data.interval) : undefined}
        onBack={() => router.back()}
      />

      {isError && !deleted ? (
        <AppErrorState
          title="Could not load this schedule"
          description={error instanceof Error ? error.message : undefined}
          onRetry={() => void refetch()}
        />
      ) : isPending || !data ? (
        <View style={{ padding: theme.spacing.base }}>
          <ListSkeleton count={4} />
        </View>
      ) : (
        <RecurringForm
          initial={data}
          submitLabel="Save changes"
          submitting={update.isPending}
          onSubmit={async (values) => {
            const saved = await update.mutateAsync({ id: data.id, body: values });

            toast.show({
              title: 'Schedule updated',
              description: `${describeRecurrence(saved.frequency, saved.interval)} · Next on ${formatDate(saved.nextRunDate)}`,
              tone: 'success',
            });

            goBack();
          }}
          footer={
            <View style={{ gap: theme.spacing.md }}>
              <ScheduleStatus schedule={data} />

              <AppButton
                label={data.isPaused ? 'Resume schedule' : 'Pause schedule'}
                variant="outline"
                fullWidth
                loading={setPaused.isPending}
                onPress={() => void togglePaused(data)}
                leadingIcon={
                  <MaterialCommunityIcons
                    name={data.isPaused ? 'play' : 'pause'}
                    size={18}
                    color={theme.c.textPrimary}
                  />
                }
              />

              {/* A pressable rather than an AppButton: no variant renders an
                  error-coloured label on a plain surface, and a solid red bar
                  would out-shout Save on a screen where saving is the point. */}
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Delete schedule"
                onPress={() => setConfirmDelete(true)}
                style={({ pressed }) => [
                  styles.deleteButton,
                  {
                    minHeight: theme.hitTarget.comfortable,
                    backgroundColor: theme.c.surface,
                    borderRadius: theme.radius.medium,
                    borderWidth: 1,
                    borderColor: theme.c.border,
                    paddingVertical: theme.spacing.md,
                    gap: theme.spacing.sm,
                    opacity: pressed ? theme.opacity.pressed : 1,
                  },
                ]}
              >
                <MaterialCommunityIcons
                  name="trash-can-outline"
                  size={18}
                  color={theme.c.error}
                />
                <AppText variant="bodyStrong" color="error">
                  Delete schedule
                </AppText>
              </Pressable>
            </View>
          }
        />
      )}

      <ConfirmDialog
        visible={confirmDelete}
        title="Delete this schedule?"
        message="It will stop creating transactions. The ones it has already created stay in your ledger. If you only want to stop it for now, pause it instead."
        confirmLabel="Delete"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => void deleteSchedule()}
      />
    </View>
  );
}

/** What this schedule has done and what it will do next — the context an edit form cannot show. */
function ScheduleStatus({ schedule }: { schedule: RecurringTransaction }) {
  const theme = useAppTheme();

  const rows: { icon: React.ComponentProps<typeof MaterialCommunityIcons>['name']; label: string }[] = [
    {
      icon: 'calendar-arrow-right',
      label: schedule.isPaused
        ? `Paused — was next on ${formatDate(schedule.nextRunDate)}`
        : `Next on ${formatDate(schedule.nextRunDate)}`,
    },
    {
      icon: 'history',
      label: schedule.lastRunDate
        ? `Last added ${formatDate(schedule.lastRunDate)} · ${schedule.occurrencesGenerated} so far`
        : 'Has not run yet',
    },
    {
      icon: 'bell-outline',
      label: `Reminder ${describeReminder(schedule.reminderDaysBefore).toLowerCase()}`,
    },
  ];

  if (schedule.endDate) {
    rows.push({ icon: 'calendar-end', label: `Ends ${formatDate(schedule.endDate)}` });
  }

  return (
    <AppCard>
      <View style={{ gap: theme.spacing.sm }}>
        {schedule.isPaused ? (
          <View
            style={{
              alignSelf: 'flex-start',
              backgroundColor: theme.c.warningMuted,
              borderRadius: theme.radius.pill,
              paddingHorizontal: theme.spacing.sm,
              paddingVertical: theme.spacing.xxs,
              marginBottom: theme.spacing.xs,
            }}
          >
            <AppText variant="caption" color="warning">
              Paused
            </AppText>
          </View>
        ) : null}

        {rows.map((row) => (
          <View key={row.label} style={[styles.statusRow, { gap: theme.spacing.sm }]}>
            <MaterialCommunityIcons name={row.icon} size={16} color={theme.c.textTertiary} />
            <AppText variant="bodySmall" color="textSecondary" style={styles.flex}>
              {row.label}
            </AppText>
          </View>
        ))}
      </View>
    </AppCard>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  deleteButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
