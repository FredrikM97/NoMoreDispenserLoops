#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf -- "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/runner/package/TestMod" "$tmp/workspace" "$tmp/source/TestMod/anim"

cat > "$tmp/bin/gh" <<'EOF'
#!/usr/bin/env bash
case "$1 $2" in
  "release view")
    [[ "$*" == "release view unreleased --json assets --jq .assets[].name" ]] || exit 1
    if [[ "${GH_FAIL:-}" == 1 ]]; then
      echo "Simulated release lookup failure" >&2
      exit 1
    fi
    echo old.zip
    ;;
  "release delete-asset" | "release upload" | "release edit") echo "$1 $2" >> "$GH_CALLS" ;;
  "release download") cp "$FIXTURE" "$RUNNER_TEMP/$MOD-$VERSION.zip" ;;
  *) echo "Unexpected gh command" >&2; exit 1 ;;
esac
EOF
chmod +x "$tmp/bin/gh"
export PATH="$tmp/bin:$PATH"
export RUNNER_TEMP="$tmp/runner" GITHUB_WORKSPACE="$tmp/workspace" GITHUB_ENV="$tmp/env"
export MOD=TestMod VERSION=1.2.3 TAG=v1.2.3 REPOSITORY=example/repo
export FIXTURE="$tmp/fixture.zip" GH_CALLS="$tmp/gh-calls"

if MODS=invalid bash "$root/scripts/release/test.sh" > "$tmp/error" 2>&1; then
  echo "Expected invalid test matrix to fail" >&2
  exit 1
fi
if MODS=invalid bash "$root/scripts/release/package.sh" > "$tmp/error" 2>&1; then
  echo "Expected invalid package matrix to fail" >&2
  exit 1
fi

printf '123\n' > "$RUNNER_TEMP/package/TestMod/oni-build.txt"
printf 'archive\n' > "$RUNNER_TEMP/package/TestMod-1.2.3.zip"
git -C "$GITHUB_WORKSPACE" init -q
git -C "$GITHUB_WORKSPACE" -c user.name=test -c user.email=test@example.com \
  commit --allow-empty -qm initial
if (cd "$GITHUB_WORKSPACE" && GH_FAIL=1 MODS='[{"name":"TestMod"}]' COMMIT=HEAD LAST_TAG='' \
    bash "$root/scripts/release/publish.sh" > "$tmp/error" 2>&1); then
  echo "Expected release asset lookup to fail" >&2
  exit 1
fi
grep -Fq 'Simulated release lookup failure' "$tmp/error"
(cd "$GITHUB_WORKSPACE" && MODS='[{"name":"TestMod"}]' COMMIT=HEAD LAST_TAG='' \
  bash "$root/scripts/release/publish.sh")
grep -Fxq 'release delete-asset' "$GH_CALLS"
grep -Fxq 'release upload' "$GH_CALLS"
grep -Fxq 'release edit' "$GH_CALLS"

printf 'binary\n' > "$tmp/source/TestMod/TestMod.dll"
printf 'metadata\n' > "$tmp/source/TestMod/mod.yaml"
printf 'version: 1.2.3\n' > "$tmp/source/TestMod/mod_info.yaml"
printf 'resource\n' > "$tmp/source/TestMod/anim/resource.bin"
(cd "$tmp/source" && zip -qr "$FIXTURE" TestMod)
bash "$root/scripts/release/download-workshop.sh"
cmp "$tmp/source/TestMod/anim/resource.bin" "$GITHUB_WORKSPACE/workshop-upload/anim/resource.bin"
grep -Fxq 'RELEASE_VERSION=1.2.3' "$GITHUB_ENV"

export VERSION=1.2.4
if bash "$root/scripts/release/download-workshop.sh" > "$tmp/error" 2>&1; then
  echo "Expected a package version mismatch to fail" >&2
  exit 1
fi
grep -Fq 'Release package version does not match' "$tmp/error"

export VERSION=1.2.3
zip -dq "$FIXTURE" TestMod/mod.yaml
if bash "$root/scripts/release/download-workshop.sh" > "$tmp/error" 2>&1; then
  echo "Expected a missing package file to fail" >&2
  exit 1
fi
grep -Fq 'missing or contains an empty mod.yaml' "$tmp/error"
(cd "$tmp/source" && zip -q "$FIXTURE" TestMod/mod.yaml)
printf 'unexpected\n' > "$tmp/source/unexpected"
(cd "$tmp/source" && zip -q "$FIXTURE" unexpected)
if bash "$root/scripts/release/download-workshop.sh" > "$tmp/error" 2>&1; then
  echo "Expected an unexpected package path to fail" >&2
  exit 1
fi
grep -Fq 'Release package contains an unexpected path' "$tmp/error"
echo "Release step tests passed."
