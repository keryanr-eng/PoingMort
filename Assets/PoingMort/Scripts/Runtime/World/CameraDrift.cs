using UnityEngine;

namespace PoingMort.World
{
    /// <summary>Very slow sway of a fixed camera (home screen), so the 3D backdrop feels alive.</summary>
    public sealed class CameraDrift : MonoBehaviour
    {
        public float amplitude = 0.12f;
        public float rotationAmplitude = 0.6f;
        public float period = 14f;

        Vector3 m_BasePosition;
        Quaternion m_BaseRotation;

        void Start()
        {
            m_BasePosition = transform.position;
            m_BaseRotation = transform.rotation;
        }

        void LateUpdate()
        {
            float t = Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, period);
            transform.position = m_BasePosition + transform.right * (Mathf.Sin(t) * amplitude) + Vector3.up * (Mathf.Sin(t * 0.7f) * amplitude * 0.3f);
            transform.rotation = m_BaseRotation * Quaternion.Euler(Mathf.Sin(t * 0.8f) * rotationAmplitude * 0.4f, Mathf.Sin(t) * rotationAmplitude, 0f);
        }
    }
}
