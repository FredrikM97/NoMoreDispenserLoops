#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
shopt -s nullglob

if [[ $# -gt 1 || ( $# -eq 1 && "$1" != "--without-game" ) ]]; then
    echo "Usage: scripts/check.sh [--without-game]" >&2
    exit 1
fi

projects=(
    "$root"/scripts/OniBuildInfo/OniBuildInfo.csproj
    "$root"/tests/*/*.csproj
)
if [[ ${1:-} != "--without-game" ]]; then
    projects+=("$root"/*/*.csproj)
fi
for project in "${projects[@]}"; do
    dotnet format "$project" --verify-no-changes
done

test_projects=("$root"/tests/*/*.csproj)
for project in "${test_projects[@]}"; do
    dotnet test "$project" -c Release
done
