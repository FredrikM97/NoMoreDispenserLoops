# Oxygen Not Included Mods

Each mod has its own top-level folder.

Releases publish all mods together with one shared version.
The release workflow delegates its draft, preparation, publishing, and
Workshop steps to scripts in `scripts/release/`. Published shared and legacy
per-mod releases can also trigger a Workshop upload; those runs use the
release's tagged files with the current workflow's scripts.
