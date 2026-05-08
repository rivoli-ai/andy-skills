#!/usr/bin/env sh
# Stop the skill stack cleanly, then build and start (avoids stale containers / port conflicts).
set -eu
cd "$(dirname "$0")/.."
docker compose down --remove-orphans
exec docker compose up -d --build "$@"
