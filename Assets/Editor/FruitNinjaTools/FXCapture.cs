// Captures comparables des effets PCVR et Quest dans la scène de jeu (Part 1, Task 3).
// Même caméra (point de vue du joueur), même instant de simulation ; la variante Quest est rendue
// avec le niveau de qualité "Mobile" (celui du Quest), la variante PCVR avec le niveau "PC".
// Lancement (sans -nographics) : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.FXCapture.Run -quit
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class FXCapture
    {
        const string OutDir = "docs/captures";
        const int Width = 1600, Height = 900;

        [MenuItem("Tools/Fruit Ninja/Captures des effets")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FruitNinjaVR_Main.unity");
            Directory.CreateDirectory(OutDir);
            var stats = new StringBuilder("effet;variante;qualité;particules à t;particules max (somme maxParticles);sous-systèmes;collision;noise;soft particles\n");

            var cam = GameObject.Find("XRRig/Camera Offset/Main Camera").GetComponent<Camera>();
            var rig = GameObject.Find("XRRig").transform;
            // Hauteur des yeux debout (le CameraOffset l'ajoute au runtime), regard vers les points de spawn.
            cam.transform.position = rig.position + Vector3.up * 1.6f;
            cam.transform.rotation = Quaternion.LookRotation(rig.forward, Vector3.up);
            Vector3 target = cam.transform.position + rig.forward * 1.1f + Vector3.down * 0.15f;
            HideUI();
            int initialQuality = QualitySettings.GetQualityLevel();

            var fruit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fruits/Fruit_Watermelon.prefab");
            var bomb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab");

            // Rendu de chauffe : le premier rendu en batchmode n'a pas encore l'éclairage ambiant.
            WarmUp(cam);

            Shot("FruitJuice", "PCVR", "PC", fruit, target, 0.12f, new Color(1f, 0.2f, 0.3f), cam, stats);
            Shot("FruitJuice", "Quest", "Mobile", fruit, target, 0.12f, new Color(1f, 0.2f, 0.3f), cam, stats);
            Shot("BombExplosion", "PCVR", "PC", bomb, target + rig.forward * 0.6f, 0.25f, null, cam, stats);
            Shot("BombExplosion", "Quest", "Mobile", bomb, target + rig.forward * 0.6f, 0.25f, null, cam, stats);

            QualitySettings.SetQualityLevel(initialQuality, true);
            File.WriteAllText(Path.Combine(OutDir, "fx-stats.csv"), stats.ToString(), new UTF8Encoding(false));
            Debug.Log("[FXCapture]\n" + stats);
        }

        static void Shot(string effect, string variant, string quality, GameObject propPrefab, Vector3 pos, float time,
                         Color? tint, Camera cam, StringBuilder stats)
        {
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, quality), true);

            var prop = (GameObject)PrefabUtility.InstantiatePrefab(propPrefab);
            prop.transform.position = pos;
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;

            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FX/Prefabs/FX_{effect}_{variant}.prefab");
            var fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab);
            // Le jus part de la surface du fruit, côté joueur ; l'explosion du centre de la bombe.
            fx.transform.position = tint.HasValue ? pos - cam.transform.forward * 0.12f : pos;
            var systems = fx.GetComponentsInChildren<ParticleSystem>();
            if (tint.HasValue)
                foreach (var ps in systems) { var m = ps.main; m.startColor = new Color(tint.Value.r, tint.Value.g, tint.Value.b, m.startColor.color.a); }
            var root = fx.GetComponent<ParticleSystem>();
            root.Simulate(time, true, true, true);

            var rt = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            string file = Path.Combine(OutDir, $"FX_{effect}_{variant}.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);

            int alive = systems.Sum(s => s.particleCount);
            int max = systems.Sum(s => s.main.maxParticles);
            bool soft = systems.Any(s => s.GetComponent<ParticleSystemRenderer>().sharedMaterial.IsKeywordEnabled("_SOFTPARTICLES_ON"));
            stats.AppendLine($"{effect};{variant};{quality};{alive};{max};{systems.Length};{systems.Any(s => s.collision.enabled)};{systems.Any(s => s.noise.enabled)};{soft}");
            Debug.Log($"[FXCapture] {file}");

            Object.DestroyImmediate(fx);
            Object.DestroyImmediate(prop);
        }

        static void WarmUp(Camera cam)
        {
            DynamicGI.UpdateEnvironment();
            var rt = new RenderTexture(64, 64, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 3; i++) cam.Render();
            cam.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt);
        }

        static void HideUI()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                c.gameObject.SetActive(false);
        }
    }
}
