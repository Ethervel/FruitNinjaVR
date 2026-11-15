// Configuration XR et Player pour Meta Quest 3 (Part 1, Task 4).
// Valeurs : exigences publiques Meta / Unity (voir docs/builds.md), à confirmer avec les paramètres du cours.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.QuestSetup.Run -quit
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace FruitNinjaTools
{
    public static class QuestSetup
    {
        const string ReportPath = "Logs/QuestSetup.txt";

        [MenuItem("Tools/Fruit Ninja/Configurer Android pour Meta Quest")]
        public static void Run()
        {
            var log = new StringBuilder();

            // 1. XR Plug-in Management : loader OpenXR pour Android
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            android.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(android.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
            log.AppendLine($"Loader OpenXR Android : {(assigned ? "assigné" : "déjà présent ou échec")} ; loaders = {string.Join(", ", android.Manager.activeLoaders.Select(l => l.name))}");
            EditorUtility.SetDirty(android); EditorUtility.SetDirty(android.Manager); EditorUtility.SetDirty(perTarget);

            // 2. Features OpenXR
            var oxrAndroid = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            Enable<MetaQuestFeature>(oxrAndroid, true, log);
            Enable<OculusTouchControllerProfile>(oxrAndroid, true, log);
            Enable<MetaQuestTouchPlusControllerProfile>(oxrAndroid, true, log); // manettes Touch Plus du Quest 3
            oxrAndroid.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(oxrAndroid);
            var oxrPc = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            Enable<OculusTouchControllerProfile>(oxrPc, true, log);
            Enable<MetaQuestTouchPlusControllerProfile>(oxrPc, true, log);
            EditorUtility.SetDirty(oxrPc);

            // 3. Player Settings Android
            var named = NamedBuildTarget.Android;
            PlayerSettings.companyName = "JunghoonKIM";
            PlayerSettings.productName = "FruitNinjaVR";
            PlayerSettings.SetApplicationIdentifier(named, "com.JunghoonKIM.FruitNinjaVR");
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetMobileMTRendering(named, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // 4. Qualité Quest (niveau "Mobile", par défaut sur Android) : MSAA 4x recommandé par Meta,
            //    HDR et post-process désactivés (passes plein écran coûteuses), Forward.
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            mobile.msaaSampleCount = 4;
            mobile.supportsHDR = false;
            mobile.renderScale = 1f;
            EditorUtility.SetDirty(mobile);
            var mobileRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
            mobileRenderer.postProcessData = null;
            mobileRenderer.renderingMode = RenderingMode.Forward;
            EditorUtility.SetDirty(mobileRenderer);

            AssetDatabase.SaveAssets();

            // 5. Project Validation (OpenXR) pour Android et PC, correctifs automatiques
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
                Validate(group, log, fix: true);

            Write(log);
        }

        [MenuItem("Tools/Fruit Ninja/Rapport de validation XR")]
        public static void ValidateOnly()
        {
            var log = new StringBuilder();
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
                Validate(group, log, fix: false);
            Write(log);
        }

        static void Validate(BuildTargetGroup group, StringBuilder log, bool fix)
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, group);
            log.AppendLine($"\nValidation OpenXR {group} : {issues.Count} problème(s)");
            foreach (var i in issues)
            {
                log.AppendLine($"  [{(i.error ? "ERREUR" : "warning")}] {i.message}");
                if (fix && i.fixIt != null && i.fixItAutomatic)
                {
                    i.fixIt();
                    log.AppendLine($"    -> corrigé : {i.fixItMessage}");
                }
            }
            if (fix && issues.Count > 0)
            {
                issues.Clear();
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, group);
                log.AppendLine($"  Après correction : {issues.Count} problème(s)");
                foreach (var i in issues) log.AppendLine($"  [{(i.error ? "ERREUR" : "warning")}] {i.message}");
            }
        }

        static void Enable<T>(OpenXRSettings settings, bool on, StringBuilder log) where T : OpenXRFeature
        {
            var f = settings.GetFeature<T>();
            if (f == null) { log.AppendLine($"Feature introuvable : {typeof(T).Name}"); return; }
            f.enabled = on;
            EditorUtility.SetDirty(f);
            log.AppendLine($"Feature {typeof(T).Name} ({settings.name}) : {(on ? "activée" : "désactivée")}");
        }

        static void Write(StringBuilder log)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[QuestSetup]\n" + log);
        }
    }
}
