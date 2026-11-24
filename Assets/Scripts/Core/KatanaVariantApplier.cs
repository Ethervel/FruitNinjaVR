using System.Collections.Generic;
using UnityEngine;

// Applique la variante de katana chargée par Addressables (Part 2, Task 4) aux sabres
// de la scène. Les sabres existants (prefabs Katana_Left / Katana_Right, détaillés)
// restent la référence PCVR et le repli ; sur Quest, leur visuel (mesh + matériaux)
// est remplacé par la variante allégée chargée depuis le groupe FX_Quest.
//
// On ne remplace PAS le GameObject du sabre : il est référencé par le rig XR
// (attach points des manettes) ; on ne touche qu'au MeshFilter / MeshRenderer /
// MeshCollider, en conservant tag, layer, Rigidbody et KatanaSlicer.
public static class KatanaVariantApplier
{
    public static bool Applied { get; private set; }
    public static string AppliedVariant { get; private set; }

    public static void Apply(GameObject variantPrefab)
    {
        if (variantPrefab == null)
        {
            Debug.LogWarning("[KatanaVariant] Prefab variante nul : sabres inchangés (repli PCVR).");
            return;
        }

        var srcFilter = variantPrefab.GetComponentInChildren<MeshFilter>();
        var srcRenderer = variantPrefab.GetComponentInChildren<MeshRenderer>();
        if (srcFilter == null || srcFilter.sharedMesh == null || srcRenderer == null)
        {
            Debug.LogError("[KatanaVariant] Prefab variante sans MeshFilter/MeshRenderer : sabres inchangés.");
            return;
        }

        var slicers = Object.FindObjectsByType<KatanaSlicer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int n = 0;
        foreach (var slicer in slicers)
        {
            var go = slicer.gameObject;
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            var mc = go.GetComponent<MeshCollider>();
            if (mf == null || mr == null) continue;

            mf.sharedMesh = srcFilter.sharedMesh;
            mr.sharedMaterials = srcRenderer.sharedMaterials;
            if (mc != null) mc.sharedMesh = srcFilter.sharedMesh;
            n++;
        }

        Applied = n > 0;
        AppliedVariant = variantPrefab.name;
        Debug.Log($"[KatanaVariant] Variante '{variantPrefab.name}' appliquée à {n} sabre(s) " +
                  $"(mesh '{srcFilter.sharedMesh.name}', {srcFilter.sharedMesh.vertexCount} sommets, " +
                  $"{srcRenderer.sharedMaterials.Length} matériau(x)).");
    }
}