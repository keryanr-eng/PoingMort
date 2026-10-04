using PoingMort.Controls;
using PoingMort.Core;
using UnityEngine;

namespace PoingMort.Player
{
    public enum CameraMode { OnFoot, Vehicle, Fixed }

    /// <summary>
    /// Third-person camera: orbit behind the character on foot, chase camera in a car, smooth blend
    /// between the two, collision with the scenery and a camera shake that respects the option.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Current { get; private set; }

        [Header("À pied")]
        public float footDistance = 3.4f;
        public float footHeight = 1.55f;
        public float shoulderOffset = 0.42f;
        public float minPitch = -30f;
        public float maxPitch = 62f;
        public float footFov = 58f;
        [Tooltip("Recul supplémentaire de la caméra pendant un combat.")]
        public float combatExtraDistance = 0.6f;

        [Header("Voiture")]
        public float vehicleDistance = 6.6f;
        public float vehicleHeight = 2.1f;
        public float vehicleLookHeight = 1.2f;
        public float vehicleFov = 64f;
        [Tooltip("Délai avant que la caméra se recale derrière la voiture (s).")]
        public float vehicleRecenterDelay = 1.6f;

        [Header("Collisions")]
        public LayerMask collisionMask = ~0;
        public float collisionRadius = 0.22f;

        [Header("Transitions")]
        public float blendDuration = 0.65f;

        public Camera Camera { get; private set; }
        public CameraMode Mode { get; private set; } = CameraMode.OnFoot;
        public float Yaw => m_Yaw;

        Transform m_FootTarget;
        Transform m_VehicleTarget;
        Transform m_CombatFocus;
        float m_Yaw, m_Pitch = 12f;
        float m_VehicleYawOffset, m_VehiclePitch = 8f, m_LastVehicleLookTime;
        float m_CurrentDistance;

        Vector3 m_BlendFromPos;
        Quaternion m_BlendFromRot;
        float m_BlendFromFov;
        float m_BlendT = 1f;

        Vector3 m_FixedPos;
        Quaternion m_FixedRot;

        // Shake
        float m_ShakeAmplitude;
        float m_ShakeTime;

