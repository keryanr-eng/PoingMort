# POING MORT — Brief de développement Unity

Tu travailles sur POING MORT, un jeu PC solo en 3D à la troisième personne. Tu interviens comme développeur Unity, game designer et directeur artistique, avec une priorité : livrer un petit jeu réellement jouable et visuellement cohérent, pas une démonstration technique présentée comme un jeu terminé.

La planche jointe est la référence de direction artistique : accueil, personnage, exploration, conduite, combat, course et carte. Elle représente une intention, pas des ressources 3D déjà disponibles. Les personnages et les scènes doivent exister dans Unity et être visibles depuis une caméra libre, pas être simulés par une image de fond.

Lis l’ensemble du brief. Inspecte ensuite le projet et les outils disponibles, puis réalise le premier jalon défini à la fin. Si un projet existe, améliore-le sans repartir de zéro inutilement. Ne te contente pas de répondre avec un plan.

## 1. Concept et règles non négociables

Le joueur évolue dans un petit quartier ouvert contemporain. Il se déplace à pied, se bat avec ses poings, participe à des combats clandestins et à des courses pour gagner de l’argent.

La particularité du monde : les voitures qui circulent sont autonomes et ne s’arrêtent jamais pendant le gameplay.

- La ville possède des trottoirs, des bâtiments, des commerces et des piétons. Elle n’est pas remplie exclusivement de voitures.
- La circulation est normale et espacée : aucun embouteillage permanent ou flot autoroutier à chaque coin de rue.
- Monter et descendre d’une voiture se fait toujours pendant qu’elle roule.
- Une voiture continue de rouler après la sortie du joueur.
- Aucune attente à l’arrêt aux intersections, pendant l’embarquement ou à l’arrivée d’une course.
- Les combats reposent sur les poings : directs, crochets, uppercuts, garde, esquive et contre. Pas de coups de pied, d’armes à feu, de couteaux ou d’épées.
- Des équipements fixés aux mains pourront exister : bandes, gants renforcés, poings américains fictifs. Ce n’est pas prioritaire pour le premier jalon.

Hypothèse de gameplay à utiliser pour le prototype : la voiture avance et suit sa voie automatiquement ; le joueur peut influencer l’allure, changer de voie et choisir une direction aux intersections. Il ne peut jamais freiner jusqu’à l’arrêt. Ce compromis donne un rôle au joueur dans les courses sans supprimer l’autonomie des véhicules. Garde ce fonctionnement paramétrable.

La pause explicite du jeu peut suspendre la simulation. En revanche, aucun effet de combat ni simple panneau d’interaction ne doit arrêter silencieusement le trafic.

## 2. Direction artistique : 3D stylisée urbaine, sobre et soignée

Reprends l’esprit de la dernière planche, en adaptant son niveau de détail aux ressources réellement disponibles.

Je recherche :
- Une 3D stylisée adulte, avec des silhouettes lisibles et de vraies proportions humaines.
- Un personnage reconnaissable : vêtements urbains simples, hoodie sombre à capuche claire, pantalon ample, baskets, coiffure identifiable.
- Des visages construits, des mains crédibles et des articulations qui se déforment correctement.
- Des surfaces plutôt mates, des textures simples et une palette cohérente : gris chauds, béton, brique désaturée, touches de rouge et lumière de fin de journée.
- Un quartier compact, avec des façades modulaires réutilisées intelligemment.
- Des cadrages de gameplay clairs et un éclairage qui détache le personnage du décor.

Je ne veux pas :
- De photoréalisme, de rues miroir, de mégalopole cyberpunk ou de détails microscopiques partout.
- De personnage final composé de cubes, de capsules et de sphères visibles.
- De personnages cubiques, de mains en boules ou de silhouettes de mannequins sans personnalité.
- De mélange incohérent entre personnages cartoon, voitures photoréalistes et décors d’une autre époque graphique.
- D’un écran saturé de slogans, de néons, de particules ou de texte explicatif.

Simplifie le décor avant de sacrifier le personnage et ses animations. Un quartier plus petit mais cohérent vaut mieux qu’une grande ville vide.

## 3. Ressources graphiques : peu d’assets, pas zéro asset par magie

Commence par inventorier les modèles, animations, matériaux et outils réellement accessibles.

