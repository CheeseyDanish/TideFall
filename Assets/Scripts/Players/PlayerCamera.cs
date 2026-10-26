// TideWall 3D — one camera per player (split screen). Follows from above and behind (looking out to sea),
// and is where several ??? sauce effects live: upside down, Dutch tilt, fisheye, first-person POV, shake.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TideWall
{
    public class PlayerCamera : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        Player player;
        float shake, roll, fovExtra;
        Vector3 smoothPos;
        static readonly Vector3 Offset = new(0, 13.5f, -9f);

        public static PlayerCamera Create(Player p)
        {
            var go = new GameObject($"Camera P{p.Index + 1}");
            var pc = go.AddComponent<PlayerCamera>();
            pc.player = p;
            pc.Camera = go.AddComponent<Camera>();
            pc.Camera.fieldOfView = 50;
            pc.Camera.nearClipPlane = .1f; pc.Camera.farClipPlane = 220;
            pc.Camera.clearFlags = CameraClearFlags.SolidColor;
            var data = pc.Camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            pc.smoothPos = p.transform.position + Offset;
            go.transform.position = pc.smoothPos;
            return pc;
        }

        public void AddShake(float amount) => shake = Mathf.Min(1.2f, shake + amount);

        void LateUpdate()
        {
            if (player == null) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            var st = player.Statuses;
            Camera.backgroundColor = GameManager.I.Phases.SkyColor;
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);

            if (st.Has(StatusType.FirstPerson))
            {
                // ??? sauce: see the world from behind your own eyes
                var eye = player.transform.position + Vector3.up * (1.7f * (st.Has(StatusType.Giant) ? 1.45f : st.Has(StatusType.Tiny) ? .6f : 1));
                transform.SetPositionAndRotation(eye + player.AimDir * .3f, Quaternion.LookRotation(player.AimDir + Vector3.down * .15f));
                Camera.fieldOfView = 75;
                return;
            }

            // Look a little toward where you're aiming, so you see what's coming.
            var lead = Vector3.ClampMagnitude(player.AimPoint - player.transform.position, 8) * .25f; lead.y = 0;
            // Short, wide viewports (two players stacked) see little depth at a fixed vertical FOV:
            // pull back and look further out to sea so the beach still fits.
            float wide = Mathf.Clamp(Camera.aspect / 1.78f, 1, 1.6f);
            lead += Vector3.forward * (wide - 1) * 5;
            var target = player.transform.position + lead + Offset * wide * (st.Has(StatusType.Giant) ? 1.15f : 1);
            smoothPos = Vector3.Lerp(smoothPos, target, 1 - Mathf.Exp(-dt * 6));
            var jitter = shake > 0 ? Random.insideUnitSphere * shake * .4f : Vector3.zero;
            transform.position = smoothPos + jitter;

            float targetRoll = st.Has(StatusType.UpsideDown) ? 180 : st.Has(StatusType.DutchTilt) ? 22 : 0;
            roll = Mathf.LerpAngle(roll, targetRoll, 1 - Mathf.Exp(-dt * 4));
            transform.rotation = Quaternion.LookRotation(player.transform.position + lead + Vector3.up * .5f - smoothPos) * Quaternion.Euler(0, 0, roll);

            fovExtra = Mathf.Lerp(fovExtra, st.Has(StatusType.Fisheye) ? 55 : 0, 1 - Mathf.Exp(-dt * 3));
            Camera.fieldOfView = 50 + fovExtra;
        }
    }
}
