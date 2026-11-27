#!/usr/bin/env bash
set -euo pipefail
mod_names=$(jq -er '.[].name' <<< "$MODS")
bash scripts/package.sh --all "$VERSION" "$RUNNER_TEMP/package"
while IFS= read -r mod; do
  oni_build=$(<"$RUNNER_TEMP/package/$mod/oni-build.txt")
  if [[ ! "$oni_build" =~ ^[0-9]+$ || ! -s "$RUNNER_TEMP/package/$mod-$VERSION.zip" ]]; then
    echo "::error::Package for '$mod' is missing or has invalid ONI build metadata."
    exit 1
  fi
done <<< "$mod_names"
