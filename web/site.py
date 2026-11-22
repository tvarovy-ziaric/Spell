"""Publish the public changelog and serve its single HTML page on loopback."""

import argparse
import http.client
import os
import re
import tempfile
from functools import partial
from html import escape
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

WEB = Path(__file__).resolve().parent


def inline(text):
    """Format the two inline styles used by the release notes, escaping all text."""
    pieces = re.split(r"(`[^`]+`|\*\*[^*]+\*\*)", text)
    result = []
    for part in pieces:
        if len(part) > 1 and part.startswith("`") and part.endswith("`"):
            result.append(f"<code>{escape(part[1:-1])}</code>")
        elif len(part) > 3 and part.startswith("**") and part.endswith("**"):
            result.append(f"<strong>{escape(part[2:-2])}</strong>")
        else:
            result.append(escape(part))
    return "".join(result)


def render_notes(source):
    """Render the heading, paragraph and bullet conventions of our two Markdown files."""
    html = []
    group = None
    lines = []

    def flush():
        if group == "list" and lines:
            html.append("<ul>" + "".join(f"<li>{inline(x)}</li>" for x in lines) + "</ul>")
        elif lines:
            html.append(f"<p>{inline(' '.join(lines))}</p>")
        lines.clear()

    for line in source.splitlines():
        line = line.strip()
        if not line or line.startswith("# "):
            flush()
            group = None
        elif line.startswith(("## ", "### ")):
            flush()
            group = None
            tag = "h3" if line.startswith("## ") else "h4"
            html.append(f"<{tag}>{inline(line.split(' ', 1)[1])}</{tag}>")
        else:
            next_group = "list" if line.startswith("- ") else "paragraph"
            if group != next_group:
                flush()
                group = next_group
            lines.append(line[2:] if group == "list" else line)
    flush()
    return "\n".join(html)


def publish(target):
    template = (WEB / "template.html").read_text(encoding="utf-8")
    values = {
        "STATUS": WEB / "status.md",
        "BUILDS": WEB.parent / "CHANGELOG.md",
    }
    page = re.sub(r"\{\{(STATUS|BUILDS)\}\}", lambda m: render_notes(
        values[m[1]].read_text(encoding="utf-8-sig")), template)
    target.parent.mkdir(parents=True, exist_ok=True)
    tmp = tempfile.NamedTemporaryFile(dir=target.parent, delete=False)
    temp_path = Path(tmp.name)
    try:
        with tmp:
            tmp.write(page.encode("utf-8"))
        os.replace(temp_path, target)
    finally:
        temp_path.unlink(missing_ok=True)


def verify_public(target):
    conn = http.client.HTTPSConnection("spell.arnavigator.site", timeout=15)
    try:
        conn.request("GET", "/", headers={"Cache-Control": "no-cache"})
        response = conn.getresponse()
        if response.status != 200 or not response.getheader("CF-Ray") or response.read() != target.read_bytes():
            raise RuntimeError("Public HTTPS response differs from the published Spell page.")
    finally:
        conn.close()


class PageHandler(BaseHTTPRequestHandler):
    def __init__(self, *args, page, **kwargs):
        self.page = page
        super().__init__(*args, **kwargs)

    def do_HEAD(self):
        self.send_page(head_only=True)

    def do_GET(self):
        self.send_page(head_only=False)

    def send_page(self, head_only):
        if self.path.partition("?")[0] not in ("/", "/index.html"):
            self.send_error(404)
            return
        try:
            body = self.page.read_bytes()
        except OSError:
            self.send_error(503)
            return
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'")
        self.end_headers()
        if not head_only:
            self.wfile.write(body)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("publish", "verify", "serve"))
    default_dir = Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local" / "share"))
    parser.add_argument("--page", type=Path, default=default_dir / "Spell" / "status" / "index.html")
    parser.add_argument("--port", type=int, default=18873)
    args = parser.parse_args()
    if args.action == "publish":
        publish(args.page)
        print(args.page)
    elif args.action == "verify":
        verify_public(args.page)
        print("Public Spell page verified.")
    else:
        with ThreadingHTTPServer(("127.0.0.1", args.port), partial(PageHandler, page=args.page)) as server:
            server.serve_forever()
