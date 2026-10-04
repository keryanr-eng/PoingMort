using PoingMort.Traffic;
using UnityEngine;

namespace PoingMort.Vehicles
{
    /// <summary>
    /// Visual and interaction side of an autonomous car. Its motion is computed by <see cref="TrafficSystem"/>:
    /// it always follows its lane and never stops; a player inside only influences pace and lane.
    /// </summary>
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [Header("Repères (enfants du modèle)")]
        public Transform seatAnchor;
        public Transform doorEntryLeft;
        public Transform doorEntryRight;
        public Transform exitPointLeft;
        public Transform exitPointRight;
        public VehicleDoor doorLeft;
        public VehicleDoor doorRight;

        [Header("Roues")]
        public Transform[] wheels = new Transform[0];
        public Transform[] steeringWheels = new Transform[0];
        public float wheelRadius = 0.33f;

        [Header("Dimensions")]
        public float length = 4.9f;
        public float width = 1.86f;

        [Header("Audio")]
        public AudioSource engineAudio;
        public AudioSource hornAudio;
        public float enginePitchIdle = 0.55f;
        public float enginePitchPerMps = 0.045f;

        [Header("Pilotage joueur")]
        [Tooltip("Variation de l'allure demandée par seconde quand le joueur accélère/ralentit (m/s²).")]
        public float playerPaceRate = 5f;

        public TrafficAgent Agent { get; internal set; }
        public TrafficSystem System { get; internal set; }
        public Vector3 Velocity { get; private set; }
        public float Speed => Agent != null ? Agent.Speed : 0f;
        public bool IsPlayerDriven { get; private set; }
        public bool IsOccupied => IsPlayerDriven || m_Reserved;
        public float HailedUntil { get; private set; } = -1f;
        public bool IsHailed => Time.time < HailedUntil;

        bool m_Reserved;
        Vector3 m_LastPosition;
        bool m_HasLastPosition;
        float m_WheelAngle;
        float m_SteerAngle;
        float m_LastHornTime = -10f;

        void Awake()
        {
            if (engineAudio != null && engineAudio.clip != null && !engineAudio.isPlaying)
            {
                engineAudio.loop = true;
                engineAudio.time = Random.value * engineAudio.clip.length;
                engineAudio.Play();
            }
        }

        /// <summary>Called by the traffic system after moving the car this frame.</summary>
        internal void AfterMove(float dt, float yawRateDegPerSec)
        {
            Vector3 pos = transform.position;
            if (m_HasLastPosition && dt > 0f)
                Velocity = (pos - m_LastPosition) / dt;
            m_LastPosition = pos;
            m_HasLastPosition = true;

            float speed = Speed;
            m_WheelAngle = (m_WheelAngle + speed * dt / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg) % 360f;
            foreach (var w in wheels)
                if (w != null) w.localRotation = Quaternion.Euler(m_WheelAngle, 0f, 0f);

            float steerTarget = Mathf.Clamp(yawRateDegPerSec * 0.35f, -28f, 28f);
            m_SteerAngle = Mathf.Lerp(m_SteerAngle, steerTarget, 1f - Mathf.Exp(-8f * dt));
            foreach (var w in steeringWheels)
                if (w != null) w.localRotation = Quaternion.Euler(m_WheelAngle, m_SteerAngle, 0f);

            if (engineAudio != null)
                engineAudio.pitch = enginePitchIdle + speed * enginePitchPerMps;
        }

        public void Hail(float duration)
        {
            HailedUntil = Mathf.Max(HailedUntil, Time.time + duration);
        }

        public void ClearHail() => HailedUntil = -1f;

        /// <summary>Reserves the car while a boarding transition is running.</summary>
        public void Reserve(bool reserved) => m_Reserved = reserved;

        public void SetPlayerDriven(bool driven)
        {
            IsPlayerDriven = driven;
            m_Reserved = false;
            if (Agent != null)
                Agent.PlayerTargetSpeed = driven ? Mathf.Max(Agent.Speed, System != null ? System.Parameters.minSpeed : 3f) : -1f;
            if (!driven) ClearHail();
        }

        /// <summary>Player pace input in [-1, 1]: changes the requested speed, never below the minimum.</summary>
        public void ApplyPlayerPace(float input, float dt)
        {
            if (!IsPlayerDriven || Agent == null || System == null) return;
            var p = System.Parameters;
            float target = Agent.PlayerTargetSpeed < 0f ? Agent.Speed : Agent.PlayerTargetSpeed;
            target += input * playerPaceRate * dt;
            Agent.PlayerTargetSpeed = Mathf.Clamp(target, p.minSpeed, p.maxSpeed);
        }

        public bool RequestLaneChange(LaneChangeDirection direction)
        {
            if (System == null || Agent == null) return false;
            return System.Simulation.RequestLaneChange(Agent, direction);
        }

        public void Honk()
        {
            if (Time.time - m_LastHornTime < 0.8f) return;
            m_LastHornTime = Time.time;
            if (hornAudio != null && hornAudio.clip != null) hornAudio.Play();
        }

        public Transform GetDoorEntry(VehicleSide side) => side == VehicleSide.Left ? doorEntryLeft : doorEntryRight;
        public Transform GetExitPoint(VehicleSide side) => side == VehicleSide.Left ? exitPointLeft : exitPointRight;
        public VehicleDoor GetDoor(VehicleSide side) => side == VehicleSide.Left ? doorLeft : doorRight;

        /// <summary>World direction pointing out of the car on the given side.</summary>
        public Vector3 SideDirection(VehicleSide side) => side == VehicleSide.Left ? -transform.right : transform.right;

        /// <summary>Side of the car facing the kerb (preferred exit side).</summary>
        public VehicleSide KerbSide
        {
            get
            {
                if (System != null && Agent != null)
                {
                    var lane = System.GetLane(Agent.Lane);
                    if (lane != null) return lane.kerbOnRight ? VehicleSide.Right : VehicleSide.Left;
                }
                return VehicleSide.Right;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (doorEntryLeft) Gizmos.DrawWireSphere(doorEntryLeft.position, 0.2f);
            if (doorEntryRight) Gizmos.DrawWireSphere(doorEntryRight.position, 0.2f);
            Gizmos.color = Color.green;
            if (exitPointLeft) Gizmos.DrawWireSphere(exitPointLeft.position, 0.3f);
            if (exitPointRight) Gizmos.DrawWireSphere(exitPointRight.position, 0.3f);
            Gizmos.color = Color.magenta;
            if (seatAnchor) Gizmos.DrawWireCube(seatAnchor.position, Vector3.one * 0.25f);
        }
    }
}
