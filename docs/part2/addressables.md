# Addressables — Part 2, Tasks 3 et 4

Paquet : `com.unity.addressables` **2.10.3** (compatible Unity 6000.0.56f1).

## Groupes, adresses, labels

| Groupe | Plateforme | Assets (adresse) | Labels |
|---|---|---|---|
| `FX_PCVR` | Windows | `FX_FruitJuice` → FX_FruitJuice_PCVR.prefab, `FX_BombExplosion` → FX_BombExplosion_PCVR.prefab, `Katana` → Katana_PCVR.prefab | `fx`, `katana`, `FX_PCVR` |
| `FX_Quest` | Android | `FX_FruitJuice` → FX_FruitJuice_Quest.prefab, `FX_BombExplosion` → FX_BombExplosion_Quest.prefab, `Katana` → Katana_Quest.prefab | `fx`, `katana`, `FX_Quest` |

- **Mêmes adresses** dans les deux groupes : le code charge `FX_FruitJuice`, `FX_BombExplosion`,
  `Katana` sans connaître la plateforme. C'est le **catalogue du build** qui résout l'adresse
  vers le bundle de SA plateforme.
- Profils `PCVR` et `Quest` : bundles locaux dans `aa/PCVR/` et `aa/Quest/`
  (BuildPath `…BuildPath]/<plateforme>`, LoadPath `…RuntimePath}/<plateforme>`).
- `IncludeInBuild` : `FruitNinjaTools.AddressablesSetup.SetPlatform("PCVR"|"Quest")` active
  le profil et n'inclut QUE le groupe de la plateforme ; appelé par
  `AddressablesBuild.BuildContentPCVR` / `BuildContentQuest` avant `BuildPlayerContent`.
- Compression LZ4 (chargement rapide), PackTogether (1 bundle par groupe).
- Les fruits/bombes restent des références directes du spawner (pooling Task 1) : le sujet
  ne demande Addressables que pour les FX et le prefab supplémentaire.

## Cycle de vie (AssetLoader, `Assets/Scripts/Core/AssetLoader.cs`)

1. **LoadAssetAsync AVANT la partie** : coroutine `Preload` au démarrage de la scène ;
   le bench attend `IsReady` avant la première coupe. GameFX attend aussi (pools alimentés
   par `SetupPools` avec les assets chargés).
2. **Pools alimentés par les handles** : `GameObjectPool.SetPrefab(asset chargé, stock)` —
   les clones référencent les mesh/matériaux/textures du bundle.
3. **Handle gardé tant que des clones existent** : dictionnaire `handles`, jamais libéré
   pendant le jeu.
4. **Destruction des instances AVANT Release** : à la destruction de la scène, les pools
   détruisent leurs clones (`DestroyAll` / OnDestroy) ; la libération des handles est
   différée d'une frame sur un hôte persistant (`DeferredRelease`) pour garantir l'ordre.
5. **Échec de chargement** : journalisé (`[AssetLoader] ÉCHEC…`), handle raté libéré,
   **repli sur références directes** (celles de la Part 1, embarquées dans la scène) :
   le jeu reste jouable.

## Contenu du build ≠ contenu chargé en mémoire

- **Contenu du build** : ce qui est LIVRÉ dans l'APK / le dossier du jeu. Ici : les bundles
  de la plateforme active (`aa/Quest/…` dans l'APK, `aa/PCVR/…` à côté de l'exe), le catalogue,
  plus les assets référencés directement par les scènes (fruits, bombes, décor, replis FX).
  L'autre variante n'est PAS dans le build (groupe exclu par `IncludeInBuild`).
- **Contenu chargé en mémoire** : ce qui est RÉSIDENT à l'exécution. Un bundle sur le disque
  ne coûte rien tant qu'il n'est pas chargé ; `LoadAssetAsync` charge le bundle + l'asset,
  et le handle maintient l'ensemble en mémoire. Les clones des pools ne dupliquent pas les
  mesh/textures (ils les référencent). `Release(handle)` décharge le bundle quand plus
  aucun clone n'en dépend.
- Conséquence mesurable : la taille du build dépend des groupes inclus ; la mémoire
  (`Total Used Memory` du bench) dépend des handles actifs et des pools préchauffés.

## Test du cas d'échec de chargement (simulé sur PC)

- Lancement : `FruitNinjaVR.exe -bench -benchFailAssets …` (ou `FN_FAIL_ASSETS=1`).
  AssetLoader charge alors des adresses invalides (`FX_FruitJuice__FAIL__`…) : les trois
  chargements échouent, l'erreur est journalisée, les replis directs prennent le relais.
- Attendu : log `[AssetLoader] ÉCHEC de chargement … Repli sur référence directe`,
  `fallback=True`, le bench complète ses 3 parties avec le MÊME score (les replis sont
  les prefabs de la Part 1). Résultats archivés dans `docs/part2/mesures-apres/echec-assets/`.
- Sur Quest, le même mécanisme existe (`-e benchFailAssets 1`), mais le test n'est pas
  requis au casque (le repli est identique).

## Katana : 2 variantes (Task 4)

- `Katana_PCVR.prefab` : copie conforme du sabre détaillé de la Part 1
  (mesh FBX 1275 sommets / 851 triangles, matériau d'origine).
- `Katana_Quest.prefab` : mesh simplifié **132 sommets / 260 triangles (~31 %)**,
  asset 14 ko, matériau URP **Simple Lit sans texture** (une passe, teinte acier).
  Généré par decimation editor (`KatanaQuestBuilder`, clustering de sommets sur grille
  1,5 % de la diagonale + suppression des triangles dégénérés, normales recalculées) —
  pas de dépendance externe, reproductible par commande.
- Application : `KatanaVariantApplier` remplace mesh/matériaux/MeshCollider des sabres
  de la scène par la variante chargée (le GameObject du sabre, référencé par le rig XR,
  n'est pas remplacé). Sur PC la variante PCVR = visuel identique à la Part 1.