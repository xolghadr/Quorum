#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

docker compose --env-file deploy/local.env -p quorum -f docker-compose.yml down -v
