#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"

if [[ $# -ne 0 ]]; then
    echo "Usage: bash scripts/release.sh" >&2
    exit 1
fi

for command in git gh dotnet zip; do
    if ! command -v "$command" >/dev/null; then
        echo "$command is required." >&2
        exit 1
    fi
done

remote=$(git remote get-url origin)
remote=${remote%.git}
if [[ "$remote" =~ ^git@github\.com(-[A-Za-z0-9_.-]+)?:([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)$ ]]; then
    repository=${BASH_REMATCH[2]}
elif [[ "$remote" =~ ^(https://github\.com/|ssh://git@github\.com/)([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)$ ]]; then
    repository=${BASH_REMATCH[2]}
else
    echo "Cannot determine a GitHub repository from origin: $remote" >&2
    exit 1
fi
if [[ "$(git branch --show-current)" != main || -n "$(git status --porcelain)" ]]; then
    echo "Release requires a clean checkout of main." >&2
    exit 1
fi

while true; do
    printf 'Version bump (patch, minor, or major): ' >&2
    if ! IFS= read -r bump; then
        echo "No version bump provided; release aborted." >&2
        exit 1
    fi
    case "$bump" in
        patch|minor|major) break ;;
        *) echo "Choose patch, minor, or major." >&2 ;;
    esac
done

commit=$(git rev-parse HEAD)
remote_commit=$(gh api --hostname github.com "repos/$repository/git/ref/heads/main" --jq .object.sha)
if [[ "$commit" != "$remote_commit" ]]; then
    echo "Local main does not match GitHub main. Commit and push your changes first." >&2
    exit 1
fi
draft=$(gh release view unreleased --repo "$repository" --json isDraft --jq .isDraft)
if [[ "$draft" != true ]]; then
    echo "The unreleased release must be a draft." >&2
    exit 1
fi

git fetch --tags origin
last_tag=$(git tag --list 'v*' | awk '/^v[0-9]+\.[0-9]+\.[0-9]+$/' | sort -V | tail -n 1)
base=${last_tag#v}
base=${base:-0.0.0}
IFS=. read -r major minor patch <<< "$base"
case "$bump" in
    major) major=$((10#$major + 1)); minor=0; patch=0 ;;
    minor) minor=$((10#$minor + 1)); patch=0 ;;
    patch) patch=$((10#$patch + 1)) ;;
esac
version="$major.$minor.$patch"
tag="v$version"
if git rev-parse --verify --quiet "refs/tags/$tag" >/dev/null; then
    echo "Tag $tag already exists." >&2
    exit 1
fi

mkdir -p dist
notes="$root/dist/release-notes-$tag.md"
range=$commit
if [[ -n "$last_tag" ]]; then
    range="$last_tag..$commit"
fi
{
    echo '## Changes'
    git log "$range" --no-merges --format='- %s (%h)'
    echo
    if [[ -n "$last_tag" ]]; then
        echo "**Full changelog:** https://github.com/$repository/compare/$last_tag...$tag"
    else
        echo "**Full changelog:** https://github.com/$repository/commits/$tag"
    fi
} > "$notes"
echo "Release notes written to $notes."

dotnet test tests/NoMoreDispenserLoops.Tests/NoMoreDispenserLoops.Tests.csproj -c Release
bash scripts/package.sh "$version"

remote_commit=$(gh api --hostname github.com "repos/$repository/git/ref/heads/main" --jq .object.sha)
if [[ "$commit" != "$remote_commit" || -n "$(git status --porcelain)" ]]; then
    echo "Main or the local worktree changed during the build; release aborted." >&2
    exit 1
fi
gh release upload unreleased "$root/dist/NoMoreDispenserLoops-$version.zip" --repo "$repository" --clobber
gh release edit unreleased --repo "$repository" --tag "$tag" --title "$tag" \
    --notes-file "$notes" --target "$commit" --draft=false --latest
echo "Published $tag."
