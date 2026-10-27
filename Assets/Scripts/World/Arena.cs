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
        public const float WaterLevel = 0f;
        public const float TownMinZ = -23f;
        public const float BoundX = 50f, MinZ = -23f, MaxZ = 54f;   // where players can walk
        public static readonly Vector3 PlantPos = new(17, 0, -15);  // the Nuclear Sand Powerplant (east side of town, clear of the keep's window)
        public const float PlantRadius = 5.5f;

        /// <summary>Part II: the sea is held back in two walls and the seabed between them is flat, open and dry.</summary>
        public static bool Seabed;

        /// <summary>Ground height: flat sand and town, then the seabed sloping away from the shore.</summary>
        public static float GroundY(float z) => Seabed || z <= ShoreZ ? 0f : Mathf.Max(-6f, -(z - ShoreZ) * .12f);

        public static bool InWater(Vector3 p) => !Seabed && p.z > ShoreZ && GroundY(p.z) < WaterLevel - .05f;

        /// <summary>How deep the water is at this point (0 on land).</summary>
        public static float Depth(Vector3 p) => Mathf.Max(0, WaterLevel - GroundY(p.z));

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
