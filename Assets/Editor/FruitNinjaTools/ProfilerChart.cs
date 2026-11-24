// PNG de synthèse « Profiler » à partir des frames.csv du bench (Part 2, session casque).
//   Unity.exe -batchmode -quit -projectPath . -executeMethod FruitNinjaTools.ProfilerChart.Run -logFile <log>
// Produit, pour chaque run disponible (Quest avant/après, Play Mode après), un PNG
// CPU ms + GC alloc ko par frame avec repères 72 fps (13,9 ms) et 60 fps (16,7 ms).
// Ce sont des GRAPHIQUES des mesures ProfilerRecorder du build, pas des captures
// d'écran de la fenêtre Profiler (le rapport le dit explicitement).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class ProfilerChart
    {
        const string OutDir = "docs/part2/captures-profiler";

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            Chart("quest-avant", "docs/part2/mesures-avant-quest/frames.csv", "Quest AVANT (72 Hz, build dev)");
            Chart("quest-apres", "docs/part2/mesures-apres-quest/frames.csv", "Quest APRES (72 Hz, build dev, pools+Addressables)");
            Chart("playmode-apres", "docs/part2/captures-profiler/playmode-apres-frames.csv", "Play Mode editor APRES (PC, sans casque)");
            Debug.Log("[ProfilerChart] terminé.");
        }

        static void Chart(string name, string csvPath, string title)
        {
            if (!File.Exists(csvPath)) { Debug.LogWarning($"[ProfilerChart] absent : {csvPath}"); return; }
            var cpu = new List<double>(); var gc = new List<double>(); var phase = new List<string>();
            foreach (var line in File.ReadLines(csvPath))
            {
                if (line.StartsWith("frame,")) continue;
                var c = line.Split(',');
                if (c.Length < 4) continue;
                double v = double.Parse(c[2], CultureInfo.InvariantCulture);
                cpu.Add(Math.Min(v, 60.0)); // écrête à 60 ms pour la lisibilité (pics système notés)
                gc.Add(double.Parse(c[3], CultureInfo.InvariantCulture));
                phase.Add(c[1]);
            }
            int n = cpu.Count; if (n == 0) return;
            double maxCpu = 0, maxGc = 0, sumCpu = 0, sumGc = 0;
            for (int i = 0; i < n; i++) { maxCpu = Math.Max(maxCpu, cpu[i]); maxGc = Math.Max(maxGc, gc[i]); sumCpu += cpu[i]; sumGc += gc[i]; }
            maxCpu = Math.Max(maxCpu, 20); maxGc = Math.Max(maxGc, 50);

            int W = 1600, H = 760;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.12f, 0.12f, 0.14f);
            // bandes de phase (fond) : part = bleu sombre, menu = vert sombre, loading = gris
            for (int i = 0; i < n; i++)
            {
                int x = 60 + i * (W - 80) / Math.Max(1, n - 1);
                var bg = phase[i].StartsWith("part") ? new Color(0.14f, 0.16f, 0.20f)
                       : phase[i].StartsWith("menu") ? new Color(0.14f, 0.20f, 0.16f) : new Color(0.18f, 0.18f, 0.18f);
                for (int y = 40; y < H - 40; y++) px[y * W + x] = bg;
            }
            // Coordonnées « écran » (haut = 0) : GC en haut (60..330), CPU en bas (400..690),
            // barres montant depuis la baseline de chaque panneau.
            for (int i = 0; i < n; i++)
            {
                int x = 60 + i * (W - 80) / Math.Max(1, n - 1);
                int hc = (int)(cpu[i] / maxCpu * 280);
                for (int y = 690 - hc; y < 690; y++) px[y * W + x] = new Color(0.35f, 0.75f, 1f);
                int hg = (int)(gc[i] / maxGc * 260);
                var col = gc[i] > 100 ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.75f, 0.3f);
                for (int y = 330 - Math.Max(hg, 1); y < 330; y++) px[y * W + x] = col;
            }
            // repères 13,9 / 16,7 ms (depuis la baseline CPU 690)
            foreach (double ms in new[] { 13.9, 16.7 })
            {
                int y = 690 - (int)(ms / maxCpu * 280);
                if (y > 400) for (int x = 60; x < W - 20; x += 2) px[y * W + x] = new Color(0.9f, 0.3f, 0.3f);
            }
            // px est en coordonnées écran (haut=0) ; Texture2D a l'origine en bas à gauche :
            // on retourne verticalement à l'écriture.
            var flip = new Color[W * H];
            for (int y = 0; y < H; y++)
                Array.Copy(px, y * W, flip, (H - 1 - y) * W, W);
            tex.SetPixels(flip); tex.Apply();
            string png = Path.Combine(OutDir, $"{name}-profiler-synthese.png");
            File.WriteAllBytes(png, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            var ci = CultureInfo.InvariantCulture;
            File.WriteAllText(Path.Combine(OutDir, $"{name}-profiler-stats.txt"),
                $"{title}\nFrames : {n}\nCPU moyen : {(sumCpu / n).ToString("F3", ci)} ms (écrêté à 60 ms pour le graphique)\n" +
                $"GC alloc total : {(sumGc / 1024).ToString("F2", ci)} Mo ; max/frame : {maxGc.ToString("F1", ci)} ko\n" +
                $"PNG : haut = GC Allocated In Frame (ko, orange >100 ko), bas = CPU Main Thread (ms),\n" +
                $"lignes rouges = 13,9 ms (72 fps) et 16,7 ms (60 fps). Fond : bleu = partie, vert = menu.\n" +
                $"Source : ProfilerRecorder du build (frames.csv), PAS une capture d'écran du Profiler.\n",
                new System.Text.UTF8Encoding(false));
            Debug.Log($"[ProfilerChart] {png} ({n} frames)");
        }
    }
}