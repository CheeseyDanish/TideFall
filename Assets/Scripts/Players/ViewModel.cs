// TideWall 3D — the first-person "hands": the gun and melee weapon in the bottom corners of your view.
// Lives under the player's camera on that player's view layer, so only their camera draws it. It animates from
// the player's state alone (ammo dropping = recoil, swing timer, reload progress), so it works the same for an
// online client, whose shots happen on the host.
using UnityEngine;

namespace TideWall
{
    public class ViewModel : MonoBehaviour
    {
        Player p;
        Transform gunRoot, meleeRoot;
        ModelInstance gun, melee;
        int lastAmmo;
        float kick, bobT, swingShow;
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
            bool show = p.FirstPerson && !p.IsDowned && !p.IsOut && p.ShownMenu == null;
            gunRoot.gameObject.SetActive(show && p.CarryCount == 0);
            meleeRoot.gameObject.SetActive(show && (p.SwingT > 0 || swingShow > .01f));   // only out while swinging
            if (!show) { lastAmmo = p.Ammo; return; }

            // Recoil when the magazine count drops
            if (p.Ammo < lastAmmo) kick = 1;
            lastAmmo = p.Ammo;
            kick = Mathf.MoveTowards(kick, 0, dt * 7);

            // Walk bob
            var moved = p.transform.position - lastPos; moved.y = 0;
            lastPos = p.transform.position;
            float speed = dt > 0 ? moved.magnitude / dt : 0;
            bobT += dt * Mathf.Clamp(speed, 0, 8) * 1.6f;
            float bobAmt = Mathf.Clamp01(speed / 6) * .018f;
            var bob = new Vector3(Mathf.Cos(bobT) * bobAmt, Mathf.Abs(Mathf.Sin(bobT)) * bobAmt, 0);

            // Gun: rest pose, recoil kick, dipped and tilted while reloading
            float reload = p.ReloadProgress >= 0 ? Mathf.Sin(p.ReloadProgress * Mathf.PI) : 0;
            // Short split-screen strips have a narrower vertical FOV: keep the gun inside the bottom of the view.
            var cam = p.Cam != null ? p.Cam.Camera : null;
            float k = cam != null ? Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(37.5f * Mathf.Deg2Rad) : 1;
            gunRoot.localPosition = new Vector3(GunRest.x, GunRest.y * k, GunRest.z) + bob + new Vector3(0, -.18f * reload * k, -.07f * kick);
            gunRoot.localRotation = Quaternion.Euler(-14 * kick + 35 * reload, -3, 25 * reload);

            // Melee: comes up from below on the right and sweeps across the view
            if (p.SwingT > 0) swingShow = 1 - p.SwingT / .25f; else swingShow = Mathf.MoveTowards(swingShow, 0, dt * 6);
            float s = p.SwingT > 0 ? swingShow : 0;
            meleeRoot.localPosition = Vector3.Lerp(new Vector3(.45f, -.2f, .55f), new Vector3(-.35f, -.3f, .6f), s) + bob;
            meleeRoot.localRotation = Quaternion.Euler(10, -60 + 120 * s, -50 + 30 * s);
            if (p.SwingT > 0) gunRoot.localPosition += new Vector3(.05f, -.12f, 0);   // the gun dips out of the way
        }
    }
}
