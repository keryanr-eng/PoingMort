using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PoingMort.EditorTools;
using PoingMort.Traffic;
using UnityEngine;

namespace PoingMort.Tests
{
    /// <summary>
    /// Checks of the generated neighbourhood (pure geometry, no scene needed): nothing overlaps, street
    /// furniture stays off the road, facades face the street, lanes and cars sit where the traffic expects them.
    /// </summary>
    public class LayoutTests
    {
        static QuartierLayout s_Layout;
        static QuartierLayout Layout => s_Layout ??= QuartierLayout.Create();

        // Footprints in asset space (centre offset, half extents on X/Z) and height.
        static readonly Dictionary<string, (Vector2 c, Vector2 h, float height)> Props = new Dictionary<string, (Vector2, Vector2, float)>
        {
            { "Prop_StreetLamp", (Vector2.zero, new Vector2(0.13f, 0.13f), 5.5f) },
            { "Prop_Floodlight", (Vector2.zero, new Vector2(0.1f, 0.1f), 4.9f) },
            { "Prop_Bench", (Vector2.zero, new Vector2(0.95f, 0.25f), 0.85f) },
            { "Prop_TrashBin", (Vector2.zero, new Vector2(0.31f, 0.31f), 0.91f) },
            { "Prop_Tree", (Vector2.zero, new Vector2(0.7f, 0.7f), 5.2f) },
            { "Prop_Hydrant", (Vector2.zero, new Vector2(0.2f, 0.16f), 0.7f) },
            { "Prop_Bollard", (Vector2.zero, new Vector2(0.1f, 0.1f), 1f) },
            { "Prop_Barrier", (Vector2.zero, new Vector2(1.03f, 0.04f), 1.05f) },
            { "Prop_Fence", (Vector2.zero, new Vector2(1.54f, 0.04f), 2.2f) },
            { "Prop_Crate", (Vector2.zero, new Vector2(0.47f, 0.47f), 0.8f) },
            { "Prop_Dumpster", (Vector2.zero, new Vector2(1.0f, 0.6f), 1.28f) },
            { "Prop_SignOneWay", (Vector2.zero, new Vector2(0.06f, 0.06f), 2.6f) },
            { "Prop_Wall", (Vector2.zero, new Vector2(2.0f, 0.15f), 3.55f) },
            { "Prop_BusStop", (new Vector2(0f, -0.15f), new Vector2(2.1f, 0.55f), 2.6f) },
        };

        sealed class Box
        {
            public QuartierLayout.Placement p;
            public Vector2[] corners;
            public float y0, y1;
            public bool IsBuilding => p.asset.StartsWith("Bld_");
            public override string ToString() => $"{p.asset} ({p.position.x:0.0}, {p.position.z:0.0})";
        }

        static Vector2 Rotate(Vector2 v, float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c); // Unity yaw around +Y
        }

        static Box Footprint(QuartierLayout.Placement p)
        {
            Vector2 c, h;
            float height;
            if (p.asset.StartsWith("Bld_Tower"))
            {
                var size = p.asset == "Bld_Tower1" ? new Vector2(16f, 14f) : p.asset == "Bld_Tower2" ? new Vector2(12f, 12f) : new Vector2(18f, 14f);
                (c, h, height) = (new Vector2(0f, -size.y / 2f), size / 2f, 30f);
            }
            else if (p.asset.StartsWith("Bld_"))
                (c, h, height) = (new Vector2(0f, -5f), new Vector2(QuartierLayout.Widths[p.asset] / 2f, 5f), 12f);
            else
                (c, h, height) = Props[p.asset];
            h.x *= p.scale.x;
            c.x *= p.scale.x;
            var local = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
            var origin = new Vector2(p.position.x, p.position.z);
            return new Box { p = p, corners = local.Select(k => Rotate(c + k, p.yaw) + origin).ToArray(), y0 = p.position.y, y1 = p.position.y + height };
        }

