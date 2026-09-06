#!/bin/bash
# Blocks until the .NET 8 SDK provisioned by the async session-start hook is
# ready, then returns. Run this once before the first `dotnet` command in a
# Claude Code on the web session (see CLAUDE.md > Workflow).
#
# The real readiness condition is "a working .NET 8 SDK", so we poll for that
# directly — robust regardless of how far along the background install is.
set -euo pipefail

READY_MARKER="/tmp/.dotnet-sdk-ready"
TIMEOUT_SECS="${1:-600}"
waited=0

dotnet_ready() {
  command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^8\.'
}

if dotnet_ready; then
  echo "[wait-for-dotnet] ready: $(dotnet --version)"
  exit 0
fi

echo "[wait-for-dotnet] waiting for the background .NET 8 SDK install..."
while [ "$waited" -lt "$TIMEOUT_SECS" ]; do
  if [ -f "$READY_MARKER" ] && dotnet_ready; then
    echo "[wait-for-dotnet] ready after ${waited}s: $(dotnet --version)"
    exit 0
  fi
  sleep 3
  waited=$((waited + 3))
done

echo "[wait-for-dotnet] timed out after ${TIMEOUT_SECS}s; .NET SDK not ready." >&2
echo "[wait-for-dotnet] check the session-start hook log for install errors." >&2
exit 1
