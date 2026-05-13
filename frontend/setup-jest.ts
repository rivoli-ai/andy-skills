import { webcrypto } from 'node:crypto';

/** jsdom under Jest 29 does not implement randomUUID; browsers and Jest 30 do. */
if (typeof globalThis.crypto?.randomUUID !== 'function') {
  Object.defineProperty(globalThis, 'crypto', {
    value: webcrypto,
    configurable: true,
    writable: true,
  });
}

import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';

setupZoneTestEnv();
