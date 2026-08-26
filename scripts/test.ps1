$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet build (Join-Path $root 'Varn.slnx') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project (Join-Path $root 'tests/Varn.Tests/Varn.Tests.csproj') --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project (Join-Path $root 'tests/Varn.Adapter.Tests/Varn.Adapter.Tests.csproj') --no-build
exit $LASTEXITCODE
