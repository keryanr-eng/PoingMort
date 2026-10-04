using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.Traffic
{
    /// <summary>Tuning of the autonomous traffic. Every speed is in metres per second.</summary>
    [Serializable]
    public class TrafficParameters
    {
        [Tooltip("Vitesse plancher : une voiture ne descend jamais en dessous (jamais d'arrêt).")]
        public float minSpeed = 2.6f;
        [Tooltip("Vitesse maximale autorisée (y compris pilotée par le joueur).")]
        public float maxSpeed = 21f;
        [Tooltip("Vitesse de croisière moyenne des voitures autonomes.")]
        public float cruiseSpeed = 10.5f;
        [Tooltip("Variation aléatoire de la vitesse de croisière (+/-).")]
        public float cruiseVariation = 1.5f;
        [Tooltip("Accélération maximale.")]
        public float acceleration = 2.4f;
        [Tooltip("Décélération confortable (anticipation).")]
        public float comfortableDeceleration = 3.2f;
        [Tooltip("Décélération maximale utilisée en cas d'urgence.")]
        public float maxDeceleration = 7.5f;
        [Tooltip("Temps inter-véhicules visé (secondes).")]
        public float timeHeadway = 1.3f;
        [Tooltip("Distance minimale pare-choc à pare-choc (m).")]
        public float minimumGap = 5f;
        [Tooltip("Accélération latérale tolérée en virage : fixe la vitesse en courbe.")]
        public float lateralAcceleration = 3.6f;
        [Tooltip("Distance d'anticipation des virages (m).")]
        public float curveLookAhead = 22f;
        [Tooltip("Durée d'un changement de voie (s).")]
        public float laneChangeDuration = 1.8f;
        [Tooltip("Espace libre requis devant dans la voie cible (m).")]
        public float laneChangeFrontGap = 12f;
        [Tooltip("Espace libre requis derrière dans la voie cible (m).")]
        public float laneChangeRearGap = 9f;
    }

    public enum LaneChangeDirection { Left = -1, Right = 1 }

    /// <summary>One vehicle in the simulation. Plain data so tests can inspect it.</summary>
    public sealed class TrafficAgent
    {
        public int Id;
        public int Lane;
        public float Distance;
        public float Speed;
        public float CruiseSpeed;
        public float Length = 4.9f;

        /// <summary>Speed requested by the player (if driven). Negative means "no request".</summary>
        public float PlayerTargetSpeed = -1f;

        /// <summary>Temporary cap (e.g. obstacle ahead, hail by a pedestrian). Reset each step by the owner.</summary>
        public float SpeedCap = float.MaxValue;

        public bool IsChangingLane;
        public int FromLane;
        public float LaneChangeProgress;   // 0..1
        public float FromLaneDistance;     // arc position on the lane being left

        public float DistanceTravelled;    // total distance, for diagnostics/tests

        public float LastAcceleration;
    }

    /// <summary>
    /// Arcade traffic model: every agent follows a closed lane at a speed that never drops below
    /// <see cref="TrafficParameters.minSpeed"/>. Spacing uses a simplified Intelligent Driver Model,
    /// curves reduce the target speed, lane changes only happen into safe gaps.
    /// </summary>
    public sealed class TrafficSimulation
    {
        readonly List<LaneLoop> m_Lanes = new List<LaneLoop>();
        readonly List<int> m_LeftNeighbour = new List<int>();
        readonly List<int> m_RightNeighbour = new List<int>();
        readonly List<TrafficAgent> m_Agents = new List<TrafficAgent>();

        public TrafficParameters Parameters { get; }
        public IReadOnlyList<TrafficAgent> Agents => m_Agents;
        public IReadOnlyList<LaneLoop> Lanes => m_Lanes;

        public TrafficSimulation(TrafficParameters parameters)
        {
            Parameters = parameters ?? new TrafficParameters();
        }

        public int AddLane(LaneLoop lane)
        {
            m_Lanes.Add(lane);
            m_LeftNeighbour.Add(-1);
            m_RightNeighbour.Add(-1);
            return m_Lanes.Count - 1;
        }

        /// <summary>Declares two parallel lanes travelling in the same direction.</summary>
        public void SetNeighbours(int leftLane, int rightLane)
        {
            m_RightNeighbour[leftLane] = rightLane;
            m_LeftNeighbour[rightLane] = leftLane;
        }

        public int GetNeighbour(int lane, LaneChangeDirection dir)
        {
            return dir == LaneChangeDirection.Left ? m_LeftNeighbour[lane] : m_RightNeighbour[lane];
        }

        public TrafficAgent AddAgent(int lane, float distance, float cruiseSpeed, float length = 4.9f)
        {
            var agent = new TrafficAgent
            {
                Id = m_Agents.Count,
                Lane = lane,
                Distance = m_Lanes[lane].Wrap(distance),
                CruiseSpeed = Mathf.Clamp(cruiseSpeed, Parameters.minSpeed, Parameters.maxSpeed),
                Length = length,
            };
            agent.Speed = agent.CruiseSpeed;
            m_Agents.Add(agent);
            return agent;
        }

        /// <summary>Is the agent currently occupying the lane (including while changing lanes)?</summary>
        static bool Occupies(TrafficAgent a, int lane)
        {
            return a.Lane == lane || (a.IsChangingLane && a.FromLane == lane);
        }

        float PositionOn(TrafficAgent a, int lane)
        {
            if (a.Lane == lane) return a.Distance;
            if (a.IsChangingLane && a.FromLane == lane) return a.FromLaneDistance;
            // Not on this lane: project (used for gap tests on neighbour lanes).
            Vector3 world = m_Lanes[a.Lane].Evaluate(a.Distance);
            return m_Lanes[lane].ClosestDistance(world);
        }

        /// <summary>
        /// Finds the closest agent ahead on <paramref name="lane"/> from arc position <paramref name="distance"/>.
        /// Returns the bumper-to-bumper gap (may be negative when overlapping).
        /// </summary>
        public TrafficAgent FindLeader(int lane, float distance, TrafficAgent self, float selfLength, out float gap)
        {
            TrafficAgent best = null;
            float bestDelta = float.MaxValue;
            var loop = m_Lanes[lane];
            foreach (var other in m_Agents)
            {
                if (other == self || !Occupies(other, lane)) continue;
                float delta = loop.ForwardDelta(distance, PositionOn(other, lane));
                if (delta < 0.01f) delta += loop.Length; // same spot: treat as a full lap away
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = other;
                }
            }
            gap = best == null ? float.MaxValue : bestDelta - 0.5f * (selfLength + best.Length);
            return best;
        }

        /// <summary>Closest agent behind on <paramref name="lane"/>.</summary>
        public TrafficAgent FindFollower(int lane, float distance, TrafficAgent self, float selfLength, out float gap)
        {
            TrafficAgent best = null;
            float bestDelta = float.MaxValue;
            var loop = m_Lanes[lane];
            foreach (var other in m_Agents)
            {
                if (other == self || !Occupies(other, lane)) continue;
                float delta = loop.ForwardDelta(PositionOn(other, lane), distance);
                if (delta < 0.01f) delta += loop.Length;
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = other;
                }
            }
            gap = best == null ? float.MaxValue : bestDelta - 0.5f * (selfLength + best.Length);
            return best;
        }

        /// <summary>Target speed allowed by the curvature of the lane ahead.</summary>
        public float CurveSpeedLimit(int lane, float distance)
        {
            float k = m_Lanes[lane].MaxCurvatureAhead(distance, Parameters.curveLookAhead);
            if (k < 1e-4f) return Parameters.maxSpeed;
            return Mathf.Sqrt(Parameters.lateralAcceleration / k);
        }

        public float DesiredSpeed(TrafficAgent a)
        {
            float desired = a.PlayerTargetSpeed >= 0f ? a.PlayerTargetSpeed : a.CruiseSpeed;
            desired = Mathf.Min(desired, CurveSpeedLimit(a.Lane, a.Distance));
            desired = Mathf.Min(desired, a.SpeedCap);
            return Mathf.Clamp(desired, Parameters.minSpeed, Parameters.maxSpeed);
        }

        /// <summary>Checks whether <paramref name="a"/> may move into the neighbouring lane right now.</summary>
        public bool CanChangeLane(TrafficAgent a, LaneChangeDirection dir, out int targetLane)
        {
            targetLane = -1;
            if (a.IsChangingLane) return false;
            int lane = GetNeighbour(a.Lane, dir);
            if (lane < 0) return false;
            float pos = PositionOn(a, lane);
            var leader = FindLeader(lane, pos, a, a.Length, out float frontGap);
            var follower = FindFollower(lane, pos, a, a.Length, out float rearGap);
            float needFront = Parameters.laneChangeFrontGap + a.Speed * 0.6f;
            if (leader != null && frontGap < needFront) return false;
            if (follower != null)
            {
                float closing = Mathf.Max(0f, follower.Speed - a.Speed);
                if (rearGap < Parameters.laneChangeRearGap + closing * 2f) return false;
            }
            targetLane = lane;
            return true;
        }

        public bool RequestLaneChange(TrafficAgent a, LaneChangeDirection dir)
        {
            if (!CanChangeLane(a, dir, out int target)) return false;
            a.FromLane = a.Lane;
            a.FromLaneDistance = a.Distance;
            a.Distance = PositionOn(a, target);
            a.Lane = target;
            a.IsChangingLane = true;
            a.LaneChangeProgress = 0f;
            return true;
        }

        /// <summary>World position and forward direction of an agent, including lane change blending.</summary>
        public void GetPose(TrafficAgent a, out Vector3 position, out Vector3 forward)
        {
            var lane = m_Lanes[a.Lane];
            lane.Evaluate(a.Distance, out position, out _);
            forward = lane.SmoothTangent(a.Distance, 3f);
            if (a.IsChangingLane)
            {
                var from = m_Lanes[a.FromLane];
                Vector3 fromPos = from.Evaluate(a.FromLaneDistance);
                Vector3 fromFwd = from.SmoothTangent(a.FromLaneDistance, 3f);
                float t = a.LaneChangeProgress;
                float s = t * t * (3f - 2f * t);
                position = Vector3.Lerp(fromPos, position, s);
                // Heading tilts toward the destination lane during the manoeuvre.
                Vector3 lateral = (position - fromPos);
                float yawBias = Mathf.Sin(t * Mathf.PI) * 0.12f;
                Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
                float sign = Mathf.Sign(Vector3.Dot(lateral, side));
                forward = (Vector3.Lerp(fromFwd, forward, s) + side * (sign * yawBias)).normalized;
            }
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            var p = Parameters;

            // 1) Compute accelerations from the current state (so the order of agents does not matter).
            foreach (var a in m_Agents)
            {
                float desired = DesiredSpeed(a);
                TrafficAgent leaderAgent = FindLeader(a.Lane, a.Distance, a, a.Length, out float gap);
                float leaderSpeed = leaderAgent != null ? leaderAgent.Speed : desired;
                if (a.IsChangingLane)
                {
                    // Keep watching the lane being left until the manoeuvre is complete.
                    var fromLeader = FindLeader(a.FromLane, a.FromLaneDistance, a, a.Length, out float gapFrom);
                    if (fromLeader != null && gapFrom < gap)
                    {
                        gap = gapFrom;
                        leaderSpeed = fromLeader.Speed;
                    }
                }

                float v = a.Speed;
                float free = 1f - Mathf.Pow(v / Mathf.Max(desired, 0.1f), 4f);
                float interaction = 0f;
                if (gap < 200f)
                {
                    float dv = v - leaderSpeed;
                    float sStar = p.minimumGap + Mathf.Max(0f, v * p.timeHeadway + v * dv / (2f * Mathf.Sqrt(p.acceleration * p.comfortableDeceleration)));
                    float s = Mathf.Max(gap, 0.1f);
                    interaction = (sStar / s) * (sStar / s);
                }
                float acc = p.acceleration * (free - interaction);
                // Above the desired speed (curve ahead, cap) decelerate smoothly but firmly.
                if (v > desired) acc = Mathf.Min(acc, -p.comfortableDeceleration * Mathf.Clamp01((v - desired) / 2f));
                a.LastAcceleration = Mathf.Clamp(acc, -p.maxDeceleration, p.acceleration);
            }

            // 2) Integrate.
            foreach (var a in m_Agents)
            {
                a.Speed = Mathf.Clamp(a.Speed + a.LastAcceleration * dt, p.minSpeed, p.maxSpeed);
                float advance = a.Speed * dt;
                a.Distance = m_Lanes[a.Lane].Wrap(a.Distance + advance);
                a.DistanceTravelled += advance;
                if (a.IsChangingLane)
                {
                    a.FromLaneDistance = m_Lanes[a.FromLane].Wrap(a.FromLaneDistance + advance);
                    a.LaneChangeProgress += dt / Mathf.Max(0.2f, p.laneChangeDuration);
                    if (a.LaneChangeProgress >= 1f)
                    {
                        a.IsChangingLane = false;
                        a.LaneChangeProgress = 0f;
                        a.FromLane = a.Lane;
                    }
                }
            }
        }

        /// <summary>Smallest bumper-to-bumper gap between any two agents sharing a lane (diagnostics/tests).</summary>
        public float SmallestGap()
        {
            float min = float.MaxValue;
            foreach (var a in m_Agents)
            {
                FindLeader(a.Lane, a.Distance, a, a.Length, out float gap);
                if (gap < min) min = gap;
            }
            return min;
        }
    }
}
