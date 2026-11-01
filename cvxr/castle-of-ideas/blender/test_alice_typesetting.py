"""Run with the existing Pillow interpreter; no dependencies added to .venv."""
import unittest

from alice_book_wallpaper import available_line_spans, compose_line


class AliceTypesettingTest(unittest.TestCase):
    def test_justification_fills_span_without_changing_word_order(self):
        words = "one two three four five".split()
        positions, end = compose_line(words, 0, 10, 26, len, 1, 3)
        self.assertEqual([word for x, word in positions], words[:3])
        self.assertEqual(end, 3)
        self.assertEqual(positions[0][0], 10)
        self.assertAlmostEqual(positions[-1][0]+len(positions[-1][1]), 26)

    def test_two_word_island_is_deferred_without_losing_words(self):
        words = "one two three four".split()
        positions, end = compose_line(words, 0, 0, 8, len, 1, 3)
        self.assertEqual((positions, end), ([], 0))
        positions, end = compose_line(words, end, 0, 14, len, 1, 3)
        self.assertEqual([word for x, word in positions], words[:3])
        self.assertEqual(end, 3)

    def test_overlapping_contours_are_merged_and_clipped(self):
        contours = [(0, [(20, 50)]*10), (0, [(40, 80)]*10), (0, [(-5, 5)]*10)]
        self.assertEqual(available_line_spans(0, 100, 0, 10, contours), [(5, 20), (80, 100)])
        self.assertEqual(available_line_spans(0, 100, 20, 10, contours), [(0, 100)])

    def test_descenders_are_included_in_obstacle_band(self):
        contours = [(18, [(0, 100)]*3)]
        self.assertEqual(available_line_spans(0, 100, 0, 22, contours), [])


if __name__ == "__main__":
    unittest.main()
