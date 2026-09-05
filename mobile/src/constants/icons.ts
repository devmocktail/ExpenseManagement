import type { ComponentProps } from 'react';
import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';

export type IconName = ComponentProps<typeof MaterialCommunityIcons>['name'];

/**
 * Maps the icon slugs the server stores on a category to real glyph names.
 *
 * The server persists a stable slug ("food", "transport") rather than a glyph
 * name so the icon set can be swapped, or a second set added for another
 * platform, without a data migration. Anything unrecognised falls back rather
 * than rendering an empty box.
 */
export const CATEGORY_ICONS: Record<string, IconName> = {
  // Expense
  food: 'silverware-fork-knife',
  groceries: 'cart-outline',
  transport: 'bus',
  fuel: 'gas-station',
  shopping: 'shopping-outline',
  'shopping-bag': 'shopping-outline',
  bills: 'file-document-outline',
  rent: 'home-city-outline',
  utilities: 'flash-outline',
  entertainment: 'movie-open-outline',
  healthcare: 'heart-pulse',
  health: 'heart-pulse',
  education: 'school-outline',
  travel: 'airplane',
  subscriptions: 'refresh-circle',
  'personal-care': 'face-woman-shimmer-outline',
  personal: 'face-woman-shimmer-outline',
  pets: 'paw-outline',
  gifts: 'gift-outline',
  insurance: 'shield-check-outline',
  investment: 'chart-line',
  charity: 'hand-heart-outline',

  // Income
  salary: 'cash-multiple',
  freelance: 'laptop',
  business: 'briefcase-outline',
  gift: 'gift-outline',
  refund: 'cash-refund',
  interest: 'bank-outline',

  // Fallback
  category: 'shape-outline',
  other: 'dots-horizontal-circle-outline',
};

export function resolveCategoryIcon(slug: string | null | undefined): IconName {
  if (!slug) return CATEGORY_ICONS.category;
  return CATEGORY_ICONS[slug.toLowerCase()] ?? CATEGORY_ICONS.category;
}

/** Icons offered when a user creates or edits their own category. */
export const SELECTABLE_CATEGORY_ICONS: readonly string[] = [
  'food',
  'groceries',
  'transport',
  'fuel',
  'shopping',
  'bills',
  'rent',
  'utilities',
  'entertainment',
  'healthcare',
  'education',
  'travel',
  'subscriptions',
  'personal-care',
  'pets',
  'gifts',
  'insurance',
  'investment',
  'charity',
  'salary',
  'freelance',
  'business',
  'refund',
  'interest',
  'category',
  'other',
];

/** Palette offered in the category colour picker. Mirrors the chart palette. */
export const SELECTABLE_CATEGORY_COLORS: readonly string[] = [
  '#6366F1',
  '#8B5CF6',
  '#A855F7',
  '#EC4899',
  '#EF4444',
  '#F97316',
  '#F59E0B',
  '#84CC16',
  '#22C55E',
  '#14B8A6',
  '#06B6D4',
  '#0EA5E9',
  '#3B82F6',
  '#64748B',
];

/** Human labels for the payment-method enum, kept out of the screens. */
export const PAYMENT_METHOD_LABELS: Record<string, string> = {
  Cash: 'Cash',
  CreditCard: 'Credit card',
  DebitCard: 'Debit card',
  Upi: 'UPI',
  BankTransfer: 'Bank transfer',
  Wallet: 'Wallet',
  Other: 'Other',
};

export const PAYMENT_METHOD_ICONS: Record<string, IconName> = {
  Cash: 'cash',
  CreditCard: 'credit-card-outline',
  DebitCard: 'credit-card-chip-outline',
  Upi: 'cellphone-nfc',
  BankTransfer: 'bank-outline',
  Wallet: 'wallet-outline',
  Other: 'dots-horizontal',
};
