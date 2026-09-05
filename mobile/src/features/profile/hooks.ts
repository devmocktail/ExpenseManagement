import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { profileApi } from '@/api/profile-api';
import { queryKeys } from '@/api/query-client';
import { useAuthStore } from '@/store/auth-store';
import type { UpdateProfileRequest, UpdateSettingsRequest } from '@/types/api';

export function useProfile() {
  const setUser = useAuthStore((s) => s.setUser);

  return useQuery({
    queryKey: queryKeys.profile.detail(),
    queryFn: async () => {
      const user = await profileApi.get();
      setUser(user);
      return user;
    },
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const setUser = useAuthStore((s) => s.setUser);

  return useMutation({
    mutationFn: (body: UpdateProfileRequest) => profileApi.update(body),
    onSuccess: (user) => {
      setUser(user);
      queryClient.setQueryData(queryKeys.profile.detail(), user);
    },
  });
}

export function useSettings() {
  const setSettings = useAuthStore((s) => s.setSettings);

  return useQuery({
    queryKey: queryKeys.profile.settings(),
    queryFn: async () => {
      const settings = await profileApi.getSettings();
      setSettings(settings);
      return settings;
    },
  });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();
  const setSettings = useAuthStore((s) => s.setSettings);

  return useMutation({
    mutationFn: (body: UpdateSettingsRequest) => profileApi.updateSettings(body),
    onSuccess: async (settings) => {
      setSettings(settings);
      queryClient.setQueryData(queryKeys.profile.settings(), settings);

      // Currency and the month-start day change how every amount and period is
      // rendered, so everything derived from them has to be recomputed.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.analytics.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.budgets.all }),
      ]);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (body: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
      import('@/api/auth-api').then((m) => m.authApi.changePassword(body)),
  });
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (password: string) => profileApi.deleteAccount(password),
  });
}
