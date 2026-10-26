// TideWall 3D — the people behind the stalls: the hotdog chef and the fishmonger. They turn to whoever comes near,
// wave, and shout lines (GameDatabase.chefLines / fishmongerLines) in a speech bubble. Scenery only: every machine
// animates its own vendors.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class Vendor : MonoBehaviour
    {
        List<string> lines;
        ModelInstance model;
        Transform armR, armL, head;
        TextMesh bubble;
        float sayT, bubbleT, waveT, t, baseYaw;

        public static Vendor Create(Transform parent, string modelName, Vector3 pos, float yaw, List<string> lines)
        {
            var go = new GameObject(modelName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var v = go.AddComponent<Vendor>();
            v.lines = lines;
            v.baseYaw = yaw;
            v.model = ModelFactory.Spawn(modelName, go.transform, Vector3.zero);
            foreach (var tr in v.model.root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == "Part_ArmR") v.armR = tr;
                if (tr.name == "Part_ArmL") v.armL = tr;
                if (tr.name == "Part_Head") v.head = tr;
            }
            v.bubble = new GameObject("Bubble").AddComponent<TextMesh>();
            v.bubble.transform.SetParent(go.transform, false);
            v.bubble.transform.localPosition = new Vector3(0, 2.8f, 0);
            v.bubble.font = Ui.Font; v.bubble.fontSize = 110; v.bubble.characterSize = .018f; v.bubble.anchor = TextAnchor.LowerCenter;
            v.bubble.color = new Color(1, .97f, .85f);
            v.bubble.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            v.sayT = Random.Range(3f, 8f);
            return v;
        }

        /// <summary>Say something (a random line, or this one).</summary>
        public void Say(string line = null)
        {
            if (lines == null || lines.Count == 0) return;
            bubble.text = line ?? lines[Random.Range(0, lines.Count)];
            bubbleT = 3.5f;
            waveT = 1.2f;
            sayT = Random.Range(9f, 15f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var gm = GameManager.I;
            Player near = null; float bd = 12;
            if (gm != null)
                foreach (var p in gm.Players.All)
                {
                    float d = Vector3.Distance(p.transform.position, transform.position);
                    if (d < bd) { bd = d; near = p; }
                }
            // Turn toward a customer, back to the counter otherwise
            float want = baseYaw;
            if (near != null)
            {
                var to = near.transform.position - transform.position; to.y = 0;
                if (to.sqrMagnitude > .01f) want = Quaternion.LookRotation(to).eulerAngles.y;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, want, 0), 180 * dt);
            sayT -= dt;
            if (near != null && sayT <= 0) Say();
            bubbleT -= dt;
            bubble.gameObject.SetActive(bubbleT > 0);
            var cam = gm != null ? gm.Players.FirstViewCamera(null) : null;
            if (cam != null) bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position);

            // Idle: a little sway, busy hands; a big wave while talking
            waveT -= dt;
            if (armR != null) armR.localRotation = waveT > 0 ? Quaternion.Euler(-160, 0, Mathf.Sin(t * 14) * 25) : Quaternion.Euler(-40 + Mathf.Sin(t * 2.2f) * 12, 0, 0);
            if (armL != null) armL.localRotation = Quaternion.Euler(-50 + Mathf.Sin(t * 2.6f + 1) * 14, 0, 0);
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 4, 0, Mathf.Sin(t * .9f) * 5);
            model.root.transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t * 2.2f)) * .03f, 0);
        }
    }
}
