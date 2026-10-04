import { apiClient, unwrap } from './client';
import type {
  Account,
  ApiEnvelope,
  CreateAccountRequest,
  UpdateAccountRequest,
} from '@/types/api';

export const accountApi = {
  /**
   * Archived accounts are excluded by default: they exist to keep history
   * intact, not to clutter a picker. The management screen asks for them.
   */
  list: (includeArchived = false): Promise<Account[]> =>
    unwrap(
      apiClient.get<ApiEnvelope<Account[]>>('/accounts', {
        params: includeArchived ? { includeArchived: true } : undefined,
      }),
    ),

  getById: (id: string): Promise<Account> =>
    unwrap(apiClient.get<ApiEnvelope<Account>>(`/accounts/${id}`)),

  create: (body: CreateAccountRequest): Promise<Account> =>
    unwrap(apiClient.post<ApiEnvelope<Account>>('/accounts', body)),

  update: (id: string, body: UpdateAccountRequest): Promise<Account> =>
    unwrap(apiClient.put<ApiEnvelope<Account>>(`/accounts/${id}`, body)),

  /**
   * Only succeeds for an account nothing references. One with history answers
   * 422 — archive it instead, which hides it while keeping what it paid for.
   */
  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/accounts/${id}`)),

  setDefault: (id: string): Promise<Account> =>
    unwrap(apiClient.post<ApiEnvelope<Account>>(`/accounts/${id}/default`)),
};
