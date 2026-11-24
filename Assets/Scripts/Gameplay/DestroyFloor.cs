using UnityEngine;

public class DestroyFloor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string[] tagsToDestroy = { "Fruit" };
    
    void OnTriggerEnter(Collider other)
    {
        foreach (string tag in tagsToDestroy)
        {
            if (other.gameObject.tag == tag) // CompareTag journalise une erreur si le tag n'existe pas (ex. « Slice » de la scène)
            {
                Retire(other.gameObject);
                return;
            }
        }

        // Alternative: vérifier par le nom (si vous n'utilisez pas de tags)
        if (other.gameObject.name.Contains("Slice") || other.gameObject.name.Contains("(Conc)"))
        {
            Retire(other.gameObject);
        }
    }

    // Pooling (Part 2) : le sol REND l'objet à son pool au lieu de le détruire.
    static void Retire(GameObject go)
    {
        var po = go.GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(go);
    }
}