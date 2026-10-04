using System;
using PoingMort.Characters;
using PoingMort.Combat;
using PoingMort.Controls;
using PoingMort.Core;
using PoingMort.Traffic;
using PoingMort.Vehicles;
using UnityEngine;

namespace PoingMort.Player
{
    /// <summary>
    /// The player character. Owns the state machine (ÀPied, Embarquement, EnVoiture, Sortie, Chute, KO)
    /// and makes sure exactly one system moves the character at any time:
    /// the CharacterMotor on foot, a vehicle-space path during boarding/exit, the seat while driving.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(Fighter))]
    public sealed class PlayerController : MonoBehaviour, IVehicleImpactReceiver
    {
        public static PlayerController Current { get; private set; }

        [Header("Déplacement à pied (m/s)")]
        public float walkSpeed = 1.6f;
        public float runSpeed = 3.9f;
        public float sprintSpeed = 6.7f;
        public float combatSpeed = 2.3f;
        public float guardSpeed = 1.25f;
        [Tooltip("Distance à laquelle le joueur se met en garde face à un adversaire en combat.")]
        public float combatRange = 6f;
        [Tooltip("Distance à laquelle un coup s'oriente automatiquement vers l'adversaire.")]
        public float softLockRange = 3.2f;

        [Header("Voitures")]
        [Tooltip("Couches qui bloquent une sortie de voiture (décor, voitures, personnages).")]
        public LayerMask exitBlockMask = ~0;
        [Tooltip("Couches considérées comme sol.")]
        public LayerMask groundMask = ~0;
        [Tooltip("Marge de sécurité vis-à-vis des voitures qui arrivent lors d'une sortie (s).")]
        public float exitTrafficLookAhead = 0.9f;

        [Header("Chute")]
        public float fallDuration = 1.05f;
        public float getUpDuration = 0.95f;
        public float fallImpulseDamping = 2.6f;

        [Header("Divers")]
        public Transform respawnPoint;
        public float killPlaneY = -25f;

        public CharacterMotor Motor { get; private set; }
        public Fighter Fighter { get; private set; }
        public CharacterAnimatorBase Anim { get; private set; }
        public PlayerStateMachine States { get; } = new PlayerStateMachine();
        public PlayerState State => States.State;
        public TrafficVehicle CurrentVehicle { get; private set; }
        public bool InCombat { get; private set; }

        /// <summary>Key of the available interaction (e.g. "F"), null when nothing is available.</summary>
        public string PromptKey { get; private set; }
        /// <summary>Action of the available interaction (e.g. "Monter").</summary>
        public string PromptAction { get; private set; }
        /// <summary>Interaction line (e.g. "F  Monter"), null when nothing is available.</summary>
        public string Prompt
        {
            get => PromptAction == null ? null : $"{PromptKey}  {PromptAction}";
            private set
            {
                if (value == null) { PromptKey = null; PromptAction = null; return; }
                int split = value.IndexOf("  ", System.StringComparison.Ordinal);
                PromptKey = split > 0 ? value.Substring(0, split) : null;
                PromptAction = split > 0 ? value.Substring(split + 2) : value;
            }
        }
        /// <summary>Secondary hint shown under the prompt (e.g. "Trop rapide : cours à sa vitesse").</summary>
        public string PromptHint { get; private set; }

        public int BoardingCount { get; private set; }
        public int ExitCount { get; private set; }

        public event Action<string> Notified;

        BoardingParameters m_Boarding = new BoardingParameters();
        VehicleSide m_Side;
        float m_T;
        Vector3 m_StartLocal, m_DoorLocal, m_SeatLocal, m_ExitLocal;
        Quaternion m_StartRotLocal = Quaternion.identity;
        float m_ExitSpeed;
        float m_ExitRetryTime;
        float m_FallTimer;
        bool m_GettingUp;
        float m_DefaultImpulseDamping;
        float m_LastLaneInput;
        bool m_LockOn;
        Fighter m_LockTarget;
        float m_LastCarHitNotice = -100f;

        TrafficVehicle m_Candidate;
        VehicleSide m_CandidateSide;
        float m_CandidateDistance;
        BoardingRefusal m_CandidateRefusal;
        Interactable m_Interactable;

