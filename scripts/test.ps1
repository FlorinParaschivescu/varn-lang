$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# Mirrors .github/workflows/ci.yml. Restoring from the solution is what catches a solution
# entry pointing at a project that no longer exists.
$steps = @(
    @('restore', (Join-Path $root 'Varn.slnx')),
    @('build', (Join-Path $root 'Varn.slnx'), '--no-restore', '--nologo'),
    @('run', '--project', (Join-Path $root 'tests/Varn.Tests/Varn.Tests.csproj'), '--no-build'),
    @('run', '--project', (Join-Path $root 'tests/Varn.Adapter.Tests/Varn.Adapter.Tests.csproj'), '--no-build'),
    @('format', (Join-Path $root 'Varn.slnx'), '--verify-no-changes', '--no-restore', '--verbosity', 'minimal'),
    @('run', '--project', (Join-Path $root 'src/Varn.Cli/Varn.Cli.csproj'), '--no-build', '--', 'check', (Join-Path $root 'examples/hello.varn')),
    @('run', '--project', (Join-Path $root 'src/Varn.Cli/Varn.Cli.csproj'), '--no-build', '--', 'inspect', (Join-Path $root 'examples/hello.varn')),
    @('run', '--project', (Join-Path $root 'src/Varn.Cli/Varn.Cli.csproj'), '--no-build', '--', 'run', (Join-Path $root 'examples/hello.varn'), '--allow', 'console.write')
)

foreach ($step in $steps) {
    & dotnet @step
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Output 'test: build, tests, formatting, and CLI smoke all passed'
