"""Regression tests for the local ModDB/TinyMCE preview workflow."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from moddb_preview import (  # noqa: E402
    IMAGE_MARKER,
    SOURCE,
    description,
    load_image_map,
    resolve_images,
    resolve_remote_images,
)


class ModdbPreviewTests(unittest.TestCase):
    def test_paste_fragment_omits_the_maintenance_comment(self):
        body = description(SOURCE.read_text(encoding="utf-8"))

        self.assertTrue(body.lstrip().startswith('<div style='))
        self.assertNotIn("make moddb-preview", body)

    def test_paste_fragment_uses_moddb_cdn_for_every_image(self):
        body = description(SOURCE.read_text(encoding="utf-8"))
        rendered = resolve_remote_images(body)

        self.assertEqual(rendered.count("https://moddbcdn.vintagestory.at/"), 4)
        self.assertNotRegex(rendered, IMAGE_MARKER)

    def test_missing_production_image_fails_instead_of_shipping_a_marker(self):
        body = '<!-- image: docs/screenshots/not-uploaded.png — missing -->'

        with self.assertRaisesRegex(ValueError, "not-uploaded.png"):
            resolve_remote_images(body, load_image_map())

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
