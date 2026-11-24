// Mise en place de la scène pour Addressables (Part 2, Tasks 3-4) :
// GameObject "AssetLoader" avec replis = références directes de la Part 1.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.Part2Setup.Run -quit
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class Part2Setup
    {
        const string MainScene = "Assets/Scenes/FruitNinjaVR_Main.unity";

        [MenuItem("Tools/Fruit Ninja/Configurer AssetLoader (Part 2)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            var loader = Object.FindFirstObjectByType<AssetLoader>();
            if (loader == null)
            {
                var go = new GameObject("AssetLoader");
                loader = go.AddComponent<AssetLoader>();
            }

            // Replis : les références directes actuelles du GameFX (variantes de la Part 1),
            // et le sabre droit existant comme repli katana (visuel PCVR détaillé).
            var fx = Object.FindFirstObjectByType<GameFX>();
            if (fx != null)
            {
                loader.fruitJuicePCVRFallback = fx.fruitJuicePCVR;
                loader.bombExplosionPCVRFallback = fx.bombExplosionPCVR;
                loader.fruitJuiceQuestFallback = fx.fruitJuiceQuest;
                loader.bombExplosionQuestFallback = fx.bombExplosionQuest;
            }
            loader.katanaFallback = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Katanas/Katana_PCVR.prefab");
            if (loader.katanaFallback == null)
                loader.katanaFallback = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Katanas/Katana_Left.prefab");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Part2Setup] AssetLoader configuré (replis FX={(fx != null)}, katana={loader.katanaFallback?.name}).");
        }
    }
}