        void Awake()
        {
            Current = this;
            Camera = GetComponentInChildren<Camera>();
            if (Camera == null) Camera = gameObject.AddComponent<Camera>();
            m_CurrentDistance = footDistance;
            m_Yaw = transform.eulerAngles.y;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void SetFootTarget(Transform target, bool snap)
        {
            m_FootTarget = target;
            if (snap && target != null)
            {
                m_Yaw = target.eulerAngles.y;
                Mode = CameraMode.OnFoot;
                m_BlendT = 1f;
                ApplyPose(ComputeFootPose(out var fov), fov);
            }
        }

        public void SetCombatFocus(Transform focus) => m_CombatFocus = focus;

        public void EnterVehicle(Transform vehicle)
        {
            m_VehicleTarget = vehicle;
            m_VehicleYawOffset = Mathf.DeltaAngle(vehicle.eulerAngles.y, m_Yaw);
            m_VehiclePitch = 8f;
            m_LastVehicleLookTime = Time.time;
            StartBlend(CameraMode.Vehicle);
        }

        public void ExitVehicle()
        {
            if (m_VehicleTarget != null)
                m_Yaw = m_VehicleTarget.eulerAngles.y + m_VehicleYawOffset;
            m_Pitch = 10f;
            StartBlend(CameraMode.OnFoot);
        }

        public void SetFixed(Vector3 position, Quaternion rotation, bool snap)
        {
            m_FixedPos = position;
            m_FixedRot = rotation;
            if (snap)
            {
                Mode = CameraMode.Fixed;
                m_BlendT = 1f;
                ApplyPose(new Pose(position, rotation), footFov);
            }
            else StartBlend(CameraMode.Fixed);
        }

        void StartBlend(CameraMode mode)
        {
            m_BlendFromPos = transform.position;
            m_BlendFromRot = transform.rotation;
            m_BlendFromFov = Camera.fieldOfView;
            m_BlendT = 0f;
            Mode = mode;
        }

        /// <summary>Adds a short shake. Scaled by the "secousses de caméra" option (0 disables it).</summary>
        public void Shake(float amplitude)
        {
            float scaled = amplitude * GameSettings.CameraShake;
            if (scaled <= 0.001f) return;
            m_ShakeAmplitude = Mathf.Max(m_ShakeAmplitude, scaled);
            m_ShakeTime = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool paused = GameSession.Current != null && GameSession.Current.IsPaused;
            if (!paused) ReadLookInput(dt);

            Pose target;
            float fov;
            switch (Mode)
            {
                case CameraMode.Vehicle: target = ComputeVehiclePose(out fov, dt); break;
                case CameraMode.Fixed: target = new Pose(m_FixedPos, m_FixedRot); fov = footFov; break;
                default: target = ComputeFootPose(out fov); break;
            }

            if (m_BlendT < 1f)
            {
                m_BlendT = Mathf.Min(1f, m_BlendT + dt / Mathf.Max(0.05f, blendDuration));
                float s = m_BlendT * m_BlendT * (3f - 2f * m_BlendT);
                target = new Pose(Vector3.Lerp(m_BlendFromPos, target.position, s), Quaternion.Slerp(m_BlendFromRot, target.rotation, s));
                fov = Mathf.Lerp(m_BlendFromFov, fov, s);
            }

            ApplyPose(target, fov);
            ApplyShake(dt);
        }

        void ReadLookInput(float dt)
        {
            if (!GameInput.Exists) return;
            var input = GameInput.Instance;
            if (Mode == CameraMode.OnFoot && input.Context == InputContext.OnFoot)
            {
                Vector2 look = input.ReadLookDegrees(false, dt);
                m_Yaw += look.x;
                m_Pitch = Mathf.Clamp(m_Pitch - look.y, minPitch, maxPitch);
            }
            else if (Mode == CameraMode.Vehicle && input.Context == InputContext.Vehicle)
            {
                Vector2 look = input.ReadLookDegrees(true, dt);
                if (look.sqrMagnitude > 0.0004f) m_LastVehicleLookTime = Time.unscaledTime;
                m_VehicleYawOffset += look.x;
                m_VehiclePitch = Mathf.Clamp(m_VehiclePitch - look.y, -5f, 40f);
            }
        }

        Pose ComputeFootPose(out float fov)
        {
            fov = footFov;
            if (m_FootTarget == null) return new Pose(transform.position, transform.rotation);

            bool combat = m_CombatFocus != null && m_CombatFocus.gameObject.activeInHierarchy;
            float wantedDistance = footDistance + (combat ? combatExtraDistance : 0f);
            Quaternion rot = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            Vector3 pivot = m_FootTarget.position + Vector3.up * footHeight;
            if (combat)
            {
                // Keep the opponent in frame: pivot slightly toward the middle of the fight.
                Vector3 mid = Vector3.Lerp(m_FootTarget.position, m_CombatFocus.position, 0.3f);
                pivot = Vector3.Lerp(pivot, mid + Vector3.up * footHeight, 0.5f);
            }
            Vector3 right = rot * Vector3.right;
            Vector3 shoulderPivot = pivot + right * shoulderOffset;
            Vector3 desired = shoulderPivot - rot * Vector3.forward * wantedDistance;

            Vector3 castDir = desired - pivot;
            float castLength = castDir.magnitude;
            castDir = castLength > 1e-4f ? castDir / castLength : -(rot * Vector3.forward);
            float allowed = castLength;
            if (Physics.SphereCast(pivot, collisionRadius, castDir, out RaycastHit hit, castLength, collisionMask, QueryTriggerInteraction.Ignore)
                && !IsOwnTarget(hit.collider.transform))
            {
                allowed = Mathf.Max(0.6f, hit.distance - 0.05f);
            }
            // Pull in instantly (never show the inside of a wall), ease back out.
            if (allowed < m_CurrentDistance) m_CurrentDistance = allowed;
            else m_CurrentDistance = Mathf.Lerp(m_CurrentDistance, allowed, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            desired = pivot + castDir * m_CurrentDistance;
            return new Pose(desired, rot);
        }

        bool IsOwnTarget(Transform t)
        {
            return (m_FootTarget != null && t.IsChildOf(m_FootTarget.root)) ||
                   (m_VehicleTarget != null && t.IsChildOf(m_VehicleTarget.root));
        }

        Pose ComputeVehiclePose(out float fov, float dt)
        {
            fov = vehicleFov;
            if (m_VehicleTarget == null) return new Pose(transform.position, transform.rotation);

            // Recentre behind the car after a while without camera input.
            if (Time.unscaledTime - m_LastVehicleLookTime > vehicleRecenterDelay)
            {
                m_VehicleYawOffset = Mathf.LerpAngle(m_VehicleYawOffset, 0f, 1f - Mathf.Exp(-2.2f * dt));
                m_VehiclePitch = Mathf.Lerp(m_VehiclePitch, 8f, 1f - Mathf.Exp(-2.2f * dt));
            }
            float yaw = m_VehicleTarget.eulerAngles.y + m_VehicleYawOffset;
            Quaternion orbit = Quaternion.Euler(m_VehiclePitch, yaw, 0f);
            Vector3 lookAt = m_VehicleTarget.position + Vector3.up * vehicleLookHeight;
            Vector3 desired = lookAt - orbit * Vector3.forward * vehicleDistance + Vector3.up * (vehicleHeight - vehicleLookHeight);
            if (Physics.SphereCast(lookAt, collisionRadius, (desired - lookAt).normalized, out RaycastHit hit, (desired - lookAt).magnitude, collisionMask, QueryTriggerInteraction.Ignore)
                && !IsOwnTarget(hit.collider.transform))
            {
                desired = lookAt + (desired - lookAt).normalized * Mathf.Max(1.2f, hit.distance - 0.05f);
            }
            Quaternion rot = Quaternion.LookRotation(lookAt + m_VehicleTarget.forward * 2f - desired, Vector3.up);
            return new Pose(desired, rot);
        }

        void ApplyPose(Pose pose, float fov)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (Camera != null) Camera.fieldOfView = fov;
        }

        void ApplyShake(float dt)
        {
            if (m_ShakeAmplitude <= 0.0005f) return;
            m_ShakeTime += dt;
            float decay = Mathf.Exp(-m_ShakeTime * 9f);
            float a = m_ShakeAmplitude * decay;
            float t = m_ShakeTime * 38f;
            Vector3 offset = new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 1.7f + 1f), 0f) * (0.06f * a);
            transform.position += transform.rotation * offset;
            transform.rotation *= Quaternion.Euler(Mathf.Sin(t) * 1.2f * a, 0f, Mathf.Sin(t * 0.9f + 2f) * 0.8f * a);
            if (decay < 0.02f) m_ShakeAmplitude = 0f;
        }

        /// <summary>Camera-relative movement basis on the horizontal plane.</summary>
        public void GetPlanarBasis(out Vector3 forward, out Vector3 right)
        {
            Quaternion yawRot = Quaternion.Euler(0f, Mode == CameraMode.OnFoot ? m_Yaw : transform.eulerAngles.y, 0f);
            forward = yawRot * Vector3.forward;
            right = yawRot * Vector3.right;
        }
    }
}
