# Rapport du jalon P01 — première preuve jouable et visuelle

Ce rapport distingue trois choses : ce qui a été **exécuté** avec un résultat observable, ce qui a
seulement été **écrit**, et ce qui reste à **vérifier humainement**. Un test de code ne prouve pas
qu'une animation est belle ni qu'une scène est réussie.

## 1. Blocage principal

L'environnement cloud de production ne permettait pas d'installer ni d'exécuter Unity : les hôtes
de téléchargement d'Unity et le registre de paquets `packages.unity.com` sont bloqués, et une
licence serait nécessaire. Par conséquent :

- le projet **n'a jamais été ouvert dans Unity** ;
- aucune scène n'a été construite ni lancée, aucun build Windows n'a été produit ;
- **aucune capture d'écran ni vidéo de jeu n'existe** ; aucune image de ce dépôt ne doit être prise
  pour un rendu Unity ;
- les performances n'ont pas été mesurées.

## 2. Ce qui a été exécuté ici

| Vérification | Moyen | Résultat |
|---|---|---|
| Compilation du code de jeu, en configuration lecteur et en configuration éditeur | Assemblies de référence Unity (API 2021.3), uGUI, Input System 1.20.1 compilé depuis ses sources | 0 erreur, 0 avertissement |
| Compilation des outils éditeur | Mêmes références, plus `UnityEditor.dll` (API 2021.1) | 0 erreur, 0 avertissement |
| Compilation de tous les tests (EditMode et PlayMode) | Mêmes références, NUnit 3 | 0 erreur |
| 36 tests EditMode de logique et de disposition | Exécutés hors Unity : Mono, NUnit 3, `UnityEngine.dll` de référence | **36/36 réussis** |
| Disposition du quartier | Contrôles géométriques automatisés, puis reconstruction **interne** de la disposition dans Blender avec les vrais modèles, sur 4 vues | Défauts trouvés et corrigés (§5) |
| Ressources | Rendus Blender internes pendant la production : personnages, animations planche par planche, voiture, décor | Défauts corrigés au fil de l'eau |

Les 36 tests exécutés couvrent :

- **Trafic** : les voitures ne s'arrêtent jamais et avancent toujours, l'espacement est conservé
  malgré les voitures hélées, les changements de voie et les piétons, le joueur ne peut pas freiner
  jusqu'à l'arrêt, les changements de voie sont refusés quand l'espace est occupé, les voitures
  ralentissent en courbe, et la boucle et ses distances sont cohérentes.
- **Combat** : fenêtres d'anticipation, de contact et de récupération ; un seul impact par coup ;
  portée et angle respectés ; garde efficace seulement de face ; enchaînements ; santé et K.O.
- **Embarquement et sortie** : distance, vitesse relative et côté libre exigés ; vitesse d'une
  voiture hélée toujours positive ; sortie de l'autre côté ou refus ; roulade au-delà d'un seuil ;
  trajectoires ; dix cycles d'entrée et de sortie ; transitions interdites refusées.
- **Disposition du quartier** : aucun chevauchement ; mobilier hors chaussée ; façades, bancs,
  abribus et lampadaires tournés vers la rue ; voies au bon endroit et en sens horaire ; voitures
  sur leurs voies et espacées ; départs et zone de combat dégagés ; triangles du sol orientés vers
  le haut.

**Limites de ces vérifications.** Les assemblies de référence sont plus anciennes qu'Unity 6. Une
API modifiée dans Unity 6 pourrait encore provoquer une erreur que seule une compilation dans
Unity révèlera. Les rendus Blender montrent des modèles et une disposition, pas l'éclairage URP ni
le rendu du jeu.

## 3. Ce qui est écrit mais n'a pas été exécuté

- **Tests EditMode qui exigent l'éditeur** (`ProjectAssetTests`) :
  - les deux avatars Humanoid sont valides ;
  - les 22 clips sont présents et les boucles réglées ;
  - aucune pose en T sur les clips debout ;
  - chaque matériau des modèles existe dans le catalogue URP, et les textures sont présentes ;
  - contenu des scènes générées : aucun script manquant, aucun matériau rose ou manquant, tous les
    systèmes présents.
- **Tests PlayMode** (`QuartierPlayTests`, dans la vraie scène Quartier) : voir la matrice au §4.
- **Outils éditeur** : configuration du projet, import, prefabs et construction des scènes.

