"""Fast consistency checks for generated assets committed to the repository."""

from __future__ import annotations

import json
import sys
import unittest
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from mvp_works import MVP_WORKS  # noqa: E402


CATALOG = ROOT / "mod" / "assets" / "liberterra" / "config" / "liberterra-catalog.json"
LORE_DIR = ROOT / "mod" / "assets" / "liberterra" / "config" / "lore"
LANG = ROOT / "mod" / "assets" / "liberterra" / "lang" / "en.json"


class CommittedCatalogTests(unittest.TestCase):
    def test_catalog_matches_the_configured_work_manifest(self):
        catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
        volumes_by_work: dict[str, list[dict]] = defaultdict(list)
        for volume in catalog["works"]:
            volumes_by_work[volume["baseCode"]].append(volume)

        configured = {work["code"]: work for work in MVP_WORKS}
        self.assertEqual(set(volumes_by_work), set(configured))

        for code, work in configured.items():
            with self.subTest(code=code):
                volumes = sorted(volumes_by_work[code], key=lambda volume: volume["volume"])
                volume_count = len(volumes)
                self.assertGreater(volume_count, 0)
                self.assertEqual(
                    [volume["volume"] for volume in volumes],
                    list(range(1, volume_count + 1)),
                )
                self.assertTrue(
                    all(volume["volumeCount"] == volume_count for volume in volumes)
                )
                self.assertTrue(all(volume["group"] == work["group"] for volume in volumes))
                self.assertTrue(
                    all(volume["gutenbergId"] == (work.get("id") or 0) for volume in volumes)
                )
                expected_source = (
                    work["url"]
                    if work.get("url")
                    else f"https://www.gutenberg.org/ebooks/{work['id']}"
                )
                self.assertTrue(
                    all(volume.get("sourceUrl") == expected_source for volume in volumes)
                )

                expected_titles = (
                    [work["title"]]
                    if volume_count == 1
                    else [f'{work["title"]} (Vol. {number})' for number in range(1, volume_count + 1)]
                )
                self.assertEqual([volume["title"] for volume in volumes], expected_titles)


class CommittedLoreJournalTests(unittest.TestCase):
    """Vanilla journals a book only if config/lore JSON, lang keys, and catalog agree."""

    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
        cls.lang = json.loads(LANG.read_text(encoding="utf-8"))

    def test_every_volume_has_matching_lore_json_and_lang_keys(self):
        for work in self.catalog["works"]:
            code = work["code"]
            with self.subTest(code=code):
                lore_path = LORE_DIR / f"lore-{code}.json"
                self.assertTrue(lore_path.is_file(), lore_path)
                lore = json.loads(lore_path.read_text(encoding="utf-8"))
                self.assertEqual(lore["code"], code)
                self.assertEqual(lore["category"], code)
                self.assertEqual(lore["title"], f"liberterra:lore-{code}-title")
                self.assertEqual(len(lore["pieces"]), work["pieceCount"])
                self.assertEqual(lore["pieces"], work["textCodes"])
                self.assertIn(f"lore-{code}-title", self.lang)
                self.assertEqual(self.lang[f"lore-{code}-title"], work["title"])
                self.assertIn(f"game:ingamediscovery-lore-{code}", self.lang)
                self.assertIn(f"game:loretype-{code}", self.lang)
                for index, key in enumerate(lore["pieces"], start=1):
                    self.assertEqual(key, f"liberterra:lore-{code}-piece{index}")
                    self.assertIn(f"lore-{code}-piece{index}", self.lang)

    def test_republic_is_eighteen_volumes_named_like_the_other_works(self):
        volumes = [
            work
            for work in self.catalog["works"]
            if work["baseCode"] == "republic"
        ]
        self.assertEqual(len(volumes), 18)
        self.assertEqual(
            [work["title"] for work in volumes],
            [f"Plato: The Republic (Vol. {number})" for number in range(1, 19)],
        )
        self.assertTrue(all(work["pieceCount"] > 0 for work in volumes))


if __name__ == "__main__":
    unittest.main()
