#!/usr/bin/env bash
set -euo pipefail
last_tag=$(git for-each-ref --sort=-creatordate --format='%(refname:short)' refs/tags \
  | grep -E '(^v[0-9]+\.[0-9]+\.[0-9]+$|^[A-Za-z0-9][A-Za-z0-9_-]*-v[0-9]+\.[0-9]+\.[0-9]+$)' \
  | head -n 1 || true)
range=HEAD
if [[ -n "$last_tag" ]]; then
  range="$last_tag..HEAD"
fi
{
  echo '## Changes'
  git log "$range" --no-merges --format='- %s (%h)'
  echo
  if [[ -n "$last_tag" ]]; then
    echo "**Full changelog:** https://github.com/$REPOSITORY/compare/$last_tag...main"
  else
    echo "**Full changelog:** https://github.com/$REPOSITORY/commits/main"
  fi
} > release-notes.md

existing=$(gh release list --limit 1000 --json tagName,isDraft \
  --jq '[.[] | select(.tagName == "unreleased") | if .isDraft then "draft" else "published" end] | first // "missing"')
if [[ "$existing" == draft ]]; then
  gh release edit unreleased --title Unreleased --notes-file release-notes.md --target main --draft
elif [[ "$existing" == published ]]; then
  echo "::error::The unreleased release already exists and is not a draft."
  exit 1
else
  gh release create unreleased --title Unreleased --notes-file release-notes.md --target main --draft
fi
