// TideWall 3D — the sea: a gently rolling surface (CPU vertex waves on a copy of the saved mesh) that swells under
// breaking waves, and a foam line at the shore.
using System;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>A gently rolling sea (CPU vertex waves on a grid) with breaking-wave swells and a foam line at the shore.</summary>
    public class Water : MonoBehaviour
    {
        [SerializeField] Transform foam;
        Mesh mesh; Vector3[] baseVerts, verts;

        /// <summary>The waves move the vertices of a copy of the saved sea mesh (the asset itself never changes).</summary>
        void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            mesh = Instantiate(mf.sharedMesh);
            mesh.name = "Sea (waves)";
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            baseVerts = mesh.vertices;
            verts = (Vector3[])baseVerts.Clone();
        }

        public static Water Build(Transform parent, Rect area, float step)
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<Water>();
            int nx = Mathf.CeilToInt(area.width / step) + 1, nz = Mathf.CeilToInt(area.height / step) + 1;
            var v = new Vector3[nx * nz]; var uv = new Vector2[v.Length]; var tris = new int[(nx - 1) * (nz - 1) * 6];
            for (int z = 0, i = 0; z < nz; z++) for (int x = 0; x < nx; x++, i++)
                { v[i] = new Vector3(area.xMin + x * step, 0, area.yMin + z * step); uv[i] = new Vector2(x, z) * .1f; }
            for (int z = 0, t = 0; z < nz - 1; z++) for (int x = 0; x < nx - 1; x++)
                {
                    int i = z * nx + x;
                    tris[t++] = i; tris[t++] = i + nx; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + nx; tris[t++] = i + nx + 1;
                }
            var mesh = new Mesh { name = "Sea", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(Mats.Transparent(new Color(.14f, .46f, .68f, .8f))) { name = "Sea" };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .9f);
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Foam where the waves meet the sand
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Util.Destroy(f.GetComponent<Collider>());
            f.name = "Foam";
            f.transform.SetParent(parent, false);
            f.transform.localScale = new Vector3(area.width, .02f, 2.2f);
            var fr = f.GetComponent<MeshRenderer>();
            fr.sharedMaterial = Mats.Transparent(new Color(.97f, .99f, 1f, .55f));
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.foam = f.transform;
            return w;
        }

        void Update()
        {
            if (mesh == null) Awake();
            float t = Time.time;
            var breakers = Breaker.All;
            for (int i = 0; i < verts.Length; i++)
            {
                var b = baseVerts[i];
                float y = Mathf.Sin(b.z * .35f - t * 1.6f) * .12f + Mathf.Sin(b.x * .22f + t * .9f) * .07f;
                for (int k = 0; k < breakers.Count; k++)
                {
                    var br = breakers[k];
                    float dz = b.z - br.Z;
                    if (dz < -6 || dz > 10) continue;
                    float dx = Mathf.Abs(b.x - br.X) - br.Width * .5f;
                    float edge = dx <= 0 ? 1 : Mathf.Clamp01(1 - dx / 5);
                    y += br.Swell * edge * Mathf.Exp(-(dz * dz) / (dz < 0 ? 3f : 14f));   // steep front, long back
                }
                verts[i].y = y;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            if (foam)
            {
                float s = Mathf.Sin(t * .9f);
                foam.position = new Vector3(0, .03f + Mathf.Max(0, s) * .02f, Arena.ShoreZ - .6f - s * 1.4f);
            }
        }
    }
}
