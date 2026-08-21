"""Regression tests for the local ModDB/TinyMCE preview workflow."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from moddb_preview import PAGE, SOURCE, description  # noqa: E402


class ModdbPreviewTests(unittest.TestCase):
    def test_paste_fragment_omits_the_maintenance_comment(self):
        body = description(SOURCE.read_text(encoding="utf-8"))

        self.assertTrue(body.lstrip().startswith('<div style='))
        self.assertNotIn("make moddb-preview", body)

    def test_source_uses_moddb_cdn_for_every_image(self):
        body = description(SOURCE.read_text(encoding="utf-8"))

        self.assertEqual(body.count("https://moddbcdn.vintagestory.at/"), 4)
        self.assertNotIn("<!-- image:", body)

    def test_every_cdn_image_has_alt_text_and_responsive_sizing(self):
        body = description(SOURCE.read_text(encoding="utf-8"))

        self.assertEqual(body.count('<img src="https://moddbcdn.vintagestory.at/'), 4)
        self.assertEqual(body.count('style="max-width: 100%; height: auto;"'), 4)
        self.assertEqual(body.count(' alt="'), 4)

    def test_preview_wraps_the_same_cdn_images(self):
        body = description(SOURCE.read_text(encoding="utf-8"))
        preview = PAGE.format(body=body)

        self.assertEqual(preview.count("https://moddbcdn.vintagestory.at/"), 4)


if __name__ == "__main__":
    unittest.main()
