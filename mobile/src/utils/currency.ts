/**
 * Currency and number formatting.
 *
 * Every amount rendered anywhere in the app goes through this module. No
 * component contains a currency symbol, and no component decides how many
 * decimals to show (Rule 12) — so adding a currency is a data change here,
 * not a sweep through the UI.
 */

export type CurrencyCode = string;

type CurrencyMeta = {
  code: CurrencyCode;
  symbol: string;
  name: string;
  /** BCP 47 locale whose grouping rules suit this currency. */
  locale: string;
  /** Minor units. JPY has none; most have two. */
  decimals: number;
};

/**
 * Currencies offered in settings. The list is small and explicit rather than
 * pulled from Intl, because a picker of 300 ISO codes is worse UX than eight
 * relevant ones — and adding one is a single line.
 */
export const SUPPORTED_CURRENCIES: readonly CurrencyMeta[] = [
  { code: 'INR', symbol: '₹', name: 'Indian Rupee', locale: 'en-IN', decimals: 2 },
  { code: 'USD', symbol: '$', name: 'US Dollar', locale: 'en-US', decimals: 2 },
  { code: 'EUR', symbol: '€', name: 'Euro', locale: 'de-DE', decimals: 2 },
  { code: 'GBP', symbol: '£', name: 'British Pound', locale: 'en-GB', decimals: 2 },
  { code: 'AUD', symbol: 'A$', name: 'Australian Dollar', locale: 'en-AU', decimals: 2 },
  { code: 'CAD', symbol: 'C$', name: 'Canadian Dollar', locale: 'en-CA', decimals: 2 },
  { code: 'SGD', symbol: 'S$', name: 'Singapore Dollar', locale: 'en-SG', decimals: 2 },
  { code: 'AED', symbol: 'د.إ', name: 'UAE Dirham', locale: 'ar-AE', decimals: 2 },
  { code: 'JPY', symbol: '¥', name: 'Japanese Yen', locale: 'ja-JP', decimals: 0 },
] as const;

const CURRENCY_BY_CODE = new Map(SUPPORTED_CURRENCIES.map((c) => [c.code, c]));

const FALLBACK: CurrencyMeta = SUPPORTED_CURRENCIES[0];

export function getCurrencyMeta(code: CurrencyCode | undefined | null): CurrencyMeta {
  if (!code) return FALLBACK;
  return CURRENCY_BY_CODE.get(code.toUpperCase()) ?? {
    // An unknown code still renders sensibly rather than throwing: show the code
    // itself as the symbol so the number is never ambiguous.
    code: code.toUpperCase(),
    symbol: code.toUpperCase(),
    name: code.toUpperCase(),
    locale: 'en-US',
    decimals: 2,
  };
}

export function getCurrencySymbol(code: CurrencyCode | undefined | null): string {
  return getCurrencyMeta(code).symbol;
}

export type FormatCurrencyOptions = {
  /** Drop the decimal part. Used for large summary figures where cents are noise. */
  compactDecimals?: boolean;
  /** Render magnitude only, no symbol. */
  hideSymbol?: boolean;
  /** Force a leading + on positive values (used for income rows). */
  showSign?: boolean;
  /**
   * Abbreviate large numbers (1.2L, 12.5K, 3.4M) using the convention that
   * matches the currency's locale — Indian numbering uses lakh/crore, which is
   * what an INR user expects to read, not "120K".
   */
  abbreviate?: boolean;
};

/**
 * Formats a number as currency.
 *
 * Intl.NumberFormat is used where available (Hermes ships full ICU on both
 * platforms in SDK 54+), with a manual grouping fallback so a runtime without
 * ICU degrades to a still-correct number rather than a crash.
 */
export function formatCurrency(
  amount: number,
  code: CurrencyCode | undefined | null,
  options: FormatCurrencyOptions = {},
): string {
  const meta = getCurrencyMeta(code);
  const { compactDecimals = false, hideSymbol = false, showSign = false, abbreviate = false } = options;

  const safeAmount = Number.isFinite(amount) ? amount : 0;
  const magnitude = Math.abs(safeAmount);

  if (abbreviate) {
    const abbreviated = abbreviateNumber(magnitude, meta.locale);
    if (abbreviated) {
      return applySign(safeAmount, hideSymbol ? abbreviated : `${meta.symbol}${abbreviated}`, showSign);
    }
  }

  const decimals = compactDecimals ? 0 : meta.decimals;
  const body = formatNumber(magnitude, meta.locale, decimals);

  return applySign(safeAmount, hideSymbol ? body : `${meta.symbol}${body}`, showSign);
}

