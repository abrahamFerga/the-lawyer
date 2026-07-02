#!/usr/bin/env bash
# Boot TheLawyer to a known-good running state and point the .http catalog at it.
#
# One command to a running stack: starts the Aspire AppHost, waits for the
# dashboard + API, writes http/.env (API_BASE) for the http/*.http catalog,
# and prints the URLs. Intended for Linux/WSL/CI.
#
# Uses `dotnet run` (NOT `aspire run`): the installed Aspire CLI (13.x) rejects
# the repo's pinned 9.0.0 Aspire packages. See memory: aspire-version-mismatch.
set -euo pipefail

DASHBOARD_PORT="${DASHBOARD_PORT:-15090}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-180}"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
apphost_proj="$repo_root/src/TheLawyer.AppHost/TheLawyer.AppHost.csproj"
env_file="$repo_root/http/.env"
log_file="$(mktemp)"

echo "==> Starting TheLawyer AppHost (dotnet run --launch-profile http)..."
( cd "$repo_root" && dotnet run --project "$apphost_proj" --launch-profile http >"$log_file" 2>&1 ) &
apphost_pid=$!

wait_for() { # <seconds> <description> <command...>
  local secs="$1" what="$2"; shift 2
  local deadline=$(( SECONDS + secs ))
  until "$@" >/dev/null 2>&1; do
    if (( SECONDS >= deadline )); then echo "Timed out waiting for: $what" >&2; exit 1; fi
    sleep 2
  done
}

echo "==> Waiting for dashboard on :$DASHBOARD_PORT..."
wait_for "$TIMEOUT_SECONDS" "dashboard" \
  bash -c "curl -sf -o /dev/null http://localhost:$DASHBOARD_PORT/ || curl -s -o /dev/null http://localhost:$DASHBOARD_PORT/"

# Discover the API port from the AppHost console log (Aspire logs the resource endpoint),
# falling back to probing localhost ports for /health == 200 "Healthy".
echo "==> Discovering API endpoint..."
api_base=""
deadline=$(( SECONDS + TIMEOUT_SECONDS ))
while [ -z "$api_base" ] && (( SECONDS < deadline )); do
  for port in $(ss -tlnH 2>/dev/null | grep -oE '127.0.0.1:[0-9]+|\[::1\]:[0-9]+' | grep -oE '[0-9]+$' | sort -u); do
    body="$(curl -s --max-time 3 "http://localhost:$port/health" 2>/dev/null || true)"
    if [ "$(printf '%s' "$body" | tr -d '[:space:]')" = "Healthy" ]; then
      api_base="http://localhost:$port"; break
    fi
  done
  [ -z "$api_base" ] && sleep 2
done
[ -z "$api_base" ] && { echo "Could not locate the API /health endpoint." >&2; exit 1; }

# Write http/.env (preserve other keys).
tmp="$(mktemp)"
{ echo "API_BASE=$api_base"; [ -f "$env_file" ] && grep -vE '^\s*API_BASE\s*=' "$env_file" || true; } >"$tmp"
mv "$tmp" "$env_file"

cat <<EOF

TheLawyer is running.
  Dashboard : http://localhost:$DASHBOARD_PORT
  API       : $api_base
  Wrote     : $env_file (API_BASE)
  AppHost PID: $apphost_pid  (stop with: kill $apphost_pid)

Exercise it: open http/foundations.http and send requests.
EOF
