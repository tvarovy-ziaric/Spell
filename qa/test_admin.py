"""Integration and restart checks against a dedicated local Spell PostgreSQL instance."""
import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
RULES = dict(hitPoints=120, damage=23, auraPenalty=1, speed=6, range=12, radius=1.5,
             auraWarning=2, auraCollision=.6, fireballEnabled=True)


@contextmanager
def server(config, *server_args):
    sdk = os.environ.get("SPELL_DOTNET", "dotnet")
    environment = dict(os.environ, SPELL_ADMIN_CONFIG=str(config))
    environment.pop("SPELL_POSTGRES", None)
    environment.pop("SPELL_ADMIN_PASSWORD_HASH", None)
    process = subprocess.Popen([sdk, str(ROOT / "server/bin/Release/net10.0/Spell.Server.dll"),
        "--urls", "http://127.0.0.1:0", "--player-hosting", "true", "--arena", "contracts/fixtures/hill.bytes",
        "--digest", (ROOT / "contracts/fixtures/hill.sha256").read_text().strip(), *server_args], cwd=ROOT, env=environment,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")
    try:
        for line in process.stdout:
            match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", line)
            if match:
                yield match[1]
                break
        else:
            raise RuntimeError("Spell server failed to start. Check the test database and local SDK.")
    finally:
        process.terminate()
        process.wait(timeout=10)


def call(origin, method, path, *, code=200, token=None, body=None, headers=None):
    headers = dict(headers or {})
    headers.setdefault("User-Agent", "Spell-Android-QA/1.0")
    if body is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(origin + "/v1/" + path, method=method, headers=headers,
                                 data=json.dumps(body).encode() if body is not None else None)
    try:
        with urllib.request.urlopen(req, timeout=8) as response:
            assert code == response.status, (path.split("?")[0], response.status, code)
            raw = response.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        assert e.code == code, (path.split("?")[0], e.code, code)


def run(config):
    password = config.with_name("founder-password.txt").read_text().strip()
    identity = secrets.token_hex(32)
    login = dict(password=password, deviceSecret=identity)
    with server(config) as base:
        call(base, "POST", "admin/grant", body=dict(login, password="wrong"), code=401)
        call(base, "GET", "admin/presets", code=401)
        a = call(base, "POST", "admin/grant", body=login)["token"]
        b = call(base, "POST", "admin/grant", body=dict(login, deviceSecret=secrets.token_hex(32)))["token"]
        def put(body, code=200):
            return call(base, "POST", "admin/presets", token=a, body=body, code=code)
        body = dict(version=1, requestId=str(uuid.uuid4()), rules=RULES)
        put(dict(body, version=2), 400)
        put(dict(body, rules=dict(RULES, auraCollision=3)), 400)
        put(dict(body, rules={k: v for k, v in RULES.items() if k != "damage"}), 400)
        first = put(body)
        assert put(body)["id"] == first["id"]
        put(dict(body, rules=dict(RULES, speed=18)), 409)
        for i in range(11):
            put(dict(body, requestId=str(uuid.uuid4()), rules=dict(RULES, hitPoints=200+i)))
        p1 = call(base, "GET", "admin/presets", token=a)
        p2 = call(base, "GET", "admin/presets?before=" + str(p1["nextBefore"]), token=a)
        assert p1["version"] == 1 and len(p1["items"]) == 10 and len(p2["items"]) == 2
        assert p2["nextBefore"] == 0 and p2["items"][-1]["id"] == first["id"]
        assert len({r["id"] for r in p1["items"] + p2["items"]}) == 12
        assert call(base, "GET", "admin/presets", token=b)["items"] == []
        headers = {"Idempotency-Key": uuid.uuid4().hex, "Spell-Preset": first["id"]}
        call(base, "POST", "session", headers=headers, code=401)
        call(base, "POST", "session", headers=dict(headers, **{"Spell-Grant": b}), code=404)
        created = call(base, "POST", "session", headers=dict(headers, **{"Spell-Grant": a}))
        ticket = created["player"]
        assert ticket["preset"] == first["id"] and ticket["rulesVersion"] == 1 and ticket["rules"] == RULES
        # Retry with another selection must not change the immutable lobby.
        call(base, "POST", "session", headers={"Idempotency-Key": headers["Idempotency-Key"]}, code=409)
        guest = call(base, "POST", "join", body=dict(code=created["invitation"]["payload"].rsplit("/", 1)[1],
                                                     attempt=uuid.uuid4().hex))
        assert guest["rules"] == ticket["rules"] and guest["preset"] == ticket["preset"]
        call(base, "GET", "admin/presets", token=guest["seat"]["token"], code=401)
        call(base, "DELETE", "session", token=ticket["seat"]["token"], code=204)
    with server(config) as base:
        page = call(base, "GET", "admin/presets", token=a)
        assert len(page["items"]) == 10
        call(base, "DELETE", "admin/grant", token=a, code=204)
        call(base, "GET", "admin/presets", token=a, code=401)
        again = call(base, "POST", "admin/grant", body=login)["token"]
        assert call(base, "GET", "admin/presets", token=again)["items"] == page["items"]
    print("PASS PostgreSQL preset history, isolation, pagination, retry, immutable duel snapshot, restart and revocation")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("config", type=Path, help="Private config of the QA database")
    run(p.parse_args().config.resolve())
