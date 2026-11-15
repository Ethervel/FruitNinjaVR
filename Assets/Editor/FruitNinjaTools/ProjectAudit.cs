// Outil d'audit du projet (Part 1, Task 1 et Task 2).
// Lancement : menu Tools > Fruit Ninja > Audit du projet,
// ou en ligne de commande : Unity.exe -batchmode -projectPath . -executeMethod FruitNinjaTools.ProjectAudit.Run -quit
// Le rapport est écrit dans Logs/FruitNinjaAudit.txt (dossier ignoré par git).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruitNinjaTools
{
    public static class ProjectAudit
    {
        const string ReportPath = "Logs/FruitNinjaAudit.txt";

        [MenuItem("Tools/Fruit Ninja/Audit du projet")]
        public static void Run()
        {
            var sb = new StringBuilder();
            int missingScripts = 0, brokenRefs = 0, missingPrefabs = 0, badShaders = 0;

            sb.AppendLine($"Unity {Application.unityVersion}");
            sb.AppendLine($"Build target actif : {EditorUserBuildSettings.activeBuildTarget}");
            sb.AppendLine($"Render pipeline par défaut : {Describe(GraphicsSettings.defaultRenderPipeline)}");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                sb.AppendLine($"  Qualité[{i}] {QualitySettings.names[i]} : {Describe(QualitySettings.GetRenderPipelineAssetAt(i))}");

            sb.AppendLine("\n== Paquets");
            foreach (var p in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                         .Where(p => p.isDirectDependency).OrderBy(p => p.name))
                sb.AppendLine($"  {p.name} {p.version} ({p.source})");

            sb.AppendLine("\n== Scènes du build");
            foreach (var s in EditorBuildSettings.scenes)
                sb.AppendLine($"  [{(s.enabled ? "x" : " ")}] {s.path} {(File.Exists(s.path) ? "" : "(INTROUVABLE)")}");

            sb.AppendLine("\n== Scènes");
            string setup = SceneManager_SaveSetup();
            foreach (var path in Find("t:Scene"))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var issues = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        CheckGameObject(t.gameObject, issues, ref missingScripts, ref brokenRefs, ref missingPrefabs);
                sb.AppendLine($"  {path} : {issues.Count} problème(s)");
                foreach (var s in issues) sb.AppendLine("    " + s);
                if (path.EndsWith("SampleScene.unity") || path.Contains("/Scenes/FruitNinja"))
                    DumpHierarchy(scene, sb);
            }

            sb.AppendLine("\n== Prefabs");
            foreach (var path in Find("t:Prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var issues = new List<string>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    CheckGameObject(t.gameObject, issues, ref missingScripts, ref brokenRefs, ref missingPrefabs);
                if (issues.Count > 0)
                {
                    sb.AppendLine($"  {path} : {issues.Count} problème(s)");
                    foreach (var s in issues) sb.AppendLine("    " + s);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            sb.AppendLine("\n== Matériaux (shader manquant ou non URP)");
            foreach (var path in Find("t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) { sb.AppendLine($"  {path} : shader NULL"); badShaders++; continue; }
                string sh = m.shader.name;
                bool bad = sh == "Hidden/InternalErrorShader" || !m.shader.isSupported
                           || sh == "Standard" || sh.StartsWith("Legacy Shaders/");
                if (bad) { sb.AppendLine($"  {path} : {sh}"); badShaders++; }
            }

            sb.AppendLine("\n== Fichiers à la racine d'Assets");
            foreach (var f in Directory.GetFileSystemEntries("Assets").Where(f => !f.EndsWith(".meta")).OrderBy(f => f))
                sb.AppendLine("  " + f.Replace('\\', '/'));

            sb.Insert(0, $"TOTAL missingScripts={missingScripts} brokenRefs={brokenRefs} missingPrefabs={missingPrefabs} badShaders={badShaders}\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[FruitNinjaAudit] missingScripts={missingScripts} brokenRefs={brokenRefs} missingPrefabs={missingPrefabs} badShaders={badShaders} -> {ReportPath}");
            if (!Application.isBatchMode && !string.IsNullOrEmpty(setup)) EditorSceneManager.OpenScene(setup);
        }

        static string SceneManager_SaveSetup()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty && !Application.isBatchMode) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            return active.path;
        }

        static IEnumerable<string> Find(string filter) =>
            AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith("Assets/TextMesh Pro/") && !p.StartsWith("Assets/Samples/"))
                .Distinct().OrderBy(p => p);

        static string Describe(Object o) => o == null ? "aucun (Built-in)" : $"{o.name} ({o.GetType().Name}) {AssetDatabase.GetAssetPath(o)}";

        static void CheckGameObject(GameObject go, List<string> issues, ref int missingScripts, ref int brokenRefs, ref int missingPrefabs)
        {
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (n > 0) { missingScripts += n; issues.Add($"{PathOf(go)} : {n} script(s) manquant(s)"); }

            if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.IsPrefabAssetMissing(go)
                && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
            { missingPrefabs++; issues.Add($"{PathOf(go)} : prefab source manquant"); }

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                    { brokenRefs++; issues.Add($"{PathOf(go)} : {c.GetType().Name}.{it.propertyPath} -> référence cassée"); }
                }
            }
        }

        static void DumpHierarchy(UnityEngine.SceneManagement.Scene scene, StringBuilder sb)
        {
            sb.AppendLine($"  -- Hiérarchie de {scene.path}");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    int depth = 0; for (var p = t.parent; p != null; p = p.parent) depth++;
                    var comps = go.GetComponents<Component>().Where(c => c != null && !(c is Transform))
                        .Select(c => c.GetType().Name);
                    string prefab = PrefabUtility.IsOutermostPrefabInstanceRoot(go)
                        ? $" <{AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go))}>" : "";
                    sb.AppendLine($"    {new string(' ', depth * 2)}{go.name}{(go.activeSelf ? "" : " (inactif)")}{prefab} [{string.Join(", ", comps)}]");

                    // Détail des références des composants du rig XR (actions des contrôleurs, poses).
                    bool inRig = root.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "XROrigin")
                                 || root.name.Contains("XR");
                    if (!inRig && !go.name.Contains("Katana")) continue;
                    foreach (var c in go.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)))
                    {
                        var it = new SerializedObject(c).GetIterator();
                        while (it.NextVisible(true))
                            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null
                                && (it.propertyPath.Contains("Action") || it.propertyPath.Contains("action") || it.propertyPath.Contains("Reference")))
                                sb.AppendLine($"    {new string(' ', depth * 2)}  · {c.GetType().Name}.{it.propertyPath} = {it.objectReferenceValue.name}");
                    }
                }
        }

        static string PathOf(GameObject go)
        {
            var t = go.transform; string p = go.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
