# SAVE-001B — Fail-closed save slot protection

Status: draft; **not compiled or tested in CMS21**.

## Scope

New `SaveSlotGuard` is read-only and checks indexes 4–21 in `GlobalStrings.SaveDirectory` for game-owned `profileN.cms21b` and `profileN.cms21`, and relative `Mods/togetherMod/saves/save_N.cms21mp` for legacy metadata.

It classifies an absent slot as `Empty`, a single profile with a recognizable v1 extension footer as `CurrentV1`, and **all other cases as `Protected`**. A `Protected` slot cannot be used in CreateNewSave, StartServer, LoadGame, StartGame, DeleteSave or the `GameDataManager.Save` patch for indexes >= 4. The UI displays `Protected save` instead of `New game`.

The guard does not migrate, change, rename, delete, or write files. When uncertain it blocks. It does not verify the vanilla binary prefix; even a `CurrentV1` result does not establish that the profile is playable.

## Known limitations requiring review BEFORE merge

- Path uses the same relative legacy metadata convention as old `SavesManager`; launcher working directory mismatches may prevent finding metadata. Verify path resolution on actual Windows/Steam installs.
- Game's `GlobalStrings.SaveDirectory` may be absent at startup; guard then protects all slots by design. Confirm startup ordering and fresh-save usability before release.
- `CurrentV1` is an extension format classification only, not full game-save validation. Unknown/new `ModVersion` is blocked.
- `GameDataManager.Save` is intercepted for indexes >=4 only; vanilla slots 0–3 remain unchanged.
- The temporary client slot 21 needs special two-PC review; guard protects it when an existing unknown file is present.
- This is an interim prevention mechanism. It does not fix upstream loss, the malformed spawn packet, or incomplete migration.

## Required runtime checks

1. Fresh install with empty save directory: create and reload a new multiplayer save.
2. Legacy metadata + existing profile: UI says protected, Create/Load/Start/Delete/Save do not modify either file.
3. Existing profile without extension: protected even if no legacy JSON.
4. Both .cms21 and .cms21b in a slot: protected.
5. Malformed marker, unsupported version, unreadable file, or legacy metadata alone: protected.
6. Valid 0.4.17 v1 profile: still loads and saves.
7. Steam host/client on two PCs: join/reconnect, client slot 21, save/reload, compare hashes of protected files.
8. Confirm logs do not expose player identifiers or save contents.

## Verification performed

Source-level inspection only. The previous standalone SAVE-001A Python audit had 14/14 synthetic tests passing locally. This new C# protection is **not compiled** (MSBuild/.NET Framework toolchain unavailable here), and its runtime protection has **not been validated**. Do not merge until compilation and disposable-save tests pass.

Rollback: revert this PR; no migration or on-disk transformation was included.
