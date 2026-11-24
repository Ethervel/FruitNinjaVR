using System.Collections;
using UnityEngine;

// Morceau de fruit découpé, poolé (Part 2, Task 2).
// Le GameObject est réutilisé ; son Mesh aussi : le Slicer réécrit le mesh existant
// (Mesh.Clear + SetVertices...) au lieu d'allouer un nouveau Mesh à chaque coupe.
// La géométrie de découpe est inchangée (même algorithme, mêmes caps).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlicedPiece : MonoBehaviour
{
    [Tooltip("Secondes avant retour au pool (filet de sécurité si le sol ne le rattrape pas).")]
    public float lifetime = 8f;

    int generation;

    void OnEnable()
    {
        generation++;
        StartCoroutine(Expire(generation));
    }

    void OnDisable()
    {
        generation++;
        StopAllCoroutines();
    }

    IEnumerator Expire(int gen)
    {
        yield return new WaitForSeconds(lifetime);
        if (gen != generation) yield break;
        var po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }

    // Destruction finale du mesh réutilisable : seulement quand l'instance meurt vraiment.
    void OnDestroy()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "SlicedPieceMesh")
            Destroy(mf.sharedMesh);
    }
}