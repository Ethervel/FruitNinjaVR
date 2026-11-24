# Protocole de mesure Quest (session casque unique)

Les deux APK de référence sont construits depuis la même branche `part2`, avec le **même scénario
bench** (graine 12345, difficulté Normal, 3 parties de ≤ 90 s, coupes simulées toutes les 0,35 s de
temps de jeu, bombe touchée ×3 après 15 s, 2 retours menu + relances — `Assets/Scripts/Debug/BenchMode.cs`).

| Référence | Commit source | APK | Build Windows équivalent |
|---|---|---|---|
| **avant** (Part 1 + bench, sans pooling ni Addressables) | `c68d99e` + instrumentation Android uniquement | `Builds/Bench/avant/FruitNinjaVR_avant_bench.apk` | `Builds/Bench/avant/Windows/FruitNinjaVR.exe` |
| **après** (Part 2 : pools, FX poolés, Addressables, katana 2 variantes) | HEAD de `part2` à la fin de la Part 2 | `Builds/Bench/apres/FruitNinjaVR_apres_bench.apk` | `Builds/Bench/apres/Windows/FruitNinjaVR.exe` |

Les deux APK sont des builds **Development + Autoconnect Profiler + Deep Profiling** :
- même identifiant d'application `com.JunghoonKIM.FruitNinjaVR` → `adb install -r` remplace l'un par l'autre ;
- l'instrumentation (`ProfilerRecorder`) écrit les mesures **dans le build**, sans fichier `.raw` ;
- le Profiler de l'éditeur peut s'y connecter en USB pour les captures d'écran (voir session-casque.md).

Seule différence de code entre l'APK « avant » et le commit `c68d99e` : l'ajout, dans `BenchMode.cs`,
de la lecture des extras d'intent Android et du chemin de sortie Quest (ci-dessous). Aucun changement
de gameplay, de spawner ou de slicer — le scénario et les compteurs sont identiques.

## Lancement en mode bench sur le Quest

Sur Android, les extras `am start -e unity ...` **n'arrivent pas** dans
`Environment.GetCommandLineArgs()`. La méthode retenue (implémentée dans `BenchMode.ParseArgs`,
branche `UNITY_ANDROID`) lit les extras de l'intent via `UnityPlayer.currentActivity.getIntent()` :

```bash
adb install -r Builds/Bench/avant/FruitNinjaVR_avant_bench.apk
adb shell am force-stop com.JunghoonKIM.FruitNinjaVR
adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerActivity -e bench 1
```

Extras reconnus (tous facultatifs, défauts identiques au PC) :
`-e bench 1`, `-e benchParts 3`, `-e benchDuration 90`, `-e benchSeed 12345`,
`-e benchOut /sdcard/bench.json`, `-e benchNoQuit 1`.

Méthode de secours si les extras ne passent pas (fichier déclencheur lu au démarrage) :

```bash
adb shell "run-as com.JunghoonKIM.FruitNinjaVR sh -c 'echo 1 > files/bench.flag'"
adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerActivity
# après la mesure, supprimer le déclencheur pour rejouer normalement :
adb shell "run-as com.JunghoonKIM.FruitNinjaVR rm files/bench.flag"
```

> À vérifier au branchement du casque : que l'extra `bench` active bien le mode (log
> `[Bench] START seed=12345 ...` dans `adb logcat -s Unity`). Sinon, utiliser `bench.flag`.

Le bench joue les 3 parties puis quitte l'application tout seul (~3 × 35 s de jeu + chargements,
le game over survient vers 32 s à 3 vies comme sur PC).

## Où sont les résultats sur le casque

Défaut : `Application.persistentDataPath` =
`/storage/emulated/0/Android/data/com.JunghoonKIM.FruitNinjaVR/files/bench/`

- `bench-summary.json` (compteurs, mémoire, résumés de parties)
- `frames.csv` (une ligne par frame : CPU ms, GC ko, mémoire Mo, instances actives)
- `summary.csv` (agrégats CPU moyenne/p95/p99/max, allocations, collectes GC)

Récupération (build Development → `run-as` autorisé ; pas de permission stockage requise) :

```bash
P=com.JunghoonKIM.FruitNinjaVR
adb shell "run-as $P cat files/bench/bench-summary.json" > bench-summary.json
adb shell "run-as $P cat files/bench/frames.csv"         > frames.csv
adb shell "run-as $P cat files/bench/summary.csv"        > summary.csv
```

Solution de repli si `run-as` échoue : relancer avec `-e benchOut /sdcard/bench.json`
(écriture directe sur le stockage partagé, accessible à l'app en Development), puis
`adb pull /sdcard/bench.json` (+ `frames.csv`/`summary.csv` dans le même dossier).

## Identité du scénario avant/après

- Graine `UnityEngine.Random.InitState(12345)` avant chaque partie (le spawner n'utilise que
  `UnityEngine.Random`) → même séquence de prefabs, positions, forces.
- Pilotage par `Time.time` (temps de jeu) : mêmes actions quel que soit le framerate.
- Les compteurs d'instances sont obtenus par observation de la scène (`FindObjectsByType`),
  sans instrumentation du gameplay ; les compteurs de pool (étape 2+) s'ajoutent au JSON « après ».