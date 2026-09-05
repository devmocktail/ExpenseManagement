import { QueryClient, type DefaultOptions } from '@tanstack/react-query';
import { ApiError } from './client';

/**
 * TanStack Query configuration.
 *
 * The defaults here encode two product decisions:
 *
 *  1. Financial data must not look stale. `staleTime` is short and every screen
 *    refetches when it regains focus, because a balance that silently lags
 *    behind a transaction the user just added destroys trust in the number.
 *
 *  2. Retrying a request that failed for a *reason* is pointless and harmful —
 *    it delays the error the user needs to see, and on a 401 it would race the
 *    token refresh. Only genuinely transient failures are retried.
 */

function shouldRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= 2) return false;

  if (error instanceof ApiError) {
    // 4xx means the request itself is wrong; sending it again cannot help.
    // 401 in particular is already handled by the axios interceptor's refresh.
    return error.isRetryable;
  }

  return true;
}

const defaultOptions: DefaultOptions = {
  queries: {
    retry: shouldRetry,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),

    // 30 seconds is long enough that navigating between tabs does not refetch
    // constantly, short enough that a stale balance is never on screen for long.
    staleTime: 30_000,

    // Keep data in cache for five minutes after the last observer unmounts, so
    // going back to a screen shows content instantly while it revalidates.
    gcTime: 5 * 60_000,

    refetchOnWindowFocus: true,
    refetchOnReconnect: true,

    // Refetching on mount when data is fresh causes a request storm on a tab
    // switch. `staleTime` already governs when a refetch is warranted.
    refetchOnMount: true,
  },
  mutations: {
    // A mutation that failed may have partially applied server-side. Retrying
    // automatically risks a duplicate transaction, so the user decides.
    retry: false,
  },
};

export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions });
}

/**
 * Query keys, centralised.
 *
 * Invalidation is the part of this app that quietly rots: adding a transaction
 * has to refresh the list, the dashboard, the budgets and the analytics. Keeping
 * every key in one hierarchy means an invalidation can target a whole branch
 * (`queryKeys.transactions.all`) instead of enumerating leaves that a later
 * feature will forget to add itself to.
 */
export const queryKeys = {
  profile: {
    all: ['profile'] as const,
    detail: () => [...queryKeys.profile.all, 'detail'] as const,
    settings: () => [...queryKeys.profile.all, 'settings'] as const,
  },
  categories: {
    all: ['categories'] as const,
    list: (type?: string) => [...queryKeys.categories.all, 'list', type ?? 'all'] as const,
  },
  transactions: {
    all: ['transactions'] as const,
    list: (filters: Record<string, unknown>) =>
      [...queryKeys.transactions.all, 'list', filters] as const,
    detail: (id: string) => [...queryKeys.transactions.all, 'detail', id] as const,
  },
  budgets: {
    all: ['budgets'] as const,
    list: (at?: string) => [...queryKeys.budgets.all, 'list', at ?? 'current'] as const,
    detail: (id: string) => [...queryKeys.budgets.all, 'detail', id] as const,
  },
  dashboard: {
    all: ['dashboard'] as const,
    detail: (at?: string) => [...queryKeys.dashboard.all, at ?? 'current'] as const,
  },
  analytics: {
    all: ['analytics'] as const,
    summary: (period: string, at?: string) =>
      [...queryKeys.analytics.all, 'summary', period, at ?? 'current'] as const,
  },
  recurring: {
    all: ['recurring'] as const,
    list: () => [...queryKeys.recurring.all, 'list'] as const,
    detail: (id: string) => [...queryKeys.recurring.all, 'detail', id] as const,
  },
  notifications: {
    all: ['notifications'] as const,
    list: () => [...queryKeys.notifications.all, 'list'] as const,
  },
} as const;

/**
 * Everything a write to a transaction invalidates.
 *
 * Exported as one list so a new derived view only has to be added here, rather
 * than in every mutation that could affect it.
 */
export const TRANSACTION_DEPENDENT_KEYS = [
  queryKeys.transactions.all,
  queryKeys.dashboard.all,
  queryKeys.budgets.all,
  queryKeys.analytics.all,
  queryKeys.categories.all,
] as const;
