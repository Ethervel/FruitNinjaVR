# Mesures Quest « après » — protocole et résultats (session casque du 25/11)

## APK corrigé pendant la session
L'APK initial (commit 472aa1c) avait un catalogue Addressables Quest SANS l'entrée Katana
(groupe FX_Quest sérialisé avec 2 entrées au lieu de 3) → au casque : échec
`ChainOperation failed` sur 'Katana', repli sur Katana_PCVR (mesh détaillé 1275 sommets).
Corrigé : AddressablesSetup vérifie la sérialisation de chaque entrée ; contenu Quest +
APK rebuilds (0 erreur) ; catalogue vérifié : Katana_Quest présent. Ancien APK archivé
en scratch (FruitNinjaVR_apres_bench_SANS_KATANA.apk.bak).

## Run de référence (casque porté, 02:31–02:33)
- `[KatanaVariant] Variante 'Katana_Quest' appliquée à 2 sabre(s) (132 sommets, 1 matériau)`
- `[AssetLoader] Préchargement terminé. fallback=False handles=3` — AUCUN échec.
- `[GameFX] variante utilisée : Quest` — jus 8, explosion 4.
- Parties : 25/13/37 coupes (25,5/16,9/33,8 s), 3 touches + 1 explosion chacune.
  Scénario NON identique au run avant (51 coupes/70,7 s) ni au PC : la physique à 72 fps
  rend la séquence sensible à l'état initial (cf. mesures-avant-quest/protocole.md).
  Comparaison normalisée par coupe et par minute dans comparatif-quest.csv et le rapport.
- VrApi : 72/72 FPS (73/72 dominant), App moyen 8,87 ms, CPU&GPU moyen 12,44 ms
  (avant : 8,62 / 13,31). 1 frame à 1 FPS = fin de run (quit), pas une régression.

## Chiffres clés (frames.csv, pic système >1 s exclu des deux côtés)
| | Avant | Après |
|---|---|---|
| Frames / durée | 4275 / 70,7 s | 5541 / 77,4 s |
| Coupes totales | 51 | 75 |
| CPU moyen / p95 (ms) | 13,95 / 14,57 | 13,91 / 14,38 |
| GC alloc (ko/frame) | 1,37 | 1,17 |
| GC alloc (ko/coupe) | 115,0 | 86,8 |
| Collectes GC | 28 | 38 |
| Instantiate réels | (à la demande) | 74 = stocks |
| Réutilisations pools | 0 | 322 |
| Destroy en jeu | oui | 0 |
| Mémoire utilisée CSV fin (Mo) | 845,0 | 893,8 |

Mémoire : +50 Mo = pré-allocation des stocks au démarrage ; croissance ensuite quasi nulle
(+0,5 Mo sur les 2000 dernières frames) → pas de fuite. Les stocks Quest (74 instances)
sont dimensionnés pour Normal ; pics d'utilisation ≤ stocks partout (peakInUse max 7/24
sur les morceaux).

## Fichiers
bench-summary.json / frames.csv / summary.csv (run de référence), vrapi.log,
variantes.log (logs AssetLoader/GameFX/KatanaVariant), comparatif-quest.csv.
