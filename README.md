# Fruit Ninja VR – Part 1 (ST2VAAS 2026-2027)

Membre : **Junghoon KIM** (groupe d'une personne). Projet repris du mini-projet S7 (ST2OOS 2025-2026) :
mini-jeu VR inspiré de Fruit Ninja, où le joueur tranche des fruits avec deux katanas.

Dépôt : https://github.com/Ethervel/FruitNinjaVR. Remise : branche `part1`, tag `part1-remise`.
Rapport : `docs/rapport/rapport.pdf`. La version S7 d'origine, reprise du dépôt `LaelKim/Fruit_Ninja`, se
trouve sur la branche `main` (tag `s7-initial`).

## Ouvrir le projet

- Version d'Unity : **6000.0.56f1** (Unity 6, changeset 9c7b5e468860). Ouvrir avec cette version exacte.
- Modules utiles : Windows Build Support (IL2CPP) pour le PCVR ; Android Build Support (SDK, NDK, OpenJDK)
  pour le Meta Quest.
- Dossiers versionnés : `Assets` (avec les `.meta`), `Packages`, `ProjectSettings`. `Library` et `Builds`
  ne sont pas versionnés (Unity régénère `Library` à l'ouverture).
- Rendu : URP 17.0.4. XR : OpenXR 1.15.1, XR Interaction Toolkit 3.0.8, XR Plugin Management 4.5.2.
- Entrées : Input System 1.14.2 (Active Input Handling = Input System Package).

## Scène de démarrage

`Assets/Scenes/FruitNinjaVR_Main.unity` : seule scène du build (index 0). Menus, partie et fin de partie
s'y déroulent. `Assets/Scenes/Tests/SlicerTestScene.unity` est une scène de test hors build.

## Lancer le jeu

**Dans l'éditeur (PCVR)**
1. Casque Meta Quest relié au PC (Quest Link ou Air Link) ; Meta Horizon Link défini comme runtime OpenXR.
2. Ouvrir `FruitNinjaVR_Main`, cible de build *Windows*, puis Play.

**Build Windows PCVR** : décompresser `FruitNinjaVR_Part1_Windows_PCVR.zip`, puis lancer
`FruitNinjaVR_Windows/FruitNinjaVR.exe`, casque relié par Link avec Meta Horizon Link comme runtime OpenXR.

**Build Meta Quest 3** : `adb install -r FruitNinjaVR_Part1_Quest.apk`, puis lancer *FruitNinjaVR* depuis
*Bibliothèque > Sources inconnues*, manettes allumées, câble débranché (mode autonome).

**Refaire les builds** : menus `Tools > Fruit Ninja > Build Windows PCVR` et `Build Quest (APK)`
(sorties `Builds/Windows/` et `Builds/Quest/`). Paramètres détaillés : `docs/builds.md`. Le PATH Windows
doit contenir `C:\Windows\System32`, sinon le build Android échoue (« Failed to update Android SDK »).

## Contrôles

| Action | Contrôle |
|---|---|
| Viser un bouton de menu | rayon du contrôleur **droit** |
| Valider | **gâchette** du contrôleur droit |
| Couper un fruit | mouvement rapide d'un des deux katanas (un dans chaque main) à travers le fruit |
| Bombe | à éviter : chaque touche retire 25 points et une vie ; elle explose à la 3e touche |

Attention : le bouton Quitter du menu réagit aussi au contact des katanas.

## Règles

- Partie de 120 s ; fin anticipée quand il n'y a plus de vie.
- Fruit coupé : +10 points ; 3 fruits en moins de 2 s : combo +50 points.
- Fruit bonus (pomme dorée, rare) : +50 points (ajout de la Part 1).
- Effets de particules (Part 1) : jus au point de coupe, explosion à chaque touche de bombe ; variante PCVR
  ou Quest choisie automatiquement selon la plateforme.
- Difficultés : Facile (5 vies), Normal (3), Difficile (2), Extrême (1) ; la cadence et la part de bombes
  augmentent avec le niveau.
- Le meilleur score est conservé entre les sessions (PlayerPrefs).

## Tests (16/11/2026)

Meta Quest 3 (Horizon OS v205) en autonome et PC (RTX 3080) via Meta Horizon Link : parcours de test OK
selon le joueur. Les deux builds tiennent 72 Hz en jeu. La grande explosion à 3 touches n'est pas confirmée
par les journaux. Détails : `docs/checklist-casque.md` et la section Tests du rapport.

## Fonctions S7 absentes

Réglage du volume, mode entraînement, classement et sauvegarde JSON ne figurent pas dans la version S7
remise (voir `docs/etat-initial.md`).

## Documentation

- `docs/rapport/rapport.pdf` (source `rapport.tex`) : rapport de la Part 1.
- `docs/etat-initial.md` : état initial S7 et corrections.
- `docs/organisation.md` : organisation du projet (avant → après), prefabs, séquence de démarrage.
- `docs/particules.md` : effets PCVR / Quest, paramètres et captures.
- `docs/builds.md` : configuration des builds Windows et Quest, casque de test, sources des valeurs Android.
- `docs/analyse-apk.md` : contenu et taille de l'APK, comparaison avec le build Windows.
- `docs/checklist-casque.md` : procédure et grille de test au casque, résultats.
- `docs/tests/` : journaux des tests (logcat du Quest, `Player.log` du PC).
- `docs/captures/` : captures de l'éditeur, de l'APK Analyzer et dans le casque (`quest/`, `pcvr/`).

## Modifications de la Part 1 par rapport à S7

Organisation des assets, prefabs des sabres, fruit bonus, Particle Systems PCVR/Quest, configuration
OpenXR Android (Meta Quest), Input System uniquement, correction d'un son joué en double et de l'erreur
`Tag: Slice is not defined` (`DestroyFloor`). L'outil de développement `com.coplaydev.unity-mcp`, utilisé
pendant le travail, a été retiré avant la remise.

## Part 2 : gestion des ressources et optimisation mémoire (branche `part2`, tag `part2-remise`)

Changements par rapport à la Part 1 :
- **Pools** (Tasks 1-2) : un pool par prefab (fruits, bonus, bombe, jus, explosions, morceaux de
  fruits), réinitialisation complète à la réactivation, plus aucun `Instantiate`/`Destroy` en jeu
  (74 créations = stocks initiaux, 322-423 réutilisations selon la plateforme). Meshes des morceaux
  réécrits au lieu d'être réalloués. Scripts : `Assets/Scripts/Core/` (GameObjectPool, PoolManager,
  PooledObject, PooledLifetime, PoolRegistry).
- **Addressables 2.10.3** (Tasks 3-4) : groupes `FX_PCVR` / `FX_Quest`, mêmes adresses
  (`FX_FruitJuice`, `FX_BombExplosion`, `Katana`), handles gardés jusqu'à la destruction des clones,
  repli sur références directes en cas d'échec (`AssetLoader`). Katana en deux variantes :
  PCVR 1275 sommets, Quest 132 sommets / 1 matériau (`KatanaVariantApplier`).
- **Bench déterministe** (`Assets/Scripts/Debug/BenchMode.cs`, seed 12345) : mesures CPU/GC/mémoire
  par frame, activable par `-bench` (PC) ou extra d'intent `-e bench 1` (Quest, activité
  `UnityPlayerGameActivity`).