Réutilise en priorité ce qui existe. Des ressources gratuites et légalement utilisables sont acceptables. Aucun achat, abonnement ou service payant sans mon accord. Vérifie les licences ; ne suppose pas qu’un téléchargement gratuit autorise tous les usages.

Budget de variété initial :
- Un personnage principal articulé.
- Une base d’adversaire/piéton, déclinée par vêtements ou couleurs si possible.
- Un modèle de voiture pour le premier jalon ; deux pour la petite version complète.
- Quelques modules de façade, fenêtres, portes et devantures.
- Un petit ensemble de mobilier : lampadaire, banc, poubelle, arbre, barrière, panneau.

Pour le personnage, utilise un véritable modèle avec squelette, pondération des déformations et animations compatibles. Blender peut servir à produire ou corriger un vrai modèle ; ce qui est interdit est le résultat grossier en primitives, pas le logiciel.

Si une ressource essentielle manque, indique rapidement le fichier nécessaire, son rôle, son format et une source précise vérifiée lorsqu’elle est accessible. Ne cache pas le problème sous un personnage improvisé présenté comme définitif. Continue les tâches indépendantes, mais marque la validation visuelle comme bloquée tant que la ressource manque.

Des formes provisoires sont acceptées dans une scène technique de test, jamais comme preuve que la DA est réussie.

Tiens un fichier ASSETS.md : ressource, auteur/source, licence, usage et éventuelle intervention manuelle. N’invente pas des noms de packs ou des fichiers téléchargés.

## 4. Monde ouvert initial

La cible est un quartier, pas une ville entière : environ 200 à 300 mètres de côté à ajuster selon le gameplay, deux à quatre îlots, des routes connectées, une petite place et une cour dédiée aux combats.

Prévois un circuit routier bouclé pour pouvoir circuler indéfiniment, quelques variantes de parcours pour une future course, et des limites naturelles : bâtiments, murs, ponts ou clôtures.

Les bâtiments peuvent être essentiellement des façades. Pas d’intérieurs accessibles partout. Une entrée de commerce peut ouvrir une interface simple sans charger un magasin complet.

La qualité vient de la composition, des proportions, des couleurs et de quelques repères reconnaissables, pas de centaines d’objets uniques.

Utilise une heure fixe de fin d’après-midi pour commencer. Pas de cycle jour/nuit, de météo dynamique ou de streaming complexe dans cette première version.

## 5. Trafic permanent et interactions avec les voitures

Le trafic est un système central, pas une décoration.

Commence avec des voies et des trajectoires bouclées maîtrisées. Sur le premier jalon, privilégie un circuit simple sans carrefour conflictuel. Ajoute les intersections plus complexes seulement après validation.

Les véhicules doivent conserver une vitesse réellement positive dans le monde, pas seulement une variable « speed » supérieure à zéro pendant qu’ils sont bloqués contre un mur.

Prévois des espacements suffisants, une anticipation des obstacles et des trajectoires qui ne produisent pas de blocages. Ne résous pas les collisions en faisant traverser les murs. Pas de système physique automobile réaliste si un comportement arcade contrôlé répond mieux au concept.

Pour monter :
- Sélection d’une voiture proche avec indication claire d’interaction.
- Conditions de distance, de côté accessible et de vitesse relative compatibles avec le personnage.
- Brève transition animée et alignée sur la voiture mobile.
- Passage fluide de la caméra à pied à la caméra véhicule.
- Pas de téléportation visible depuis plusieurs mètres ni d’arrêt caché de la voiture.

Pour descendre :
- Vérification d’un espace libre du bon côté.
- Sortie animée avec mouvement hérité du véhicule et réception lisible.
- Si la sortie est dangereuse ou bouchée, elle est refusée temporairement ; la voiture continue.
- Pas d’apparition dans un mur, sous la chaussée ou dans un autre véhicule.

Gère proprement les états ÀPied, Embarquement, EnVoiture, Sortie et Chute. Un seul système contrôle le déplacement à un instant donné. Évite les conflits entre animation, physique, parentage et contrôleur de personnage.

Le modèle doit permettre de représenter une entrée/sortie crédible, notamment une porte mobile ou une ouverture adaptée. N’affiche pas le personnage en train de traverser une carrosserie fermée.

## 6. Personnage et combats

Déplacements relatifs à la caméra, rotation fluide, marche/course, sprint et esquive. Prise en charge du clavier AZERTY et du QWERTY, avec commandes affichées cohérentes et séparation des commandes à pied/en voiture/menu.

