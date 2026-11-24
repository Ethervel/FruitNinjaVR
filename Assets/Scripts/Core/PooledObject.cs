using UnityEngine;

// Marqueur d'instance poolée (Part 2, Task 1) : relie un clone à son pool d'origine.
// Ajouté automatiquement par GameObjectPool.Get ; permet à n'importe quel système
// (spawner, slicer, sol, GameManager, timeout) de rendre l'objet au lieu de le détruire.
public class PooledObject : MonoBehaviour
{
    // Pool propriétaire (null si l'objet n'est pas poolé).
    public GameObjectPool Pool { get; private set; }

    // Nom du prefab source (sans "(Clone)") : stable à travers les réutilisations.
    public string PrefabName { get; private set; }

    // Vrai tant que l'instance est sortie du pool (en jeu).
    public bool InUse { get; private set; }

    public void Init(GameObjectPool pool, string prefabName)
    {
        Pool = pool;
        PrefabName = prefabName;
    }

    public void MarkInUse() => InUse = true;

    // Rend l'instance à son pool. Sans pool (fallback), destruction classique.
    // Sûr à appeler plusieurs fois : les appels suivants sont ignorés.
    public void ReturnToPool()
    {
        if (!InUse) return;
        InUse = false;
        if (Pool != null) Pool.Return(gameObject);
        else Destroy(gameObject);
    }
}