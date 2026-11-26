import csv
import math
from pathlib import Path
import shutil
import tempfile
import unittest

from m2 import p95, summarize


class FieldEvidence(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.folder = Path(tmp.name)
        for name in ("points", "attempts", "observations", "anchors"):
            shutil.copy(Path(__file__).with_name(name + ".csv"), self.folder)

    def append(self, name, *values):
        with (self.folder / (name + ".csv")).open("a", newline="") as fh:
            csv.writer(fh).writerow(values)

    def test_empty(self):
        result = summarize(self.folder)
        self.assertIsNone(result["mutual_3d_p95_m"])
        self.assertIsNone(result["ready_within_120s_fraction"])
        self.assertIsNone(result["play_terrain_bytes_max"])

    def base(self):
        for point, east, north in (("P1", 10, 20), ("P2", 11, 20), ("P3", 11, 21)):
            self.append("points", point, east, north, 100, .02, .04)
        for run, day, reverse in (("1", "2026-01-01", False), ("2", "2026-01-02", True)):
            for phone, role in (("A", "guest" if reverse else "founder"), ("B", "founder" if reverse else "guest")):
                self.append("attempts", run, day, phone, role, "failed" if reverse else "ready",
                            "" if reverse else 11, "", "")

    def test_metrics_pairing_failure_denominator_and_missing_evidence(self):
        self.base()
        self.append("observations", 1, "A", "P1", 5, 1, 5, 11, 20, 99.5)
        self.append("observations", 1, "B", "P1", 8, 5, 5, "", "", "")
        result = summarize(self.folder)
        self.assertEqual((result["mutual_pairs"], result["mutual_3d_p95_m"]), (1, 5))
        self.assertEqual((result["absolute_samples"], result["absolute_horizontal_p95_m"],
                          result["absolute_vertical_p95_m"]), (1, 1, .5))
        self.assertAlmostEqual(result["vertical_plus_survey_uncertainty_p95_m"], .54)
        self.assertEqual(result["ready_within_120s_fraction"], .5)
        self.assertTrue(result["roles_swapped"] and result["survey_controls_noncollinear"])
        self.assertEqual((result["two_phone_runs"], result["days"]), (2, 2))
        self.assertEqual(p95(range(1, 101)), 95)

    def test_invalid_identity(self):
        self.base()
        self.append("observations", 1, "wrong", "P1", 1, 2, 3, "", "", "")
        with self.assertRaises(ValueError):
            summarize(self.folder)

    def test_bad_coordinates(self):
        self.base()
        self.append("observations", 1, "A", "P1", math.nan, 2, 3, "", "", "")
        with self.assertRaises(ValueError):
            summarize(self.folder)

    def test_duplicate_input(self):
        self.base()
        self.append("attempts", 1, "2026-01-01", "A", "founder", "ready", 20, "", "")
        with self.assertRaises(ValueError):
            summarize(self.folder)

    def test_partial_absolute(self):
        self.base()
        self.append("observations", 1, "A", "P1", 1, 2, 3, 11, "", "")
        with self.assertRaises(ValueError):
            summarize(self.folder)

    def add_anchor(self, board, e, n, frame="EPSG:8353+8357", sigma=.01):
        self.append("anchors", "1", "A", board, frame, "phone-fix", "synthetic test", "2026-01-01T12:00:00Z",
                    e, n, 98, 0, 0, 1, sigma, .02)

    def test_turned_baseline_and_measured_antenna_offset(self):
        self.base()
        self.add_anchor("A", 10, 20)
        self.add_anchor("B", 10, 21.5)
        self.append("observations", "1", "A", "P1", 1, 1, 2, "", "", "")
        result = summarize(self.folder)
        self.assertAlmostEqual(result["absolute_horizontal_p95_m"], math.sqrt(5))
        self.assertEqual(result["absolute_vertical_p95_m"], 0)
        self.assertGreater(result["anchor_horizontal_sensitivity_p95_m"], .01)

    def test_unobservable_bearing(self):
        self.base()
        self.add_anchor("A", 10, 20, sigma=1)
        self.add_anchor("B", 10, 21.5, sigma=1)
        self.append("observations", "1", "A", "P1", 1, 1, 2, "", "", "")
        result = summarize(self.folder)
        self.assertIsNone(result["absolute_vertical_p95_m"])
        self.assertEqual(result["insufficient_anchor_geometry_observations"], 1)

    def test_geographic_frame_cannot_enter_projected_fit(self):
        self.base()
        self.add_anchor("A", 10, 20, "EPSG:4979")
        with self.assertRaisesRegex(ValueError, "JTSK03/Bpv"):
            summarize(self.folder)

    def test_traffic_delta(self):
        self.append("attempts", 1, "2026-01-01", "A", "founder", "ready", 3, 2, 624)
        result = summarize(self.folder)
        self.assertEqual(result["play_terrain_bytes_max"], 624)
        self.assertEqual(result["play_terrain_requests_max"], 2)
        self.assertFalse(result["roles_swapped"] or result["survey_controls_noncollinear"])


if __name__ == "__main__":
    unittest.main()
