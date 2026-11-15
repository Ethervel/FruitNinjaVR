# Fruit Ninja VR – Part 1 – Task 2 : organisation du projet

Tous les déplacements et renommages ont été faits **par l'API de l'éditeur Unity**
(`AssetDatabase.MoveAsset`, `PrefabUtility.SaveAsPrefabAssetAndConnect`), jamais dans l'explorateur :
les GUID et les `.meta` sont conservés, donc les références des scènes, prefabs et matériaux restent
valides. Scripts reproductibles : `Assets/Editor/FruitNinjaTools/ProjectReorganizer.cs`
(menus `Tools > Fruit Ninja > ...`). Commits : `9d2611e` (assets) et `02788aa` (sabres, noms de scène).

## Arborescence

Avant (racine d'Assets en vrac) :

```
Assets/
  AudioManager.cs  BombController.cs  BombPrefab.prefab  BombTester.cs  destroyFLoor.cs
  DifficultySettings.cs  FruitController.cs  FruitTag.cs  GameManager.cs  KatanaSlicer.cs
  NewFruitSpawner.cs  Slicer.cs  UIManager.cs  InputSystem_Actions.inputactions  Readme.asset
  Jenpeuplu.meta (dossier vide)
  Material/ (matériaux + textures mélangés)   modeles/ (modèles, prefabs de fruits, textures)
  Scenes/ (SampleScene, SlicerTestScene, Katana.fbx)   sound/   Test/
  Samples/  Settings/  TextMesh Pro/  TutorialInfo/  XR/  XRI/
```

Après :

```
Assets/
  Audio/        SFX/ (katana, fruit, bombe, début/fin de partie)  UI/ (boutons)
  Editor/       FruitNinjaTools/ (audit, réorganisation, création du fruit bonus)
  FX/           Materials/ (FX_BombFlash) — les Particle Systems de la Task 3 iront ici
  Materials/    Bomb/  Environment/  Fruits/  Katana/  Slicing/  _Unused/
  Models/       Bomb/  Environment/  Fruits/Source/  Katana/  _Unused/
  Prefabs/      Bombs/  Fruits/  Katanas/  _Unused/
  Scenes/       FruitNinjaVR_Main.unity   Tests/SlicerTestScene.unity
  Scripts/      Audio/  Core/  Debug/  Gameplay/  Slicing/  UI/
  Textures/     Bomb/  Environment/  Fruits/  Skybox/
  Settings/     (URP, volumes)  Input/InputSystem_Actions.inputactions
  Samples/  TextMesh Pro/  TutorialInfo/ (+ Readme.asset)  XR/  XRI/   ← gérés par Unity/paquets, laissés en place
```

Plus aucun fichier à la racine d'Assets. Les dossiers `_Unused` regroupent les assets hérités
qu'aucune scène n'utilise (conservés : aucune suppression de contenu).

## Détail avant → après

| Avant | Après |
|---|---|
| `Scenes/SampleScene.unity` | `Scenes/FruitNinjaVR_Main.unity` |
| `Scenes/SlicerTestScene.unity` | `Scenes/Tests/SlicerTestScene.unity` |
| `GameManager.cs`, `DifficultySettings.cs` | `Scripts/Core/` |
| `UIManager.cs` | `Scripts/UI/` |
| `AudioManager.cs` | `Scripts/Audio/` |
| `NewFruitSpawner.cs` (classe `FruitSpawner`) | `Scripts/Gameplay/FruitSpawner.cs` |
| `destroyFLoor.cs` (classe `DestroyFloor`) | `Scripts/Gameplay/DestroyFloor.cs` |
| `FruitController.cs`, `FruitTag.cs`, `BombController.cs` | `Scripts/Gameplay/` |
| `Slicer.cs`, `KatanaSlicer.cs` | `Scripts/Slicing/` |
| `BombTester.cs`, `Test/SliceableFruit.cs` | `Scripts/Debug/` |
| `modeles/fruits/Banana 1.prefab` | `Prefabs/Fruits/Fruit_Banana.prefab` |
| `modeles/fruits/{Coconut,GreenApple,Orange,Pear,RedApple,Tomato,Watermelon}.prefab` | `Prefabs/Fruits/Fruit_<Nom>.prefab` |
| — (nouveau, Part 1) | `Prefabs/Fruits/Fruit_Bonus.prefab` (variante de Fruit_RedApple) |
| `BombPrefab.prefab` | `Prefabs/Bombs/Bomb.prefab` |
| instances de `Katana.fbx` modifiées dans la scène | `Prefabs/Katanas/Katana_Left.prefab`, `Katana_Right.prefab` |
| `modeles/fruits/Banana.prefab`, `Fruits (1).prefab` (inutilisés) | `Prefabs/_Unused/Fruit_Banana_Old.prefab`, `Fruits_AllInOne.prefab` |
| `modeles/fruits/source/Fruits/` (fbx utilisé + blend/dae/glb/obj/stl + tex) | `Models/Fruits/Source/` |
| `modeles/Bomb.fbx`, `Scenes/Katana.fbx`, `modeles/dojo mockup.fbx` | `Models/Bomb/`, `Models/Katana/`, `Models/Environment/` (noms de FBX conservés) |
| `modeles/fruits/Fruits.fbx`, `beam.fbx`, `Untitled.fbx` (inutilisés) | `Models/_Unused/` |
| `modeles/fruits/textures/` | `Textures/Fruits/` |
| `Material/bomb/*.png/.jpeg`, `.exr` du ciel, `base_texture_01.png` | `Textures/Bomb/`, `Textures/Skybox/`, `Textures/Environment/` |
| `Material/bomb/Materials/bomb_basecol.mat` (+ roughness, ao) | `Materials/Bomb/Bomb_BaseColor.mat` (+ `Bomb_Roughness`, `Bomb_GroundAO`) |
| `Material/BeamMaterial.mat` | `Materials/Katana/Katana_Beam.mat` |
| `Test/CapMaterial.mat` | `Materials/Slicing/SliceCap.mat` |
| `Test/New Material.mat` (inutilisé) | `Materials/_Unused/NewMaterial.mat` |
| `Material/Materials/citrus_orchard_road_puresky_4k.mat` | `Materials/Environment/Skybox_CitrusOrchard.mat` |
| `modeles/Materials/base_texture_01.mat` | `Materials/Environment/Environment_Base.mat` |
| `Material/EclipseMaterial.mat` (flash de la bombe) | `FX/Materials/FX_BombFlash.mat` |
| `sound/sword-slice-393847.mp3` | `Audio/SFX/SFX_Katana_Swipe.mp3` |
| `sound/steel-blade-slice-2-188214.mp3` | `Audio/SFX/SFX_Fruit_Slice.mp3` |
| `sound/metal-hit-95-200424.mp3` | `Audio/SFX/SFX_Bomb_Touch.mp3` |
| `sound/explosion-fx-343683.mp3` | `Audio/SFX/SFX_Bomb_Explosion.mp3` |
| `sound/chinese-ident-transition-2/3-*.mp3` | `Audio/SFX/SFX_Game_Start.mp3`, `SFX_Game_Over.mp3` |
| `sound/button-press-*.mp3`, `hover-button-*.mp3` | `Audio/UI/SFX_UI_ButtonPress.mp3`, `SFX_UI_ButtonHover.mp3` |
| `InputSystem_Actions.inputactions` | `Settings/Input/` |
| `Readme.asset` | `TutorialInfo/Readme.asset` |
| `Jenpeuplu/` (dossier vide, seul son .meta était versionné) | supprimé |

Les noms des FBX n'ont pas été changés : les identifiants internes de leurs sous-objets, utilisés par les
instances de la scène, dépendent des noms d'objets du modèle.

## Objets de la scène renommés

| Avant | Après | Rôle |
|---|---|---|
| `XRRig/Left Controller/Katana` | `Katana_Left` | sabre gauche (prefab) |
| `XRRig/Right Controller/Katana` | `Katana_Right` | sabre droit (prefab) |
| `BombPrefab` | `Bomb_Test` | bombe de test S7 posée dans la scène |
| `touchtester` | `Debug_BombTester` | test clavier de la bombe |
| `Dojo/dojo mockup`, `dojo mockup (1)` | `Dojo/Dojo_A`, `Dojo_B` | décor |
| `EclipseQuad` | `FX_BombFlashQuad` | flash de l'explosion |

## Prefabs du gameplay

| Rôle | Prefab | Utilisé par |
|---|---|---|
| Fruits | `Prefabs/Fruits/Fruit_{Banana,Coconut,GreenApple,Orange,Pear,RedApple,Tomato,Watermelon}` | `FruitSpawner.fruitPrefabs[0..7]` |
| Fruit bonus | `Prefabs/Fruits/Fruit_Bonus` | `FruitSpawner.bonusFruitPrefab` |
| Bombe | `Prefabs/Bombs/Bomb` | `FruitSpawner.fruitPrefabs[8]` (`bombPrefabIndex = 8`) |
| Sabres | `Prefabs/Katanas/Katana_Left`, `Katana_Right` | enfants des contrôleurs du `XRRig` |
| Effets | aucun prefab en S7 (flash = quad de scène + `FX_BombFlash`, créé à la volée sur les clones) | Task 3 : `FX/` |

## Vérifications après réorganisation

Audit (`Tools > Fruit Ninja > Audit du projet`) relancé après chaque étape, sous 6000.0.56f1 :

| Contrôle | Résultat |
|---|---|
| Erreurs de compilation | 0 |
| Références cassées (scènes + prefabs) | 0 |
| Prefabs sources manquants | 0 |
| Scripts manquants (« Missing Script ») | 0 dans `FruitNinjaVR_Main` et les prefabs ; 1 dans `Tests/SlicerTestScene` (hérité de S7, scène hors build) |
| Matériaux non URP | 1 : matériau embarqué de `Models/Fruits/Source/Fruits.dae` (fichier source inutilisé) |

## Séquence de démarrage

Build Profiles / Scene List : une seule scène, **`Assets/Scenes/FruitNinjaVR_Main.unity` (index 0)**.
Tout le jeu (menus, partie, fin de partie) se déroule dans cette scène ; `Tests/SlicerTestScene` est une
scène de test, volontairement hors build.

Au lancement : `GameManager.Start` initialise l'`UIManager` → panneau d'accueil → choix de la difficulté
→ `GameManager.StartGame` (minuteur 120 s, `FruitSpawner.StartSpawnSystem`) → fin de partie (0 vie ou
fin du temps) → Rejouer ou Menu.
