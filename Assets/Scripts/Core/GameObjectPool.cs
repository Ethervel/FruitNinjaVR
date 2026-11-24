using System.Collections.Generic;
using UnityEngine;

// Pool générique par prefab (Part 2, Tasks 1 et 2).
//
// Cycle de vie d'une instance :
//   Instantiate (une seule fois, stock initial ou croissance)  ->  Get (réactivation +
//   remise à zéro physique/visuelle)  ->  en jeu  ->  Return (désactivation, retour au stock).
// La DESTRUCTION FINALE n'a lieu qu'à l'arrêt du jeu / au déchargement du pool
// (DestroyAll, ou destruction de la scène) : jamais pendant une partie.
//
// Réinitialisation À LA RÉACTIVATION (Get), pas au retour : l'objet désactivé conserve
// son dernier état (utile au debug), et rien de l'ancien usage ne peut fuiter vers le suivant.
public class GameObjectPool : MonoBehaviour
{
    [Tooltip("Prefab source (référence directe ou asset chargé par Addressables).")]
    public GameObject prefab;
    [Tooltip("Stock initial instancié à la création du pool.")]
    public int initialStock = 8;
    [Tooltip("Plafond d'instances (0 = illimité). Au-delà, Get rend null et l'appelant se rabat sur Instantiate.")]
    public int maxSize = 0;

    readonly Stack<GameObject> idle = new Stack<GameObject>();
    Transform idleRoot;

    public string PoolName => prefab != null ? prefab.name : name;
    public int Created { get; private set; }   // Instantiate effectués par ce pool
    public int Reused { get; private set; }    // Get servis depuis le stock
    public int Returned { get; private set; }  // retours au stock
    public int InUse => Created - idle.Count;
    public int IdleCount => idle.Count;
    public int PeakInUse { get; private set; }

    public void Init(GameObject source, int stock, int cap = 0)
    {
        prefab = source;
        initialStock = Mathf.Max(0, stock);
        maxSize = cap;
        EnsureRoot();
        for (int i = 0; i < initialStock; i++) CreateInstance();
        PoolRegistry.Register(this);
    }

    // Remplace le prefab source (ex. asset chargé par Addressables) et préchauffe le stock.
    // À n'appeler que sur un pool vide de toute instance (avant la première partie).
    public void SetPrefab(GameObject source, int stock, int cap = 0)
    {
        if (idleRoot != null)
            foreach (Transform child in idleRoot) if (child != null) Destroy(child.gameObject);
        idle.Clear();
        Created = 0; Reused = 0; Returned = 0; PeakInUse = 0;
        prefab = source;
        initialStock = Mathf.Max(0, stock);
        maxSize = cap;
        EnsureRoot();
        for (int i = 0; i < initialStock; i++) CreateInstance();
    }

    void EnsureRoot()
    {
        if (idleRoot != null) return;
        var go = new GameObject("Pool_" + PoolName);
        go.transform.SetParent(transform, false);
        go.SetActive(false); // tout le stock dort ici, hors de la simulation
        idleRoot = go.transform;
    }

    GameObject CreateInstance()
    {
        EnsureRoot();
        var go = Instantiate(prefab, idleRoot);
        go.name = prefab.name; // pas de "(Clone)" : noms stables pour le suivi/bench
        var po = go.GetComponent<PooledObject>();
        if (po == null) po = go.AddComponent<PooledObject>();
        po.Init(this, prefab.name);
        go.SetActive(false);
        idle.Push(go);
        Created++;
        return go;
    }

    // Sort une instance réinitialisée, placée et orientée. Null si plafond atteint.
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        GameObject go;
        if (idle.Count > 0) { go = idle.Pop(); Reused++; }
        else
        {
            if (maxSize > 0 && Created >= maxSize) return null;
            go = CreateInstance();
            idle.Pop();
        }

        ResetInstance(go, position, rotation);
        go.GetComponent<PooledObject>().MarkInUse();
        if (InUse > PeakInUse) PeakInUse = InUse;
        return go;
    }

    // Réinitialisation complète à la réactivation (exigence Task 1) :
    // transform, vitesses, physique, colliders, apparence. Les données de jeu
    // (type, valeur, état coupé/explosé, durée de vie) sont réinitialisées par les
    // composants eux-mêmes via OnEnable (FruitController, BombController, PooledLifetime).
    void ResetInstance(GameObject go, Vector3 position, Quaternion rotation)
    {
        var t = go.transform;
        t.SetParent(null, false);
        t.SetPositionAndRotation(position, rotation);
        t.localScale = prefab.transform.localScale;

        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.Sleep();
        }
        // Colliders : présents sur le prefab -> état restauré depuis le prefab ;
        // ajoutés en jeu (morceaux) -> laissés tels quels, le jeu les reconfigure.
        var prefabCols = prefab.GetComponentsInChildren<Collider>(true);
        if (prefabCols.Length > 0)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length && i < prefabCols.Length; i++)
                cols[i].enabled = prefabCols[i].enabled;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        go.SetActive(true); // déclenche OnEnable des composants de jeu (reset de leurs données)
    }

    // Retour au stock : désactivation seulement (la réinitialisation aura lieu au prochain Get).
    public void Return(GameObject go)
    {
        if (go == null) return;
        var po = go.GetComponent<PooledObject>();
        if (po == null || po.Pool != this) { Destroy(go); return; }
        EnsureRoot();
        go.SetActive(false);
        go.transform.SetParent(idleRoot, false);
        idle.Push(go);
        Returned++;
    }

    // Destruction finale : uniquement à l'arrêt / au déchargement (voir en-tête).
    // Les instances encore en jeu sont rendues d'abord pour être détruites avec le stock.
    public void DestroyAll()
    {
        // Copie : Return modifie la pile idle pendant le parcours.
        var inFlight = new List<PooledObject>(GetComponentsInChildren<PooledObject>(true));
        foreach (var po in inFlight)
            if (po != null && po.InUse && po.Pool == this) Return(po.gameObject);
        foreach (var go in idle) if (go != null) Destroy(go);
        idle.Clear();
    }

    void OnDestroy()
    {
        PoolRegistry.Unregister(this);
        // À la destruction de la scène, Unity détruit de toute façon chaque instance
        // (elles ne sont pas DontDestroyOnLoad) : pas de Destroy supplémentaire ici.
    }
}