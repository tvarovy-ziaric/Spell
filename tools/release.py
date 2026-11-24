"""Reserve a Spell APK, then record, publish and deliver that exact artifact."""

import argparse
from contextlib import contextmanager
from datetime import datetime
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
from urllib.parse import urlencode
import uuid

ROOT = Path(__file__).resolve().parents[1]
STORE = Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local/share")) / "Spell" / "releases"
BOT = "codexer_komunikacia_bot"
PACKAGE = "site.arnavigator.spell"
BUILD_HEADING = re.compile(r"^## [^\n]+ · build (\d+)\s*$", re.M)
EMPTY_NOTE = "Prvé APK sa pripravuje. Po jeho zostavení tu pribudne verzia, zoznam zmien a výsledok skúšok."


def require(condition, message):
    if not condition:
        raise ValueError(message)


def atomic_write(path, text):
    tmp = tempfile.NamedTemporaryFile(dir=path.parent, delete=False, mode="w", encoding="utf-8", newline="\n")
    try:
        with tmp:
            tmp.write(text)
            tmp.flush()
            os.fsync(tmp.fileno())
        os.replace(tmp.name, path)
    finally:
        Path(tmp.name).unlink(missing_ok=True)


def save_json(path, value):
    atomic_write(path, json.dumps(value, indent=2, ensure_ascii=False) + "\n")


