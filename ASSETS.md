# ASSETS — sources, auteurs et licences

Ce fichier recense chaque ressource du projet : son origine, sa licence, son usage dans le jeu
et toute intervention manuelle. Rien n'a été acheté, et aucun pack tiers de modèles, de sons ou
de textures n'est utilisé. Les ressources « créées pour le projet » sont produites par des scripts
versionnés dans `tools/` et peuvent être régénérées à partir de ces scripts.

## Bases tierces libres

| Ressource | Auteur / source | Licence | Usage dans le jeu | Remarques |
|---|---|---|---|---|
| Maillage humain de base MakeHuman, cibles de morphologie, masques UV de peau (`mpfb_lips.jpg`, `mpfb_eyelids.jpg`, `mpfb_ears.jpg`, `mpfb_fingernails.jpg`, `mpfb_toenails.jpg`) | MakeHuman Community, extension Blender MPFB2 : https://github.com/makehumancommunity/mpfb2 (commit `d0a32e5`, 2026-10-04) | **CC0 1.0** pour les ressources (`LICENSE.ASSETS.md` de MPFB2) | Corps des deux personnages (`PM_Player.fbx`, `PM_Opponent.fbx`) ; les masques servent seulement à composer `T_Player_Skin.png` et `T_Opponent_Skin.png` | Le code de MPFB2 (GPLv3) a servi d'outil dans Blender. Il n'est pas inclus dans le jeu. MakeHuman ne revendique aucun droit sur les modèles exportés (`LICENSE.md`, section D). |
| Squelette « mixamo_unity » (`data/rigs/standard/rig.mixamo_unity.json`) et sa pondération | MPFB2, même dépôt | CC0 1.0 | Squelette des personnages (os `mixamorig:*`), importé en Humanoid | Ce n'est pas un téléchargement Mixamo : seuls les noms d'os suivent la convention Mixamo. |
| Yeux « low-poly » (`data/eyes/low-poly/low-poly.mhclo`) | MakeHuman : https://github.com/makehumancommunity/makehuman (commit `a8bc2d5`) | CC0 1.0 (`LICENSE.ASSETS.md`) | Yeux des personnages | Matériau remplacé par une couleur unie. |
| Police **Inter** Regular et SemiBold, version 4.000 (`Art/Fonts/Inter-*.otf`) | The Inter Project Authors, https://github.com/rsms/inter ; fichiers du paquet Debian `fonts-inter` 4.0+ds-1 | **SIL OFL 1.1** (texte complet dans `Art/Fonts/OFL-Inter.txt`) | Textes de l'interface ; sous-titres des enseignes rendus dans les textures | Licence vérifiée dans le champ « license » du fichier de police et dans le fichier copyright Debian. |
| Police **Bebas Neue** Bold, version 1.300 (`Art/Fonts/BebasNeue-Bold.otf`) | Ryoichi Tsunekawa / Dharma Type, https://dharmatype.com/bebas-neue ; paquet Debian `fonts-bebas-neue` 3.0-2 | **SIL OFL 1.1** (texte complet dans `Art/Fonts/OFL-BebasNeue.txt`) | Titres des panneaux d'interface ; lettrage des enseignes rendu dans les textures | Licence lue dans le champ « license » du fichier de police. « Bebas Neue » est une marque de l'auteur : le fichier est utilisé sans modification. |
| Police **Yusei Magic** | The Yusei Magic Project Authors (Tanukizamurai), https://github.com/tanukifont/YuseiMagic ; paquet Debian `fonts-yusei-magic` | SIL OFL 1.1 | Lettrage du logo « POING MORT » rendu dans `Art/UI/T_Title_PoingMort.png` | La police n'est pas incluse dans le projet, seule l'image produite l'est. L'OFL n'impose pas de licence aux documents créés avec la police. |

## Créé pour le projet

