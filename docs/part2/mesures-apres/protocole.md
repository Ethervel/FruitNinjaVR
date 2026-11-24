# Protocole de mesure « après » — Part 2 (PC)

Même machine, même version Unity (6000.0.56f1), même build Development
(+ Autoconnect Profiler + Deep Profiling), même scénario bench que l'« avant »
(`docs/part2/mesures-avant/protocole.md`) : seed 12345, Normal, 3 parties ≤ 90 s,
coupes simulées toutes les 0,35 s de temps de jeu, bombe ×3 après 15 s, 2 retours menu.

Commande :

```
Builds/WindowsDev/FruitNinjaVR.exe -batchmode -nographics -bench -benchParts 3 \
  -benchDuration 90 -benchSeed 12345 -benchOut <dir>/bench-summary.json -logFile <log>
```

## Lecture des compteurs

- `fruits/bombs/fx/pieces.created` = (ré)activations observées (identiques avant/après :
  le scénario est le même) ; `destroyed` = fins d'usage observées (destruction OU retour au pool).
- `pools[]` = compteurs RÉELS des pools (Part 2) : `created` = Instantiate effectués
  (74 au total = stocks initiaux, aucune croissance), `reused` = réutilisations (423),
  `returned` = retours au stock, `peakInUse` ≤ stock partout.
- Avant : 424 Instantiate / 424 Destroy sur le scénario ; après : **74 Instantiate, 0 Destroy en jeu**.

## Écarts assumés par rapport à l'« avant »

1. **Score 895 → 815 par partie** (identique sur les 3 parties, ×3). Cause : les bombes
   explosées ne restent plus figées dans la scène (retour au pool ~2 s après le flash) ;
   le bench, qui re-touche la bombe la plus basse après 15 s, touche alors une NOUVELLE
   bombe (3 pénalités de plus par partie : 3 × 25 = 75... écart constaté 80 avec l'effet
   de bord sur les combos). C'est le comportement correct face à un joueur réel ;
   l'« avant » profitait de bombes figées déjà explosées (état résiduel). Fruits coupés
   (34/partie) et séquence de spawn identiques.
2. **Alloc GC max/frame 243,3 ko** (contre 41,6 ko avant) : pic ponctuel au chargement
   Addressables (catalogue + bundles, une seule fois au démarrage, phase `loading`).
   Le TOTAL d'allocations baisse (242,6 → 224,9 Mo) et les collectes gen0 aussi (145 → 134).
3. **CPU max 43,9 ms** : pic de chargement initial (bundle + préchauffage des pools),
   même nature que le pic de 42,3 ms de l'avant (première frame). p95/p99 en baisse
   (0,55/0,70 → 0,44/0,56 ms), CPU moyen 0,39 → 0,37 ms.

## Test d'échec de chargement (`echec-assets/`)

`-benchFailAssets` : les 3 adresses deviennent invalides → 3 échecs journalisés
(`InvalidKeyException`), repli sur les références directes, `fallback=True handles=0`.
Le bench complète ses 3 parties avec le même score 815 et les mêmes 34 coupes/partie.
ATTENTION : cette exécution a subi une suspension machine en cours de route
(pic CPU max 105 588 ms = temps mural, allocations gonflées) : ses métriques CPU/GC
ne sont PAS comparables ; seuls les compteurs de jeu et le comportement de repli
sont exploités. La comparaison avant/après utilise `../bench-summary.json` (run propre).