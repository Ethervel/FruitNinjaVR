# Session casque — checklist unique (à faire, casque branché)

Tout ce qui pouvait être fait sans casque est fait (pooling, Addressables, variantes, mesures PC,
builds). Cette session regroupe **toutes** les mesures et tous les tests Quest en une fois.
Durée estimée : **35–45 min** (2 × ~8 min de bench + installations IL2CPP + captures + jeu libre).

Prérequis : Quest 3 en mode développeur, câble USB, `adb devices` le voit ;
éditeur Unity 6000.0.56f1 ouvert sur le projet (pour les captures Profiler) ;
PCVR : Link/Air Link actif pour le test PC VR.

Variables : `P=com.JunghoonKIM.FruitNinjaVR`, dossier de collecte `docs/part2/mesures-quest/`.

## 1. Mesures « avant » Quest (~10 min)

```bash
adb install -r Builds/Bench/avant/FruitNinjaVR_avant_bench.apk
adb shell am force-stop com.JunghoonKIM.FruitNinjaVR
adb logcat -c
adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerGameActivity -e bench 1
adb logcat -s Unity   # vérifier "[Bench] START seed=12345" ; sinon : bench.flag (protocole-casque.md)
```

- Laisser tourner jusqu'au quit automatique (~3–4 min). NE PAS retirer le casque pendant la mesure
  (le proximity sensor met l'app en pause ; le poser sur le front ou désactiver le capteur).
- Pendant la partie 2 : Unity Editor > Window > Analysis > Profiler > cible « AndroidPlayer (USB) »
  → captures d'écran CPU + Memory (module Timeline) → `mesures-quest/avant/profiler-cpu.png`,
  `profiler-memory.png`.
- Récupérer :

```bash
mkdir -p docs/part2/mesures-quest/avant
adb shell "run-as com.JunghoonKIM.FruitNinjaVR cat files/bench/bench-summary.json" > docs/part2/mesures-quest/avant/bench-summary.json
adb shell "run-as com.JunghoonKIM.FruitNinjaVR cat files/bench/frames.csv"         > docs/part2/mesures-quest/avant/frames.csv
adb shell "run-as com.JunghoonKIM.FruitNinjaVR cat files/bench/summary.csv"        > docs/part2/mesures-quest/avant/summary.csv
adb exec-out screencap -p > docs/part2/mesures-quest/avant/ecran.png   # si l'app est encore affichée
```

## 2. Mesures « après » Quest (~10 min)

```bash
adb install -r Builds/Bench/apres/FruitNinjaVR_apres_bench.apk
adb shell am force-stop com.JunghoonKIM.FruitNinjaVR
adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerGameActivity -e bench 1
```

- Mêmes captures Profiler → `mesures-quest/apres/`, mêmes `run-as cat` → `mesures-quest/apres/`.
- Vérifier dans le JSON : `pools` (réutilisations > 0, créations ≈ stocks initiaux),
  `score` identique à l'avant (895/partie attendu, même seed).

## 3. Test de jeu rapide Quest (~8 min)

Relancer sans extra bench (supprimer `bench.flag` s'il a été créé) :

```bash
adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerGameActivity
```

Checklist visuelle (2–3 parties, difficulté Normal puis Hard) :
- [ ] Jus de fruit à chaque coupe, couleur du fruit, variante **Quest** (log `[GameFX] variante utilisée : Quest`)
- [ ] Explosion de bombe : touches 1–2 (petite), 3e (shake + flash + explosion complète)
- [ ] Katana : variante **Quest** chargée (log `[AssetLoader]`), aspect correct, découpe fonctionnelle
- [ ] Morceaux de fruits : découpe nette, cap coloré, chute et disparition normales
- [ ] 3 parties d'affilée + retours menu : pas de fruit fantôme, score/vies remis à zéro, pas de ralentissement
- [ ] capture d'écran en jeu : `adb exec-out screencap -p > mesures-quest/apres/jeu.png`

## 4. Test PCVR via Link (~7 min)

```bash
Builds/Bench/apres/Windows/FruitNinjaVR.exe   # ou Builds/Windows/FruitNinjaVR.exe (release)
```

- [ ] Variante **PCVR** des FX (jus + explosion plus riches), log `[GameFX] variante utilisée : PCVR`
- [ ] Katana variante **PCVR** (mesh détaillé)
- [ ] 2 parties + retour menu : mêmes vérifications d'état résiduel qu'au Quest

## 5. Clôture

- Compléter les tableaux Quest du rapport (`docs/part2/rapport/`) avec les summary.csv récupérés.
- Remplacer les encadrés « session casque à faire » par les résultats et captures.
- Commit `mesures(part2): mesures Quest avant/après + tests casque`.