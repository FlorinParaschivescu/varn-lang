$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'Varn.slnx'
$toolHost = Join-Path $root 'src/Varn.ToolHost/bin/Release/net10.0/Varn.ToolHost.dll'
$codex = Get-Command codex -ErrorAction Stop

dotnet build $solution --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $codex.Source mcp add varn -- dotnet $toolHost
exit $LASTEXITCODE
