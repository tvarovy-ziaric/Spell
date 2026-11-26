"""Exercise an integrated local server's batch endpoint and confirm its database effects."""
import argparse
import hashlib
import json
import subprocess
import time
import uuid
from pathlib import Path

from test_admin import call, server


def count(container, key):
    # key is a local hex digest of this test seat, never an access token.
    assert len(key) == 64 and all(c in "0123456789ABCDEF" for c in key)
    out = subprocess.check_output(["docker", "exec", container, "psql", "-U", "spell", "-d", "spell", "-Atqc",
        f"SELECT COUNT(*) FROM spell_diagnostic_batches WHERE seat_hash='{key}'"], text=True)
    return int(out.strip())


def exercise(base, container):
    made = call(base, "POST", "session", headers={"Idempotency-Key": uuid.uuid4().hex})
    ticket = made["player"]
    token = ticket["seat"]["token"]
    permit = ticket["diagnosticPermit"]
    assert permit and permit != token
    digest = hashlib.sha256(token.encode()).hexdigest().upper()
    assert count(container, digest) == 0
    call(base, "DELETE", "session", token=token, code=204)
    counters = dict(version=1, gnssFixes=2, visualFrames=20, estimates=10, sentPoses=3, terrainBytes=0,
                    terrainRequests=0, peakThermalStatus=-1)
    path = "diagnostics/batch"
    call(base, "POST", path, body=counters, code=401)
    call(base, "POST", path, token=token, body=counters, code=401)
    call(base, "POST", path, token=permit, body=dict(counters, version=42), code=400)
    call(base, "POST", path, token=permit, body=dict(counters, sentPoses=11), code=400)
    assert count(container, digest) == 0
    call(base, "POST", path, token=permit, body=counters, code=204)
    call(base, "POST", path, token=permit, body=counters, code=204)
    call(base, "POST", path, token=permit, body=dict(counters, gnssFixes=3), code=409)
    assert count(container, digest) == 1
    item = dict(ms=10, kind=5, value=100, detail=90, eastCm=0, upCm=0, northCm=0)
    first = dict(version=1, pageNumber=0, pages=2, dropped=0, events=[item]*12)
    second = dict(first, pageNumber=1, events=[dict(item, ms=20)])
    path = "diagnostics/events"
    call(base, "POST", path, token=token, body=first, code=401)
    call(base, "POST", path, token=permit, body=dict(first, version=2), code=400)
    call(base, "POST", path, token=permit, body=dict(first, events=[dict(item, kind=9)]*12), code=400)
    call(base, "POST", path, token=permit, body=dict(first, events=[dict(item, eastCm=100)]*12), code=400)
    call(base, "POST", path, token=permit, body=second, code=409)
    call(base, "POST", path, token=permit, body=first, code=204)
    call(base, "POST", path, token=permit, body=first, code=204)
    call(base, "POST", path, token=permit, body=dict(first, dropped=1), code=409)
    call(base, "POST", path, token=permit, body=dict(second, events=[dict(item, ms=5)]), code=409)
    call(base, "POST", path, token=permit, body=second, code=204)
    call(base, "POST", path, token=permit, body=second, code=204)
    size = subprocess.check_output(["docker", "exec", container, "psql", "-U", "spell", "-d", "spell", "-Atqc",
        f"SELECT count(*),sum(jsonb_array_length(events)) FROM spell_diagnostic_events WHERE seat_hash='{digest}'"], text=True)
    assert size.strip() == "2|13"
    print("PASS diagnostic receipt; exactly one counter row, two ordered pages, 13 events; duplicates ignored, unauthorized precision rejected")
    return digest


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("config", type=Path)
    p.add_argument("--container", default="spell-admin-qa")
    args = p.parse_args()
    with server(args.config.resolve()) as base:
        synthetic = exercise(base, args.container)
    # Age only this synthetic seat in the private QA database, then exercise startup cleanup.
    assert len(synthetic) == 64 and all(c in "0123456789ABCDEF" for c in synthetic)
    subprocess.run(["docker", "exec", args.container, "psql", "-U", "spell", "-d", "spell", "-qc",
        f"UPDATE spell_diagnostic_batches SET received_at=now()-interval '8 days' WHERE seat_hash='{synthetic}'"], check=True)
    with server(args.config.resolve()):
        for _ in range(20):
            if count(args.container, synthetic) == 0:
                break
            time.sleep(.25)
        children = subprocess.check_output(["docker", "exec", args.container, "psql", "-U", "spell", "-d", "spell", "-Atqc",
            f"SELECT count(*) FROM spell_diagnostic_events WHERE seat_hash='{synthetic}'"], text=True)
        assert count(args.container, synthetic) == 0 and children.strip() == "0"
    print("PASS seven-day server retention removes counters and event pages after restart")
