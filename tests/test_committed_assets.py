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
                self.assertTrue(all(volume["gutenbergId"] == work["id"] for volume in volumes))

                expected_titles = (
                    [work["title"]]
                    if volume_count == 1
                    else [f'{work["title"]} (Vol. {number})' for number in range(1, volume_count + 1)]
                )
                self.assertEqual([volume["title"] for volume in volumes], expected_titles)


if __name__ == "__main__":
    unittest.main()
