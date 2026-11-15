# Fruit Ninja VR – Part 1 – Check-list de test au casque (Task 4)

Casque : Meta Quest 3. **Rien de ceci n'a encore été testé** : à faire par l'utilisateur, puis à reporter
dans le rapport (casque, PC, paramètres graphiques, problèmes rencontrés).

PC de test : Windows 11, i9-14900KF, RTX 3080, 32 Go RAM. Builds : `Builds/Windows/` et
`Builds/Quest/FruitNinjaVR_Quest.apk` (Unity 6000.0.56f1).

## A. Build Windows PCVR (casque relié au PC)

1. Installer et lancer **Meta Quest Link** sur le PC ; dans *Paramètres > Général*, définir Meta Quest Link
   comme **runtime OpenXR actif**.
2. Relier le Quest 3 (câble Link ou Air Link) et entrer dans Link depuis le casque.
3. Lancer `Builds/Windows/FruitNinjaVR.exe` (ou le dossier extrait de `FruitNinjaVR_Windows.zip`).
4. Faire le parcours de test (section C), colonne **PCVR**.
5. Vérifier la variante d'effets dans
   `%USERPROFILE%\AppData\LocalLow\JunghoonKIM\FruitNinjaVR\Player.log` : ligne
   `[GameFX] variante utilisée : PCVR`.
6. FPS : compteur de performances de Meta Quest Link (*Performance Overlay* via l'Oculus Debug Tool) ;
   noter l'outil, la scène et la difficulté.

## B. Build Quest autonome (APK)

1. Casque en **mode développeur** (application Meta Horizon sur le téléphone > appareil > Mode développeur).
2. Relier le casque en USB-C au PC, accepter « Autoriser le débogage USB » dans le casque.
3. Vérifier : `adb devices` → le casque apparaît avec l'état `device`.
4. Installer : `adb install -r Builds/Quest/FruitNinjaVR_Quest.apk`.
5. Débrancher le câble (test **autonome**, pas via Link). Lancer *FruitNinjaVR* depuis
   *Bibliothèque > Sources inconnues*.
6. Variante d'effets (câble rebranché) : `adb logcat -s Unity | findstr GameFX` → `variante utilisée : Quest`.
7. Captures : `adb exec-out screencap -p > docs/captures/quest_<nom>.png`.
8. FPS : **OVR Metrics Tool** (installé depuis le Store ou par adb), overlay activé ; relever FPS moyen
   et minimum, utilisation CPU/GPU, pendant une partie en difficulté Normal puis Extrême.

## C. Parcours de test (identique sur les deux builds)

Cocher OK / KO et noter les remarques.

| # | Élément | Vérification | PCVR | Quest |
|---|---|---|---|---|
| 1 | Chargement | l'application démarre sans écran noir ni crash ; temps de chargement | | |
| 2 | Tracking casque | la vue suit la tête ; hauteur des yeux correcte, sol à la bonne hauteur | | |
| 3 | Tracking manettes | les deux manettes suivies, sans retard ni décalage d'orientation | | |
| 4 | Sabres | un katana dans chaque main, bien orienté | | |
| 5 | Menu | rayon visible sur la manette droite ; la **gâchette droite** valide Start, la difficulté, Retour | | |
| 6 | Lancement des fruits | fruits lancés depuis les 4 points, atteignables | | |
| 7 | Coupe | un geste rapide coupe le fruit en deux, son de coupe, +10 points | | |
| 8 | Effet jus | éclaboussure au point de coupe, couleur du fruit | | |
| 9 | Combo | 3 fruits en moins de 2 s → +50, texte « Combo » | | |
| 10 | Fruit bonus | pomme dorée (rare) → +50 points | | |
| 11 | Bombe touchée | son, −25 points, −1 vie, petite explosion de particules | | |
| 12 | Explosion | 3e touche : tremblement, grande explosion de particules, flash, son | | |
| 13 | Fin de partie | à 0 vie ou à 120 s : score, meilleur score, fruits, bombes ; Rejouer et Menu | | |
| 14 | Difficultés | Facile (5 vies) et Extrême (1 vie) changent vies, cadence et bombes | | |
| 15 | Lisibilité | textes de l'UI lisibles, effets visibles sans gêner | | |
| 16 | Fluidité | pas de saccade lors des coupes multiples et des explosions | | |
| 17 | FPS | moyenne / minimum, outil et conditions | | |
| 18 | Stabilité | 2 parties complètes sans crash ni blocage | | |

Fonctions S7 absentes, donc non testables : réglage du volume, mode entraînement, classement, sauvegarde
JSON (voir `docs/etat-initial.md`).

## D. À relever pour le rapport

- Casque et version du système du Quest ; version de Meta Quest Link.
- Paramètres graphiques : voir `docs/builds.md`.
- Problèmes rencontrés et contournements ; tout test non réalisé, signalé comme tel.
