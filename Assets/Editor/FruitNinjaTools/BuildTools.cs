// Builds de la Part 1 (Task 4) et relevé de leurs paramètres.
// Windows : Unity.exe -batchmode -buildTarget StandaloneWindows64 -projectPath . -executeMethod FruitNinjaTools.BuildTools.BuildWindows -quit
// Quest   : Unity.exe -batchmode -buildTarget Android -projectPath . -executeMethod FruitNinjaTools.BuildTools.BuildQuest -quit
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace FruitNinjaTools
{
    public static class BuildTools
    {
        [MenuItem("Tools/Fruit Ninja/Build Windows PCVR")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/FruitNinjaVR.exe");

        [MenuItem("Tools/Fruit Ninja/Build Quest (APK)")]
        public static void BuildQuest() =>
            Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Quest/FruitNinjaVR_Quest.apk");

        static void Build(BuildTarget target, BuildTargetGroup group, string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            if (target == BuildTarget.Android)
            {
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC; // recommandé par Meta
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            });

            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"Build {target} -> {output}");
            sb.AppendLine($"Résultat : {s.result} ; durée {s.totalTime:mm\\:ss} ; taille rapportée {s.totalSize} octets ; erreurs {s.totalErrors} ; warnings {s.totalWarnings}");
            sb.AppendLine($"Unity {Application.unityVersion}");
            for (int i = 0; i < scenes.Length; i++) sb.AppendLine($"Scène [{i}] {scenes[i]}");
            AppendSettings(sb, target, group);

            sb.AppendLine("\nPlus gros assets du build (source, octets) :");
            foreach (var a in report.packedAssets.SelectMany(p => p.contents)
                         .GroupBy(c => c.sourceAssetPath).Select(g => (path: g.Key, size: g.Sum(c => (long)c.packedSize)))
                         .OrderByDescending(x => x.size).Take(25))
                sb.AppendLine($"  {a.size,10}  {a.path}");

            foreach (var step in report.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    sb.AppendLine($"ERREUR [{step.name}] {m.content}");

            Directory.CreateDirectory("Logs");
            File.WriteAllText($"Logs/build-{target}.txt", sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[BuildTools]\n" + sb);
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static void AppendSettings(StringBuilder sb, BuildTarget target, BuildTargetGroup group)
        {
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            sb.AppendLine($"Produit : {PlayerSettings.companyName} / {PlayerSettings.productName} {PlayerSettings.bundleVersion}, id {PlayerSettings.GetApplicationIdentifier(named)}");
            sb.AppendLine($"Scripting backend : {PlayerSettings.GetScriptingBackend(named)} ; stripping : {PlayerSettings.GetManagedStrippingLevel(named)} ; API .NET : {PlayerSettings.GetApiCompatibilityLevel(named)}");
            sb.AppendLine($"Graphics APIs : {string.Join(", ", PlayerSettings.GetGraphicsAPIs(target))} (auto : {PlayerSettings.GetUseDefaultGraphicsAPIs(target)}) ; espace couleur : {PlayerSettings.colorSpace}");
            sb.AppendLine($"Qualité active : {QualitySettings.names[QualitySettings.GetQualityLevel()]} ; RP : {QualitySettings.renderPipeline?.name ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline?.name}");
            if (target == BuildTarget.Android)
            {
                sb.AppendLine($"Android : min API {PlayerSettings.Android.minSdkVersion}, cible {PlayerSettings.Android.targetSdkVersion}, archi {PlayerSettings.Android.targetArchitectures}, installation {PlayerSettings.Android.preferredInstallLocation}");
                sb.AppendLine($"Compression des textures : {EditorUserBuildSettings.androidBuildSubtarget} ; multithread rendering : {PlayerSettings.GetMobileMTRendering(named)} ; compression APK : {EditorUserBuildSettings.androidBuildType}");
            }
            else
            {
                sb.AppendLine($"Architecture : {target} ; fenêtre : {PlayerSettings.fullScreenMode} {PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}");
            }
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            sb.AppendLine($"XR : init au démarrage {xr?.InitManagerOnStart} ; loaders {string.Join(", ", xr?.Manager?.activeLoaders.Select(l => l.name) ?? new string[0])}");
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            sb.AppendLine($"OpenXR : rendu {oxr.renderMode} ; features actives : {string.Join(", ", oxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name))}");
        }
    }
}
