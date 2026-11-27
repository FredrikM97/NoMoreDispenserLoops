#!/usr/bin/env bash
set -euo pipefail

legacy_mod=""
version=""
if [[ "$EVENT_NAME" == "workflow_dispatch" ]]; then
  draft=$(gh release list --limit 1000 --json tagName,isDraft \
    --jq '[.[] | select(.tagName == "unreleased") | if .isDraft then "draft" else "published" end] | first // "missing"')
  if [[ "$draft" != draft ]]; then
    echo "::error::Create the unreleased draft before publishing."
    exit 1
  fi
elif [[ "$RELEASE_TAG" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  version="${BASH_REMATCH[1]}"
elif [[ "$RELEASE_TAG" =~ ^([A-Za-z0-9][A-Za-z0-9_-]*)-v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  legacy_mod="${BASH_REMATCH[1]}"
  version="${BASH_REMATCH[2]}"
else
  echo "::error::Expected a vMAJOR.MINOR.PATCH or legacy MOD-vMAJOR.MINOR.PATCH tag."
  exit 1
fi

shopt -s nullglob
projects=("$GITHUB_WORKSPACE"/*/*.csproj)
if [[ ${#projects[@]} -eq 0 ]]; then
  echo "::error::No top-level mod projects were found."
  exit 1
fi

mods='[]'
initial_versions=()
for project in "${projects[@]}"; do
  mod=$(basename "$project" .csproj)
  if [[ "$(basename "$(dirname "$project")")" != "$mod" ||
        ! "$mod" =~ ^[A-Za-z0-9][A-Za-z0-9_-]*$ ]]; then
    echo "::error::Mod project must be in a matching top-level folder: $project"
    exit 1
  fi
  if [[ -n "$legacy_mod" && "$mod" != "$legacy_mod" ]]; then
    continue
  fi
  for file in mod.yaml mod_info.yaml README.md; do
    if [[ ! -s "$GITHUB_WORKSPACE/$mod/$file" ]]; then
      echo "::error::Mod '$mod' is missing $mod/$file."
      exit 1
    fi
  done

  workshop_id_file="$GITHUB_WORKSPACE/$mod/steam-workshop-id.txt"
  workshop_id=""
  if [[ -s "$workshop_id_file" ]]; then
    workshop_id=$(<"$workshop_id_file")
  fi
  if [[ ! "$workshop_id" =~ ^0*[1-9][0-9]*$ ]]; then
    echo "::error::Set a numeric Steam Workshop item ID in $mod/steam-workshop-id.txt."
    exit 1
  fi

  initial_version=$(sed -nE 's/^version: ([0-9]+\.[0-9]+\.[0-9]+)$/\1/p' "$GITHUB_WORKSPACE/$mod/mod_info.yaml" | head -n 1)
  if [[ -z "$initial_version" ]]; then
    echo "::error::No valid version in $mod/mod_info.yaml."
    exit 1
  fi
  initial_versions+=("$initial_version")
  mods=$(jq --arg name "$mod" --arg workshop_id "$workshop_id" \
    '. + [{name: $name, workshop_id: $workshop_id}]' <<< "$mods")
done

if [[ "$mods" == '[]' ]]; then
  echo "::error::No matching mods were found."
  exit 1
fi

last_tag=""
ref="$RELEASE_TAG"
if [[ "$EVENT_NAME" == "workflow_dispatch" ]]; then
  last_tag=$(git tag --list 'v*' | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1 || true)
  if [[ -n "$last_tag" ]]; then
    base="${last_tag#v}"
    IFS=. read -r major minor patch <<< "$base"
    case "$BUMP" in
      major) major=$((10#$major + 1)); minor=0; patch=0 ;;
      minor) major=$((10#$major)); minor=$((10#$minor + 1)); patch=0 ;;
      patch) major=$((10#$major)); minor=$((10#$minor)); patch=$((10#$patch + 1)) ;;
      *) echo "::error::Invalid version bump."; exit 1 ;;
    esac
    version="$major.$minor.$patch"
  else
    version=$(printf '%s\n' "${initial_versions[@]}" | sort -V | tail -n 1)
  fi
  RELEASE_TAG="v$version"
  if git rev-parse --verify --quiet "refs/tags/$RELEASE_TAG" >/dev/null; then
    echo "::error::Tag $RELEASE_TAG already exists."
    exit 1
  fi
  ref="$GITHUB_SHA"
fi

echo "tag=$RELEASE_TAG" >> "$GITHUB_OUTPUT"
echo "version=$version" >> "$GITHUB_OUTPUT"
echo "last_tag=$last_tag" >> "$GITHUB_OUTPUT"
echo "mods=$mods" >> "$GITHUB_OUTPUT"
echo "ref=$ref" >> "$GITHUB_OUTPUT"
