#!/usr/bin/env sh
# Mirrors .github/workflows/ci.yml. If this passes, CI passes; anything CI checks that
# this does not is a gap that lets a push break main.
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)

# Restore from the solution, not from a project: this is what catches a solution entry
# pointing at a project that no longer exists.
dotnet restore "$root/Varn.slnx"
dotnet build "$root/Varn.slnx" --no-restore --nologo
dotnet run --project "$root/tests/Varn.Tests/Varn.Tests.csproj" --no-build
dotnet run --project "$root/tests/Varn.Adapter.Tests/Varn.Adapter.Tests.csproj" --no-build
dotnet format "$root/Varn.slnx" --verify-no-changes --no-restore --verbosity minimal
dotnet run --project "$root/src/Varn.Cli/Varn.Cli.csproj" --no-build -- check "$root/examples/hello.varn"
dotnet run --project "$root/src/Varn.Cli/Varn.Cli.csproj" --no-build -- inspect "$root/examples/hello.varn"
dotnet run --project "$root/src/Varn.Cli/Varn.Cli.csproj" --no-build -- run "$root/examples/hello.varn" --allow console.write
echo "test: build, tests, formatting, and CLI smoke all passed"
