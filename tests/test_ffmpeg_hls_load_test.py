import importlib.util
from pathlib import Path
import unittest


PATH = Path(__file__).resolve().parents[1] / 'cvxr/castle-of-ideas/tools/ffmpeg-hls-load-test.py'
SPEC = importlib.util.spec_from_file_location('ffmpeg_hls_load_test', PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FFmpegDiagnosticsTest(unittest.TestCase):
    def test_http_status_is_counted_without_private_url(self):
        self.assertEqual(MODULE.error_category('[https @ abc] HTTP error 404 https://private.invalid/secret/index.m3u8'), 'http_404')
        self.assertEqual(MODULE.error_category('Server returned 503 Service Unavailable'), 'http_503')

    def test_url_words_do_not_become_error_labels(self):
        self.assertIsNone(MODULE.error_category('https://private.invalid/error-timeout-failed/index.m3u8'))
        self.assertEqual(MODULE.error_category('Failed to open segment https://private.invalid/key/1.ts'), 'hls_fetch_error')

    def test_transport_failure_and_normal_progress_are_distinct(self):
        self.assertEqual(MODULE.error_category('Connection timed out'), 'timeout')
        self.assertIsNone(MODULE.error_category('progress=continue'))


if __name__ == '__main__':
    unittest.main()
