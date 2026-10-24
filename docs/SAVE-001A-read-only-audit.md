# SAVE-001A — Read-only offline save audit

This standalone Python 3.8+ standard-library diagnostic does not load CMS21 or mod hooks, and does not write, delete, move, migrate, or alter any save.

## Usage

Examine offline copies. Run from repository root (PowerShell):

    python -B tools/save_audit.py --legacy-dir "C:\test-copies\Mods\togetherMod\saves" --game-save-dir "C:\test-copies\Save"

Either directory may be omitted, but at least one is required. The JSON report prints slot numbers, recognized schema field names (not values), byte counts, SHA-256 checksums and classification. It does not print player names, GUIDs or paths. Checksums are file fingerprints; do not publish reports without review.

Synthetic test command:

    $env:PYTHONDONTWRITEBYTECODE="1"
    python -B -m unittest discover -s tools/tests -v

## Supported layouts

- Legacy 0.4.16hf3: JSON save_N.cms21mp (ModSaveData), paired by index with game-owned profileN.cms21 or profileN.cms21b.
- 0.4.17: game profile with UTF-8 JSON (ModProfileExtension) appended, followed by ASCII marker CMS21-TOGETHER and little-endian signed int32 JSON byte count; extension version 1 is recognized.

The audit DOES NOT decode the game-owned binary prefix. A legacy_pair_candidate is only a filename and basic metadata candidate; it is not evidence of a valid, playable or recovered save. extension_v1 likewise does not validate vanilla binary content.

Statuses:
- legacy_pair_candidate: old metadata plus profile without new extension footer;
- extension_v1: recognized extension version 1;
- metadata_profile_conflict: old metadata and profile with other/incompatible footer;
- ambiguous_multiple_files: two game profile variants for one slot;
- legacy_without_profile: old metadata without matching game profile;
- profile_without_metadata: game profile without recognized mod metadata;
- legacy_metadata_problem / profile_needs_review: unknown, malformed or unreadable data.

A missing extension is never permission to overwrite a slot. Old money, mission, and UID metadata have no direct field mapping in ModProfileExtension; migration must be designed separately.

## Verification and limitations

Synthetic tests verify detection of both layouts, index mismatch, conflicting files, invalid JSON, invalid footer length, unsupported version, report redaction and unchanged SHA-256 digests. Fake binary prefixes are used, so no real CMS21 saves have yet been validated. Mod Release build and two-PC Steam tests have NOT been run. No C# code, hooks, csproj or runtime behavior has been changed.

Preserve original saves and independent backups. Do not scan or test production saves inside the game. No automatic migration or deletion has been added.
