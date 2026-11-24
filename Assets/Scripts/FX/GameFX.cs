using System.Collections;
using UnityEngine;

// Effets de particules (Part 1 Task 3, poolés en Part 2 Task 2).
// Chaque effet existe en deux variantes ; la variante Quest est utilisée sur Android.
//
// Part 2 : chaque variante a son POOL — plus d'Instantiate ni de Destroy par éclaboussure.
// Clear + Play à la sortie du pool (aucune particule de l'usage précédent), retour au
// pool à la fin de l'effet (FXReturn). À l'étape 3, les prefabs des pools viendront
// d'Addressables (SetupPools appelé par AssetLoader) ; les références directes de la
// scène servent de base tant que le préchargement n'a pas eu lieu, et de repli.
public class GameFX : MonoBehaviour
{
    public enum Variant { Auto, PCVR, Quest }

    public static GameFX Instance { get; private set; }

    [Header("Variantes PCVR")]
    public ParticleSystem fruitJuicePCVR;
    public ParticleSystem bombExplosionPCVR;

    [Header("Variantes Quest")]
    public ParticleSystem fruitJuiceQuest;
    public ParticleSystem bombExplosionQuest;

    [Header("Réglages")]
    [Tooltip("Auto : Quest sur Android, PCVR sur les autres plateformes.")]
    public Variant variant = Variant.Auto;
    [Tooltip("Échelle de l'explosion pour une touche de bombe qui ne la fait pas encore exploser.")]
    [Range(0.1f, 1f)]
    public float bombTouchScale = 0.5f;

    [Header("Pools FX (Part 2, Task 2)")]
    [Tooltip("Stock initial de jus. Justification : 1 coupe / 0,35 s (bench) × ~1,6 s d'effet ≈ 5 effets simultanés ; 8 couvre les rafales de coupes réelles.")]
    public int juicePoolStock = 8;
    [Tooltip("Stock initial d'explosions. Justification : 3 effets par bombe (2 touches + explosion), 1 bombe à la fois en pratique ; 4 suffit, croissance possible.")]
    public int explosionPoolStock = 4;

    GameObjectPool juicePool;
    GameObjectPool explosionPool;

    public bool IsQuest => variant == Variant.Quest || (variant == Variant.Auto && IsAndroidBuild);

    static bool IsAndroidBuild
    {
        get
        {
#if UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }
    }

    public ParticleSystem FruitJuicePrefab => IsQuest ? fruitJuiceQuest : fruitJuicePCVR;
    public ParticleSystem BombExplosionPrefab => IsQuest ? bombExplosionQuest : bombExplosionPCVR;

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        // Étape 3 (Addressables) : attend le préchargement AVANT la partie ; AssetLoader
        // appelle SetupPools avec les assets chargés (ou les replis en cas d'échec).
        // Timeout de sécurité : sans AssetLoader dans la scène, pools sur références directes.
        float deadline = Time.realtimeSinceStartup + 10f;
        while (AssetLoader.Instance != null && !AssetLoader.Instance.IsReady &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (juicePool == null)
            SetupPools(FruitJuicePrefab, BombExplosionPrefab);
        Debug.Log($"[GameFX] variante utilisée : {(IsQuest ? "Quest" : "PCVR")} " +
                  $"(jus={juicePool?.PoolName}, explosion={explosionPool?.PoolName}) " +
                  $"pools : jus {juicePool?.IdleCount ?? 0}, explosion {explosionPool?.IdleCount ?? 0}");
    }

    // Crée (ou ré-alimente) les pools avec les prefabs donnés. À l'étape 3, AssetLoader
    // l'appelle avec les assets chargés par Addressables AVANT la première partie.
    public void SetupPools(ParticleSystem juice, ParticleSystem explosion)
    {
        if (juice != null)
        {
            if (juicePool == null) juicePool = gameObject.AddComponent<GameObjectPool>();
            juicePool.SetPrefab(juice.gameObject, juicePoolStock);
        }
        if (explosion != null)
        {
            if (explosionPool == null) explosionPool = gameObject.AddComponent<GameObjectPool>();
            explosionPool.SetPrefab(explosion.gameObject, explosionPoolStock);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Destruction finale des instances FX (fin de scène) — jamais pendant une partie.
        if (juicePool != null) juicePool.DestroyAll();
        if (explosionPool != null) explosionPool.DestroyAll();
    }

    // Éclaboussure au point de contact, teintée de la couleur du fruit.
    public ParticleSystem PlayFruitJuice(Vector3 point, Color juiceColor)
    {
        var fx = Spawn(juicePool, FruitJuicePrefab, point, 1f);
        if (fx == null) return null;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            float alpha = main.startColor.color.a;
            main.startColor = new Color(juiceColor.r, juiceColor.g, juiceColor.b, alpha);
        }
        ClearAndPlay(fx);
        return fx;
    }

    public ParticleSystem PlayBombExplosion(Vector3 position, float scale = 1f)
    {
        var fx = Spawn(explosionPool, BombExplosionPrefab, position, scale);
        if (fx != null) ClearAndPlay(fx);
        return fx;
    }

    // Sort une instance du pool (fallback : Instantiate classique si pas de pool).
    static ParticleSystem Spawn(GameObjectPool pool, ParticleSystem fallbackPrefab, Vector3 position, float scale)
    {
        ParticleSystem fx = null;
        if (pool != null)
        {
            var go = pool.Get(position, Quaternion.identity);
            if (go != null) fx = go.GetComponent<ParticleSystem>();
        }
        if (fx == null)
        {
            if (fallbackPrefab == null) return null;
            fx = Instantiate(fallbackPrefab, position, Quaternion.identity);
            Destroy(fx.gameObject, MaxDuration(fx) + 0.5f); // pas de pool : comportement Part 1
        }
        fx.transform.localScale = Vector3.one * scale;
        return fx;
    }

    // Clear + Play À LA RÉUTILISATION : aucune particule de l'usage précédent ne subsiste.
    static void ClearAndPlay(ParticleSystem root)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
        root.Play(true);

        // Retour au pool à la fin de l'effet (durée + marge), au lieu de Destroy.
        var po = root.GetComponent<PooledObject>();
        if (po != null)
        {
            var ret = root.GetComponent<FXReturn>();
            if (ret == null) ret = root.gameObject.AddComponent<FXReturn>();
            ret.ReturnAfter(MaxDuration(root) + 0.5f);
        }
    }

    static float MaxDuration(ParticleSystem root)
    {
        float max = 0f;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
            max = Mathf.Max(max, ps.main.duration + ps.main.startLifetime.constantMax);
        return max;
    }
}

// Retour au pool d'un FX à la fin de sa durée de vie (Part 2, Task 2).
// Remplace Destroy(fx, durée) ; réarmé à chaque Play, caduc si l'instance est rendue avant.
public class FXReturn : MonoBehaviour
{
    int generation;

    public void ReturnAfter(float delay)
    {
        StopAllCoroutines();
        StartCoroutine(ReturnRoutine(++generation, delay));
    }

    void OnDisable()
    {
        generation++;
        StopAllCoroutines();
    }

    IEnumerator ReturnRoutine(int gen, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gen != generation) yield break;
        var po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
    }
}