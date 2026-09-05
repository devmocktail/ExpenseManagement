import { apiClient, unwrap } from './client';
import type {
  ApiEnvelope,
  AppNotification,
  ExportRequest,
  RegisterDeviceRequest,
  UpdateProfileRequest,
  UpdateSettingsRequest,
  UserProfile,
  UserSettings,
} from '@/types/api';

export const profileApi = {
  get: (): Promise<UserProfile> => unwrap(apiClient.get<ApiEnvelope<UserProfile>>('/profile')),

  update: (body: UpdateProfileRequest): Promise<UserProfile> =>
    unwrap(apiClient.put<ApiEnvelope<UserProfile>>('/profile', body)),

  getSettings: (): Promise<UserSettings> =>
    unwrap(apiClient.get<ApiEnvelope<UserSettings>>('/profile/settings')),

  updateSettings: (body: UpdateSettingsRequest): Promise<UserSettings> =>
    unwrap(apiClient.put<ApiEnvelope<UserSettings>>('/profile/settings', body)),

  // Irreversible. The UI gates this behind a typed confirmation.
  deleteAccount: (password: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>('/profile', { data: { password } })),

  // Returns raw text/CSV rather than the JSON envelope, so it bypasses unwrap().
  exportTransactions: async (body: ExportRequest): Promise<string> => {
    const response = await apiClient.get<string>('/profile/export', {
      params: body,
      responseType: 'text',
      headers: { Accept: body.format === 'csv' ? 'text/csv' : 'application/json' },
    });
    return response.data;
  },
};

export const notificationApi = {
  list: (): Promise<AppNotification[]> =>
    unwrap(apiClient.get<ApiEnvelope<AppNotification[]>>('/notifications')),

  markRead: (id: string): Promise<void> =>
    unwrap(apiClient.put<ApiEnvelope<void>>(`/notifications/${id}/read`, {})),

  markAllRead: (): Promise<void> =>
    unwrap(apiClient.put<ApiEnvelope<void>>('/notifications/read-all', {})),

  registerDevice: (body: RegisterDeviceRequest): Promise<void> =>
    unwrap(apiClient.post<ApiEnvelope<void>>('/notifications/devices', body)),

  unregisterDevice: (token: string): Promise<void> =>
    unwrap(apiClient.delete<ApiEnvelope<void>>('/notifications/devices', { data: { token } })),
};
