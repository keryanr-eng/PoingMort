using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.Traffic
{
    /// <summary>
    /// Closed lane described by a polyline, parametrised by arc length (metres).
    /// Pure logic: no MonoBehaviour, no logging, so it can be unit tested outside Unity.
    /// </summary>
    public sealed class LaneLoop
    {
        readonly Vector3[] m_Points;
        readonly float[] m_Cumulative; // cumulative length at the start of each segment

        public float Length { get; }
        public int PointCount => m_Points.Length;

        public LaneLoop(IList<Vector3> points)
        {
            if (points == null || points.Count < 3)
                throw new ArgumentException("A lane loop needs at least 3 points.", nameof(points));

            m_Points = new Vector3[points.Count];
            points.CopyTo(m_Points, 0);
            m_Cumulative = new float[m_Points.Length + 1];
            float total = 0f;
            for (int i = 0; i < m_Points.Length; i++)
            {
                m_Cumulative[i] = total;
                total += Vector3.Distance(m_Points[i], m_Points[(i + 1) % m_Points.Length]);
            }
            m_Cumulative[m_Points.Length] = total;
            if (total <= 0.01f)
                throw new ArgumentException("Degenerate lane loop.", nameof(points));
            Length = total;
        }

        public Vector3 GetPoint(int index) => m_Points[((index % m_Points.Length) + m_Points.Length) % m_Points.Length];

        public float Wrap(float distance)
        {
            float d = distance % Length;
            return d < 0f ? d + Length : d;
        }

        /// <summary>Shortest signed distance travelling forward from <paramref name="from"/> to <paramref name="to"/> in [0, Length).</summary>
        public float ForwardDelta(float from, float to)
        {
            return Wrap(to - from);
        }

        int FindSegment(float wrappedDistance)
        {
            int lo = 0, hi = m_Points.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (m_Cumulative[mid] <= wrappedDistance) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        public Vector3 Evaluate(float distance)
        {
            Evaluate(distance, out var p, out _);
            return p;
        }

        public void Evaluate(float distance, out Vector3 position, out Vector3 tangent)
        {
            float d = Wrap(distance);
            int seg = FindSegment(d);
            Vector3 a = m_Points[seg];
            Vector3 b = m_Points[(seg + 1) % m_Points.Length];
            float segLen = m_Cumulative[seg + 1] - m_Cumulative[seg];
            float t = segLen > 1e-5f ? (d - m_Cumulative[seg]) / segLen : 0f;
            position = Vector3.LerpUnclamped(a, b, t);
            tangent = segLen > 1e-5f ? (b - a) / segLen : Vector3.forward;
        }

        /// <summary>Smoothed tangent, averaged over a small window, used for vehicle orientation.</summary>
        public Vector3 SmoothTangent(float distance, float window)
        {
            Vector3 a = Evaluate(distance - window * 0.5f);
            Vector3 b = Evaluate(distance + window * 0.5f);
            Vector3 dir = b - a;
            dir.y = 0f;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
        }

        /// <summary>Arc-length position of the closest point on the lane (XZ plane).</summary>
        public float ClosestDistance(Vector3 worldPoint)
        {
            return ClosestDistance(worldPoint, out _);
        }

        public float ClosestDistance(Vector3 worldPoint, out float planarDistance)
        {
            float best = float.MaxValue;
            float bestDistance = 0f;
            Vector2 p = new Vector2(worldPoint.x, worldPoint.z);
            for (int i = 0; i < m_Points.Length; i++)
            {
                Vector3 a3 = m_Points[i];
                Vector3 b3 = m_Points[(i + 1) % m_Points.Length];
                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                Vector2 c = a + ab * t;
                float dist2 = (p - c).sqrMagnitude;
                if (dist2 < best)
                {
                    best = dist2;
                    bestDistance = m_Cumulative[i] + (m_Cumulative[i + 1] - m_Cumulative[i]) * t;
                }
            }
            planarDistance = Mathf.Sqrt(best);
            return bestDistance;
        }

        /// <summary>
        /// Approximate curvature (1/radius) around <paramref name="distance"/>, measured as the change of heading over a window.
        /// </summary>
        public float Curvature(float distance, float window = 4f)
        {
            Vector3 t0 = SmoothTangent(distance - window * 0.5f, 1f);
            Vector3 t1 = SmoothTangent(distance + window * 0.5f, 1f);
            float angle = Vector3.Angle(t0, t1) * Mathf.Deg2Rad;
            return angle / Mathf.Max(window, 0.01f);
        }

        /// <summary>
        /// Highest curvature found between distance and distance + lookAhead (sampled every 'step' metres).
        /// </summary>
        public float MaxCurvatureAhead(float distance, float lookAhead, float step = 2f)
        {
            float max = 0f;
            for (float s = 0f; s <= lookAhead; s += step)
                max = Mathf.Max(max, Curvature(distance + s));
            return max;
        }

        /// <summary>
        /// Builds the points of a rounded rectangle centred on <paramref name="center"/>, travelling counter-clockwise
        /// when seen from above (+Y) unless <paramref name="clockwise"/> is true.
        /// </summary>
        public static List<Vector3> RoundedRectangle(Vector3 center, float halfSizeX, float halfSizeZ, float cornerRadius, int cornerSegments, bool clockwise)
        {
            cornerRadius = Mathf.Clamp(cornerRadius, 0.5f, Mathf.Min(halfSizeX, halfSizeZ) - 0.01f);
            cornerSegments = Mathf.Max(2, cornerSegments);
            var pts = new List<Vector3>();
            // Corner centres in counter-clockwise order starting at the bottom-right (+X, -Z).
            Vector2[] centres =
            {
                new Vector2(halfSizeX - cornerRadius, -halfSizeZ + cornerRadius),
                new Vector2(halfSizeX - cornerRadius, halfSizeZ - cornerRadius),
                new Vector2(-halfSizeX + cornerRadius, halfSizeZ - cornerRadius),
                new Vector2(-halfSizeX + cornerRadius, -halfSizeZ + cornerRadius),
            };
            float[] startAngles = { -90f, 0f, 90f, 180f };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float a = (startAngles[c] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    Vector2 p = centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cornerRadius;
                    pts.Add(center + new Vector3(p.x, 0f, p.y));
                }
            }
            // Remove duplicates that are too close (segment ends meeting the next corner start).
            for (int i = pts.Count - 1; i > 0; i--)
                if ((pts[i] - pts[i - 1]).sqrMagnitude < 1e-4f)
                    pts.RemoveAt(i);
            if (clockwise)
                pts.Reverse();
            return pts;
        }
    }
}
