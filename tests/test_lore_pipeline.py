"""Unit tests for the lore text pipeline.

These cover the pure transforms in tools/build_lore_assets.py — no network,
no Gutenberg cache, no Vintage Story install. The chunking functions are
where a silent regression would corrupt every book, so most of these assert
that content survives the trip rather than just checking sizes.
"""

import sys
import textwrap
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from build_lore_assets import (  # noqa: E402
    escape_lang,
    keeps_line_breaks,
    pack_pieces,
    paragraphs,
    reflow,
    split_volumes,
    strip_gutenberg,
    unwrap_paragraph,
    volume_preface,
    wrap_column,
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


PROSE = (
    "The sources from which Bede draws his material are briefly indicated in the "
    "dedication to King Ceolwulf which forms the Preface, and in it he acknowledges "
    "his obligations to the friends and correspondents who have helped him. For the "
    "greater part of Book I, which forms the introduction to his real subject, he "
    "depends on earlier authors, whom he indicates only in general terms."
)

VERSE = "\n".join(
    [
        "Forth he fared at the fated moment,",
        "sturdy Scyld to the shelter of God.",
        "Then they bore him over to ocean's billow,",
        "loving clansmen, as late he charged them,",
        "while wielded words the winsome Scyld,",
        "the leader beloved who long had ruled.",
    ]
)


class ReflowTests(unittest.TestCase):
    """The GUI wraps to its own width, so source hard wraps have to come out."""

    def test_hard_wrapped_prose_round_trips_back_to_one_line(self):
        body = "\n\n".join(textwrap.fill(PROSE, width=70) for _ in range(4))
        self.assertEqual(reflow(body), "\n\n".join([PROSE] * 4))

    def test_survives_a_narrower_source_margin(self):
        body = "\n\n".join(textwrap.fill(PROSE, width=62) for _ in range(4))
        self.assertEqual(reflow(body), "\n\n".join([PROSE] * 4))

    def test_short_lined_verse_keeps_every_break(self):
        self.assertEqual(reflow(VERSE), VERSE)

    def test_capitalised_line_starts_mark_verse_even_at_prose_width(self):
        # Long verse lines (Pope, Dryden) sit at the same width as wrapped prose;
        # the capital at the head of each line is what gives them away.
        stanza = "\n".join(
            f"Then spoke the hero, and his words were these, and thus he said {i:02d},"
            for i in range(30)
        )
        self.assertTrue(keeps_line_breaks(stanza))
        self.assertEqual(reflow(stanza), stanza)

    def test_work_flag_forces_line_breaks_to_survive(self):
        body = "\n\n".join(textwrap.fill(PROSE, width=70) for _ in range(4))
        self.assertNotEqual(reflow(body), body)
        self.assertEqual(reflow(body, {"keep_line_breaks": True}), body)

    def test_list_entries_keep_their_own_lines(self):
        # Each entry wraps once, so only half the breaks were forced by the margin.
        toc = (
            "Chap. I. Of the Situation of Britain and Ireland, and of their ancient\n"
            "inhabitants.\n"
            "Chap. II. How Caius Julius Caesar was the first Roman that came into\n"
            "Britain."
        )
        self.assertEqual(
            unwrap_paragraph(toc, 70).split("\n"),
            [
                "Chap. I. Of the Situation of Britain and Ireland, and of their ancient"
                " inhabitants.",
                "Chap. II. How Caius Julius Caesar was the first Roman that came into"
                " Britain.",
            ],
        )

    def test_wrap_column_ignores_a_single_long_outlier(self):
        text = "\n".join(["x" * 70] * 40 + ["y" * 300])
        self.assertEqual(wrap_column(text), 70)

    def test_loses_no_words(self):
        body = "\n\n".join(textwrap.fill(PROSE, width=70) for _ in range(4))
        self.assertEqual(reflow(body).split(), body.split())


class PackPiecesTests(unittest.TestCase):
    def test_preserves_every_paragraph_in_order(self):
        paras = [f"para-{i} " + "x" * 100 for i in range(20)]
        rejoined = "\n\n".join(pack_pieces(paras, piece_target=250))
        self.assertEqual(rejoined.split("\n\n"), paras)

    def test_respects_the_piece_target_for_multi_paragraph_pieces(self):
        paras = ["x" * 100 for _ in range(20)]
        for piece in pack_pieces(paras, piece_target=250):
            if "\n\n" in piece:
                self.assertLessEqual(len(piece), 250)

    def test_oversized_paragraph_gets_its_own_piece(self):
        self.assertEqual(pack_pieces(["y" * 5000, "short"], piece_target=100), ["y" * 5000, "short"])


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
