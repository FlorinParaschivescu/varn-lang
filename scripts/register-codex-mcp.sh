#!/usr/bin/env sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
command -v codex >/dev/null
dotnet build "$root/Varn.slnx" --configuration Release --nologo
codex mcp add varn -- dotnet "$root/src/Varn.ToolHost/bin/Release/net10.0/Varn.ToolHost.dll"
