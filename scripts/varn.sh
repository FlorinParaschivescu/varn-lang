#!/usr/bin/env sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
dotnet run --project "$root/src/Varn.Cli/Varn.Cli.csproj" -- "$@"
