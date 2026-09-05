import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { authApi } from '@/api/auth-api';
import { profileApi } from '@/api/profile-api';
import { queryKeys } from '@/api/query-client';
import { tokenStorage } from '@/api/token-storage';
import { useAuthStore } from '@/store/auth-store';
import type {
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
} from '@/types/api';

/**
 * Authentication mutations.
 *
 * The store is updated only after the API call succeeds, so a failed login
 * never leaves the app in a half-authenticated state where the router thinks
 * there is a session and every request 401s.
 */

export function useLogin() {
  const signIn = useAuthStore((s) => s.signIn);
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: LoginRequest) => authApi.login(body),
    onSuccess: async (response) => {
      // Any cache from a previous account on this device must go before the new
      // session's queries start, or the dashboard briefly shows the old user's
      // balance.
      queryClient.clear();
      await signIn(response);
    },
  });
}

export function useRegister() {
  const signIn = useAuthStore((s) => s.signIn);
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: RegisterRequest) => authApi.register(body),
    onSuccess: async (response) => {
      queryClient.clear();
      await signIn(response);
    },
  });
}

export function useLogout() {
  const signOut = useAuthStore((s) => s.signOut);
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const refreshToken = await tokenStorage.getRefreshToken();

      if (refreshToken) {
        // Best effort. Signing out must work offline and must not leave the
        // user staring at an error because the revocation call failed — the
        // local tokens are cleared either way, and the server-side token
        // expires on its own.
        await authApi.logout(refreshToken).catch(() => undefined);
      }
    },
    onSettled: async () => {
      await signOut();
      queryClient.clear();
    },
  });
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: (body: ForgotPasswordRequest) => authApi.forgotPassword(body),
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (body: ResetPasswordRequest) => authApi.resetPassword(body),
  });
}

/**
 * Loads the profile and settings once a session exists.
 *
 * Kept as a query rather than folded into login because the app can start with
 * a restored session and no user object in memory — the router only needed the
 * refresh token to make its decision.
 */
export function useProfileBootstrap(enabled: boolean) {
  const setUser = useAuthStore((s) => s.setUser);
  const setSettings = useAuthStore((s) => s.setSettings);

  const profile = useQuery({
    queryKey: queryKeys.profile.detail(),
    queryFn: async () => {
      const user = await profileApi.get();
      setUser(user);
      return user;
    },
    enabled,
    staleTime: 5 * 60_000,
  });

  const settings = useQuery({
    queryKey: queryKeys.profile.settings(),
    queryFn: async () => {
      const value = await profileApi.getSettings();
      setSettings(value);
      return value;
    },
    enabled,
    staleTime: 5 * 60_000,
  });

  return {
    isLoading: profile.isPending || settings.isPending,
    isError: profile.isError || settings.isError,
  };
}
