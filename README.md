# POING MORT — prototype P01

Jeu PC solo en 3D à la troisième personne (Unity 6, URP, C#). Un petit quartier contemporain où
les voitures autonomes **ne s'arrêtent jamais** : on y monte et on en descend en marche, et on se
bat aux poings dans une cour cachée.

Ce dépôt contient le jalon **P01, « première preuve jouable et visuelle »** :

- écran d'accueil fonctionnel avec la rue en 3D et le personnage en arrière-plan ;
- une rue en boucle (sens unique, deux voies) avec trottoirs, commerces, mobilier, et cinq voitures
  espacées utilisant un seul modèle ;
- un vrai personnage riggé et animé (base MakeHuman CC0), contrôlable à la souris et au clavier
  AZERTY ou QWERTY ;
- l'embarquement et la sortie d'une voiture en mouvement ;
- un adversaire, « Le Mur », pour tester les coups, la garde, sa défaite et le redémarrage du combat.

Version : **0.1.0-p01**. Brief : [`docs/BRIEF.md`](docs/BRIEF.md). Planche de direction
artistique : [`docs/reference/planche_concept.webp`](docs/reference/planche_concept.webp).

> **Important : ce qui a été vérifié et ce qui ne l'a pas été.** Unity ne pouvait pas être installé
> dans l'environnement cloud où ce jalon a été produit. Le code a été compilé contre des
> assemblies de référence Unity et 36 tests de logique ont été exécutés hors Unity. **Le projet n'a
> jamais été ouvert dans Unity** : aucun lancement, aucun build et aucune capture d'écran n'ont été
> faits, et aucun rendu Unity n'a été validé visuellement. Le détail figure dans
> [`docs/P01_RAPPORT.md`](docs/P01_RAPPORT.md), avec la liste des vérifications à faire.

## Ouvrir le projet et jouer

1. Installe **Unity 6.3 LTS (6000.3.x)** avec Unity Hub. Unity 6.0 LTS (6000.0.x) convient
   aussi. Sous Windows, le module de build Windows est inclus par défaut.
2. Dans Unity Hub, choisis **Add > Add project from disk**, puis sélectionne le dossier du dépôt.
   À la première ouverture, Unity importe les paquets et les ressources, ce qui prend quelques
   minutes. Si Unity propose d'aligner la version d'URP sur celle de l'éditeur, accepte.
3. Si Unity demande d'activer le nouvel Input System (« native platform backends »), réponds
   **Yes**. L'éditeur redémarre.
4. Une fenêtre « Première ouverture du projet » propose **Configurer et construire** : accepte.
   Elle ne s'affiche qu'une fois. Sinon, utilise le menu **PoingMort > 1. Configurer le projet**,
   puis **PoingMort > 2. Construire les scènes**.
5. Lance **PoingMort > 3. Jouer depuis l'accueil**.

La scène d'entrée est `Assets/PoingMort/Scenes/MainMenu.unity`, et la scène de jeu
`Assets/PoingMort/Scenes/Quartier.unity`. Les deux sont générées à l'étape 4 : elles ne sont pas
dans le dépôt tant qu'aucune machine ne les a construites et commitées.

**Travail sur plusieurs postes** (PC fixe, portable, cloud, Codex) : construis les scènes **sur une
seule machine**, puis commite :

- `ProjectSettings/`
- `Packages/packages-lock.json`
- `Assets/PoingMort/Generated/`
- `Assets/PoingMort/Scenes/`
- `Assets/PoingMort/Settings/`

Les autres postes récupèrent ces fichiers et n'ont rien à reconstruire. Une reconstruction ultérieure
réutilise les mêmes fichiers et donc les mêmes GUID Unity. Construire en parallèle sur deux machines
avant le premier commit créerait des GUID différents.

## Commandes

Les touches sont définies par leur **position physique** : ZQSD sur un clavier AZERTY correspond à
WASD sur un clavier QWERTY. Les invites à l'écran affichent la lettre de ton clavier.

**À pied**

| Action | Clavier et souris | Manette |
|---|---|---|
| Se déplacer | ZQSD (AZERTY), WASD (QWERTY) ou flèches | Stick gauche |
| Caméra | Souris | Stick droit |
| Sprinter | Maj gauche | Clic du stick gauche |
| Interagir : héler, monter, défier, recommencer | F | Y / Triangle |
| Coup rapide (direct, puis enchaînement direct–croisé) | Clic gauche | X / Carré |
| Coup puissant (crochet) | E | B / Rond |
| Garde (maintenir) | Clic droit | Gâchette gauche |
| Esquive | Espace | A / Croix |
| Verrouiller la cible | Tab ou clic molette | Clic du stick droit |
| Pause | Échap | Start |

**En voiture**

| Action | Clavier | Manette |
|---|---|---|
| Accélérer / ralentir, jamais jusqu'à l'arrêt | Z / S (AZERTY), W / S (QWERTY) ou ↑ / ↓ | Gâchettes |
| Changer de voie | Q / D (AZERTY), A / D (QWERTY) ou ← / → | Stick gauche |
| Descendre | F | Y / Triangle |
| Klaxon | Espace | A / Croix |
| Pause | Échap | Start |

## Parcours de test du jalon

1. Sur l'accueil, choisis **Nouvelle partie**. « Continuer » reste grisé : il n'y a pas encore de
   sauvegarde (prévue en P02).
2. Tu apparais sur le trottoir, face aux voitures qui arrivent sur ta droite. Approche-toi de la
   chaussée et appuie sur **F** pour **héler** une voiture. Elle ralentit à environ 15 km/h, sans
   jamais s'arrêter.
3. Cours le long de la portière. Quand l'invite affiche **Monter**, appuie sur **F** : la portière
   s'ouvre et le personnage s'installe pendant que la voiture roule.
4. En voiture, la voiture suit seule sa voie. Tu peux régler son allure (sans pouvoir l'arrêter) et
   changer de voie. Appuie sur **F** pour descendre. Côté trottoir, la sortie est privilégiée. Elle
   est refusée avec un message si l'espace est occupé, si une voiture arrive ou si la vitesse
   dépasse environ 50 km/h. Au-dessus d'environ 23 km/h, la sortie se termine par une roulade.
5. Entre dans la **ruelle** en face du point de départ : elle mène à la cour. Approche-toi de
   « Le Mur » et appuie sur **F** pour **le défier**. L'adversaire annonce ses coups par un bref
   éclat rouge sur son poing.
6. À la victoire ou au K.O., appuie sur **F** pour recommencer le combat. **Échap** met le jeu en
   pause : c'est la seule chose qui suspend le trafic. Le menu de pause donne accès aux options
   (audio, sensibilité, inversion, secousses de caméra, qualité, plein écran) et au retour à
   l'accueil.

## Contenu technique

- **Scripts** dans `Assets/PoingMort/Scripts/Runtime`. Ils sont organisés en systèmes séparés :
  - `Traffic` : simulation des voies et du trafic, contrôle de la vitesse réelle des voitures ;
  - `Vehicles` : voiture, portières, règles d'embarquement et de sortie ;
  - `Player` : machine à états ÀPied, Embarquement, EnVoiture, Sortie, Chute et KO, et caméra ;
  - `Combat` : fenêtres d'attaque, portée, garde, esquive, adversaire et cour de combat ;
  - `Character` : déplacement et animation ;
  - `Input` : commandes et invites selon le clavier ;
  - `UI` : accueil, pause, options, crédits et HUD ;
  - `Core` : session, réglages persistants et sons.

  Les paramètres importants sont exposés dans l'Inspector : vitesses, distances d'embarquement,
  vitesse plancher, coups, etc.
- **Outils éditeur** dans `Assets/PoingMort/Scripts/Editor`, accessibles par le menu `PoingMort`.
  Ils sont reproductibles et réutilisent les fichiers existants :
  - configuration du projet (URP, couches, Input System) ;
  - règles d'import des modèles (Humanoid, clips, matériaux) ;
  - catalogue de matériaux URP ;
  - prefabs des personnages, de la voiture et du décor ;
  - sol généré ;
  - construction des deux scènes.
- **Disposition du quartier** (`QuartierLayout.cs`) : elle est calculée sans dépendre de l'éditeur,
  ce qui permet de la tester hors scène.
- **Ressources** dans `Assets/PoingMort/Art` : modèles FBX, textures, sons et polices, avec leurs
  générateurs reproductibles dans `tools/`. Leurs sources et licences sont détaillées dans
  [`ASSETS.md`](ASSETS.md).

## Tests

Dans Unity : **Window > General > Test Runner**.

- **EditMode**
  - Logique : trafic, combat, embarquement, machine à états.
  - Disposition du quartier : aucun chevauchement, mobilier hors de la chaussée, façades tournées
    vers la rue, voies, départs dégagés.
  - Import : avatars Humanoid valides, clips présents, aucune pose en T, aucun matériau
    manquant.
  - Contenu des scènes générées.
- **PlayMode**, dans la vraie scène Quartier ; il faut d'abord construire les scènes :
  - les voitures ne s'arrêtent jamais ;
  - dix montées et sorties de voitures en mouvement ;
  - sortie impossible refusée proprement ;
  - pause et reprise ;
  - défaite de l'adversaire, défaite du joueur et redémarrage ;
  - coups qui ne touchent qu'à portée, une seule fois et jamais à travers un mur.

Les 36 tests EditMode de logique et de disposition ont été exécutés hors Unity et passent. Les
autres tests n'ont pas pu être exécutés ici. Voir [`docs/P01_RAPPORT.md`](docs/P01_RAPPORT.md).

## Build Windows

Utilise **File > Build Profiles > Windows > Build**. L'accueil et le Quartier sont déjà dans la
liste des scènes, l'accueil en premier. Aucun build n'a été produit pour cette livraison.

## Dépendances

- Unity 6.3 LTS ou 6.0 LTS.
- Paquets Unity (`Packages/manifest.json`) :
  - Universal RP ;
  - Input System 1.20.0 ;
  - uGUI 2.0.0 ;
  - Test Framework 1.6.0 ;
  - intégrations Rider et Visual Studio.
- Aucun serveur, abonnement ni API distante à l'exécution.
- Régénération des ressources (facultative) :
  - Blender 4.5 ou le module Python `bpy` 4.5 ;
  - MPFB2 et les données MakeHuman, pour les personnages ;
  - Python 3 avec numpy, scipy et Pillow.

  Les commandes figurent en tête de chaque script de `tools/`.

## Limites connues (P01)

- **Rien n'a été exécuté dans Unity** : ni l'import des FBX (avatar Humanoid, clips), ni les
  matériaux URP, ni l'éclairage, ni le post-traitement, ni les performances. Les risques principaux
  sont listés dans le rapport.
- Les animations sont des clips créés pour le projet par pose-clés procédurales. Elles sont
  fonctionnelles mais simples : leur qualité doit être jugée en jeu.
- Une seule boucle sans carrefour : pas de choix de direction aux intersections dans ce jalon.
- Un seul modèle de voiture (cinq couleurs), un seul adversaire, aucun piéton non joueur.
- Pas encore d'argent, de sauvegarde, de carte, de course, de boutique ni d'apparence (P02).
- La cour de combat est peu habillée : bennes, caisses, barrières et projecteurs.
- Performances non mesurées : aucune machine de test Unity n'était disponible.

## Arborescence

```
Assets/PoingMort/
  Art/          modèles, textures, sons, polices (sources versionnées)
  Scripts/      Runtime (jeu) et Editor (outils de construction)
  Tests/        EditMode et PlayMode
  Generated/    matériaux, prefabs, maillages, contrôleurs (créés par PoingMort > 2)
  Scenes/       MainMenu et Quartier (créées par PoingMort > 2)
  Settings/     URP, éclairage, post-traitement, thème d'interface (créés par PoingMort > 1/2)
docs/           brief, planche, rapport du jalon
tools/          générateurs Blender et Python des ressources, création des .meta
```
