// TideWall 3D — the shape of the playfield. Unity axes: +X along the shore, +Z out to sea, +Y up.
//
//   z  -38 ... -24 ......... -2 | 0 | ........................ 34 ... 40 ....................... 100+
//      keep   town (plant,       wall  dry beach (twice the old  wet    sea (seabed slopes down;
//             shop, stand, bell)       size: 88 m wide, 40 deep) sand   breakers roll in from ~z 100)
using UnityEngine;

namespace TideWall
{
    public static class Arena
    {
        public const float WallZ = 0f;              // centre line of the wall
        public const float WallFront = .6f;         // enemies stop here (+ their radius) unless a section is breached
        public const float WallHalfLength = 44f;    // sections span x = -44..44
        public const float SegmentLength = 4f;
        public static readonly float[] GapCentres = { -22f, 22f };   // sally ports: players walk through, creatures can't
        public const float ShoreZ = 40f;            // water line (ground dips below y = 0 here)
        /// <summary>Where the sea's surface is. Only The Last Light moves it.</summary>
        public static float WaterLevel = 0f;
        public const float TownMinZ = -23f;
        public const float BoundX = 50f, MinZ = -23f, MaxZ = 54f;   // where players can walk
        public static readonly Vector3 PlantPos = new(17, 0, -15);  // the Nuclear Sand Powerplant (east side of town, clear of the keep's window)
        public const float PlantRadius = 5.5f;

        /// <summary>Part II: the sea is held back in two walls and the seabed between them is flat, open and dry.</summary>
        public static bool Seabed;

        /// <summary>Ground height: flat sand and town, then the seabed sloping away from the shore.</summary>
        public static float GroundY(float z) => Seabed || z <= ShoreZ ? 0f : Mathf.Max(-6f, -(z - ShoreZ) * .12f);

        public static bool InWater(Vector3 p) => Island ? IslandY(p.x, p.z) < WaterLevel - .05f : !Seabed && p.z > ShoreZ && GroundY(p.z) < WaterLevel - .05f;

        /// <summary>The Last Light: the ground is a hill rising out of open sea, centred on the world's origin.</summary>
        public static bool Island;
        public const float IslandTop = 3f, IslandPlateau = 11f, IslandShore = 48f, IslandBase = 92f, IslandDepth = -5f, IslandBound = 66f;

        public static float IslandY(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            if (r <= IslandPlateau) return IslandTop;
            if (r <= IslandShore) return Mathf.Lerp(IslandTop, 0, Mathf.SmoothStep(0, 1, (r - IslandPlateau) / (IslandShore - IslandPlateau)));
            return Mathf.Lerp(0, IslandDepth, Mathf.Clamp01((r - IslandShore) / (IslandBase - IslandShore)));
        }

        /// <summary>How far from the middle the waterline is right now.</summary>
        public static float ShoreRadius()
        {
            float lo = 0, hi = IslandBase;
            for (int i = 0; i < 14; i++) { float m = (lo + hi) / 2; if (IslandY(m, 0) > WaterLevel) lo = m; else hi = m; }
            return lo;
        }

        /// <summary>Ground height at a point, whichever level this is.</summary>
        public static float GroundAt(Vector3 p) => Island ? IslandY(p.x, p.z) : GroundY(p.z);

        /// <summary>How deep the water is at this point (0 on land).</summary>
        public static float Depth(Vector3 p) => Mathf.Max(0, WaterLevel - GroundAt(p));

        public static bool IsGap(float x)
        {
            foreach (var g in GapCentres) if (Mathf.Abs(x - g) < SegmentLength / 2) return true;
            return false;
        }

        /// <summary>Inside the walls (town side)?</summary>
        public static bool Inside(Vector3 p) => !Seabed && p.z < WallZ - .4f;

        /// <summary>The spot on the wall an enemy at x should attack (gaps redirect to the nearest stone).</summary>
        public static Vector3 WallTarget(float x, float radius)
        {
            x = Mathf.Clamp(x, -WallHalfLength + 1, WallHalfLength - 1);
            foreach (var g in GapCentres)
                if (Mathf.Abs(x - g) < SegmentLength / 2) x = g + Mathf.Sign(x - g + .01f) * (SegmentLength / 2 + .8f);
            return new Vector3(x, 0, WallFront + radius);
        }
    }
}
