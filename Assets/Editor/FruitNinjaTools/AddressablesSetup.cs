// Mise en place d'Addressables (Part 2, Task 3) : settings, groupes FX_PCVR / FX_Quest,
// adresses et labels. Le groupe de chaque plateforme n'est inclus que dans SON build
// (IncludeInBuild piloté par SetPlatform avant chaque build de contenu).
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.AddressablesSetup.Run -quit
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class AddressablesSetup
    {
        // (chemin de l'asset, groupe, adresse, label)
        static readonly (string path, string group, string address, string label)[] Entries =
        {
            ("Assets/FX/Prefabs/FX_FruitJuice_PCVR.prefab",     "FX_PCVR",  "FX_FruitJuice",     "fx"),
            ("Assets/FX/Prefabs/FX_BombExplosion_PCVR.prefab",  "FX_PCVR",  "FX_BombExplosion",  "fx"),
            ("Assets/Prefabs/Katanas/Katana_PCVR.prefab",       "FX_PCVR",  "Katana",            "katana"),
            ("Assets/FX/Prefabs/FX_FruitJuice_Quest.prefab",    "FX_Quest", "FX_FruitJuice",     "fx"),
            ("Assets/FX/Prefabs/FX_BombExplosion_Quest.prefab", "FX_Quest", "FX_BombExplosion",  "fx"),
            ("Assets/Prefabs/Katanas/Katana_Quest.prefab",      "FX_Quest", "Katana",            "katana"),
        };

        [MenuItem("Tools/Fruit Ninja/Configurer Addressables (Part 2)")]
        public static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            // Profils : un profil de build par plateforme, chemins locaux distincts.
            string pcProfile = EnsureProfile(settings, "PCVR",
                "[UnityEngine.AddressableAssets.Addressables.BuildPath]/PCVR",
                "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/PCVR");
            string questProfile = EnsureProfile(settings, "Quest",
                "[UnityEngine.AddressableAssets.Addressables.BuildPath]/Quest",
                "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/Quest");
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", "ServerData/Quest");

            var pc = EnsureGroup(settings, "FX_PCVR", pcProfile);
            var quest = EnsureGroup(settings, "FX_Quest", questProfile);

            foreach (var e in Entries)
            {
                string guid = AssetDatabase.AssetPathToGUID(e.path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[AddressablesSetup] Asset introuvable : {e.path}");
                    EditorApplication.Exit(1);
                    return;
                }
                var group = e.group == "FX_PCVR" ? pc : quest;
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = e.address;
                entry.SetLabel(e.label, true, true);
                entry.SetLabel(e.group, true, true);
            }

            // Par défaut : profil PCVR actif (le build Quest repositionne via SetPlatform).
            SetPlatform("PCVR");
            AssetDatabase.SaveAssets();

            // Vérification : chaque entrée doit être SÉRIALISÉE dans son groupe après sauvegarde.
            // (Session casque : l'entrée Katana de FX_Quest avait disparu du groupe sérialisé alors
            // que CreateOrMoveEntry l'avait acceptée — catalogue Quest sans katana, repli PCVR au
            // runtime. On re-crée l'entrée manquante et on échoue bruyamment si elle ne tient pas.)
            AssetDatabase.Refresh();
            foreach (var e in Entries)
            {
                string guid = AssetDatabase.AssetPathToGUID(e.path);
                var group = e.group == "FX_PCVR" ? pc : quest;
                var entry = settings.FindAssetEntry(guid);
                if (entry == null || entry.parentGroup != group)
                {
                    Debug.LogWarning($"[AddressablesSetup] Entrée {e.group}/{e.address} absente après sauvegarde, re-création.");
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.address = e.address;
                    entry.SetLabel(e.label, true, true);
                    entry.SetLabel(e.group, true, true);
                    EditorUtility.SetDirty(group);
                    AssetDatabase.SaveAssets();
                    entry = settings.FindAssetEntry(guid);
                }
                if (entry == null || entry.parentGroup != group)
                {
                    Debug.LogError($"[AddressablesSetup] ÉCHEC : {e.group}/{e.address} ({e.path}) refuse de rester dans le groupe.");
                    EditorApplication.Exit(1);
                    return;
                }
            }
            Debug.Log($"[AddressablesSetup] Groupes : {string.Join(", ", settings.groups.Select(g => g.name))} ; " +
                      $"entrées : {string.Join(", ", Entries.Select(e => e.group + "/" + e.address))}");
        }

        // Active le profil et l'inclusion des groupes pour une plateforme ("PCVR" ou "Quest").
        // À appeler AVANT AddressableAssetSettings.BuildPlayerContent et le build du joueur.
        public static void SetPlatform(string platform)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return;
            string profileId = settings.profileSettings.GetProfileId(platform);
            if (!string.IsNullOrEmpty(profileId)) settings.activeProfileId = profileId;
            foreach (var g in settings.groups)
            {
                var schema = g.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;
                if (g.name == "FX_PCVR") schema.IncludeInBuild = platform == "PCVR";
                else if (g.name == "FX_Quest") schema.IncludeInBuild = platform == "Quest";
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesSetup] Plateforme contenu : {platform} (profil {settings.profileSettings.GetValueByName(settings.activeProfileId, "Local.BuildPath")})");
        }

        static string EnsureProfile(AddressableAssetSettings settings, string name, string buildPath, string loadPath)
        {
            string id = settings.profileSettings.GetProfileId(name);
            if (string.IsNullOrEmpty(id))
                id = settings.profileSettings.AddProfile(name, settings.profileSettings.GetProfileId("Default"));
            settings.profileSettings.SetValue(id, "Local.BuildPath", buildPath);
            settings.profileSettings.SetValue(id, "Local.LoadPath", loadPath);
            return id;
        }

        static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, string profileId)
        {
            var group = settings.FindGroup(name);
            if (group == null)
                group = settings.CreateGroup(name, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether; // un bundle par groupe
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;    // chargement rapide, accès direct
            EditorUtility.SetDirty(group);
            return group;
        }
    }
}