Proposition de commandes initiales : ZQSD/WASD pour se déplacer, souris pour la caméra, Maj pour sprinter, F pour interagir ou entrer/sortir, clic gauche pour frapper, clic droit pour garder, Espace pour esquiver, Échap pour la pause. Attribue les autres actions sans conflit.

Animations attendues progressivement : repos, locomotion, garde, directs gauche/droite, crochet ou uppercut, esquive, réaction à l’impact, mise au sol et transitions véhicule.

Le combat doit être dynamique mais lisible : anticipation, contact, réaction et récupération. Évite les bras qui traversent l’adversaire et les pieds qui glissent en permanence.

La petite version complète vise une chaîne de trois coups légers, un coup puissant, garde, esquive et contre simple. Le premier jalon peut n’avoir que deux coups bien exécutés.

Les dégâts correspondent aux fenêtres actives des attaques et à une portée réelle. Une attaque ne touche pas dix fois à cause d’un collider persistant et ne frappe pas à travers un mur.

Un adversaire simple suffit au départ : approche, mise à distance, garde, attaque annoncée, réaction et défaite. Ensuite seulement, ajoute un deuxième profil.

Travaille les sensations avec des sons d’impact, une réaction corporelle, une légère impulsion de caméra et quelques effets discrets. Un éventuel arrêt d’impact doit être local aux combattants, jamais un gel global du trafic. Permets de réduire les secousses de caméra.

## 7. Menus et interface

Tout est en français. Reprends la hiérarchie de la planche, pas ses éventuelles erreurs ou ses textes promotionnels.

Accueil : titre POING MORT, personnage et portion de quartier en arrière-plan 3D, boutons Nouvelle partie, Continuer, Options, Crédits et Quitter. Continuer est indisponible sans sauvegarde. Un menu plus réduit est acceptable au premier jalon, mais chaque bouton affiché doit fonctionner.

Direction UI : panneaux sombres discrets, texte clair, rouge comme accent, typographie lisible. Le traitement graffiti est réservé au titre. Pas d’interface de site web, de cartes marketing ou de paragraphes inutiles.

En jeu : santé, endurance si elle est utilisée, argent, objectif court et interaction contextuelle. Afficher la vitesse seulement en voiture et les informations de course seulement pendant une course. Ne surcharge pas l’écran de tutoriels permanents.

Carte : plan simple du quartier, position du joueur, combat et course. Elle doit correspondre au vrai niveau, pas à une illustration indépendante. Une mini-carte est secondaire tant que les déplacements et le combat ne sont pas solides.

Pause/options : reprendre, audio, sensibilité, qualité graphique, secousses de caméra et retour à l’accueil. Les réglages implémentés persistent.

Apparence : d’abord le personnage existant présenté en 3D avec rotation, puis quelques variantes réelles de couleurs ou de vêtements. Pas de création complète de visage ni de boutons sans effet. Les options absentes du prototype ne doivent pas être simulées.

Interface adaptée au minimum à 1280 × 720 et 1920 × 1080, sans chevauchement ou texte coupé.

## 8. Boucle de jeu de la petite version complète

La cible après le premier jalon : explorer → rejoindre un combat ou une course → gagner de l’argent → acheter une amélioration simple ou une variante cosmétique → recommencer.

Un lieu de combat, deux profils d’adversaires, une course et une petite boutique suffisent. Prévois victoire, défaite, revanche et récompense sans duplication accidentelle.

Les courses utilisent un départ lancé : jamais une grille avec des voitures immobiles. Checkpoints ordonnés, classement réel et deux ou trois concurrents maximum. Après l’arrivée, les voitures continuent à rouler et le résultat apparaît sans les immobiliser.

Sauvegarde locale simple : argent, progression, apparence disponible et options. Recharge le joueur dans un emplacement sûr à pied ; ne cherche pas à sauvegarder toute la simulation du trafic.

Hors périmètre initial : multijoueur, police complexe, armes à feu, histoire ramifiée, monde procédural infini, foule massive, destruction réaliste, combat sur les toits des voitures et personnalisation automobile avancée.

## 9. Base technique et qualité du projet

Cible : Unity, C#, PC Windows, rendu URP. Respecte d’abord la version installée et les packages du projet ; ne lance pas une migration inutile. Pour un nouveau projet, utilise une version stable disponible et documente-la.

