#!/usr/bin/env bash
set -euo pipefail
while IFS= read -r mod; do
  shopt -s nullglob
  test_projects=(tests/"$mod"*.Tests/*.csproj)
  for project in "${test_projects[@]}"; do
    dotnet test "$project" -c Release
  done
done < <(jq -r '.[].name' <<< "$MODS")
