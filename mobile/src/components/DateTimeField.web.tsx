import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { StyleSheet, View } from 'react-native';
import { AppText } from '@/components/ui/AppText';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { DateTimeFieldProps } from './DateTimeField';

/**
 * Web implementation of <see cref="DateTimeField"/>.
 *
 * `@react-native-community/datetimepicker` is native-only — it has no web
 * build at all, so importing it in a browser bundle throws at module load and
 * takes the whole app down, not just this field. Metro resolves `.web.tsx`
 * ahead of `.tsx`, so this file replaces the native one on web and that import
 * never happens.
 *
 * It leans on the browser's own `<input type="date">` / `datetime-local`, which
 * is a better control than anything we would reimplement: it is localised,
 * keyboard accessible and screen-reader aware for free.
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

  const inputType = mode === 'time' ? 'time' : mode === 'datetime' ? 'datetime-local' : 'date';

  return (
    <View>
      <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: 6 }}>
        {label}
      </AppText>

      <View
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

        {/* A raw DOM input rather than a react-native-web TextInput: only the
            real element gives the browser's native picker. This type-checks
            because the project pulls in React's DOM JSX types alongside React
            Native's. */}
        <input
          type={inputType}
          aria-label={label}
          value={toInputValue(value, inputType)}
          min={minimumDate ? toInputValue(minimumDate, inputType) : undefined}
          max={maximumDate ? toInputValue(maximumDate, inputType) : undefined}
          onChange={(event: { target: { value: string } }) => {
            const parsed = new Date(event.target.value);
            // An empty or half-typed value parses to Invalid Date; ignoring it
            // keeps the last good value rather than propagating NaN into the form.
            if (!Number.isNaN(parsed.getTime())) onChange(parsed);
          }}
          style={{
            flex: 1,
            border: 'none',
            outline: 'none',
            background: 'transparent',
            color: theme.c.textPrimary,
            fontSize: theme.typography.body.fontSize,
            fontFamily: 'inherit',
            padding: `${theme.spacing.md}px 0`,
          }}
        />
      </View>

      {error ? (
        <AppText variant="caption" color="error" style={{ marginTop: 6 }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}

/**
 * Formats a Date for an `<input>`'s `value`.
 *
 * The element expects LOCAL wall-clock time with no zone, so `toISOString()`
 * is wrong here — it would shift the displayed date by the UTC offset and show
 * yesterday to anyone west of Greenwich.
 */
function toInputValue(date: Date, inputType: string): string {
  const pad = (n: number) => String(n).padStart(2, '0');

  const yyyy = date.getFullYear();
  const mm = pad(date.getMonth() + 1);
  const dd = pad(date.getDate());
  const hh = pad(date.getHours());
  const mi = pad(date.getMinutes());

  if (inputType === 'time') return `${hh}:${mi}`;
  if (inputType === 'datetime-local') return `${yyyy}-${mm}-${dd}T${hh}:${mi}`;
  return `${yyyy}-${mm}-${dd}`;
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
  },
});
