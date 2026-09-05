import { useQuery } from '@tanstack/react-query';
import { analyticsApi } from '@/api/analytics-api';
import { queryKeys } from '@/api/query-client';
import type { AnalyticsPeriod } from '@/types/api';

// Aggregation happens in SQL; this hook only fetches the compact result.
export function useAnalytics(period: AnalyticsPeriod, at?: string) {
  return useQuery({
    queryKey: queryKeys.analytics.summary(period, at),
    queryFn: () => analyticsApi.full(period, at),
    // Analytics are expensive to compute and change slowly within a session.
    staleTime: 60_000,
  });
}
