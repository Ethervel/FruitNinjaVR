using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

// Mode de mesure déterministe (Part 2, étape 1 « mesures avant »).
//
// Activation : argument de ligne de commande -bench (ou variable d'env FN_BENCH=1).
// Le mode joue tout seul un scénario FIXE et reproductible : graine fixe pour le
// spawner, difficulté Normal, séquence temporisée de coupes simulées sur les fruits
// (même chemin de code que KatanaSlicer.TrySliceFruit, sans casque), une bombe touchée
// jusqu'à explosion par partie, puis 2 retours au menu + relances. À la fin il écrit un
// résumé JSON (compteurs Instantiate/Destroy par type, coupes, score, mémoire) et quitte.
//
// Ce mode est le SEUL changement autorisé avant la mesure « avant » : il sert à mesurer
// la Part 1 puis la Part 2 avec EXACTEMENT le même scénario. Il n'altère ni le gameplay
// ni le spawner : il les pilote par leurs API publiques et compte les créations/destructions
// en observant la scène (aucune modification de FruitSpawner / Slicer / GameFX).
//
// Options :
//   -bench            active le mode
//   -benchOut <path>  chemin du JSON de sortie (sinon docs/part2/bench-<horodatage>.json)
//   -benchDuration <s>  durée d'une partie en secondes (défaut 90)
//   -benchParts <n>   nombre de parties (défaut 3 = 1 + 2 relances)
//   -benchSeed <n>    graine du spawner (défaut 12345)
//   -benchNoQuit      ne quitte pas à la fin (pour inspection)
public class BenchMode : MonoBehaviour
{
    const string Version = "bench-1";

    [Serializable]
    public class Counters
    {
        public int created;
        public int destroyed;
        public int peakAlive;
    }

    [Serializable]
    public class MemorySnapshot
    {
        public float totalAllocatedMB;
        public float totalReservedMB;
        public float monoUsedMB;
        public int gcCollections;
    }

    [Serializable]
    public class PartSummary
    {
        public int index;
        public float duration;
        public int score;
        public int fruitsSliced;
        public int bombsTouched;
        public int bombExplosions;
        public int sliceAttempts;
        public float wallTime;
    }

    [Serializable]
    public class PoolStats
    {
        public string pool;
        public int initialStock;
        public int created;
        public int reused;
        public int returned;
        public int idleEnd;
        public int peakInUse;
    }

    [Serializable]
    public class BenchSummary
    {
        public string version;
        public string unityVersion;
        public string platform;
        public string deviceModel;
        public string timestampUtc;
        public int seed;
        public string difficulty;
        public float partDuration;
        public int partsPlanned;
        public int partsPlayed;
        public float totalWallTime;
        public int frames;
        public int scoreFinal;
        public int totalFruitsSliced;
        public int totalBombsTouched;
        public int totalBombExplosions;
        public int totalSliceAttempts;
        public Counters fruits = new Counters();
        public Counters bombs = new Counters();
        public Counters fx = new Counters();
        public Counters pieces = new Counters();
        public MemorySnapshot memoryStart = new MemorySnapshot();
        public MemorySnapshot memoryEnd = new MemorySnapshot();
        public List<PartSummary> parts = new List<PartSummary>();
        // Part 2 : compteurs de pools (vide sur l'APK « avant », qui n'a pas de pools).
        public List<PoolStats> pools = new List<PoolStats>();
        public string notes;
    }

    // ---- configuration lue depuis la ligne de commande ----
    static bool enabled;
    static string outPath;
    static float partDuration = 90f;
    static int partsPlanned = 3;
    static int seed = 12345;
    static bool autoQuit = true;

    // ---- références runtime ----
    GameManager gm;
    Slicer slicer;
    FruitSpawner spawner;

    // ---- suivi des instances (observation de la scène) ----
    // Clé = InstanceID (stable même quand l'objet est détruit, contrairement à la référence).
    readonly Dictionary<int, int> tracked = new Dictionary<int, int>();
    // catégories : 0=fruit, 1=bombe, 2=FX, 3=morceau
    readonly int[] created = new int[4];
    readonly int[] destroyed = new int[4];
    readonly int[] peakAlive = new int[4];
    readonly int[] currentAlive = new int[4];

