#!/bin/sh
# Packs every shippable Varn project and verifies the produced set, so a project that silently
# stops being packable fails here instead of at someone's first `dotnet add package`.
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
output=${1:-"$root/artifacts"}

rm -rf "$output"
dotnet pack "$root/Varn.slnx" --configuration Release --output "$output" --nologo

expected="Varn.Adapter Varn.Cli Varn.Lexer Varn.ModuleSdk Varn.Modules.Standard Varn.Parser Varn.Runtime Varn.Syntax Varn.ToolHost Varn.TypeSystem"
missing=""
for package in $expected; do
    if ! ls "$output/$package."*.nupkg >/dev/null 2>&1; then
        missing="$missing $package"
    fi
done

if [ -n "$missing" ]; then
    echo "pack: missing expected packages:$missing" >&2
    echo "pack: a project probably lost <IsPackable>true</IsPackable>." >&2
    exit 1
fi

unexpected=$(ls "$output"/*.nupkg | sed 's|.*/||; s|\.[0-9].*||' | sort -u | tr '\n' ' ')
echo "pack: produced $(ls "$output"/*.nupkg | wc -l | tr -d ' ') packages: $unexpected"
