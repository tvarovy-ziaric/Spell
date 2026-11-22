"""Build and test Spell from the checkout, with an ephemeral loopback server."""
import os
from pathlib import Path
import queue
import re
import secrets
import subprocess
import sys
import tempfile
import threading

ROOT = Path(__file__).resolve().parents[1]
SDK = os.environ.get("SPELL_DOTNET", "dotnet")


def run(*args, **kwargs):
    subprocess.run(args, cwd=ROOT, check=True, **kwargs)


def main():
    fixtures = ROOT / "contracts" / "fixtures"
    with tempfile.TemporaryDirectory() as tmp:
        target = Path(tmp) / "hill.bytes"
        run(sys.executable, "terrain/arena.py", "fixture", str(target))
        for name in ("hill.bytes", "hill.sha256"):
            if (target.parent / name).read_bytes() != (fixtures / name).read_bytes():
                raise ValueError(f"Non-reproducible fixture: {name}")
    run(SDK, "build", "qa/Spell.Checks.csproj", "-c", "Release", "--nologo")
    run(SDK, "run", "--project", "terrain/tests/GeometryChecks.csproj", "-c", "Release",
        "--", str(fixtures / "hill.bytes"))
    digest = (fixtures / "hill.sha256").read_text().strip()
    env = dict(os.environ, SPELL_FOUNDER_KEY=secrets.token_hex(32))
    proc = subprocess.Popen([SDK, "server/bin/Release/net10.0/Spell.Server.dll", "--arena",
                             str(fixtures / "hill.bytes"), "--digest", digest,
                             "--urls", "http://127.0.0.1:0"], cwd=ROOT, env=env,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                            errors="replace")
    addresses = queue.Queue()

    def watch():
        for line in proc.stdout:
            match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", line)
            if match:
                addresses.put(match[1])
        addresses.put(None)

    monitor = threading.Thread(target=watch, daemon=True)
    monitor.start()
    try:
        address = addresses.get(timeout=30)
        if not address:
            raise RuntimeError("Diagnostic server exited during startup.")
        run(SDK, "qa/bin/Release/net10.0/Spell.Checks.dll", str(fixtures), address, env=env)
    finally:
        proc.terminate()
        proc.wait(timeout=10)
        monitor.join(timeout=3)
        proc.stdout.close()


if __name__ == "__main__":
    main()
