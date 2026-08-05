"""Unit tests for the lore text pipeline.

These cover the pure transforms in tools/build_lore_assets.py — no network,
no Gutenberg cache, no Vintage Story install. The chunking functions are
where a silent regression would corrupt every book, so most of these assert
that content survives the trip rather than just checking sizes.
"""

import json
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from build_lore_assets import (  # noqa: E402
    LANG_OVERLAY_DIR,
    apply_lang_overlays,
    check_shipped_lang_files,
    escape_lang,
    keeps_line_breaks,
    load_lang_overlays,
    pack_pieces,
    paragraphs,
    reflow,
    split_volumes,
    stray_lang_files,
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


class LangOverlayTests(unittest.TestCase):
    """The overlays are the only reason the book pile UI has any strings at all.

    Vintage Story reads every file in a lang folder as a locale named after the file, so these
    partials cannot live beside en.json — an en-bookpile.json there loads as locale
    "en-bookpile" and never reaches an English player. They live in mod/lang and get merged.

    Real translations are welcome in the shipped folder; only names that are not locales are not.
    """

    def test_shipped_lang_folder_holds_only_locales(self):
        self.assertEqual(stray_lang_files(), [])

    def test_accepts_real_translations(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("en.json", "de.json", "pt-br.json", "es-419.json", "zh-cn.json"):
                (root / name).write_text("{}", encoding="utf-8")
            self.assertEqual(stray_lang_files(root), [])
            check_shipped_lang_files(root)

    def test_rejects_a_partial_that_only_looks_like_a_locale(self):
        # The bug this guards: en-bookpile is shaped exactly like the real pt-br and es-419,
        # so nothing but the locale list can tell them apart.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "en.json").write_text("{}", encoding="utf-8")
            (root / "en-bookpile.json").write_text("{}", encoding="utf-8")
            self.assertEqual(stray_lang_files(root), ["en-bookpile.json"])
            with self.assertRaises(ValueError):
                check_shipped_lang_files(root)

    def test_merges_every_overlay_file(self):
        merged = load_lang_overlays()
        self.assertIn("blockinfo-bookpile-count", merged)
        self.assertIn("item-bookstack", merged)

    # Must match BookPileLayoutMode in mod/src/Storage/BookPileUtil.cs.
    LAYOUT_MODES = ("messy", "neat", "tumbled", "shelved", "leaning")

    def test_ships_a_string_for_every_layout_mode(self):
        merged = load_lang_overlays()
        for mode in self.LAYOUT_MODES:
            self.assertIn(f"bookpile-layout-{mode}", merged)
            self.assertIn(f"blockinfo-bookpile-layout-{mode}", merged)

    def test_every_layout_mode_has_poses_in_the_config(self):
        config = json.loads(
            (
                Path(__file__).resolve().parents[1]
                / "mod"
                / "assets"
                / "liberterra"
                / "config"
                / "bookpile-layout.json"
            ).read_text(encoding="utf-8")
        )
        self.assertEqual(sorted(config), sorted(self.LAYOUT_MODES))
        for mode, slots in config.items():
            self.assertEqual(len(slots), 16, f"{mode} must fill all 16 pile slots")

    def test_rejects_a_key_that_shadows_a_generated_one(self):
        lang = {"blockinfo-bookpile-count": "generated"}
        with self.assertRaises(ValueError):
            apply_lang_overlays(lang)

    def test_rejects_the_same_key_in_two_overlay_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "en-a.json").write_text('{"shared": "one"}', encoding="utf-8")
            (root / "en-b.json").write_text('{"shared": "two"}', encoding="utf-8")
            with self.assertRaises(ValueError):
                load_lang_overlays(root)

    def test_overlay_dir_is_not_packaged_into_assets(self):
        self.assertNotIn("assets", LANG_OVERLAY_DIR.parts)


if __name__ == "__main__":
    unittest.main()
