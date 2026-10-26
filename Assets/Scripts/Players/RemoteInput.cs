// TideWall 3D — what a host knows about an online player's controls: the latest input packet from their machine.
// Button presses are accumulated between packets and handed to the Player for exactly one host frame.
using UnityEngine;

namespace TideWall
{
    /// <summary>Where a player is aiming: a ray from their camera (first person: the crosshair; top-down: the cursor
    /// or the stick's reticle), plus the stick direction for top-down gamepads and whether aim assist is on.</summary>
    public struct AimSample
    {
        public Ray ray;
        public Vector3 dirHint;    // top-down gamepad: the direction the stick points (zero = none)
        public bool assist;
    }

    public class RemoteInput
    {
        public Vector2 Move, Nav;
        public float Yaw, Pitch;
        public bool FirstPerson = true;
        public AimSample Aim;
        public Vector3 Position;
        public bool HasPosition;
        public bool Connected = true;
        ushort held, pending, now;

        public bool Held(Btn b) => (held & (1 << (int)b)) != 0;
        public bool Pressed(Btn b) => (now & (1 << (int)b)) != 0;

        /// <summary>A packet arrived (network thread data, applied on the main thread).</summary>
        public void Receive(Vector2 move, Vector2 nav, ushort heldMask, ushort pressedMask, float yaw, float pitch, bool firstPerson, AimSample aim, Vector3 pos)
        {
            Move = Vector2.ClampMagnitude(move, 1);
            Nav = nav;
            held = heldMask;
            pending |= pressedMask;
            Yaw = yaw; Pitch = pitch; FirstPerson = firstPerson; Aim = aim;
            Position = pos; HasPosition = true;
        }

        /// <summary>Start of the host frame: presses that arrived since the last frame become "pressed this frame".</summary>
        public void BeginFrame() { now = pending; pending = 0; }
        public void EndFrame() => now = 0;
    }
}
