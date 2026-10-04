import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  CreateTransferRequest,
  Paged,
  Transfer,
  TransferQuery,
  UpdateTransferRequest,
} from '@/types/api';

/**
 * Builds the query string, omitting anything unset — an empty `accountId=`
 * would make the server filter on nothing and return an empty page.
 */
function toParams(query: TransferQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    params[key] = value as string | number;
  }

  return params;
}

export const transferApi = {
  list: (query: TransferQuery = {}): Promise<Paged<Transfer>> =>
    unwrap(apiClient.get<ApiEnvelope<Paged<Transfer>>>('/transfers', { params: toParams(query) })),

  getById: (id: string): Promise<Transfer> =>
    unwrap(apiClient.get<ApiEnvelope<Transfer>>(`/transfers/${id}`)),

  create: (body: CreateTransferRequest): Promise<Transfer> =>
    unwrap(apiClient.post<ApiEnvelope<Transfer>>('/transfers', body)),

  update: (id: string, body: UpdateTransferRequest): Promise<Transfer> =>
    unwrap(apiClient.put<ApiEnvelope<Transfer>>(`/transfers/${id}`, body)),

  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/transfers/${id}`)),
};