## 4. Matrice des vérifications demandées pour P01

**L'accueil et le lancement fonctionnent.**
- Couverture automatisée : `MenuSceneHasTheHomeScreen` (EditMode) vérifie le contrôleur d'accueil,
  le thème, le logo, la caméra, le personnage animé et l'ordre des scènes dans le build.
- Statut : écrit, non exécuté.
- À vérifier humainement : lancer avec PoingMort > 3. Vérifier que chaque bouton fonctionne et que
  « Continuer » est grisé.

**Aucun matériau manquant, personnage en T-pose ou erreur bloquante.**
- Couverture automatisée : `EveryModelMaterialHasALibraryMaterial`, `NoClipShowsATPose`,
  `CharacterModelsAreValidHumanoids`, `GameSceneHasEverySystem` et `MenuSceneHasTheHomeScreen`,
  qui inclut la vérification qu'aucun matériau n'est rose.
- Statut : écrit, non exécuté.
- À vérifier humainement : console vide d'erreurs après la construction et en jeu ; regarder les
  personnages en mouvement.

**Les voitures ne s'immobilisent pas : en situation normale, pendant un combat, pendant une entrée
ou une sortie.**
- Couverture automatisée :
  - logique : `VehiclesNeverStopAndAlwaysAdvance`, `HailedSpeedStaysPositiveAndCatchable`, et
    d'autres tests de trafic ;
  - scène : `CarsNeverStopInNormalPlay`, `TenBoardingsAndExitsKeepCameraAndControls`,
    `OpponentDefeatThenRestart`, qui mesurent le déplacement réel.
- Statut : logique **exécutée et réussie** ; tests de scène écrits, non exécutés.
- À vérifier humainement : observer le trafic plusieurs minutes, y compris en restant debout sur
  la chaussée.

**Dix entrées et sorties successives ne cassent ni la caméra ni les contrôles.**
- Couverture automatisée : `TenEnterExitCyclesEndOnFoot` (logique) et
  `TenBoardingsAndExitsKeepCameraAndControls` (scène : états, parentage, contexte de commandes,
  mode caméra, voiture qui continue, joueur hors de la carrosserie).
- Statut : logique **exécutée et réussie** ; test de scène écrit, non exécuté.
- À vérifier humainement : faire dix fois l'enchaînement à la main et juger la transition de caméra
  et l'alignement du personnage sur la portière.

**Une interaction impossible est refusée proprement, sans blocage.**
- Couverture automatisée : `BoardingNeedsProximityMatchingSpeedAndAFreeSide`,
  `ExitUsesTheOtherSideOrIsRefused` et `ImpossibleTransitionsAreRefused` (logique), et
  `ImpossibleExitIsRefusedCleanly` (scène : message affiché, voiture qui continue, sortie possible
  ensuite).
- Statut : logique **exécutée et réussie** ; test de scène écrit, non exécuté.
- À vérifier humainement : tenter de monter en restant immobile, de descendre trop vite ou
  contre un obstacle.

**Les coups n'appliquent les dégâts qu'à portée et pendant leur fenêtre active.**
- Couverture automatisée : `TimelineGoesThroughAnticipationContactRecovery`,
  `ATargetIsHitOnlyOncePerPunch` et `StrikeZoneRespectsReachAndAngle` (logique), et
  `PunchesHitOnlyInRangeOnceAndNotThroughWalls` (scène : hors portée, pendant l'anticipation, un
  seul impact, mur).
- Statut : logique **exécutée et réussie** ; test de scène écrit, non exécuté.
- À vérifier humainement : vérifier que le contact est lisible et que les bras ne traversent pas
  l'adversaire.

**La défaite de l'adversaire et le redémarrage du test fonctionnent.**
- Couverture automatisée : `OpponentDefeatThenRestart` et `PlayerDefeatThenRestart`.
- Statut : écrit, non exécuté.
- À vérifier humainement : gagner un combat, en perdre un, recommencer avec F.

**La pause puis la reprise restituent le mouvement et les commandes.**
- Couverture automatisée : `PauseFreezesTheWorldAndResumeRestoresIt`.
- Statut : écrit, non exécuté.
- À vérifier humainement : mettre en pause pendant une sortie de voiture et pendant un combat.

## 5. Défauts trouvés et corrigés grâce aux contrôles hors Unity

