# No More Dispenser Loops

Prevent duplicants from repeatedly picking up a dispenser's dropped debris to
refill Automatic Dispensers. Other errands and Auto-Sweepers are unaffected.

## Install and use

1. Copy the entire `dist/NoMoreDispenserLoops/` folder to Klei's `mods/Local/`
   folder (create `Local` if needed). The DLL, `mod.yaml`, and `mod_info.yaml`
   must be directly inside `Local/NoMoreDispenserLoops/`.
2. Enable the mod in the game's Mods menu and restart.
3. Select a dispenser, expand **No More Dispenser Loops** at the bottom of its
   settings, and enable **Exclude Drop Tile** (off by default). The
   protected landing cell is highlighted while the dispenser is selected.
   Items selected in its filter are protected from dispenser deliveries.
   Items not selected can be collected by other dispensers, provided their
   filters accept them. Ordinary storage bins can collect items regardless of
   this protection. If protected cells overlap, each dispenser's filter and
   priority exception applies.
   When a bin haul includes a protected pile, conflicting dispenser deliveries
   are left out of that haul instead of preventing the bin from collecting it.

Optionally enable **Allow Higher-Priority Dispensers** on a source dispenser.
The game's mod options can disable this exception globally, hiding its
per-dispenser checkbox.
The mod is built against game build 764025 but has not been tested in-game;
back up your save.
When upgrading from an earlier build, remove the old mod folder, re-enable the
mod, and check its settings; the new ID and component name may reset them.

## Build and test

The Dev Container includes .NET 8, Git, GitHub CLI, SSH, zsh, `zip`, and
`unzip`. Its integrated terminal uses zsh by default. Rebuild an existing
container after changes to its configuration. Outside the container, install
the tools you need yourself.

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