        void Awake()
        {
            Current = this;
            Motor = GetComponent<CharacterMotor>();
            Fighter = GetComponent<Fighter>();
            Fighter.team = FighterTeam.Player;
            Anim = GetComponentInChildren<CharacterAnimatorBase>();
            if (Fighter.animator == null) Fighter.animator = Anim;
            m_DefaultImpulseDamping = Motor.impulseDamping;
            Fighter.KnockedOut += OnKnockedOut;
        }

        void Start()
        {
            if (TrafficSystem.Current != null)
            {
                m_Boarding = TrafficSystem.Current.boarding;
                TrafficSystem.Current.RegisterPedestrian(Motor);
            }
            if (CameraRig.Current != null) CameraRig.Current.SetFootTarget(transform, true);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (Fighter != null) Fighter.KnockedOut -= OnKnockedOut;
            if (TrafficSystem.Current != null) TrafficSystem.Current.UnregisterPedestrian(Motor);
        }

        void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message)) Notified?.Invoke(message);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // explicit pause
            States.Tick(dt);

            switch (States.State)
            {
                case PlayerState.APied: UpdateOnFoot(dt); break;
                case PlayerState.Embarquement: UpdateBoarding(dt); break;
                case PlayerState.EnVoiture: UpdateInVehicle(dt); break;
                case PlayerState.Sortie: UpdateExit(dt); break;
                case PlayerState.Chute: UpdateFall(dt); break;
                case PlayerState.KO: UpdateKnockedOut(dt); break;
            }

            if (transform.position.y < killPlaneY && States.State != PlayerState.EnVoiture) Respawn();
        }

        // ------------------------------------------------------------------ À pied

        void UpdateOnFoot(float dt)
        {
            var input = GameInput.Instance;
            var cam = CameraRig.Current;
            Vector2 move = input.Move.ReadValue<Vector2>();
            Vector3 fwd = transform.forward, right = transform.right;
            if (cam != null) cam.GetPlanarBasis(out fwd, out right);
            Vector3 wish = fwd * move.y + right * move.x;
            float magnitude = Mathf.Clamp01(wish.magnitude);
            if (magnitude > 0.001f) wish /= wish.magnitude;

            Fighter enemy = Fighter.FindNearestEnemy(combatRange);
            if (enemy != null && enemy.Locked) enemy = null; // only opponents of an active fight
            if (m_LockOn && (m_LockTarget == null || m_LockTarget.IsKnockedOut || m_LockTarget.Locked)) m_LockOn = false;
            Fighter focus = m_LockOn ? m_LockTarget : enemy;
            InCombat = focus != null;
            if (cam != null) cam.SetCombatFocus(InCombat ? focus.transform : null);

            Fighter.SetGuard(input.Guard.IsPressed());

            float speed;
            if (InCombat) speed = Fighter.IsGuarding ? guardSpeed : combatSpeed;
            else if (input.Sprint.IsPressed() && magnitude > 0.5f) speed = sprintSpeed;
            else speed = magnitude < 0.55f ? walkSpeed : runSpeed;
            if (Fighter.IsAttacking) speed *= 0.2f;
            if (Fighter.IsStaggered) speed = 0f;
            Vector3 desired = wish * (speed * (InCombat ? 1f : Mathf.Max(magnitude, 0f)));
            if (magnitude < 0.001f) desired = Vector3.zero;
            Motor.Move(desired, dt);

            if (InCombat) Motor.Face(focus.transform.position - transform.position, dt, 0.8f);
            else if (desired.sqrMagnitude > 0.01f && !Fighter.IsAttacking) Motor.Face(desired, dt);

            if (input.LightAttack.WasPressedThisFrame()) { SoftFace(); Fighter.TryLightAttack(); }
            if (input.HeavyAttack.WasPressedThisFrame()) { SoftFace(); Fighter.TryHeavyAttack(); }
            if (input.Dodge.WasPressedThisFrame())
                Fighter.TryDodge(magnitude > 0.1f ? wish : -transform.forward);
            if (input.LockOn.WasPressedThisFrame())
            {
                if (m_LockOn) m_LockOn = false;
                else if (enemy != null) { m_LockOn = true; m_LockTarget = enemy; }
            }

            if (Anim != null)
            {
                Vector3 planar = Motor.PlanarVelocity;
                Vector3 local = transform.InverseTransformDirection(planar);
                float reference = InCombat ? Mathf.Max(0.1f, combatSpeed) : 1f;
                Vector2 dir = InCombat ? Vector2.ClampMagnitude(new Vector2(local.x, local.z) / reference, 1f) : new Vector2(local.x, local.z).normalized;
                Anim.SetLocomotion(new Vector3(planar.x, 0f, planar.z).magnitude, dir, Motor.IsGrounded);
                Anim.SetCombatStance(InCombat);
            }

            UpdateInteraction(input);
        }

        void SoftFace()
        {
            var target = Fighter.FindNearestEnemy(softLockRange);
            if (target != null && !target.Locked) Motor.FaceImmediate(target.transform.position - transform.position);
        }

        void UpdateInteraction(GameInput input)
        {
            Prompt = null;
            PromptHint = null;
            m_Interactable = null;
            m_Candidate = null;
            string key = InputPrompts.For(input.Interact);

            var interactable = Interactable.FindBest(transform.position, gameObject);
            if (interactable != null)
            {
                m_Interactable = interactable;
                Prompt = $"{key}  {interactable.GetPrompt(gameObject)}";
            }
            else if (TrafficSystem.Current != null && !InCombat)
            {
                var ts = TrafficSystem.Current;
                var vehicle = ts.FindNearestVehicle(transform.position, m_Boarding.hailRange, out var side, out float distance);
                if (vehicle != null)
                {
                    m_Candidate = vehicle;
                    m_CandidateSide = side;
                    m_CandidateDistance = distance;
                    Vector3 rel = vehicle.Velocity - Motor.Velocity;
                    rel.y = 0f;
                    bool sideFree = IsDoorSideFree(vehicle, side);
                    m_CandidateRefusal = BoardingRules.CanBoard(distance, rel.magnitude, sideFree, vehicle.IsOccupied, Fighter.IsBusy, m_Boarding);
                    if (m_CandidateRefusal == BoardingRefusal.None) Prompt = $"{key}  Monter";
                    else if (!vehicle.IsHailed) Prompt = $"{key}  Héler la voiture";
                    else PromptHint = BoardingRules.Describe(m_CandidateRefusal);
                }
            }

            if (!input.Interact.WasPressedThisFrame()) return;
            if (m_Interactable != null)
            {
                m_Interactable.Interact(gameObject);
            }
            else if (m_Candidate != null)
            {
                if (m_CandidateRefusal == BoardingRefusal.None) BeginBoarding(m_Candidate, m_CandidateSide);
                else if (m_CandidateRefusal != BoardingRefusal.Occupied)
                {
                    m_Candidate.Hail(m_Boarding.hailDuration);
                    GameAudio.Whistle(transform.position + Vector3.up * 1.6f);
                    Notify("La voiture ralentit sans s'arrêter : rejoins la portière");
                }
                else Notify(BoardingRules.Describe(m_CandidateRefusal));
            }
        }

        bool IsDoorSideFree(TrafficVehicle vehicle, VehicleSide side)
        {
            Transform door = vehicle.GetDoorEntry(side);
            if (door == null) return false;
            var hits = Physics.OverlapSphere(door.position + Vector3.up * 0.9f, 0.3f, exitBlockMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(vehicle.transform) || h.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ Embarquement

        /// <summary>Starts boarding without checking distance/speed (public for tests and debug tools).</summary>
        public bool BeginBoarding(TrafficVehicle vehicle, VehicleSide side)
        {
            if (vehicle == null || vehicle.seatAnchor == null || vehicle.GetDoorEntry(side) == null) return false;
            if (!States.TryEnter(PlayerState.Embarquement)) return false;

            CurrentVehicle = vehicle;
            m_Side = side;
            m_T = 0f;
            vehicle.Reserve(true);
            vehicle.Hail(m_Boarding.boardingDuration + 1f);
            Motor.SetDriven(true);
            Fighter.SetGuard(false);
            Fighter.Locked = true;
            Prompt = null;
            PromptHint = null;

            Transform vt = vehicle.transform;
            m_StartLocal = vt.InverseTransformPoint(transform.position);
            m_StartRotLocal = Quaternion.Inverse(vt.rotation) * transform.rotation;
            m_DoorLocal = vt.InverseTransformPoint(vehicle.GetDoorEntry(side).position);
            m_SeatLocal = vt.InverseTransformPoint(vehicle.seatAnchor.position);

            var door = vehicle.GetDoor(side);
            if (door != null) door.Open();
            if (Anim != null)
            {
                Anim.SetCombatStance(false);
                Anim.PlayVehicleTransition(true);
            }
            if (CameraRig.Current != null) CameraRig.Current.EnterVehicle(vt);
            BoardingCount++;
            return true;
        }

        void UpdateBoarding(float dt)
        {
            var vehicle = CurrentVehicle;
            if (vehicle == null) { AbortToFoot(); return; }
            m_T += dt / Mathf.Max(0.1f, m_Boarding.boardingDuration);
            Transform vt = vehicle.transform;
            // Everything is computed in the car's local space, so the character inherits the car's motion.
            Vector3 local = BoardingRules.BoardingPath(m_StartLocal, m_DoorLocal, m_SeatLocal, m_T);
            float s = Mathf.Clamp01(m_T);
            s = s * s * (3f - 2f * s);
            transform.SetPositionAndRotation(vt.TransformPoint(local), vt.rotation * Quaternion.Slerp(m_StartRotLocal, Quaternion.identity, s));
            if (m_T >= 1f) FinishBoarding();
        }

        void FinishBoarding()
        {
            var vehicle = CurrentVehicle;
            if (!States.TryEnter(PlayerState.EnVoiture)) return;
            transform.SetParent(vehicle.seatAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (Anim != null) Anim.SetSeated(true);
            vehicle.SetPlayerDriven(true);
            var door = vehicle.GetDoor(m_Side);
            if (door != null) door.Close();
            m_LastLaneInput = 0f;
            if (GameSession.Current != null) GameSession.Current.SetGameplayContext(InputContext.Vehicle);
            else GameInput.Instance.SetContext(InputContext.Vehicle);
        }

        void AbortToFoot()
        {
            transform.SetParent(null, true);
            Motor.SetDriven(false);
            Fighter.Locked = false;
            if (CurrentVehicle != null) CurrentVehicle.Reserve(false);
            CurrentVehicle = null;
            if (CameraRig.Current != null) CameraRig.Current.ExitVehicle();
            if (GameSession.Current != null) GameSession.Current.SetGameplayContext(InputContext.OnFoot);
            if (!States.TryEnter(PlayerState.APied)) States.ForceReset();
        }

        // ------------------------------------------------------------------ En voiture

        void UpdateInVehicle(float dt)
        {
            var vehicle = CurrentVehicle;
            if (vehicle == null) { AbortToFoot(); return; }
            var input = GameInput.Instance;
            vehicle.ApplyPlayerPace(input.Throttle.ReadValue<float>(), dt);

            float lane = input.LaneChange.ReadValue<float>();
            if (lane < -0.5f && m_LastLaneInput >= -0.5f) ChangeLane(vehicle, LaneChangeDirection.Left);
            if (lane > 0.5f && m_LastLaneInput <= 0.5f) ChangeLane(vehicle, LaneChangeDirection.Right);
            m_LastLaneInput = lane;

            if (input.Horn.WasPressedThisFrame()) vehicle.Honk();

            Prompt = $"{InputPrompts.For(input.ExitVehicle)}  Descendre";
            PromptHint = null;
            if (input.ExitVehicle.WasPressedThisFrame()) TryExit();
        }

        void ChangeLane(TrafficVehicle vehicle, LaneChangeDirection direction)
        {
            var sim = vehicle.System != null ? vehicle.System.Simulation : null;
            if (sim == null || vehicle.Agent == null) return;
            if (sim.GetNeighbour(vehicle.Agent.Lane, direction) < 0)
            {
                Notify(direction == LaneChangeDirection.Left ? "Pas de voie à gauche" : "Pas de voie à droite");
                return;
            }
            if (!vehicle.RequestLaneChange(direction)) Notify("Voie occupée : la voiture garde ses distances");
        }

        public void TryExit()
        {
            var vehicle = CurrentVehicle;
            if (vehicle == null || States.State != PlayerState.EnVoiture) return;
            if (Time.time < m_ExitRetryTime) return;
            VehicleSide preferred = vehicle.KerbSide;
            VehicleSide other = preferred == VehicleSide.Left ? VehicleSide.Right : VehicleSide.Left;
            bool preferredFree = IsExitFree(vehicle, preferred);
            bool otherFree = IsExitFree(vehicle, other);
            var refusal = BoardingRules.CanExit(vehicle.Speed, preferredFree, otherFree, preferred, false, m_Boarding, out var side);
            if (refusal != ExitRefusal.None)
            {
                Notify(BoardingRules.Describe(refusal));
                m_ExitRetryTime = Time.time + m_Boarding.exitRetryDelay;
                return;
            }
            BeginExit(side);
        }

        /// <summary>Free space, real ground (not a car roof) and no car about to pass on that side.</summary>
        bool IsExitFree(TrafficVehicle vehicle, VehicleSide side)
        {
            Transform exit = vehicle.GetExitPoint(side);
            if (exit == null) return false;
            Vector3 probe = exit.position + Vector3.up * 1.2f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 3f, groundMask, QueryTriggerInteraction.Ignore)) return false;
            if (ground.collider.GetComponentInParent<TrafficVehicle>() != null) return false;
            if (ground.normal.y < 0.7f) return false;
            if (!SpaceFreeIgnoring(ground.point, vehicle)) return false;
            if (VehicleComing(vehicle, ground.point)) return false;
            return true;
        }

        bool SpaceFreeIgnoring(Vector3 feet, TrafficVehicle own)
        {
            var cc = Motor.Controller;
            float radius = cc.radius + 0.05f;
            Vector3 bottom = feet + Vector3.up * (radius + 0.08f);
            Vector3 top = feet + Vector3.up * Mathf.Max(cc.height - radius, radius + 0.1f);
            var hits = Physics.OverlapCapsule(bottom, top, radius, exitBlockMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(transform) || h.transform.IsChildOf(own.transform)) continue;
                return false;
            }
            return true;
        }

        bool VehicleComing(TrafficVehicle own, Vector3 point)
        {
            var ts = TrafficSystem.Current;
            if (ts == null) return false;
            foreach (var other in ts.vehicles)
            {
                if (other == null || other == own) continue;
                Vector3 future = other.transform.position + other.Velocity * exitTrafficLookAhead;
                Vector3 rel = point - future;
                float side = Mathf.Abs(Vector3.Dot(rel, other.transform.right));
                float along = Vector3.Dot(rel, other.transform.forward);
                if (side < other.width * 0.5f + 0.7f && along > -other.length * 0.5f - 1f && along < other.length * 0.5f + 3f)
                    return true;
            }
            return false;
        }

        void BeginExit(VehicleSide side)
        {
            var vehicle = CurrentVehicle;
            if (!States.TryEnter(PlayerState.Sortie)) return;
            Transform vt = vehicle.transform;
            transform.SetParent(null, true);
            m_Side = side;
            m_T = 0f;
            m_SeatLocal = vt.InverseTransformPoint(transform.position);
            m_DoorLocal = vt.InverseTransformPoint(vehicle.GetDoorEntry(side).position);
            m_ExitLocal = vt.InverseTransformPoint(vehicle.GetExitPoint(side).position);
            m_ExitSpeed = vehicle.Speed;
            vehicle.SetPlayerDriven(false);
            vehicle.Reserve(true);
            var door = vehicle.GetDoor(side);
            if (door != null) door.Open();
            if (Anim != null)
            {
                Anim.SetSeated(false);
                Anim.PlayVehicleTransition(false);
            }
            if (CameraRig.Current != null) CameraRig.Current.ExitVehicle();
            Prompt = null;
            ExitCount++;
        }

        void UpdateExit(float dt)
        {
            var vehicle = CurrentVehicle;
            if (vehicle == null) { AbortToFoot(); return; }
            m_T += dt / Mathf.Max(0.1f, m_Boarding.exitDuration);
            Transform vt = vehicle.transform;
            Vector3 local = BoardingRules.ExitPath(m_SeatLocal, m_DoorLocal, m_ExitLocal, m_T);
            float s = Mathf.Clamp01(m_T);
            s = s * s * (3f - 2f * s);
            Quaternion faceOut = Quaternion.Euler(0f, m_Side == VehicleSide.Left ? -55f : 55f, 0f);
            transform.SetPositionAndRotation(vt.TransformPoint(local), vt.rotation * Quaternion.Slerp(Quaternion.identity, faceOut, s * 0.6f));
            if (m_T >= 1f) FinishExit();
        }

        void FinishExit()
        {
            var vehicle = CurrentVehicle;
            Vector3 inherited = vehicle.Velocity;
            inherited.y = 0f;
            inherited += vehicle.SideDirection(m_Side) * 1.1f;

            Vector3 pos = transform.position;
            if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 3f, groundMask, QueryTriggerInteraction.Ignore))
                pos.y = ground.point.y;
            Vector3 facing = vehicle.transform.forward;
            facing.y = 0f;

            Motor.Teleport(pos, Quaternion.LookRotation(facing.normalized, Vector3.up));
            Motor.SetDriven(false);
            Motor.AddImpulse(inherited);

            var door = vehicle.GetDoor(m_Side);
            if (door != null) door.Close();
            vehicle.Reserve(false);
            CurrentVehicle = null;
            Fighter.Locked = false;
            if (GameSession.Current != null) GameSession.Current.SetGameplayContext(InputContext.OnFoot);
            else GameInput.Instance.SetContext(InputContext.OnFoot);

            if (BoardingRules.ExitEndsInRoll(m_ExitSpeed, m_Boarding))
            {
                States.TryEnter(PlayerState.Chute);
                StartFall();
            }
            else States.TryEnter(PlayerState.APied);
        }

        // ------------------------------------------------------------------ Chute

        void StartFall()
        {
            m_FallTimer = 0f;
            m_GettingUp = false;
            Fighter.SetGuard(false);
            Fighter.Locked = true;
            Motor.impulseDamping = fallImpulseDamping;
            if (Anim != null) Anim.SetFallen(true);
        }

        void UpdateFall(float dt)
        {
            Motor.Move(Vector3.zero, dt);
            m_FallTimer += dt;
            if (!m_GettingUp && m_FallTimer >= fallDuration)
            {
                m_GettingUp = true;
                Motor.impulseDamping = m_DefaultImpulseDamping;
                if (Anim != null) Anim.PlayGetUp();
            }
            if (m_FallTimer >= fallDuration + getUpDuration)
            {
                Motor.impulseDamping = m_DefaultImpulseDamping;
                Fighter.Locked = false;
                States.TryEnter(PlayerState.APied);
            }
        }

        public void OnHitByVehicle(TrafficVehicle vehicle, Vector3 push, float damage)
        {
            if (States.State != PlayerState.APied) return;
            // Cars never kill in this prototype: keep at least 1 HP.
            float allowed = Mathf.Max(0f, Mathf.Min(damage, Fighter.Health.Current - 1f));
            Fighter.Health.Damage(allowed);
            Motor.AddImpulse(push);
            if (States.TryEnter(PlayerState.Chute)) StartFall();
            if (Time.time - m_LastCarHitNotice > 8f)
            {
                m_LastCarHitNotice = Time.time;
                Notify("Les voitures ne s'arrêtent jamais : attention en traversant !");
            }
        }

        // ------------------------------------------------------------------ KO

        void OnKnockedOut(Fighter fighter)
        {
            if (States.State == PlayerState.APied || States.State == PlayerState.Chute)
            {
                Motor.impulseDamping = m_DefaultImpulseDamping;
                States.TryEnter(PlayerState.KO);
                InCombat = false;
            }
        }

        void UpdateKnockedOut(float dt)
        {
            Motor.Move(Vector3.zero, dt);
            Prompt = null;
        }

        /// <summary>Restores health and gets the player back on foot (fight restart).</summary>
        public void Revive()
        {
            bool wasDown = States.State == PlayerState.KO;
            Fighter.Revive();
            Fighter.Locked = false;
            if (States.State == PlayerState.KO || States.State == PlayerState.Chute)
            {
                States.ForceReset();
                if (wasDown && Anim != null) Anim.PlayGetUp();
            }
        }

        public void Respawn()
        {
            if (CurrentVehicle != null) AbortToFoot();
            Vector3 pos = respawnPoint != null ? respawnPoint.position : Vector3.zero;
            Quaternion rot = respawnPoint != null ? respawnPoint.rotation : Quaternion.identity;
            transform.SetParent(null, true);
            Motor.SetDriven(false);
            Motor.Teleport(pos, rot);
            Fighter.Revive();
            Fighter.Locked = false;
            States.ForceReset();
            if (CameraRig.Current != null) CameraRig.Current.SetFootTarget(transform, true);
            if (GameSession.Current != null) GameSession.Current.SetGameplayContext(InputContext.OnFoot);
        }
    }
}
