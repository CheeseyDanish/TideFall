// TideWall 3D — what a host knows about an online player's controls: the numbered commands their machine sent.
// The host takes each new command once, in order (NetHost → Player.RunRemoteCommands), moves the player itself,
// and reports the last one it used so the client can reconcile. Everything from the wire is checked here first.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>Where a player is aiming: the crosshair's ray from their camera (eyes, or over the shoulder in third
    /// person), and whether aim assist is on. (dirHint is unused since the top-down view became third person.)</summary>
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
        public bool Connected = true;
        /// <summary>The newest command received, and the newest one simulated (sent back to the client).</summary>
        public uint Received { get; private set; }
        public uint Processed { get; private set; }

        /// <summary>Movement time a client may use: it grows with real time, so sending commands faster than the
        /// game runs (a speed hack) just runs out of budget.</summary>
        const float MaxBudget = .5f;
        const int MaxQueued = 64;
        readonly Queue<NetCommand> queue = new();
        float budget;
        ushort held, pending, now;

        public bool Held(Btn b) => (held & (1 << (int)b)) != 0;
        public bool Pressed(Btn b) => (now & (1 << (int)b)) != 0;

        /// <summary>A command arrived. Repeats (commands are sent several times) and anything malformed are ignored.</summary>
        public void Receive(NetCommand c)
        {
            if (c.seq <= Received) return;
            if (!Finite(c.move) || !Finite(c.nav) || !Finite(c.yaw) || !Finite(c.pitch) || !Finite(c.dt)) return;
            c.move = Vector2.ClampMagnitude(c.move, 1);
            c.nav = Vector2.ClampMagnitude(c.nav, 1);
            c.pitch = Mathf.Clamp(c.pitch, -89, 89);
            c.dt = Mathf.Clamp(c.dt, 0, NetCommand.MaxDt);
            Received = c.seq;
            if (queue.Count >= MaxQueued) queue.Dequeue();
            queue.Enqueue(c);
        }

        public void ReceiveAim(AimSample a)
        {
            if (!Finite(a.ray.origin) || !Finite(a.ray.direction) || !Finite(a.dirHint)) return;
            a.ray = new Ray(new Vector3(Mathf.Clamp(a.ray.origin.x, -150, 150), Mathf.Clamp(a.ray.origin.y, -10, 80), Mathf.Clamp(a.ray.origin.z, -90, 200)), a.ray.direction);
            a.dirHint = Vector3.ClampMagnitude(a.dirHint, 1);
            Aim = a;
        }

        /// <summary>Host frame: real time passed, so this much more movement is allowed.</summary>
        public void AddBudget(float dt) => budget = Mathf.Min(MaxBudget, budget + dt);

        /// <summary>The next command to simulate. Its buttons and look always count; it may only move the player while
        /// there's budget (canMove).</summary>
        public bool Next(out NetCommand c, out bool canMove)
        {
            canMove = false;
            if (queue.Count == 0) { c = default; return false; }
            c = queue.Dequeue();
            Processed = c.seq;
            Move = c.move; Nav = c.nav; Yaw = c.yaw; Pitch = c.pitch; FirstPerson = c.firstPerson;
            held = c.held;
            pending |= c.pressed;
            if (budget >= c.dt - .0001f) { budget = Mathf.Max(0, budget - c.dt); canMove = true; }
            return true;
        }

        /// <summary>Start of the host frame: presses that arrived since the last frame become "pressed this frame".</summary>
        public void BeginFrame() { now = pending; pending = 0; }
        public void EndFrame() => now = 0;

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Vector2 v) => Finite(v.x) && Finite(v.y);
    }
}
