# Fruit Ninja VR – Part 1 (ST2VAAS 2026-2027)

Auteur : **Junghoon KIM** (groupe d'une personne). Projet repris du mini-projet S7 (ST2OOS 2025-2026).

## Ouvrir le projet

- Version d'Unity : **6000.0.56f1** (Unity 6, changeset 9c7b5e468860). Ouvrir avec cette version exacte.
- Modules utiles : Windows Build Support (IL2CPP) pour le PCVR ; Android Build Support (SDK, NDK, OpenJDK)
  pour le Meta Quest.
- Dossiers nécessaires : `Assets` (avec les `.meta`), `Packages`, `ProjectSettings`.
- Rendu : URP 17.0.4. XR : OpenXR 1.15.1, XR Interaction Toolkit 3.0.8, XR Plugin Management 4.5.2.

## Scène de démarrage

`Assets/Scenes/FruitNinjaVR_Main.unity` : seule scène du build (index 0). Menus, partie et fin de partie
s'y déroulent. `Assets/Scenes/Tests/SlicerTestScene.unity` est une scène de test hors build.

## Lancer le jeu

**Dans l'éditeur (PCVR)**
1. Casque Meta Quest relié au PC (Quest Link ou Air Link) ; Meta Quest Link défini comme runtime OpenXR.
2. Ouvrir `FruitNinjaVR_Main`, cible de build *Windows*, puis Play.

**Build Windows** : lancer l'exécutable du dossier `Builds/Windows/` casque connecté.

**Build Quest** : `adb install -r <fichier>.apk`, puis lancer l'application depuis la bibliothèque du casque
(*Sources inconnues*). Configuration Android à finaliser en Task 4.

## Contrôles

| Action | Contrôle |
|---|---|
| Viser un bouton de menu | rayon du contrôleur **droit** |
| Valider | **gâchette** du contrôleur droit |
| Couper un fruit | mouvement rapide d'un des deux katanas (un dans chaque main) à travers le fruit |
| Bombe | à éviter : chaque touche retire 25 points et une vie ; elle explose à la 3e touche |

## Règles

- Partie de 120 s ; fin anticipée quand il n'y a plus de vie.
- Fruit coupé : +10 points ; 3 fruits en moins de 2 s : combo +50 points.
- Fruit bonus (pomme dorée, rare) : +50 points (ajout de la Part 1).
- Difficultés : Facile (5 vies), Normal (3), Difficile (2), Extrême (1) ; la cadence et la part de bombes
  augmentent avec le niveau.
- Le meilleur score est conservé entre les sessions (PlayerPrefs).

## Fonctions S7 absentes

Réglage du volume, mode entraînement, classement et sauvegarde JSON ne figurent pas dans la version S7
remise (voir `docs/etat-initial.md`).

## Documentation

- `docs/etat-initial.md` : état initial S7, corrections, check-list de test au casque.
- `docs/organisation.md` : organisation du projet (avant → après), prefabs, séquence de démarrage.