Utilise les versions compatibles des outils nécessaires : Input System pour les commandes, Animator et Avatar Humanoid correctement configuré pour les personnages compatibles, Cinemachine si cela simplifie les caméras. Vérifie les API de la version installée au lieu de mélanger des exemples incompatibles.

Architecture simple : systèmes séparés pour personnage, combat, véhicules, trafic, activités, interface et sauvegarde. Paramètres importants modifiables dans l’Inspector ou des données dédiées. Pas de framework géant avant le premier gameplay.

Préserve les fichiers .meta et la configuration du projet. Si tu crées des outils Editor pour monter les scènes, ils doivent être reproductibles et ne pas dupliquer tout à chaque exécution.

Limite les matériaux, les lumières coûteuses, les ombres dynamiques et le nombre d’entités actives. Réutilise les ressources. Vise 60 images/seconde à 1080p sur la machine de test, mais donne uniquement des performances mesurées et précise les conditions. Ce n’est pas une promesse pour tout PC.

Le jeu doit fonctionner sans serveur, abonnement ou API distante au runtime.

## 10. Ordre de production

Ne tente pas de construire toute la cible en une seule passe.

### P01 — Première preuve jouable et visuelle

Réalise un accueil fonctionnel, une courte rue soignée avec circuit bouclé, un vrai personnage animé contrôlable, quelques voitures espacées utilisant un seul modèle, l’embarquement et la sortie en mouvement, et un adversaire permettant de tester deux coups, la garde et sa défaite.

Le décor peut rester très petit. Les systèmes d’économie, de course complète, de carte détaillée et de personnalisation avancée attendent. En revanche, le personnage ne doit pas être un mannequin géométrique présenté comme final.

Le joueur doit pouvoir lancer le jeu, marcher, prendre une voiture mobile, en descendre, tester un combat et recommencer.

### P02 — Petite boucle complète

Étends au quartier initial, ajoute le combat rémunéré, la course à départ lancé, une amélioration, la sauvegarde, la carte simple et les menus correspondants.

### P03 — Finition

Améliore animations, transitions, lisibilité, sons, équilibrage et performances. Ajoute de la variété uniquement si la base est stable.

Dans cette intervention, concentre-toi sur P01. Ne dilue pas la livraison en commençant simultanément tous les systèmes de P02/P03.

## 11. Vérifications et livraison

Teste réellement ce que ton environnement permet : compilation Unity, lancement, transitions de contrôle et interactions répétées.

Pour P01, vérifie au minimum :
- L’accueil et le lancement fonctionnent.
- Aucun matériau manquant, personnage en T-pose ou erreur bloquante dans la console.
- Les voitures ne s’immobilisent pas en situation normale, lors d’un combat ou pendant une entrée/sortie.
- Dix entrées/sorties successives ne cassent ni la caméra ni les contrôles.
- Une interaction impossible est refusée proprement, sans blocage.
- Les coups appliquent les dégâts attendus uniquement à portée et pendant leur fenêtre active.
- La défaite de l’adversaire et le redémarrage du test fonctionnent.
- La pause puis la reprise restituent correctement le mouvement et les commandes.

Sépare les tests automatisés, les observations en jeu et ce qui reste à vérifier humainement. Un test de code ne prouve pas qu’une animation est belle.

Livre le projet et un README permettant de l’ouvrir et de jouer. Indique la scène d’entrée, la version, les commandes, les dépendances et les limites connues. Fournis un build Windows si l’environnement permet réellement de le produire.

Si tu as accès à Unity, prends de vraies captures de l’accueil, de la rue en gameplay et du combat, ainsi qu’une courte capture vidéo du trafic et de l’embarquement si l’outil existe. Ne remplace jamais ces preuves par des images générées ou par la planche de référence.

Si Unity ou une ressource essentielle manque dans ton environnement, dis précisément ce qui a été écrit, ce qui n’a pas été exécuté et l’intervention minimale nécessaire. N’annonce ni build réussi, ni tests passés, ni résultat visuel validé sans preuve.

Commence maintenant par inspecter l’environnement et les ressources, expose brièvement les choix et éventuels blocages, puis travaille sur P01. N’attends pas des confirmations pour les décisions secondaires. La priorité est un premier morceau de POING MORT vraiment jouable, cohérent avec la planche et honnête sur ce qui est réellement livré.