| Ressource | Fichiers | Générateur | Licence | Intervention manuelle |
|---|---|---|---|---|
| Personnage joueur : hoodie sombre à capuche claire, pantalon ample, baskets, coiffure en twists | `Art/Models/Characters/PM_Player.fbx` (1,78 m), `PM_Player.materials.json`, `Art/Textures/T_Player_Skin.png` | `tools/blender/build_character.py`, puis `export_character.py` | Propriété du projet ; corps de base sous CC0, voir plus haut | Aucune. Avatar Humanoid configuré automatiquement à l'import, à vérifier dans Unity. |
| Adversaire « Le Mur » : débardeur, casquette, jean | `PM_Opponent.fbx` (1,83 m), `PM_Opponent.materials.json`, `T_Opponent_Skin.png` | idem | idem | idem |
| Vêtements, chaussures, cheveux et sourcils | inclus dans les FBX des personnages | dérivés du maillage du corps par `build_character.py` et `pm_cloth.py` | Propriété du projet | Aucune |
| 22 animations : repos, marche, course, sprint, garde et pas de garde, direct, croisé, crochet, esquive, parade, réactions aux coups, K.O., relevé, chute, montée, conduite, descente | incluses dans les deux FBX (`Armature|<clip>`, renommées à l'import) | `tools/blender/build_anims.py` et `pm_pose.py` (poses-clés procédurales, IK) | Propriété du projet | Aucune. Ce ne sont **pas** des animations Mixamo ni de la capture de mouvement. |
| Berline générique années 80-90, sans marque ni logo : portières avant ouvrantes, roues séparées, repères de siège et de portières | `Art/Models/Vehicles/PM_Car_Sedan.fbx` | `tools/blender/build_car.py` | Propriété du projet | Aucune. Cinq teintes appliquées par matériau dans Unity. |
| Bâtiments modulaires : épicerie, laverie, café, garage, deux immeubles d'habitation et un immeuble simple, plus trois tours d'horizon | `Art/Models/City/Bld_*.fbx`, `city_assets.json` | `tools/blender/build_city.py` | Propriété du projet | Aucune |
| Mobilier : lampadaire, projecteur, banc, poubelle, arbre, borne d'incendie, potelet, barrière, grille, caisse, benne, abribus, panneau « sens unique », mur | `Art/Models/City/Prop_*.fbx` | `tools/blender/build_city.py` | Propriété du projet | Aucune |
| Textures tuilables (brique, enduit, béton, asphalte, trottoir, bordure, gravier, rideau métallique, feuillage, écorce, avec leurs normal maps) ; enseignes ; intérieurs éclairés de boutiques et de fenêtres | `Art/Textures/T_*.png` | `tools/textures/generate_textures.py` (procédural, numpy et Pillow) | Propriété du projet | Aucune. Aucune photo ni image tierce. |
| Logo « POING MORT » | `Art/UI/T_Title_PoingMort.png` | `tools/textures/generate_textures.py` | Propriété du projet ; lettrage Yusei Magic, OFL | Aucune |
| Sons : coups, souffles, parades, chute K.O., moteur en boucle, klaxon, portières, sifflet, clics d'interface, ambiance urbaine | `Art/Audio/*.wav` (44,1 kHz, mono) | `tools/audio/generate_audio.py` (synthèse numpy et scipy) | Propriété du projet | Aucune |
| Sol du quartier (chaussée, bordures, trottoirs, marquages, passage piéton), matériaux URP, prefabs, contrôleurs d'animation, scènes | `Assets/PoingMort/Generated/`, `Scenes/`, `Settings/` | Outils éditeur du menu `PoingMort` | Propriété du projet | Générés dans Unity par **PoingMort > 2. Construire les scènes**, puis à commiter |

## Fourni par Unity (non versionné ici)

| Ressource | Source | Licence | Usage |
|---|---|---|---|
| Universal Render Pipeline, Input System 1.20.0, uGUI 2.0.0, Test Framework 1.6.0, intégrations Rider et Visual Studio | Unity Package Manager (`Packages/manifest.json`) | Unity Companion License / conditions d'Unity | Rendu, commandes, interface, tests |
| Shader `Skybox/Procedural` | Intégré à l'éditeur | Conditions d'Unity | Ciel de fin d'après-midi |
| Police intégrée `LegacyRuntime.ttf` | Intégrée à Unity | Conditions d'Unity | Secours uniquement, si les polices du thème manquaient |

## Référence, hors jeu

| Ressource | Source | Usage |
|---|---|---|
| `docs/reference/planche_concept.webp` | Fournie par le porteur du projet | Référence de direction artistique uniquement. Elle n'est utilisée ni dans le jeu ni comme preuve de rendu. |
| `docs/BRIEF.md` | Fourni par le porteur du projet | Brief de développement |

## Outils utilisés pendant la production, non inclus dans le jeu

- **Blender 4.5**, sous forme du module Python `bpy` 4.5.x (GPL) : modélisation, rig, animation et
  export FBX par script.
- **MPFB2** (code GPLv3) : génération du corps et du squelette dans Blender.
- **Assemblies de référence Unity publiées sur NuGet par la communauté** (paquets non officiels
  `Unity3D.SDK`, `UnityEngine.Modules`, `Unity3D.UnityEngine.UI`) et code source de l'Input System
  (https://github.com/Unity-Technologies/InputSystem) : ils ont servi uniquement à compiler et à
  tester le code hors Unity dans l'environnement cloud. Aucun de ces fichiers n'est versionné ni
  redistribué.
