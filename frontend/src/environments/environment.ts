export const environment = {
  production: false,
  /** Dev server proxies `/api` → SkillRegistry.API (see `proxy.conf.json`). */
  apiBaseUrl: '',
  /** Shown in UI for CLI install hints (your API origin, not the SPA port). */
  cliRegistryUrl: 'http://localhost:5289',
};
