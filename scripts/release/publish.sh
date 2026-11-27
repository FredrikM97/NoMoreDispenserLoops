#!/usr/bin/env bash
set -euo pipefail
range="$COMMIT"
if [[ -n "$LAST_TAG" ]]; then
  range="$LAST_TAG..$COMMIT"
fi
first_mod=$(jq -r '.[0].name' <<< "$MODS")
oni_build=$(<"$RUNNER_TEMP/package/$first_mod/oni-build.txt")
{
  echo "**Mods:** $(jq -r '[.[].name] | join(", ")' <<< "$MODS")"
  echo "**Built against ONI build:** $oni_build"
  echo
  echo '## Changes'
  git log "$range" --no-merges --format='- %s (%h)'
  echo
  if [[ -n "$LAST_TAG" ]]; then
    echo "**Full changelog:** https://github.com/$REPOSITORY/compare/$LAST_TAG...$TAG"
  else
    echo "**Full changelog:** https://github.com/$REPOSITORY/commits/$TAG"
  fi
} > release-notes.md

shopt -s nullglob
packages=("$RUNNER_TEMP/package/"*-"$VERSION".zip)
if [[ ${#packages[@]} -eq 0 ]]; then
  echo "::error::No mod packages were created."
  exit 1
fi
assets=$(gh release view unreleased --json assets --jq '.assets[].name')
while IFS= read -r asset; do
  if [[ "$asset" == *.zip && ! -e "$RUNNER_TEMP/package/$asset" ]]; then
    gh release delete-asset unreleased "$asset" --yes
  fi
done <<< "$assets"
gh release upload unreleased "${packages[@]}" --clobber
gh release edit unreleased --tag "$TAG" --title "$TAG" \
  --notes-file release-notes.md --target "$COMMIT" --draft=false --latest
