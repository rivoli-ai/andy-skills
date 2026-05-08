# Skill Registry (andy-skills)

Enterprise-oriented **agent skill registry** MVP aligned with [RULE.md](./RULE.md): SkillHub-style domains on a **DevPilot-shaped** stack (**ASP.NET Core 10**, **Angular 19**, **PostgreSQL**).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for Postgres)

## Database

**Credentials match DevPilot’s Postgres defaults:** user `analyser`, password `analyser_password`, host `localhost`, port `5432`. Only the **database name** differs: **`skillregistry`** (DevPilot uses `analyzer` by default).

### Using DevPilot’s Postgres (recommended when DevPilot is already running)

Create the database once (from any `psql` connected as `analyser`):

```sql
CREATE DATABASE skillregistry;
```

[`backend/src/API/appsettings.json`](backend/src/API/appsettings.json) points at `skillregistry` with the same login as DevPilot.

### Standalone Postgres via Compose

From the repo root:

```bash
docker compose up -d postgres
```

Uses the same user/password as DevPilot (`DB_USER` / `DB_PASSWORD` env overrides supported). If **`5432` is already taken** (e.g. by DevPilot), set `POSTGRES_PORT=5433` when starting compose and add `Port=5433` to `ConnectionStrings:DefaultConnection`.

## Backend API

```bash
cd backend/src/API
dotnet run --launch-profile http
```

- HTTP: `http://localhost:5289`
- Health: `GET /health`, `GET /api/health`
- OpenAPI document (Development): served via `Microsoft.AspNetCore.OpenApi` (`MapOpenApi`)

Apply EF migrations happens automatically on startup (`Migrate()`). To add migrations later:

```bash
dotnet ef migrations add <Name> --project ../../Infrastructure/SkillRegistry.Infrastructure.csproj --startup-project SkillRegistry.API.csproj --output-dir Persistence/Migrations
```

## Frontend

Uses DevPilot’s global **`styles.css`** tokens (copied from `~/dev/andy-devpilot/frontend/src/styles.css`). Dev server proxies `/api` to the backend (`frontend/proxy.conf.json`).

The SPA includes forms for **creating namespaces**, **creating skill packages**, **listing versions**, **publishing versions** (remote `artifactUri`), and **uploading a ZIP** for a version (stored in the database). Optional **Dev user** under **Settings** maps to `X-Dev-User-Id` for audit trails.

```bash
cd frontend
npm install
npm start
```

SPA: `http://localhost:4200`

Ensure the API is running on port **5289** while using `npm start`.

## Dev identity header

Optional **`X-Dev-User-Id`** header is recorded as the actor for audits until OIDC is wired (same idea as SkillHub mock headers in local mode).

## API sketch

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/namespaces` | List namespaces |
| POST | `/api/namespaces` | Create namespace (+ owner membership) |
| PUT | `/api/namespaces/{slug}` | Update namespace (display name, description, visibility) |
| DELETE | `/api/namespaces/{slug}` | Delete namespace and all packages/versions (cascade) |
| GET | `/api/namespaces/{slug}/packages` | List packages |
| POST | `/api/namespaces/{slug}/packages` | Create package |
| GET | `/api/namespaces/{slug}/packages/{skill}/versions` | List versions for a skill |
| POST | `/api/namespaces/{slug}/packages/{skill}/versions` | Publish version (`artifactUri`, semver `version`; omit ZIP unless referencing remote artifact) |
| POST | `/api/namespaces/{slug}/packages/{skill}/versions/upload` | Publish version by multipart ZIP upload (bytes stored in DB `skill_versions.PackageZip`; `artifactUri` is the registry install URL) |
| GET | `/api/install/{ns}/{skill}/{version}/package.zip` | Download stored ZIP from DB, or redirect if version uses remote HTTP(S) `artifactUri` only |
| GET | `/api/namespaces/{slug}/packages/{skill}/versions/{version}/SKILL.md` | Extract `SKILL.md` from the stored ZIP (manager preview); versions without stored ZIP return 404 |
| GET | `/api/search?q=` | Search packages |

Uploaded ZIPs are stored **in PostgreSQL** (`bytea`), not on disk. Set **`Skills:PublicBaseUrl`** in [`backend/src/API/appsettings.json`](backend/src/API/appsettings.json) to the public origin users use for install links (behind TLS/proxy if applicable).

## CLI: install a skill locally (full tree)

The [`cli/`](cli/) package downloads `{registry}/api/install/{namespace}/{skill}/{version}/package.zip` and **extracts the archive under your skills folder** (default **`~/.agents/skills`**, preserving directories). Optional **`SKILL_REGISTRY_URL`** env var avoids repeating `--registry`.

```bash
cd /path/to/andy-skills/cli
npm install

# From repo root (andy-skills/)
node cli/bin/andy-skill.js install --registry http://localhost:5289 my-namespace my-skill 1.0.0

# Or shorthand:
node cli/bin/andy-skill.js install --registry http://localhost:5289 my-namespace/my-skill@1.0.0

# Default registry via env:
SKILL_REGISTRY_URL=http://localhost:5289 node cli/bin/andy-skill.js install my-namespace/my-skill@1.0.0

# Another editor’s folder (example: Cline):
node cli/bin/andy-skill.js install --registry http://localhost:5289 my-namespace/my-skill@1.0.0 --dir ~/.cline/skills
```

Install the CLI globally from the clone — **`andy-skill`** is then on your `PATH` (npm adds the shim; you never type `node` yourself):

```bash
cd /path/to/andy-skills/cli && npm install && npm install -g .
andy-skill install --registry http://localhost:5289 my-namespace/my-skill@1.0.0
```

One-off without global install (still requires Node): `npx` resolves and runs the package binary the same way, e.g. from the registry after **`npm publish`**,  
`npx --yes @andy-skill/cli install …`.

After **`npm publish`** of `@andy-skill/cli`, `npx @andy-skill/cli install …` works without cloning this repo (same underlying CLI).

### Without installing globally — still uses Node

`npx` avoids **`npm install -g`** but does **not** remove the Node requirement.

## License

Specify your license here when you open-source or distribute internally.
