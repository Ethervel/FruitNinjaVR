# Fruit Ninja VR – Part 1 – Task 4 : configuration des builds

Builds générés par `Assets/Editor/FruitNinjaTools/BuildTools.cs` (menus `Tools > Fruit Ninja > Build ...`),
configuration Quest appliquée par `QuestSetup.cs`. Rapports bruts : `Logs/build-StandaloneWindows64.txt`,
`Logs/build-Android.txt`, `Logs/QuestSetup.txt` (dossier `Logs/` non versionné).

| | Windows PCVR | Meta Quest 3 (autonome) |
|---|---|---|
| Résultat | Succeeded, 0 erreur, 0 warning (45 s) | Succeeded, 0 erreur (1 min 12 s ; 1er build IL2CPP plus long) |
| Sortie | `Builds/Windows/FruitNinjaVR.exe` (+ `FruitNinjaVR_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`, `D3D12`, `UnityCrashHandler64.exe`) | `Builds/Quest/FruitNinjaVR_Quest.apk` |
| Éditeur | Unity 6000.0.56f1 | Unity 6000.0.56f1 |
| Scènes (ordre) | [0] `Assets/Scenes/FruitNinjaVR_Main.unity` | [0] `Assets/Scenes/FruitNinjaVR_Main.unity` |
| Scripting backend | Mono | **IL2CPP** |
| Architecture | x86_64 | **ARM64** uniquement |
| API graphique | Direct3D 11 (auto) | **Vulkan** uniquement |
| Espace couleur | Linear | Linear |
| Compression des textures | (défaut PC : DXT/BC) | **ASTC** (Player Settings ; le sous-réglage du build reste « Use Player Settings ») |
| API Android | — | **min 29 (Android 10), cible « Automatic (highest installed) » = 36** |
| Identifiant | — | `com.JunghoonKIM.FruitNinjaVR`, version 0.1.0, installation Auto |
| Rendu multithread | — | activé |
| Niveau de qualité | `PC` → `PC_RPAsset` (HDR, SSAO, ombres douces 4 cascades, post-process, texture de profondeur) | `Mobile` → `Mobile_RPAsset` : **MSAA 4x, HDR off, render scale 1, Forward, post-process off**, 1 cascade, pas d'ombres douces |
| XR Plug-in Management | OpenXR, init au démarrage | OpenXR, init au démarrage |
| Features OpenXR | profils Oculus Touch + Meta Quest Touch Plus | **Meta Quest Support** + profils Oculus Touch + Meta Quest Touch Plus |
| Rendu stéréo | Single Pass Instanced | Single Pass Instanced ; latence « Prioritize Input Polling » |
| Effets (Task 3) | `FX_*_PCVR` | `FX_*_Quest` (sélection `#if UNITY_ANDROID`) |
| Gestion des entrées | Input System uniquement | Input System uniquement |
| Runtime OpenXR requis | Meta Quest Link (PC) | runtime du casque |

## Valeurs Android : source et statut

Les paramètres « validés en cours » ne sont pas connus. Ils sont remplacés par les recommandations
officielles actuelles de Meta et d'Unity pour un Quest 3 avec Unity 6 et OpenXR. **Valeurs à confirmer par
l'utilisateur avec celles du cours.**

| Paramètre | Valeur | Source |
|---|---|---|
| Minimum API Level | Android 10 (API 29) | Meta : « select Android 10 (API level 29) or higher for Meta Quest headsets » ; Unity, profil Meta Quest : « Android 10 (API level 29) » |
| Target API Level | Automatic (highest installed) → **API 36** dans l'APK | Meta : « select Automatic (highest installed) ». Le profil Meta Quest d'Unity 6.1+ propose 32 (Android 12L) : premier essai avec 32, **échec Gradle** car la plateforme SDK 32 n'est pas installée (34, 35, 36 le sont) et le SDK d'Unity, sous `Program Files`, n'est pas inscriptible sans droits administrateur. Si le cours impose 32 : installer *Android SDK Platform 32* (sdkmanager en administrateur) et repasser la cible à 32 |
| Scripting backend / architecture | IL2CPP, ARM64 seul | Meta : « select IL2CPP », « Clear ARMv7 and select ARM64. The Meta Horizon Store accepts only 64-bit apps » |
| API graphique | Vulkan | Unity, profil Meta Quest : Vulkan ; Meta recommande Vulkan (certaines fonctions récentes du Quest n'existent qu'en Vulkan) |
| Rendu stéréo | Single Pass Instanced (multiview) | Unity, profil Meta Quest : « Instancing » |
| Compression des textures | ASTC | Meta, *Tech Note: Unity Settings for Mobile VR* |
| MSAA | 4x | Meta, *Configure Unity Settings* : Anti Aliasing « select 4x » (à régler à la main sous URP) |
| Installation | Automatic | Meta, *Prepare for publish* |

