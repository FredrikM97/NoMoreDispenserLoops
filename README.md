# Oxygen Not Included Mods

Each mod has its own top-level folder.

Releases publish all mods together with one shared version.
The release workflow delegates its draft, preparation, publishing, and
Workshop steps to scripts in `scripts/release/`. Published shared and legacy
per-mod releases can also trigger a Workshop upload; those runs use the
release's tagged files with the current workflow's scripts.

Run checks with `bash scripts/check.sh`; build packages with
`bash scripts/package.sh`.

Package builds use the ONI Managed assemblies in `lib/`, or the directory
specified by `ONI_MANAGED_DIR`.

The ONI library update workflow checks weekly (and can be run manually on
`main`). It downloads the latest Windows build and opens or updates a PR when
the assemblies change. CI validates and packages against the PR's libraries;
releases use the libraries already merged into `main` and do not download ONI.
Until the first library PR is merged, CI runs game-independent checks only.

The update workflow and Workshop publishing use `STEAM_USERNAME` and
`STEAM_CONFIG_VDF` from the `steam` GitHub environment. Create that
environment, move both secrets into it (and
remove any repository or organization copies), and configure deployment
protection before enabling workflows: allow only protected `main` and protected
release tags, and require trusted reviewers with self-review disabled. Protect
the branch and tag patterns against untrusted changes. Environment protection
is essential: a branch can otherwise change its own workflow to access secrets.
Environment approval may be needed for scheduled updates. Allow GitHub Actions
to create pull requests in repository settings; runs on its generated PRs may
also require approval. SteamCMD output and login files are removed after the
download, including on failure; credentials are not cached or included in PRs.
