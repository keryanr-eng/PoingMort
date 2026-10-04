using System.Collections.Generic;
using PoingMort.Characters;
using PoingMort.Vehicles;
using UnityEngine;

namespace PoingMort.Traffic
{
    /// <summary>
    /// Runs the autonomous traffic of the scene. Cars are kinematic path followers: they never stop,
    /// keep their distance, slow down in curves and near pedestrians (but stay above the minimum speed),
    /// and push away anyone standing on the road. A watchdog measures the real world displacement.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TrafficSystem : MonoBehaviour
    {
        public static TrafficSystem Current { get; private set; }

        public TrafficParameters parameters = new TrafficParameters();
        public BoardingParameters boarding = new BoardingParameters();
        public List<TrafficLane> lanes = new List<TrafficLane>();
        [Tooltip("Voitures gérées (placées par le constructeur de scène).")]
        public List<TrafficVehicle> vehicles = new List<TrafficVehicle>();
        public int randomSeed = 7;

        [Header("Piétons sur la chaussée")]
        [Tooltip("Distance d'anticipation d'un piéton devant une voiture (m).")]
        public float pedestrianLookAhead = 22f;
        [Tooltip("Demi-largeur du couloir surveillé devant la voiture (m).")]
        public float pedestrianCorridor = 1.5f;
        public float knockSpeedFactor = 0.75f;
        public float knockDamage = 12f;
        [Tooltip("Couches des personnages pouvant être bousculés par les voitures.")]
        public LayerMask characterMask = ~0;

        [Header("Vie du trafic")]
        [Tooltip("Probabilité par seconde qu'une voiture autonome tente un dépassement si elle est gênée.")]
        public float overtakeChance = 0.35f;

        TrafficSimulation m_Sim;
        readonly Dictionary<TrafficAgent, TrafficVehicle> m_VehicleByAgent = new Dictionary<TrafficAgent, TrafficVehicle>();
        readonly Dictionary<Object, float> m_LastKnock = new Dictionary<Object, float>();
        readonly List<CharacterMotor> m_Pedestrians = new List<CharacterMotor>();
        readonly Collider[] m_Overlap = new Collider[16];
        System.Random m_Random;

        // Watchdog: real displacement over a 1 s window.
        readonly Dictionary<TrafficVehicle, Vector3> m_WatchStart = new Dictionary<TrafficVehicle, Vector3>();
        float m_WatchTimer;

        public TrafficSimulation Simulation => m_Sim;
        public TrafficParameters Parameters => parameters;
        /// <summary>Lowest real speed (m/s) observed over 1 s windows since start (diagnostics/tests).</summary>
        public float MinimumObservedSpeed { get; private set; } = float.MaxValue;
        public int StallWarnings { get; private set; }

        void Awake()
        {
            Current = this;
            m_Random = new System.Random(randomSeed);
            Build();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public TrafficLane GetLane(int index) => index >= 0 && index < lanes.Count ? lanes[index] : null;

        void Build()
        {
            m_Sim = new TrafficSimulation(parameters);
            var index = new Dictionary<TrafficLane, int>();
            foreach (var lane in lanes)
            {
                if (lane == null || lane.Loop == null) continue;
                index[lane] = m_Sim.AddLane(lane.Loop);
            }
            foreach (var lane in lanes)
            {
                if (lane == null || !index.ContainsKey(lane)) continue;
                if (lane.rightNeighbour != null && index.ContainsKey(lane.rightNeighbour))
                    m_Sim.SetNeighbours(index[lane], index[lane.rightNeighbour]);
            }

            foreach (var v in vehicles)
            {
                if (v == null) continue;
                // Attach each placed car to the closest lane at its current position.
                int bestLane = -1;
                float bestDist = float.MaxValue, bestArc = 0f;
                foreach (var pair in index)
                {
                    float arc = pair.Key.Loop.ClosestDistance(v.transform.position, out float planar);
                    if (planar < bestDist)
                    {
                        bestDist = planar;
                        bestLane = pair.Value;
                        bestArc = arc;
                    }
                }
                if (bestLane < 0) continue;
                float cruise = parameters.cruiseSpeed + (float)(m_Random.NextDouble() * 2.0 - 1.0) * parameters.cruiseVariation;
                var agent = m_Sim.AddAgent(bestLane, bestArc, cruise, v.length);
                v.Agent = agent;
                v.System = this;
                m_VehicleByAgent[agent] = v;
                m_WatchStart[v] = v.transform.position;
            }
            ApplyPoses(0f);
        }

        public void RegisterPedestrian(CharacterMotor motor)
        {
            if (motor != null && !m_Pedestrians.Contains(motor)) m_Pedestrians.Add(motor);
        }

        public void UnregisterPedestrian(CharacterMotor motor) => m_Pedestrians.Remove(motor);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Sim == null) return; // explicit pause (timeScale 0) is the only way to suspend traffic

            UpdateCaps(dt);
            m_Sim.Step(dt);
            ApplyPoses(dt);
            Physics.SyncTransforms();
            KnockPedestrians();
            Watchdog(dt);
        }

