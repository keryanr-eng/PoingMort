using PoingMort.Core;
using UnityEngine;

namespace PoingMort.Vehicles
{
    /// <summary>Door rotating around its hinge (the transform's pivot must sit on the hinge axis).</summary>
    public sealed class VehicleDoor : MonoBehaviour
    {
        [Tooltip("Angle d'ouverture (degrés). Positif ou négatif selon le côté.")]
        public float openAngle = 62f;
        public float openDuration = 0.22f;
        public float closeDuration = 0.3f;
        [Tooltip("Axe de rotation local (vertical par défaut).")]
        public Vector3 hingeAxis = Vector3.up;

        Quaternion m_Closed;
        float m_Open;      // 0 closed .. 1 open
        float m_Target;
        bool m_Initialised;

        public bool IsOpen => m_Open > 0.6f;
        public float Openness => m_Open;

        void Awake() => Init();

        void Init()
        {
            if (m_Initialised) return;
            m_Closed = transform.localRotation;
            m_Initialised = true;
        }

        public void Open()
        {
            Init();
            if (m_Target < 0.5f && m_Open < 0.5f) GameAudio.DoorOpened(transform.position);
            m_Target = 1f;
        }

        public void Close() { Init(); m_Target = 0f; }

        void Update()
        {
            if (Mathf.Approximately(m_Open, m_Target)) return;
            float duration = m_Target > m_Open ? openDuration : closeDuration;
            float before = m_Open;
            m_Open = Mathf.MoveTowards(m_Open, m_Target, Time.deltaTime / Mathf.Max(0.01f, duration));
            if (before > 0f && m_Open <= 0f) GameAudio.DoorClosed(transform.position);
            float eased = m_Target > 0.5f ? 1f - (1f - m_Open) * (1f - m_Open) : m_Open * m_Open;
            transform.localRotation = m_Closed * Quaternion.AngleAxis(openAngle * eased, hingeAxis);
        }
    }
}
