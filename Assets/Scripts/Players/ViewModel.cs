// TideWall 3D — the first-person "hands": the gun bottom-right, and the melee weapon swinging through the view.
// Lives under the player's camera on that player's view layer, so only their camera draws it. It animates from
// the player's state alone (ammo dropping = a shot, swing progress, reload progress), so it works the same for an
// online client, whose shots happen on the host.
//   Shots: a hard kick back and up, a muzzle flash, an ejected shell and a camera kick (PlayerCamera.Kick).
//   Melee: each style has its own motion: a spade chops down, a cutlass slashes across with a wrist roll, an oar
//   sweeps wide, a boat hook jabs and yanks, an anchor is heaved overhead and slammed down.
//   While guns are holstered (the first waves), the melee weapon is held ready instead.
using UnityEngine;

namespace TideWall
{
    public class ViewModel : MonoBehaviour
    {
        Player p;
        Transform gunRoot, meleeRoot, flash;
        ModelInstance gun, melee;
        int lastAmmo;
        float kick, bobT, flashT;
        Vector3 lastPos;
        string gunId, meleeId;

        static readonly Vector3 GunRest = new(.3f, -.27f, .6f);

        public static ViewModel Create(Player p, PlayerCamera cam)
        {
            var root = new GameObject("ViewModel");
            root.transform.SetParent(cam.transform, false);
            var vm = root.AddComponent<ViewModel>();
            vm.p = p;
            vm.gunRoot = new GameObject("Gun").transform; vm.gunRoot.SetParent(root.transform, false);
            vm.meleeRoot = new GameObject("Melee").transform; vm.meleeRoot.SetParent(root.transform, false);
            vm.lastPos = p.transform.position;
            // Muzzle flash: a bright additive quad
            var f = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(f.GetComponent<Collider>());
            f.name = "MuzzleFlash";
            var fr = f.GetComponent<MeshRenderer>();
            fr.sharedMaterial = Mats.ParticleAdditive;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            f.layer = Player.ViewLayer + p.Index;
            vm.flash = f.transform;
            vm.flash.SetParent(vm.gunRoot, false);
            vm.flash.gameObject.SetActive(false);
            vm.Rebuild();
            return vm;
        }

        /// <summary>The player changed weapons: swap the held models.</summary>
        public void Rebuild()
        {
            if (p == null || p.Gun == null || p.Melee == null || gunRoot == null) return;
            if (gunId != p.Gun.id)
            {
                if (gun != null) Destroy(gun.root);
                gun = ModelFactory.Spawn(p.Gun.model, gunRoot, Vector3.zero, .55f);
                Prepare(gun);
                gunId = p.Gun.id;
                lastAmmo = p.Ammo;
                if (flash != null) flash.localPosition = gun.sockets.TryGetValue("Muzzle", out var mz) ? gunRoot.InverseTransformPoint(mz.position) : new Vector3(0, .03f, .3f);
            }
            if (meleeId != p.Melee.id)
            {
                if (melee != null) Destroy(melee.root);
                melee = ModelFactory.Spawn(p.Melee.model, meleeRoot, Vector3.zero, .42f);
                Prepare(melee);
                meleeId = p.Melee.id;
            }
        }

