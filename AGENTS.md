# Consignes pour les agents (Claude Code, Codex…)

Projet : **POING MORT**, jeu PC solo 3D à la troisième personne, sous Unity 6 avec URP et C#. Le
brief de référence est [`docs/BRIEF.md`](docs/BRIEF.md). L'état du jalon en cours se trouve dans
[`docs/P01_RAPPORT.md`](docs/P01_RAPPORT.md).

## Règles

- Interface, menus, messages au joueur, README et rapports : **en français**. Le code, ses
  commentaires et les messages de commit sont en anglais.
- **Honnêteté** : ne jamais annoncer un build, des tests passés ou un rendu validé sans preuve.
  Quand Unity n'est pas disponible, dire précisément ce qui a été écrit, ce qui n'a pas été exécuté
  et l'intervention nécessaire. Ne jamais présenter une image générée ou la planche de référence
  comme une capture du jeu.
- **Ressources** : rien de payant sans accord du porteur du projet. Vérifier chaque licence et
  tenir [`ASSETS.md`](ASSETS.md) à jour : ressource, auteur ou source, licence, usage, intervention
  manuelle. Ne jamais inventer un nom de pack ou de fichier.
- **Fichiers `.meta`** : ne jamais modifier un GUID existant. Après l'ajout de fichiers sous
  `Assets/`, lancer `python3 tools/generate_metas.py`. Ce script crée les `.meta` manquants avec un
  GUID déterministe et n'écrase rien.
- **Scènes et ressources générées** (`Assets/PoingMort/Scenes`, `Generated`, `Settings`) : elles
  sont produites par le menu Unity **PoingMort > 2. Construire les scènes**. Pour les changer,
  modifier les outils de `Assets/PoingMort/Scripts/Editor`, pas les fichiers à la main. La
  disposition du quartier se trouve dans `QuartierLayout.cs` ; son calcul est pur et testé par
  `LayoutTests`.
- **Trafic** : les voitures ne s'arrêtent jamais pendant le jeu. Seule la pause explicite suspend
  la simulation. Aucun effet de combat ne doit geler le temps global ; l'arrêt d'impact reste
  local aux combattants.
- Garder les systèmes séparés (trafic, véhicules, joueur, combat, interface, sauvegarde) et les
  paramètres exposés dans l'Inspector.

## Structure

```
Assets/PoingMort/Scripts/Runtime   jeu (asmdef PoingMort.Runtime)
Assets/PoingMort/Scripts/Editor    outils de construction (asmdef PoingMort.Editor, menu PoingMort)
Assets/PoingMort/Tests             EditMode (logique, disposition, import, scènes) et PlayMode
Assets/PoingMort/Art               sources des ressources (FBX, textures, sons, polices)
tools/                             générateurs Blender/Python des ressources, generate_metas.py
```

## Vérifier un changement

- **Avec Unity** :
  - ouvrir **Window > General > Test Runner** et lancer EditMode puis PlayMode ;
  - pour PlayMode, construire d'abord les scènes (PoingMort > 2) ;
  - en ligne de commande :
    `Unity -batchmode -projectPath . -executeMethod PoingMort.EditorTools.PoingMortMenu.BatchBuild -quit`.
- **Sans Unity**, par exemple dans un environnement cloud : seules des vérifications partielles sont
  possibles.
  - Les fichiers de logique pure (`LaneLoop`, `TrafficSimulation`, `CombatLogic`,
    `AttackDefinition`, `BoardingRules`, `PlayerStateMachine`, `QuartierLayout`, `GroundGeometry`)
    et les tests EditMode qui ne touchent pas à l'éditeur se compilent et s'exécutent avec NUnit,
    à condition de disposer d'une `UnityEngine.dll` de référence.
  - Ces vérifications ne remplacent pas Unity.

## Branches

Une branche par session de travail, fusion par pull request vers `main`. Avant de pousser des
ressources générées par Unity (scènes, prefabs, `ProjectSettings/`), s'assurer qu'une seule
machine les a créées, pour éviter des GUID divergents.
