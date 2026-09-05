import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  CreateTransactionRequest,
  Paged,
  Transaction,
  TransactionQuery,
  UpdateTransactionRequest,
} from '@/types/api';

/**
 * Builds the query string, omitting anything unset.
 *
 * Sending `?search=` or `?categoryId=undefined` makes the server's filter
 * predicates fire on empty values and quietly return nothing, so absent
 * parameters must actually be absent.
 */
function toParams(query: TransactionQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    params[key] = value as string | number;
  }

  return params;
}

export const transactionApi = {
  list: (query: TransactionQuery = {}): Promise<Paged<Transaction>> =>
    unwrap(
      apiClient.get<ApiEnvelope<Paged<Transaction>>>('/transactions', { params: toParams(query) }),
    ),

  getById: (id: string): Promise<Transaction> =>
    unwrap(apiClient.get<ApiEnvelope<Transaction>>(`/transactions/${id}`)),

  create: (body: CreateTransactionRequest): Promise<Transaction> =>
    unwrap(apiClient.post<ApiEnvelope<Transaction>>('/transactions', body)),

  update: (id: string, body: UpdateTransactionRequest): Promise<Transaction> =>
    unwrap(apiClient.put<ApiEnvelope<Transaction>>(`/transactions/${id}`, body)),

  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/transactions/${id}`)),
};
