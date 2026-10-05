import { ACCOUNT_TYPES, type Account, type AccountType } from '@/types/api';

export type NetWorth = {
  /** Everything you have. */
  assets: number;
  /** Everything you owe, as a positive number. */
  liabilities: number;
  /** assets − liabilities. */
  total: number;
  currencyCode: string;
};

export type AccountGroup = {
  type: AccountType;
  label: string;
  accounts: Account[];
  /** Null when the group mixes currencies, where any single figure would be a lie. */
  subtotal: { amount: number; currencyCode: string } | null;
};

export const ACCOUNT_TYPE_LABELS: Record<AccountType, string> = {
  Cash: 'Cash',
  Bank: 'Bank accounts',
  CreditCard: 'Credit cards',
  Wallet: 'Wallets',
  Savings: 'Savings',
  Other: 'Other',
};

/**
 * Splits balances into what you have and what you owe.
 *
 * Decided by the SIGN of each balance, not by the account's type, and the
 * distinction matters in both directions: an overdrawn current account is
 * genuinely money owed even though a bank account is nominally an asset, and a
 * credit card paid past zero is genuinely money held even though a card is
 * nominally a liability. Classifying by type would state both backwards, and
 * would do it silently — the totals would still add up, they would just be
 * describing something other than the user's position.
 *
 * Returns null for a mixed-currency set: adding a USD balance to an INR one
 * produces a number that means nothing, and showing it confidently is worse
 * than showing none.
 */
export function computeNetWorth(accounts: Account[]): NetWorth | null {
  if (accounts.length === 0) return null;

  const currencies = new Set(accounts.map((account) => account.currencyCode));
  if (currencies.size !== 1) return null;

  let assets = 0;
  let liabilities = 0;

  for (const account of accounts) {
    if (account.balance >= 0) {
      assets += account.balance;
    } else {
      liabilities += -account.balance;
    }
  }

  return {
    assets,
    liabilities,
    total: assets - liabilities,
    currencyCode: accounts[0].currencyCode,
  };
}

/**
 * Groups accounts by type, in the fixed order of the AccountType enum rather
 * than by size or alphabetically — a list that reorders itself as balances move
 * is one the user has to re-read every time instead of reaching for a position
 * they remember.
 *
 * Empty groups are dropped: a "Credit cards" heading above nothing is a
 * question the screen cannot answer.
 */
export function groupAccounts(accounts: Account[]): AccountGroup[] {
  const groups: AccountGroup[] = [];

  for (const type of ACCOUNT_TYPES) {
    const members = accounts.filter((account) => account.type === type);
    if (members.length === 0) continue;

    const currencies = new Set(members.map((account) => account.currencyCode));

    groups.push({
      type,
      label: ACCOUNT_TYPE_LABELS[type],
      accounts: members,
      subtotal:
        currencies.size === 1
          ? {
              amount: members.reduce((sum, account) => sum + account.balance, 0),
              currencyCode: members[0].currencyCode,
            }
          : null,
    });
  }

  return groups;
}
