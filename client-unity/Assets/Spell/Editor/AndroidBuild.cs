using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Spell.Editor
{
    public static class AndroidBuild
    {
        internal static string ReservedOutput { get; private set; }

        [Serializable]
        sealed class Request
        {
            public string versionName;
            public int versionCode;
            public string sourceCommit;
            public string apkPath;
        }

        [MenuItem("Spell/Build reserved APK")]
        public static void Build()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string requestPath = Environment.GetEnvironmentVariable("SPELL_BUILD_REQUEST");
            if (string.IsNullOrEmpty(requestPath) && !Application.isBatchMode)
                requestPath = EditorUtility.OpenFilePanel("Vyber rezerváciu buildu", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "json");
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                if (Application.isBatchMode) throw new BuildFailedException("Chýba SPELL_BUILD_REQUEST.");
                return;
            }
            if (!Path.IsPathRooted(requestPath)) throw new BuildFailedException("Rezervácia vyžaduje absolútnu cestu.");
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (request == null || request.versionCode < 1 ||
                !Regex.IsMatch(request.versionName ?? "", @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$") ||
                !Regex.IsMatch(request.sourceCommit ?? "", "^[0-9a-f]{40}$") ||
                string.IsNullOrEmpty(request.apkPath) || !Path.IsPathRooted(request.apkPath) ||
                Path.GetFileName(request.apkPath) != $"Spell-{request.versionName}-{request.versionCode}.apk")
                throw new BuildFailedException("Neplatná rezervácia Android buildu.");
            if (File.Exists(request.apkPath)) throw new BuildFailedException("APK už existuje. Pokračuj príkazom release.py finish s pôvodnou rezerváciou.");
            if (Run("git", "rev-parse HEAD", root).Trim() != request.sourceCommit ||
                Run("git", "status --porcelain", root).Trim().Length != 0)
                throw new BuildFailedException("Verzia zdrojov sa líši od čistej revízie rezervácie.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("V Build Profiles najskôr prepni aktívny profil na Android.");

            Directory.CreateDirectory(Path.GetDirectoryName(request.apkPath));
            using var reservationLock = new FileStream(request.apkPath + ".build-lock",
                FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            if (File.Exists(request.apkPath)) throw new BuildFailedException("Pre túto rezerváciu už APK existuje.");
            string attempt = Path.Combine(Path.GetDirectoryName(request.apkPath), ".build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attempt);
            string partial = Path.Combine(attempt, Path.GetFileName(request.apkPath));
            string oldVersion = PlayerSettings.bundleVersion;
            int oldCode = PlayerSettings.Android.bundleVersionCode;
            var oldBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                PlayerSettings.bundleVersion = request.versionName;
                PlayerSettings.Android.bundleVersionCode = request.versionCode;
                EditorUserBuildSettings.buildAppBundle = false;
                ReservedOutput = partial;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Spell/Preflight.unity" },
                    locationPathName = partial, target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Zostavenie zlyhalo: {report.summary.result}");
                File.Move(partial, request.apkPath);
            }
            finally
            {
                ReservedOutput = null;
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.Android.bundleVersionCode = oldCode;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                if (File.Exists(partial)) File.Delete(partial);
                if (Directory.GetFileSystemEntries(attempt).Length == 0) Directory.Delete(attempt);
            }

            try { Debug.Log(Run("python", "tools/release.py finish \"" + requestPath + "\"", root)); }
            catch (Exception error)
            {
                throw new BuildFailedException($"APK je zostavené. Dokonči odovzdanie príkazom python tools/release.py finish \"{requestPath}\". {error.Message}");
            }
        }

        static string Run(string executable, string arguments, string directory)
        {
            var start = new ProcessStartInfo(executable, arguments) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new BuildFailedException(errors.GetAwaiter().GetResult());
            return output.GetAwaiter().GetResult();
        }
    }
}
