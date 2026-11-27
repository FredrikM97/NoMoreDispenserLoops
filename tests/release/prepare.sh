#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf -- "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/workspace"

cat > "$tmp/bin/gh" <<'EOF'
#!/usr/bin/env bash
if [[ "$1 $2" != "release list" ]]; then
  echo "Unexpected gh command" >&2
  exit 1
fi
echo "${GH_RELEASE_STATE:-draft}"
EOF
chmod +x "$tmp/bin/gh"
export PATH="$tmp/bin:$PATH"
export GITHUB_WORKSPACE="$tmp/workspace" GITHUB_OUTPUT="$tmp/output"
export EVENT_NAME=workflow_dispatch RELEASE_TAG='' BUMP=patch GITHUB_SHA=test-commit

for mod in Alpha Beta; do
  mkdir -p "$GITHUB_WORKSPACE/$mod"
  printf 'project\n' > "$GITHUB_WORKSPACE/$mod/$mod.csproj"
  printf 'metadata\n' > "$GITHUB_WORKSPACE/$mod/mod.yaml"
  printf 'version: 1.2.3\n' > "$GITHUB_WORKSPACE/$mod/mod_info.yaml"
  printf 'readme\n' > "$GITHUB_WORKSPACE/$mod/README.md"
  printf '12345\n' > "$GITHUB_WORKSPACE/$mod/steam-workshop-id.txt"
done
git -C "$GITHUB_WORKSPACE" init -q
git -C "$GITHUB_WORKSPACE" -c user.name=test -c user.email=test@example.com \
  commit --allow-empty -qm initial

run_prepare() {
  : > "$GITHUB_OUTPUT"
  (cd "$GITHUB_WORKSPACE" && bash "$root/scripts/release/prepare.sh")
}

run_prepare
test "$(wc -l < "$GITHUB_OUTPUT")" -eq 5
while IFS= read -r line; do
  [[ "$line" =~ ^(tag|version|last_tag|mods|ref)= ]] || {
    echo "Invalid GitHub Actions output: $line" >&2
    exit 1
  }
done < "$GITHUB_OUTPUT"
grep -Fxq 'tag=v1.2.3' "$GITHUB_OUTPUT"
sed -n 's/^mods=//p' "$GITHUB_OUTPUT" | jq -e \
  'length == 2 and [.[].name] == ["Alpha", "Beta"] and all(.[]; .workshop_id == "12345")' >/dev/null

git -C "$GITHUB_WORKSPACE" tag v1.2.3
run_prepare
grep -Fxq 'tag=v1.2.4' "$GITHUB_OUTPUT"
grep -Fxq 'last_tag=v1.2.3' "$GITHUB_OUTPUT"

export EVENT_NAME=release RELEASE_TAG=v3.0.0
run_prepare
grep -Fxq 'ref=v3.0.0' "$GITHUB_OUTPUT"
sed -n 's/^mods=//p' "$GITHUB_OUTPUT" | jq -e 'length == 2' >/dev/null

export EVENT_NAME=release RELEASE_TAG=Alpha-v2.0.0
run_prepare
grep -Fxq 'ref=Alpha-v2.0.0' "$GITHUB_OUTPUT"
sed -n 's/^mods=//p' "$GITHUB_OUTPUT" | jq -e \
  'length == 1 and .[0].name == "Alpha"' >/dev/null

export EVENT_NAME=workflow_dispatch RELEASE_TAG='' GH_RELEASE_STATE=missing
if run_prepare > "$tmp/error" 2>&1; then
  echo "Expected a missing draft to fail" >&2
  exit 1
fi
grep -Fq 'Create the unreleased draft' "$tmp/error"
echo "Release preparation tests passed."
