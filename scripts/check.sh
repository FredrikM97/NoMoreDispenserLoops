#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
shopt -s nullglob

projects=(
    "$root"/*/*.csproj
    "$root"/scripts/OniBuildInfo/OniBuildInfo.csproj
    "$root"/tests/*/*.csproj
)
for project in "${projects[@]}"; do
    dotnet format "$project" --verify-no-changes
done

test_projects=("$root"/tests/*/*.csproj)
for project in "${test_projects[@]}"; do
    dotnet test "$project" -c Release
done
