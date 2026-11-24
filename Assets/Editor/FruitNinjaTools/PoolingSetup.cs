// Mise en place du pooling dans la scène principale (Part 2, Task 1).
// Ajoute le GameObject "PoolManager" (composant PoolManager) s'il n'existe pas.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.PoolingSetup.Run -quit
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class PoolingSetup
    {
        const string MainScene = "Assets/Scenes/FruitNinjaVR_Main.unity";

        [MenuItem("Tools/Fruit Ninja/Configurer le pooling (Part 2)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            var pm = Object.FindFirstObjectByType<PoolManager>();
            if (pm == null)
            {
                var go = new GameObject("PoolManager");
                pm = go.AddComponent<PoolManager>();
                Debug.Log("[PoolingSetup] PoolManager créé (stocks : fruits 4, bonus 2, bombes 4).");
            }
            else
            {
                Debug.Log("[PoolingSetup] PoolManager déjà présent.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PoolingSetup] Scène enregistrée.");
        }
    }
}