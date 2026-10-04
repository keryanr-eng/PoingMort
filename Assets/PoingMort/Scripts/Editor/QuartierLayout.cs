using System.Collections.Generic;
using PoingMort.Traffic;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Layout of the P01 neighbourhood, computed with plain maths (no Unity editor or native call) so it can
    /// be checked outside Unity. A one-way street loops around a block (rounded rectangle 120 x 52 m on its
    /// centreline, two lanes, clockwise traffic). The block holds two rows of buildings back to back with a
    /// courtyard between them, reached by an alley: that is where the street fight takes place.
    /// Asset axes: buildings have their facade at z = 0 facing +Z; lamps, benches and the bus stop face +Z.
    /// </summary>
    public sealed class QuartierLayout
    {
        // Street centreline
        public const float HalfX = 60f, HalfZ = 26f, CornerRadius = 12f;
        public const float LaneOffset = 1.75f;
        public const float Ground = GroundGeometry.Kerb;
        public const float Facade = GroundGeometry.SidewalkOuter; // building line, from the centreline

        public sealed class Placement
        {
            public string asset;
            public string group;
            public Vector3 position;
            public float yaw;
            public Vector3 scale = Vector3.one;
            public string label;
        }

        public sealed class CarSpawn
        {
            public Vector3 position;
            public float yaw;
            public int paint;
        }

        public readonly LoopPath path = new LoopPath(HalfX, HalfZ, CornerRadius);
        public readonly List<Placement> placements = new List<Placement>();
        public List<Vector3> innerLane, outerLane;
        public readonly List<CarSpawn> cars = new List<CarSpawn>();
        /// <summary>Layout problems found while building (reported by the scene builder).</summary>
        public readonly List<string> warnings = new List<string>();

        // On the block's sidewalk, facing the oncoming cars (road on the right, courtyard alley ahead-left),
        // with open sidewalk behind so the camera is not squeezed against a facade.
        public Vector3 playerSpawn = new Vector3(-6f, Ground, -21.2f);
        public float playerYaw = 90f;
        public Vector3 arenaCentre = new Vector3(0f, Ground, 0f);
        public Vector3 playerStart = new Vector3(0f, Ground, -3f);
        public Vector3 opponentStart = new Vector3(0f, Ground, 3f);
        public float courtyardHalfX = 14f;
        public float alleyHalfWidth = 3f;

        /// <summary>Invisible walls around the playable area (centre, size).</summary>
        public readonly List<(Vector3 centre, Vector3 size)> boundaries = new List<(Vector3, Vector3)>();

        // Home screen framing: on the shops' sidewalk, looking along the street toward the oncoming cars;
        // the character stands on the right third (the menu column covers the left of the screen).
        public Vector3 menuCamera = new Vector3(-16f, 1.6f, -30.55f);
        public Vector3 menuLookAt = new Vector3(-2f, 1.35f, -31.2f);
        public float menuFov = 40f;
        public Vector3 menuCharacter = new Vector3(-9.6f, Ground, -32.35f);
        public float menuCharacterYaw = -62f;

        public static readonly Dictionary<string, float> Widths = new Dictionary<string, float>
        {
            { "Bld_Epicerie", 12f }, { "Bld_Laverie", 10f }, { "Bld_Cafe", 9f }, { "Bld_Residential", 14f },
            { "Bld_ResidentialB", 11f }, { "Bld_Garage", 12f }, { "Bld_Plain", 8f },
        };

        public static QuartierLayout Create()
        {
            var l = new QuartierLayout();
            l.Build();
            return l;
        }

        public static float YawFromDirection(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        public static Vector3 DirectionFromYaw(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        void Build()
        {
            Lanes();
            Buildings();
            Corners();
            Courtyard();
            StreetFurniture();
            Skyline();
            Cars();
            Boundaries();
        }

        // ------------------------------------------------------------------ lanes and cars

        void Lanes()
        {
            // Clockwise seen from above: the right-hand lane is the inner one (kerb on the right).
            innerLane = LaneLoop.RoundedRectangle(Vector3.zero, HalfX - LaneOffset, HalfZ - LaneOffset, CornerRadius - LaneOffset, 10, true);
            outerLane = LaneLoop.RoundedRectangle(Vector3.zero, HalfX + LaneOffset, HalfZ + LaneOffset, CornerRadius + LaneOffset, 10, true);
        }

        void Cars()
        {
            var inner = new LaneLoop(innerLane);
            var outer = new LaneLoop(outerLane);
            // First car comes toward the spawn point a few seconds after the start.
            AddCar(inner, new Vector3(26f, 0f, -24.25f), 0);
            AddCar(inner, new Vector3(-30f, 0f, 24.25f), 2);
            AddCar(inner, new Vector3(58.25f, 0f, 4f), 3);
            AddCar(outer, new Vector3(-24f, 0f, -27.75f), 1);
            AddCar(outer, new Vector3(18f, 0f, 27.75f), 4);
        }

        void AddCar(LaneLoop lane, Vector3 near, int paint)
        {
            float s = lane.ClosestDistance(near);
            lane.Evaluate(s, out var p, out var t);
            cars.Add(new CarSpawn { position = p, yaw = YawFromDirection(lane.SmoothTangent(s, 2f)), paint = paint });
        }

        // ------------------------------------------------------------------ buildings

        void Add(string asset, string group, Vector3 pos, float yaw, float scaleX = 1f, string label = null)
        {
            placements.Add(new Placement { asset = asset, group = group, position = pos, yaw = yaw, scale = new Vector3(scaleX, 1f, 1f), label = label });
        }

        /// <summary>
        /// Fills the facade line from a to b (on the ground) with buildings facing <paramref name="facing"/>.
        /// Widths are scaled by a common factor (a few percent) so the row ends exactly at b.
        /// </summary>
        void Row(Vector3 a, Vector3 b, Vector3 facing, params string[] names)
        {
            float total = 0f;
            foreach (var n in names) total += Widths[n];
            float length = Vector3.Distance(a, b);
            float k = length / total;
            if (k < 0.88f || k > 1.12f)
                warnings.Add($"Rangée de bâtiments trop étirée ({k:0.00}) entre {a} et {b}.");
            Vector3 u = (b - a).normalized;
            float yaw = YawFromDirection(facing);
            float x = 0f;
            foreach (var n in names)
            {
                float w = Widths[n] * k;
                Add(n, "Bâtiments", a + u * (x + w / 2f), yaw, k);
                x += w;
            }
        }

        void Buildings()
        {
            float zi = HalfZ - Facade;          // 18.7: inner building line (north/south)
            float xi = HalfX - Facade;          // 52.7
            float zo = HalfZ + Facade;          // 33.3: outer building line
            float xo = HalfX + Facade;          // 67.3
            float s = HalfX - CornerRadius;     // 48: end of the straights
            float e = HalfZ - CornerRadius;     // 14
            float y = Ground;
            var N = Vector3.forward;
            var S = Vector3.back;
            var E = Vector3.right;
            var W = Vector3.left;

            // Across the street from the spawn point: the shops (first thing the player sees).
            Row(new Vector3(-s, y, -zo), new Vector3(s, y, -zo), N,
                "Bld_Residential", "Bld_Cafe", "Bld_ResidentialB", "Bld_Epicerie", "Bld_Residential", "Bld_Laverie", "Bld_Garage", "Bld_Residential");
            // Player side (south row of the block), split by the alley leading to the courtyard.
            Row(new Vector3(-s, y, -zi), new Vector3(-alleyHalfWidth, y, -zi), S,
                "Bld_Residential", "Bld_ResidentialB", "Bld_Plain", "Bld_ResidentialB");
            Row(new Vector3(alleyHalfWidth, y, -zi), new Vector3(s, y, -zi), S,
                "Bld_Plain", "Bld_ResidentialB", "Bld_Residential", "Bld_ResidentialB");
            // North row of the block (its backs close the courtyard).
            Row(new Vector3(-s, y, zi), new Vector3(s, y, zi), N,
                "Bld_Residential", "Bld_Plain", "Bld_ResidentialB", "Bld_Plain", "Bld_Residential", "Bld_ResidentialB", "Bld_Plain", "Bld_Residential", "Bld_Plain");
            Row(new Vector3(-s, y, zo), new Vector3(s, y, zo), S,
                "Bld_ResidentialB", "Bld_Residential", "Bld_Plain", "Bld_Residential", "Bld_ResidentialB", "Bld_Plain", "Bld_Residential", "Bld_ResidentialB");
            // Short sides
            Row(new Vector3(xi, y, -e), new Vector3(xi, y, e), E, "Bld_Plain", "Bld_ResidentialB", "Bld_Plain");
            Row(new Vector3(-xi, y, e), new Vector3(-xi, y, -e), W, "Bld_ResidentialB", "Bld_Plain", "Bld_Plain");
            Row(new Vector3(xo, y, e), new Vector3(xo, y, -e), W, "Bld_Plain", "Bld_Residential", "Bld_Plain");
            Row(new Vector3(-xo, y, -e), new Vector3(-xo, y, e), E, "Bld_ResidentialB", "Bld_Plain", "Bld_ResidentialB");
        }

        /// <summary>Outer corners: concrete walls following the curve of the sidewalk, a tree and a lamp.</summary>
        void Corners()
        {
            Vector2[] centres = { new Vector2(HalfX - CornerRadius, HalfZ - CornerRadius), new Vector2(-(HalfX - CornerRadius), HalfZ - CornerRadius),
                                  new Vector2(-(HalfX - CornerRadius), -(HalfZ - CornerRadius)), new Vector2(HalfX - CornerRadius, -(HalfZ - CornerRadius)) };
            float[] start = { 0f, 90f, 180f, 270f };
            float wallRadius = CornerRadius + Facade + 0.3f;
            for (int c = 0; c < 4; c++)
            {
                var centre = new Vector3(centres[c].x, Ground, centres[c].y);
                const int segments = 8;
                for (int k = 0; k < segments; k++)
                {
                    float a = (start[c] + 90f * (k + 0.5f) / segments) * Mathf.Deg2Rad;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Add("Prop_Wall", "Coins", centre + radial * wallRadius, YawFromDirection(-radial), 1.02f);
                }
                float mid = (start[c] + 45f) * Mathf.Deg2Rad;
                var diag = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                Add("Prop_Tree", "Coins", centre + diag * (CornerRadius + 5.6f), 0f);
                float lampA = (start[c] + 20f) * Mathf.Deg2Rad;
                var lampDir = new Vector3(Mathf.Cos(lampA), 0f, Mathf.Sin(lampA));
                Add("Prop_StreetLamp", "Éclairage", centre + lampDir * (CornerRadius + 4.55f), YawFromDirection(-lampDir));
                // Inner corner pocket between the block's buildings
                Add("Prop_Tree", "Coins", centre + diag * (CornerRadius - Facade - 2.2f), 0f);
            }
        }

        // ------------------------------------------------------------------ courtyard (fight)

        void Courtyard()
        {
            float zb = HalfZ - Facade - 10f; // 8.7: backs of the two rows
            const string g = "Cour";
            // Side walls closing the courtyard (between the backs of the buildings).
            foreach (float side in new[] { -1f, 1f })
            {
                const int n = 5;
                float len = 2f * zb / n;
                for (int k = 0; k < n; k++)
                {
                    float z = -zb + len * (k + 0.5f);
                    Add("Prop_Wall", g, new Vector3(side * courtyardHalfX, Ground, z), side > 0 ? -90f : 90f, len / 4f + 0.02f);
                }
            }
            // Dressing kept along the walls: the central 12 m are free for the fight.
            Add("Prop_Dumpster", g, new Vector3(-10.6f, Ground, zb - 0.75f), 180f);
            Add("Prop_Dumpster", g, new Vector3(9.8f, Ground, -zb + 0.75f), 0f);
            Add("Prop_Crate", g, new Vector3(12.9f, Ground, 4.2f), 8f);
            Add("Prop_Crate", g, new Vector3(12.95f, Ground, 3.1f), -12f);
            Add("Prop_Crate", g, new Vector3(12.9f, Ground + 0.8f, 4.15f), 25f);
            Add("Prop_Crate", g, new Vector3(-12.7f, Ground, -5.6f), 4f);
            Add("Prop_Barrier", g, new Vector3(-6.5f, Ground, zb - 0.45f), 180f);
            Add("Prop_Barrier", g, new Vector3(6.2f, Ground, -zb + 0.45f), 0f);
            Add("Prop_Barrier", g, new Vector3(-13.4f, Ground, 3.5f), 90f);
            Add("Prop_TrashBin", g, new Vector3(4.1f, Ground, zb - 0.45f), 0f);
            // Floodlights in two corners, aimed at the centre.
            AddAimed("Prop_Floodlight", g, new Vector3(-13.1f, Ground, -zb + 0.6f), arenaCentre);
            AddAimed("Prop_Floodlight", g, new Vector3(13.1f, Ground, zb - 0.6f), arenaCentre);
            // Alley: a lamp so the passage reads as one, bins along the walls.
            AddAimed("Prop_StreetLamp", "Éclairage", new Vector3(-2.55f, Ground, -13.6f), new Vector3(0f, Ground, -13.6f));
            Add("Prop_TrashBin", g, new Vector3(2.45f, Ground, -16.9f), 0f);
            Add("Prop_TrashBin", g, new Vector3(2.45f, Ground, -11.4f), 0f);
        }

        void AddAimed(string asset, string group, Vector3 pos, Vector3 target)
        {
            var d = target - pos;
            d.y = 0f;
            Add(asset, group, pos, YawFromDirection(d));
        }

        // ------------------------------------------------------------------ street furniture

        /// <summary>Point on a straight at abscissa t (metres along it) and signed offset from the centreline.</summary>
        static Vector3 OnStraight(int straight, float t, float offset, out Vector3 outward, out Vector3 along)
        {
            switch (straight)
            {
                case 0: outward = Vector3.back; along = Vector3.left; return new Vector3(t, Ground, -HalfZ - offset);      // south
                case 1: outward = Vector3.forward; along = Vector3.right; return new Vector3(t, Ground, HalfZ + offset);   // north
                case 2: outward = Vector3.right; along = Vector3.back; return new Vector3(HalfX + offset, Ground, t);      // east
                default: outward = Vector3.left; along = Vector3.forward; return new Vector3(-HalfX - offset, Ground, t);  // west
            }
        }

        void Prop(string asset, int straight, float t, float offset, float extraYaw = 0f, string group = "Mobilier")
        {
            var p = OnStraight(straight, t, offset, out var outward, out _);
            var toRoad = offset < 0f ? outward : -outward;
            Add(asset, group, p, YawFromDirection(toRoad) + extraYaw);
        }

        bool NearOpenings(int straight, float t, float offset)
        {
            // Keep the alley mouth, the crosswalk and the spawn point clear.
            if (straight != 0) return false;
            if (offset < 0f && Mathf.Abs(t) < alleyHalfWidth + 1.5f) return true;
            if (Mathf.Abs(t - (-9f)) < 2.6f) return true;
            if (offset < 0f && Mathf.Abs(t - playerSpawn.x) < 2f) return true;
            return false;
        }

        void StreetFurniture()
        {
            float s = HalfX - CornerRadius, e = HalfZ - CornerRadius;
            float lampOffset = GroundGeometry.RoadHalf + GroundGeometry.CurbWidth + 0.45f;   // 4.55
            float treeOffset = 5.25f;
            // Every 12 m: a lamp on one side and a tree facing it on the other, alternating,
            // so each sidewalk has a lamp every 24 m with trees in between.
            for (int st = 0; st < 4; st++)
            {
                float half = st < 2 ? s : e;
                for (int k = 0; ; k++)
                {
                    float t = -half + 6f + 12f * k;
                    if (t > half - 4f) break;
                    float side = (k + st) % 2 == 0 ? -1f : 1f;
                    if (!NearOpenings(st, t, side * lampOffset)) Prop("Prop_StreetLamp", st, t, side * lampOffset, 0f, "Éclairage");
                    if (!NearOpenings(st, t + 1.1f, side * lampOffset)) Prop("Prop_TrashBin", st, t + 1.1f, side * (lampOffset + 0.1f));
                    if (!NearOpenings(st, t, -side * treeOffset)) Prop("Prop_Tree", st, t, -side * treeOffset);
                }
            }
            // Bus stop with its bench, on the shops' side (south outer sidewalk).
            Prop("Prop_BusStop", 0, 20f, 6.55f);
            Prop("Prop_Bench", 0, 26.5f, 6.85f);
            Prop("Prop_Bench", 1, -6f, -6.85f);
            Prop("Prop_Bench", 2, -2f, -6.85f);
            // Hydrants and bollards
            Prop("Prop_Hydrant", 0, -21.5f, 4.5f);
            Prop("Prop_Hydrant", 1, 26.5f, -4.5f);
            Prop("Prop_Hydrant", 3, 0.5f, 4.5f);
            // Bollards at the crosswalk, on the shops' side only (the block side is the spawn area).
            foreach (float t in new[] { -10.9f, -7.1f })
                Prop("Prop_Bollard", 0, t, 4.4f);
            // "Sens unique" at the beginning of each straight, on the drivers' right (inner side).
            Prop("Prop_SignOneWay", 0, s - 3f, -4.45f, 90f, "Signalisation");
            Prop("Prop_SignOneWay", 1, -s + 3f, -4.45f, 90f, "Signalisation");
            Prop("Prop_SignOneWay", 2, e - 2f, -4.45f, 90f, "Signalisation");
            Prop("Prop_SignOneWay", 3, -e + 2f, -4.45f, 90f, "Signalisation");
        }

        // ------------------------------------------------------------------ distance

        void Skyline()
        {
            var towers = new (string asset, Vector3 pos)[]
            {
                ("Bld_Tower2", new Vector3(-8f, 0f, -78f)), ("Bld_Tower1", new Vector3(34f, 0f, -72f)), ("Bld_Tower3", new Vector3(-48f, 0f, -70f)),
                ("Bld_Tower3", new Vector3(10f, 0f, 74f)), ("Bld_Tower1", new Vector3(-36f, 0f, 70f)), ("Bld_Tower2", new Vector3(52f, 0f, 76f)),
                ("Bld_Tower1", new Vector3(104f, 0f, 6f)), ("Bld_Tower2", new Vector3(-106f, 0f, -4f)),
                ("Bld_Tower3", new Vector3(86f, 0f, -50f)), ("Bld_Tower3", new Vector3(-84f, 0f, 50f)),
            };
            foreach (var (asset, pos) in towers)
            {
                var toCentre = -pos;
                toCentre.y = 0f;
                Add(asset, "Horizon", new Vector3(pos.x, Ground, pos.z), YawFromDirection(toCentre.normalized));
            }
        }

        void Boundaries()
        {
            float x = HalfX + Facade + 12f, z = HalfZ + Facade + 12f;
            boundaries.Add((new Vector3(0f, 6f, z), new Vector3(2f * x + 4f, 14f, 2f)));
            boundaries.Add((new Vector3(0f, 6f, -z), new Vector3(2f * x + 4f, 14f, 2f)));
            boundaries.Add((new Vector3(x, 6f, 0f), new Vector3(2f, 14f, 2f * z + 4f)));
            boundaries.Add((new Vector3(-x, 6f, 0f), new Vector3(2f, 14f, 2f * z + 4f)));
        }
    }
}
