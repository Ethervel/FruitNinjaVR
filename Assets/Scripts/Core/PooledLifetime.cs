using System.Collections;
using UnityEngine;

// Durée de vie d'une instance poolée (Part 2, Task 1) : remplace Destroy(objet, délai).
// Le délai est réarmé à chaque activation (OnEnable) et rendu caduc à la désactivation :
// une réutilisation ne peut jamais être tuée par le timeout de l'usage précédent.
public class PooledLifetime : MonoBehaviour
{
    [Tooltip("Secondes avant retour au pool (0 = pas de timeout).")]
    public float seconds = 10f;

    int generation;

    void OnEnable()
    {
        StartCoroutine(Expire(++generation));
    }

    void OnDisable()
    {
        generation++; // invalide le compte à rebours en cours
        StopAllCoroutines();
    }

    IEnumerator Expire(int gen)
    {
        // Le délai peut être fixé juste APRÈS l'activation (AddComponent en jeu) :
        // on attend qu'il soit positif avant d'armer le compte à rebours.
        while (seconds <= 0f)
        {
            yield return null;
            if (gen != generation) yield break;
        }
        yield return new WaitForSeconds(seconds);
        if (gen != generation) yield break;
        var po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}