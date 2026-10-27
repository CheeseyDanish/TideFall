// TideWall 3D — the living picture outside the keep's window while the main menu is up.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TideWall
{
    /// <summary>The living picture outside the keep's window while the main menu is up.</summary>
    public class MenuScenery : MonoBehaviour
    {
        Transform ball;
        readonly List<Transform> players = new();
        readonly List<(Transform t, float r, float speed, float phase, Vector3 centre)> circlers = new();
        float t;

        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("Menu Scenery");
            root.transform.SetParent(parent, false);
            var s = root.AddComponent<MenuScenery>();
            // Beach volleyball on the dry sand
            var net = ModelFactory.Spawn("VolleyNet", root.transform, new Vector3(-6, 0, 9));
            net.root.transform.rotation = Quaternion.Euler(0, 90, 0);
            Vector3[] spots = { new(-9, 0, 7.5f), new(-9, 0, 10.5f), new(-3, 0, 7.5f), new(-3, 0, 10.5f) };
            for (int i = 0; i < 4; i++)
            {
                var mi = ModelFactory.Spawn("Sailor", root.transform, spots[i]);
                ModelFactory.Recolor(mi, "coat", Player.Colors[i]);
                mi.root.transform.rotation = Quaternion.Euler(0, i < 2 ? 90 : -90, 0);
                s.players.Add(mi.root.transform);
            }
            s.ball = ModelFactory.Spawn("Volleyball", root.transform, new Vector3(-6, 3, 9)).root.transform;
            // Shark fins circling offshore, gulls overhead
            for (int i = 0; i < 2; i++)
            {
                var shark = ModelFactory.Spawn("Shark", root.transform, Vector3.zero);
                s.circlers.Add((shark.root.transform, 6 + i * 3, .25f + i * .08f, i * 2.5f, new Vector3(-10 + i * 18, Arena.WaterLevel - .62f, Arena.ShoreZ + 14 + i * 8)));
            }
            for (int i = 0; i < 3; i++)
            {
                var gull = ModelFactory.Spawn("Gull", root.transform, Vector3.zero);
                s.circlers.Add((gull.root.transform, 6 + i * 2, .5f + i * .1f, i * 2.1f, new Vector3(4 + i * 3, 9 + i, 14 + i * 4)));
            }
            return root;
        }

        // Hits alternate between the teams (players 0-1 on one side of the net, 2-3 on the other).
        Transform Hitter(int n) => n % 2 == 0 ? players[(n / 2) % 2] : players[2 + (n / 2) % 2];

        void Update()
        {
            t += Time.deltaTime;
            // The ball goes back and forth over the net between the two teams
            const float rally = 1.6f;
            int hit = Mathf.FloorToInt(t / rally);
            float k = t / rally - hit;
            var from = Hitter(hit).position + Vector3.up * 2.2f;
            var to = Hitter(hit + 1).position + Vector3.up * 2.2f;
            ball.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3.2f;
            ball.rotation = Quaternion.Euler(t * 300, t * 120, 0);
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                float jump = Mathf.Max(0, Mathf.Sin((t + i * .7f) * 3.4f)) * .25f;
                p.position = new Vector3(p.position.x, jump, p.position.z);
            }
            foreach (var (tr, r, speed, phase, centre) in circlers)
            {
                float a = t * speed + phase;
                tr.position = centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2) * .15f, Mathf.Sin(a) * r);
                tr.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            }
        }
    }
}
