// Réorganisation du projet (Part 1, Task 2), exécutée par l'API de l'éditeur
// (AssetDatabase.MoveAsset) pour garder les GUID, les .meta et les références.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.ProjectReorganizer.Run -quit
// Idempotent : un déplacement déjà fait est ignoré.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class ProjectReorganizer
    {
        // (ancien chemin, nouveau chemin)
        static readonly (string from, string to)[] Moves =
        {
            // Scènes
            ("Assets/Scenes/SampleScene.unity", "Assets/Scenes/FruitNinjaVR_Main.unity"),
            ("Assets/Scenes/SlicerTestScene.unity", "Assets/Scenes/Tests/SlicerTestScene.unity"),

            // Scripts (les fichiers dont le nom différait de la classe sont renommés)
            ("Assets/GameManager.cs", "Assets/Scripts/Core/GameManager.cs"),
            ("Assets/DifficultySettings.cs", "Assets/Scripts/Core/DifficultySettings.cs"),
            ("Assets/UIManager.cs", "Assets/Scripts/UI/UIManager.cs"),
            ("Assets/AudioManager.cs", "Assets/Scripts/Audio/AudioManager.cs"),
            ("Assets/NewFruitSpawner.cs", "Assets/Scripts/Gameplay/FruitSpawner.cs"),
            ("Assets/FruitController.cs", "Assets/Scripts/Gameplay/FruitController.cs"),
            ("Assets/FruitTag.cs", "Assets/Scripts/Gameplay/FruitTag.cs"),
            ("Assets/BombController.cs", "Assets/Scripts/Gameplay/BombController.cs"),
            ("Assets/destroyFLoor.cs", "Assets/Scripts/Gameplay/DestroyFloor.cs"),
            ("Assets/Slicer.cs", "Assets/Scripts/Slicing/Slicer.cs"),
            ("Assets/KatanaSlicer.cs", "Assets/Scripts/Slicing/KatanaSlicer.cs"),
            ("Assets/BombTester.cs", "Assets/Scripts/Debug/BombTester.cs"),
            ("Assets/Test/SliceableFruit.cs", "Assets/Scripts/Debug/SliceableFruit.cs"),

            // Prefabs de gameplay
            ("Assets/modeles/fruits/Banana 1.prefab", "Assets/Prefabs/Fruits/Fruit_Banana.prefab"),
            ("Assets/modeles/fruits/Coconut.prefab", "Assets/Prefabs/Fruits/Fruit_Coconut.prefab"),
            ("Assets/modeles/fruits/GreenApple.prefab", "Assets/Prefabs/Fruits/Fruit_GreenApple.prefab"),
            ("Assets/modeles/fruits/Orange.prefab", "Assets/Prefabs/Fruits/Fruit_Orange.prefab"),
            ("Assets/modeles/fruits/Pear.prefab", "Assets/Prefabs/Fruits/Fruit_Pear.prefab"),
            ("Assets/modeles/fruits/RedApple.prefab", "Assets/Prefabs/Fruits/Fruit_RedApple.prefab"),
            ("Assets/modeles/fruits/Tomato.prefab", "Assets/Prefabs/Fruits/Fruit_Tomato.prefab"),
            ("Assets/modeles/fruits/Watermelon.prefab", "Assets/Prefabs/Fruits/Fruit_Watermelon.prefab"),
            ("Assets/BombPrefab.prefab", "Assets/Prefabs/Bombs/Bomb.prefab"),
            // Prefabs hérités non utilisés par les scènes (conservés, isolés)
            ("Assets/modeles/fruits/Banana.prefab", "Assets/Prefabs/_Unused/Fruit_Banana_Old.prefab"),
            ("Assets/modeles/fruits/Fruits (1).prefab", "Assets/Prefabs/_Unused/Fruits_AllInOne.prefab"),

            // Modèles 3D (noms de FBX conservés : les identifiants internes des sous-objets en dépendent)
            ("Assets/modeles/fruits/source/Fruits", "Assets/Models/Fruits/Source"),
            ("Assets/modeles/Bomb.fbx", "Assets/Models/Bomb/Bomb.fbx"),
            ("Assets/Scenes/Katana.fbx", "Assets/Models/Katana/Katana.fbx"),
            ("Assets/modeles/dojo mockup.fbx", "Assets/Models/Environment/dojo mockup.fbx"),
            ("Assets/modeles/fruits/Fruits.fbx", "Assets/Models/_Unused/Fruits.fbx"),
            ("Assets/modeles/beam.fbx", "Assets/Models/_Unused/beam.fbx"),
            ("Assets/modeles/Untitled.fbx", "Assets/Models/_Unused/Untitled.fbx"),

            // Textures
            ("Assets/modeles/fruits/textures", "Assets/Textures/Fruits"),
            ("Assets/Material/bomb/bomb_basecol.png", "Assets/Textures/Bomb/bomb_basecol.png"),
            ("Assets/Material/bomb/bomb_roughness.png", "Assets/Textures/Bomb/bomb_roughness.png"),
            ("Assets/Material/bomb/internal_ground_ao_texture.jpeg", "Assets/Textures/Bomb/internal_ground_ao_texture.jpeg"),
            ("Assets/Material/citrus_orchard_road_puresky_4k.exr", "Assets/Textures/Skybox/citrus_orchard_road_puresky_4k.exr"),
            ("Assets/modeles/base_texture_01.png", "Assets/Textures/Environment/base_texture_01.png"),

            // Matériaux
            ("Assets/Material/bomb/Materials/bomb_basecol.mat", "Assets/Materials/Bomb/Bomb_BaseColor.mat"),
            ("Assets/Material/bomb/Materials/bomb_roughness.mat", "Assets/Materials/Bomb/Bomb_Roughness.mat"),
            ("Assets/Material/bomb/Materials/internal_ground_ao_texture.mat", "Assets/Materials/Bomb/Bomb_GroundAO.mat"),
            ("Assets/Material/Materials/citrus_orchard_road_puresky_4k.mat", "Assets/Materials/Environment/Skybox_CitrusOrchard.mat"),
            ("Assets/modeles/Materials/base_texture_01.mat", "Assets/Materials/Environment/Environment_Base.mat"),
            ("Assets/Material/BeamMaterial.mat", "Assets/Materials/Katana/Katana_Beam.mat"),
            ("Assets/Test/CapMaterial.mat", "Assets/Materials/Slicing/SliceCap.mat"),
            ("Assets/Test/New Material.mat", "Assets/Materials/_Unused/NewMaterial.mat"),

            // Effets (Task 3 ajoutera les Particle Systems dans Assets/FX)
            ("Assets/Material/EclipseMaterial.mat", "Assets/FX/Materials/FX_BombFlash.mat"),

            // Audio
            ("Assets/sound/sword-slice-393847.mp3", "Assets/Audio/SFX/SFX_Katana_Swipe.mp3"),
            ("Assets/sound/steel-blade-slice-2-188214.mp3", "Assets/Audio/SFX/SFX_Fruit_Slice.mp3"),
            ("Assets/sound/metal-hit-95-200424.mp3", "Assets/Audio/SFX/SFX_Bomb_Touch.mp3"),
            ("Assets/sound/explosion-fx-343683.mp3", "Assets/Audio/SFX/SFX_Bomb_Explosion.mp3"),
            ("Assets/sound/chinese-ident-transition-2-283707.mp3", "Assets/Audio/SFX/SFX_Game_Start.mp3"),
            ("Assets/sound/chinese-ident-transition-3-283709.mp3", "Assets/Audio/SFX/SFX_Game_Over.mp3"),
            ("Assets/sound/button-press-382713.mp3", "Assets/Audio/UI/SFX_UI_ButtonPress.mp3"),
            ("Assets/sound/hover-button-287656.mp3", "Assets/Audio/UI/SFX_UI_ButtonHover.mp3"),

            // Divers
            ("Assets/InputSystem_Actions.inputactions", "Assets/Settings/Input/InputSystem_Actions.inputactions"),
            ("Assets/Readme.asset", "Assets/TutorialInfo/Readme.asset"),
        };

        // Dossiers vidés par les déplacements : supprimés seulement s'ils ne contiennent plus aucun asset.
        static readonly string[] OldFolders =
        {
            "Assets/modeles/fruits/source", "Assets/modeles/fruits", "Assets/modeles/Materials", "Assets/modeles",
            "Assets/Material/bomb/Materials", "Assets/Material/bomb", "Assets/Material/Materials", "Assets/Material",
            "Assets/Test", "Assets/sound",
            "Assets/Jenpeuplu", // dossier vide hérité (seul son .meta était versionné)
        };

        [MenuItem("Tools/Fruit Ninja/Réorganiser le projet (Task 2)")]
        public static void Run()
        {
            int done = 0, skipped = 0;
            var errors = new List<string>();
            {
                foreach (var (from, to) in Moves)
                {
                    bool fromExists = AssetExists(from);
                    if (!fromExists && AssetExists(to)) { skipped++; continue; }
                    if (!fromExists) { errors.Add($"Source introuvable : {from}"); continue; }
                    EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
                    string err = AssetDatabase.MoveAsset(from, to);
                    if (string.IsNullOrEmpty(err)) { done++; Debug.Log($"[Reorg] {from} -> {to}"); }
                    else errors.Add($"{from} -> {to} : {err}");
                }
            }

            foreach (var folder in OldFolders)
                if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets("", new[] { folder }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(folder);
                    Debug.Log($"[Reorg] dossier vide supprimé : {folder}");
                }

            // Scène de démarrage en premier, scène de test exclue du build.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/FruitNinjaVR_Main.unity", true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (var e in errors) Debug.LogError("[Reorg] " + e);
            Debug.Log($"[Reorg] terminé : {done} déplacés, {skipped} déjà faits, {errors.Count} erreur(s)");
        }

        static bool AssetExists(string path) =>
            AssetDatabase.IsValidFolder(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
