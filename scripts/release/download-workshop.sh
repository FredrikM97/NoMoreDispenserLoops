#!/usr/bin/env bash
set -euo pipefail
mkdir -p "$GITHUB_WORKSPACE/workshop-upload"
archive="$RUNNER_TEMP/$MOD-$VERSION.zip"
gh release download "$TAG" --repo "$REPOSITORY" \
  --pattern "$MOD-$VERSION.zip" --dir "$RUNNER_TEMP"
entries=$(unzip -Z1 "$archive")
declare -A extracted=()
while IFS= read -r entry; do
  if [[ "$entry" != "$MOD/"* ]]; then
    echo "::error::Release package contains an unexpected path."
    exit 1
  fi
  relative=${entry#"$MOD/"}
  if [[ -z "$relative" ]]; then
    continue
  fi
  if [[ "$relative" == *\\* || "$relative" == *[\*\?\[]* ||
        "/$relative/" == *"/../"* || "/$relative/" == *"/./"* ||
        "$relative" == /* || "$relative" == *//* ]]; then
    echo "::error::Release package contains an unsafe path."
    exit 1
  fi
  if [[ "$relative" == */ ]]; then
    continue
  fi
  if [[ -n "${extracted["$relative"]+present}" ]]; then
    echo "::error::Release package contains a duplicate path."
    exit 1
  fi
  extracted["$relative"]=1
  destination="$GITHUB_WORKSPACE/workshop-upload/$relative"
  mkdir -p "$(dirname "$destination")"
  if ! unzip -p "$archive" "$entry" > "$destination"; then
    echo "::error::Failed to extract $entry from the release package."
    exit 1
  fi
done <<< "$entries"
for file in "$MOD.dll" mod.yaml mod_info.yaml; do
  if [[ -z "${extracted["$file"]+present}" || ! -s "$GITHUB_WORKSPACE/workshop-upload/$file" ]]; then
    echo "::error::Release package is missing or contains an empty $file."
    exit 1
  fi
done
if ! grep -Fxq "version: $VERSION" "$GITHUB_WORKSPACE/workshop-upload/mod_info.yaml"; then
  echo "::error::Release package version does not match $TAG."
  exit 1
fi
echo "RELEASE_VERSION=$VERSION" >> "$GITHUB_ENV"
