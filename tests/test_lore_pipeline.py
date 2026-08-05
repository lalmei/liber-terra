"""Unit tests for the lore text pipeline.

These cover the pure transforms in tools/build_lore_assets.py — no network,
no Gutenberg cache, no Vintage Story install. The chunking functions are
where a silent regression would corrupt every book, so most of these assert
that content survives the trip rather than just checking sizes.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from build_lore_assets import (  # noqa: E402
    NEWPAGE,
    escape_lang,
    pack_pages,
    pack_pieces,
    paragraphs,
    split_volumes,
    strip_gutenberg,
    volume_preface,
)


class StripGutenbergTests(unittest.TestCase):
    def test_keeps_only_the_text_between_markers(self):
        raw = (
            "Title page junk\n"
            "*** START OF THE PROJECT GUTENBERG EBOOK BEOWULF ***\n\n"
            "HWAET! We Gardena in geardagum.\n\n"
            "*** END OF THE PROJECT GUTENBERG EBOOK BEOWULF ***\n"
            "Licence footer junk\n"
        )
        body = strip_gutenberg(raw)
        self.assertEqual(body, "HWAET! We Gardena in geardagum.")

    def test_handles_the_plain_end_footer_variant(self):
        raw = (
            "*** START OF THE PROJECT GUTENBERG EBOOK X ***\n\n"
            "Real text here.\n\n"
            "End of the Project Gutenberg EBook of X\n\n"
            "trailing licence blurb\n"
        )
        self.assertEqual(strip_gutenberg(raw), "Real text here.")

    def test_drops_leading_boilerplate_paragraphs(self):
        raw = (
            "*** START OF THE PROJECT GUTENBERG EBOOK X ***\n\n"
            "This ebook is for the use of anyone anywhere, from Project Gutenberg.\n\n"
            "Produced by volunteers at https://www.pgdp.net\n\n"
            "THE REAL OPENING LINE.\n\n"
            "*** END OF THE PROJECT GUTENBERG EBOOK X ***\n"
        )
        self.assertEqual(strip_gutenberg(raw), "THE REAL OPENING LINE.")

    def test_normalises_crlf_and_collapses_blank_runs(self):
        self.assertEqual(strip_gutenberg("Alpha\r\n\r\n\r\n\r\nBeta"), "Alpha\n\nBeta")

    def test_passes_through_text_without_markers(self):
        self.assertEqual(strip_gutenberg("Just a plain text."), "Just a plain text.")


class ParagraphsTests(unittest.TestCase):
    def test_splits_on_blank_lines(self):
        self.assertEqual(paragraphs("one\n\ntwo\n\nthree"), ["one", "two", "three"])

    def test_never_returns_empty(self):
        self.assertEqual(paragraphs("   "), [""])


class PackPagesTests(unittest.TestCase):
    def test_preserves_every_paragraph_in_order(self):
        paras = [f"para-{i} " + "x" * 100 for i in range(20)]
        rejoined = "\n\n".join(pack_pages(paras, page_target=250))
        self.assertEqual(rejoined.split("\n\n"), paras)

    def test_respects_the_page_target_for_multi_paragraph_pages(self):
        paras = ["x" * 100 for _ in range(20)]
        for page in pack_pages(paras, page_target=250):
            if "\n\n" in page:
                self.assertLessEqual(len(page), 250)

    def test_oversized_paragraph_gets_its_own_page(self):
        pages = pack_pages(["y" * 5000, "short"], page_target=100)
        self.assertEqual(pages, ["y" * 5000, "short"])


class PackPiecesTests(unittest.TestCase):
    def test_joins_pages_with_the_newpage_marker(self):
        pieces = pack_pieces(["A" * 100, "B" * 100, "C" * 100], piece_target=250)
        self.assertIn(NEWPAGE, pieces[0])
        self.assertEqual(pieces[-1], "C" * 100)

    def test_preserves_page_content(self):
        pages = [f"page-{i} " + "z" * 200 for i in range(12)]
        recovered = []
        for piece in pack_pieces(pages, piece_target=700):
            recovered.extend(piece.split(f"\n\n{NEWPAGE}\n\n"))
        self.assertEqual(recovered, pages)


class SplitVolumesTests(unittest.TestCase):
    def test_splits_when_over_max_chars(self):
        pieces = ["x" * 400, "y" * 400, "z" * 400]
        self.assertEqual(
            split_volumes(pieces, max_chars=900),
            [["x" * 400, "y" * 400], ["z" * 400]],
        )

    def test_never_drops_pieces(self):
        pieces = [f"piece-{i}" + "q" * 300 for i in range(25)]
        volumes = split_volumes(pieces, max_chars=1000)
        self.assertEqual([p for vol in volumes for p in vol], pieces)

    def test_single_oversized_piece_still_lands_in_a_volume(self):
        self.assertEqual(split_volumes(["w" * 90_000], max_chars=70_000), [["w" * 90_000]])


class LangAndPrefaceTests(unittest.TestCase):
    def test_preface_omits_volume_number_for_single_volume_works(self):
        self.assertEqual(volume_preface({"title": "Beowulf"}, 1, 1), "Beowulf.")

    def test_preface_numbers_multi_volume_works(self):
        self.assertEqual(volume_preface({"title": "Iliad"}, 2, 16), "Iliad. Volume 2 of 16.")

    def test_escape_lang_uses_crlf(self):
        self.assertEqual(escape_lang("a\nb"), "a\r\nb")


if __name__ == "__main__":
    unittest.main()
