// Outil de mesure du Profiler pendant le bench Play Mode (Part 2, session casque).
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.ProfilerBenchCapture.Run -quit
// Le bench tourne en Play Mode (même seed/durée/parties que les builds, variante PCVR) ;
// l'outil enregistre le Profiler editor autour des coupes et de la première explosion,
// sauve les profils .data et exporte des CSV par frame (CPU ms, GC alloc) + un PNG de
// synthèse (timeline CPU / GC alloc / mémoire). Ce sont des mesures du Profiler editor
// en Play Mode, PAS des captures de l'écran du Profiler ni des mesures du casque :
// le rapport le dit explicitement (repli documenté de la session casque).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class ProfilerBenchCapture
    {
        const string OutDir = "docs/part2/captures-profiler";
        const string Session = "playmode-apres";

        [MenuItem("Tools/Fruit Ninja/Captures Profiler du bench")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene("Assets/Scenes/FruitNinjaVR_Main.unity");
            // Le domain reload à l'entrée en Play Mode efface les champs statiques et les
            // abonnements : on passe la config par variable d'env (lue par BenchMode.ParseArgs)
            // et on s'abonne à update APRÈS l'entrée en Play Mode.
            Environment.SetEnvironmentVariable("FN_BENCH", "1");
            // delayCall survit au domain reload de l'entrée en Play Mode (les champs statiques
            // et abonnements directs sont effacés) : on s'abonne à update une fois en Play Mode.
            EditorApplication.isPlaying = true;
            EditorApplication.delayCall += ArmTick;
            Debug.Log("[ProfilerBenchCapture] Play Mode demandé (FN_BENCH=1)…");
        }

        static void ArmTick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.delayCall += ArmTick; return; }
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[ProfilerBenchCapture] Play Mode entré, Tick abonné.");
        }

        static int state; // 0=attente bench 1=record 2=extraction 3=fait
        static int waited;
        static BenchMode bench;
        static int startFrame = -1, endFrame = -1;

        static void Tick()
        {
            waited++;
            if (waited > 60 * 60 * 6) { Debug.LogError("[ProfilerBenchCapture] timeout global"); state = 3; }

            if (!EditorApplication.isPlaying && state < 3) return;

            if (state == 0)
            {
                bench = UnityEngine.Object.FindFirstObjectByType<BenchMode>();
                if (bench == null)
                {
                    // Secours si le bootstrap n'a pas créé le BenchMode (FN_BENCH=1 est lu
                    // par ParseArgs : mêmes défauts seed 12345 / 3 parties / 90 s que les builds).
                    if (waited > 240)
                    {
                        var go = new GameObject("BenchMode");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        bench = go.AddComponent<BenchMode>();
                        Debug.Log("[ProfilerBenchCapture] BenchMode créé manuellement (FN_BENCH=1).");
                    }
                    return;
                }
                ProfilerDriver.enabled = true;
                ProfilerDriver.deepProfiling = false;
                startFrame = -1;
                waited = 0; state = 1;
                Debug.Log("[ProfilerBenchCapture] enregistrement Profiler démarré (1 partie de bench).");
                return;
            }

            if (state == 1)
            {
                var so = new SerializedObject(bench);
                string phase = so.FindProperty("phase").stringValue;
                if (startFrame < 0 && phase == "part1")
                    startFrame = Mathf.Max(0, ProfilerDriver.lastFrameIndex - 2);
                // enregistre jusqu'à la fin de la 1re partie (pics coupe + explosion couverts)
                if (startFrame >= 0 && (phase == "menu1" || phase == "end" || waited > 60 * 120))
                {
                    endFrame = ProfilerDriver.lastFrameIndex;
                    state = 11; waited = 0;
                }
                return;
            }
            if (state == 11)
            {
                if (ProfilerDriver.lastFrameIndex >= endFrame || waited > 60 * 30)
                {
                    endFrame = Mathf.Min(endFrame, ProfilerDriver.lastFrameIndex);
                    ProfilerDriver.enabled = false;
                    string data = Path.Combine(OutDir, $"{Session}.data");
                    ProfilerDriver.SaveProfile(data);
                    Debug.Log($"[ProfilerBenchCapture] profil sauvé : {data} frames {startFrame}..{endFrame}");
                    Extract(startFrame, endFrame);
                    state = 3;
                }
                return;
            }
            if (state == 3)
            {
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
                Debug.Log("[ProfilerBenchCapture] terminé.");
                if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        static void Extract(int f0, int f1)
        {
            var cpu = new List<(int f, double ms)>();
            var gc = new List<(int f, long bytes)>();
            var mem = new List<(int f, long used, long reserved)>();
            for (int f = f0; f <= f1; f++)
            {
                using (var d = ProfilerDriver.GetRawFrameDataView(f, 0))
                {
                    if (d == null || !d.valid) continue;
                    cpu.Add((f, d.frameTimeMs));
                    int idAlloc = d.GetMarkerId("GC.Alloc");
                    long bytes = 0;
                    if (idAlloc >= 0)
                        for (int s = 0; s < d.sampleCount; s++)
                            if (d.GetSampleMarkerId(s) == idAlloc) bytes += d.GetSampleMetadataAsLong(s, 0);
                    gc.Add((f, bytes));
                }
            }
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("frame,cpu_ms,gc_alloc_ko\n");
            for (int i = 0; i < cpu.Count; i++)
                sb.AppendLine($"{cpu[i].f},{cpu[i].ms.ToString("F4", ci)},{(gc[i].bytes / 1024.0).ToString("F2", ci)}");
            File.WriteAllText(Path.Combine(OutDir, $"{Session}-profiler-frames.csv"), sb.ToString(), new UTF8Encoding(false));

            // PNG de synthèse : CPU (haut), GC alloc (milieu), zones coupe/explosion.
            int W = 1600, H = 700;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.13f, 0.13f, 0.15f);
            double maxCpu = 0, maxGc = 0;
            foreach (var c in cpu) maxCpu = Math.Max(maxCpu, c.ms);
            foreach (var g in gc) maxGc = Math.Max(maxGc, g.bytes / 1024.0);
            maxCpu = Math.Max(maxCpu, 5); maxGc = Math.Max(maxGc, 10);
            for (int i = 0; i < cpu.Count; i++)
            {
                int x = i * (W - 80) / Math.Max(1, cpu.Count - 1) + 60;
                int hc = (int)(cpu[i].ms / maxCpu * 260);
                for (int y = 360; y < 360 + hc && y < 640; y++) px[y * W + x] = new Color(0.35f, 0.75f, 1f);
                int hg = (int)(gc[i].bytes / 1024.0 / maxGc * 240);
                var col = gc[i].bytes > 0 ? new Color(1f, 0.65f, 0.2f) : new Color(0.3f, 0.3f, 0.32f);
                for (int y = 60; y < 60 + Math.Max(hg, 1) && y < 300; y++) px[y * W + x] = col;
            }
            // lignes de repère 16,7 ms (60 fps) et 13,9 ms (72 fps) sur le CPU
            foreach (double ms in new[] { 13.9, 16.7 })
            {
                int y = 360 + (int)(ms / maxCpu * 260);
                if (y < 640) for (int x = 60; x < W - 20; x += 2) px[y * W + x] = new Color(0.9f, 0.3f, 0.3f);
            }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, $"{Session}-profiler-synthese.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            // stats texte (jointes au rapport)
            double mean = 0, p95 = 0; var sorted = new List<double>();
            foreach (var c in cpu) { mean += c.ms; sorted.Add(c.ms); }
            sorted.Sort();
            if (cpu.Count > 0) { mean /= cpu.Count; p95 = sorted[(int)(sorted.Count * 0.95)]; }
            long tot = 0; foreach (var g in gc) tot += g.bytes;
            File.WriteAllText(Path.Combine(OutDir, $"{Session}-profiler-stats.txt"),
                $"Frames analysées : {cpu.Count} (Play Mode editor, 1 partie de bench, même seed 12345)\n" +
                $"CPU moyen : {mean.ToString("F3", ci)} ms ; p95 : {p95.ToString("F3", ci)} ms ; max : {sorted[sorted.Count - 1].ToString("F3", ci)} ms\n" +
                $"GC alloc total : {(tot / 1048576.0).ToString("F2", ci)} Mo ; max/frame : {(maxGc).ToString("F2", ci)} ko\n" +
                $"PNG : haut = GC Allocated In Frame (ko), bas = CPU frame (ms), lignes rouges = 13,9 / 16,7 ms\n",
                new UTF8Encoding(false));
            Debug.Log($"[ProfilerBenchCapture] extraction OK : {cpu.Count} frames, CPU moyen {mean:F3} ms, GC {(tot / 1048576.0):F2} Mo");
        }
    }
}