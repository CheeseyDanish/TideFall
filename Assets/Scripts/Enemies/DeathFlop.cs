// TideWall 3D — the moment a creature dies: thrown along the killing blow, it tumbles over and thumps onto the sand,
// rather than snapping straight to its corpse pose. Picked up mid-fall, it just finishes at once.
using UnityEngine;

namespace TideWall
{
    public class DeathFlop : MonoBehaviour
    {
        Enemy enemy; Corpse corpse;
        Quaternion from, to;
        Vector3 push;
        float t, seconds, startY, groundY, hop;
        bool falls;

        public static void Begin(Enemy e, Corpse c, Quaternion to, Vector3 push, bool falls)
        {
            var f = e.gameObject.AddComponent<DeathFlop>();
            f.enemy = e; f.corpse = c; f.to = to; f.push = push; f.falls = falls;
            f.from = e.Model.root.transform.localRotation;
            f.startY = e.transform.position.y;
            f.groundY = Arena.GroundY(e.transform.position.z);
            f.seconds = Mathf.Clamp(.34f + e.Radius * .12f, .38f, .8f);
            f.hop = Mathf.Clamp(.65f - e.Radius * .12f, .2f, .6f);
        }

        void Update()
        {
            if (enemy == null) { Destroy(this); return; }
            t += Time.deltaTime / seconds;
            bool cut = corpse == null || corpse.Holder != null || corpse.Thief != null;
            float k = cut ? 1 : Mathf.Clamp01(t);
            var root = enemy.Model.root.transform;
            root.localRotation = Quaternion.Slerp(from, to, k * k * (3 - 2 * k));
            root.localPosition = Vector3.up * (.1f + Mathf.Sin(k * Mathf.PI) * hop);
            root.localScale = Vector3.one * enemy.Def.scale;
            if (!cut)
            {
                var p = transform.position + push * (1 - k) * Time.deltaTime;
                if (GameManager.I.World != null && !Arena.Inside(transform.position)) p.z = Mathf.Max(p.z, Arena.WallFront + enemy.Radius * .6f);
                if (falls) p.y = Mathf.Lerp(startY, groundY, k * k);
                transform.position = p;
                enemy.LiftCorpse();
            }
            if (k < 1) return;
            if (!cut)
            {
                Fx.Debris(transform.position, new Color(.9f, .82f, .6f), Mathf.Clamp(Mathf.RoundToInt(enemy.Radius * 6), 3, 12));
                Sfx.Play("thud", Mathf.Clamp01(.3f + enemy.Radius * .3f), .06f);
            }
            Destroy(this);
        }
    }
}
