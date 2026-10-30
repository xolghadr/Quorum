#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

compose=(docker compose --env-file deploy/local.env -p quorum -f docker-compose.yml)

if "${compose[@]}" ps --status running --services 2>/dev/null | grep -qx quorum; then
  printf '%s\n' "A self-contained deployment is already running. Refusing to start a second one. Stored data was not erased."
  exit 1
fi

mkdir -p deploy/.run
set -a
# shellcheck disable=SC1091
. deploy/local.env
set +a

docker run --rm httpd:2.4.69-alpine htpasswd -nbB quorum "$COLLECTOR_PASSWORD" > deploy/.run/htpasswd
line=$(docker run --rm httpd:2.4.69-alpine htpasswd -nbB quorum "$PROMETHEUS_PASSWORD")
hash=${line#*:}
printf 'basic_auth_users:\n  quorum: "%s"\n' "$hash" > deploy/.run/web.yml

"${compose[@]}" up -d
