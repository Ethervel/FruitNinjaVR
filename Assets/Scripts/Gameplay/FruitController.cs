using UnityEngine;

public class FruitController : MonoBehaviour
{
    [Header("Fruit Settings")]
    public int pointsValue = 10;
    public bool isSpecial = false;
    [Tooltip("Couleur de l'éclaboussure de jus (Part 1, Task 3).")]
    public Color juiceColor = new Color(1f, 0.85f, 0.3f);

    private bool wasSliced = false;
    private int generation; // pooling : invalide les coroutines d'un usage précédent

    void Start()
    {
        // S'assurer que le fruit a un Rigidbody pour la physique
        if (GetComponent<Rigidbody>() == null)
        {
            gameObject.AddComponent<Rigidbody>();
        }
    }

    // Pooling (Part 2, Task 1) : réinitialisation des données de jeu à chaque réactivation.
    void OnEnable()
    {
        wasSliced = false;
        generation++;
    }

    void OnDisable()
    {
        generation++;
        StopAllCoroutines();
    }

    // Garde centralisée « un fruit = une coupe = un score » (Part 2, Task 1).
    // Appelée par les DEUX sabres et par le bench : le premier appel rend true,
    // les suivants (autre sabre, même frame ou frame suivante) rendent false.
    public bool TryMarkSliced()
    {
        if (wasSliced) return false;
        wasSliced = true;
        return true;
    }

    public bool WasSliced => wasSliced;

    public void OnSliced()
    {
        if (!TryMarkSliced()) return;

        // Notifier le GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFruitSliced(gameObject);
        }

        // Désactiver la physique et ajouter un effet de disparition
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Retour au pool (ou destruction) après un délai
        StartCoroutine(ReturnAfter(2f, generation));
    }

    void OnBecameInvisible()
    {
        // Si le fruit sort de l'écran sans être coupé
        if (!wasSliced && GameManager.Instance != null)
        {
            GameManager.Instance.OnFruitMissed(gameObject);
            StartCoroutine(ReturnAfter(1f, generation));
        }
    }

    System.Collections.IEnumerator ReturnAfter(float delay, int gen)
    {
        yield return new WaitForSeconds(delay);
        if (gen != generation) yield break; // instance réutilisée entre-temps : ne rien faire
        var po = GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(gameObject);
    }
}