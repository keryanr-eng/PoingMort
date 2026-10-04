using System.Collections.Generic;
using NUnit.Framework;
using PoingMort.Traffic;
using UnityEngine;

namespace PoingMort.Tests
{
    public class LaneLoopTests
    {
        [Test]
        public void RoundedRectangleLengthMatchesGeometry()
        {
            float hx = 50f, hz = 20f, r = 10f;
            var lane = new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, hx, hz, r, 24, false));
            float expected = 2f * (2f * hx - 2f * r) + 2f * (2f * hz - 2f * r) + 2f * Mathf.PI * r;
            Assert.That(lane.Length, Is.EqualTo(expected).Within(0.5f));
        }

        [Test]
        public void EvaluateWrapsAroundTheLoop()
        {
            var lane = new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 30f, 15f, 6f, 12, false));
            Vector3 a = lane.Evaluate(5f);
            Vector3 b = lane.Evaluate(5f + lane.Length);
            Vector3 c = lane.Evaluate(5f - lane.Length);
            Assert.That(Vector3.Distance(a, b), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(a, c), Is.LessThan(1e-3f));
        }

        [Test]
        public void ClosestDistanceRoundTrips()
        {
            var lane = new LaneLoop(LaneLoop.RoundedRectangle(new Vector3(3f, 0f, -2f), 40f, 18f, 8f, 16, true));
            for (float d = 0f; d < lane.Length; d += 7.3f)
            {
                Vector3 p = lane.Evaluate(d);
                float back = lane.ClosestDistance(p, out float planar);
                Assert.That(planar, Is.LessThan(0.01f));
                Assert.That(Mathf.Min(lane.ForwardDelta(d, back), lane.ForwardDelta(back, d)), Is.LessThan(0.05f));
            }
        }

        [Test]
        public void CurvatureIsHigherInCorners()
        {
            var lane = new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 50f, 20f, 10f, 24, false));
            // Distance 0 is the start of the first corner arc (bottom right), straight sections follow.
            float straight = lane.Curvature(lane.Length * 0.5f - 25f);
            float corner = 0f;
            for (float d = 0f; d < lane.Length; d += 1f) corner = Mathf.Max(corner, lane.Curvature(d));
            Assert.That(corner, Is.GreaterThan(0.07f));
            Assert.That(corner, Is.GreaterThan(straight * 3f + 0.01f));
        }
    }

    public class TrafficSimulationTests
    {
        static TrafficSimulation BuildTwoLaneLoop(int carsPerLane, out int inner, out int outer)
        {
            var sim = new TrafficSimulation(new TrafficParameters());
            // Clockwise travel: the inner lane is on the right of the direction of travel.
            inner = sim.AddLane(new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 60f, 26f, 11f, 16, true)));
            outer = sim.AddLane(new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 63.4f, 29.4f, 14.4f, 16, true)));
            sim.SetNeighbours(outer, inner);
            var rng = new System.Random(3);
            for (int i = 0; i < carsPerLane; i++)
            {
                sim.AddAgent(inner, sim.Lanes[inner].Length * i / carsPerLane, 10f + (float)rng.NextDouble() * 2f);
                sim.AddAgent(outer, sim.Lanes[outer].Length * (i + 0.5f) / carsPerLane, 9f + (float)rng.NextDouble() * 2f);
            }
            return sim;
        }

        [Test]
        public void VehiclesNeverStopAndAlwaysAdvance()
        {
            var sim = BuildTwoLaneLoop(3, out _, out _);
            var p = sim.Parameters;
            var lastTravelled = new float[sim.Agents.Count];
            const float dt = 1f / 50f;
            int steps = (int)(10f * 60f / dt); // 10 simulated minutes
            for (int s = 1; s <= steps; s++)
            {
                sim.Step(dt);
                foreach (var a in sim.Agents)
                    Assert.That(a.Speed, Is.GreaterThanOrEqualTo(p.minSpeed - 1e-4f), "Une voiture est descendue sous la vitesse minimale.");
                if (s % 50 == 0) // every simulated second
                {
                    for (int i = 0; i < sim.Agents.Count; i++)
                    {
                        float advanced = sim.Agents[i].DistanceTravelled - lastTravelled[i];
                        Assert.That(advanced, Is.GreaterThanOrEqualTo(p.minSpeed * 0.99f), "Une voiture n'a pas réellement avancé pendant une seconde.");
                        lastTravelled[i] = sim.Agents[i].DistanceTravelled;
                    }
                }
            }
        }

        [Test]
        public void SpacingIsKeptWithHailsLaneChangesAndPedestrians()
        {
            var sim = BuildTwoLaneLoop(3, out _, out _);
            var rng = new System.Random(11);
            const float dt = 1f / 50f;
            float minGap = float.MaxValue;
            float hailUntil = -1f;
            int hailed = 0;
            for (int s = 0; s < 50 * 600; s++) // 10 simulated minutes
            {
                float time = s * dt;
                foreach (var a in sim.Agents) a.SpeedCap = float.MaxValue;
                // A pedestrian hails a car from time to time (the car slows, never stops).
                if (time > hailUntil + 6f && rng.NextDouble() < 0.01)
                {
                    hailed = rng.Next(sim.Agents.Count);
                    hailUntil = time + 7f;
                }
                if (time < hailUntil) sim.Agents[hailed].SpeedCap = 4.2f;
                // Someone stands on the road ahead of car 0 from time to time.
                if ((s / 500) % 7 == 3) sim.Agents[0].SpeedCap = sim.Parameters.minSpeed;
                // Random lane change requests (as the player or overtaking AI would do).
                if (rng.NextDouble() < 0.02)
                {
                    var a = sim.Agents[rng.Next(sim.Agents.Count)];
                    sim.RequestLaneChange(a, rng.NextDouble() < 0.5 ? LaneChangeDirection.Left : LaneChangeDirection.Right);
                }
                sim.Step(dt);
                minGap = Mathf.Min(minGap, sim.SmallestGap());
            }
            Assert.That(minGap, Is.GreaterThan(1.5f), "Deux voitures se sont touchées.");
        }

        [Test]
        public void PlayerCannotBrakeToAStop()
        {
            var sim = BuildTwoLaneLoop(1, out int inner, out _);
            var car = sim.Agents[0];
            car.PlayerTargetSpeed = 0f; // the player holds "slow down" forever
            for (int s = 0; s < 50 * 60; s++) sim.Step(1f / 50f);
            Assert.That(car.Speed, Is.EqualTo(sim.Parameters.minSpeed).Within(0.01f));
            Assert.That(car.Speed, Is.GreaterThan(0f));
        }

        [Test]
        public void LaneChangeIsRefusedIntoAnOccupiedGap()
        {
            var sim = new TrafficSimulation(new TrafficParameters());
            int right = sim.AddLane(new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 60f, 26f, 11f, 16, true)));
            int left = sim.AddLane(new LaneLoop(LaneLoop.RoundedRectangle(Vector3.zero, 63.4f, 29.4f, 14.4f, 16, true)));
            sim.SetNeighbours(left, right);
            var a = sim.AddAgent(right, 30f, 10f);
            Vector3 world = sim.Lanes[right].Evaluate(30f);
            sim.AddAgent(left, sim.Lanes[left].ClosestDistance(world) + 2f, 10f); // alongside
            Assert.That(sim.RequestLaneChange(a, LaneChangeDirection.Left), Is.False);
            Assert.That(sim.RequestLaneChange(a, LaneChangeDirection.Right), Is.False, "Pas de voie à droite.");
        }

        [Test]
        public void LaneChangeCompletesOnAFreeLane()
        {
            var sim = BuildTwoLaneLoop(1, out int inner, out int outer);
            var a = sim.Agents[0];
            Assert.That(a.Lane, Is.EqualTo(inner));
            Assert.That(sim.RequestLaneChange(a, LaneChangeDirection.Left), Is.True);
            for (int s = 0; s < 50 * 3; s++) sim.Step(1f / 50f);
            Assert.That(a.Lane, Is.EqualTo(outer));
            Assert.That(a.IsChangingLane, Is.False);
        }

        [Test]
        public void CarsSlowDownInCorners()
        {
            var sim = BuildTwoLaneLoop(1, out int inner, out _);
            float straightLimit = 0f, cornerLimit = float.MaxValue;
            var lane = sim.Lanes[inner];
            for (float d = 0f; d < lane.Length; d += 1f)
            {
                float limit = sim.CurveSpeedLimit(inner, d);
                if (lane.Curvature(d) > 0.05f) cornerLimit = Mathf.Min(cornerLimit, limit);
                straightLimit = Mathf.Max(straightLimit, limit);
            }
            Assert.That(cornerLimit, Is.LessThan(9f));
            Assert.That(cornerLimit, Is.GreaterThan(sim.Parameters.minSpeed));
            Assert.That(straightLimit, Is.GreaterThan(cornerLimit));
        }

        [Test]
        public void PosesStayOnTheLaneAndFaceForward()
        {
            var sim = BuildTwoLaneLoop(2, out _, out _);
            for (int s = 0; s < 500; s++)
            {
                sim.Step(1f / 50f);
                foreach (var a in sim.Agents)
                {
                    sim.GetPose(a, out Vector3 pos, out Vector3 fwd);
                    Assert.That(fwd.magnitude, Is.EqualTo(1f).Within(0.01f));
                    Assert.That(float.IsNaN(pos.x) || float.IsNaN(pos.z), Is.False);
                }
            }
        }
    }
}