@contextmanager
def locked(store):
    store.mkdir(parents=True, exist_ok=True)
    with (store / "release.lock").open("a+b") as lock:
        if lock.tell() == 0:
            lock.write(b"0")
            lock.flush()
        lock.seek(0)
        if os.name == "nt":
            import msvcrt
            msvcrt.locking(lock.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(lock.fileno(), fcntl.LOCK_EX)
        yield


def git(root, *command):
    return subprocess.check_output(["git", *command], cwd=root, text=True).strip()


def reserve(root, store, version, change, verified, status):
    require(re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", version), "Use a numeric release version such as 0.1.0 or 0.1.0-dev.")
    for note in (change, verified, status):
        require(note.strip() and len(note) <= 640 and "\n" not in note and "\r" not in note, "Release notes must be individual non-empty lines up to 640 characters.")
    require(not git(root, "status", "--porcelain"), "Commit the build source before reserving an APK.")
    recorded = BUILD_HEADING.findall((root / "CHANGELOG.md").read_text(encoding="utf-8"))
    reserved = [p.name for p in store.iterdir() if p.is_dir() and p.name.isdecimal()]
    code = max(map(int, [*recorded, *reserved]), default=0) + 1
    require(code <= 2_100_000_000, "Android versionCode exhausted.")
    directory = store.resolve() / f"{code:06}"
    directory.mkdir()
    apk = directory / f"Spell-{version}-{code}.apk"
    request = dict(versionName=version, versionCode=code, sourceCommit=git(root, "rev-parse", "HEAD"),
                   apkPath=str(apk), change=change, verified=verified, status=status)
    path = directory / "request.json"
    save_json(path, request)
    return path


def read_request(store, path):
    path = path.resolve()
    require(path.parent.parent == store.resolve() and path.name == "request.json", "Choose a request from the Spell release ledger.")
    req = json.loads(path.read_text(encoding="utf-8"))
    code, version = req["versionCode"], req["versionName"]
    require(path.parent.name == f"{code:06}" and
            Path(req["apkPath"]) == path.parent / f"Spell-{version}-{code}.apk", "Reservation and APK path differ.")
    return req


def aapt_path():
    if os.environ.get("SPELL_AAPT"):
        return os.environ["SPELL_AAPT"]
    filename = "aapt.exe" if os.name == "nt" else "aapt"
    roots = [x for x in (os.getenv("ANDROID_SDK_ROOT"), os.getenv("ANDROID_HOME")) if x]
    if os.getenv("LOCALAPPDATA"):
        roots.append(str(Path(os.environ["LOCALAPPDATA"]) / "Android/Sdk"))
    for root in roots:
        versions = [x for x in (Path(root) / "build-tools").glob("*/" + filename)
                    if re.fullmatch(r"[0-9]+(?:\.[0-9]+)*", x.parent.name)]
        if versions:
            return str(max(versions, key=lambda x: tuple(map(int, x.parent.name.split(".")))))
    require(shutil.which(filename), "Supply SPELL_AAPT from Android SDK build-tools.")
    return filename


def inspect_apk(req):
    path = Path(req["apkPath"])
    aapt = aapt_path()
    output = subprocess.check_output([aapt, "dump", "badging", str(path)], encoding="utf-8", errors="replace")
    package = next((line for line in output.splitlines() if line.startswith("package: ")), "")
    metadata = dict(re.findall(r"(\w+)='([^']*)'", package))
    require(metadata.get("name") == PACKAGE and metadata.get("versionName") == req["versionName"] and
            metadata.get("versionCode") == str(req["versionCode"]), "Embedded APK package or version differs from the reservation.")
    build_tools = Path(shutil.which(aapt) or aapt).parent
    java_home = os.environ.get("JAVA_HOME")
    java = str(Path(java_home) / "bin" / ("java.exe" if os.name == "nt" else "java")) if java_home else "java"
    subprocess.check_output([java, "-jar", str(build_tools / "lib/apksigner.jar"), "verify", str(path)])
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def record(root, req, date, digest):
    version, code = req["versionName"], req["versionCode"]
    path = root / "CHANGELOG.md"
    log = path.read_text(encoding="utf-8")
    codes = [int(x) for x in BUILD_HEADING.findall(log)]
    title = f"## {version} · build {code}"
    section = (f"{title}\n\n- **Dátum vydania** {date}\n- **Zdrojový commit** `{req['sourceCommit']}`\n"
               f"- **APK** `{Path(req['apkPath']).name}`\n- **SHA-256** `{digest}`\n"
               f"- **Zmeny** {req['change']}\n- **Overené** {req['verified']} Skontrolované boli aj identifikátor a verzia v manifeste APK.\n")
    if code not in codes:
        log = log.replace(EMPTY_NOTE, "").rstrip() + "\n"
        following = next((m.start() for m in BUILD_HEADING.finditer(log) if int(m[1]) < code), len(log))
        before, after = log[:following].rstrip(), log[following:].lstrip()
        atomic_write(path, before + "\n\n" + section + ("\n" + after if after else ""))
    else:
        require(section.rstrip() in log, "Existing changelog entry differs from the recorded artifact.")
    if code >= max(codes, default=0):
        state_file = root / "web/status.md"
        content = state_file.read_text(encoding="utf-8").split("\n## Posledné APK\n", 1)[0].rstrip()
        atomic_write(state_file, f"{content}\n\n## Posledné APK\n\n**{version} · build {code}**. {req['status']}\n")


def telegram_credentials():
    configured = os.environ.get("SPELL_TELEGRAM_CONFIG")
    if not configured and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
            try:
                configured = winreg.QueryValueEx(key, "SPELL_TELEGRAM_CONFIG")[0]
            except FileNotFoundError:
                pass
    require(configured, "Set SPELL_TELEGRAM_CONFIG to the operator's private properties file.")
    path = Path(configured).resolve()
    require(not path.is_relative_to(ROOT), "Keep Telegram credentials outside the Spell repository.")
    data = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        m = re.fullmatch(r"\s*(TELEGRAM_BOT_TOKEN|TELEGRAM_CHAT_ID)\s*[:=]\s*(.+?)\s*", line)
        if m:
            data[m[1]] = m[2].strip('"').replace(r"\:", ":")
    token, chat = data.get("TELEGRAM_BOT_TOKEN", ""), data.get("TELEGRAM_CHAT_ID", "")
    require(re.fullmatch(r"\d+:[\w-]+", token) and re.fullmatch(r"\d+", chat), "Private bot configuration is incomplete.")
    return token, chat


class Rejected(Exception):
    """Telegram explicitly confirms the request failed."""


def api(token, method, body, headers):
    local_port = os.environ.get("SPELL_TELEGRAM_LOCAL_API_PORT")
    connection = (http.client.HTTPConnection("127.0.0.1", int(local_port), timeout=600) if local_port
                  else http.client.HTTPSConnection("api.telegram.org", timeout=180))
    try:
        connection.request("POST", f"/bot{token}/{method}", body=body, headers=headers)
        response = connection.getresponse()
        result = json.loads(response.read())
        if result.get("ok") is False:
            raise Rejected(f"Telegram rejected {method} (HTTP {response.status}).")
        require(response.status == 200 and result.get("ok") is True, "Unconfirmed Telegram response.")
        return result["result"]
    finally:
        connection.close()


def verify_destination(token, chat):
    headers = {"Content-Type": "application/x-www-form-urlencoded"}
    bot = api(token, "getMe", b"", headers)
    require(bot.get("is_bot") and bot.get("username") == BOT, "Bot identity does not match codexer.")
    target = api(token, "getChat", urlencode({"chat_id": chat}), headers)
    require(target.get("type") == "private" and str(target.get("id")) == chat, "Private chat identity differs.")


def send_apk(token, chat, req):
    apk = Path(req["apkPath"])
    boundary = uuid.uuid4().hex
    caption = f"Spell {req['versionName']} · build {req['versionCode']}\n{req['change']}"
    form = []
    for field, value in (("chat_id", chat), ("caption", caption)):
        form.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"\r\n\r\n{value}\r\n')
    form.append(f'--{boundary}\r\nContent-Disposition: form-data; name="document"; filename="{apk.name}"\r\nContent-Type: application/vnd.android.package-archive\r\n\r\n')
    prefix, suffix = "".join(form).encode(), f"\r\n--{boundary}--\r\n".encode()
    size = apk.stat().st_size
    with apk.open("rb") as stream:
        def body():
            yield prefix
            yield from iter(lambda: stream.read(1 << 16), b"")
            yield suffix
        result = api(token, "sendDocument", body(), {"Content-Type": "multipart/form-data; boundary=" + boundary,
                                                     "Content-Length": str(len(prefix) + size + len(suffix))})
    doc = result.get("document", {})
    require(str(result.get("chat", {}).get("id")) == chat and doc.get("file_name") == apk.name and
            doc.get("file_size") == size and result.get("message_id", 0) > 0, "Unconfirmed Telegram document receipt.")
    return result["message_id"]


def finish(root, path, req, inspect=inspect_apk, credentials=telegram_credentials,
           verify=verify_destination, send=send_apk, publish=None):
    git(root, "merge-base", "--is-ancestor", req["sourceCommit"], "HEAD")
    ledger = path.parent / "delivery.json"
    digest = inspect(req)
    if ledger.exists():
        state = json.loads(ledger.read_text(encoding="utf-8"))
        require(state["sha256"] == digest, "APK changed after it was recorded.")
    else:
        state = dict(sha256=digest, date=datetime.now().astimezone().date().isoformat(), delivery="ready")
        save_json(ledger, state)
    record(root, req, state["date"], digest)
    public_error = None
    try:
        if publish is None:
            for action in ("publish", "verify"):
                subprocess.run([sys.executable, str(root / "web/site.py"), action], check=True)
        else:
            publish()
    except (OSError, RuntimeError, subprocess.CalledProcessError) as exc:
        public_error = exc
    try:
        deliver(req, ledger, state, credentials, verify, send)
    except Exception:
        if public_error:
            print(f"Site publication also requires retry: {public_error}", file=sys.stderr)
        raise
    if public_error:
        raise RuntimeError("Telegram delivery confirmed. Retry finish to complete site publication.") from public_error


def deliver(req, ledger, state, credentials, verify, send):
    if state["delivery"] == "sent":
        return
    require(state["delivery"] == "ready", "Previous upload has an unknown result. Check the exact APK in codexer before resolving.")
    limit_mib = 2000 if os.environ.get("SPELL_TELEGRAM_LOCAL_API_PORT") else 50
    require(Path(req["apkPath"]).stat().st_size <= limit_mib * 1024**2, "APK exceeds the configured Telegram API size limit. The original artifact and its release entry have been retained.")
    token, chat = credentials()
    verify(token, chat)
    state["delivery"] = "sending"
    save_json(ledger, state)
    try:
        receipt = send(token, chat, req)
    except Rejected:
        state["delivery"] = "ready"
        save_json(ledger, state)
        raise
    state.update(delivery="sent", messageId=receipt)
    save_json(ledger, state)


def resolve(path, message_id):
    ledger = path.parent / "delivery.json"
    state = json.loads(ledger.read_text(encoding="utf-8"))
    require(state["delivery"] == "sending", "No ambiguous upload to resolve.")
    if message_id is None:
        state["delivery"] = "ready"
    else:
        require(message_id > 0, "Expected the positive ID of the confirmed document message.")
        state.update(delivery="sent", messageId=message_id)
    save_json(ledger, state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_subparsers(dest="action", required=True)
    plan = actions.add_parser("reserve")
    plan.add_argument("version")
    for field in ("change", "verified", "status"):
        plan.add_argument("--" + field, required=True)
    actions.add_parser("finish").add_argument("request", type=Path)
    check = actions.add_parser("resolve")
    check.add_argument("request", type=Path)
    choice = check.add_mutually_exclusive_group(required=True)
    choice.add_argument("--message-id", type=int)
    choice.add_argument("--confirmed-absent", action="store_true")
    args = parser.parse_args()
    with locked(STORE):
        if args.action == "reserve":
            print(reserve(ROOT, STORE, args.version, args.change, args.verified, args.status))
        else:
            req = read_request(STORE, args.request)
            if args.action == "finish":
                finish(ROOT, args.request, req)
                print(f"Recorded and delivered Spell {req['versionName']} build {req['versionCode']}.")
            else:
                resolve(args.request, args.message_id)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, OSError, Rejected, subprocess.CalledProcessError, http.client.HTTPException) as exc:
        sys.exit(str(exc))