function applySign(amount: number, body: string, showSign: boolean): string {
  if (amount < 0) return `-${body}`;
  if (showSign && amount > 0) return `+${body}`;
  return body;
}

function formatNumber(value: number, locale: string, decimals: number): string {
  try {
    return new Intl.NumberFormat(locale, {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    }).format(value);
  } catch {
    return fallbackGrouping(value, decimals, locale);
  }
}

/**
 * Grouping without Intl. Handles the Indian 2,2,3 pattern explicitly because
 * naive 3-digit grouping renders ₹1,00,000 as ₹100,000, which an Indian user
 * reads as a different number at a glance.
 */
function fallbackGrouping(value: number, decimals: number, locale: string): string {
  const fixed = value.toFixed(decimals);
  const [whole, fraction] = fixed.split('.');

  const grouped = locale.endsWith('-IN')
    ? whole.replace(/(\d)(?=(\d\d)+\d$)/g, '$1,')
    : whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');

  return fraction ? `${grouped}.${fraction}` : grouped;
}

/** Returns null when the value is small enough that abbreviating would not help. */
function abbreviateNumber(value: number, locale: string): string | null {
  if (locale.endsWith('-IN')) {
    if (value >= 1_00_00_000) return `${trimZeros(value / 1_00_00_000)}Cr`;
    if (value >= 1_00_000) return `${trimZeros(value / 1_00_000)}L`;
    if (value >= 1_000) return `${trimZeros(value / 1_000)}K`;
    return null;
  }

  if (value >= 1_000_000_000) return `${trimZeros(value / 1_000_000_000)}B`;
  if (value >= 1_000_000) return `${trimZeros(value / 1_000_000)}M`;
  if (value >= 1_000) return `${trimZeros(value / 1_000)}K`;
  return null;
}

function trimZeros(value: number): string {
  return value.toFixed(1).replace(/\.0$/, '');
}

/**
 * Formats a ledger amount with an explicit direction sign.
 *
 * Colour alone must never carry the income/expense distinction (accessibility
 * requirement), so the sign is part of the string itself and is present for
 * screen readers and for users who cannot distinguish the two colours.
 */
export function formatSignedAmount(
  amount: number,
  type: 'Income' | 'Expense',
  code: CurrencyCode | undefined | null,
  options: Omit<FormatCurrencyOptions, 'showSign'> = {},
): string {
  const magnitude = Math.abs(amount);
  const body = formatCurrency(magnitude, code, options);
  return type === 'Income' ? `+${body}` : `-${body}`;
}

/**
 * Parses user keypad input into a number.
 *
 * Accepts a bare decimal string; strips grouping separators and any currency
 * symbol the user may have pasted. Returns null for anything that is not a
 * finite, non-negative amount so callers can show a validation message rather
 * than storing NaN.
 */
export function parseAmountInput(input: string): number | null {
  if (!input) return null;

  const cleaned = input.replace(/[^\d.,-]/g, '').replace(/,/g, '');
  if (!cleaned || cleaned === '.' || cleaned === '-') return null;

  const value = Number(cleaned);
  if (!Number.isFinite(value) || value < 0) return null;

  // Guard against precision loss: beyond this, a float can no longer represent
  // every 2-decimal value exactly, and the server would reject it anyway.
  if (value > 999_999_999_999) return null;

  return roundToMinorUnits(value);
}

/** Rounds to 2 decimals, away from zero, matching the server's Money.Round. */
export function roundToMinorUnits(value: number, decimals = 2): number {
  const factor = 10 ** decimals;
  // The epsilon nudge stops binary-float representation from turning
  // 1.005 into 1.00 — the classic JS rounding surprise on money.
  return Math.round((value + Number.EPSILON) * factor) / factor;
}

/** Percentage helper matching the backend: a zero or negative total yields 0, never NaN. */
export function percentageOf(part: number, total: number): number {
  if (!total || total <= 0) return 0;
  return roundToMinorUnits((part / total) * 100);
}
