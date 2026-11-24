using System.Collections.Generic;
using UnityEngine;

// Point d'entrée des pools fruits / bonus / bombes (Part 2, Task 1).
// Un pool PAR PREFAB : les instances ne changent jamais de type en cours de vie.
//
// Stocks initiaux justifiés à partir des mesures « avant » (docs/part2/mesures-avant) :
//   - pic d'instances fruits vivantes mesuré : 10 (toutes variétés confondues, Normal).
//     Pic PAR VARIÉTÉ ≈ 2-3 ; stock 4 par fruit ordinaire couvre le pic sans croissance
//     dans le scénario bench, tout en restant modeste (8 variétés × 4 = 32 instances).
//   - bonus : 7 % des spawns, pic observé 1-2 → stock 2.
//   - bombes : ~10 % des spawns, durée de vie longue (10 s + explosion ~2 s) → stock 4.
// Les pools peuvent croître au-delà (maxSize = 0) si une difficulté plus rapide l'exige.
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [Header("Stocks initiaux")]
    public int fruitStock = 4;
    public int bonusStock = 2;
    public int bombStock = 4;

    readonly Dictionary<GameObject, GameObjectPool> poolsByPrefab = new Dictionary<GameObject, GameObjectPool>();

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Crée (ou retrouve) le pool du prefab et préchauffe son stock.
    // Appelé par FruitSpawner.Start et par AssetLoader (Addressables).
    public GameObjectPool Warm(GameObject prefab, int stock)
    {
        if (prefab == null) return null;
        if (poolsByPrefab.TryGetValue(prefab, out var existing)) return existing;
        var pool = gameObject.AddComponent<GameObjectPool>();
        pool.Init(prefab, stock);
        poolsByPrefab[prefab] = pool;
        return pool;
    }

    public GameObjectPool GetPool(GameObject prefab)
    {
        if (prefab == null) return null;
        poolsByPrefab.TryGetValue(prefab, out var pool);
        return pool;
    }

    // Sort une instance du pool du prefab ; fallback Instantiate si pas de pool
    // (sécurité : le jeu doit fonctionner même sans PoolManager).
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var pool = GetPool(prefab);
        if (pool != null)
        {
            var go = pool.Get(position, rotation);
            if (go != null) return go;
        }
        return Instantiate(prefab, position, rotation);
    }

    public int StockFor(GameObject prefab)
    {
        if (prefab == null) return 0;
        if (prefab.GetComponent<BombController>() != null) return bombStock;
        var fc = prefab.GetComponent<FruitController>();
        if (fc != null && fc.isSpecial) return bonusStock;
        return fruitStock;
    }
}