"""Synthetic-only regression tests for SAVE-001A."""
import hashlib
import json
import struct
import tempfile
import unittest
from pathlib import Path
from tools.save_audit import MAGIC, audit


def old(slot=4, **fields):
    data = {"Name": "Synthetic", "saveIndex": slot, "selectedGamemode": 3,
            "alreadyLoaded": True, "playerInfos": [], "money": 500}
    data.update(fields)
    return json.dumps(data).encode()


def game(ext=None):
    base = b"SYNTHETIC-NOT-A-REAL-GAME-SAVE"
    if ext is None:
        return base
    payload = ext if isinstance(ext, bytes) else json.dumps(ext).encode()
    return base + payload + MAGIC + struct.pack("<i", len(payload))


def v1(**fields):
    data = {"MagicWord": MAGIC.decode(), "ModVersion": 1, "Name": "Synthetic",
            "SelectedGamemode": 3, "PlayerInfos": []}
    data.update(fields)
    return data


class AuditTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        self.legacy = self.root / "legacy"
        self.profiles = self.root / "profiles"
        self.legacy.mkdir()
        self.profiles.mkdir()

    def put_old(self, slot=4, data=None):
        path = self.legacy / f"save_{slot}.cms21mp"
        path.write_bytes(old(slot) if data is None else data)
        return path

    def put_game(self, slot=4, data=None, ext="cms21b"):
        path = self.profiles / f"profile{slot}.{ext}"
        path.write_bytes(game() if data is None else data)
        return path

    def row(self):
        return audit(self.legacy, self.profiles)["slots"][0]

    def test_legacy_pair(self):
        self.put_old()
        self.put_game()
        self.assertEqual(self.row()["status"], "legacy_pair_candidate")

    def test_extension_v1(self):
        self.put_game(data=game(v1()))
        self.assertEqual(self.row()["status"], "extension_v1")

    def test_mismatched_slot(self):
        self.put_old(data=old(5))
        self.assertEqual(self.row()["legacy"][0]["status"], "slot_mismatch")

    def test_legacy_and_extension_conflict(self):
        self.put_old()
        self.put_game(data=game(v1()))
        self.assertEqual(self.row()["status"], "metadata_profile_conflict")

    def test_two_profile_variants(self):
        self.put_game(ext="cms21")
        self.put_game(ext="cms21b")
        self.assertEqual(self.row()["status"], "ambiguous_multiple_files")

    def test_duplicate_json_fields(self):
        self.put_old(data=b'{"saveIndex":4,"saveIndex":4}')
        self.assertEqual(self.row()["legacy"][0]["status"], "invalid_json")

    def test_legacy_without_profile(self):
        self.put_old()
        self.assertEqual(self.row()["status"], "legacy_without_profile")

    def test_bad_length(self):
        self.put_game(data=b"PREFIX" + MAGIC + struct.pack("<i", 10000))
        self.assertEqual(self.row()["profiles"][0]["status"], "invalid_extension_length")

    def test_corrupt_extension(self):
        self.put_game(data=game(b"{broken"))
        self.assertEqual(self.row()["profiles"][0]["status"], "invalid_extension_json")

    def test_unknown_version(self):
        self.put_game(data=game(v1(ModVersion=2)))
        self.assertEqual(self.row()["profiles"][0]["status"], "unsupported_extension_version")

    def test_no_player_names_or_paths_in_report(self):
        secret = "PRIVATE-SYNTHETIC-NAME"
        self.put_old(data=old(Name=secret))
        self.put_game()
        report = json.dumps(audit(self.legacy, self.profiles))
        self.assertNotIn(secret, report)
        self.assertNotIn(str(self.root), report)

    def test_original_hashes_unchanged(self):
        paths = [self.put_old(), self.put_game(data=game(v1()))]
        before = [hashlib.sha256(p.read_bytes()).hexdigest() for p in paths]
        self.row()
        after = [hashlib.sha256(p.read_bytes()).hexdigest() for p in paths]
        self.assertEqual(before, after)

    def test_empty_directory(self):
        self.assertEqual(audit(self.legacy, self.profiles)["summary"]["slots_found"], 0)

    def test_requires_input(self):
        with self.assertRaises(ValueError):
            audit()


if __name__ == "__main__":
    unittest.main()
