import importlib.util
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


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


class RehearsalPlanTest(unittest.TestCase):
    def plan(self, target_dual=9, target_single=1, actual_dual=5, actual_single=0):
        return {
            'target': {'dualFeedClients': target_dual, 'programOnlyClients': target_single},
            'actual': {'dualFeedClients': actual_dual, 'programOnlyClients': actual_single},
        }

    def test_owner_is_included_once_and_satellite_has_no_portal(self):
        result = MODULE.rehearsal_load(self.plan())
        self.assertEqual((result['target']['program'], result['target']['portal']), (10, 9))
        self.assertEqual((result['actual']['program'], result['actual']['portal']), (5, 5))
        self.assertEqual(result['synthetic'], {'program': 5, 'portal': 4})

    def test_no_additional_receivers_when_real_clients_cover_target(self):
        self.assertEqual(MODULE.rehearsal_load(self.plan(actual_dual=10, actual_single=2))['synthetic'],
                         {'program': 0, 'portal': 0})

    def test_subtracts_per_feed_when_actual_viewer_roles_differ(self):
        result = MODULE.rehearsal_load(self.plan(3, 1, 2, 3))
        self.assertEqual(result['synthetic'], {'program': 0, 'portal': 1})

    def test_rejects_missing_boolean_negative_and_noninteger_counts(self):
        for value in (None, True, -1, 1.5, '5', 26):
            with self.subTest(value=value), self.assertRaises(ValueError):
                MODULE.rehearsal_load(self.plan(actual_dual=value))
        with self.assertRaises(ValueError):
            MODULE.rehearsal_load({'target': {}, 'actual': {}})

    def test_dry_run_needs_neither_ffmpeg_nor_receiver_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            plan_path = root/'plan.json'
            plan_path.write_text(json.dumps(self.plan()))
            with patch.object(MODULE, 'ROOT', root), patch.object(MODULE.shutil, 'which') as which:
                output = io.StringIO()
                with contextlib.redirect_stdout(output):
                    code = MODULE.main(['--rehearsal-plan', str(plan_path), '--dry-run'])
                self.assertEqual(code, 0)
                self.assertIn('Synthetic receivers: program=5, portal=4', output.getvalue())
                which.assert_not_called()
                self.assertFalse((root/'build').exists())


if __name__ == '__main__':
    unittest.main()
