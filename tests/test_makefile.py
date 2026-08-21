"""Regression tests for offline normal build and packaging targets."""

from __future__ import annotations

import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MISSING_STAMP = ROOT / ".test-missing-gutenberg-stamp"


def dry_run(target: str) -> str:
    """Render a Make target as if the Gutenberg cache stamp did not exist."""
    return subprocess.check_output(
        ["make", "--dry-run", target, f"DOWNLOAD_STAMP={MISSING_STAMP}"],
        cwd=ROOT,
        text=True,
        stderr=subprocess.STDOUT,
    )


class OfflineBuildTargetsTests(unittest.TestCase):
    def test_normal_build_targets_do_not_download_or_regenerate_books(self):
        self.assertFalse(MISSING_STAMP.exists())

        for target in ("build", "package", "install"):
            with self.subTest(target=target):
                commands = dry_run(target)
                self.assertNotIn("download_texts.py", commands)
                self.assertNotIn("build_lore_assets.py", commands)

    def test_explicit_assets_target_still_runs_the_catalog_pipeline(self):
        self.assertFalse(MISSING_STAMP.exists())
        commands = dry_run("assets")

        self.assertIn("download_texts.py", commands)
        self.assertIn("build_lore_assets.py", commands)


if __name__ == "__main__":
    unittest.main()
