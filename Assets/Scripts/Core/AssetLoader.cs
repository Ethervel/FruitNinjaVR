using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Chargement du contenu variable par plateforme via Addressables (Part 2, Tasks 3 et 4).
//
// Cycle de vie exigé par le sujet :
//   1. LoadAssetAsync AVANT la première partie (préchargement au démarrage de la scène) ;
//   2. les pools sont alimentés par les assets chargés (prefabs FX, katana) ;
//   3. le HANDLE est gardé tant que des clones existent (les clones référencent les
//      mesh/matériaux/textures du bundle : le libérer trop tôt les détruirait) ;
//   4. à l'arrêt : destruction des instances (pools) PUIS Addressables.Release(handle).
//
// Groupes : FX_PCVR (Windows) et FX_Quest (Android), même adresse pour les deux variantes
// (« FX_FruitJuice », « FX_BombExplosion », « Katana ») : seul le catalogue du build décide
// quel bundle contient l'asset. La détection de plateforme se fait donc AU BUILD (chaque
// groupe n'est inclus que dans le build de sa plateforme), plus un contrôle runtime.
public class AssetLoader : MonoBehaviour
{
    public static AssetLoader Instance { get; private set; }

    public const string JuiceKey = "FX_FruitJuice";
    public const string ExplosionKey = "FX_BombExplosion";
    public const string KatanaKey = "Katana";

    // Vrai quand le préchargement est terminé (succès OU échec traité).
    public bool IsReady { get; private set; }
    public bool UsedFallback { get; private set; }
    public string LastError { get; private set; }

    // Handles gardés tant que des clones existent (un par asset chargé).
    readonly Dictionary<string, AsyncOperationHandle> handles = new Dictionary<string, AsyncOperationHandle>();

    // Repli si un chargement échoue : références directes (celles de la Part 1),
    // embarquées dans la scène. Le jeu reste jouable, l'échec est journalisé.
    [Header("Repli (références directes, utilisées si Addressables échoue)")]
    public ParticleSystem fruitJuicePCVRFallback;
    public ParticleSystem bombExplosionPCVRFallback;
    public ParticleSystem fruitJuiceQuestFallback;
    public ParticleSystem bombExplosionQuestFallback;
    public GameObject katanaFallback;

    // Simulation d'échec de chargement (test documenté, PC) : -benchFailAssets ou
    // variable d'env FN_FAIL_ASSETS=1 -> adresses invalides, déclenche le repli.
    // Sur Android, l'échec peut aussi être provoqué en supprimant le dossier
    // aa/ de l'APK installé (voir docs/part2/protocole-casque.md) — non requis au PC.
    public static bool SimulateFailure
    {
        get
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-benchFailAssets") return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            // mêmes extras d'intent que le bench : -e benchFailAssets 1
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    if (intent != null && intent.Call<string>("getStringExtra", "benchFailAssets") == "1") return true;
            }
            catch { /* pas d'activité (tests éditeur) */ }
#endif
            return Environment.GetEnvironmentVariable("FN_FAIL_ASSETS") == "1";
        }
    }

    public static bool IsQuestPlatform
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        yield return StartCoroutine(Preload());
    }

    // Préchargement AVANT la partie : FX (jus + explosion) et katana, puis alimentation
    // des pools FX et application du katana. Le bench attend IsReady avant de jouer.
    public IEnumerator Preload()
    {
        IsReady = false;
        UsedFallback = false;
        LastError = null;
        string suffix = SimulateFailure ? "__FAIL__" : "";
        Debug.Log($"[AssetLoader] Préchargement Addressables ({(IsQuestPlatform ? "Quest" : "PCVR")})" +
                  (SimulateFailure ? " — ÉCHEC SIMULÉ (-benchFailAssets)" : ""));

        var juice = Addressables.LoadAssetAsync<GameObject>(JuiceKey + suffix);
        var explosion = Addressables.LoadAssetAsync<GameObject>(ExplosionKey + suffix);
        var katana = Addressables.LoadAssetAsync<GameObject>(KatanaKey + suffix);
        yield return juice;
        yield return explosion;
        yield return katana;

        var juicePrefab = Resolve(juice, JuiceKey,
            IsQuestPlatform ? fruitJuiceQuestFallback : fruitJuicePCVRFallback);
        var explosionPrefab = Resolve(explosion, ExplosionKey,
            IsQuestPlatform ? bombExplosionQuestFallback : bombExplosionPCVRFallback);
        var katanaPrefab = Resolve(katana, KatanaKey, katanaFallback);

        // Alimente les pools FX avec les prefabs chargés (stocks justifiés dans GameFX).
        if (GameFX.Instance != null)
            GameFX.Instance.SetupPools(juicePrefab != null ? juicePrefab.GetComponent<ParticleSystem>() : null,
                                      explosionPrefab != null ? explosionPrefab.GetComponent<ParticleSystem>() : null);

        // Katana : remplace le visuel des sabres par la variante chargée.
        KatanaVariantApplier.Apply(katanaPrefab);

        IsReady = true;
        Debug.Log($"[AssetLoader] Préchargement terminé. fallback={UsedFallback} handles={handles.Count}" +
                  (LastError != null ? $" erreur=\"{LastError}\"" : ""));
    }

    // En cas de succès : garde le handle (les clones vivants en dépendent).
    // En cas d'échec : journalise, libère le handle raté, bascule sur la référence directe.
    T Resolve<T>(AsyncOperationHandle<GameObject> handle, string key, T fallback) where T : UnityEngine.Object
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            handles[key] = handle; // gardé jusqu'à ReleaseAll (après destruction des clones)
            // Pour T = GameObject, l'asset EST le GameObject (GetComponent<GameObject> rend null).
            if (handle.Result is T direct) return direct;
            var asset = handle.Result.GetComponent<T>();
            if (asset != null) return asset;
            Debug.LogError($"[AssetLoader] Asset '{key}' chargé mais composant {typeof(T).Name} absent.");
        }
        else
        {
            LastError = handle.OperationException != null ? handle.OperationException.Message : $"statut {handle.Status}";
            Debug.LogError($"[AssetLoader] ÉCHEC de chargement de '{key}' : {LastError}. Repli sur référence directe.");
            UsedFallback = true;
        }
        if (handle.IsValid()) Addressables.Release(handle);
        return fallback;
    }

    // Libération finale : appelée à la destruction de la scène, APRÈS la destruction
    // des instances poolées (l'ordre des OnDestroy n'étant pas garanti, on diffère d'une frame).
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Les clones poolés sont détruits avec la scène (les onDestroy des pools et du
        // GameFX tournent dans le même déchargement). La libération est différée d'une
        // frame sur un hôte persistant pour garantir l'ordre : instances PUIS Release.
        if (handles.Count == 0) return;
        var host = new GameObject("AssetLoader_Release");
        DontDestroyOnLoad(host);
        host.AddComponent<DeferredRelease>().Run(new List<AsyncOperationHandle>(handles.Values));
        handles.Clear();
    }

    class DeferredRelease : MonoBehaviour
    {
        public void Run(List<AsyncOperationHandle> list) => StartCoroutine(ReleaseNextFrame(list));

        IEnumerator ReleaseNextFrame(List<AsyncOperationHandle> list)
        {
            yield return null; // laisse les OnDestroy des pools détruire clones et matériaux
            foreach (var h in list)
                if (h.IsValid()) Addressables.Release(h);
            Debug.Log($"[AssetLoader] {list.Count} handles Addressables libérés.");
            Destroy(gameObject);
        }
    }
}