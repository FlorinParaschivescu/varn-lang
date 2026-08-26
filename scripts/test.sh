#!/usr/bin/env sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
dotnet build "$root/Varn.slnx" --nologo
dotnet run --project "$root/tests/Varn.Tests/Varn.Tests.csproj" --no-build
dotnet run --project "$root/tests/Varn.Adapter.Tests/Varn.Adapter.Tests.csproj" --no-build
