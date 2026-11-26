"""Summarize a private Spell M2 field dataset. Never infers missing physical observations."""
import argparse
import csv
from datetime import date
import json
import math
from pathlib import Path

from anchors import estimate, read_anchor


def rows(folder, name):
    with (folder / (name + ".csv")).open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def number(raw, nonnegative=False):
    val = float(raw)
    if not math.isfinite(val) or (nonnegative and val < 0):
        raise ValueError("Expected finite measurement " + str(raw))
    return val


def xyz(row, names, optional=False):
    items = [row[n] for n in names.split()]
    if optional and all(i == "" for i in items):
        return None
    return tuple(number(i) for i in items)


def p95(values):
    """Nearest rank P95, including the maximum for fewer than twenty measurements."""
    ordered = sorted(values)
    return ordered[math.ceil(.95 * len(values)) - 1] if values else None


def magnitude(a, b):
    return math.dist(a, b)


def summarize(folder):
    points, attempts, observations = {}, {}, {}
    for r in rows(folder, "points"):
        key = r["point"]
        if not key or key in points:
            raise ValueError("Missing or duplicate control point")
        absolute = xyz(r, "jtsk_e jtsk_n bpv", optional=True)
        sigmas = xyz(r, "survey_h_m survey_v_m", optional=True)
        if (absolute is None) != (sigmas is None) or (sigmas and min(sigmas) < 0):
            raise ValueError("A surveyed control requires both reference uncertainties")
        points[key] = absolute, sigmas
    for r in rows(folder, "attempts"):
        run, phone = r["run"], r["phone"]
        key = run, phone
        if not run or not phone or key in attempts or r["role"] not in {"founder", "guest"}:
            raise ValueError("Invalid or duplicate run/phone/role")
        date.fromisoformat(r["day"])
        if r["outcome"] not in {"ready", "failed"}:
            raise ValueError("Record ready or failed, including abandoned calibrations")
        elapsed = number(r["seconds"], True) if r["seconds"] else None
        if r["outcome"] == "ready" and elapsed is None:
            raise ValueError("Ready requires a measured time")
        stream = xyz(r, "play_terrain_requests play_terrain_bytes", optional=True)
        if stream and any(x < 0 or not x.is_integer() for x in stream):
            raise ValueError("Counts must be nonnegative integers")
        attempts[key] = (r["day"], r["role"], elapsed if r["outcome"] == "ready" else None, stream)
    phones = sorted({phone for _, phone in attempts})
    if len(phones) > 2:
        raise ValueError("Use a separate sheet for another pair of phones")
    runs = sorted({run for run, _ in attempts})
    for run in runs:
        entries = [a for (rid, _), a in attempts.items() if rid == run]
        if len({a[0] for a in entries}) != 1 or len({a[1] for a in entries}) != len(entries):
            raise ValueError("Inconsistent date or duplicate role in run " + run)
    anchors = {}
    if (folder / "anchors.csv").exists():
        for r in rows(folder, "anchors"):
            key = r["run"], r["phone"]
            board = r["board"]
            pair = anchors.setdefault(key, {})
            if key not in attempts or board not in {"A", "B"} or board in pair:
                raise ValueError("Unknown capture or duplicate board")
            pair[board] = read_anchor(r)
    sensitivity_h, sensitivity_v = [], []
    unsupported_anchor_geometry = 0
    for r in rows(folder, "observations"):
        run, phone, point = r["run"], r["phone"], r["point"]
        key = run, phone, point
        if key in observations or (run, phone) not in attempts or point not in points:
            raise ValueError("Duplicate or unknown observation identity")
        if attempts[run, phone][2] is None:
            raise ValueError("Observation requires a successful calibration")
        local = xyz(r, "shared_x shared_y shared_z")
        geo = xyz(r, "phone_e phone_n phone_bpv", optional=True)
        if geo is None and points[point][0] is not None and (run, phone) in anchors:
            converted = estimate(anchors[run, phone], local)
            if converted is None:
                unsupported_anchor_geometry += 1
            else:
                geo, sh, sv = converted
                sensitivity_h.append(sh)
                sensitivity_v.append(sv)
        if geo and points[point][0] is None:
            raise ValueError("Absolute observation requires independent surveyed point")
        observations[key] = local, geo
    mutual, horizontal, vertical, upper_h, upper_v = [], [], [], [], []
    for run in runs:
        for point, (control, sigmas) in points.items():
            pair = []
            for phone in phones:
                sample = observations.get((run, phone, point))
                if sample is None:
                    continue
                pos, geo = sample
                pair.append(pos)
                if geo:
                    h, v = magnitude(geo[:2], control[:2]), abs(geo[2] - control[2])
                    horizontal.append(h)
                    vertical.append(v)
                    upper_h.append(h + sigmas[0])
                    upper_v.append(v + sigmas[1])
            if len(pair) == 2:
                mutual.append(magnitude(*pair))
    controls = [c[:2] for c, _ in points.values() if c is not None]
    independent_layout = any(abs((x1-x0)*(y2-y0) - (y1-y0)*(x2-x0)) > 1e-6
                             for i, (x0, y0) in enumerate(controls)
                             for j, (x1, y1) in enumerate(controls[i+1:], i+1)
                             for x2, y2 in controls[j+1:])
    ready = [a[2] for a in attempts.values() if a[2] is not None]
    traffic = [a[3] for a in attempts.values() if a[3] is not None]
    days = len({a[0] for a in attempts.values()})
    swapped = len(phones) == 2 and all(
        {v[1] for k, v in attempts.items() if k[1] == phone} == {"founder", "guest"} for phone in phones)
    complete_runs = sum(all((rid, phone) in attempts for phone in phones) for rid in runs) if len(phones) == 2 else 0
    return {
        "decision": "PHYSICAL TEST REQUIRED - posudzuje vlastnik",
        "run_count": len(runs), "two_phone_runs": complete_runs, "days": days, "roles_swapped": swapped,
        "phones": phones, "control_points": len(points), "survey_controls_noncollinear": independent_layout,
        "attempts": len(attempts), "ready_count": len(ready), "failed_count": len(attempts)-len(ready),
        "ready_within_120s_fraction": sum(s <= 120 for s in ready) / len(attempts) if attempts else None,
        "calibration_time_p95_s_successes": p95(ready),
        "mutual_pairs": len(mutual), "mutual_3d_p95_m": p95(mutual),
        "absolute_samples": len(vertical), "absolute_horizontal_p95_m": p95(horizontal),
        "insufficient_anchor_geometry_observations": unsupported_anchor_geometry,
        "anchor_horizontal_sensitivity_p95_m": p95(sensitivity_h),
        "anchor_vertical_sensitivity_p95_m": p95(sensitivity_v),
        "absolute_vertical_p95_m": p95(vertical), "horizontal_plus_survey_uncertainty_p95_m": p95(upper_h),
        "vertical_plus_survey_uncertainty_p95_m": p95(upper_v),
        "play_traffic_measurements": len(traffic),
        "play_terrain_requests_max": max((int(t[0]) for t in traffic), default=None),
        "play_terrain_bytes_max": max((int(t[1]) for t in traffic), default=None),
        "fully_observed_pairs_expected_at_all_controls": complete_runs * len(points),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(summarize(args.folder), ensure_ascii=False, indent=2, allow_nan=False))
    except (OSError, KeyError, ValueError) as exc:
        parser.exit(2, f"Invalid M2 input: {exc}\n")
