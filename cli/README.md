# @andy-skills/cli

Download a skill ZIP from a [Skill Registry](https://github.com/rivoli-ai/andy-skills) (`GET /api/install/...`) and extract it under **`~/.agents/skills`** (or `--dir`), preserving the archive layout.

## Requirements

- Node.js **18+**

## Install

```bash
npm install -g @andy-skills/cli
```

## Usage

```bash
andy-skills install --registry https://your-registry.example.com my-ns my-skill 1.0.0
andy-skills install my-ns/my-skill@1.0.0

SKILL_REGISTRY_URL=https://your-registry.example.com andy-skills install my-ns/my-skill@1.0.0
andy-skills install my-ns my-skill 1.0.0 --dir ~/.cursor/skills
```

Help: `andy-skills --help`

## Without global install

```bash
npx --yes @andy-skills/cli install --registry https://your-registry.example.com my-ns/my-skill@1.0.0
```

## Publishing (maintainers)

Requires npm login and permission to publish under **`@andy-skills`**.

```bash
npm login
cd cli && npm publish --dry-run && npm publish
```

Scoped packages need **`publishConfig.access`: `"public"`** (already set in this package).

## License

MIT
