using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Spell.Editor
{
    public sealed class InvitationQrBuild : BuildPlayerProcessor, IPostGenerateGradleAndroidProject
    {
        const string Activity = "site.arnavigator.spell.InvitationScanActivity";
        const string Dependency = "dependencies { implementation 'com.google.zxing:core:3.5.4' }";
        const string BridgeRules = "-keep class site.arnavigator.spell.InvitationScanActivity { public static *; }";

        public override int callbackOrder => 10;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            if (context.BuildPlayerOptions.target != BuildTarget.Android) return;
            foreach (string name in new[] { "ZXing-LICENSE.txt", "ZXing-NOTICE.txt", "Protobuf-LICENSE.txt" })
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../ThirdParty", name));
                context.AddAdditionalPathToStreamingAssets(path, "legal/" + name);
            }
        }

        public void OnPostGenerateGradleAndroidProject(string libraryPath)
        {
            AppendOnce(Path.Combine(libraryPath, "build.gradle"), Dependency);
            AppendOnce(Path.Combine(libraryPath, "proguard-unity.txt"), BridgeRules);
            string path = Path.Combine(libraryPath, "src/main/AndroidManifest.xml");
            var document = XDocument.Load(path);
            var manifest = document.Root;
            XNamespace android = "http://schemas.android.com/apk/res/android";
            if (!manifest.Elements("uses-permission").Any(element => (string)element.Attribute(android + "name") == "android.permission.CAMERA"))
                manifest.AddFirst(new XElement("uses-permission", new XAttribute(android + "name", "android.permission.CAMERA")));
            var application = manifest.Element("application");
            if (application == null) throw new BuildFailedException("Android manifest nemá element application.");
            if (!application.Elements("activity").Any(element => (string)element.Attribute(android + "name") == Activity))
                application.Add(new XElement("activity", new XAttribute(android + "name", Activity),
                    new XAttribute(android + "exported", "false"),
                    new XAttribute(android + "screenOrientation", "portrait"),
                    new XAttribute(android + "theme", "@android:style/Theme.Material.NoActionBar")));
            document.Save(path);
        }

        static void AppendOnce(string path, string line)
        {
            if (!File.ReadAllText(path).Contains(line)) File.AppendAllText(path, "\n" + line + "\n");
        }
    }
}