        void Prepare(ModelInstance mi)
        {
            foreach (var t in mi.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Player.ViewLayer + p.Index;
            foreach (var r in mi.renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }

        void LateUpdate()
        {
            if (p == null) return;
            Rebuild();
            float dt = Time.deltaTime;
            bool show = p.FirstPerson && !p.IsDowned && !p.IsOut;
            bool locked = p.GunsLocked;
            float sp = p.SwingProgress;
            gunRoot.gameObject.SetActive(show && p.CarryCount == 0 && !locked);
            meleeRoot.gameObject.SetActive(show && p.CarryCount == 0 && (sp >= 0 || locked));
            if (!show) { lastAmmo = p.Ammo; return; }

            // A shot: the magazine count dropped
            if (p.Ammo < lastAmmo && !locked)
            {
                kick = 1; flashT = .05f;
                if (p.Cam != null) p.Cam.Kick(p.EffGun != null ? p.EffGun.recoil : 3);
                Fx.Casing(gunRoot.position + gunRoot.right * .05f, gunRoot.right);
            }
            lastAmmo = p.Ammo;
            kick = Mathf.MoveTowards(kick, 0, dt * 6);
            flashT -= dt;
            flash.gameObject.SetActive(flashT > 0);
            if (flashT > 0) { flash.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f)); flash.localScale = Vector3.one * Random.Range(.18f, .3f); }

            // Walk bob
            var moved = p.transform.position - lastPos; moved.y = 0;
            lastPos = p.transform.position;
            float speed = dt > 0 ? moved.magnitude / dt : 0;
            bobT += dt * Mathf.Clamp(speed, 0, 8) * 1.6f;
            float bobAmt = Mathf.Clamp01(speed / 6) * .018f;
            var bob = new Vector3(Mathf.Cos(bobT) * bobAmt, Mathf.Abs(Mathf.Sin(bobT)) * bobAmt, 0);

            // Short split-screen strips have a narrower vertical FOV: keep the weapons inside the bottom of the view.
            var cam = p.Cam != null ? p.Cam.Camera : null;
            float k = cam != null ? Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(37.5f * Mathf.Deg2Rad) : 1;

            // Gun: rest pose, a hard kick back and up (bigger for heavy guns), dipped and tilted while reloading
            float reload = p.ReloadProgress >= 0 ? Mathf.Sin(p.ReloadProgress * Mathf.PI) : 0;
            float heavy = p.EffGun != null ? Mathf.Clamp(p.EffGun.recoil / 4, .6f, 2f) : 1;
            gunRoot.localPosition = new Vector3(GunRest.x, GunRest.y * k, GunRest.z) + bob + new Vector3(0, -.18f * reload * k + .03f * kick * heavy, -.12f * kick * heavy);
            gunRoot.localRotation = Quaternion.Euler(-22 * kick * heavy + 35 * reload, -3, 25 * reload + Random.Range(-2f, 2f) * kick);
            if (sp >= 0) gunRoot.localPosition += new Vector3(.05f, -.14f * k, 0);   // the gun dips out of the way of a swing

            // Melee: held ready (guns holstered) or swinging in its weapon's style
            if (sp < 0)
            {
                // Held ready at the lower right, blade up and angled away (not poking into the middle of the view)
                float breathe = Mathf.Sin(Time.time * 1.6f) * .006f;
                meleeRoot.localPosition = new Vector3(.42f, -.44f * k + breathe, .55f) + bob;
                meleeRoot.localRotation = Quaternion.Euler(-52, 26, -22);
                return;
            }
            var (pos, rot) = Swing(p.Melee, sp, k);
            meleeRoot.localPosition = pos + bob * .5f;
            meleeRoot.localRotation = rot;
        }

        static float Ease(float x) => x * x * (3 - 2 * x);

