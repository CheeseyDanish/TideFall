// TideWall 3D — the shape of the playfield. Unity axes: +X along the shore, +Z out to sea, +Y up.
//
//   z  -24 ........ -2 | 0 | ......... 16 .... 20 ............ 60
//      town (shop,      wall  dry beach      wet sand  sea (seabed slopes down;
//      stand, bell)                                      enemies spawn ~z 48-56)
using UnityEngine;

namespace TideWall
{
    public static class Arena
    {
        public const float WallZ = 0f;              // centre line of the wall
        public const float WallFront = .6f;         // enemies stop here (+ their radius)
        public const float WallHalfLength = 24f;    // segments span x = -24..24
        public const float SegmentLength = 4f;
        public static readonly float[] GapCentres = { -10f, 10f };   // sally ports players can walk through
        public const float ShoreZ = 20f;            // water line (ground dips below y = 0 here)
        public const float WaterLevel = 0f;
        public const float TownMinZ = -23f;
        public const float BoundX = 27f, MinZ = -23f, MaxZ = 31f;   // where players can walk
        public const float SpawnMinZ = 46f, SpawnMaxZ = 56f;

        /// <summary>Ground height: flat sand and town, then the seabed sloping away from the shore.</summary>
        public static float GroundY(float z) => z <= ShoreZ ? 0f : Mathf.Max(-5f, -(z - ShoreZ) * .12f);

        public static bool InWater(Vector3 p) => p.z > ShoreZ && GroundY(p.z) < WaterLevel - .05f;

        /// <summary>How deep the water is at this point (0 on land).</summary>
        public static float Depth(Vector3 p) => Mathf.Max(0, WaterLevel - GroundY(p.z));

        public static bool IsGap(float x)
        {
            foreach (var g in GapCentres) if (Mathf.Abs(x - g) < SegmentLength / 2) return true;
            return false;
        }

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
