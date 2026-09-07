#!/bin/bash
# SessionStart hook: provision the .NET 8 SDK so `dotnet build` / `dotnet test`
# work in Claude Code on the web. Runs only in the remote environment; locally
# the user already has the SDK installed.
#
# sim/Directory.Build.props pins net8.0 across every project, and they are all
# plain Microsoft.NET.Sdk (design.md §15 keeps the engine out), so the SDK alone
# is the whole toolchain — no Godot, no export templates. Installed from the
# Ubuntu archive (dotnet-sdk-8.0), which is reachable under the environment's
# network policy — unlike builds.dotnet.microsoft.com, which the firewall blocks.
set -euo pipefail

# Runs asynchronously: the session starts immediately while this installs in the
# background. Anything that needs `dotnet` must first block on the readiness
# marker via .claude/hooks/wait-for-dotnet.sh (see CLAUDE.md > Workflow).
echo '{"async": true, "asyncTimeout": 600000}'

READY_MARKER="/tmp/.dotnet-sdk-ready"
rm -f "$READY_MARKER"

# Only run in Claude Code on the web; no-op on the user's local machine.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Idempotent: if a .NET 8 SDK is already present (cached container), do nothing.
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  echo "[session-start] .NET 8 SDK already present: $(dotnet --version)"
  touch "$READY_MARKER"
  exit 0
fi

echo "[session-start] Installing .NET 8 SDK from the Ubuntu archive..."
export DEBIAN_FRONTEND=noninteractive

# Refresh apt lists. Tolerate failures from unrelated third-party PPAs that the
# network policy blocks — dotnet-sdk-8.0 comes from the official Ubuntu archive,
# which is reachable.
apt-get update -o Acquire::Retries=3 || true

apt-get install -y --no-install-recommends dotnet-sdk-8.0

# Signal readiness so wait-for-dotnet.sh can release any blocked dotnet commands.
touch "$READY_MARKER"
echo "[session-start] Installed .NET SDK: $(dotnet --version)"
