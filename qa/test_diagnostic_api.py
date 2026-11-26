"""Exercise an integrated local server's batch endpoint and confirm its database effects."""
import argparse
import hashlib
import json
import subprocess
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
    print("PASS diagnostic API version, credentials, offline receipt, duplicate and conflict; exactly one PostgreSQL row")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("config", type=Path)
    p.add_argument("--container", default="spell-admin-qa")
    args = p.parse_args()
    with server(args.config.resolve()) as base:
        exercise(base, args.container)
