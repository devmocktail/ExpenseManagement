import NetInfo, { type NetInfoState } from '@react-native-community/netinfo';
import { onlineManager } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

/**
 * Network reachability.
 *
 * `isConnected` alone is not enough: a device joined to a captive-portal Wi-Fi
 * reports connected while no request can actually complete. `isInternetReachable`
 * is what distinguishes the two, and it is deliberately treated as "online"
 * while it is still null (undetermined) so the app does not flash an offline
 * banner during the first moments after launch.
 */
export function useNetworkStatus() {
  const [state, setState] = useState<{ isOffline: boolean; type: string | null }>({
    isOffline: false,
    type: null,
  });

  useEffect(() => {
    const handle = (netInfo: NetInfoState) => {
      const reachable = netInfo.isInternetReachable ?? true;
      setState({
        isOffline: !netInfo.isConnected || !reachable,
        type: netInfo.type,
      });
    };

    // Fetch once so the first render is correct rather than optimistic.
    NetInfo.fetch().then(handle).catch(() => {});

    return NetInfo.addEventListener(handle);
  }, []);

  return state;
}

/**
 * Bridges NetInfo into TanStack Query's online manager.
 *
 * Query's own detection is browser-oriented and never fires on React Native, so
 * without this, `refetchOnReconnect` would never trigger and paused mutations
 * would never resume.
 *
 * Called once at module load rather than from a hook so it is active before the
 * first query runs.
 */
onlineManager.setEventListener((setOnline) =>
  NetInfo.addEventListener((netInfo) => {
    setOnline(Boolean(netInfo.isConnected) && (netInfo.isInternetReachable ?? true));
  }),
);
