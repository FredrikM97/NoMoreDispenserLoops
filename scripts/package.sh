#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
version=${1:-}
output_root=${2:-"$root/dist"}

if [[ $# -gt 2 || ( -n "$version" && ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ) ]]; then
    echo "Usage: scripts/package.sh [VERSION] [OUTPUT_DIRECTORY]" >&2
    exit 1
fi
if ! command -v zip >/dev/null; then
    echo "zip is required to create the mod package." >&2
    exit 1
fi

mkdir -p "$output_root"
output_root=$(cd "$output_root" && pwd)
mod_dir="$output_root/NoMoreDispenserLoops"
dotnet build "$root/src/NoMoreDispenserLoops/NoMoreDispenserLoops.csproj" -c Release \
    -p:ModOutputDir="$mod_dir"

archive="$output_root/NoMoreDispenserLoops"
if [[ -n "$version" ]]; then
    sed "s/^version: .*/version: $version/" "$mod_dir/mod_info.yaml" > "$mod_dir/mod_info.yaml.tmp"
    mv "$mod_dir/mod_info.yaml.tmp" "$mod_dir/mod_info.yaml"
    grep -Fx "version: $version" "$mod_dir/mod_info.yaml"
    archive="$archive-$version"
fi
archive="$archive.zip"

temporary_archive=$(mktemp "$output_root/.NoMoreDispenserLoops.XXXXXXXX.zip")
trap 'rm -f -- "$temporary_archive"' EXIT
rm -f -- "$temporary_archive"
(
    cd "$output_root"
    zip "$temporary_archive" \
        NoMoreDispenserLoops/NoMoreDispenserLoops.dll \
        NoMoreDispenserLoops/mod.yaml \
        NoMoreDispenserLoops/mod_info.yaml \
        NoMoreDispenserLoops/README.md
)
mv -f -- "$temporary_archive" "$archive"
echo "Package created: $archive"
