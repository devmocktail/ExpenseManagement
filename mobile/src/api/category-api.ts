import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  Category,
  CreateCategoryRequest,
  DeleteCategoryRequest,
  TransactionType,
  UpdateCategoryRequest,
} from '@/types/api';

export const categoryApi = {
  list: (type?: TransactionType): Promise<Category[]> =>
    unwrap(
      apiClient.get<ApiEnvelope<Category[]>>('/categories', {
        params: type ? { type } : undefined,
      }),
    ),

  create: (body: CreateCategoryRequest): Promise<Category> =>
    unwrap(apiClient.post<ApiEnvelope<Category>>('/categories', body)),

  update: (id: string, body: UpdateCategoryRequest): Promise<Category> =>
    unwrap(apiClient.put<ApiEnvelope<Category>>(`/categories/${id}`, body)),

  // Deleting a category that still has transactions requires an explicit
  // destination, so history is reassigned rather than orphaned.
  remove: (id: string, body: DeleteCategoryRequest = {}): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/categories/${id}`, { data: body })),
};
