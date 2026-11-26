# No More Dispenser Loops

Prevent duplicants from repeatedly picking up a dispenser's dropped debris to
refill Automatic Dispensers. Other errands and Auto-Sweepers are unaffected.

## Install and use

1. Copy the entire `dist/NoMoreDispenserLoops/` folder to Klei's `mods/Local/`
   folder (create `Local` if needed). The DLL, `mod.yaml`, and `mod_info.yaml`
   must be directly inside `Local/NoMoreDispenserLoops/`.
2. Enable the mod in the game's Mods menu and restart.
3. Select a dispenser, expand **No More Dispenser Loops**, and enable
   **Exclude Drop Tile** (off by default). The protected landing cell is
   highlighted while the dispenser is selected.

Optionally enable **Allow Higher-Priority Dispensers** on a source dispenser.
The game's mod options can disable this exception globally, hiding its
per-dispenser checkbox.
The mod is built against game build 764025 but has not been tested in-game;
back up your save.
When upgrading from an earlier build, remove the old mod folder, re-enable the
mod, and check its settings; the new ID and component name may reset them.

## Build and test

The Dev Container includes .NET 8 and `zip`; rebuild an existing container to
install `zip`. Outside the container, install both tools yourself.

Building requires your ONI `OxygenNotIncluded_Data/Managed` DLLs: copy them
into `lib/`, or set `ONI_MANAGED_DIR` to that directory. They are ignored by
Git and are not needed to install a built mod. Then run:

```bash
bash scripts/package.sh
dotnet test tests/NoMoreDispenserLoops.Tests/NoMoreDispenserLoops.Tests.csproj -c Release
```

The script creates `dist/NoMoreDispenserLoops/` and `dist/NoMoreDispenserLoops.zip`.
Both are ignored by Git. Tests use game stubs and do not verify in-game behavior.

## Releases

Pushes to `main` update a draft release. To publish, run the **Release**
workflow on `main` and choose `publish` and a version bump. Publishing requires
a trusted Linux self-hosted Actions runner with the `oni-managed` label, .NET 8,
`zip`, `gh`, and `ONI_MANAGED_DIR` set in the runner service environment. The
game DLLs stay on that runner and are not included in the release ZIP.
