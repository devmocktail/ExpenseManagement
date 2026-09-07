import { useEffect, useState } from 'react';
import { tokenStorage } from '@/api/token-storage';
import { config } from '@/constants/config';

export type AuthenticatedImageSource = {
  uri: string;
  headers?: Record<string, string>;
};

/**
 * Builds an image source for a receipt.
 *
 * Receipts are served from an authorizing endpoint, not a public URL — that is
 * what stops one user reading another's receipt by guessing an id. A plain
 * <Image source={{ uri }} /> sends no Authorization header and would just 401,
 * so the token has to be attached explicitly.
 *
 * expo-image supports request headers natively, which is why it is used here in
 * preference to React Native's own Image: it also gives disk caching keyed on
 * the URL, so a receipt is fetched once rather than on every render.
 */
export function useAuthenticatedImageSource(path: string): AuthenticatedImageSource | undefined {
  const [source, setSource] = useState<AuthenticatedImageSource>();

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const token = await tokenStorage.getAccessToken();
      if (cancelled) return;

      // The server returns a root-relative API path; make it absolute without
      // assuming a particular host (Rule 11).
      const uri = path.startsWith('http') ? path : `${config.api.baseUrl}${path}`;

      setSource({
        uri,
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      });
    })();

    return () => {
      cancelled = true;
    };
  }, [path]);

  return source;
}
