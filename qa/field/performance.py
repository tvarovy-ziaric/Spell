"""Summarize private, paired M4 timings per phone, run and quality mode. Times are integer microseconds."""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path

from m2 import p95


HEADER = "run phone mode metric method source sample start_us end_us".split()
METHODS = {"window": "duel_clock", "tap": "high_speed_video",
           "frame": "presentation_times", "terrain": "main_thread_trace"}
TARGET_MS = {"tap": 50, "frame": 34, "terrain": 2}


def coverage(intervals, window):
    if not intervals or not window:
        return None
    left, right = window
    covered = 0
    longest_gap = 0
    for start, end in sorted(intervals):
        if start < left:
            raise ValueError("Overlapping presentation intervals or two frames for the same instant")
        longest_gap = max(longest_gap, start-left)
        covered += end-start
        left = end
    return {"recorded_s": covered/1e6,
            "fraction": covered/(right-window[0]),
            "longest_unobserved_s": max(longest_gap, right-left)/1e6}


def metric_report(metric, samples, window):
    durations, starts, ends, sources = [], [], [], set()
    for spans in samples.values():
        intervals = sorted((start, end) for start, end, _ in spans)
        if metric != "terrain" and len(spans) != 1:
            raise ValueError(f"Repeated sample identifier for {metric}")
        if len({s for _, _, s in spans}) != 1:
            raise ValueError("All spans for a sample must use the same clock/source")
        if any(b[0] < a[1] for a, b in zip(intervals, intervals[1:])):
            raise ValueError("Overlapping terrain work within a frame")
        durations.append(sum(end-start for start, end in intervals)/1000)
        starts.append(intervals[0][0])
        ends.append(intervals[-1][1])
        sources.update(source for _, _, source in spans)
    spans = sorted(zip(starts, ends))
    if metric in ("frame", "terrain") and any(b[0] < a[1] for a,b in zip(spans,spans[1:])):
        raise ValueError(f"Overlapping {metric} frame samples")
    quantile = p95(durations)
    limit = TARGET_MS[metric]
    return {"sample_count": len(durations), "p95_ms": quantile, "maximum_ms": max(durations, default=None),
            "proposed_p95_limit_ms": limit, "samples_over_limit": sum(t > limit for t in durations),
            "p95_comparison": "MISSING" if quantile is None else "WITHIN" if quantile <= limit else "ABOVE",
            "first_sample_s": min(starts)/1e6 if starts else None,
            "last_sample_s": max(ends)/1e6 if ends else None,
            "sources": sorted(sources),
            **({"presentation_coverage": coverage(spans, window)} if metric == "frame" else {})}


def analyze(path):
    groups = defaultdict(lambda: defaultdict(lambda: defaultdict(list)))
    seen = set()
    with path.open(encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        if reader.fieldnames != HEADER:
            raise ValueError("Expected header " + ",".join(HEADER))
        for line, row in enumerate(reader, 2):
            if set(row) != set(HEADER) or any(not (value or "").strip() for value in row.values()):
                raise ValueError(f"Line {line}: missing field or extra data")
            row = {k:v.strip() for k,v in row.items()}
            metric = row["metric"]
            if METHODS.get(metric) != row["method"]:
                raise ValueError(f"Line {line}: unknown metric or incompatible measurement method")
            try:
                start, end = int(row["start_us"]), int(row["end_us"])
            except ValueError as exc:
                raise ValueError(f"Line {line}: expected integer microseconds") from exc
            if start < 0 or end < start or end > 2**63-1 or (end == start and metric in ("window","frame")):
                raise ValueError(f"Line {line}: invalid pair or reversed timestamps")
            group = row["run"], row["phone"], row["mode"]
            key = group, metric, start, end
            if key in seen:
                raise ValueError(f"Line {line}: duplicate timing pair for {metric}")
            seen.add(key)
            groups[group][metric][row["sample"]].append((start, end, row["source"]))
    reports = []
    for (run, phone, mode), data in sorted(groups.items()):
        windows = [span for spans in data.get("window", {}).values() for span in spans]
        if len(windows) > 1:
            raise ValueError("Separate interrupted sessions into distinct run identifiers")
        window = windows[0][:2] if windows else None
        if window and any(start < window[0] or end > window[1] for metric in TARGET_MS
                          for spans in data.get(metric, {}).values() for start,end,_ in spans):
            raise ValueError("Measurement outside the active-duel window")
        duration = (window[1] - window[0])/1e6 if window else None
        reports.append({"run": run, "phone": phone, "mode": mode, "active_duel_s": duration,
                        "window_15min": "MISSING" if duration is None else "REACHED" if duration >= 900 else "SHORT",
                        "window_source": windows[0][2] if windows else None,
                        "metrics": {m:metric_report(m, data.get(m, {}), window) for m in TARGET_MS}})
    return {"decision": "M4 requires owner review of physical tests",
            "phones_reported": sorted({r["phone"] for r in reports}), "groups": reports}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("csv", type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(analyze(args.csv), indent=2, ensure_ascii=False, allow_nan=False))
    except (OSError, ValueError, csv.Error) as exc:
        parser.exit(2, f"Invalid M4 data: {exc}\n")
