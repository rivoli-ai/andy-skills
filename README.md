# Skill Registry (andy-skills)

Enterprise-oriented **agent skill registry** MVP aligned with [RULE.md](./RULE.md): SkillHub-style domains on a **DevPilot-shaped** stack (**ASP.NET Core 10**, **Angular 19**, **PostgreSQL**).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — local **`dotnet run`** / EF tooling (not needed if you only use Docker Compose for the full stack)
- [Node.js 18+](https://nodejs.org/) — local **`npm start`** (not needed if you only use Docker Compose for the full stack)
- [Docker](https://www.docker.com/) with Compose v2 (`docker compose`) — optional Postgres-only stack, or **full Postgres + API + SPA**

## Run with Docker Compose

[`docker-compose.yml`](docker-compose.yml) defines three services under the Compose project name **`skill`**:

| Service | Purpose |
|--------|---------|
| **`postgres`** | PostgreSQL 16, database **`skillregistry`** |
| **`api`** | ASP.NET Core API (listens on port **8080** inside the Docker network) |
| **`web`** | nginx serves the **production Angular build** and **proxies `/api`** to **`api`** |

From the browser’s perspective the app is **same-origin**: the SPA loads from nginx and calls **`/api/...`**, which nginx forwards to the API. You do **not** need Node or .NET on the host for this path—only Docker.

### 1. Configure environment variables

Compose reads a **`.env`** file in the **repository root** (create it if missing):

```bash
cp .env.example .env
```

Edit **`.env`** for your environment. At minimum, align **Azure AD** (`AZURE_AD_*`), **`JWT_SECRET`** (at least 32 characters), and **`SKILLS_PUBLIC_BASE_URL`** with the URL you will open in the browser (defaults assume **`http://localhost:8080`**). See **[`.env.example`](.env.example)** for every variable.

### 2. Build and start

From the **repository root**:

```bash
docker compose up -d --build
```

Recommended after failures or port changes (**stops the stack first**, removes orphaned containers):

```bash
./scripts/docker-up.sh
```

- **`--build`** rebuilds the **`api`** and **`web`** images after Dockerfile or application changes; you can omit it on later starts if images are already current.
- **`postgres` must be running and healthy** before **`api`** starts; the API resolves **`postgres`** only on the Compose network. If Postgres failed to bind a host port earlier, fix **`POSTGRES_PORT`** (see **Port conflicts**), run **`docker compose down --remove-orphans`**, then **`./scripts/docker-up.sh`** again.
- The **`api`** container applies EF migrations on startup against **`postgres:5432`** inside Docker (not **`localhost`** on your machine).

### 3. Open the app

- **UI:** `http://localhost:8080` — or whatever host/port you set with **`SKILL_WEB_PORT`** / **`SKILLS_PUBLIC_BASE_URL`**.
- **Health check:** `http://localhost:8080/api/health` (served via nginx → API).

Postgres data is stored in the Compose volume **`skill_pg`**.

### Common commands

```bash
# Follow logs (all services)
docker compose logs -f

# Stop containers (keeps volumes)
docker compose down

# Stop and delete the Postgres volume (wipes local DB)
docker compose down -v
```

### Port conflicts

- **Postgres on the host:** Compose maps the container to **`localhost:${POSTGRES_PORT:-5433}`** by default (**`5433`**) so another Postgres using **`5432`** on your machine does not block **`skill-postgres`**. From **`psql` on the host**, connect with **`Port=5433`** (or whatever you set). **Inside Docker**, the API always uses **`Host=postgres;Port=5432`** unless **`SKILL_DB_CONNECTION_STRING`** overrides it.
- To expose Postgres on **`5432`** instead, set **`POSTGRES_PORT=5432`** in **`.env`** only when that port is free.
- If **`8080`** is busy, change **`SKILL_WEB_PORT`** and **`SKILLS_PUBLIC_BASE_URL`** together so CORS and OIDC redirect URIs stay consistent.

### Troubleshooting

- **`Name or service not known`** / **`postgres`**: The API container is trying **`Host=postgres`** but that host exists **only** on the Compose network. Fix a broken Postgres publish/start (**check `docker compose ps`**), run **`docker compose down --remove-orphans`**, then **`./scripts/docker-up.sh`**. Do not point **`SKILL_DB_CONNECTION_STRING`** at **`localhost`** for **`Host`** while **`api`** runs **inside** Docker (inside the container, **`localhost` is not your laptop’s Postgres**).
- **`Bind for 0.0.0.0:5432 failed`**: Another process owns **`5432`**. Keep **`POSTGRES_PORT=5433`** (default in **[`.env.example`](.env.example)**) or choose another free host port.

### Entra ID (Azure AD)

Register the SPA URL you use (e.g. **`http://localhost:8080`**) as an allowed **redirect URI** for your app registration so OIDC sign-in works behind Compose.

### CLI install against Compose

When the registry runs only in Docker, point the CLI at the **public web origin** (not **`5289`**):

```bash
andy-skill install --registry http://localhost:8080 my-namespace/my-skill@1.0.0
```

## Database

**Credentials match DevPilot’s Postgres defaults:** user `analyser`, password `analyser_password`, host `localhost`, port `5432`. Only the **database name** differs: **`skillregistry`** (DevPilot uses `analyzer` by default).

### Using DevPilot’s Postgres (recommended when DevPilot is already running)

Create the database once (from any `psql` connected as `analyser`):

```sql
CREATE DATABASE skillregistry;
```

[`backend/src/API/appsettings.json`](backend/src/API/appsettings.json) omits the connection string in source control; local credentials live in **`appsettings.Development.json`**.

[`backend/src/API/appsettings.Development.json.example`](backend/src/API/appsettings.Development.json.example) is the template for local secrets (copy to `appsettings.Development.json`). For Docker Compose, use **[`.env`](.env)** / **[`.env.example`](.env.example)** instead.

### Standalone Postgres via Compose

If you run **`dotnet run`** and **`npm start`** on the host but want only Postgres in Docker:

```bash
docker compose up -d postgres
```

Uses **`DB_USER`** / **`DB_PASSWORD`** from **`.env`** when present. Compose publishes Postgres on **`localhost:${POSTGRES_PORT:-5433}`** by default so **`5432`** on your machine can stay free for DevPilot or another Postgres. For **`dotnet run`**, use **`ConnectionStrings:DefaultConnection`** with **`Port=5433`** when connecting to that mapped port.

## Backend API

Run the API on the host when developing without Docker:

```bash
cd backend/src/API
dotnet run --launch-profile http
```

If you use **[Docker Compose](#run-with-docker-compose)** instead, the API is reached through nginx at **`http://localhost:8080`** (same URL as the SPA); direct **`5289`** does not apply unless you publish that port yourself.

- HTTP (local profile): `http://localhost:5289`
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

Ensure the API is running on port **5289** while using **`npm start`**. If you use **[Docker Compose](#run-with-docker-compose)** instead, use **`http://localhost:8080`** for the built SPA (no `npm start`).

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
