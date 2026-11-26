"""Verify the Android protobuf runtime and generated v1 messages against the server schema."""
import hashlib
import os
from pathlib import Path
import platform
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
lib = ROOT / "client-unity/Assets/Plugins/SpellProtocol"
if hashlib.sha256((lib / "Google.Protobuf.dll").read_bytes()).hexdigest() != "47f3d4534309a33fec7241e995151935628e54511142515a647d67d74710d67a":
    raise ValueError("Android protobuf runtime differs from the netstandard2.0 assembly in Google.Protobuf 3.36.2")
packages = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
version = ET.parse(ROOT / "server/Spell.Server.csproj").find(".//PackageReference[@Include='Grpc.Tools']").get("Version")
system = {"Windows": "windows", "Linux": "linux", "Darwin": "macosx"}[platform.system()]
machine = "arm64" if platform.machine().lower() in ("arm64", "aarch64") else "x64"
tool = packages / "grpc.tools" / version / "tools" / f"{system}_{machine}" / ("protoc.exe" if system == "windows" else "protoc")
with tempfile.TemporaryDirectory() as directory:
    subprocess.run([str(tool), "--proto_path=contracts", "--csharp_out=" + directory, "contracts/spell_v1.proto"], cwd=ROOT, check=True)
    if (Path(directory) / "SpellV1.cs").read_text(encoding="utf-8") != (lib / "SpellV1.cs").read_text(encoding="utf-8"):
        raise ValueError("Regenerate the Android messages from contracts/spell_v1.proto with the server's protoc")
print("PASS Android and server schema parity and pinned runtime")