- **Vitrines** : les vitrines de l'épicerie, de la laverie et du café étaient fermées par un verre
  opaque qui masquait l'intérieur éclairé. Elles sont devenues des vitrines profondes, avec le fond
  éclairé visible.
- **Abribus** : il mordait de 10 cm sur deux façades.
- **Cour** : des caisses chevauchaient les projecteurs.
- **Point de départ** : le joueur, dos à une façade, avait la caméra écrasée contre le mur, et un
  potelet se trouvait juste devant la caméra. Le joueur est désormais tourné vers les voitures qui
  arrivent, avec le trottoir dégagé derrière lui.
- **Accueil** : le personnage se trouvait derrière la colonne de menu, à 44 % de la largeur de
  l'écran. Il est maintenant placé au tiers droit, à 68 %, devant l'épicerie.
- **Calque d'animation du haut du corps** : son état de repos vide pouvait imposer une pose par
  défaut aux bras. Son poids est désormais piloté par le code et vaut 0 hors des coups et de la
  garde.
- **Contrôleur d'animation** : il était recréé à chaque construction, ce qui changeait son GUID.
  Il est désormais reconstruit en place, avec des références stables entre machines.

## 6. Risques principaux à surveiller à la première ouverture dans Unity

1. **Import Humanoid** des personnages. L'association automatique des os `mixamorig:*` est
   attendue sans intervention.
   - En cas d'échec, le test `CharacterModelsAreValidHumanoids` l'indique.
   - Correction : `Rig > Configure` sur le FBX.
2. **Clips** : renommage `Armature|X` en `X`, réglage des boucles et racine figée. Le test
   `CharacterModelsContainEveryClip` le contrôle.
3. **Configuration URP par réflexion** : création de l'asset, Forward+, profil de post-traitement.
   - Si une API diffère, la console affiche un avertissement et les étapes manuelles.
   - La scène reste jouable sans post-traitement.
4. **Alignement des transitions de voiture** : hauteur d'assise, ouverture de la portière et
   personnage qui ne doit pas traverser la carrosserie. Cela ne se juge qu'en jeu.
5. **Qualité des animations**, créées par poses-clés procédurales, et **sensation de la caméra**.
6. **Coût de l'éclairage** : 25 lampadaires et 2 projecteurs, sans ombres, en Forward+.

## 7. Intervention minimale pour valider le jalon

1. Installer Unity 6.3 LTS (ou 6.0 LTS). Ouvrir le dossier dans Unity Hub. Accepter l'activation
   de l'Input System si elle est demandée.
2. Accepter **Configurer et construire**, ou lancer PoingMort > 1 puis PoingMort > 2. Relever
   les erreurs et avertissements de la console.
3. Ouvrir **Window > General > Test Runner** et lancer **EditMode** puis **PlayMode**.
4. Jouer le parcours décrit dans le README : accueil, trottoir, héler, monter, conduire, descendre,
   ruelle, combat, victoire ou défaite, recommencer, pause.
5. Prendre de vraies captures :
   - l'accueil ;
   - la rue en gameplay ;
   - le combat ;
   - une courte vidéo du trafic et de l'embarquement (Unity Recorder si disponible).
6. Commiter les fichiers générés : `ProjectSettings/`, `Packages/packages-lock.json`,
   `Assets/PoingMort/Generated/`, `Scenes/` et `Settings/`.
7. Faire un build Windows avec File > Build Profiles et mesurer les images par seconde en 1080p,
   en notant la machine utilisée.

## 8. Choix et écarts assumés

- **Caméra** : un rig de caméra maison (orbite à pied, poursuite en voiture, collisions, fondu,
  secousses réglables) plutôt que Cinemachine, pour éviter un paquet de plus.
- **Circuit** : une boucle à sens unique et deux voies, sans carrefour. Le brief recommande pour le
  premier jalon un circuit simple sans carrefour conflictuel.
- **Contenu reporté** : pas de piéton non joueur, d'argent, d'endurance, de carte ni de course dans
  P01, car ils ne figurent pas dans la liste du jalon. Le HUD affiche la santé, l'objectif,
  l'interaction contextuelle, la vitesse en voiture seulement et l'adversaire pendant un combat.
- **Commandes** : coup rapide au clic gauche (direct, puis croisé en enchaînement), coup puissant
  (crochet) sur E, verrouillage sur Tab ou clic molette, klaxon sur Espace en voiture.