        /// <summary>Where the melee weapon is through a swing (t 0 → 1), by style: wind-up, strike, follow-through.</summary>
        static (Vector3, Quaternion) Swing(MeleeDef m, float t, float k)
        {
            float w = m.windup / m.SwingTime, st = (m.windup + m.strike) / m.SwingTime;
            float a = t < w ? t / w : t < st ? 1 + (t - w) / Mathf.Max(.001f, st - w) : 2 + (t - st) / Mathf.Max(.001f, 1 - st);
            var rest = new Vector3(.32f, -.3f * k, .55f);
            switch (m.style)
            {
                case MeleeStyle.Slash:   // cocked to the right, a fast flat slash to the left with a wrist roll
                    if (a < 1) return (Vector3.Lerp(rest, new Vector3(.5f, -.05f, .45f), Ease(a)), Quaternion.Euler(-10, Mathf.Lerp(-20, -80, Ease(a)), Mathf.Lerp(-25, -70, Ease(a))));
                    if (a < 2) return (Vector3.Lerp(new Vector3(.5f, -.05f, .45f), new Vector3(-.45f, -.2f, .6f), a - 1), Quaternion.Euler(-10, Mathf.Lerp(-80, 70, a - 1), Mathf.Lerp(-70, 20, a - 1)));
                    return (Vector3.Lerp(new Vector3(-.45f, -.2f, .6f), rest, Ease(a - 2)), Quaternion.Euler(-10, Mathf.Lerp(70, -20, Ease(a - 2)), Mathf.Lerp(20, -25, Ease(a - 2))));
                case MeleeStyle.Sweep:   // the oar: drawn far right, a big low sweep across the whole view
                    if (a < 1) return (Vector3.Lerp(rest, new Vector3(.7f, -.25f, .35f), Ease(a)), Quaternion.Euler(10, Mathf.Lerp(-20, -95, Ease(a)), -80));
                    if (a < 2) return (Vector3.Lerp(new Vector3(.7f, -.25f, .35f), new Vector3(-.7f, -.3f, .5f), Ease(a - 1)), Quaternion.Euler(10, Mathf.Lerp(-95, 95, Ease(a - 1)), -80));
                    return (Vector3.Lerp(new Vector3(-.7f, -.3f, .5f), rest, Ease(a - 2)), Quaternion.Euler(Mathf.Lerp(10, -35, a - 2), Mathf.Lerp(95, -20, Ease(a - 2)), Mathf.Lerp(-80, -25, a - 2)));
                case MeleeStyle.Thrust:  // the boat hook: drawn back, jabbed straight out, then yanked back
                    if (a < 1) return (Vector3.Lerp(rest, new Vector3(.2f, -.22f, .3f), Ease(a)), Quaternion.Euler(-80, 0, 0));
                    if (a < 2) return (Vector3.Lerp(new Vector3(.2f, -.22f, .3f), new Vector3(.08f, -.12f, 1.1f), (a - 1) * (3 - (a - 1)) / 2), Quaternion.Euler(-88, 0, 0));
                    return (Vector3.Lerp(new Vector3(.08f, -.12f, 1.1f), rest, Ease(a - 2)), Quaternion.Euler(Mathf.Lerp(-88, -35, a - 2), 0, 0));
                case MeleeStyle.Slam:    // the anchor: heaved up out of view, then crashing down the middle
                    if (a < 1) return (Vector3.Lerp(rest, new Vector3(.1f, .55f, .3f), Ease(a)), Quaternion.Euler(Mathf.Lerp(-35, -150, Ease(a)), 0, 0));
                    if (a < 2) return (Vector3.Lerp(new Vector3(.1f, .55f, .3f), new Vector3(0, -.55f, .75f), (a - 1) * (a - 1)), Quaternion.Euler(Mathf.Lerp(-150, 40, (a - 1) * (a - 1)), 0, 0));
                    return (Vector3.Lerp(new Vector3(0, -.55f, .75f), rest, Ease(a - 2)), Quaternion.Euler(Mathf.Lerp(40, -35, Ease(a - 2)), 0, 0));
                default:                 // the spade: raised high on the right, chopped down and across
                    if (a < 1) return (Vector3.Lerp(rest, new Vector3(.35f, .3f, .4f), Ease(a)), Quaternion.Euler(Mathf.Lerp(-35, -110, Ease(a)), -15, -10));
                    if (a < 2) return (Vector3.Lerp(new Vector3(.35f, .3f, .4f), new Vector3(-.05f, -.35f, .7f), a - 1), Quaternion.Euler(Mathf.Lerp(-110, 20, a - 1), -15, -10));
                    return (Vector3.Lerp(new Vector3(-.05f, -.35f, .7f), rest, Ease(a - 2)), Quaternion.Euler(Mathf.Lerp(20, -35, Ease(a - 2)), -15, -10));
            }
        }
    }
}
