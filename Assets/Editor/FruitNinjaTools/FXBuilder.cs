// Génère les Particle Systems de la Part 1 (Task 3) et les branche dans la scène :
// FX_FruitJuice_PCVR / _Quest et FX_BombExplosion_PCVR / _Quest dans Assets/FX/Prefabs.
// Les paramètres de chaque variante sont décrits dans docs/particules.md.
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.FXBuilder.Run -quit
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class FXBuilder
    {
        const string PrefabDir = "Assets/FX/Prefabs";
        const string MatDir = "Assets/FX/Materials";
        const string MainScene = "Assets/Scenes/FruitNinjaVR_Main.unity";

        enum Blend { Alpha = 0, Additive = 2 }

        // Paramètres d'un sous-système de particules.
        class Spec
        {
            public string name;
            public int burst, maxParticles;
            public Vector2 lifetime, speed, size;
            public float gravity, duration = 0.5f, shapeRadius = 0.03f;
            public Color color = Color.white;
            public Gradient colorOverLifetime;
            public AnimationCurve sizeOverLifetime;
            public bool stretched, collision, noise, randomRotation;
            public Material material;
        }

        [MenuItem("Tools/Fruit Ninja/Générer les effets de particules")]
        public static void Run()
        {
            EnsureFolder(PrefabDir); EnsureFolder(MatDir);
            Texture2D tex = SoftDotTexture();

            // Matériaux : PCVR = shaders URP Particles avec soft particles ; Quest = Unlit sans soft particles.
            var juicePC = Mat("FX_Juice_PCVR", "Universal Render Pipeline/Particles/Lit", Blend.Alpha, true, tex);
            var firePC = Mat("FX_Fire_PCVR", "Universal Render Pipeline/Particles/Unlit", Blend.Additive, true, tex);
            var smokePC = Mat("FX_Smoke_PCVR", "Universal Render Pipeline/Particles/Lit", Blend.Alpha, true, tex);
            var juiceQ = Mat("FX_Juice_Quest", "Universal Render Pipeline/Particles/Unlit", Blend.Alpha, false, tex);
            var fireQ = Mat("FX_Fire_Quest", "Universal Render Pipeline/Particles/Unlit", Blend.Additive, false, tex);
            // Sans HDR ni bloom (Quest), un feu additif se perd sur le décor clair : boule de feu en alpha blend.
            var fireballQ = Mat("FX_Fireball_Quest", "Universal Render Pipeline/Particles/Unlit", Blend.Alpha, false, tex);

            var fade = Fade(Color.white, Color.white);
            var fire = new Gradient();
            fire.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.35f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            var shrink = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            var grow = AnimationCurve.Linear(0f, 0.5f, 1f, 1.4f);

            // ---------- Jus de fruit ----------
            Build("FX_FruitJuice_PCVR", new List<Spec>
            {
                new Spec { name = "Droplets", burst = 60, maxParticles = 80, lifetime = V(0.5f, 0.9f), speed = V(1.5f, 3.5f), size = V(0.015f, 0.04f),
                           gravity = 1f, colorOverLifetime = fade, sizeOverLifetime = shrink, stretched = true, collision = true, material = juicePC },
                new Spec { name = "Mist", burst = 25, maxParticles = 30, lifetime = V(0.3f, 0.5f), speed = V(0.3f, 0.8f), size = V(0.06f, 0.14f),
                           gravity = 0.1f, color = new Color(1f, 1f, 1f, 0.6f), colorOverLifetime = fade, sizeOverLifetime = grow, randomRotation = true, material = juicePC },
            });
            Build("FX_FruitJuice_Quest", new List<Spec>
            {
                new Spec { name = "Droplets", burst = 20, maxParticles = 24, lifetime = V(0.3f, 0.5f), speed = V(1.2f, 2.5f), size = V(0.035f, 0.06f),
                           gravity = 1f, colorOverLifetime = fade, material = juiceQ },
            });

            // ---------- Explosion de bombe ----------
            Build("FX_BombExplosion_PCVR", new List<Spec>
            {
                new Spec { name = "Flash", burst = 1, maxParticles = 1, lifetime = V(0.15f, 0.15f), speed = V(0f, 0f), size = V(1.5f, 1.5f),
                           color = new Color(1f, 0.9f, 0.6f), colorOverLifetime = fade, shapeRadius = 0.01f, material = firePC },
                new Spec { name = "Fireball", burst = 60, maxParticles = 70, lifetime = V(0.5f, 0.9f), speed = V(1f, 3f), size = V(0.25f, 0.6f),
                           colorOverLifetime = fire, sizeOverLifetime = grow, noise = true, randomRotation = true, shapeRadius = 0.1f, material = firePC },
                new Spec { name = "Sparks", burst = 100, maxParticles = 120, lifetime = V(0.4f, 1f), speed = V(4f, 8f), size = V(0.02f, 0.04f),
                           gravity = 0.6f, color = new Color(1f, 0.75f, 0.3f), colorOverLifetime = fade, stretched = true, collision = true, material = firePC },
                new Spec { name = "Smoke", burst = 25, maxParticles = 30, lifetime = V(1.5f, 2.5f), speed = V(0.3f, 1f), size = V(0.4f, 0.9f),
                           gravity = -0.05f, color = new Color(0.25f, 0.25f, 0.25f, 0.5f), colorOverLifetime = fade, sizeOverLifetime = grow, noise = true, randomRotation = true, shapeRadius = 0.15f, material = smokePC },
            });
            Build("FX_BombExplosion_Quest", new List<Spec>
            {
                new Spec { name = "Flash", burst = 1, maxParticles = 1, lifetime = V(0.12f, 0.12f), speed = V(0f, 0f), size = V(1.2f, 1.2f),
                           color = new Color(1f, 0.9f, 0.6f), colorOverLifetime = fade, shapeRadius = 0.01f, material = fireQ },
                new Spec { name = "Fireball", burst = 16, maxParticles = 20, lifetime = V(0.5f, 0.7f), speed = V(0.6f, 1.6f), size = V(0.4f, 0.75f),
                           colorOverLifetime = fire, sizeOverLifetime = grow, randomRotation = true, shapeRadius = 0.1f, material = fireballQ },
                new Spec { name = "Sparks", burst = 24, maxParticles = 30, lifetime = V(0.3f, 0.6f), speed = V(3f, 6f), size = V(0.05f, 0.08f),
                           gravity = 0.6f, color = new Color(1f, 0.75f, 0.3f), colorOverLifetime = fade, material = fireQ },
            });

            AssetDatabase.SaveAssets();
            SetJuiceColors();
            WireScene();
            Debug.Log("[FX] effets générés et branchés");
        }

        // Disque blanc à bord doux (alpha radial), 64 px : commun aux deux plateformes, très léger.
        static Texture2D SoftDotTexture()
        {
            const string path = "Assets/FX/Textures/FX_SoftDot.png";
            if (!System.IO.File.Exists(path))
            {
                EnsureFolder("Assets/FX/Textures");
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                        float a = Mathf.Clamp01(1f - d);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                    }
                System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Vector2 V(float a, float b) => new Vector2(a, b);

        static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Material Mat(string name, string shader, Blend blend, bool softParticles, Texture tex)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);              // transparent
            m.SetFloat("_Blend", (float)blend);
            m.SetFloat("_SoftParticlesEnabled", softParticles ? 1f : 0f);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            m.SetFloat("_SoftParticlesFarFadeDistance", 0.3f);
            m.SetFloat("_ReceiveShadows", 0f);
            BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Build(string prefabName, List<Spec> specs)
        {
            var root = new GameObject(prefabName);
            ParticleSystem rootPs = null;
            foreach (var s in specs)
            {
                var go = rootPs == null ? root : new GameObject(s.name);
                if (go != root) go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Configure(ps, s);
                if (rootPs == null) rootPs = ps;
            }
            string path = $"{PrefabDir}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[FX] prefab : {path}");
        }

        static void Configure(ParticleSystem ps, Spec s)
        {
            var main = ps.main;
            main.duration = s.duration;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.lifetime.x, s.lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.speed.x, s.speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(s.size.x, s.size.y);
            main.startColor = s.color;
            main.gravityModifier = s.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = s.maxParticles;
            main.startRotation = s.randomRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.burst) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = s.shapeRadius;

            var col = ps.colorOverLifetime;
            col.enabled = s.colorOverLifetime != null;
            if (col.enabled) col.color = s.colorOverLifetime;

            var sol = ps.sizeOverLifetime;
            sol.enabled = s.sizeOverLifetime != null;
            if (sol.enabled) sol.size = new ParticleSystem.MinMaxCurve(1f, s.sizeOverLifetime);

            var noise = ps.noise;
            noise.enabled = s.noise;
            if (s.noise) { noise.strength = 0.5f; noise.frequency = 1.5f; noise.quality = ParticleSystemNoiseQuality.Medium; }

            var collision = ps.collision;
            collision.enabled = s.collision;
            if (s.collision)
            {
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.bounce = 0.2f;
                collision.dampen = 0.6f;
                collision.lifetimeLoss = 0.4f;
                collision.collidesWith = 1; // Default : sol et décor
            }

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = s.material;
            r.renderMode = s.stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (s.stretched) { r.velocityScale = 0.04f; r.lengthScale = 1.5f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Couleur du jus de chaque fruit (FruitController.juiceColor).
        static void SetJuiceColors()
        {
            var colors = new Dictionary<string, Color>
            {
                { "Fruit_Banana", new Color(1f, 0.93f, 0.55f) },
                { "Fruit_Coconut", new Color(0.97f, 0.97f, 0.92f) },
                { "Fruit_GreenApple", new Color(0.75f, 0.95f, 0.45f) },
                { "Fruit_Orange", new Color(1f, 0.6f, 0.1f) },
                { "Fruit_Pear", new Color(0.9f, 0.95f, 0.6f) },
                { "Fruit_RedApple", new Color(1f, 0.95f, 0.7f) },
                { "Fruit_Tomato", new Color(0.9f, 0.15f, 0.1f) },
                { "Fruit_Watermelon", new Color(1f, 0.2f, 0.3f) },
                { "Fruit_Bonus", new Color(1f, 0.82f, 0.2f) },
            };
            foreach (var kv in colors)
            {
                string path = $"Assets/Prefabs/Fruits/{kv.Key}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                var fc = root.GetComponent<FruitController>();
                if (fc != null) { fc.juiceColor = kv.Value; PrefabUtility.SaveAsPrefabAsset(root, path); }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(MainScene);
            var fx = Object.FindFirstObjectByType<GameFX>();
            if (fx == null) fx = new GameObject("GameFX").AddComponent<GameFX>();
            fx.fruitJuicePCVR = Load("FX_FruitJuice_PCVR");
            fx.fruitJuiceQuest = Load("FX_FruitJuice_Quest");
            fx.bombExplosionPCVR = Load("FX_BombExplosion_PCVR");
            fx.bombExplosionQuest = Load("FX_BombExplosion_Quest");
            EditorUtility.SetDirty(fx);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static ParticleSystem Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab").GetComponent<ParticleSystem>();

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
