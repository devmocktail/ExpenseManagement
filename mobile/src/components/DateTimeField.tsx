import DateTimePicker, {
  type DateTimePickerEvent,
} from '@react-native-community/datetimepicker';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useState } from 'react';
import { Modal, Platform, Pressable, StyleSheet, View } from 'react-native';
import { AppButton } from '@/components/ui/AppButton';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import { formatDate, formatTimeOfDay } from '@/utils/date';

export type DateTimeFieldProps = {
  label: string;
  value: Date;
  onChange: (date: Date) => void;
  mode?: 'date' | 'time' | 'datetime';
  minimumDate?: Date;
  maximumDate?: Date;
  error?: string;
};

/**
 * A date and/or time field.
 *
 * The two platforms need genuinely different handling and pretending otherwise
 * is where date pickers go wrong:
 *
 *  - Android shows a modal dialog that dismisses itself; its `onChange` fires
 *    once with either `set` or `dismissed`, and rendering the picker again
 *    afterwards re-opens it.
 *  - iOS renders an inline spinner that stays mounted, so it needs its own
 *    container and an explicit Done button, and its `onChange` fires on every
 *    scroll tick.
 *
 * Combining `datetime` also differs: Android has no combined mode, so the date
 * dialog is chained into a time dialog.
 */
export function DateTimeField({
  label,
  value,
  onChange,
  mode = 'date',
  minimumDate,
  maximumDate,
  error,
}: DateTimeFieldProps) {
  const theme = useAppTheme();

  const [androidStep, setAndroidStep] = useState<'idle' | 'date' | 'time'>('idle');
  const [iosOpen, setIosOpen] = useState(false);
  const [draft, setDraft] = useState(value);

  const displayText =
    mode === 'time'
      ? formatTimeOfDay(value)
      : mode === 'datetime'
        ? `${formatDate(value)} · ${formatTimeOfDay(value)}`
        : formatDate(value);

  const open = () => {
    setDraft(value);

    if (Platform.OS === 'android') {
      setAndroidStep(mode === 'time' ? 'time' : 'date');
    } else {
      setIosOpen(true);
    }
  };

  const handleAndroidChange = (event: DateTimePickerEvent, picked?: Date) => {
    if (event.type === 'dismissed' || !picked) {
      setAndroidStep('idle');
      return;
    }

    if (androidStep === 'date' && mode === 'datetime') {
      // Carry the existing time across so choosing a date does not reset the
      // clock to midnight before the time step runs.
      const merged = new Date(picked);
      merged.setHours(value.getHours(), value.getMinutes(), 0, 0);
      setDraft(merged);
      setAndroidStep('time');
      return;
    }

    if (androidStep === 'time') {
      const merged = new Date(draft);
      merged.setHours(picked.getHours(), picked.getMinutes(), 0, 0);
      onChange(merged);
    } else {
      onChange(picked);
    }

    setAndroidStep('idle');
  };

  return (
    <View>
      <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: 6 }}>
        {label}
      </AppText>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${label}. Currently ${displayText}`}
        accessibilityHint="Opens a picker"
        onPress={open}
        style={[
          styles.field,
          {
            backgroundColor: theme.c.surface,
            borderColor: error ? theme.c.error : theme.c.border,
            borderWidth: error ? 2 : 1,
            borderRadius: theme.radius.medium,
            paddingHorizontal: theme.spacing.md,
            minHeight: theme.hitTarget.comfortable,
            gap: theme.spacing.sm,
          },
        ]}
      >
        <MaterialCommunityIcons
          name={mode === 'time' ? 'clock-outline' : 'calendar-outline'}
          size={18}
          color={theme.c.textSecondary}
        />

        <AppText variant="body" style={styles.flex}>
          {displayText}
        </AppText>

        <MaterialCommunityIcons name="chevron-down" size={20} color={theme.c.textTertiary} />
      </Pressable>

      {error ? (
        <AppText variant="caption" color="error" style={{ marginTop: 6 }}>
          {error}
        </AppText>
      ) : null}

      {Platform.OS === 'android' && androidStep !== 'idle' ? (
        <DateTimePicker
          value={androidStep === 'time' ? draft : value}
          mode={androidStep}
          display="default"
          minimumDate={androidStep === 'date' ? minimumDate : undefined}
          maximumDate={androidStep === 'date' ? maximumDate : undefined}
          onChange={handleAndroidChange}
        />
      ) : null}

      {Platform.OS === 'ios' ? (
        <Modal visible={iosOpen} transparent animationType="slide" onRequestClose={() => setIosOpen(false)}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Cancel"
            onPress={() => setIosOpen(false)}
            style={[styles.iosBackdrop, { backgroundColor: theme.c.backdrop }]}
          />

          <View
            style={{
              backgroundColor: theme.c.surfaceElevated,
              borderTopLeftRadius: theme.radius.xlarge,
              borderTopRightRadius: theme.radius.xlarge,
              padding: theme.spacing.base,
              paddingBottom: theme.spacing.xxl,
            }}
          >
            <AppText variant="heading3" align="center" style={{ marginBottom: theme.spacing.sm }}>
              {label}
            </AppText>

            <DateTimePicker
              value={draft}
              mode={mode}
              display="spinner"
              minimumDate={minimumDate}
              maximumDate={maximumDate}
              themeVariant={theme.isDark ? 'dark' : 'light'}
              onChange={(_event, picked) => {
                if (picked) setDraft(picked);
              }}
            />

            <View style={[styles.iosActions, { gap: theme.spacing.md }]}>
              <AppButton
                label="Cancel"
                variant="ghost"
                onPress={() => setIosOpen(false)}
                style={styles.flex}
              />
              <AppButton
                label="Done"
                onPress={() => {
                  onChange(draft);
                  setIosOpen(false);
                }}
                style={styles.flex}
              />
            </View>
          </View>
        </Modal>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  flex: {
    flex: 1,
  },
  iosBackdrop: {
    flex: 1,
  },
  iosActions: {
    flexDirection: 'row',
    marginTop: 8,
  },
});
