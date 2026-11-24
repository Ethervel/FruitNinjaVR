// Génération du contenu Addressables + builds joueur (Part 2, Tasks 3-4).
// Le groupe de chaque plateforme n'est inclus que dans son build (SetPlatform),
// et le profil actif fixe les chemins des bundles (PCVR/ ou Quest/).
//
//   Unity.exe -batchmode -quit -projectPath . -executeMethod FruitNinjaTools.AddressablesBuild.BuildContentPCVR -logFile <log>
//   Unity.exe -batchmode -quit -buildTarget Android -projectPath . -executeMethod FruitNinjaTools.AddressablesBuild.BuildContentQuest -logFile <log>
//   puis BenchTools.BuildWindowsDevBench / BuildQuestDevBench (build joueur, même cible active).
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class AddressablesBuild
    {
        [MenuItem("Tools/Fruit Ninja/Contenu Addressables PCVR")]
        public static void BuildContentPCVR() => BuildContent("PCVR");

        [MenuItem("Tools/Fruit Ninja/Contenu Addressables Quest")]
        public static void BuildContentQuest() => BuildContent("Quest");

        static void BuildContent(string platform)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                Debug.LogError("[AddressablesBuild] Settings absents : lancer AddressablesSetup.Run d'abord.");
                EditorApplication.Exit(1);
                return;
            }
            AddressablesSetup.SetPlatform(platform);
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[AddressablesBuild] ÉCHEC contenu {platform} : {result.Error}");
                EditorApplication.Exit(1);
                return;
            }
            int files = 0;
            foreach (var f in result.FileRegistry.GetFilePaths()) files++;
            Debug.Log($"[AddressablesBuild] Contenu {platform} généré : {files} fichiers, " +
                      $"{result.LocationCount} locations, {result.OutputPath}, durée {result.Duration:F1}s");
        }
    }
}