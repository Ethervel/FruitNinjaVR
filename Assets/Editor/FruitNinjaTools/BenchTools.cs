// Outils de mesure Part 2 (étape 1 : « mesures avant » sur l'état Part 1 + bench).
// Build Windows Development avec Autoconnect Profiler, joué en mode bench sans casque.
//   Unity.exe -batchmode -quit -buildTarget StandaloneWindows64 -projectPath . -executeMethod FruitNinjaTools.BenchTools.BuildWindowsDevBench -logFile <log>
// puis :
//   Builds/WindowsDev/FruitNinjaVR.exe -bench -benchParts 3 -benchDuration 90 -profiler-log-file <abs.raw> -profiler-enable-deep-profiling
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FruitNinjaTools
{
    public static class BenchTools
    {
        const string Output = "Builds/WindowsDev/FruitNinjaVR.exe";

        // Variante Development du build Windows de la Part 1 : mêmes scènes et réglages,
        // Development + AutoconnectProfiler + DeepProfiling pour la mesure « avant ».
        public static void BuildWindowsDevBench()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.EnableDeepProfilingSupport,
            });
            var s = report.summary;
            Debug.Log($"[BenchTools] Build dev bench -> {Output} : {s.result}, erreurs {s.totalErrors}, warnings {s.totalWarnings}, durée {s.totalTime:mm\\:ss}, taille {s.totalSize} octets, Unity {Application.unityVersion}");
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}