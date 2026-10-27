// TideWall 3D — Player: walking, dashing and gravity; online movement (host commands, client prediction and reconciliation).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ movement
        float SpeedMultiplier()
        {
            float m = 1;
            if (Statuses.Has(StatusType.Speed)) m *= 1.4f;
            if (Statuses.Has(StatusType.Slow)) m *= .6f;
            if (Statuses.Has(StatusType.Giant)) m *= .9f;
            if (Statuses.Has(StatusType.Tiny)) m *= 1.15f;
            if (Arena.InWater(transform.position)) m *= Mathf.Lerp(1, db.playerWaterSpeedMult, Mathf.Clamp01(Arena.Depth(transform.position) / .8f));
            m *= 1 - (1 - db.carrySpeedMult) * CarryWeight / MaxCarry;
            if (swingT > 0 && Melee.id == "anchor") m *= .4f;
            return m * SpeedBonus;
        }

        void UpdateMovement(float dt)
        {
            // Online players are moved by their commands as they arrive (RunRemoteCommands); here they only turn
            if (Kind == PlayerKind.Remote) { FaceAim(dt); return; }
            var raw = Controls != null ? Controls.Move.ReadValue<Vector2>() : Vector2.zero;
            var dir = StepMove(raw, Pressed(Btn.Dash), Yaw, dt);
            FaceAim(dt);
        }

        /// <summary>
        /// One step of movement from one frame of controls (raw stick/WASD). The same code moves local players, online
        /// players on the host (one call per command) and a client's prediction and replays, so they all agree.
        /// Returns the world direction moved in.
        /// </summary>
        Vector3 StepMove(Vector2 move, bool dash, float yaw, float dt)
        {
            if (IsOut || dt <= 0) return Vector3.zero;
            if (Noclip(move, dash, yaw, dt)) return Vector3.zero;
            if (Statuses.Has(StatusType.ReverseControls)) move = -move;
            var dir = new Vector3(move.x, 0, move.y);
            dir = Quaternion.Euler(0, yaw, 0) * dir;   // relative to the view, first or third person
            if (dir.magnitude > 1) dir.Normalize();
            if (grabbedBy != null || Statuses.Has(StatusType.Grabbed)) dir = Vector3.zero;
            if (IsDowned) { Gravity(dt, dir); return dir; }   // crawl slowly toward safety
            dashCd -= dt;
            if (dash && dashCd <= 0 && dir.sqrMagnitude > .01f)
            {
                dashT = db.dashSeconds; dashCd = db.dashCooldown / (1 + (SpeedBonus - 1) * 2); dashDir = dir.normalized;
                if (Simulated) Fx.Smoke(transform.position, 4, .3f, new Color(.9f, .85f, .7f, .5f));
            }
            Vector3 vel = dir * db.playerSpeed * SpeedMultiplier();
            if (dashT > 0) { dashT -= dt; vel = dashDir * db.dashSpeed; }
            vel += push / dt; push = Vector3.zero;
            Gravity(dt, vel);
            return dir;
        }

        /// <summary>Host: an online player's new commands, in order. Buttons and look always count; movement only
        /// within the real time that has passed (RemoteInput's budget), so a sped-up client can't outrun the game.</summary>
        public void RunRemoteCommands(float frameDt)
        {
            Remote.AddBudget(frameDt);
            bool canPlay = gm.State == GameState.Playing && Time.timeScale > 0;
            while (Remote.Next(out var c, out bool canMove))
            {
                PrefersFirstPerson = c.firstPerson;
                if (canMove && canPlay) StepMove(c.move, c.Has(Btn.Dash), c.yaw, c.dt);
            }
        }

        /// <summary>Client: this frame's controls as a numbered command. We move by it straight away (prediction)
        /// and keep it until the host confirms it.</summary>
        void PredictMove(float dt, bool canMove)
        {
            var c = new NetCommand { seq = ++commandSeq, dt = dt, yaw = Yaw, pitch = Pitch, firstPerson = PrefersFirstPerson };
            if (statsWanted) { c.pressed |= 1 << (int)Btn.Stats; statsWanted = false; }
            if (Controls != null && LocalMenu == null && !Rebinder.Busy)
            {
                c.nav = Controls.Nav();
                if (canMove) c.move = Controls.Move.ReadValue<Vector2>();
                foreach (Btn b in System.Enum.GetValues(typeof(Btn)))
                {
                    if (b is Btn.Pause or Btn.View) continue;  // handled here (Stats goes to the host, which owns that menu)
                    if (Controls.Held(b)) c.held |= (ushort)(1 << (int)b);
                    if (Controls.Pressed(b)) c.pressed |= (ushort)(1 << (int)b);
                }
            }
            c = c.Quantized();
            if (canMove) StepMove(c.move, c.Has(Btn.Dash), c.yaw, c.dt);
            unconfirmed.Add(c);
            if (unconfirmed.Count > 240) unconfirmed.RemoveAt(0);   // the host has stopped answering: don't grow forever
            if (Cam != null) lastAim = LocalAim();
        }

        /// <summary>Client: the commands to send, newest last (every unconfirmed one, up to max), with the current aim.</summary>
        public void WriteCommands(System.IO.BinaryWriter w, int max)
        {
            w.Write((byte)Index);
            w.Write(lastAim);
            int from = Mathf.Max(0, unconfirmed.Count - max);
            w.Write((byte)(unconfirmed.Count - from));
            for (int i = from; i < unconfirmed.Count; i++) unconfirmed[i].Write(w);
        }

        /// <summary>What the host's simulation says about a player's movement (sent to the player's own machine).</summary>
        public struct MoveState
        {
            public uint confirmed;
            public Vector3 pos, dashDir;
            public float yVel, dashT, dashCd;
        }

        public void WriteMoveState(System.IO.BinaryWriter w)
        {
            w.Write(Remote?.Processed ?? 0); w.Write(transform.position); w.Write(dashDir);
            w.Write(yVel); w.Write(dashT); w.Write(dashCd);
        }

        public static MoveState ReadMoveState(System.IO.BinaryReader r) => new()
        {
            confirmed = r.ReadUInt32(), pos = r.ReadVector3(), dashDir = r.ReadVector3(),
            yVel = r.ReadSingle(), dashT = r.ReadSingle(), dashCd = r.ReadSingle(),
        };

        /// <summary>
        /// Client: the host says where we were after command `confirmed`. Start from there and replay every command
        /// it hasn't seen yet. Usually that lands exactly where we already are; when it doesn't (a push, a grab, a
        /// collision we didn't predict) we take the host's answer and let the camera ease across the difference.
        /// </summary>
        public void Reconcile(MoveState m)
        {
            unconfirmed.RemoveAll(c => c.seq <= m.confirmed);
            var predicted = transform.position;
            Place(m.pos);
            yVel = m.yVel; dashT = m.dashT; dashCd = m.dashCd; dashDir = m.dashDir;
            bool canMove = !IsOut && gm.State == GameState.Playing;
            if (canMove) foreach (var c in unconfirmed) StepMove(c.move, c.Has(Btn.Dash), c.yaw, c.dt);
            var error = predicted - transform.position;
            RenderOffset = error.magnitude < 3 ? RenderOffset + error : Vector3.zero;
        }

        void FaceAim(float dt)
        {
            var target = Quaternion.Euler(0, Yaw, 0);   // the body faces where you look
            visual.rotation = Quaternion.RotateTowards(visual.rotation, target, 900 * dt);
        }

        void Gravity(float dt, Vector3 vel)
        {
            yVel = cc.isGrounded ? -2 : yVel + Physics.gravity.y * dt;
            cc.Move((vel + Vector3.up * yVel) * dt);
            // Never sink below the sand/seabed (CharacterController + MeshCollider ground also handles this)
            var p = transform.position;
            float g = Arena.GroundY(p.z);
            if (p.y < g - .05f) { p.y = g; transform.position = p; yVel = 0; }
        }

        /// <summary>Debug noclip (this machine's players): fly where you look, through anything; Dash goes fast. True while flying.</summary>
        bool Noclip(Vector2 move, bool dash, float yaw, float dt)
        {
            bool on = DebugTools.Noclip && Kind == PlayerKind.Local;
            if (!on) { if (flying) { flying = false; cc.enabled = true; } return false; }
            if (!flying) { flying = true; cc.enabled = false; yVel = 0; }
            var dir = Quaternion.Euler(Pitch, yaw, 0) * new Vector3(move.x, 0, move.y);
            if (dash) flyBoost = .6f;
            flyBoost -= dt;
            transform.position += dir * db.playerSpeed * (flyBoost > 0 ? 6 : 2) * dt;
            return true;
        }
        bool flying;
        float flyBoost;

        /// <summary>Shove the player (knockback, an octopus pulling). Applied on their next movement step; an online
        /// player's machine catches up through reconciliation.</summary>
        public void Push(Vector3 delta) => push += delta;

        /// <summary>Put the player somewhere directly (respawns, tests). The CharacterController has to be off for it.</summary>
        public void Place(Vector3 at)
        {
            cc.enabled = false; transform.position = at; cc.enabled = true;
        }
    }
}