        void UpdateCaps(float dt)
        {
            foreach (var pair in m_VehicleByAgent)
            {
                var agent = pair.Key;
                var vehicle = pair.Value;
                float cap = float.MaxValue;

                if (vehicle.IsHailed || (vehicle.IsOccupied && !vehicle.IsPlayerDriven))
                    cap = Mathf.Min(cap, boarding.hailedSpeed);

                // Anticipate pedestrians on the road ahead: slow down to the minimum, honk, change lane if possible.
                if (PedestrianAhead(vehicle, out float distanceAhead))
                {
                    cap = Mathf.Min(cap, Mathf.Lerp(parameters.minSpeed, parameters.cruiseSpeed * 0.5f, Mathf.InverseLerp(4f, pedestrianLookAhead, distanceAhead)));
                    if (distanceAhead < 14f) vehicle.Honk();
                    if (!vehicle.IsPlayerDriven && !agent.IsChangingLane && distanceAhead < 16f)
                    {
                        if (!m_Sim.RequestLaneChange(agent, LaneChangeDirection.Left))
                            m_Sim.RequestLaneChange(agent, LaneChangeDirection.Right);
                    }
                }
                agent.SpeedCap = cap;

                // Occasional overtaking by autonomous cars stuck behind a slower one.
                if (!vehicle.IsPlayerDriven && !vehicle.IsOccupied && !agent.IsChangingLane)
                {
                    var leader = m_Sim.FindLeader(agent.Lane, agent.Distance, agent, agent.Length, out float gap);
                    if (leader != null && gap < 25f && leader.Speed < agent.CruiseSpeed - 1.5f && m_Random.NextDouble() < overtakeChance * dt)
                    {
                        if (!m_Sim.RequestLaneChange(agent, LaneChangeDirection.Left))
                            m_Sim.RequestLaneChange(agent, LaneChangeDirection.Right);
                    }
                }
            }
        }

        bool PedestrianAhead(TrafficVehicle vehicle, out float distanceAhead)
        {
            distanceAhead = float.MaxValue;
            Transform t = vehicle.transform;
            foreach (var ped in m_Pedestrians)
            {
                if (ped == null || !ped.isActiveAndEnabled || ped.IsDriven) continue;
                Vector3 local = t.InverseTransformPoint(ped.transform.position);
                if (local.z < vehicle.length * 0.4f || local.z > pedestrianLookAhead) continue;
                if (Mathf.Abs(local.x) > pedestrianCorridor) continue;
                if (local.z < distanceAhead) distanceAhead = local.z;
            }
            return distanceAhead < float.MaxValue;
        }

        void ApplyPoses(float dt)
        {
            foreach (var pair in m_VehicleByAgent)
            {
                var agent = pair.Key;
                var vehicle = pair.Value;
                m_Sim.GetPose(agent, out Vector3 pos, out Vector3 fwd);
                Quaternion previous = vehicle.transform.rotation;
                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
                vehicle.transform.SetPositionAndRotation(pos, rot);
                float yawRate = dt > 0f ? Mathf.DeltaAngle(previous.eulerAngles.y, rot.eulerAngles.y) / dt : 0f;
                vehicle.AfterMove(dt, yawRate);
            }
        }

        void KnockPedestrians()
        {
            foreach (var vehicle in vehicles)
            {
                if (vehicle == null) continue;
                Transform t = vehicle.transform;
                Vector3 centre = t.position + Vector3.up * 0.8f;
                Vector3 half = new Vector3(vehicle.width * 0.5f, 0.75f, vehicle.length * 0.5f);
                int count = Physics.OverlapBoxNonAlloc(centre, half, m_Overlap, t.rotation, characterMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var motor = m_Overlap[i].GetComponentInParent<CharacterMotor>();
                    if (motor == null || motor.IsDriven) continue;
                    if (m_LastKnock.TryGetValue(motor, out float last) && Time.time - last < 1.2f) continue;
                    m_LastKnock[motor] = Time.time;
                    Vector3 away = motor.transform.position - t.position;
                    away.y = 0f;
                    Vector3 side = Vector3.Dot(away, t.right) >= 0f ? t.right : -t.right;
                    Vector3 push = (t.forward * 0.6f + side).normalized * Mathf.Max(3f, vehicle.Speed * knockSpeedFactor);
                    var target = motor.GetComponent<IVehicleImpactReceiver>();
                    if (target != null) target.OnHitByVehicle(vehicle, push, knockDamage);
                    else motor.AddImpulse(push);
                }
            }
        }

        void Watchdog(float dt)
        {
            m_WatchTimer += dt;
            if (m_WatchTimer < 1f) return;
            foreach (var v in vehicles)
            {
                if (v == null) continue;
                Vector3 start = m_WatchStart.TryGetValue(v, out var s) ? s : v.transform.position;
                float speed = Vector3.Distance(start, v.transform.position) / m_WatchTimer;
                MinimumObservedSpeed = Mathf.Min(MinimumObservedSpeed, speed);
                if (speed < parameters.minSpeed * 0.5f)
                {
                    StallWarnings++;
                    Debug.LogWarning($"[PoingMort] {v.name} n'a parcouru que {speed:0.00} m/s sur la dernière seconde.", v);
                }
                m_WatchStart[v] = v.transform.position;
            }
            m_WatchTimer = 0f;
        }

        /// <summary>Finds the best car the player could hail or board.</summary>
        public TrafficVehicle FindNearestVehicle(Vector3 position, float maxDistance, out VehicleSide side, out float doorDistance)
        {
            TrafficVehicle best = null;
            side = VehicleSide.Right;
            doorDistance = float.MaxValue;
            foreach (var v in vehicles)
            {
                if (v == null || v.IsPlayerDriven) continue;
                foreach (VehicleSide s in new[] { VehicleSide.Left, VehicleSide.Right })
                {
                    Transform door = v.GetDoorEntry(s);
                    if (door == null) continue;
                    Vector3 d = door.position - position;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < doorDistance && dist <= maxDistance)
                    {
                        doorDistance = dist;
                        best = v;
                        side = s;
                    }
                }
            }
            return best;
        }
    }

    /// <summary>Implemented by characters that react to being hit by a car (fall, damage).</summary>
    public interface IVehicleImpactReceiver
    {
        void OnHitByVehicle(TrafficVehicle vehicle, Vector3 push, float damage);
    }
}