        /// <summary>Overlap depth of two convex quads (separating axis test), 0 when apart.</summary>
        static float Penetration(Vector2[] a, Vector2[] b)
        {
            float min = float.MaxValue;
            foreach (var poly in new[] { a, b })
                for (int i = 0; i < 4; i++)
                {
                    Vector2 e = poly[(i + 1) % 4] - poly[i];
                    Vector2 n = new Vector2(-e.y, e.x).normalized;
                    float overlap = Mathf.Min(a.Max(p => Vector2.Dot(p, n)), b.Max(p => Vector2.Dot(p, n))) -
                                    Mathf.Max(a.Min(p => Vector2.Dot(p, n)), b.Min(p => Vector2.Dot(p, n)));
                    if (overlap <= 0f) return 0f;
                    min = Mathf.Min(min, overlap);
                }
            return min;
        }

        /// <summary>Signed distance from the street centreline (negative inside the block).</summary>
        static float Offset(Vector3 p)
        {
            var path = Layout.path;
            float best = float.MaxValue, offset = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path.points[i], ab = path.points[i + 1] - a;
                Vector3 ap = p - a;
                ab.y = 0f;
                ap.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                Vector3 d = ap - ab * t;
                if (d.magnitude < best)
                {
                    best = d.magnitude;
                    offset = Vector3.Dot(d, Vector3.Lerp(path.normals[i], path.normals[i + 1], t).normalized);
                }
            }
            return offset;
        }

        static List<Box> Boxes() => Layout.placements.Select(Footprint).ToList();

        [Test]
        public void LayoutBuildsWithoutWarnings()
        {
            Assert.That(Layout.warnings, Is.Empty, string.Join("\n", Layout.warnings));
            Assert.That(Layout.placements.Count, Is.GreaterThan(100));
        }

        [Test]
        public void NothingOverlaps()
        {
            var boxes = Boxes();
            var problems = new List<string>();
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Box a = boxes[i], b = boxes[j];
                    if (a.y1 <= b.y0 || b.y1 <= a.y0) continue;
                    // Block corners: perpendicular buildings share their hidden interiors on purpose.
                    if (a.IsBuilding && b.IsBuilding && Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(a.p.yaw, b.p.yaw)) - 90f) < 1f) continue;
                    bool walls = (a.IsBuilding || a.p.asset == "Prop_Wall") && (b.IsBuilding || b.p.asset == "Prop_Wall");
                    float pen = Penetration(a.corners, b.corners);
                    if (pen > (walls ? 0.35f : 0f)) problems.Add($"{a} / {b} : {pen:0.00} m");
                }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void StreetFurnitureStaysOffTheRoadAndBuildingsOffTheSidewalk()
        {
            var problems = new List<string>();
            foreach (var b in Boxes().Where(b => b.p.group != "Horizon"))
                foreach (var c in b.corners)
                {
                    float off = Mathf.Abs(Offset(new Vector3(c.x, 0f, c.y)));
                    if (off < GroundGeometry.RoadHalf) problems.Add($"{b} sur la chaussée");
                    if (b.IsBuilding && off < GroundGeometry.SidewalkOuter - 0.05f) problems.Add($"{b} empiète sur le trottoir");
                }
            Assert.That(problems.Distinct(), Is.Empty, string.Join("\n", problems.Distinct()));
        }

        [Test]
        public void StreetSideAssetsFaceTheStreet()
        {
            var problems = new List<string>();
            foreach (var p in Layout.placements)
            {
                bool streetSide = p.asset.StartsWith("Bld_") && p.group == "Bâtiments" || p.asset == "Prop_Bench" || p.asset == "Prop_BusStop" ||
                                  p.asset == "Prop_StreetLamp" && p.group == "Éclairage" && Mathf.Abs(Offset(p.position)) < 8f;
                if (!streetSide) continue;
                Vector3 front = QuartierLayout.DirectionFromYaw(p.yaw);
                if (Mathf.Abs(Offset(p.position + front * 0.5f)) > Mathf.Abs(Offset(p.position))) problems.Add($"{p.asset} ({p.position.x:0.0}, {p.position.z:0.0})");
            }
            Assert.That(problems, Is.Empty, "Tournés vers l'arrière :\n" + string.Join("\n", problems));
        }

        [Test]
        public void LanesFollowTheRoadClockwise()
        {
            foreach (var (lane, expected) in new[] { (Layout.innerLane, -QuartierLayout.LaneOffset), (Layout.outerLane, QuartierLayout.LaneOffset) })
            {
                Assert.That(lane.Max(p => Mathf.Abs(Offset(p) - expected)), Is.LessThan(0.15f));
                var loop = new LaneLoop(lane);
                loop.Evaluate(loop.ClosestDistance(new Vector3(0f, 0f, -QuartierLayout.HalfZ + expected)), out _, out var tangent);
                Assert.That(tangent.x, Is.LessThan(-0.9f), "Sur la ligne droite sud, la circulation va vers -X (sens horaire).");
            }
        }

        [Test]
        public void CarsStartOnLanesSpacedAndAwayFromThePlayer()
        {
            var cars = Layout.cars;
            Assert.That(cars.Count, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < cars.Count; i++)
            {
                Assert.That(Mathf.Abs(Mathf.Abs(Offset(cars[i].position)) - QuartierLayout.LaneOffset), Is.LessThan(0.2f), $"voiture {i} hors voie");
                Assert.That(Vector3.Distance(cars[i].position, Layout.playerSpawn), Is.GreaterThan(12f), $"voiture {i} trop proche du départ");
                for (int j = 0; j < i; j++)
                    Assert.That(Vector3.Distance(cars[i].position, cars[j].position), Is.GreaterThan(15f), $"voitures {i} et {j} trop proches");
            }
        }

        [Test]
        public void SpawnPointsAndFightAreaAreFree()
        {
            var boxes = Boxes();
            foreach (var (what, p, r) in new[] { ("départ joueur", Layout.playerSpawn, 0.6f), ("centre du combat", Layout.arenaCentre, 5.5f),
                         ("départ combat joueur", Layout.playerStart, 0.6f), ("départ adversaire", Layout.opponentStart, 0.6f), ("personnage d'accueil", Layout.menuCharacter, 0.6f) })
            {
                var square = new[] { new Vector2(p.x - r, p.z - r), new Vector2(p.x + r, p.z - r), new Vector2(p.x + r, p.z + r), new Vector2(p.x - r, p.z + r) };
                var blocking = boxes.Where(b => Penetration(square, b.corners) > 0f).Select(b => b.ToString()).ToList();
                Assert.That(blocking, Is.Empty, $"{what} encombré : " + string.Join(", ", blocking));
            }
            float spawn = Offset(Layout.playerSpawn);
            Assert.That(spawn, Is.InRange(-GroundGeometry.SidewalkOuter + 0.3f, -(GroundGeometry.RoadHalf + GroundGeometry.CurbWidth) - 0.3f), "départ sur le trottoir de l'îlot");
        }

        [Test]
        public void GroundMeshesFaceUpAndHaveNoDegenerateTriangle()
        {
            foreach (var piece in GroundGeometry.Pieces(Layout.path))
            {
                var m = piece.mesh;
                Assert.That(m.Empty, Is.False, piece.assetId);
                for (int t = 0; t < m.t.Count; t += 3)
                {
                    Vector3 a = m.v[m.t[t]], b = m.v[m.t[t + 1]], c = m.v[m.t[t + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    Assert.That(n.magnitude, Is.GreaterThan(1e-6f), $"triangle dégénéré dans {piece.assetId}");
                    if (Mathf.Abs(n.normalized.y) > 0.5f) Assert.That(n.y, Is.GreaterThan(0f), $"triangle retourné dans {piece.assetId}");
                }
            }
        }

        [Test]
        public void LoopPathIsClosedAndMatchesTheRoundedRectangle()
        {
            var path = Layout.path;
            Assert.That(Vector3.Distance(path.points[0], path.points[path.Count - 1]), Is.LessThan(1e-3f));
            float expected = 2f * (2f * QuartierLayout.HalfX - 2f * QuartierLayout.CornerRadius) + 2f * (2f * QuartierLayout.HalfZ - 2f * QuartierLayout.CornerRadius) +
                             2f * Mathf.PI * QuartierLayout.CornerRadius;
            Assert.That(path.Length, Is.EqualTo(expected).Within(0.3f));
        }
    }
}
