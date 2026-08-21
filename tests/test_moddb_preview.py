"""Regression tests for the local ModDB/TinyMCE preview workflow."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from moddb_preview import IMAGE_MARKER, SOURCE, description, resolve_images  # noqa: E402


class ModdbPreviewTests(unittest.TestCase):
    def test_paste_fragment_omits_the_maintenance_comment(self):
        body = description(SOURCE.read_text(encoding="utf-8"))

        self.assertTrue(body.lstrip().startswith('<div style='))
        self.assertNotIn("make moddb-preview", body)

    def test_every_screenshot_marker_resolves_including_hyphenated_names(self):
        body = description(SOURCE.read_text(encoding="utf-8"))
        marker_count = len(IMAGE_MARKER.findall(body))
        preview = resolve_images(body, ROOT / "dist")

        self.assertEqual(marker_count, 4)
        self.assertEqual(preview.count("<figure><img"), marker_count)
        self.assertNotIn("missing image:", preview)
        self.assertIn("../docs/screenshots/i-can-carry-books.png", preview)


if __name__ == "__main__":
    unittest.main()