Sources :
- Meta, *Prepare your app for publishing* : https://developers.meta.com/horizon/documentation/unity/unity-prepare-for-publish/
- Meta, *Configure Unity settings* : https://developers.meta.com/horizon/documentation/unity/unity-conf-settings/
- Meta, *Tech Note: Unity Settings for Mobile VR* : https://developers.meta.com/horizon/blog/tech-note-unity-settings-for-mobile-vr/
- Unity 6.3, *Meta Quest build profile* : https://docs.unity.com/en-us/engine/6000.3/manual/xr/configuring-project-for/meta-quest-develop/build-profile
  (le profil « Meta Quest » existe à partir d'Unity 6.1 ; sous 6000.0 les mêmes valeurs sont réglées à la main
  sur la cible Android)

Consultées le 15/11/2026.

## Rig XR

Le rig S7 (`XRRig` : `CameraOffset` + `TrackedPoseDriver` de *XR Legacy Input Helpers*) est **conservé**.
Ces composants lisent les poses via le sous-système d'entrée XR, que le plug-in OpenXR fournit sur PC comme
sur Android. Aucune règle de Project Validation ne le signale, et le remplacer par un XR Origin aurait obligé
à refaire la hiérarchie (contrôleurs, sabres, rayon UI) sans nécessité. À confirmer au casque : hauteur des
yeux et suivi des manettes (`docs/checklist-casque.md`).

## Project Validation (OpenXR)

| Plateforme | Avant | Après |
|---|---|---|
| Android | 2 avertissements : SSAO coûteux ; latence « Prioritize Input Polling » recommandée | 1 avertissement : SSAO |
| Windows | 0 | 0 |

L'avertissement SSAO restant est un **faux positif** : le SSAO n'est que sur `PC_Renderer`, et le niveau de
qualité `PC` est exclu de la plateforme Android (Quality Settings). Le renderer du Quest (`Mobile_Renderer`)
n'a aucune *render feature*.

## Corrections apportées pour le déploiement

| Problème | Correction |
|---|---|
| Android sans XR (S7) | loader OpenXR + Meta Quest Support + profils de manettes (`QuestSetup`) |
| « Active Input Handling = Both » non supporté sur Android (avertissement du build) | passage à **Input System uniquement** ; les raccourcis de débogage S7 (espace = toucher les bombes, B = bombe de test, clic = couper) utilisent maintenant `Keyboard.current` / `Mouse.current` |
| Build Android : échec Gradle « Failed to install the following SDK components » (Android SDK Platform 32) | cible API passée à « Automatic (highest installed) » (36) |
| Build Android : « Failed to update Android SDK » (version des platform-tools lue 0.0) | cause : `C:\Windows\System32` absent du PATH du poste, donc `sdkmanager.bat` ne trouve pas `findstr`. Le build a été relancé avec System32 ajouté au PATH du seul processus Unity. **À corriger sur le poste** pour builder depuis l'éditeur (voir le rapport) |

## Casque de test (relevé par adb le 16/11/2026)

| Élément | Valeur | Source |
|---|---|---|
| Modèle | Meta Quest 3 (`eureka`) | `ro.product.model`, `adb devices -l` |
| Android | 14 (API 34), build `UP1A.231005.007.A1`, incrémentale `52270740038100520` | `ro.build.display.id`, `ro.build.version.*` |
| Correctif de sécurité | 2026-06-01 ; build système du 02/07/2026 | `ro.build.version.security_patch`, `ro.build.date` |
| Meta Horizon OS | v205 (Horizon UI `205.0.0.112.1674`), API OVR 160 | `ro.vros.build.version`, `com.oculus.systemux`, `ro.ovr.os.api.version` |
| Batterie au début du test | 97 %, en charge sur secteur (câble USB-C branché) | `dumpsys battery` |
| Panneau | 4128 × 2208 (deux yeux, soit 2064 × 2208 par œil) | `dumpsys display` |
| Fréquences d'affichage | 72 à 120 Hz ; 72 Hz actif au moment du relevé (90 Hz par défaut du système) | `dumpsys display` |
| Paquet installé | `com.JunghoonKIM.FruitNinjaVR` (`adb install -r`, « Success ») | `pm list packages` |

Au lancement par adb sans manette active, Horizon OS bloque l'application (dialogue
`app_launch_blocked_controller_required`) : il faut allumer les manettes avant.

## Tests

Procédure et grille : `docs/checklist-casque.md`. Log du test Quest : `docs/tests/quest-logcat.txt`.

- **Quest (16/11/2026)** : testé dans le casque, y compris en autonome sans câble. Grille OK ; partie
  Normal à 72,5 FPS en moyenne et 71 au minimum (écran à 72 Hz) ; variante d'effets Quest confirmée ;
  aucune exception ni crash.
- **PC (Meta Horizon Link, runtime 1.208.0, câble)** : testé, grille OK. Avec le HUD de performance de
  Link, l'appli reste à 72 Hz en jeu : 1 image perdue en 38 s en Normal, aucune en Extrême, marge de
  71 à 85 %. Un creux à 63 Hz au retour au menu. Variante d'effets PCVR confirmée dans `Player.log`.
- **Après les tests** : correction de `DestroyFloor` (erreur `Tag: Slice is not defined`), puis les deux
  builds ont été refaits. Le nouvel APK est installé ; son démarrage au casque n'a pas été revérifié
  après le correctif ; le build s'est terminé sans erreur.
