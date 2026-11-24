// Variante Quest allégée du katana (Part 2, Task 4) — générée SANS outil externe.
//
// PCVR  : Katana_PCVR.prefab = copie conforme du sabre détaillé (mesh FBX complet,
//         matériau d'origine) — c'est la référence visuelle de la Part 1.
// Quest : Katana_Quest.prefab = mesh simplifié par DECIMATION (clustering de sommets
//         sur grille, soudures + suppression des triangles dégénérés, normales
//         recalculées) + matériau URP Simple Lit allégé (pas de textures, lissage
//         par normales). La géométrie de découpe du jeu n'est pas touchée (le
//         MeshCollider trigger du sabre est réassigné au runtime sur le mesh simplifié).
//
// Lancement : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.KatanaQuestBuilder.Run -quit
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitNinjaTools
{
    public static class KatanaQuestBuilder
    {
        const string SourcePrefab = "Assets/Prefabs/Katanas/Katana_Left.prefab";
        const string PcvrPrefab = "Assets/Prefabs/Katanas/Katana_PCVR.prefab";
        const string QuestPrefab = "Assets/Prefabs/Katanas/Katana_Quest.prefab";
        const string QuestMeshPath = "Assets/Models/Katana/Katana_Quest.asset";
        const string QuestMatPath = "Assets/Materials/Katana/Katana_Quest.mat";

        [MenuItem("Tools/Fruit Ninja/Générer katana PCVR + Quest (Part 2)")]
        public static void Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (source == null) { Debug.LogError("[KatanaQuest] Prefab source introuvable : " + SourcePrefab); EditorApplication.Exit(1); return; }

            // ---- Variante PCVR : copie conforme (référence détaillée, servie par Addressables) ----
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PcvrPrefab))
            {
                if (!AssetDatabase.CopyAsset(SourcePrefab, PcvrPrefab))
                { Debug.LogError("[KatanaQuest] Copie PCVR impossible."); EditorApplication.Exit(1); return; }
            }
            var pcvr = AssetDatabase.LoadAssetAtPath<GameObject>(PcvrPrefab);
            var pcvrMf = pcvr.GetComponentInChildren<MeshFilter>();
            var pcvrMr = pcvr.GetComponentInChildren<MeshRenderer>();
            Debug.Log($"[KatanaQuest] PCVR : mesh '{pcvrMf.sharedMesh.name}' {pcvrMf.sharedMesh.vertexCount} sommets, " +
                      $"{pcvrMf.sharedMesh.triangles.Length / 3} triangles, matériau {pcvrMr.sharedMaterial.name}");

            // ---- Décimation du mesh (clustering de sommets) ----
            Mesh src = pcvrMf.sharedMesh;
            Mesh questMesh = Decimate(src, out int keptVertices, out int keptTriangles);
            AssetDatabase.CreateAsset(questMesh, QuestMeshPath);

            // ---- Matériau Quest allégé : Simple Lit sans textures (1 passe, pas de maps) ----
            var questMat = AssetDatabase.LoadAssetAtPath<Material>(QuestMatPath);
            if (questMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                questMat = new Material(shader);
                Color baseColor = Color.white;
                var srcMat = pcvrMr.sharedMaterial;
                if (srcMat != null)
                {
                    if (srcMat.HasProperty("_BaseColor")) baseColor = srcMat.GetColor("_BaseColor");
                    else if (srcMat.HasProperty("_Color")) baseColor = srcMat.GetColor("_Color");
                }
                // Le matériau du FBX est blanc (pas de _BaseColor distinctif) : teinte acier sombre.
                if (baseColor.r > 0.9f && baseColor.g > 0.9f && baseColor.b > 0.9f)
                    baseColor = new Color(0.75f, 0.77f, 0.8f, 1f);
                questMat.SetColor("_BaseColor", baseColor);
                if (questMat.HasProperty("_Smoothness")) questMat.SetFloat("_Smoothness", 0.55f);
                if (questMat.HasProperty("_Metallic")) questMat.SetFloat("_Metallic", 0.6f);
                AssetDatabase.CreateAsset(questMat, QuestMatPath);
            }

            // ---- Prefab Quest : GO simple (mesh simplifié + matériau léger), même échelle ----
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(QuestPrefab))
            {
                var go = new GameObject("Katana_Quest");
                go.tag = "Katana";
                // Même transform local que le visuel du sabre source (échelle 3/3/3.8, rot 90° Z).
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                go.transform.localScale = pcvrMf.transform.localScale;
                go.AddComponent<MeshFilter>().sharedMesh = questMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = questMat;
                PrefabUtility.SaveAsPrefabAsset(go, QuestPrefab);
                Object.DestroyImmediate(go);
            }

            AssetDatabase.SaveAssets();
            long meshBytes = new FileInfo(QuestMeshPath).Length;
            Debug.Log($"[KatanaQuest] Quest : mesh {keptVertices} sommets / {keptTriangles} triangles " +
                      $"({100f * keptTriangles / (src.triangles.Length / 3f):F0}% du détail), asset {meshBytes / 1024} ko, " +
                      $"matériau Simple Lit sans texture. Prefabs : {PcvrPrefab}, {QuestPrefab}");
        }

        // Decimation par clustering : les sommets sont soudés sur une grille de cellule
        // ~2,5 % de la diagonale des bounds ; les triangles dégénérés disparaissent.
        // Simple, déterministe, sans dépendance ; adapté à un mesh d'arme (formes allongées).
        static Mesh Decimate(Mesh src, out int keptVertices, out int keptTriangles)
        {
            Vector3[] verts = src.vertices;
            Vector2[] uvs = src.uv;
            int[] tris = src.triangles;
            Bounds b = src.bounds;
            float cell = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * 0.015f;

            var remap = new int[verts.Length];
            var cellMap = new Dictionary<(int, int, int), int>();
            var newVerts = new List<Vector3>();
            var newUv = new List<Vector2>();
            var accum = new List<Vector3>();
            var counts = new List<int>();

            for (int i = 0; i < verts.Length; i++)
            {
                var key = (Mathf.RoundToInt(verts[i].x / cell), Mathf.RoundToInt(verts[i].y / cell), Mathf.RoundToInt(verts[i].z / cell));
                if (!cellMap.TryGetValue(key, out int idx))
                {
                    idx = newVerts.Count;
                    cellMap[key] = idx;
                    newVerts.Add(verts[i]);
                    newUv.Add(uvs != null && i < uvs.Length ? uvs[i] : Vector2.zero);
                    accum.Add(verts[i]);
                    counts.Add(1);
                }
                else
                {
                    accum[idx] += verts[i];
                    counts[idx]++;
                }
                remap[i] = idx;
            }
            for (int i = 0; i < newVerts.Count; i++) newVerts[i] = accum[i] / counts[i];

            var newTris = new List<int>();
            var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = remap[tris[i]], b2 = remap[tris[i + 1]], c = remap[tris[i + 2]];
                if (a == b2 || b2 == c || a == c) continue; // dégénéré
                var key = (Mathf.Min(a, Mathf.Min(b2, c)), a + b2 + c - Mathf.Min(a, Mathf.Min(b2, c)) - Mathf.Max(a, Mathf.Max(b2, c)), Mathf.Max(a, Mathf.Max(b2, c)));
                if (!seen.Add(key)) continue; // doublon
                newTris.Add(a); newTris.Add(b2); newTris.Add(c);
            }

            var mesh = new Mesh { name = "Katana_Quest" };
            mesh.indexFormat = newVerts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(newVerts);
            mesh.SetUVs(0, newUv);
            mesh.SetTriangles(newTris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            keptVertices = newVerts.Count;
            keptTriangles = newTris.Count / 3;
            return mesh;
        }
    }
}