    // ---- compteurs d'action ----
    int sliceAttempts;
    int bombExplosions;
    int bombTouchesThisPart;
    int partsPlayed;
    float totalWallTime;
    int frames;

    readonly List<PartSummary> partSummaries = new List<PartSummary>();
    MemorySnapshot memStart;

    // ---- instrumentation runtime (API publique Unity.Profiling.ProfilerRecorder) ----
    // Mesure DIRECTEMENT dans le build Development, sans dépendre de l'extraction du .raw.
    ProfilerRecorder mainThreadRecorder;   // ProfilerCategory.Internal, "Main Thread" (ns)
    ProfilerRecorder gcAllocRecorder;      // ProfilerCategory.Memory, "GC Allocated In Frame" (octets)
    ProfilerRecorder gcUsedRecorder;       // ProfilerCategory.Memory, "GC Used Memory" (octets)
    ProfilerRecorder totalUsedRecorder;    // ProfilerCategory.Memory, "Total Used Memory" (octets)
    ProfilerRecorder totalReservedRecorder;// ProfilerCategory.Memory, "Total Reserved Memory" (octets)
    readonly List<FrameRow> frameRows = new List<FrameRow>();
    string phase = "loading";
    int gcCollectStart;

    struct FrameRow
    {
        public int frame;
        public string phase;
        public float cpuMs;
        public float gcAllocKB;
        public float gcUsedMB;
        public float totalUsedMB;
        public float totalReservedMB;
        public int aliveFruits, aliveBombs, aliveFX, alivePieces;
        public int gcCollections;
    }

    // coupe simulée : rythme fixe
    const float SliceInterval = 0.35f;
    const float SliceMinY = 0.3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        ParseArgs();
        if (!enabled) return;
        var go = new GameObject("BenchMode");
        DontDestroyOnLoad(go);
        go.AddComponent<BenchMode>();
    }

    static void ParseArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
#if UNITY_ANDROID && !UNITY_EDITOR
        // Sur Android, les extras "am start -e unity ..." n'arrivent PAS dans
        // Environment.GetCommandLineArgs() : on passe par les intents de UnityPlayerActivity.
        //   adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerActivity -e bench 1
        // (ou fichier déclencheur <persistentDataPath>/bench.flag, lu ci-dessous).
        args = ReadAndroidIntentArgs();
#endif
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-bench": enabled = true; break;
                case "-benchOut": if (i + 1 < args.Length) outPath = args[++i]; break;
                case "-benchDuration": if (i + 1 < args.Length) float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out partDuration); break;
                case "-benchParts": if (i + 1 < args.Length) int.TryParse(args[++i], out partsPlanned); break;
                case "-benchSeed": if (i + 1 < args.Length) int.TryParse(args[++i], out seed); break;
                case "-benchNoQuit": autoQuit = false; break;
            }
        }
        if (!enabled && Environment.GetEnvironmentVariable("FN_BENCH") == "1") enabled = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Fichier déclencheur (méthode de secours si les extras d'intent ne passent pas) :
        //   adb shell "run-as com.JunghoonKIM.FruitNinjaVR sh -c 'echo 1 > files/bench.flag'"
        if (!enabled && File.Exists(Path.Combine(Application.persistentDataPath, "bench.flag"))) enabled = true;
#endif
        if (partDuration <= 0f) partDuration = 90f;
        if (partsPlanned <= 0) partsPlanned = 3;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Convertit les extras d'intent en pseudo-arguments : -e bench 1 -> "-bench",
    // -e benchOut /sdcard/x.json -> "-benchOut /sdcard/x.json", etc.
    static string[] ReadAndroidIntentArgs()
    {
        var result = new List<string>();
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                if (intent == null) return result.ToArray();
                foreach (var key in new[] { "bench", "benchOut", "benchParts", "benchDuration", "benchSeed", "benchNoQuit" })
                {
                    string v = intent.Call<string>("getStringExtra", key);
                    if (string.IsNullOrEmpty(v)) continue;
                    result.Add("-" + key);
                    if (key != "bench" && key != "benchNoQuit") result.Add(v);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bench] Lecture des extras d'intent impossible : {e.Message}");
        }
        return result.ToArray();
    }
