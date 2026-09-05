import { useQuery } from '@tanstack/react-query';
import { analyticsApi } from '@/api/analytics-api';
import { queryKeys } from '@/api/query-client';

// One request, not ten. The server assembles the whole home screen so the app
// makes a single round trip on the most-visited screen in the product.
export function useDashboard(at?: string) {
  return useQuery({
    queryKey: queryKeys.dashboard.detail(at),
    queryFn: () => analyticsApi.dashboard(at),
  });
}
