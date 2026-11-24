using System.Collections.Generic;
using UnityEngine;

// Registre des pools de la scène (Part 2). Permet au bench et au rapport de lire les
// compteurs créations / réutilisations / stock sans instrumenter le gameplay.
public static class PoolRegistry
{
    static readonly List<GameObjectPool> pools = new List<GameObjectPool>();

    public static IReadOnlyList<GameObjectPool> Pools => pools;

    public static void Register(GameObjectPool pool)
    {
        if (!pools.Contains(pool)) pools.Add(pool);
    }

    public static void Unregister(GameObjectPool pool) => pools.Remove(pool);

    public static int TotalCreated { get { int n = 0; foreach (var p in pools) n += p.Created; return n; } }
    public static int TotalReused { get { int n = 0; foreach (var p in pools) n += p.Reused; return n; } }
    public static int TotalReturned { get { int n = 0; foreach (var p in pools) n += p.Returned; return n; } }
}