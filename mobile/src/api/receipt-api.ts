import { apiClient, unwrap } from './client';
import type { ApiEnvelope, ReceiptSummary } from '@/types/api';

/**
 * Receipt upload and retrieval.
 *
 * Images are sent as multipart/form-data. React Native's FormData takes a
 * `{ uri, name, type }` object rather than a Blob — passing a Blob works on web
 * and silently uploads a zero-byte file on device.
 */
export const receiptApi = {
  upload: async (params: {
    transactionId: string;
    uri: string;
    fileName: string;
    mimeType: string;
    onProgress?: (percent: number) => void;
  }): Promise<ReceiptSummary> => {
    const form = new FormData();

    form.append('transactionId', params.transactionId);
    form.append('file', {
      uri: params.uri,
      name: params.fileName,
      type: params.mimeType,
    } as unknown as Blob);

    return unwrap(
      apiClient.post<ApiEnvelope<ReceiptSummary>>('/receipts', form, {
        // Let the platform set the multipart boundary; a hand-written
        // Content-Type omits it and the server cannot parse the body.
        headers: { 'Content-Type': 'multipart/form-data' },
        // Uploads over a slow mobile connection legitimately outlast the
        // default request timeout.
        timeout: 60_000,
        onUploadProgress: (event) => {
          if (!params.onProgress || !event.total) return;
          params.onProgress(Math.round((event.loaded / event.total) * 100));
        },
      }),
    );
  },

  getById: (id: string): Promise<ReceiptSummary> =>
    unwrap(apiClient.get<ApiEnvelope<ReceiptSummary>>(`/receipts/${id}`)),

  remove: (id: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>(`/receipts/${id}`)),
};
