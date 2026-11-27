#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

if [[ $# -gt 3 ]]; then
    echo "Usage: scripts/package.sh [MOD_NAME [VERSION [OUTPUT_DIRECTORY]]]" >&2
    exit 1
fi

mod_name=${1:-}
version=${2:-}
output_root=${3:-"$root/dist"}

if [[ -n "$version" && ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Version must use MAJOR.MINOR.PATCH format." >&2
    exit 1
fi
if [[ "$mod_name" == "--all" && $# -ne 1 ]]; then
    echo "Usage: scripts/package.sh [MOD_NAME [VERSION [OUTPUT_DIRECTORY]]]" >&2
    exit 1
fi

shopt -s nullglob
projects=("$root"/*/*.csproj)
if [[ ${#projects[@]} -eq 0 ]]; then
    echo "No mod projects found in top-level mod folders." >&2
    exit 1
fi

mods=()
for project in "${projects[@]}"; do
    project_dir=$(dirname "$project")
    project_name=$(basename "$project" .csproj)
    if [[ "$(basename "$project_dir")" != "$project_name" ]]; then
        echo "Mod directory and project must have the same name: $project" >&2
        exit 1
    fi
    mods+=("$project_name")
done

if [[ -n "$mod_name" && "$mod_name" != "--all" ]]; then
    if [[ ! "$mod_name" =~ ^[A-Za-z0-9][A-Za-z0-9_-]*$ ]]; then
        echo "Invalid mod name '$mod_name'." >&2
        exit 1
    fi
    found=false
    for candidate in "${mods[@]}"; do
        if [[ "$candidate" == "$mod_name" ]]; then
            found=true
            break
        fi
    done
    if [[ "$found" != true ]]; then
        echo "Unknown mod '$mod_name'. Available mods: ${mods[*]}" >&2
        exit 1
    fi
    mods=("$mod_name")
fi

if ! command -v zip >/dev/null; then
    echo "zip is required to create mod packages." >&2
    exit 1
fi

mkdir -p "$output_root"
output_root=$(cd "$output_root" && pwd)

for mod in "${mods[@]}"; do
    source_dir="$root/$mod"
    project="$source_dir/$mod.csproj"
    mod_dir="$output_root/$mod"
    for file in mod.yaml mod_info.yaml README.md; do
        if [[ ! -s "$source_dir/$file" ]]; then
            echo "Mod '$mod' is missing $source_dir/$file." >&2
            exit 1
        fi
    done

    dotnet build "$project" -c Release -p:ModOutputDir="$mod_dir"
    if [[ ! -s "$mod_dir/$mod.dll" || ! -s "$mod_dir/oni-build.txt" ]]; then
        echo "Build did not create the expected output for '$mod'." >&2
        exit 1
    fi

    if [[ -n "$version" ]]; then
        sed "s/^version: .*/version: $version/" "$mod_dir/mod_info.yaml" > "$mod_dir/mod_info.yaml.tmp"
        mv "$mod_dir/mod_info.yaml.tmp" "$mod_dir/mod_info.yaml"
        grep -Fx "version: $version" "$mod_dir/mod_info.yaml"
    fi

    archive="$output_root/$mod"
    if [[ -n "$version" ]]; then
        archive="$archive-$version"
    fi
    archive="$archive.zip"

    temporary_archive=$(mktemp "$output_root/.$mod.XXXXXXXX.zip")
    trap 'rm -f -- "$temporary_archive"' EXIT
    rm -f -- "$temporary_archive"
    (
        cd "$output_root"
        zip -qr "$temporary_archive" "$mod" -x "$mod/oni-build.txt" "$mod/README.md"
    )
    mv -f -- "$temporary_archive" "$archive"
    trap - EXIT
    echo "Package created: $archive"
done
