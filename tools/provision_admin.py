"""Provision Spell's private, loopback-only PostgreSQL on the game host."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import time


def docker(*args, env=None, check=True):
    return subprocess.run(["docker", *args], env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                          text=True, check=check)


def protect_file(path):
    if os.name != "nt":
        os.chmod(path, 0o600)
        return
    owner = subprocess.check_output(["whoami"], text=True).strip()
    subprocess.run(["icacls", str(path), "/inheritance:r", "/grant:r", owner + ":F",
                    "*S-1-5-18:F", "*S-1-5-32-544:F"], check=True, stdout=subprocess.DEVNULL)


def provision(directory, name, port):
    directory.mkdir(parents=True, exist_ok=True)
    config = directory / "admin.config.json"
    if config.exists():
        raise SystemExit(f"Existing configuration preserved in {directory}.")
    volume = name + "-data"
    if docker("container", "inspect", name, check=False).returncode == 0 or docker("volume", "inspect", volume, check=False).returncode == 0:
        raise SystemExit("Container or volume exists. Recover the existing private configuration before provisioning.")
    db_password = secrets.token_hex(32)
    password = secrets.token_urlsafe(20)
    salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 210000)
    # Persist recovery credentials before creating the durable volume. Do not log secret values.
    private = directory / "founder-password.txt"
    with private.open("x", encoding="utf8") as out:
        protect_file(private)
        out.write(password + "\n")
    with config.open("x", encoding="utf8") as out:
        protect_file(config)
        json.dump(dict(postgres=f"Host=127.0.0.1;Port={port};Database=spell;Username=spell;Password={db_password}",
                       passwordHash=":".join(base64.b64encode(x).decode() for x in (salt, digest))), out)
    env = dict(os.environ, POSTGRES_PASSWORD=db_password)
    docker("run", "--detach", "--name", name, "--restart", "unless-stopped", "-p", f"127.0.0.1:{port}:5432",
           "--mount", f"type=volume,source={volume},target=/var/lib/postgresql/data",
           "-e", "POSTGRES_USER=spell", "-e", "POSTGRES_DB=spell", "-e", "POSTGRES_PASSWORD", "postgres:16-alpine", env=env)
    for _ in range(60):
        if docker("exec", name, "pg_isready", "-h", "127.0.0.1", "-U", "spell", check=False).returncode == 0:
            print(f"Local PostgreSQL ready. Private configuration in {directory}.")
            return
        time.sleep(1)
    raise SystemExit("Container started. Check local PostgreSQL readiness before enabling admin.")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--directory", type=Path, required=True)
    p.add_argument("--name", default="spell-postgres")
    p.add_argument("--port", type=int, default=18875)
    args = p.parse_args()
    provision(args.directory, args.name, args.port)
