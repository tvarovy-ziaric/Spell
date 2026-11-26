"""Reconstruct estimates in DMR coordinates from each phone's corrected antenna observations."""
from dataclasses import dataclass
from datetime import datetime
import math


@dataclass(frozen=True)
class Anchor:
    east: float
    north: float
    bpv: float
    h_accuracy: float
    v_accuracy: float


def read_anchor(row):
    if row["frame"] != "EPSG:8353+8357" or row["source"] != "phone-fix" or not row["conversion"].strip():
        raise ValueError("Each anchor needs the corrected JTSK03/Bpv phone fix and its transformation provenance")
    if datetime.fromisoformat(row["fix_utc"].replace("Z", "+00:00")).utcoffset() is None:
        raise ValueError("Missing timezone on field fix")
    e, n, h, de, dn, dh, sh, sv = (float(row[k]) for k in
        ("antenna_e", "antenna_n", "antenna_bpv", "to_board_e", "to_board_n", "to_board_up",
         "h_accuracy_m", "v_accuracy_m"))
    if not all(map(math.isfinite, (e, n, h, de, dn, dh, sh, sv))) or min(sh, sv) < 0:
        raise ValueError("Anchor needs finite corrections and measured uncertainty")
    return Anchor(e + de, n + dn, h + dh, sh, sv)


def estimate(pair, shared):
    """Shared X points A→B. A and B are level, 1.50 m apart in this diagnostic APK."""
    if set(pair) != {"A", "B"}:
        return None
    a, b = pair["A"], pair["B"]
    e, n = b.east-a.east, b.north-a.north
    distance = math.hypot(e, n)
    sh = a.h_accuracy + b.h_accuracy
    sv = a.v_accuracy + b.v_accuracy
    if distance <= sh or abs(distance - 1.50) > sh + .001 or abs(a.bpv - b.bpv) > sv + .001:
        return None
    ex, nx = e/distance, n/distance
    x, y, z = shared
    x -= .75
    location = ((a.east+b.east)/2 + x*ex - z*nx,
                (a.north+b.north)/2 + x*nx + z*ex, (a.bpv+b.bpv)/2 + y)
    # Sensitivity to reported anchor accuracy, not a statistical coverage interval.
    angle = math.asin(sh / distance)
    h_sensitivity = sh/2 + 2*math.hypot(x, z)*math.sin(angle/2)
    return location, h_sensitivity, sv/2
