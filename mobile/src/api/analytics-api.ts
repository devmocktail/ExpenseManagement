import { apiClient, unwrap } from './client';
import type {
  AnalyticsPeriod,
  AnalyticsResponse,
  ApiEnvelope,
  CategoryBreakdownItem,
  DashboardResponse,
  TrendPoint,
} from '@/types/api';

// Aggregation happens in SQL. The client never downloads a transaction list to
// compute a total — that would be slow, wrong on paginated data, and would put
// money arithmetic in JavaScript floats.
export const analyticsApi = {
  dashboard: (at?: string): Promise<DashboardResponse> =>
    unwrap(
      apiClient.get<ApiEnvelope<DashboardResponse>>('/dashboard', {
        params: at ? { at } : undefined,
      }),
    ),

  full: (period: AnalyticsPeriod, at?: string): Promise<AnalyticsResponse> =>
    unwrap(
      apiClient.get<ApiEnvelope<AnalyticsResponse>>('/analytics/summary', {
        params: { period, ...(at ? { at } : {}) },
      }),
    ),

  trends: (period: AnalyticsPeriod, at?: string): Promise<TrendPoint[]> =>
    unwrap(
      apiClient.get<ApiEnvelope<TrendPoint[]>>('/analytics/trends', {
        params: { period, ...(at ? { at } : {}) },
      }),
    ),

  categories: (period: AnalyticsPeriod, at?: string): Promise<CategoryBreakdownItem[]> =>
    unwrap(
      apiClient.get<ApiEnvelope<CategoryBreakdownItem[]>>('/analytics/categories', {
        params: { period, ...(at ? { at } : {}) },
      }),
    ),

  incomeVsExpense: (period: AnalyticsPeriod, at?: string): Promise<TrendPoint[]> =>
    unwrap(
      apiClient.get<ApiEnvelope<TrendPoint[]>>('/analytics/income-vs-expense', {
        params: { period, ...(at ? { at } : {}) },
      }),
    ),
};
