using UnityEngine;

// Effets de particules (Part 1, Task 3) : jus à la coupe et explosion de bombe.
// Chaque effet existe en deux variantes ; la variante Quest est choisie sur Android
// (build Quest ou éditeur en cible Android), la variante PCVR ailleurs.
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
        Debug.Log($"[GameFX] variante utilisée : {(IsQuest ? "Quest" : "PCVR")} ({FruitJuicePrefab?.name}, {BombExplosionPrefab?.name})");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Éclaboussure au point de contact, teintée de la couleur du fruit.
    public ParticleSystem PlayFruitJuice(Vector3 point, Color juiceColor)
    {
        var fx = Spawn(FruitJuicePrefab, point, 1f);
        if (fx == null) return null;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            float alpha = main.startColor.color.a;
            main.startColor = new Color(juiceColor.r, juiceColor.g, juiceColor.b, alpha);
        }
        fx.Play(true);
        return fx;
    }

    public ParticleSystem PlayBombExplosion(Vector3 position, float scale = 1f)
    {
        var fx = Spawn(BombExplosionPrefab, position, scale);
        if (fx != null) fx.Play(true);
        return fx;
    }

    static ParticleSystem Spawn(ParticleSystem prefab, Vector3 position, float scale)
    {
        if (prefab == null) return null;
        var fx = Instantiate(prefab, position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        Destroy(fx.gameObject, MaxDuration(fx) + 0.5f);
        return fx;
    }

    static float MaxDuration(ParticleSystem root)
    {
        float max = 0f;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
            max = Mathf.Max(max, ps.main.duration + ps.main.startLifetime.constantMax);
        return max;
    }
}
