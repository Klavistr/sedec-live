import json
import tempfile
import unittest
from pathlib import Path

from scripts.check_repo import (
    LARGE_FILE_BYTES,
    REQUIRED_DIRECTORIES,
    check_json_files,
    check_large_files,
    check_overlay_entrypoints,
    check_required_directories,
)


class RepositoryChecksTest(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)

    def tearDown(self):
        self.temporary_directory.cleanup()

    def test_required_directories_reports_only_missing_paths(self):
        for relative_path in REQUIRED_DIRECTORIES[:-1]:
            (self.root / relative_path).mkdir(parents=True, exist_ok=True)

        findings = check_required_directories(self.root)

        self.assertEqual(len(findings), 1)
        self.assertIn(REQUIRED_DIRECTORIES[-1], findings[0].message)

    def test_json_check_accepts_utf8_bom(self):
        json_path = self.root / "obs" / "scene-collections" / "valid.json"
        json_path.parent.mkdir(parents=True)
        json_path.write_text(json.dumps({"name": "SEDEC"}), encoding="utf-8-sig")

        self.assertEqual(check_json_files(self.root), [])

    def test_json_check_reports_invalid_file(self):
        json_path = self.root / "obs" / "scene-collections" / "invalid.json"
        json_path.parent.mkdir(parents=True)
        json_path.write_text("{", encoding="utf-8")

        findings = check_json_files(self.root)

        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].level, "ERROR")

    def test_overlay_check_requires_index_html(self):
        overlay = self.root / "assets" / "overlays" / "lower-third"
        overlay.mkdir(parents=True)

        findings = check_overlay_entrypoints(self.root)

        self.assertEqual(len(findings), 1)
        (overlay / "index.html").touch()
        self.assertEqual(check_overlay_entrypoints(self.root), [])

    def test_large_file_check_warns_over_limit(self):
        large_file = self.root / "assets" / "video" / "large.mp4"
        large_file.parent.mkdir(parents=True)
        with large_file.open("wb") as file:
            file.truncate(LARGE_FILE_BYTES + 1)

        findings = check_large_files(self.root)

        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0].level, "WARNING")


if __name__ == "__main__":
    unittest.main()

