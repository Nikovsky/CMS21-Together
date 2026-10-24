#!/usr/bin/env python3
"""SAVE-001A: read-only offline CMS21 Together save format auditor.

No game/Unity imports; never writes, renames, loads in-game, or migrates saves.
"""

import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path

MAGIC = b"CMS21-TOGETHER"
MAX_JSON_BYTES = 8 * 1024 * 1024
LEGACY_RE = re.compile(r"^save_(\d+)\.cms21mp$")
PROFILE_RE = re.compile(r"^profile(\d+)\.(cms21b|cms21)$")
LEGACY_FIELDS = (
    "Name", "saveIndex", "selectedGamemode", "alreadyLoaded",
    "playerInfos", "inventoryItemUID", "storyMissionInProgress",
    "missionFinished", "money", "additionnalStand",
)
EXTENSION_FIELDS = (
    "MagicWord", "ModVersion", "Name", "SelectedGamemode",
    "PlayerInfos", "AdditionnalStand",
)


class DuplicateKeyError(ValueError):
    pass


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateKeyError("duplicate JSON key")
        result[key] = value
    return result


def _decode_json(data):
    # Avoid Python accepting JSON NaN and infinities which aren't valid JSON.
    def bad_constant(_):
        raise ValueError("non-JSON numeric constant")

    return json.loads(
        data.decode("utf-8-sig"),
        object_pairs_hook=_unique_object,
        parse_constant=bad_constant,
    )


def _digest(stream):
    h = hashlib.sha256()
    size = 0
    while True:
        chunk = stream.read(1024 * 1024)
        if not chunk:
            break
        size += len(chunk)
        h.update(chunk)
    return h.hexdigest(), size


def _read_file_metadata(path):
    # A file is opened only for reading. Never follow a symlink intentionally.
    if path.is_symlink():
        return None, "symlink_skipped"
    if not path.is_file():
        return None, "not_regular_file"
    try:
        with path.open("rb") as stream:
            sha, size = _digest(stream)
        return {"sha256": sha, "size_bytes": size}, None
    except OSError:
        return None, "unreadable"


def _schema_fields(obj, known):
    return [key for key in known if key in obj]


def _valid_gamemode(value):
    # Newtonsoft.Json can represent enum values as integers or strings.
    return (type(value) is int or isinstance(value, str))


def inspect_legacy(path, slot):
    item = {"kind": "legacy_metadata", "status": "unreadable"}
    metadata, problem = _read_file_metadata(path)
    if problem:
        item["status"] = problem
        return item
    item.update(metadata)
    if metadata["size_bytes"] > MAX_JSON_BYTES:
        item["status"] = "json_too_large"
        return item
    try:
        with path.open("rb") as stream:
            obj = _decode_json(stream.read())
    except (OSError, UnicodeError, ValueError, RecursionError):
        item["status"] = "invalid_json"
        return item

    if not isinstance(obj, dict):
        item["status"] = "invalid_schema"
        return item
    item["known_fields_present"] = _schema_fields(obj, LEGACY_FIELDS)
    if (type(obj.get("saveIndex")) is not int or
            not isinstance(obj.get("Name"), str) or
            not _valid_gamemode(obj.get("selectedGamemode")) or
            ("playerInfos" in obj and not isinstance(obj["playerInfos"], list)) or
            ("alreadyLoaded" in obj and type(obj["alreadyLoaded"]) is not bool)):
        item["status"] = "invalid_schema"
    elif obj["saveIndex"] != slot:
        item["status"] = "slot_mismatch"
    else:
        item["status"] = "legacy_json_valid"
    return item


