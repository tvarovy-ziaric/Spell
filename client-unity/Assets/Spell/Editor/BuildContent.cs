using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Spell.Client.Preflight;

namespace Spell.Editor
{
    public sealed class BuildContent : BuildPlayerProcessor, IPostGenerateGradleAndroidProject, IPreprocessBuildWithReport
    {
        public override int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android || EditorUserBuildSettings.buildAppBundle ||
                EditorUserBuildSettings.exportAsGoogleAndroidProject) return;
            if (!string.Equals(Path.GetFullPath(report.summary.outputPath), AndroidBuild.ReservedOutput, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("APK vyžaduje číslo buildu. Použi Spell → Build reserved APK.");
        }

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../../contracts/fixtures"));
            string content = Path.Combine(fixture, "hill.bytes");
            string checksum = Path.Combine(fixture, "hill.sha256");
            ArenaPreflight.Load(File.ReadAllBytes(content), File.ReadAllText(checksum).Trim());
            context.AddAdditionalPathToStreamingAssets(content, "fixture/hill.bytes");
            context.AddAdditionalPathToStreamingAssets(checksum, "fixture/hill.sha256");
        }

        public void OnPostGenerateGradleAndroidProject(string libraryPath)
        {
            string path = Path.Combine(libraryPath, "src/main/AndroidManifest.xml");
            XDocument document = XDocument.Load(path);
            var root = document.Root;
            XNamespace android = "http://schemas.android.com/apk/res/android";
            foreach (string name in new[] { "android.permission.ACCESS_COARSE_LOCATION", "android.permission.ACCESS_FINE_LOCATION" })
                if (!root.Elements("uses-permission").Any(element => (string)element.Attribute(android + "name") == name))
                    root.AddFirst(new XElement("uses-permission", new XAttribute(android + "name", name)));
            document.Save(path);
            // Compress native libraries in the standalone APK delivered via Telegram.
            string launcher = Path.Combine(libraryPath, "../launcher/build.gradle");
            const string packaging = "android.packaging.jniLibs.useLegacyPackaging = true";
            if (!File.ReadAllText(launcher).Contains(packaging))
                File.AppendAllText(launcher, "\n" + packaging + "\n");
        }
    }
}
