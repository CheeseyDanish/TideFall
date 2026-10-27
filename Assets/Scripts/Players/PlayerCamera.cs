// TideWall 3D — one camera per local player (split screen). First person by default: the camera sits at the
// player's eyes, their own body is hidden (body layer) and a held-weapon view model is drawn instead. The View
// button switches to the top-down camera (above and behind, looking out to sea). Several ??? sauce effects live
// here too: upside down, Dutch tilt, fisheye, wrong view, shake.
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
        static readonly Vector3 Offset = new(0, 13.5f, -9f);

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
            pc.smoothPos = p.transform.position + Offset;
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
            World.SetupCameraSky(Camera, GameManager.I.Phases.SkyColor);
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
            bool fp = player.FirstPerson && !player.IsOut;
            Camera.cullingMask = Mask(fp);
            float targetRoll = st.Has(StatusType.UpsideDown) ? 180 : st.Has(StatusType.DutchTilt) ? 22 : 0;
            if (player.IsDowned && fp) targetRoll += 25;
            roll = Mathf.LerpAngle(roll, targetRoll, 1 - Mathf.Exp(-dt * 4));
            fovExtra = Mathf.Lerp(fovExtra, st.Has(StatusType.Fisheye) ? 55 : 0, 1 - Mathf.Exp(-dt * 3));
            var jitter = shake > 0 ? Random.insideUnitSphere * shake : Vector3.zero;

            if (fp)
            {
                // Eyes: steady horizontally, a little smoothing vertically (stairs, ducking when downed).
                var eye = player.EyePos + player.RenderOffset;   // eases across corrections from an online host
                if (!wasFirstPerson) smoothPos = eye;
                smoothPos = new Vector3(eye.x, Mathf.Lerp(smoothPos.y, eye.y, 1 - Mathf.Exp(-dt * 14)), eye.z);
                kickPitch = Mathf.Lerp(kickPitch, 0, 1 - Mathf.Exp(-dt * 12));
                kickYaw = Mathf.Lerp(kickYaw, 0, 1 - Mathf.Exp(-dt * 12));
                punch = Mathf.Lerp(punch, 0, 1 - Mathf.Exp(-dt * 10));
                transform.SetPositionAndRotation(smoothPos, player.LookRotation * Quaternion.Euler(jitter.x * 3 - kickPitch, jitter.y * 3 + kickYaw, roll));
                Camera.fieldOfView = FirstPersonFov(Settings.Current.fov + fovExtra - punch, Camera.aspect);
                wasFirstPerson = true;
                return;
            }
            if (wasFirstPerson) smoothPos = player.transform.position + Offset;
            wasFirstPerson = false;

            // Look a little toward where you're aiming, so you see what's coming.
            var lead = Vector3.ClampMagnitude(player.AimPoint - player.transform.position, 8) * .25f; lead.y = 0;
            // Short, wide viewports (two players stacked) see little depth at a fixed vertical FOV:
            // pull back and look further out to sea so the beach still fits.
            float wide = Mathf.Clamp(Camera.aspect / 1.78f, 1, 1.6f);
            lead += Vector3.forward * (wide - 1) * 5;
            var target = player.transform.position + lead + Offset * wide * (st.Has(StatusType.Giant) ? 1.15f : 1);
            smoothPos = Vector3.Lerp(smoothPos, target, 1 - Mathf.Exp(-dt * 6));
            transform.position = smoothPos + jitter * .4f;
            transform.rotation = Quaternion.LookRotation(player.transform.position + lead + Vector3.up * .5f - smoothPos) * Quaternion.Euler(0, 0, roll);
            Camera.fieldOfView = 50 + fovExtra;
        }
    }
}