Résultats (détail et réserves dans le rapport) : PC, GC 242,6 → 224,9 Mo, 424 → 74 Instantiate,
score 895 → 815 assumé (bombes explosées ne restent plus figées). Quest (72 Hz, casque porté),
GC −25 % par coupe, 0 Destroy en jeu, mémoire stable après pré-allocation des stocks.

Chemins :
- Rapport : `docs/part2/rapport/rapport.pdf` (source `rapport.tex`, 8 pages).
- Mesures PC : `docs/part2/mesures-avant/`, `docs/part2/mesures-apres/` (comparatif-pc.csv).
- Mesures Quest : `docs/part2/mesures-avant-quest/`, `docs/part2/mesures-apres-quest/`
  (comparatif-quest.csv, logs VrApi et variantes).
- Graphiques Profiler (mesures ProfilerRecorder, pas des captures d'écran) :
  `docs/part2/captures-profiler/`.
- Builds de référence : `Builds/Bench/avant/` et `Builds/Bench/apres/` (APK Quest dev + Windows dev).
- Dossier de remise : `Builds/Remise-Part2/` (rapport.pdf, APK Quest après corrigé, ZIP Windows après).
- Tag de remise : [`part2-remise`](https://github.com/Ethervel/FruitNinjaVR/releases/tag/part2-remise).
