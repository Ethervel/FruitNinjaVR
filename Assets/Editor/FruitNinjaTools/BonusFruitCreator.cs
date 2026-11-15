// Crée le fruit bonus de la Part 1 (absent en S7) : variante dorée de Fruit_RedApple,
// puis la branche sur le FruitSpawner de la scène principale.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.BonusFruitCreator.Run -quit
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class BonusFruitCreator
    {
        const string BasePrefab = "Assets/Prefabs/Fruits/Fruit_RedApple.prefab";
        const string BonusPrefab = "Assets/Prefabs/Fruits/Fruit_Bonus.prefab";
        const string BonusMaterial = "Assets/Materials/Fruits/Fruit_Bonus_Gold.mat";
        const string MainScene = "Assets/Scenes/FruitNinjaVR_Main.unity";
        const int BonusPoints = 50;

        [MenuItem("Tools/Fruit Ninja/Créer le fruit bonus")]
        public static void Run()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BonusMaterial);
            if (mat == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Fruits")) AssetDatabase.CreateFolder("Assets/Materials", "Fruits");
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var gold = new Color(1f, 0.78f, 0.15f);
                mat.SetColor("_BaseColor", gold);
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.75f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", gold * 0.6f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                AssetDatabase.CreateAsset(mat, BonusMaterial);
            }

            var bonus = AssetDatabase.LoadAssetAtPath<GameObject>(BonusPrefab);
            if (bonus == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = "Fruit_Bonus";
                instance.transform.localScale *= 1.2f;
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => mat);
                var fc = instance.GetComponent<FruitController>();
                fc.isSpecial = true;
                fc.pointsValue = BonusPoints;
                bonus = PrefabUtility.SaveAsPrefabAsset(instance, BonusPrefab); // variante de Fruit_RedApple
                Object.DestroyImmediate(instance);
                Debug.Log($"[Bonus] prefab créé : {BonusPrefab}");
            }

            var scene = EditorSceneManager.OpenScene(MainScene);
            var spawner = Object.FindFirstObjectByType<FruitSpawner>();
            if (spawner == null) { Debug.LogError("[Bonus] FruitSpawner introuvable"); return; }
            Undo.RecordObject(spawner, "Fruit bonus");
            spawner.bonusFruitPrefab = bonus;
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bonus] branché sur {spawner.name} (chance {spawner.bonusSpawnChance:P0}, {BonusPoints} points)");
        }
    }
}
