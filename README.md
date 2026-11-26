# No More Dispenser Loops

Prevent duplicants from repeatedly picking up a dispenser's dropped debris to
refill Automatic Dispensers. Other errands and Auto-Sweepers are unaffected.

## Install and use

1. Copy the entire `dist/NoMoreDispenserLoops/` folder to Klei's `mods/Local/`
   folder (create `Local` if needed). The DLL, `mod.yaml`, and `mod_info.yaml`
   must be directly inside `Local/NoMoreDispenserLoops/`.
2. Enable the mod in the game's Mods menu and restart.
3. Select a dispenser and enable **Exclude Drop Tile** (off by default). The
   protected landing cell is highlighted while the dispenser is selected.

Optionally enable **Allow Higher-Priority Dispensers** on a source dispenser.
The game's mod options can disable this exception globally, hiding its
per-dispenser checkbox.
The mod is built against game build 764025 but has not been tested in-game;
back up your save.
When upgrading from an earlier build, remove the old mod folder, re-enable the
mod, and check its settings; the new ID and component name may reset them.

## Build and test

The Dev Container includes .NET 8, Git, GitHub CLI, SSH, `zip`, and `unzip`.
Rebuild an existing container after changes to its configuration. Outside the
container, install the tools you need yourself.

Run format checks and tests locally with:

```bash
bash scripts/check.sh
```

Building requires your ONI `OxygenNotIncluded_Data/Managed` DLLs: copy them
into `lib/`, or set `ONI_MANAGED_DIR` to that directory. They are ignored by
Git and are not needed to install a built mod. Build the package with:

```bash
bash scripts/package.sh
```

The script creates `dist/NoMoreDispenserLoops/` and `dist/NoMoreDispenserLoops.zip`.
Both are ignored by Git. Tests use game stubs and do not verify in-game behavior.

## Releases

Pushes to `main` update a draft release. The **Dependabot Auto-merge** workflow
can also be run manually to check open Dependabot PRs; it merges only PRs with
successful checks and refreshes the draft notes after a merge. This does not
start a Dependabot scan.

To publish, first check out the `main` commit to be released and build locally
with the game DLLs in `lib/` or `ONI_MANAGED_DIR`. Install the GitHub CLI and
authenticate with `gh auth login`, and make sure the **Unreleased** draft
exists. With a clean `main` checkout matching the pushed GitHub branch, run:

```bash
bash scripts/release.sh
```

The script asks for a `patch`, `minor`, or `major` version bump and detects the
repository from the `origin` remote. It generates release notes in `dist/`,
runs tests, builds the versioned ZIP locally, and publishes the draft. Game
DLLs are not included in the release ZIP.

Alternatively, upload a locally built `dist/NoMoreDispenserLoops.zip` to the
draft and run the **Release** workflow on `main` with `publish`. The
GitHub-hosted runner cannot verify that the uploaded binary matches the commit.

### SSH in the Dev Container

VS Code forwards a running host SSH agent into the container. Start the agent
and add your GitHub key on the host before opening the container, then use
`ssh-add -l` in the container to check that the key is available. The container
also maps the `github.com-private` SSH alias to `github.com`; the private key
stays in your host agent. Verify the connection with
`ssh -T git@github.com-private` (GitHub returns exit code 1 on successful
authentication). Only forward your agent to containers you trust.
