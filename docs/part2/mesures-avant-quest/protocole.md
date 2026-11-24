# Mesures Quest « avant » — protocole et constats (session casque du 25/11)

## Conditions
- Quest 3 (2G0YC1ZG3T0539), Android 14, build vOS 205, câble USB, `adb install -r`.
- APK : `Builds/Bench/avant/FruitNinjaVR_avant_bench.apk` (code c68d99e + activation Android).
- Lancement : `adb shell am start -n com.JunghoonKIM.FruitNinjaVR/com.unity3d.player.UnityPlayerGameActivity -e bench 1`
  (ATTENTION : l'activité est **UnityPlayerGameActivity**, pas UnityPlayerActivity — la doc
  session-casque.md initiale avait l'ancien nom, corrigé).
- Extras d'intent : FONCTIONNENT (vérifié au casque, `[Bench] START seed=12345`), pas besoin de bench.flag.
- Affichage : 72 Hz pendant le jeu (VrApi `FPS=72/72` ou `73/72`), 90 Hz au menu système.
- VrApi run de référence : App moyen 8,62 ms, CPU&GPU moyen 13,31 ms (63 échantillons).

## Veille du casque : piège majeur
Le capteur de proximité endort l'app dès que le casque est posé : le bench se fige
(`APP_CMD_STOP`), et un run interrompu produit des parties TRONQUÉES (ex. 13 coupes au lieu
de 25, score faux). `svc power stayon true` n'empêche PAS la veille VR du Quest.
**Le casque doit être porté (ou capteur couvert) pendant tout le bench.**
3 runs ont été faits ; les runs interrompus sont archivés dans le scratch, pas ici.

## Scénario Quest ≠ scénario PC (constat honnête)
Le bench est piloté par `Time.time` mais la physique des fruits dépend du framerate :
- PC (~2500 fps en -nographics) : 34 coupes / 31,4 s / score 895 par partie.
- Quest (72 fps) : 13 ou 25 coupes / 16,9 ou 25,1 s selon les parties, scores 55–655.
Les fruits montent moins haut entre deux frames à 72 fps et passent moins de temps
au-dessus du seuil de coupe (y ≥ 0,3) ; des bombes touchées par le bench font perdre
les 3 vies et terminent la partie plus tôt. La seed est la même mais la séquence
physique diverge. **La comparaison avant/après Quest reste équitable** (même build de
bench des deux côtés, même appareil, même seed) ; la comparaison Quest vs PC ne l'est pas.
Variabilité inter-runs observée (3 runs avant) : 13/13/25, 13/13/13, 13/25/13 coupes.
Le run de référence archivé ici est le run 3 (13/25/13).

## Pic système (frame 2900)
Une frame à 10 773 ms avec chute mémoire de 544 Mo en pleine partie 2 : événement
système Quest (pas du code jeu — les runs 1 et 2 ne l'ont pas). Exclu des moyennes
« hors pic » du rapport ; documenté, pas masqué.

## Fichiers
- bench-summary.json / frames.csv / summary.csv : run 3 (référence).
- vrapi.log : log VrApi complet des runs avant (FPS, App ms, CPU&GPU ms).
