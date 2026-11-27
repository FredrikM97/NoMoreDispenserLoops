#!/usr/bin/env bash
set -euo pipefail
mkdir -p "$GITHUB_WORKSPACE/workshop-upload"
archive="$RUNNER_TEMP/$MOD-$VERSION.zip"
gh release download "$TAG" --repo "$REPOSITORY" \
  --pattern "$MOD-$VERSION.zip" --dir "$RUNNER_TEMP"
for file in "$MOD.dll" mod.yaml mod_info.yaml; do
  if ! unzip -p "$archive" "$MOD/$file" > "$GITHUB_WORKSPACE/workshop-upload/$file"; then
    echo "::error::Release package is missing $file."
    exit 1
  fi
  if [[ ! -s "$GITHUB_WORKSPACE/workshop-upload/$file" ]]; then
    echo "::error::Release package contains an empty $file."
    exit 1
  fi
done
if unzip -Z1 "$archive" | grep -Fxq "$MOD/THIRD-PARTY-NOTICES.txt"; then
  unzip -p "$archive" "$MOD/THIRD-PARTY-NOTICES.txt" > "$GITHUB_WORKSPACE/workshop-upload/THIRD-PARTY-NOTICES.txt"
fi
if ! grep -Fxq "version: $VERSION" "$GITHUB_WORKSPACE/workshop-upload/mod_info.yaml"; then
  echo "::error::Release package version does not match $TAG."
  exit 1
fi
echo "RELEASE_VERSION=$VERSION" >> "$GITHUB_ENV"
