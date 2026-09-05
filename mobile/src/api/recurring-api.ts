import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  CreateRecurringRequest,
  RecurringTransaction,
  UpdateRecurringRequest,
} from '@/types/api';

export const recurringApi = {
  list: (): Promise<RecurringTransaction[]> =>
    unwrap(apiClient.get<ApiEnvelope<RecurringTransaction[]>>('/recurring')),

  getById: (id: string): Promise<RecurringTransaction> =>
    unwrap(apiClient.get<ApiEnvelope<RecurringTransaction>>(`/recurring/${id}`)),

  create: (body: CreateRecurringRequest): Promise<RecurringTransaction> =>
    unwrap(apiClient.post<ApiEnvelope<RecurringTransaction>>('/recurring', body)),

  update: (id: string, body: UpdateRecurringRequest): Promise<RecurringTransaction> =>
    unwrap(apiClient.put<ApiEnvelope<RecurringTransaction>>(`/recurring/${id}`, body)),

  // Pause keeps the schedule and its history; it is not a soft delete.
  setPaused: (id: string, isPaused: boolean): Promise<RecurringTransaction> =>
    unwrap(
      apiClient.patch<ApiEnvelope<RecurringTransaction>>(`/recurring/${id}/paused`, { isPaused }),
    ),

  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/recurring/${id}`)),
};
