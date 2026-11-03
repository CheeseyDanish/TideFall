// TideWall 3D — a creature's ragdoll. The body is thrown along the killing blow, spins, falls and bounces off the ground,
// then settles into its corpse pose. Its limbs swing loose under gravity (LimbRag), and keep swinging when it's carried.
// Picked up mid-fall, the body just finishes at once.
using UnityEngine;

namespace TideWall
{
    public class DeathFlop : MonoBehaviour
    {
        const float Gravity = 22, Bounce = .38f;
        Enemy enemy; Corpse corpse;
        Quaternion to;
        Vector3 push, spinAxis;
        float t, seconds, startY, groundY, height, vy, spin, thudVolume;
        bool falls;

        public static void Begin(Enemy e, Corpse c, Quaternion to, Vector3 push, bool falls)
        {
            var f = e.gameObject.AddComponent<DeathFlop>();
            f.enemy = e; f.corpse = c; f.to = to; f.push = push; f.falls = falls;
            f.startY = e.transform.position.y;
            f.groundY = Arena.GroundAt(e.transform.position);
            f.seconds = Mathf.Clamp(.8f + e.Radius * .25f, .9f, 1.7f);
            f.vy = Mathf.Clamp(5f - e.Radius * 1.2f, 2, 5) + push.magnitude * .25f;
            f.height = falls ? 0 : .1f;
            // Spin around the axis across the blow (or any way, if nothing sent it)
            var dir = push.sqrMagnitude > .01f ? push.normalized : Random.insideUnitSphere;
            f.spinAxis = e.transform.InverseTransformDirection(Vector3.Cross(Vector3.up, dir)).normalized;
            if (f.spinAxis.sqrMagnitude < .01f) f.spinAxis = Vector3.forward;
            f.spin = Mathf.Clamp(620 / Mathf.Max(1, e.Radius) + push.magnitude * 30, 200, 900);
            f.thudVolume = Mathf.Clamp01(.3f + e.Radius * .3f);
            LimbRag.Begin(e, push);
        }

        void Update()
        {
            if (enemy == null) { Destroy(this); return; }
            float dt = Time.deltaTime;
            t += dt / seconds;
            bool cut = corpse == null || corpse.Holder != null || corpse.Thief != null;
            var root = enemy.Model.root.transform;
            root.localScale = Vector3.one * enemy.Def.scale;
            if (cut) t = 1;
            if (t >= 1)
            {
                root.localRotation = to;
                root.localPosition = Vector3.up * .1f;
                if (!cut) enemy.LiftCorpse();
                Destroy(this);
                return;
            }

            // In the air it tumbles freely; from halfway it's being pulled round to how it'll lie
            root.localRotation = Quaternion.AngleAxis(spin * dt, spinAxis) * root.localRotation;
            spin *= Mathf.Exp(-dt * 1.6f);
            if (t > .5f) root.localRotation = Quaternion.Slerp(root.localRotation, to, 1 - Mathf.Exp(-dt * Mathf.Lerp(2, 22, (t - .5f) * 2)));

            vy -= Gravity * dt;
            height += vy * dt;
            if (falls) transform.position = new Vector3(transform.position.x, Mathf.Lerp(startY, groundY, Mathf.Clamp01(t * 2)), transform.position.z);
            if (height <= 0 && (!falls || t >= .5f))
            {
                height = 0;
                if (vy < -2)
                {
                    Fx.Debris(transform.position, new Color(.9f, .82f, .6f), Mathf.Clamp(Mathf.RoundToInt(enemy.Radius * 4 * -vy * .2f), 2, 12));
                    Sfx.Play("thud", thudVolume * Mathf.Clamp01(-vy * .15f), .06f);
                    LimbRag.Kick(enemy, -vy);
                }
                vy = vy < -2 ? -vy * Bounce : 0;
                spin *= .6f;
                push *= .5f;
            }
            root.localPosition = Vector3.up * height;

            var p = transform.position + push * dt;
            push *= Mathf.Exp(-dt * .8f);
            if (GameManager.I.World != null && !Arena.Inside(transform.position)) p.z = Mathf.Max(p.z, Arena.WallFront + enemy.Radius * .6f);
            transform.position = p;
            enemy.LiftCorpse();
        }
    }
}
