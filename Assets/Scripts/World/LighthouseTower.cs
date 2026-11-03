// TideWall 3D — The Last Light's tower, built ring by ring so you can walk through it: a door at the foot, a shop room on the
// ground floor, a ramp that winds up the inside of the wall, and a wider lamp room on top with a railed catwalk around it.
// Angles are degrees clockwise from +Z (north); the door faces south. Heights are metres above the hill's top.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class LighthouseTower
    {
        public const float ShaftRadius = 6, WallInner = 5.6f, RampInner = 3.7f, DeckHeight = 15, RoomRadius = 8, CatwalkRadius = 10, LampHeight = 17.5f;
        const float DoorHalf = 16, DoorTop = 3.2f, RampFrom = 205, RampTurns = 2.5f, RoofAt = 19.4f, CatwalkDoorHalf = 14;

        static readonly Color White = new(.9f, .87f, .8f), Red = new(.66f, .16f, .14f), Stone = new(.5f, .49f, .47f), Wood = new(.46f, .33f, .21f), Dark = new(.2f, .2f, .22f), Brass = new(.72f, .56f, .24f), Glass = new(.86f, .92f, .88f);

        public static void Build(Transform parent)
        {
            var t = new GameObject("Tower").transform;
            t.SetParent(parent, false);

            // The shaft: striped, with the door cut out of its lowest stripe
            float doorA = 180 - DoorHalf, doorB = 180 + DoorHalf;
            Ring(t, "Wall Door", White, doorB, doorA + 360, WallInner, ShaftRadius, 0, DoorTop);
            Ring(t, "Lintel", Stone, doorA, doorB, WallInner, ShaftRadius + .12f, DoorTop, DoorTop + .5f);
            Ring(t, "Wall White", White, doorB, doorB + 360 - DoorHalf * 2, WallInner, ShaftRadius, DoorTop, 5);
            Ring(t, "Wall White 2", White, doorA, doorB, WallInner, ShaftRadius, DoorTop + .5f, 5);
            Ring(t, "Wall Red", Red, 0, 360, WallInner, ShaftRadius, 5, 9);
            Ring(t, "Wall White 3", White, 0, 360, WallInner, ShaftRadius, 9, 12);
            Ring(t, "Wall Red 2", Red, 0, 360, WallInner, ShaftRadius, 12, DeckHeight - .4f);
            Ring(t, "Ground Floor", Wood, 0, 360, 0, WallInner + .1f, 0, .05f);

            // The top half flares out over a band of corbels to carry the wider room
            Ring(t, "Corbels", Stone, 0, 360, WallInner, ShaftRadius, RoomRadius - .4f, RoomRadius, 12, DeckHeight - .4f);

            // The ramp up the inside of the wall, with a rail on its open side
            float rampTo = RampFrom + 360 * RampTurns;
            Ring(t, "Ramp", Wood, RampFrom, rampTo, RampInner, WallInner + .05f, -.3f, 0, DeckHeight, 130);
            Ring(t, "Ramp Rail", Dark, RampFrom, rampTo, RampInner - .1f, RampInner + .02f, 1, 1.12f, DeckHeight, 130);
            Ring(t, "Ramp Rail Stop", Dark, RampFrom, rampTo, RampInner - .1f, RampInner + .02f, 0, 1.1f, DeckHeight, 130, renderer: false);

            // The deck: solid, but for the hole the ramp comes up through
            float y0 = DeckHeight - .4f, holeA = rampTo - 150 - 720, holeB = rampTo - 720;
            Ring(t, "Deck Middle", Wood, 0, 360, 0, RampInner - .1f, y0, DeckHeight);
            Ring(t, "Deck Ramp Side", Wood, holeB, holeA + 360, RampInner - .1f, WallInner + .1f, y0, DeckHeight);
            Ring(t, "Deck Outer", Wood, 0, 360, WallInner + .1f, CatwalkRadius, y0, DeckHeight);

            // The lamp room: a sill, glazing bars and a roof. You get out onto the catwalk through the gap facing south
            Ring(t, "Sill", White, 180 + CatwalkDoorHalf, 180 - CatwalkDoorHalf + 360, RoomRadius - .4f, RoomRadius, DeckHeight, DeckHeight + 1);
            for (int a = 0; a < 360; a += 30)
                if (Mathf.Abs(a - 180) > CatwalkDoorHalf) Ring(t, "Bar", Dark, a - 1, a + 1, RoomRadius - .32f, RoomRadius - .08f, DeckHeight + 1, RoofAt, 0, 1);
            foreach (float a in new[] { 180 - CatwalkDoorHalf, 180 + CatwalkDoorHalf })
                Ring(t, "Door Post", Dark, a - 1, a + 1, RoomRadius - .4f, RoomRadius, DeckHeight, RoofAt, 0, 1);
            Ring(t, "Eaves", Red, 0, 360, 0, RoomRadius + .7f, RoofAt, RoofAt + .3f);
            Ring(t, "Roof", Red, 0, 360, 0, RoomRadius + .7f, 0, .7f, RoofAt + .3f, RoofAt + 3.2f);
            Ring(t, "Vent", Dark, 0, 360, 0, .7f, 0, .25f, RoofAt + 3.2f, RoofAt + 4);
            Ring(t, "Lamp Plinth", Stone, 0, 360, 0, .9f, 0, .7f, DeckHeight, LampHeight - 1);
            Ring(t, "Lamp Base", Brass, 0, 360, 0, .75f, 0, .75f, LampHeight - 1, LampHeight - .85f);
            Ring(t, "Lens", Glass, 0, 360, 0, .55f, 0, .68f, LampHeight - .85f, LampHeight, 0, 12, collider: false);
            Ring(t, "Lens Top", Glass, 0, 360, 0, .68f, 0, .55f, LampHeight, LampHeight + .85f, 0, 12, collider: false);
            Ring(t, "Lamp Cap", Brass, 0, 360, 0, .6f, 0, .15f, LampHeight + .85f, LampHeight + 1.25f, collider: false);

            // The catwalk's railing: posts, two rails and something to actually stop you going over
            for (int a = 0; a < 360; a += 15) Ring(t, "Post", Dark, a - .5f, a + .5f, CatwalkRadius - .16f, CatwalkRadius - .02f, DeckHeight, DeckHeight + 1.15f, 0, 1, collider: false);
            Ring(t, "Rail Top", Dark, 0, 360, CatwalkRadius - .18f, CatwalkRadius, DeckHeight + 1.05f, DeckHeight + 1.15f, collider: false);
            Ring(t, "Rail Mid", Dark, 0, 360, CatwalkRadius - .14f, CatwalkRadius - .04f, DeckHeight + .55f, DeckHeight + .62f, collider: false);
            var stop = Ring(t, "Rail Stop", Dark, 0, 360, CatwalkRadius - .18f, CatwalkRadius, DeckHeight, DeckHeight + 1.4f, renderer: false);
            stop.layer = 2;   // it holds you in; it doesn't stop your shots over the side

            Lamp(t, "Ground Floor Light", new Vector3(0, 3.4f, 0), 14, 5);
            Lamp(t, "Ramp Light", new Vector3(0, 9, 0), 13, 4);
            Lamp(t, "Lamp Room Light", new Vector3(0, DeckHeight + 3.6f, 0), 14, 2.5f);
        }

        static void Lamp(Transform t, string name, Vector3 at, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = at;
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1, .78f, .5f); l.shadows = LightShadows.None;
        }

        static GameObject Ring(Transform t, string name, Color c, float a0, float a1, float rIn, float rOut, float yBottom, float yTop, float rise = 0, int segs = 0, bool collider = true, bool renderer = true)
            => Ring(t, name, c, a0, a1, rIn, rOut, rIn, rOut, yBottom, yTop, rise, segs, collider, renderer);

        /// <summary>A solid slice of ring from angle a0 to a1 (radii may change from bottom to top: a cone), rising by this much along its length (a ramp).</summary>
        static GameObject Ring(Transform t, string name, Color c, float a0, float a1, float rInB, float rOutB, float rInT, float rOutT, float yBottom, float yTop, float rise = 0, int segs = 0, bool collider = true, bool renderer = true)
        {
            if (segs <= 0) segs = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 7.5f));
            var v = new List<Vector3>();
            var tris = new List<int>();
            Vector3 At(float a, float r, float y) => new(Mathf.Sin(a * Mathf.Deg2Rad) * r, y, Mathf.Cos(a * Mathf.Deg2Rad) * r);
            for (int i = 0; i < segs; i++)
            {
                float k0 = i / (float)segs, k1 = (i + 1) / (float)segs;
                float a = Mathf.Lerp(a0, a1, k0), b = Mathf.Lerp(a0, a1, k1), up0 = rise * k0, up1 = rise * k1;
                Vector3 ibA = At(a, rInB, yBottom + up0), obA = At(a, rOutB, yBottom + up0), itA = At(a, rInT, yTop + up0), otA = At(a, rOutT, yTop + up0);
                Vector3 ibB = At(b, rInB, yBottom + up1), obB = At(b, rOutB, yBottom + up1), itB = At(b, rInT, yTop + up1), otB = At(b, rOutT, yTop + up1);
                Vector3 mid = At((a + b) / 2, 1, 0), along = (At(b, 1, 0) - At(a, 1, 0)).normalized;
                Quad(v, tris, otA, otB, itB, itA, Vector3.up);
                Quad(v, tris, obA, obB, ibB, ibA, Vector3.down);
                Quad(v, tris, obA, obB, otB, otA, mid);
                if (rInB > .01f || rInT > .01f) Quad(v, tris, ibA, ibB, itB, itA, -mid);
                if (i == 0) Quad(v, tris, ibA, obA, otA, itA, -along);
                if (i == segs - 1) Quad(v, tris, ibB, obB, otB, itB, along);
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit(c);
            mr.enabled = renderer;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>Four corners as a flat-shaded face, wound to face the way it's told.</summary>
        static void Quad(List<Vector3> v, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0;
            if (flip) { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2); }
            else { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3); }
        }
    }
}
