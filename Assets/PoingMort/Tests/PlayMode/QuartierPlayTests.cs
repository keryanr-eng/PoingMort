using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PoingMort.Characters;
using PoingMort.Combat;
using PoingMort.Controls;
using PoingMort.Core;
using PoingMort.Player;
using PoingMort.Traffic;
using PoingMort.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PoingMort.Tests
{
    /// <summary>
    /// Play mode checks of the P01 list, run in the real "Quartier" scene (built by PoingMort &gt; 2).
    /// They drive the game through its public API (no simulated keyboard), so they check the systems,
    /// not how the game feels.
    /// </summary>
    public class QuartierPlayTests
    {
        static IEnumerator LoadGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneFlow.GameScene))
                Assert.Ignore("Scène Quartier absente des Build Settings : lancer PoingMort > 2. Construire les scènes.");
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneFlow.GameScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(TrafficSystem.Current, Is.Not.Null, "pas de trafic dans la scène");
            Assert.That(PlayerController.Current, Is.Not.Null, "pas de joueur dans la scène");
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            float t = 0f;
            while (!condition())
            {
                if (t > timeout) Assert.Fail("Délai dépassé : " + what);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [TearDown]
        public void RestoreTime()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [UnityTest]
        public IEnumerator CarsNeverStopInNormalPlay()
        {
            yield return LoadGame();
            var ts = TrafficSystem.Current;
            var start = ts.vehicles.Select(v => v.transform.position).ToArray();
            yield return new WaitForSeconds(6f);
            Assert.That(ts.StallWarnings, Is.EqualTo(0), "une voiture a ralenti sous la moitié de la vitesse plancher");
            Assert.That(ts.MinimumObservedSpeed, Is.GreaterThanOrEqualTo(ts.Parameters.minSpeed * 0.9f), "vitesse réelle mesurée");
            for (int i = 0; i < ts.vehicles.Count; i++)
                Assert.That(Vector3.Distance(start[i], ts.vehicles[i].transform.position), Is.GreaterThan(ts.Parameters.minSpeed * 5f), ts.vehicles[i].name + " a trop peu avancé");
        }

        /// <summary>Ten boardings and exits of moving cars: states, camera, controls and traffic stay consistent.</summary>
        [UnityTest]
        public IEnumerator TenBoardingsAndExitsKeepCameraAndControls()
        {
            yield return LoadGame();
            var player = PlayerController.Current;
            var ts = TrafficSystem.Current;
            var rig = CameraRig.Current;
            Assert.That(rig, Is.Not.Null);
            var messages = new List<string>();
            player.Notified += messages.Add;

            for (int cycle = 0; cycle < 10; cycle++)
            {
                yield return WaitFor(() => player.State == PlayerState.APied, 8f, $"cycle {cycle} : retour à pied");
                var car = ts.vehicles.Where(v => v != null && !v.IsOccupied)
                    .OrderBy(v => Vector3.Distance(v.transform.position, player.transform.position)).First();
                var side = car.KerbSide;
                player.Motor.Teleport(car.GetDoorEntry(side).position, car.transform.rotation);
                Assert.That(player.BeginBoarding(car, side), Is.True, $"cycle {cycle} : embarquement refusé");
                yield return WaitFor(() => player.State == PlayerState.EnVoiture, 3f, $"cycle {cycle} : installation à bord");
                Assert.That(player.transform.parent, Is.EqualTo(car.seatAnchor));
                Assert.That(GameInput.Instance.Context, Is.EqualTo(InputContext.Vehicle));
                yield return new WaitForSeconds(0.9f);
                Assert.That(rig.Mode, Is.EqualTo(CameraMode.Vehicle));
                Assert.That(car.Speed, Is.GreaterThanOrEqualTo(ts.Parameters.minSpeed * 0.95f), "la voiture ne doit jamais s'arrêter");

                Vector3 carBefore = car.transform.position;
                for (int attempt = 0; attempt < 8 && player.State == PlayerState.EnVoiture; attempt++)
                {
                    player.TryExit();
                    yield return new WaitForSeconds(0.7f);
                }
                yield return WaitFor(() => player.State == PlayerState.APied, 6f, $"cycle {cycle} : sortie");
                Assert.That(player.transform.parent, Is.Null);
                Assert.That(player.Motor.Controller.enabled, Is.True, "contrôleur de personnage réactivé");
                Assert.That(GameInput.Instance.Context, Is.EqualTo(InputContext.OnFoot));
                Assert.That(player.transform.position.y, Is.GreaterThan(-0.3f), "le joueur ne doit pas passer sous la chaussée");
                Vector3 local = car.transform.InverseTransformPoint(player.transform.position);
                Assert.That(Mathf.Abs(local.x) > car.width * 0.5f || Mathf.Abs(local.z) > car.length * 0.5f, Is.True, "le joueur ne doit pas rester dans la carrosserie");
                Assert.That(Vector3.Distance(carBefore, car.transform.position), Is.GreaterThan(1f), "la voiture continue après la sortie");
                yield return new WaitForSeconds(rig.blendDuration + 0.1f);
                Assert.That(rig.Mode, Is.EqualTo(CameraMode.OnFoot));
            }
            Assert.That(player.BoardingCount, Is.EqualTo(10));
            Assert.That(player.ExitCount, Is.EqualTo(10));
            Assert.That(ts.StallWarnings, Is.EqualTo(0));
            Assert.That(ts.MinimumObservedSpeed, Is.GreaterThanOrEqualTo(ts.Parameters.minSpeed * 0.9f));
        }

        /// <summary>An exit that is not allowed is refused with a message, the car keeps going, a later exit works.</summary>
        [UnityTest]
        public IEnumerator ImpossibleExitIsRefusedCleanly()
        {
            yield return LoadGame();
            var player = PlayerController.Current;
            var ts = TrafficSystem.Current;
            var messages = new List<string>();
            player.Notified += messages.Add;
            var car = ts.vehicles.First(v => v != null);
            player.Motor.Teleport(car.GetDoorEntry(car.KerbSide).position, car.transform.rotation);
            Assert.That(player.BeginBoarding(car, car.KerbSide), Is.True);
            yield return WaitFor(() => player.State == PlayerState.EnVoiture, 3f, "installation à bord");

            float saved = ts.boarding.maxExitSpeed;
            try
            {
                ts.boarding.maxExitSpeed = 0.5f; // any real speed is now "too fast"
                Vector3 before = car.transform.position;
                player.TryExit();
                yield return new WaitForSeconds(0.5f);
                Assert.That(player.State, Is.EqualTo(PlayerState.EnVoiture), "la sortie doit être refusée");
                Assert.That(messages, Is.Not.Empty, "le refus doit être expliqué au joueur");
                Assert.That(Vector3.Distance(before, car.transform.position), Is.GreaterThan(1f), "la voiture continue de rouler");
            }
            finally
            {
                ts.boarding.maxExitSpeed = saved;
            }
            yield return new WaitForSeconds(0.7f);
            player.TryExit();
            yield return WaitFor(() => player.State == PlayerState.APied, 6f, "sortie après refus");
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheWorldAndResumeRestoresIt()
        {
            yield return LoadGame();
            yield return new WaitForSeconds(1f);
            var session = GameSession.Current;
            Assert.That(session, Is.Not.Null);
            var car = TrafficSystem.Current.vehicles.First(v => v != null);

            session.SetPaused(true);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(GameInput.Instance.Context, Is.EqualTo(InputContext.Menu));
            Vector3 p0 = car.transform.position;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(p0, car.transform.position), Is.LessThan(1e-3f), "rien ne bouge en pause");

            session.SetPaused(false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameInput.Instance.Context, Is.EqualTo(InputContext.OnFoot));
            yield return new WaitForSeconds(0.6f);
            Assert.That(Vector3.Distance(p0, car.transform.position), Is.GreaterThan(1f), "le trafic repart après la pause");
        }

        static IEnumerator StartFight(PlayerController player, FightArena arena)
        {
            player.Motor.Teleport(arena.playerStart.position, arena.playerStart.rotation);
            yield return null;
            arena.BeginFight(player);
            yield return WaitFor(() => arena.State == FightArena.ArenaState.Fighting, 4f, "début du combat");
        }

        [UnityTest]
        public IEnumerator OpponentDefeatThenRestart()
        {
            yield return LoadGame();
            var player = PlayerController.Current;
            var arena = FightArena.Current;
            Assert.That(arena, Is.Not.Null);
            yield return StartFight(player, arena);
            var opponent = arena.OpponentFighter;
            for (int i = 0; i < 80 && !opponent.IsKnockedOut; i++)
            {
                opponent.ReceiveHit(player.Fighter, player.Fighter.hook);
                yield return new WaitForSeconds(0.1f);
            }
            Assert.That(opponent.IsKnockedOut, Is.True);
            yield return null;
            Assert.That(arena.State, Is.EqualTo(FightArena.ArenaState.Victory));
            Assert.That(arena.Victories, Is.EqualTo(1));
            Assert.That(TrafficSystem.Current.MinimumObservedSpeed, Is.GreaterThanOrEqualTo(TrafficSystem.Current.Parameters.minSpeed * 0.9f), "le combat n'arrête pas le trafic");

            yield return new WaitForSeconds(arena.restartDelay + 0.1f);
            arena.ResetFight(true);
            Assert.That(arena.State, Is.EqualTo(FightArena.ArenaState.Waiting));
            Assert.That(opponent.IsKnockedOut, Is.False);
            Assert.That(opponent.Health.Current, Is.EqualTo(opponent.maxHealth));
            yield return StartFight(player, arena);
            Assert.That(arena.State, Is.EqualTo(FightArena.ArenaState.Fighting), "le combat peut recommencer");
        }

        [UnityTest]
        public IEnumerator PlayerDefeatThenRestart()
        {
            yield return LoadGame();
            var player = PlayerController.Current;
            var arena = FightArena.Current;
            yield return StartFight(player, arena);
            var opponent = arena.OpponentFighter;
            opponent.GetComponent<OpponentBrain>().enabled = false;
            for (int i = 0; i < 80 && !player.Fighter.IsKnockedOut; i++)
            {
                player.Fighter.ReceiveHit(opponent, opponent.hook);
                yield return new WaitForSeconds(0.1f);
            }
            yield return null;
            Assert.That(arena.State, Is.EqualTo(FightArena.ArenaState.Defeat));
            Assert.That(player.State, Is.EqualTo(PlayerState.KO));
            yield return new WaitForSeconds(arena.restartDelay + 0.1f);
            arena.ResetFight(true);
            yield return WaitFor(() => player.State == PlayerState.APied, 4f, "le joueur se relève");
            Assert.That(player.Fighter.Health.Current, Is.EqualTo(player.Fighter.maxHealth));
        }

        /// <summary>Real attack flow: damage only within reach, only during the active window, once per punch, never through a wall.</summary>
        [UnityTest]
        public IEnumerator PunchesHitOnlyInRangeOnceAndNotThroughWalls()
        {
            yield return LoadGame();
            var player = PlayerController.Current;
            var arena = FightArena.Current;
            yield return StartFight(player, arena);
            var opponent = arena.OpponentFighter;
            opponent.GetComponent<OpponentBrain>().enabled = false;
            opponent.SetGuard(false);
            var oppMotor = opponent.GetComponent<CharacterMotor>();
            AttackDefinition started = null;
            player.Fighter.AttackStarted += (f, a) => started = a;
            Vector3 origin = arena.transform.position;
            Quaternion facing = Quaternion.LookRotation(Vector3.forward);

            IEnumerator Place(float distance)
            {
                player.Motor.Teleport(origin, facing);
                oppMotor.Teleport(origin + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back));
                player.Motor.ClearMotion();
                oppMotor.ClearMotion();
                yield return new WaitForSeconds(0.3f);
            }

            // Out of reach: nothing.
            yield return Place(3f);
            float before = opponent.Health.Current;
            Assert.That(player.Fighter.TryLightAttack(), Is.True);
            yield return new WaitForSeconds(0.8f);
            Assert.That(opponent.Health.Current, Is.EqualTo(before), "un coup hors de portée ne doit pas toucher");

            // In reach: exactly one hit of the jab's damage.
            yield return Place(1.0f);
            before = opponent.Health.Current;
            Assert.That(player.Fighter.TryLightAttack(), Is.True);
            yield return new WaitForSeconds(0.15f * 0.5f);
            Assert.That(opponent.Health.Current, Is.EqualTo(before), "pas de dégât pendant l'anticipation");
            yield return new WaitForSeconds(0.8f);
            Assert.That(started, Is.Not.Null);
            Assert.That(opponent.Health.Current, Is.EqualTo(before - started.damage).Within(0.01f), "un seul impact, avec les dégâts du coup lancé");

            // Same distance with a wall in between: no hit.
            yield return new WaitForSeconds(0.5f);
            yield return Place(1.2f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0f, 1.2f, 0.6f);
            wall.transform.localScale = new Vector3(3f, 2.4f, 0.1f);
            Physics.SyncTransforms();
            before = opponent.Health.Current;
            Assert.That(player.Fighter.HasLineOfSight(opponent), Is.False);
            player.Fighter.TryLightAttack();
            yield return new WaitForSeconds(0.8f);
            Assert.That(opponent.Health.Current, Is.EqualTo(before), "pas de coup à travers un mur");
            UnityEngine.Object.Destroy(wall);
        }
    }
}
