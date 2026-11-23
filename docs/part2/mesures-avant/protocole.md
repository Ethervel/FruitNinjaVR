# Protocole de mesure « avant » — Part 2, étape 1

Mesure de l'état **Part 1 + mode bench** (branche `part2`, basée sur le tag `part1-remise` = b4bcf4a),
**avant toute modification d'optimisation** (pooling, Addressables).

## Machine
- CPU : Intel Core i9-14900KF
- GPU : NVIDIA GeForce RTX 3080 (driver 32.0.16.2101)
- RAM : 32 Go
- OS : Windows 11 Pro (10.0.26200)
- **Aucun casque branché** : mesures PC uniquement. Les mesures Quest seront faites plus tard.

## Versions
- Unity **6000.0.56f1** (identique à la Part 1 rendue ; la comparaison avant/après se fait à version identique).
- URP 17.0.4, XR Interaction Toolkit 3.0.8, OpenXR 1.15.1, Input System 1.14.2.
- Backend scripting Windows : **Mono** (Android : IL2CPP). GC incrémental activé.
- Qualité active : niveau 1 « PC » (URP PC, MSAA désactivé, vSync 0).

## Scénario de bench (déterministe, identique avant/après)
- Activation : `-bench` (BenchMode, `Assets/Scripts/Debug/BenchMode.cs`).
- Graine `UnityEngine.Random.InitState(12345)` avant chaque partie ; difficulté **Normal**
  (3 vies, spawn 0,5–1,5 s, bombes 10 %).
- Coupes simulées toutes les 0,35 s de temps de jeu sur le fruit actif le plus bas (y ≥ 0,3 m),
  en reproduisant exactement le chemin de `KatanaSlicer.TrySliceFruit`
  (`Slicer.Slice` → `GameFX.PlayFruitJuice` → `GameManager.OnFruitSliced` → son).
- Après 15 s : 1 bombe touchée 3 fois (`OnTouch` ×3 → explosion complète avec shake + flash).
- **3 parties** séparées par 2 retours au menu (`GoToMenu`) + relances (`StartGame`).
  Durée cible 90 s de temps de jeu par partie ; en pratique chaque partie se termine par
  game over (3 vies perdues : bombes + fruits ratés) vers ~32 s. Le scénario est identique
  avant/après (même seed, même séquence d'actions).
- vSync désactivé, `targetFrameRate = -1` : le CPU tourne à charge maximale (mesure de coût, pas de FPS plafonné).
- Fin : résumé JSON + `frames.csv` + `summary.csv`, puis quitte.

## Instrumentation (API publique `Unity.Profiling.ProfilerRecorder`, runtime)
Mesure **directement dans le build Development**, sans extraction du `.raw` :
- `ProfilerCategory.Internal`, `"Main Thread"` → temps CPU thread principal par frame (ns → ms),
  lu via `LastValue` (échantillon par frame, options `Default` = SumSamplesInFrame | WrapAround).
- `ProfilerCategory.Memory`, `"GC Allocated In Frame"` → octets alloués par frame, lu via `CurrentValue`.
- `ProfilerCategory.Memory`, `"GC Used Memory"`, `"Total Used Memory"`, `"Total Reserved Memory"`
  → octets, lus via `CurrentValue` (→ Mo).
- Collectes GC managées : `System.GC.CollectionCount(0)` au début et à la fin (et par frame dans le CSV).
- Une ligne CSV par frame : phase (loading / part1 / menu1 / part2 / menu2 / part3 / end),
  CPU ms, GC alloc ko, mémoire Mo, objets actifs (fruits / bombes / FX / morceaux), collectes.

## Protocole de capture
1. Build **Windows Development** : `BenchTools.BuildWindowsDevBench`
   (`BuildOptions.Development | ConnectWithProfiler | EnableDeepProfilingSupport`) → `Builds/WindowsDev/FruitNinjaVR.exe`.
2. Lancement sans casque :
   `FruitNinjaVR.exe -batchmode -nographics -bench -benchParts 3 -benchDuration 90 -benchSeed 12345 -benchOut <json> -logFile <log>`
   Le bench écrit lui-même `frames.csv` et `summary.csv` à côté du JSON.
3. Les indicateurs du sujet sont dans `summary.csv` (agrégats) et `frames.csv` (par frame).

## Résultats « avant » (résumé)
| Indicateur | Valeur | Unité |
|---|---|---|
| Frames mesurées | 243 916 | count |
| CPU moyen | 0,3925 | ms/frame |
| CPU p95 | 0,5526 | ms/frame |
| CPU p99 | 0,6987 | ms/frame |
| CPU max (pic) | 42,3026 | ms/frame (frame 10029, part1) |
| GC alloc total | 242,55 | Mo |
| GC alloc moyen | 1,02 | ko/frame |
| GC alloc max | 41,57 | ko/frame |
| Collectes GC (gen0, run) | 145 | count |
| Total Used mémoire début → fin | 81,78 → 110,94 | Mo |
| Total Reserved début → fin | 245,22 → 298,73 | Mo |
| Fruits créés / détruits | 102 / 102 | count |
| Bombes créées / détruites | 10 / 10 | count |
| FX créés / détruits | 108 / 105 | count |
| Morceaux créés / détruits | 204 / 204 | count |
| Score final (par partie) | 895 | points |
| Coupes / tentatives | 102 / 102 | count |
| Explosions de bombe | 3 (1/partie) | count |

Note : le CPU moyen est faible (~0,4 ms) car le bench tourne en `-batchmode -nographics`
(pas de rendu GPU) et à charge maximale sans vsync ; les pics (~42 ms) correspondent aux
coupes / explosions / collectes GC. La mémoire Total Used croît de ~82 à ~111 Mo sur les
3 parties (allocations non poolées). Ces chiffres serviront de référence « avant ».

## Périmètre mesuré / non mesuré
- **Mesuré dans le build PC Development** : CPU ms/frame (moyenne, p95, p99, max), GC alloc
  par frame et total, collectes GC, mémoire Unity (Total Used / Reserved) début/fin, instances
  créées/détruites par type, score, coupes.
- **Éditeur** : audit `0 erreur / 0 warning / 0 composant manquant` sur la scène de jeu
  (1 script manquant préexistant dans `Scenes/Tests/SlicerTestScene.unity`, hors build ; 1 shader
  non-URP préexistant sur `Fruits.dae`, source hors build).
- **Non mesuré sur Quest** : casque non branché. Les mesures Quest (CPU/GPU/mémoire sur appareil)
  seront faites à l'étape des builds Quest, avec le même scénario de bench.
- **Profiler `.raw`** : non conservé (le flux brut sans deep profiling atteignait ~10 Go pour 30 s,
  et ~30 Go avec deep profiling pour 3×90 s ; disque limité). Les indicateurs sont mesurés au runtime
  via ProfilerRecorder. Si des captures d'écran du Profiler sont nécessaires pour le rapport, elles
  seront faites à la main dans l'éditeur (Play Mode + Profiler connecté) plus tard.

## Fichiers
- `bench-summary.json` : résumé du bench (compteurs, score, mémoire, par partie).
- `frames.csv` : une ligne par frame (243 916 frames).
- `summary.csv` : agrégats (CPU moyenne/p95/p99/max, GC alloc, collectes, mémoire début/fin).
- `protocole.md` : ce document.