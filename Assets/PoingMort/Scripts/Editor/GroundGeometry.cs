using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Closed rounded-rectangle centreline of the street, sampled densely, with outward normals.
    /// Offsets are measured along the normal: negative toward the inside of the block.
    /// </summary>
    public sealed class LoopPath
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<float> arc = new List<float>();
        public readonly float halfX, halfZ, radius;
        public float Length { get; private set; }

        public LoopPath(float halfX, float halfZ, float radius, float straightStep = 3f, int cornerSegments = 24)
        {
            this.halfX = halfX;
            this.halfZ = halfZ;
            this.radius = radius;
            Vector2[] centres =
            {
                new Vector2(halfX - radius, -halfZ + radius),
                new Vector2(halfX - radius, halfZ - radius),
                new Vector2(-halfX + radius, halfZ - radius),
                new Vector2(-halfX + radius, -halfZ + radius),
            };
            float[] start = { -90f, 0f, 90f, 180f };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float a = (start[c] + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Add(new Vector3(centres[c].x, 0f, centres[c].y) + n * radius, n);
                }
                // Straight toward the next corner
                int next = (c + 1) % 4;
                float an = start[next] * Mathf.Deg2Rad;
                var nn = new Vector3(Mathf.Cos(an), 0f, Mathf.Sin(an));
                Vector3 from = points[points.Count - 1];
                Vector3 to = new Vector3(centres[next].x, 0f, centres[next].y) + nn * radius;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / straightStep));
                for (int k = 1; k < steps; k++)
                    Add(Vector3.Lerp(from, to, k / (float)steps), nn);
            }
            // Close the loop with a duplicate of the first sample (distinct arc length for the UVs).
            Add(points[0], normals[0]);
        }

        void Add(Vector3 p, Vector3 n)
        {
            if (points.Count > 0)
            {
                float d = Vector3.Distance(points[points.Count - 1], p);
                if (d < 1e-4f) return;
                Length += d;
            }
            points.Add(p);
            normals.Add(n.normalized);
            arc.Add(Length);
        }

        public int Count => points.Count;

        public Vector3 Offset(int i, float d) => points[i] + normals[i] * d;

        /// <summary>Position and outward normal at centreline arc length s.</summary>
        public void Sample(float s, out Vector3 p, out Vector3 n)
        {
            s = Mathf.Repeat(s, Length);
            int lo = 0, hi = arc.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (arc[mid] <= s) lo = mid; else hi = mid;
            }
            float t = Mathf.InverseLerp(arc[lo], arc[hi], s);
            p = Vector3.Lerp(points[lo], points[hi], t);
            n = Vector3.Lerp(normals[lo], normals[hi], t).normalized; // samples are close: normalised lerp is enough
        }

        /// <summary>Tangent (direction of increasing arc length) at s.</summary>
        public Vector3 Tangent(float s)
        {
            Sample(s - 0.25f, out var a, out _);
            Sample(s + 0.25f, out var b, out _);
            var t = b - a;
            t.y = 0f;
            return t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// Pure geometry of the neighbourhood ground (no Unity editor or native call, so it can be checked
    /// outside Unity): asphalt ring, curbs, sidewalks, block fills, road markings and zebra crossing.
    /// Heights: road at y = 0, curbs/sidewalks/blocks at y = 0.15.
    /// </summary>
    public static class GroundGeometry
    {
        public const float RoadHalf = 3.8f;
        public const float CurbWidth = 0.3f;
        public const float SidewalkOuter = 7.3f;
        public const float Kerb = 0.15f;
        const float ChunkLength = 32f;

        public sealed class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public int Vertex(Vector3 p, Vector2 uv0)
            {
                v.Add(p);
                uv.Add(uv0);
                return v.Count - 1;
            }

            /// <summary>Adds a triangle whose front face points toward <paramref name="facing"/>.</summary>
            public void Tri(int a, int b, int c, Vector3 facing)
            {
                Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(n, facing) < 0f) { int tmp = b; b = c; c = tmp; }
                t.Add(a); t.Add(b); t.Add(c);
            }

            public void Quad(int a, int b, int c, int d, Vector3 facing)
            {
                Tri(a, b, c, facing);
                Tri(c, d, a, facing);
            }

            public bool Empty => t.Count == 0;
        }

        /// <summary>Arc length (centreline) of the crosswalk: on the south straight, just west of the alley.</summary>
        public static float CrosswalkArc(LoopPath path) => ClosestArc(path, new Vector3(-9f, 0f, -path.halfZ));

        public static float ClosestArc(LoopPath path, Vector3 p)
        {
            float best = float.MaxValue, bestS = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path.points[i] - p).sqrMagnitude;
                if (d < best) { best = d; bestS = path.arc[i]; }
            }
            return bestS;
        }

        public static IEnumerable<(int from, int to)> Chunks(LoopPath path)
        {
            int start = 0;
            for (int i = 1; i < path.Count; i++)
            {
                if (path.arc[i] - path.arc[start] >= ChunkLength || i == path.Count - 1)
                {
                    yield return (start, i);
                    start = i;
                }
            }
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Horizontal strip between two offsets. UVs: across = offset / tileU, along = arc length at mid offset / tileV.</summary>
        public static MeshData Strip(LoopPath path, (int from, int to) range, float d0, float d1, float y, float tileU, float tileV, bool planarUv = false)
        {
            var m = new MeshData();
            float mid = (d0 + d1) * 0.5f;
            float s = ArcAtOffset(path, range.from, mid);
            int prevA = -1, prevB = -1;
            Vector3 prevMid = Vector3.zero;
            for (int i = range.from; i <= range.to; i++)
            {
                Vector3 pm = path.Offset(i, mid);
                if (i > range.from) s += Vector3.Distance(prevMid, pm);
                prevMid = pm;
                Vector3 a = path.Offset(i, d0) + Vector3.up * y;
                Vector3 b = path.Offset(i, d1) + Vector3.up * y;
                Vector2 uvA = planarUv ? new Vector2(a.x / tileU, a.z / tileV) : new Vector2(d0 / tileU, s / tileV);
                Vector2 uvB = planarUv ? new Vector2(b.x / tileU, b.z / tileV) : new Vector2(d1 / tileU, s / tileV);
                int ia = m.Vertex(a, uvA);
                int ib = m.Vertex(b, uvB);
                if (prevA >= 0) m.Quad(prevA, prevB, ib, ia, Vector3.up);
                prevA = ia;
                prevB = ib;
            }
            return m;
        }

        /// <summary>Arc length from the start of the loop to sample i, measured along the given offset.</summary>
        public static float ArcAtOffset(LoopPath path, int index, float offset)
        {
            float s = 0f;
            for (int i = 1; i <= index; i++)
                s += Vector3.Distance(path.Offset(i - 1, offset), path.Offset(i, offset));
            return s;
        }

        public static MeshData Curbs(LoopPath path, (int from, int to) range)
        {
            var m = new MeshData();
            foreach (float side in new[] { -1f, 1f })
            {
                float face = side * RoadHalf;
                float back = side * (RoadHalf + CurbWidth);
                float s = ArcAtOffset(path, range.from, face);
                int pBottom = -1, pTop = -1, pTop2 = -1, pTop3 = -1;
                Vector3 prev = Vector3.zero;
                for (int i = range.from; i <= range.to; i++)
                {
                    Vector3 f = path.Offset(i, face);
                    if (i > range.from) s += Vector3.Distance(prev, f);
                    prev = f;
                    Vector3 toRoad = -path.normals[i] * side;
                    // Vertical face (toward the road) with its own vertices for a hard edge.
                    int bottom = m.Vertex(f, new Vector2(0f, s / 2f));
                    int top = m.Vertex(f + Vector3.up * Kerb, new Vector2(Kerb / 2f, s / 2f));
                    // Top face
                    int top2 = m.Vertex(f + Vector3.up * Kerb, new Vector2(0.5f, s / 2f));
                    int top3 = m.Vertex(path.Offset(i, back) + Vector3.up * Kerb, new Vector2(0.5f + CurbWidth / 2f, s / 2f));
                    if (pBottom >= 0)
                    {
                        m.Quad(pBottom, bottom, top, pTop, toRoad);
                        m.Quad(pTop2, top2, top3, pTop3, Vector3.up);
                    }
                    pBottom = bottom; pTop = top; pTop2 = top2; pTop3 = top3;
                }
            }
            return m;
        }

        public static MeshData InnerFill(LoopPath path, float offset, float y, float tile)
        {
            var m = new MeshData();
            int centre = m.Vertex(new Vector3(0f, y, 0f), Vector2.zero);
            int first = -1, prev = -1;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 p = path.Offset(i, offset) + Vector3.up * y;
                int v = m.Vertex(p, new Vector2(p.x / tile, p.z / tile));
                if (prev >= 0) m.Tri(centre, prev, v, Vector3.up);
                else first = v;
                prev = v;
            }
            m.Tri(centre, prev, first, Vector3.up);
            return m;
        }

        public static MeshData Markings(LoopPath path, (int from, int to) range)
        {
            var m = new MeshData();
            const float y = 0.012f;
            float s0 = path.arc[range.from], s1 = path.arc[range.to];
            // Edge lines (continuous) at the outer limit of each lane.
            foreach (float d in new[] { -3.42f, 3.42f })
                Band(m, path, s0, s1, d - 0.06f, d + 0.06f, y);
            // Lane separator: 3 m dashes every 8 m (both lanes run the same way).
            const float period = 8f, dash = 3f;
            float crossing = CrosswalkArc(path);
            for (float s = Mathf.Floor(s0 / period) * period; s < s1; s += period)
            {
                float a = Mathf.Max(s, s0), b = Mathf.Min(s + dash, s1);
                if (b - a < 0.05f) continue;
                float fromCrossing = Mathf.Abs(Mathf.Repeat(a - crossing + path.Length * 0.5f, path.Length) - path.Length * 0.5f);
                if (fromCrossing < 4f) continue; // no dash across the zebra crossing
                Band(m, path, a, b, -0.07f, 0.07f, y);
            }
            return m;
        }

        /// <summary>Strip following the path between arc lengths a and b (centreline arc) and two offsets.</summary>
        public static void Band(MeshData m, LoopPath path, float a, float b, float d0, float d1, float y)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((b - a) / 0.75f));
            int pa = -1, pb = -1;
            for (int k = 0; k <= steps; k++)
            {
                float s = Mathf.Lerp(a, b, k / (float)steps);
                path.Sample(s, out var p, out var n);
                int ia = m.Vertex(p + n * d0 + Vector3.up * y, new Vector2(0f, s));
                int ib = m.Vertex(p + n * d1 + Vector3.up * y, new Vector2(1f, s));
                if (pa >= 0) m.Quad(pa, pb, ib, ia, Vector3.up);
                pa = ia;
                pb = ib;
            }
        }

        public static MeshData Crosswalk(LoopPath path, float s)
        {
            var m = new MeshData();
            const float y = 0.013f, stripe = 0.5f, gap = 0.5f, length = 2.6f;
            for (float d = -RoadHalf + 0.45f; d + stripe <= RoadHalf - 0.35f; d += stripe + gap)
                Band(m, path, s - length / 2f, s + length / 2f, d, d + stripe, y);
            return m;
        }

        /// <summary>One generated ground mesh: name in the scene, asset id, material key and collision.</summary>
        public readonly struct Piece
        {
            public readonly string name, assetId, material;
            public readonly MeshData mesh;
            public readonly bool collider;

            public Piece(string name, string assetId, string material, MeshData mesh, bool collider)
            {
                this.name = name;
                this.assetId = assetId;
                this.material = material;
                this.mesh = mesh;
                this.collider = collider;
            }
        }

        /// <summary>
        /// Every ground piece, split in ~30 m sections along the loop so that each section only receives
        /// the lamps near it (per-object light limit of forward rendering).
        /// </summary>
        public static List<Piece> Pieces(LoopPath path)
        {
            var list = new List<Piece>();
            int chunk = 0;
            foreach (var range in Chunks(path))
            {
                string n = chunk.ToString("00");
                list.Add(new Piece("Chaussée " + n, "Road_" + n, "asphalt", Strip(path, range, -RoadHalf, RoadHalf, 0f, 5f, 5f), true));
                list.Add(new Piece("Trottoir intérieur " + n, "SidewalkIn_" + n, "sidewalk", Strip(path, range, -SidewalkOuter, -(RoadHalf + CurbWidth), Kerb, 2f, 2f), true));
                list.Add(new Piece("Trottoir extérieur " + n, "SidewalkOut_" + n, "sidewalk", Strip(path, range, RoadHalf + CurbWidth, SidewalkOuter, Kerb, 2f, 2f), true));
                list.Add(new Piece("Bordures " + n, "Curbs_" + n, "curb", Curbs(path, range), true));
                list.Add(new Piece("Marquage " + n, "Markings_" + n, "paint", Markings(path, range), false));
                chunk++;
            }
            list.Add(new Piece("Cœur d'îlot", "BlockFill", "gravel", InnerFill(path, -SidewalkOuter, Kerb, 3f), true));
            list.Add(new Piece("Terrain extérieur", "OuterGround", "concrete", Strip(path, (0, path.Count - 1), SidewalkOuter, SidewalkOuter + 140f, Kerb, 3f, 3f, planarUv: true), true));
            list.Add(new Piece("Passage piéton", "Crosswalk", "paint", Crosswalk(path, CrosswalkArc(path)), false));
            return list;
        }

    }
}