def inspect_profile(path):
    item = {"kind": "game_profile", "status": "unreadable"}
    metadata, problem = _read_file_metadata(path)
    if problem:
        item["status"] = problem
        return item
    item.update(metadata)
    length = metadata["size_bytes"]
    if length == 0:
        item["status"] = "empty_profile"
        return item
    if length < len(MAGIC) + 4:
        item["status"] = "no_extension_footer"
        return item
    try:
        with path.open("rb") as stream:
            stream.seek(-(len(MAGIC) + 4), 2)
            suffix = stream.read(len(MAGIC) + 4)
            magic, raw_length = suffix[:-4], suffix[-4:]
            if magic != MAGIC:
                item["status"] = "no_extension_footer"
                return item
            payload_length = struct.unpack("<i", raw_length)[0]
            body_length = length - len(MAGIC) - 4 - payload_length
            if payload_length <= 0 or body_length <= 0:
                item["status"] = "invalid_extension_length"
                return item
            if payload_length > MAX_JSON_BYTES:
                item["status"] = "extension_json_too_large"
                return item
            stream.seek(body_length)
            payload = stream.read(payload_length)
            if len(payload) != payload_length:
                item["status"] = "invalid_extension_length"
                return item
        obj = _decode_json(payload)
    except OSError:
        item["status"] = "unreadable"
        return item
    except (UnicodeError, ValueError, RecursionError):
        item["status"] = "invalid_extension_json"
        return item

    if not isinstance(obj, dict):
        item["status"] = "invalid_extension_schema"
        return item
    item["known_fields_present"] = _schema_fields(obj, EXTENSION_FIELDS)
    if (obj.get("MagicWord") != MAGIC.decode("ascii") or
            type(obj.get("ModVersion")) is not int or
            not isinstance(obj.get("Name"), str) or
            not _valid_gamemode(obj.get("SelectedGamemode")) or
            ("PlayerInfos" in obj and not isinstance(obj["PlayerInfos"], list))):
        item["status"] = "invalid_extension_schema"
    elif obj["ModVersion"] != 1:
        item["status"] = "unsupported_extension_version"
    else:
        item["status"] = "extension_v1"
    return item


def _files_in(directory, regex):
    if directory is None:
        return {}
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError("input directory must be an existing non-symlink directory")
    result = {}
    try:
        for path in directory.iterdir():
            match = regex.fullmatch(path.name)
            if match:
                slot = int(match.group(1))
                result.setdefault(slot, []).append(path)
    except OSError as exc:
        raise ValueError("cannot enumerate input directory") from exc
    return result


def audit(legacy_dir=None, game_save_dir=None):
    """Return sanitized report; doesn't open files for writing."""
    if legacy_dir is None and game_save_dir is None:
        raise ValueError("at least one input directory is required")
    legacy_files = _files_in(Path(legacy_dir) if legacy_dir is not None else None, LEGACY_RE)
    profile_files = _files_in(Path(game_save_dir) if game_save_dir is not None else None, PROFILE_RE)
    slots = []
    statuses = {}
    for slot in sorted(set(legacy_files) | set(profile_files)):
        old = [inspect_legacy(path, slot) for path in sorted(legacy_files.get(slot, []))]
        profiles = []
        for path in sorted(profile_files.get(slot, [])):
            inspected = inspect_profile(path)
            inspected["extension"] = path.suffix.lstrip(".")
            profiles.append(inspected)

        if len(old) > 1 or len(profiles) > 1:
            status = "ambiguous_multiple_files"
        elif old and old[0]["status"] != "legacy_json_valid":
            status = "legacy_metadata_problem"
        elif old and profiles and profiles[0]["status"] == "no_extension_footer":
            status = "legacy_pair_candidate"
        elif old and profiles:
            status = "metadata_profile_conflict"
        elif old:
            status = "legacy_without_profile"
        elif profiles and profiles[0]["status"] == "extension_v1":
            status = "extension_v1"
        elif profiles and profiles[0]["status"] == "no_extension_footer":
            status = "profile_without_metadata"
        else:
            status = "profile_needs_review"
        slots.append({"slot": slot, "status": status, "legacy": old, "profiles": profiles})
        statuses[status] = statuses.get(status, 0) + 1
    return {
        "audit_version": 1,
        "mode": "read_only",
        "caution": "Formats only; binary game profile validity is not verified. No migration performed.",
        "summary": {"slots_found": len(slots), "by_status": dict(sorted(statuses.items()))},
        "slots": slots,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--legacy-dir", type=Path, help="directory containing save_N.cms21mp")
    parser.add_argument("--game-save-dir", type=Path, help="directory containing profileN.cms21(b)")
    args = parser.parse_args(argv)
    try:
        result = audit(args.legacy_dir, args.game_save_dir)
    except ValueError as exc:
        parser.error(str(exc))
    json.dump(result, sys.stdout, ensure_ascii=True, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
