using NUnit.Framework;
using PoingMort.Combat;
using PoingMort.Player;
using PoingMort.Vehicles;
using UnityEngine;

namespace PoingMort.Tests
{
    public class CombatLogicTests
    {
        [Test]
        public void TimelineGoesThroughAnticipationContactRecovery()
        {
            var jab = AttackDefinition.Jab();
            var tl = new AttackTimeline();
            tl.Start(jab);
            Assert.That(tl.Phase, Is.EqualTo(AttackPhase.Startup));
            tl.Advance(jab.startup + 0.01f);
            Assert.That(tl.Phase, Is.EqualTo(AttackPhase.Active));
            tl.Advance(jab.active);
            Assert.That(tl.Phase, Is.EqualTo(AttackPhase.Recovery));
            tl.Advance(jab.recovery);
            Assert.That(tl.IsRunning, Is.False);
        }

        [Test]
        public void ATargetIsHitOnlyOncePerPunch()
        {
            var tl = new AttackTimeline();
            tl.Start(AttackDefinition.Cross());
            var target = new object();
            Assert.That(tl.TryRegisterHit(target), Is.False, "Pas de dégâts pendant l'anticipation.");
            tl.Advance(tl.Attack.startup + 0.01f);
            Assert.That(tl.TryRegisterHit(target), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(tl.TryRegisterHit(target), Is.False);
            tl.Advance(tl.Attack.active);
            Assert.That(tl.TryRegisterHit(new object()), Is.False, "Pas de dégâts pendant la récupération.");
        }

        [Test]
        public void ANewPunchCanHitAgain()
        {
            var tl = new AttackTimeline();
            var target = new object();
            for (int i = 0; i < 2; i++)
            {
                tl.Start(AttackDefinition.Jab());
                tl.Advance(tl.Attack.startup + 0.01f);
                Assert.That(tl.TryRegisterHit(target), Is.True);
                tl.Advance(1f);
            }
        }

        [Test]
        public void StrikeZoneRespectsReachAndAngle()
        {
            var jab = AttackDefinition.Jab();
            Vector3 a = Vector3.zero;
            Vector3 fwd = Vector3.forward;
            Assert.That(HitGeometry.InStrikeZone(a, fwd, new Vector3(0f, 0f, 1.0f), jab.reach, 0.32f, jab.maxAngle), Is.True);
            Assert.That(HitGeometry.InStrikeZone(a, fwd, new Vector3(0f, 0f, 1.6f), jab.reach, 0.32f, jab.maxAngle), Is.False, "Trop loin.");
            Assert.That(HitGeometry.InStrikeZone(a, fwd, new Vector3(0f, 0f, -1.0f), jab.reach, 0.32f, jab.maxAngle), Is.False, "Derrière.");
            Assert.That(HitGeometry.InStrikeZone(a, fwd, new Vector3(1.0f, 0f, 0.2f), jab.reach, 0.32f, jab.maxAngle), Is.False, "Sur le côté.");
            Assert.That(HitGeometry.InStrikeZone(a, fwd, new Vector3(0f, 1.5f, 1.0f), jab.reach, 0.32f, jab.maxAngle), Is.True, "La hauteur n'entre pas en compte.");
        }

        [Test]
        public void GuardOnlyReducesFrontalHits()
        {
            var hook = AttackDefinition.Hook();
            Assert.That(HitGeometry.ResolveDamage(hook, true, true), Is.EqualTo(hook.damage * hook.guardDamageFactor).Within(1e-4f));
            Assert.That(HitGeometry.ResolveDamage(hook, true, false), Is.EqualTo(hook.damage));
            Assert.That(HitGeometry.ResolveDamage(hook, false, true), Is.EqualTo(hook.damage));
            Assert.That(HitGeometry.IsFrontal(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f)), Is.True);
            Assert.That(HitGeometry.IsFrontal(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -2f)), Is.False);
        }