#endif

    void Start()
    {
        // Graine fixe AVANT toute génération : rend le spawner reproductible.
        UnityEngine.Random.InitState(seed);
        // Pas de vsync : le CPU travaille aussi vite que possible (mesure de charge stable).
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        gm = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
        slicer = FindFirstObjectByType<Slicer>();
        spawner = FindFirstObjectByType<FruitSpawner>();
        if (gm == null) Debug.LogError("[Bench] GameManager introuvable.");
        if (slicer == null) Debug.LogError("[Bench] Slicer introuvable.");

        memStart = SnapshotMemory();
        gcCollectStart = GC.CollectionCount(0);
        StartRecorders();
        Debug.Log($"[Bench] START seed={seed} difficulty=Normal partDuration={partDuration}s parts={partsPlanned} " +
                  $"unity={Application.unityVersion} platform={Application.platform}");
        StartCoroutine(Run());
    }

    void StartRecorders()
    {
        // "Main Thread" : temps CPU du thread principal par frame (nanosecondes).
        // Default = SumSamplesInFrame | WrapAroundWhenCapacityReached : un échantillon par frame.
        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1, ProfilerRecorderOptions.Default);
        // "GC Allocated In Frame" est un compteur PAR FRAME : on lit sa valeur courante (CurrentValue),
        // pas LastValue (qui reste figé avec une capacité de 1).
        gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1, ProfilerRecorderOptions.Default);
        // Compteurs mémoire (octets) : valeurs courantes.
        gcUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 1, ProfilerRecorderOptions.Default);
        totalUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1, ProfilerRecorderOptions.Default);
        totalReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory", 1, ProfilerRecorderOptions.Default);
        Debug.Log($"[Bench] recorders valid: main={mainThreadRecorder.Valid} gcAlloc={gcAllocRecorder.Valid} " +
                  $"gcUsed={gcUsedRecorder.Valid} totalUsed={totalUsedRecorder.Valid} totalReserved={totalReservedRecorder.Valid}");
    }

    void StopRecorders()
    {
        mainThreadRecorder.Dispose();
        gcAllocRecorder.Dispose();
        gcUsedRecorder.Dispose();
        totalUsedRecorder.Dispose();
        totalReservedRecorder.Dispose();
    }

    void OnDestroy()
    {
        if (mainThreadRecorder.Valid) StopRecorders();
    }

    IEnumerator Run()
    {
        // Laisse la scène s'initialiser (Start de tous les managers).
        yield return null;
        yield return null;

        for (int p = 0; p < partsPlanned; p++)
        {
            phase = "part" + (p + 1);
            yield return StartCoroutine(PlayPart(p));
            partsPlayed++;
            if (p < partsPlanned - 1)
            {
                // Retour au menu puis relance (le sujet demande de tester plusieurs parties).
                phase = "menu" + (p + 1);
                gm.GoToMenu();
                yield return new WaitForSeconds(0.5f);
                // Re-seed pour que chaque partie reproduise la même séquence de spawn.
                UnityEngine.Random.InitState(seed);
            }
        }

        phase = "end";
        Finish();
    }

    IEnumerator PlayPart(int index)
    {
        gm.SetDifficulty(DifficultySettings.Difficulty.Normal);
        gm.StartGame();

        int startSliced = gm.GetTotalFruitsSliced();
        int startBombs = gm.GetTotalBombsTouched();
        int startAttempts = sliceAttempts;
        int startExplosions = bombExplosions;
        bombTouchesThisPart = 0;
        float partStart = Time.time;
        float t0 = Time.realtimeSinceStartup;
        float nextSlice = Time.time + 1.0f; // laisse les premiers fruits monter

        // Pilotage par Time.time (temps de jeu) : le nombre d'actions et la durée de jeu
        // sont identiques quel que soit le framerate -> comparaison avant/après équitable.
        while (gm.IsGameRunning() && Time.time - partStart < partDuration)
        {
            if (Time.time >= nextSlice)
            {
                nextSlice = Time.time + SliceInterval;
                SimulateAction(Time.time - partStart);
            }
            yield return null;
        }

        // Force la fin si la durée est écoulée avant la fin naturelle.
        if (gm.IsGameRunning()) gm.EndGame();
        yield return new WaitForSeconds(0.3f); // laisse les Destroy delayed se produire

        var ps = new PartSummary
        {
            index = index,
            duration = Time.time - partStart,
            score = gm.GetCurrentScore(),
            fruitsSliced = gm.GetTotalFruitsSliced() - startSliced,
            bombsTouched = gm.GetTotalBombsTouched() - startBombs,
            bombExplosions = bombExplosions - startExplosions,
            sliceAttempts = sliceAttempts - startAttempts,
            wallTime = Time.realtimeSinceStartup - t0,
        };
        partSummaries.Add(ps);
        Debug.Log($"[Bench] PART {index} score={ps.score} sliced={ps.fruitsSliced} bombs={ps.bombsTouched} " +
                  $"explosions={ps.bombExplosions} attempts={ps.sliceAttempts} dur={ps.duration:F1}s");
    }

    // Reproduit KatanaSlicer.TrySliceFruit / TryTouchBomb sans casque :
    // même ordre d'appels (Slicer.Slice -> GameFX.PlayFruitJuice -> GameManager.OnFruitSliced -> audio).
    void SimulateAction(float partElapsed)
    {
        // Une bombe touchée par partie (jusqu'à explosion), après 15 s pour laisser
        // d'abord des fruits se couper. 3 touches = touchesToExplode du prefab.
        BombController bomb = partElapsed > 15f ? FindBomb() : null;
        if (bomb != null && bombTouchesThisPart < 3)
        {
            bomb.OnTouch();
            bombTouchesThisPart++;
            // 3 touches (touchesToExplode du prefab) déclenchent la séquence d'explosion.
            if (bombTouchesThisPart >= 3) bombExplosions++;
            return;
        }

        GameObject fruit = FindSliceableFruit();
        if (fruit == null) return;

        Vector3 slicePoint = fruit.transform.position;
        Vector3 sliceNormal = Vector3.up; // plan horizontal déterministe
        FruitController fc = fruit.GetComponentInParent<FruitController>();
        Color juice = fc != null ? fc.juiceColor : Color.yellow;

        // Même garde que KatanaSlicer (Part 2), par réflexion pour rester compilable à
        // l'identique sur le commit « avant » (sans TryMarkSliced : no-op) : sur l'« après »,
        // un fruit déjà coupé est ignoré (un fruit = un score).
        if (fc != null)
        {
            var guard = typeof(FruitController).GetMethod("TryMarkSliced");
            if (guard != null && !(bool)guard.Invoke(fc, null)) return;
        }
        sliceAttempts++;

        slicer.Slice(fruit, slicePoint, sliceNormal);
        if (GameFX.Instance != null) GameFX.Instance.PlayFruitJuice(slicePoint, juice);
        if (GameManager.Instance != null) GameManager.Instance.OnFruitSliced(fruit);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFruitSlice();
    }

    GameObject FindSliceableFruit()
    {
        var fruits = FindObjectsByType<FruitController>(FindObjectsSortMode.None);
        GameObject best = null;
        float bestY = float.MaxValue;
        foreach (var fc in fruits)
        {
            if (fc == null) continue;
            var go = fc.gameObject;
            if (!go.activeInHierarchy) continue;
            if (go.name.Contains("_Slice_")) continue;
            if (go.GetComponent<MeshFilter>() == null) continue;
            float y = go.transform.position.y;
            if (y < SliceMinY) continue;
            // Choisit le fruit le plus bas (déterministe, indépendant de l'ordre de FindObjects).
            if (y < bestY) { bestY = y; best = go; }
        }
        return best;
    }

    BombController FindBomb()
    {
        var bombs = FindObjectsByType<BombController>(FindObjectsSortMode.None);
        BombController best = null;
        float bestY = float.MaxValue;
        foreach (var b in bombs)
        {
            if (b == null) continue;
            var go = b.gameObject;
            if (!go.activeInHierarchy) continue;
            float y = go.transform.position.y;
            if (y < SliceMinY) continue;
            if (y < bestY) { bestY = y; best = b; }
        }
        return best;
    }

    void Update()
    {
        frames++;
        totalWallTime += Time.unscaledDeltaTime;
        TrackInstances();
        RecordFrame();
    }

    // Échantillonne les recorders à chaque frame (CPU ms, GC alloc, mémoire, objets actifs).
    void RecordFrame()
    {
        // "Main Thread" : LastValue = dernier échantillon de frame (ns) -> ms.
        float cpuMs = mainThreadRecorder.Valid && mainThreadRecorder.Count > 0 ? mainThreadRecorder.LastValue / 1e6f : 0f;
        // Compteurs par frame / mémoire : CurrentValue (valeur instantanée), pas LastValue.
        float gcAllocKB = gcAllocRecorder.Valid ? gcAllocRecorder.CurrentValue / 1024f : 0f;
        float gcUsedMB = gcUsedRecorder.Valid ? gcUsedRecorder.CurrentValue / (1024f * 1024f) : 0f;
        float totalUsedMB = totalUsedRecorder.Valid ? totalUsedRecorder.CurrentValue / (1024f * 1024f) : 0f;
        float totalReservedMB = totalReservedRecorder.Valid ? totalReservedRecorder.CurrentValue / (1024f * 1024f) : 0f;
        frameRows.Add(new FrameRow
        {
            frame = frames,
            phase = phase,
            cpuMs = cpuMs,
            gcAllocKB = gcAllocKB,
            gcUsedMB = gcUsedMB,
            totalUsedMB = totalUsedMB,
            totalReservedMB = totalReservedMB,
            aliveFruits = currentAlive[0],
            aliveBombs = currentAlive[1],
            aliveFX = currentAlive[2],
            alivePieces = currentAlive[3],
            gcCollections = GC.CollectionCount(0) - gcCollectStart,
        });
    }

    // Observe la scène pour compter créations/destructions par type, sans modifier le code de jeu.
    // Part 2 : les pools désactivent/réactivent les instances au lieu de les détruire.
    // - « destroyed » = destruction réelle ET retours au pool (fin d'usage observable) ;
    // - « created » = toute (ré)activation d'une instance, neuve ou réutilisée.
    // Les compteurs de pools proprement dits (Instantiate vs réutilisation) sont dans `pools`.
    readonly HashSet<int> seenThisFrame = new HashSet<int>();
    readonly List<int> toRemove = new List<int>();

    void TrackInstances()
    {
        seenThisFrame.Clear();

        // Détections des (ré)activations.
        Scan<FruitController>(0);
        Scan<BombController>(1);
        ScanPieces();
        ScanFX();

        // Fins d'usage observées : objet détruit OU désactivé (retour au pool).
        // Liste construite à part pour ne pas modifier le dictionnaire pendant son parcours.
        toRemove.Clear();
        foreach (var kv in tracked)
            if (!seenThisFrame.Contains(kv.Key)) toRemove.Add(kv.Key);
        foreach (var id in toRemove)
        {
            int cat = tracked[id];
            destroyed[cat]++;
            currentAlive[cat]--;
            tracked.Remove(id);
        }

        for (int c = 0; c < 4; c++)
            if (currentAlive[c] > peakAlive[c]) peakAlive[c] = currentAlive[c];
    }

    void Scan<T>(int cat) where T : Component
    {
        var comps = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in comps)
        {
            if (c == null) continue;
            Register(c.gameObject, cat);
        }
    }

    void ScanPieces()
    {
        var mfs = FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var mf in mfs)
        {
            if (mf == null) continue;
            var go = mf.gameObject;
            if (go.name.Contains("_Slice_")) Register(go, 3);
        }
    }

    void ScanFX()
    {
        var pss = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var ps in pss)
        {
            if (ps == null) continue;
            var go = ps.gameObject;
            // FX = ParticleSystem au root (les prefabs FX ont leur ParticleSystem principal au root).
            if (go.transform.parent == null && go.name.StartsWith("FX_")) Register(go, 2);
        }
    }

    void Register(GameObject go, int cat)
    {
        int id = go.GetInstanceID();
        seenThisFrame.Add(id);
        if (tracked.ContainsKey(id)) return;
        tracked[id] = cat;
        created[cat]++;
        currentAlive[cat]++;
    }

    MemorySnapshot SnapshotMemory()
    {
        return new MemorySnapshot
        {
            totalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
            totalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f),
            monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
            gcCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
        };
    }

    void Finish()
    {
        var memEnd = SnapshotMemory();
        var s = new BenchSummary
        {
            version = Version,
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString(),
            deviceModel = SystemInfo.deviceModel,
            timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            seed = seed,
            difficulty = "Normal",
            partDuration = partDuration,
            partsPlanned = partsPlanned,
            partsPlayed = partsPlayed,
            totalWallTime = totalWallTime,
            frames = frames,
            scoreFinal = gm != null ? gm.GetCurrentScore() : 0,
            totalFruitsSliced = gm != null ? gm.GetTotalFruitsSliced() : 0,
            totalBombsTouched = gm != null ? gm.GetTotalBombsTouched() : 0,
            totalBombExplosions = bombExplosions,
            totalSliceAttempts = sliceAttempts,
            fruits = ToCounters(0),
            bombs = ToCounters(1),
            fx = ToCounters(2),
            pieces = ToCounters(3),
            memoryStart = memStart,
            memoryEnd = memEnd,
            parts = partSummaries,
            pools = CollectPoolStats(),
            notes = "Compteurs obtenus par observation de la scène (FindObjectsByType), sans modification du gameplay. " +
                    "Morceaux = GameObjects dont le nom contient _Slice_. FX = ParticleSystem au root nommés FX_*. " +
                    "GC collections = GC.CollectionCount (managé). Mémoire Unity = Profiler.GetTotalAllocated/ReservedMemoryLong.",
        };

        string json = JsonUtility.ToJson(s, true);
        string path = ResolveOutPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json, new UTF8Encoding(false));
            Debug.Log($"[Bench] SUMMARY écrit : {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bench] Échec écriture JSON : {e.Message}");
        }
        Debug.Log("[Bench] SUMMARY\n" + json);

        WriteFramesCsv(Path.GetDirectoryName(path));
        WriteSummaryCsv(Path.GetDirectoryName(path));
        StopRecorders();

        if (autoQuit)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    Counters ToCounters(int c) => new Counters { created = created[c], destroyed = destroyed[c], peakAlive = peakAlive[c] };

    // Part 2 : relève les compteurs des pools via PoolRegistry, par réflexion pour que ce
    // fichier reste compilable à l'identique sur le commit « avant » (sans pools).
    List<PoolStats> CollectPoolStats()
    {
        var list = new List<PoolStats>();
        var regType = Type.GetType("PoolRegistry");
        var poolType = Type.GetType("GameObjectPool");
        if (regType == null || poolType == null) return list;
        var pools = regType.GetProperty("Pools")?.GetValue(null) as System.Collections.IEnumerable;
        if (pools == null) return list;
        foreach (var p in pools)
        {
            if (p == null) continue;
            int I(string prop) { var v = poolType.GetProperty(prop)?.GetValue(p); return v is int n ? n : 0; }
            list.Add(new PoolStats
            {
                pool = poolType.GetProperty("PoolName")?.GetValue(p) as string ?? "?",
                initialStock = I("initialStock"),
                created = I("Created"),
                reused = I("Reused"),
                returned = I("Returned"),
                idleEnd = I("IdleCount"),
                peakInUse = I("PeakInUse"),
            });
        }
        return list;
    }

    // frames.csv : une ligne par frame (phase, CPU ms, GC alloc ko, mémoire Mo, objets actifs, collectes).
    void WriteFramesCsv(string dir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,phase,cpu_ms,gc_alloc_ko,gc_used_mo,total_used_mo,total_reserved_mo,alive_fruits,alive_bombs,alive_fx,alive_pieces,gc_collections");
        foreach (var r in frameRows)
            sb.AppendLine(string.Join(",",
                r.frame.ToString(CultureInfo.InvariantCulture),
                r.phase,
                r.cpuMs.ToString("F4", CultureInfo.InvariantCulture),
                r.gcAllocKB.ToString("F2", CultureInfo.InvariantCulture),
                r.gcUsedMB.ToString("F3", CultureInfo.InvariantCulture),
                r.totalUsedMB.ToString("F3", CultureInfo.InvariantCulture),
                r.totalReservedMB.ToString("F3", CultureInfo.InvariantCulture),
                r.aliveFruits.ToString(CultureInfo.InvariantCulture),
                r.aliveBombs.ToString(CultureInfo.InvariantCulture),
                r.aliveFX.ToString(CultureInfo.InvariantCulture),
                r.alivePieces.ToString(CultureInfo.InvariantCulture),
                r.gcCollections.ToString(CultureInfo.InvariantCulture)));
        string p = Path.Combine(dir, "frames.csv");
        File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[Bench] frames.csv écrit : {p} ({frameRows.Count} frames)");
    }

    // summary.csv : agrégats CPU (moyenne/p95/max), allocations, collectes, mémoire début/fin.
    void WriteSummaryCsv(string dir)
    {
        var cpu = new List<float>();
        var alloc = new List<float>();
        foreach (var r in frameRows) { cpu.Add(r.cpuMs); alloc.Add(r.gcAllocKB); }
        cpu.Sort();
        float Mean(List<float> l) => l.Count > 0 ? l.Average() : 0f;
        float Pct(List<float> s, float p) => s.Count > 0 ? s[Mathf.Min(s.Count - 1, (int)(s.Count * p))] : 0f;
        float sumAllocKB = 0f; foreach (var a in alloc) sumAllocKB += a;
        int gcCollectTotal = frameRows.Count > 0 ? frameRows[frameRows.Count - 1].gcCollections : 0;
        float usedStart = frameRows.Count > 0 ? frameRows[0].totalUsedMB : 0f;
        float usedEnd = frameRows.Count > 0 ? frameRows[frameRows.Count - 1].totalUsedMB : 0f;
        float reservedStart = frameRows.Count > 0 ? frameRows[0].totalReservedMB : 0f;
        float reservedEnd = frameRows.Count > 0 ? frameRows[frameRows.Count - 1].totalReservedMB : 0f;

        var sb = new StringBuilder();
        sb.AppendLine("indicateur,valeur,unite");
        sb.AppendLine($"frames,{frameRows.Count},count");
        sb.AppendLine($"cpu_mean,{Mean(cpu):F4},ms");
        sb.AppendLine($"cpu_p95,{Pct(cpu, 0.95f):F4},ms");
        sb.AppendLine($"cpu_p99,{Pct(cpu, 0.99f):F4},ms");
        sb.AppendLine($"cpu_max,{(cpu.Count > 0 ? cpu[cpu.Count - 1] : 0f):F4},ms");
        sb.AppendLine($"cpu_min,{(cpu.Count > 0 ? cpu[0] : 0f):F4},ms");
        sb.AppendLine($"gc_alloc_total,{sumAllocKB / 1024f:F2},Mo");
        sb.AppendLine($"gc_alloc_mean_per_frame,{Mean(alloc):F2},ko");
        sb.AppendLine($"gc_alloc_max_per_frame,{(alloc.Count > 0 ? alloc.Max() : 0f):F2},ko");
        sb.AppendLine($"gc_collections,{gcCollectTotal},count");
        sb.AppendLine($"total_used_start,{usedStart:F3},Mo");
        sb.AppendLine($"total_used_end,{usedEnd:F3},Mo");
        sb.AppendLine($"total_reserved_start,{reservedStart:F3},Mo");
        sb.AppendLine($"total_reserved_end,{reservedEnd:F3},Mo");
        string p = Path.Combine(dir, "summary.csv");
        File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[Bench] summary.csv écrit : {p}");
        Debug.Log($"[Bench] MESURES cpu_mean={Mean(cpu):F3}ms cpu_p95={Pct(cpu, 0.95f):F3}ms cpu_max={(cpu.Count > 0 ? cpu[cpu.Count - 1] : 0f):F3}ms " +
                  $"gc_alloc_total={sumAllocKB / 1024f:F2}Mo gc_collections={gcCollectTotal} used={usedStart:F1}->{usedEnd:F1}Mo");
    }

    string ResolveOutPath()
    {
        if (!string.IsNullOrEmpty(outPath)) return outPath;
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
#if UNITY_ANDROID && !UNITY_EDITOR
        // Sur le casque : stockage privé de l'app (pas de permission requise), récupérable
        // en Development avec : adb shell "run-as com.JunghoonKIM.FruitNinjaVR cat files/bench/bench-summary.json"
        return Path.Combine(Application.persistentDataPath, "bench", "bench-summary.json");
#else
        return Path.Combine(Application.dataPath, "..", "docs", "part2", $"bench-{stamp}.json");
#endif
    }
}