# Skills Management Application — Project Rules

Persistent guidance for implementing an **enterprise skill registry** (publish, version, discover, govern agent skills) using **.NET + Angular**, aligned with [SkillHub](https://github.com/iflytek/skillhub) capabilities and the **same structural template as DevPilot** (`dev/andy-devpilot` on this machine).

---

## 1. Template source (non-negotiable layout)

Mirror **DevPilot’s** repository shape unless a deliberate architectural ADR says otherwise:

| Area | Pattern |
|------|---------|
| **Backend** | Clean Architecture solution under `backend/`: `Domain`, `Application`, `Infrastructure`, `API`; optional `tests/` (e.g. unit tests project). |
| **Frontend** | Angular SPA under `frontend/` with `src/app/core`, `features`, `shared`, `layout`. |
| **Infra** | `docker-compose.yml` at repo root (PostgreSQL, API, frontend/nginx proxy pattern); additional `infra/` only when needed (identity, scanners, etc.). |
| **Docs** | Operational and architecture notes under `docs/`. |

Reference implementation paths on this machine: `~/dev/andy-devpilot/README.md`, `backend/DevPilot.sln`, `frontend/` Angular app.

---

## 2. SkillHub parity — product behaviors to preserve

Implement iteratively; prioritize **registry core + governance** before optional social/extras.

- **Namespaces / teams**: Scoped ownership; members with roles (e.g. Owner / Admin / Member); publishing policies per namespace.
- **Packages & versions**: Semantic versioning, tags (`beta`, `stable`), deterministic `latest` resolution rules.
- **Discovery**: Search with filters (namespace, recency, downloads if tracked); **visibility** enforced so users only see authorized skills.
- **Review & promotion**: Namespace-level review; optional platform-global promotion; **audit log** for governance actions.
- **AuthN/Z**: OAuth/OIDC for humans; **scoped API tokens** (prefix-hashed storage) for CLI/automation.
- **Storage**: Pluggable backend — local/filesystem for dev; **S3-compatible** (MinIO, etc.) for production via configuration.
- **API contract**: Versioned REST API; publish **OpenAPI**; keep frontend types in sync (regenerate client/types when the contract changes — same discipline as SkillHub’s “generate API” workflow).

CLI compatibility with existing registries (e.g. OpenClaw-style clients) is **optional**; design REST contracts so a compat layer can be added without rewriting core domains.

---

## 3. Backend (.NET) rules

- **Dependencies point inward**: `API` → `Application` + infrastructure wiring; `Application` → `Domain`; `Infrastructure` implements interfaces defined in `Application` or `Domain`.
- **Use cases**: Express behavior as commands/queries (CQRS-style) in `Application`; thin controllers/endpoints in `API`.
- **Persistence**: EF Core + PostgreSQL by default (match DevPilot stack); migrations live with `Infrastructure`; no EF types leaking into `Domain` beyond interfaces/DTOs owned by the domain layer.
- **Cross-cutting**: Authorization policies map to namespace roles and platform roles; sensitive operations emit **audit** entries.
- **Configuration**: Secrets and connection strings via environment / user secrets / compose — never commit real secrets (follow DevPilot’s `appsettings.*.template` pattern).

---

## 4. Frontend (Angular) rules

- **Structure**: New capability → new **feature** module under `features/`; reusable UI → `shared/`; auth, HTTP, interceptors → `core/`.
- **Data access**: Prefer typed API layer (generated from OpenAPI or hand-maintained models kept in lockstep with backend).
- **Security**: Assume OIDC or equivalent consistent with DevPilot (`angular-auth-oidc-client` or successor); guard routes by role/claims where the backend enforces the same rules.
- **UX**: Registry flows — browse/search, namespace admin, publish/version, token management, audit views — should mirror SkillHub’s mental model **and** look like DevPilot (same chrome, density, and component vocabulary).

### Visual design system — DevPilot template (colors & tokens)

The UI must reuse **DevPilot’s template styling**, not a separate theme.

- **Canonical stylesheet**: Use **`~/dev/andy-devpilot/frontend/src/styles.css`** (“DevPilot Design System — Professional Edition”) as the baseline for this app’s global CSS: copy it into `frontend/src/styles.css` at project creation (or keep a deliberate fork documented in `docs/`). Extend it only additively; do not replace it with Material/CDK defaults-only styling or a new unrelated palette.
- **Brand colors**: Primary **`#6366f1`** (`--brand-primary`), secondary **`#8b5cf6`** (`--brand-secondary`), accent **`#a855f7`** (`--brand-accent`). Hero and gradient accents use **`--brand-gradient`** (`135deg` indigo → violet → purple). Links, focus rings, and glow shadows stay aligned with those hues (see DevPilot `:selection`, `--shadow-glow`).
- **Light / dark**: Implement the same theme contract as DevPilot: **`[data-theme="light"]`** and **`[data-theme="dark"]`** on the document root, with paired **`--surface-*`**, **`--text-*`**, **`--border-*`**, **`--sidebar-*`**, **`--header-*`**, and semantic **`--success-*` / `--warning-*` / `--error-*` / `--info-*`** tokens.
- **Spacing, type, radius, motion**: Use DevPilot’s **`--space-*`**, **`--font-sans`** / **`--font-mono`**, **`--text-*`** scale, **`--radius-*`**, **`--duration-*`**, and **`--ease-*`** variables so spacing and typography match the template.
- **Surfaces & elevation**: Prefer **`--surface-ground`**, **`--surface-card`**, **`--surface-hover`**, **`--shadow-card`**, **`--border-light`** / **`--border-default`** for layouts instead of ad hoc hex grays.
- **Chrome dimensions**: Match shell variables unless an ADR says otherwise: **`--sidebar-width`** (`280px`), **`--sidebar-collapsed`**, **`--header-height`** (`64px`), **`--content-max-width`** (`1400px`).
- **Primary actions**: Follow DevPilot button patterns — primary CTAs use the **indigo → violet** gradient (`--brand-primary` → `--brand-secondary`), not a flat unrelated blue.
- **Skill / markdown previews**: When rendering `skill.md` or registry README-style content, reuse DevPilot’s **markdown / code-block / Prism** styling blocks from the same `styles.css` so fenced code and inline code match DevPilot light/dark behavior.

---

## 5. Naming & branding

- Use neutral solution/project names (e.g. `SkillRegistry`, `OrgSkills`) in code and assemblies unless legal/product has settled on **SkillHub** branding.
- Reserve **slug** conventions compatible with `namespace--skill` style if CLI interoperability matters later.

---

## 6. Docker & local dev

- Default stack: **PostgreSQL + API + Angular (nginx in container)** analogous to DevPilot’s compose layout.
- Document ports and env vars in `README.md` / `docs/` (API base URL, public URL for deep links and token callbacks).

---

## 7. AI assistant expectations

When generating or editing code in this repo:

1. **Preserve** DevPilot-style layering and folder names unless explicitly refactoring across the whole codebase.
2. **Prefer** small, reviewable changes; avoid drive-by rewrites unrelated to the requested feature.
3. **Align** new endpoints and entities with SkillHub-like domains: namespaces, skills/packages, versions, memberships, tokens, audit events, storage metadata.
4. After API changes, **update OpenAPI artifacts** and frontend generated/types as part of the same task when tooling exists.
5. For UI work, **use DevPilot CSS variables and patterns** (`var(--brand-primary)`, surfaces, sidebar/header tokens); avoid introducing parallel color systems or mismatched primary hues.

---

## 8. Out of scope (unless explicitly requested)

- Rewriting DevPilot’s sandbox/VPS/SignalR concerns unless this product needs them.
- Porting SkillHub’s Java/React stack; **behavior and boundaries**, not line-by-line ports.

---

*Last aligned with SkillHub README highlights (registry, RBAC, audit, versioning, storage, tokens), DevPilot README structure, and DevPilot `frontend/src/styles.css` design tokens.*
