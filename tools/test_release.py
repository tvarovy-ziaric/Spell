"""Release recovery checks. All artifacts and chat transports in this suite are synthetic."""

import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

import release


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name) / "repo"
        self.root.mkdir()
        self.store = Path(tmp.name) / "ledger"
        self.store.mkdir()
        (self.root / "web").mkdir()
        (self.root / "web/status.md").write_text("# Stav\n\nPôvodný výsledok.\n", encoding="utf-8")
        (self.root / "CHANGELOG.md").write_text("# Changelog APK\n\n" + release.EMPTY_NOTE + "\n", encoding="utf-8")
        for cmd in (["init", "-q"], ["add", "."], ["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                                                    "-c", "commit.gpgsign=false", "commit", "-qm", "fixture"]):
            subprocess.run(["git", *cmd], cwd=self.root, check=True, capture_output=True)
        self.path, self.req = self.reserve()
        self.send = Mock(return_value=23)
        self.publish = Mock()

    def reserve(self):
        with release.locked(self.store):
            path = release.reserve(self.root, self.store, "0.1.0-dev", "Skúšobná zmena.", "Skúšobný výsledok.", "Stav tejto zostavy.")
        req = release.read_request(self.store, path)
        Path(req["apkPath"]).write_bytes(b"synthetic test data")
        return path, req

    def finish(self):
        release.finish(self.root, self.path, self.req,
                       inspect=lambda q: hashlib.sha256(Path(q["apkPath"]).read_bytes()).hexdigest(),
                       credentials=lambda: ("fake", "fake"), verify=lambda *_: None,
                       send=self.send, publish=self.publish)

    def test_versions_and_clean_source(self):
        second_path, second = self.reserve()
        self.assertEqual(second["versionCode"], self.req["versionCode"] + 1)
        self.assertNotEqual(second_path, self.path)
        (self.root / "dirty").touch()
        with self.assertRaises(ValueError):
            self.reserve()

    def test_duplicate_finish(self):
        self.finish()
        self.finish()
        self.send.assert_called_once()
        log = (self.root / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertEqual(len(release.BUILD_HEADING.findall(log)), 1)
        self.assertIn(self.req["sourceCommit"], log)
        self.assertIn("Pôvodný výsledok.", (self.root / "web/status.md").read_text(encoding="utf-8"))
        self.assertEqual(json.loads((self.path.parent / "delivery.json").read_text())["messageId"], 23)

    def test_ambiguous_upload(self):
        self.send.side_effect = TimeoutError("injected lost reply")
        with self.assertRaises(TimeoutError):
            self.finish()
        with self.assertRaisesRegex(ValueError, "unknown result"):
            self.finish()
        release.resolve(self.path, 72)
        self.finish()
        self.send.assert_called_once()

    def test_rejected_then_retry(self):
        self.send.side_effect = [release.Rejected("injected rejection"), 19]
        with self.assertRaises(release.Rejected):
            self.finish()
        self.finish()
        self.assertEqual(self.send.call_count, 2)

    def test_publish_failure_still_delivers(self):
        self.publish.side_effect = [OSError("injected outage"), None]
        with self.assertRaisesRegex(RuntimeError, "Telegram delivery confirmed"):
            self.finish()
        self.finish()
        self.send.assert_called_once()

    def test_apk_replacement(self):
        self.finish()
        Path(self.req["apkPath"]).write_bytes(b"different")
        with self.assertRaisesRegex(ValueError, "APK changed"):
            self.finish()
        self.send.assert_called_once()

    def test_manifest_identity(self):
        with patch.object(release, "aapt_path", return_value="fake-aapt"), patch.object(release.subprocess, "check_output") as output:
            output.return_value = f"package: name='{release.PACKAGE}' versionCode='1' versionName='0.1.0-dev'\n"
            self.assertEqual(len(release.inspect_apk(self.req)), 64)
            output.return_value = "package: name='other' versionCode='1' versionName='0.1.0-dev'\n"
            with self.assertRaisesRegex(ValueError, "Embedded APK"):
                release.inspect_apk(self.req)


if __name__ == "__main__":
    unittest.main()
