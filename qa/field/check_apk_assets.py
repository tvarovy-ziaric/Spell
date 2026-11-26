"""Verify M2 field materials in a signed APK built from the current checkout."""

import argparse
import hashlib
import re
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / "client-unity/Assets/Spell/Resources/SpellBoards.asset"
FIXTURES = ROOT / "contracts/fixtures"


def check(apk):
    metadata = LIBRARY.with_suffix(".asset.meta").read_text(encoding="utf-8")
    guid = re.search(r"^guid: ([0-9a-f]{32})$", metadata, re.MULTILINE)
    database = re.search(
        r"key: com\.unity\.xr\.arcore\s+value: ([0-9a-f]+)",
        LIBRARY.read_text(encoding="utf-8"),
    )
    if not guid or not database:
        raise ValueError("Missing reference ARCore database in this checkout")
    with ZipFile(apk) as package:
        library = package.read(f"assets/bin/Data/{guid[1]}")
        if bytes.fromhex(database[1]) not in library:
            raise ValueError("Packaged ARCore marker database differs from the reference")
        terrain = package.read("assets/fixture/hill.bytes")
        digest = package.read("assets/fixture/hill.sha256").decode("ascii").split()[0]
        if terrain != (FIXTURES / "hill.bytes").read_bytes() or hashlib.sha256(terrain).hexdigest() != digest:
            raise ValueError("Packaged terrain or its SHA-256 differs from the reference")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    check(parser.parse_args().apk)
    print("PASS: packaged ARCore marker database, diagnostic terrain and SHA-256")
