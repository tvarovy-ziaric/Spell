import csv
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from performance import analyze, HEADER, METHODS


class PerformanceChecks(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.path = Path(temp.name) / "synthetic.csv"
        self.write(HEADER)

    def write(self, row):
        with self.path.open("a", newline="", encoding="utf8") as fh:
            csv.writer(fh).writerow(row)

    def add(self, metric, sample, start, end, run="run-a", phone="A", mode="high", method=None, source="synthetic"):
        self.write([run, phone, mode, metric, method or METHODS[metric], source, sample, start, end])

    def one(self):
        return analyze(self.path)["groups"][0]

    def test_empty_and_missing(self):
        self.assertEqual(analyze(self.path)["groups"], [])
        self.add("tap", "tap01", 10000, 21000)
        r = self.one()
        self.assertEqual(r["window_15min"], "MISSING")
        self.assertEqual(r["metrics"]["frame"]["p95_comparison"], "MISSING")
        self.assertIsNone(r["metrics"]["terrain"]["maximum_ms"])

    def test_percentile_is_distinct_from_max_and_durations_keep_units(self):
        self.add("window", "game", 0, 900000000)
        for i in range(1, 21):
            self.add("tap", "T"+str(i), i*200000, i*200000 + (50000 if i < 20 else 99000))
        r = self.one()
        self.assertEqual(r["window_15min"], "REACHED")
        m = r["metrics"]["tap"]
        self.assertEqual((m["sample_count"],m["p95_ms"],m["maximum_ms"],m["samples_over_limit"]),
                         (20,50,99,1))
        self.assertEqual(m["p95_comparison"], "WITHIN")

    def test_frame_gaps_and_terrain_span_aggregation(self):
        self.add("window", "game", 0, 130000)
        self.add("frame", "F3", 100000, 130000)
        self.add("frame", "F1", 0, 30000)
        self.add("terrain", "F1", 1000, 2900)
        self.add("terrain", "F1", 4000, 4700)
        self.add("terrain", "F3", 110000, 110000)
        r = self.one()
        self.assertEqual(r["window_15min"], "SHORT")
        t, f = r["metrics"]["terrain"], r["metrics"]["frame"]
        self.assertEqual((t["sample_count"], t["p95_ms"], t["p95_comparison"]), (2,2.6,"ABOVE"))
        self.assertEqual(f["presentation_coverage"]["longest_unobserved_s"], .07)
        self.assertAlmostEqual(f["presentation_coverage"]["fraction"], 60/130)

    def test_two_devices_and_modes_are_independent(self):
        self.add("tap", "T1", 0, 20000)
        self.add("tap", "T1", 0, 70000, phone="B")
        self.add("tap", "T1", 0, 12000, mode="low")
        report = analyze(self.path)
        self.assertEqual(report["phones_reported"], ["A","B"])
        self.assertEqual(len(report["groups"]), 3)

    def test_duplicate_pair(self):
        self.add("frame", "F1", 0, 16666)
        self.add("frame", "F2", 0, 16666)
        with self.assertRaisesRegex(ValueError, "duplicate"):
            analyze(self.path)

    def test_duplicate_sample(self):
        self.add("tap", "T1", 0, 30000)
        self.add("tap", "T1", 100000, 133000)
        with self.assertRaisesRegex(ValueError, "Repeated"):
            analyze(self.path)

    def test_overlapping_terrain(self):
        self.add("terrain", "F1", 1000, 4000)
        self.add("terrain", "F1", 2000, 3000)
        with self.assertRaisesRegex(ValueError, "Overlapping"):
            analyze(self.path)

    def test_timing_after_session(self):
        self.add("window","game",0,1000)
        self.add("frame","F1",1,1001)
        with self.assertRaisesRegex(ValueError,"outside"):
            analyze(self.path)

    def test_method_and_cli_failure(self):
        self.add("terrain", "F1", 1000, 3000, method="high_speed_video")
        cli=subprocess.run([sys.executable, str(Path(__file__).with_name("performance.py")), str(self.path)],
                           capture_output=True, text=True)
        self.assertEqual(cli.returncode,2)
        self.assertIn("incompatible",cli.stderr)

    def test_reversed_and_nonfinite(self):
        self.add("tap", "T1", 20, 10)
        with self.assertRaisesRegex(ValueError,"reversed"):
            analyze(self.path)
        self.path.unlink()
        self.write(HEADER)
        self.add("tap", "T1", 20, "NaN")
        with self.assertRaisesRegex(ValueError,"integer"):
            analyze(self.path)


if __name__ == "__main__":
    unittest.main()
