import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  Budget,
  CreateBudgetRequest,
  UpdateBudgetRequest,
} from '@/types/api';

export const budgetApi = {
  // `at` anchors which window to evaluate; the server resolves the containing
  // period in the user's own time zone.
  list: (at?: string): Promise<Budget[]> =>
    unwrap(apiClient.get<ApiEnvelope<Budget[]>>('/budgets', { params: at ? { at } : undefined })),

  getById: (id: string): Promise<Budget> =>
    unwrap(apiClient.get<ApiEnvelope<Budget>>(`/budgets/${id}`)),

  create: (body: CreateBudgetRequest): Promise<Budget> =>
    unwrap(apiClient.post<ApiEnvelope<Budget>>('/budgets', body)),

  update: (id: string, body: UpdateBudgetRequest): Promise<Budget> =>
    unwrap(apiClient.put<ApiEnvelope<Budget>>(`/budgets/${id}`, body)),

  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/budgets/${id}`)),
};
