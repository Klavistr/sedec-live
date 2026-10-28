import importlib.util
from pathlib import Path
import unittest


PATH = Path(__file__).resolve().parents[1] / "cvxr/castle-of-ideas/tools/hls-load-test.py"
SPEC = importlib.util.spec_from_file_location("hls_load_test", PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class HlsLoadTest(unittest.TestCase):
    def test_segment_sequence_duration_and_relative_urls(self):
        playlist = "#EXTM3U\n#EXT-X-MEDIA-SEQUENCE:12\n#EXT-X-TARGETDURATION:4\n#EXTINF:3.5,\n12.ts\n#EXTINF:4,\n13.ts\n"
        target, segments, init = MODULE.parse_playlist(playlist, "https://example.invalid/live/index.m3u8")
        self.assertEqual(target, 4)
        self.assertEqual(segments, [(12,3.5,"https://example.invalid/live/12.ts"),(13,4.0,"https://example.invalid/live/13.ts")])
        self.assertIsNone(init)

    def test_fmp4_initialization_is_resolved(self):
        _, _, init = MODULE.parse_playlist('#EXTM3U\n#EXT-X-MAP:URI="init.mp4"\n#EXTINF:4,\n1.m4s\n', "https://example.invalid/a/index.m3u8")
        self.assertEqual(init, "https://example.invalid/a/init.mp4")

    def test_master_and_non_media_responses_are_rejected(self):
        for text in ("404 Not Found", "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=10000000\nvariant.m3u8\n"):
            with self.assertRaises(ValueError):
                MODULE.parse_playlist(text, "https://example.invalid/index.m3u8")
