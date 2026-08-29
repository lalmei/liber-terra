"""Unit tests for non-Gutenberg text flattening."""

from __future__ import annotations

import io
import sys
import zipfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from text_sources import apply_extract, epub_to_text, extract_span, html_to_text, source_url  # noqa: E402


class HtmlToTextTests(unittest.TestCase):
    def test_drops_scripts_and_collapses_blank_runs(self):
        markup = """
        <html><head><script>ignore()</script><title>X</title></head>
        <body>
          <h1>Phaenomena</h1>
          <p>From Zeus let us begin.</p>
          <p>Study all the signs together.</p>
        </body></html>
        """
        text = html_to_text(markup)
        self.assertIn("Phaenomena", text)
        self.assertIn("From Zeus let us begin.", text)
        self.assertNotIn("ignore()", text)

    def test_turns_breaks_into_newlines(self):
        self.assertIn("\n", html_to_text("<p>one<br>two</p>"))


class ExtractSpanTests(unittest.TestCase):
    def test_keeps_the_end_marker_when_asked(self):
        body = "junk\nFrom Zeus let us begin\nmiddle\nTHE END\nfooter"
        self.assertTrue(extract_span(body, start="From Zeus", end="THE END").endswith("THE END"))

    def test_cuts_before_a_regex_end(self):
        body = "PREFACE\nstars\nCATALOGUE                II.\nmodern tables"
        cut = extract_span(body, start="PREFACE", end_re=r"CATALOGUE\s+II\.")
        self.assertIn("stars", cut)
        self.assertNotIn("modern tables", cut)

    def test_apply_extract_reads_work_keys(self):
        work = {
            "extract_start": "On the Heavens",
            "extract_end": "THE END",
        }
        text = "header\nOn the Heavens\nBook I\nTHE END\ncopyright"
        self.assertEqual(apply_extract(text, work), "On the Heavens\nBook I\nTHE END")


class EpubToTextTests(unittest.TestCase):
    def test_joins_numbered_pages_in_order(self):
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as archive:
            archive.writestr("EPUB/page_2.html", "<p>second</p>")
            archive.writestr("EPUB/page_1.html", "<p>first</p>")
            archive.writestr("EPUB/notice.html", "<p>skip me by name only</p>")
        text = epub_to_text(buf.getvalue())
        self.assertEqual(text, "first\n\nsecond")


class SourceUrlTests(unittest.TestCase):
    def test_gutenberg_id_becomes_an_ebook_url(self):
        self.assertEqual(
            source_url({"id": 70850}),
            "https://www.gutenberg.org/ebooks/70850",
        )

    def test_explicit_url_wins(self):
        self.assertEqual(
            source_url({"id": 1, "url": "https://classics.mit.edu/Aristotle/heavens.mb.txt"}),
            "https://classics.mit.edu/Aristotle/heavens.mb.txt",
        )


if __name__ == "__main__":
    unittest.main()