        [Test]
        public void ChainWindowOpensDuringRecovery()
        {
            var jab = AttackDefinition.Jab();
            var tl = new AttackTimeline();
            tl.Start(jab);
            Assert.That(tl.CanChain, Is.False);
            tl.Advance(jab.startup + jab.active + jab.recovery * jab.chainFrom + 0.005f);
            Assert.That(tl.CanChain, Is.True);
        }

        [Test]
        public void HealthClampsAndDetectsKnockOut()
        {
            var h = new HealthPool(100f);
            Assert.That(h.Damage(30f), Is.EqualTo(30f));
            Assert.That(h.Damage(200f), Is.EqualTo(70f));
            Assert.That(h.IsDepleted, Is.True);
            Assert.That(h.Damage(10f), Is.EqualTo(0f));
            h.Reset();
            Assert.That(h.Normalized, Is.EqualTo(1f));
        }

        [Test]
        public void PresetTimingsAreConsistent()
        {
            foreach (var a in new[] { AttackDefinition.Jab(), AttackDefinition.Cross(), AttackDefinition.Hook() })
            {
                Assert.That(a.startup, Is.GreaterThan(0.05f), a.id);
                Assert.That(a.active, Is.GreaterThan(0.03f), a.id);
                Assert.That(a.recovery, Is.GreaterThan(a.active), a.id);
                Assert.That(a.reach, Is.InRange(0.7f, 1.4f), a.id);
            }
            Assert.That(AttackDefinition.Hook().damage, Is.GreaterThan(AttackDefinition.Cross().damage));
        }
    }

    public class BoardingRulesTests
    {
        readonly BoardingParameters m_P = new BoardingParameters();

        [Test]
        public void BoardingNeedsProximityMatchingSpeedAndAFreeSide()
        {
            Assert.That(BoardingRules.CanBoard(1.0f, 1.0f, true, false, false, m_P), Is.EqualTo(BoardingRefusal.None));
            Assert.That(BoardingRules.CanBoard(4.0f, 1.0f, true, false, false, m_P), Is.EqualTo(BoardingRefusal.TooFar));
            Assert.That(BoardingRules.CanBoard(1.0f, 9.0f, true, false, false, m_P), Is.EqualTo(BoardingRefusal.TooFast));
            Assert.That(BoardingRules.CanBoard(1.0f, 1.0f, false, false, false, m_P), Is.EqualTo(BoardingRefusal.SideBlocked));
            Assert.That(BoardingRules.CanBoard(1.0f, 1.0f, true, true, false, m_P), Is.EqualTo(BoardingRefusal.Occupied));
            Assert.That(BoardingRules.CanBoard(1.0f, 1.0f, true, false, true, m_P), Is.EqualTo(BoardingRefusal.Busy));
        }

        [Test]
        public void HailedSpeedStaysPositiveAndCatchable()
        {
            Assert.That(m_P.hailedSpeed, Is.GreaterThan(0f), "Une voiture hélée ne s'arrête jamais.");
            Assert.That(m_P.hailedSpeed - 6.7f, Is.LessThan(m_P.maxRelativeSpeed), "Un sprint permet de rattraper une voiture hélée.");
        }

        [Test]
        public void ExitUsesTheOtherSideOrIsRefused()
        {
            Assert.That(BoardingRules.CanExit(8f, true, true, VehicleSide.Right, false, m_P, out var side), Is.EqualTo(ExitRefusal.None));
            Assert.That(side, Is.EqualTo(VehicleSide.Right));
            Assert.That(BoardingRules.CanExit(8f, false, true, VehicleSide.Right, false, m_P, out side), Is.EqualTo(ExitRefusal.None));
            Assert.That(side, Is.EqualTo(VehicleSide.Left));
            Assert.That(BoardingRules.CanExit(8f, false, false, VehicleSide.Right, false, m_P, out _), Is.EqualTo(ExitRefusal.Blocked));
            Assert.That(BoardingRules.CanExit(m_P.maxExitSpeed + 1f, true, true, VehicleSide.Right, false, m_P, out _), Is.EqualTo(ExitRefusal.TooFast));
        }

        [Test]
        public void FastExitsEndInAFall()
        {
            Assert.That(BoardingRules.ExitEndsInRoll(m_P.rollExitSpeed + 0.5f, m_P), Is.True);
            Assert.That(BoardingRules.ExitEndsInRoll(m_P.rollExitSpeed - 0.5f, m_P), Is.False);
        }

        [Test]
        public void PathsStartWhereTheCharacterIsAndEndAtTheTarget()
        {
            Vector3 start = new Vector3(1.6f, 0f, -0.4f), door = new Vector3(1.3f, 0f, 0.2f), seat = new Vector3(0.38f, 0.3f, 0.05f);
            Assert.That(Vector3.Distance(BoardingRules.BoardingPath(start, door, seat, 0f), start), Is.LessThan(1e-4f), "Pas de téléportation au départ.");
            Assert.That(Vector3.Distance(BoardingRules.BoardingPath(start, door, seat, 1f), seat), Is.LessThan(1e-4f));
            Vector3 exit = new Vector3(1.6f, 0f, 0.2f);
            Assert.That(Vector3.Distance(BoardingRules.ExitPath(seat, door, exit, 0f), seat), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(BoardingRules.ExitPath(seat, door, exit, 1f), exit), Is.LessThan(1e-4f));
            // Continuity: no jump larger than 10 cm between samples 1/60 apart.
            Vector3 prev = start;
            for (int i = 1; i <= 60; i++)
            {
                Vector3 p = BoardingRules.BoardingPath(start, door, seat, i / 60f);
                Assert.That(Vector3.Distance(prev, p), Is.LessThan(0.1f));
                prev = p;
            }
        }
    }

    public class PlayerStateMachineTests
    {
        [Test]
        public void TenEnterExitCyclesEndOnFoot()
        {
            var sm = new PlayerStateMachine();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(sm.TryEnter(PlayerState.Embarquement), Is.True);
                Assert.That(sm.TryEnter(PlayerState.EnVoiture), Is.True);
                Assert.That(sm.TryEnter(PlayerState.Sortie), Is.True);
                Assert.That(sm.TryEnter(i % 3 == 0 ? PlayerState.Chute : PlayerState.APied), Is.True);
                if (sm.State == PlayerState.Chute) Assert.That(sm.TryEnter(PlayerState.APied), Is.True);
                Assert.That(sm.State, Is.EqualTo(PlayerState.APied));
            }
        }

        [Test]
        public void ImpossibleTransitionsAreRefused()
        {
            var sm = new PlayerStateMachine();
            Assert.That(sm.TryEnter(PlayerState.EnVoiture), Is.False, "On ne s'assoit pas sans embarquer.");
            Assert.That(sm.TryEnter(PlayerState.Sortie), Is.False);
            Assert.That(sm.TryEnter(PlayerState.Embarquement), Is.True);
            Assert.That(sm.TryEnter(PlayerState.Chute), Is.False, "Pas de chute pendant l'embarquement.");
            Assert.That(sm.TryEnter(PlayerState.EnVoiture), Is.True);
            Assert.That(sm.TryEnter(PlayerState.APied), Is.False, "Sortir passe par l'état Sortie.");
            Assert.That(sm.TryEnter(PlayerState.KO), Is.False, "Pas de K.O. assis dans une voiture.");
            Assert.That(sm.State, Is.EqualTo(PlayerState.EnVoiture));
        }

        [Test]
        public void ForceResetAlwaysReturnsOnFoot()
        {
            var sm = new PlayerStateMachine();
            sm.TryEnter(PlayerState.KO);
            sm.ForceReset();
            Assert.That(sm.State, Is.EqualTo(PlayerState.APied));
        }
    }
}
