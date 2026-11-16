# Fruit Ninja VR – Part 1 – Task 1 : état initial du projet S7

Projet : Junghoon KIM (groupe d'une personne). Dépôt S7 repris : `LaelKim/Fruit_Ninja`, branche `main`,
commit `2db0f77` (26/10/2025). Copie du projet initial : **tag git `s7-initial`** sur ce commit.
Travail de la Part 1 : branche `part1`. Dépôt de remise : https://github.com/Ethervel/FruitNinjaVR
(branche `main` = version S7 d'origine, tag `s7-initial` ; branche `part1`, tag `part1-remise`).

Méthode : ouverture du projet avec sa version d'Unity, lecture du code et des scènes, audit automatique
(`Tools > Fruit Ninja > Audit du projet`, script `Assets/Editor/FruitNinjaTools/ProjectAudit.cs`,
rapport dans `Logs/FruitNinjaAudit.txt`). **Aucun test au casque n'a encore été fait** : tout ce qui
dépend du tracking ou des sabres en main est marqué « à tester » (check-list en fin de document).

## 1. Version, rendu, paquets

| Élément | Valeur relevée |
|---|---|
| Éditeur | Unity **6000.0.56f1** (changeset 9c7b5e468860), installé pour l'occasion avec Android Build Support (SDK, NDK, OpenJDK) et Windows Build Support (IL2CPP) |
| Pipeline de rendu | **URP 17.0.4** ; 2 niveaux de qualité : `Mobile` → `Mobile_RPAsset`, `PC` → `PC_RPAsset` (par défaut) |
| XR Plugin Management | 4.5.2 |
| OpenXR | 1.15.1 |
| XR Interaction Toolkit | 3.0.8 (+ sample *Starter Assets* importé) |
| Input System | 1.14.2 ; *Active Input Handling* = **Both** (ancien et nouveau) |
| XR Legacy Input Helpers | tiré en dépendance ; utilisé par le rig (CameraOffset, TrackedPoseDriver « legacy ») |
| Autres | TextMesh Pro (uGUI 2.0.0), Timeline 1.8.9, AI Navigation 2.0.8, Visual Scripting 1.9.7 |
| Ajout temporaire | `com.coplaydev.unity-mcp` v10.3.0 (outil de pilotage de l'éditeur, commit séparé, **à retirer avant la remise**) |

## 2. Configuration XR

| Point | PC (Standalone) | Android (Quest) |
|---|---|---|
| XR Plug-in Management | Loader **OpenXR**, *Initialize XR on Startup* | **Aucun loader configuré** |
| Profils d'interaction OpenXR | **Oculus Touch Controller Profile** | aucun |
| Features OpenXR | aucune autre | **Meta Quest Support désactivé** |
| Rendu OpenXR | Single Pass Instanced, Depth submission off | idem (non utilisé) |

Player Settings Android déjà présents : IL2CPP, ARM64, Min API 23, Target API « automatique », identifiant
d'application du template URP (`com.UnityTechnologies.com.unity.template.urpblank`).

**Dépendances propres à une plateforme**
- Le projet n'a été configuré que pour le PC : le build Quest exige d'activer OpenXR + Meta Quest Support
  + un profil de contrôleur Touch sur Android (Task 4).
- Le rig n'utilise pas le composant **XR Origin** : `XRRig` = `CameraOffset` (Legacy Input Helpers,
  hauteur 1,36 m en mode *Device*, mode de tracking « Default ») + `TrackedPoseDriver` legacy sur la caméra
  et les deux contrôleurs. Ces composants fonctionnent avec OpenXR mais sont dépréciés.
- Actions des contrôleurs : `InputActionManager` active `XRI Default Input Actions` ; seul le contrôleur
  **droit** porte un `XRRayInteractor` (UI Press = gâchette droite) pour les menus ; aucun interactor
  à gauche. Les sabres ne passent pas par des actions : `Katana_Left` / `Katana_Right` sont enfants des
  contrôleurs (Rigidbody cinématique + MeshCollider convexe en trigger + `KatanaSlicer`).
- UI : Canvas en World Space, `TrackedDeviceGraphicRaycaster`, `EventSystem` + `XRUIInputModule`.
- Code clavier/souris de débogage (ancien Input Manager, actif grâce au mode « Both ») : espace = toucher
  toutes les bombes (`BombController.Update`), B = toucher la bombe de test (`BombTester`), clic gauche =
  couper sous la souris (`Slicer.Update`). Sans effet sur Quest (pas de clavier).

## 3. Fonctions S7 : état observé et corrections

Légende : **OK (code)** = présent et cohérent à la lecture du code et de la scène, comportement VR à
confirmer au casque.

| Fonctionnalité | État observé | Correction apportée (Part 1) |
|---|---|---|
| Ouverture / compilation | 0 erreur, 0 warning de compilation sous 6000.0.56f1 | — |
| Suivi casque et contrôleurs | Rig legacy (CameraOffset + TrackedPoseDriver) ; à tester au casque | — |
| Deux sabres | OK (code) : un katana par main, coupe si vitesse > 1 m/s | Prefabs `Katana_Left/Right` créés (Task 2) |
| Lancement des fruits | OK (code) : `FruitSpawner`, 4 points de spawn, 8 fruits, 1 à 3 fruits par vague | Fichier `NewFruitSpawner.cs` renommé `FruitSpawner.cs` (nom = classe) |
| Coupe | OK (code) : découpe réelle du mesh (`Slicer`, faces de coupe `SliceCap`), son de coupe | — |
| Fruits bonus | **Absents en S7** (`FruitController.isSpecial/pointsValue` déclarés mais jamais utilisés) | **Ajout Part 1** : `Fruit_Bonus` (pomme dorée), 7 % des tirs, 50 points au lieu de 10 |
| Bombes | OK (code) : 10 % (Normal) ; chaque touche = −25 pts et −1 vie ; explosion (tremblement + flash `FX_BombFlashQuad`) à la 3e touche | Son de touche joué en double → corrigé |
| Score | OK (code) : 10 pts/fruit, combo +50 pts à 3 fruits en 2 s, meilleur score (PlayerPrefs) | — |
| Effets | Partiels : découpe du mesh, flash et tremblement de la bombe, sons. **Aucun Particle System** | Prévu Task 3 (jus + explosion, variantes PCVR/Quest) |
| Pénalités | Bombe : −25 pts, −1 vie, fin de partie à 0 vie ; fruit raté : combo remis à 0, pas de vie perdue | — |
| Difficultés | OK (code) : Facile 5 vies / Normal 3 / Difficile 2 / Extrême 1, cadence et % de bombes croissants | — |
| Minuteur | OK (code) : partie de 120 s, alerte à 10 s | — |
| Menu | OK (code) : accueil, choix de difficulté, HUD, fin de partie (rejouer / menu), rayon contrôleur droit | — |
| Réglage du volume | **Absent dans la version S7 remise** : `AudioManager.SetMasterVolume/SFX/Music` existe mais aucun slider ni menu | Non implémenté (décision : à noter, pas à créer) |
| Mode entraînement | **Absent dans la version S7 remise** | Non implémenté |
| Classement | **Absent dans la version S7 remise** : seul un meilleur score unique (PlayerPrefs) ; pas de saisie de nom, pas de clavier virtuel | Non implémenté |
| Sauvegarde JSON | **Absente dans la version S7 remise** (aucun `JsonUtility`, aucun fichier) | Non implémentée |
| Sons d'UI, début/fin de partie, musique | Clips assignés mais méthodes jamais appelées ; `katanaWhoosh`, `backgroundMusic`, `scoreBonus`, `fruitFall` non assignés | — (le fruit bonus appelle `PlayScoreBonus`, muet tant qu'aucun clip n'est assigné) |
| Scène de test `SlicerTestScene` | 1 script manquant sur `SlicingManager` ; hors build | — (déplacée dans `Scenes/Tests`) |

Le README S7 annonçait aussi « volume » et « saisie du nom via clavier virtuel » : ces fonctions ne sont
pas dans le code remis. Aucune autre copie du projet S7 n'existe (vérifié par le responsable du hub).

## 4. Erreurs et défauts restants, effet sur les tests

| Problème | Effet |
|---|---|
| Android non configuré pour XR (pas de loader OpenXR, pas de Meta Quest Support) | Un APK actuel démarrerait sans VR : **bloquant pour la Task 4 Quest**, à corriger en phase 2 |
| Volume, entraînement, classement, JSON absents | Non testables ; à signaler comme tels dans le rapport |
| Aucun Particle System | Effets jus/explosion à créer (Task 3) |
| `Bomb_Test` et `Debug_BombTester` dans la scène (objets de test S7) | Une bombe posée dans la scène au lancement ; pas de pénalité hors partie, détruite à la fin de la 1re partie |
| `FruitController.OnBecameInvisible` | Un fruit vu puis sorti du champ de vision est détruit 1 s après (combo remis à 0) : comportement à observer au casque |
| Logs `Debug.Log` à chaque collision des sabres | Coût CPU, surtout sur Quest ; à surveiller lors de la mesure de fluidité |
| `Fruits.blend` (source) non importable sans Blender associé aux .blend | Warning à l'import, sans effet (les prefabs utilisent `Fruits.fbx`) |
| `Fruits.dae` importé avec le shader Standard (rose sous URP) | Fichier source non utilisé, sans effet sur le jeu |

## 5. Check-list de test au casque (à faire par l'utilisateur)

Non testé à ce jour. Le **XR Device Simulator** n'est pas importé dans le projet (sample de XRI non
installé) : aucune simulation n'a été faite.

1. Casque connecté (Quest Link / Air Link, runtime OpenXR = Meta), scène `FruitNinjaVR_Main`, Play.
2. Tracking : la vue suit la tête ; hauteur des yeux correcte (sol à la bonne hauteur).
3. Sabres : un katana dans chaque main, suit le contrôleur sans retard ni décalage d'orientation.
4. Menu : rayon du contrôleur droit visible ; gâchette droite → Start, choix de difficulté, retour.
5. Fruits : lancés depuis les 4 points, trajectoires atteignables.
6. Coupe : un geste rapide coupe le fruit en deux moitiés, son de coupe, +10 points.
7. Combo : 3 fruits en moins de 2 s → +50 points, texte « Combo ».
8. Fruit bonus : pomme dorée (rare, ~1 fruit sur 14) → +50 points.
9. Bombe : une touche → son métallique, −25 points, −1 vie ; 3e touche → tremblement + flash blanc + son
   d'explosion ; à 0 vie → écran de fin de partie.
10. Fin de partie : score final, meilleur score, fruits coupés, bombes touchées ; « Rejouer » et « Menu ».
11. Difficultés : Facile (5 vies) et Extrême (1 vie) changent bien vies, cadence et bombes.
12. Fluidité : pas de saccade lors des coupes multiples.
