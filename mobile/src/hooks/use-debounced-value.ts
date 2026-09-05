import { useEffect, useState } from 'react';

/**
 * Delays propagating a rapidly-changing value.
 *
 * Used for the transaction search box: without it, every keystroke is a network
 * request, and the responses arrive out of order so the list flickers between
 * results for "gro", "groc" and "groce".
 */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);

    // Clearing on every change is what makes this a debounce rather than a
    // throttle — the timer only fires once the value has settled.
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
