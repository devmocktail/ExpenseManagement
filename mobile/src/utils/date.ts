import {
  differenceInCalendarDays,
  format,
  isThisYear,
  isToday,
  isValid,
  isYesterday,
  parseISO,
} from 'date-fns';

/**
 * Date formatting for display.
 *
 * The server stores and sends UTC; JavaScript's Date converts to the device's
 * local zone on parse, which is what the user expects to see. The one place
 * that is NOT good enough is period boundaries ("what counts as September"),
 * and those are deliberately computed on the server in the user's configured
 * time zone rather than guessed here from the device.
 */

/** Parses a server ISO string, returning null rather than an Invalid Date. */
export function parseServerDate(value: string | null | undefined): Date | null {
  if (!value) return null;

  const parsed = parseISO(value);
  return isValid(parsed) ? parsed : null;
}

/** Serialises a local Date for the API. Always UTC with a Z suffix. */
export function toServerDate(date: Date): string {
  return date.toISOString();
}

/** `Sep 1, 2026`, dropping the year when it is the current one. */
export function formatDate(value: string | Date | null | undefined): string {
  const date = typeof value === 'string' ? parseServerDate(value) : (value ?? null);
  if (!date) return '';

  return isThisYear(date) ? format(date, 'MMM d') : format(date, 'MMM d, yyyy');
}

export function formatFullDate(value: string | Date | null | undefined): string {
  const date = typeof value === 'string' ? parseServerDate(value) : (value ?? null);
  return date ? format(date, 'EEEE, d MMMM yyyy') : '';
}

export function formatTimeOfDay(value: string | Date | null | undefined): string {
  const date = typeof value === 'string' ? parseServerDate(value) : (value ?? null);
  return date ? format(date, 'h:mm a') : '';
}

export function formatDateTime(value: string | Date | null | undefined): string {
  const date = typeof value === 'string' ? parseServerDate(value) : (value ?? null);
  if (!date) return '';

  return `${formatDate(date)} · ${format(date, 'h:mm a')}`;
}

/**
 * The heading above a group of transactions: `TODAY`, `YESTERDAY`, or a date.
 *
 * Relative labels are limited to two days. Beyond that "3 days ago" is harder to
 * place than the actual date, and a ledger is something people scan by date.
 */
export function formatRelativeDayHeading(value: string | Date): string {
  const date = typeof value === 'string' ? parseServerDate(value) : value;
  if (!date) return '';

  if (isToday(date)) return 'Today';
  if (isYesterday(date)) return 'Yesterday';

  return isThisYear(date) ? format(date, 'EEEE, MMM d') : format(date, 'MMM d, yyyy');
}

/** Stable key for grouping by calendar day in the device's local zone. */
export function toDayKey(value: string | Date): string {
  const date = typeof value === 'string' ? parseServerDate(value) : value;
  return date ? format(date, 'yyyy-MM-dd') : '';
}

/** `September 2026`. Used for the dashboard period selector. */
export function formatMonthYear(value: string | Date): string {
  const date = typeof value === 'string' ? parseServerDate(value) : value;
  return date ? format(date, 'MMMM yyyy') : '';
}

/**
 * `4 days left` / `Ends today` / `Ended 2 days ago`, for budget windows.
 * Counts calendar days, not 24-hour spans, so a budget ending tomorrow morning
 * still reads as "1 day left" rather than "0".
 */
export function formatDaysRemaining(endDate: string | Date): string {
  const end = typeof endDate === 'string' ? parseServerDate(endDate) : endDate;
  if (!end) return '';

  const days = differenceInCalendarDays(end, new Date());

  if (days < 0) return `Ended ${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'} ago`;
  if (days === 0) return 'Ends today';
  if (days === 1) return '1 day left';

  return `${days} days left`;
}

/** Start of today in local time — the default upper bound for a date picker. */
export function startOfToday(): Date {
  const now = new Date();
  now.setHours(0, 0, 0, 0);
  return now;
}
