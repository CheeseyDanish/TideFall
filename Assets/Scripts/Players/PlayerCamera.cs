// TideWall 3D — one camera per local player (split screen). First person by default: the camera sits at the
// player's eyes, their own body is hidden (body layer) and a held-weapon view model is drawn instead. The View
// button switches to third person: over the right shoulder, looking where the player looks (same mouse/stick look
// and centre crosshair), pulled in when a wall or the ground gets between the camera and the sailor. Several ???
// sauce effects live here too: upside down, Dutch tilt, fisheye, wrong view, shake.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TideWall
{
    public class PlayerCamera : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public ViewModel View { get; private set; }
        Player player;
        float shake, roll, fovExtra;
        Vector3 smoothPos;
        bool wasFirstPerson;
        /// <summary>Third person: the camera's place relative to the head pivot, in the look's frame (right, up, back).</summary>
        public static readonly Vector3 Shoulder = new(.75f, .45f, -3.6f);
        const float PivotHeight = 1.55f;

        public static PlayerCamera Create(Player p)
        {
            var go = new GameObject($"Camera P{p.Index + 1}");
            var pc = go.AddComponent<PlayerCamera>();
            pc.player = p;
            pc.Camera = go.AddComponent<Camera>();
            pc.Camera.fieldOfView = 50;
            pc.Camera.nearClipPlane = .05f; pc.Camera.farClipPlane = 220;
            pc.Camera.clearFlags = CameraClearFlags.SolidColor;
            var data = pc.Camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            pc.smoothPos = p.EyePos;
            go.transform.position = pc.smoothPos;
            pc.View = ViewModel.Create(p, pc);
            return pc;
        }

        public void AddShake(float amount) => shake = Mathf.Min(1.2f, shake + amount * Settings.Current.screenShake);

        float kickPitch, kickYaw, punch;

        /// <summary>A gun's recoil: the view snaps up (and a little sideways) and settles back; the FOV punches in.</summary>
        public void Kick(float degrees)
        {
            kickPitch = Mathf.Min(kickPitch + degrees, 14);
            kickYaw += Random.Range(-.35f, .35f) * degrees;
            punch = Mathf.Min(punch + degrees * .5f, 6);
        }

        /// <summary>Which layers this player's camera draws: never other players' view models; your own body only top-down.</summary>
        int Mask(bool firstPerson)
        {
            int mask = ~0;
            for (int i = 0; i < PlayerManager.MaxPlayers; i++)
                if (i != player.Index || !firstPerson) mask &= ~(1 << (Player.ViewLayer + i));
            if (firstPerson) mask &= ~(1 << (Player.BodyLayer + player.Index));
            mask &= ~(1 << (Player.TagLayer + player.Index));   // your own name tag (in front of you in third person)
            return mask;
        }

        /// <summary>Layers for cameras that aren't a player's (lobby, overview): everything except view models.</summary>
        public static int SpectatorMask()
        {
            int mask = ~0;
            for (int i = 0; i < PlayerManager.MaxPlayers; i++) mask &= ~(1 << (Player.ViewLayer + i));
            return mask;
        }

        /// <summary>The FOV setting is the vertical FOV on a 16:9 screen. Keep its horizontal width on wide, short
        /// split-screen strips, so two stacked players don't get a stretched fisheye view.</summary>
        public static float FirstPersonFov(float fov16x9, float aspect)
        {
            if (aspect <= 16f / 9f + .01f) return fov16x9;
            float h = Camera.VerticalToHorizontalFieldOfView(fov16x9, 16f / 9f);
            return Mathf.Max(38, Camera.HorizontalToVerticalFieldOfView(h, aspect));
        }

        void LateUpdate()
        {
            if (player == null) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            var st = player.Statuses;
            Level.SetupCameraSky(Camera, GameManager.I.Phases.SkyColor);
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
            bool fp = player.FirstPerson && !player.IsOut;
            Camera.cullingMask = Mask(fp);
            float targetRoll = st.Has(StatusType.UpsideDown) ? 180 : st.Has(StatusType.DutchTilt) ? 22 : 0;
            if (player.IsDowned && fp) targetRoll += 25;
            roll = Mathf.LerpAngle(roll, targetRoll, 1 - Mathf.Exp(-dt * 4));
            fovExtra = Mathf.Lerp(fovExtra, st.Has(StatusType.Fisheye) ? 55 : 0, 1 - Mathf.Exp(-dt * 3));
            var jitter = shake > 0 ? Random.insideUnitSphere * shake : Vector3.zero;

            kickPitch = Mathf.Lerp(kickPitch, 0, 1 - Mathf.Exp(-dt * 12));
            kickYaw = Mathf.Lerp(kickYaw, 0, 1 - Mathf.Exp(-dt * 12));
            punch = Mathf.Lerp(punch, 0, 1 - Mathf.Exp(-dt * 10));
            Camera.fieldOfView = FirstPersonFov(Settings.Current.fov + fovExtra - punch, Camera.aspect);
            var look = player.LookRotation * Quaternion.Euler(jitter.x * 3 - kickPitch, jitter.y * 3 + kickYaw, 0);
            if (fp)
            {
                // Eyes: steady horizontally, a little smoothing vertically (stairs, ducking when downed).
                var eye = player.EyePos + player.RenderOffset;   // eases across corrections from an online host
                if (!wasFirstPerson) smoothPos = eye;
                smoothPos = new Vector3(eye.x, Mathf.Lerp(smoothPos.y, eye.y, 1 - Mathf.Exp(-dt * 14)), eye.z);
                transform.SetPositionAndRotation(smoothPos, look * Quaternion.Euler(0, 0, roll));
                wasFirstPerson = true;
                return;
            }
            wasFirstPerson = false;

            // Third person: over the right shoulder. Sized with the sailor (giant, tiny, downed).
            float size = st.Has(StatusType.Giant) ? 1.45f : st.Has(StatusType.Tiny) ? .6f : 1;
            var pivot = player.transform.position + player.RenderOffset + Vector3.up * (player.IsDowned ? .8f : PivotHeight * size);
            var want = pivot + look * (Shoulder * Mathf.Max(.8f, size));
            smoothPos = Clear(pivot, want);
            transform.SetPositionAndRotation(smoothPos, look * Quaternion.Euler(0, 0, roll));
        }

        /// <summary>Pull the camera in front of the first wall, rock or ground between the sailor's head and where it wants
        /// to be (creatures, players and loose things don't count, or the view would jump whenever one walked behind).</summary>
        static Vector3 Clear(Vector3 pivot, Vector3 want)
        {
            var dir = want - pivot;
            float dist = dir.magnitude;
            if (dist < .01f) return want;
            dir /= dist;
            var hits = Physics.SphereCastAll(pivot, .2f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = dist;
            foreach (var h in hits)
            {
                var c = h.collider;
                if (c.GetComponentInParent<Player>() || c.GetComponentInParent<Enemy>() || c.GetComponentInParent<Carryable>() || c.GetComponentInParent<Corpse>()) continue;
                if (h.distance > 0 && h.distance < best) best = h.distance;
            }
            return pivot + dir * Mathf.Max(.35f, best - .05f);
        }
    }
}
