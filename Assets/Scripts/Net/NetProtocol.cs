// TideWall 3D — the online protocol: message types, the state structs that travel in snapshots, and binary
// read/write helpers. Every message is one TCP frame: [int32 length][byte type][payload].
//
//   client → host   Hello(version, nick) · Join(token, device) · Input(NetInput) · Leave(player)
//   host → client   Welcome(clientId) · Assigned(token, player) · Refused(token, reason) · Snapshot · Events
//                   · GameOver(reason) · Reload · Bye(reason)
//
// Snapshots (20 Hz) carry the shared state (phase, sand dollars, wall sections, powerplant, tidal cards and any
// vote in progress), every player, the asking client's private
// player data (prompt, open menu), and the replicated things that changed since the last snapshot sent to that
// client (TCP is reliable and ordered, so deltas are safe). Events (every frame) carry effects, sounds, banners
// and per-player feedback (toasts, shakes, hit markers, pushes, teleports).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TideWall
{
    public enum Msg : byte
    {
        Hello = 1, Join = 2, Input = 3, Leave = 4,
        Welcome = 10, Assigned = 11, Refused = 12, Snapshot = 13, Events = 14, GameOver = 15, Reload = 16, Bye = 17,
    }

    public enum NetEvent : byte { Banner = 1, Sound, Fx, Toast, Shake, Hit, Push, Teleport }

    public static class NetProtocol
    {
        public const int Version = 2;
        public const int DefaultPort = 7777;
        public const int DiscoveryPort = 47777;
        public const string DiscoveryTag = "TIDEWALL1";
        public const float SnapshotInterval = .05f;   // 20 Hz
    }

    public struct FxEvent
    {
        public FxKind kind;
        public Vector3 a, b;
        public Color color;
        public float f1, f2, f3;
        public int n;
    }

    /// <summary>One frame of an online player's input, sent by their machine.</summary>
    public struct NetInput
    {
        public int playerIndex;
        public Vector2 move;
        public ushort held, pressed;
        public float yaw, pitch;
        public bool firstPerson;
        public Vector3 pos;
        public AimSample aim;
    }

    /// <summary>A player as the host sees them (what a client needs to draw them and their HUD).</summary>
    public class NetPlayerState
    {
        public int index, owner;
        public string nick = "";
        public Vector3 pos, aim;
        public float yaw, pitch, hp, maxHp, bleedLeft, revive, wet, reload, swing, carryWeight;
        public bool downed, isOut, ready, firstPerson;
        public int ammo, mag, carryCount, kills;
        public int level = 1, xp, xpNext = 60, statPoints, pendingMods;
        public float speedBonus = 1;
        public string gun = "", melee = "";
        public readonly List<(StatusType, float)> statuses = new();

        public static NetPlayerState From(Player p)
        {
            var s = new NetPlayerState
            {
                index = p.Index, owner = p.Owner, nick = p.Nick ?? "", pos = p.transform.position, aim = p.AimPoint, yaw = p.Yaw, pitch = p.Pitch,
                hp = p.Hp, maxHp = p.MaxHp, bleedLeft = p.BleedLeft, revive = p.ReviveProgress, wet = p.Wet, reload = p.ReloadProgress,
                swing = Mathf.Max(0, p.SwingT), carryWeight = p.CarryWeight, downed = p.IsDowned, isOut = p.IsOut, ready = p.Ready,
                firstPerson = p.FirstPerson, ammo = p.Ammo, mag = p.MagSize, carryCount = p.CarryCount, kills = p.Kills,
                gun = p.Gun.id, melee = p.Melee.id,
                level = p.Level, xp = p.Xp, xpNext = p.XpNext, statPoints = p.StatPoints, pendingMods = p.PendingMods, speedBonus = p.SpeedBonus,
            };
            foreach (var kv in p.Statuses.Active) s.statuses.Add((kv.Key, kv.Value));
            return s;
        }

        public void Write(BinaryWriter w)
        {
            w.Write((byte)index); w.Write(owner); w.Write(nick);
            w.Write(pos); w.Write(aim); w.Write(yaw); w.Write(pitch);
            w.Write(hp); w.Write(maxHp); w.Write(bleedLeft); w.Write(revive); w.Write(wet); w.Write(reload); w.Write(swing); w.Write(carryWeight);
            w.Write((byte)((downed ? 1 : 0) | (isOut ? 2 : 0) | (ready ? 4 : 0) | (firstPerson ? 8 : 0)));
            w.Write((short)ammo); w.Write((short)mag); w.Write((byte)carryCount); w.Write(kills);
            w.Write(gun); w.Write(melee);
            w.Write((short)level); w.Write(xp); w.Write(xpNext); w.Write((short)statPoints); w.Write((byte)pendingMods); w.Write(speedBonus);
            w.Write((byte)statuses.Count);
            foreach (var (t, left) in statuses) { w.Write((byte)t); w.Write(left); }
        }

        public static NetPlayerState Read(BinaryReader r)
        {
            var s = new NetPlayerState
            {
                index = r.ReadByte(), owner = r.ReadInt32(), nick = r.ReadString(),
                pos = r.ReadVector3(), aim = r.ReadVector3(), yaw = r.ReadSingle(), pitch = r.ReadSingle(),
                hp = r.ReadSingle(), maxHp = r.ReadSingle(), bleedLeft = r.ReadSingle(), revive = r.ReadSingle(), wet = r.ReadSingle(),
                reload = r.ReadSingle(), swing = r.ReadSingle(), carryWeight = r.ReadSingle(),
            };
            byte f = r.ReadByte();
            s.downed = (f & 1) != 0; s.isOut = (f & 2) != 0; s.ready = (f & 4) != 0; s.firstPerson = (f & 8) != 0;
            s.ammo = r.ReadInt16(); s.mag = r.ReadInt16(); s.carryCount = r.ReadByte(); s.kills = r.ReadInt32();
            s.gun = r.ReadString(); s.melee = r.ReadString();
            s.level = r.ReadInt16(); s.xp = r.ReadInt32(); s.xpNext = r.ReadInt32(); s.statPoints = r.ReadInt16(); s.pendingMods = r.ReadByte(); s.speedBonus = r.ReadSingle();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) s.statuses.Add(((StatusType)r.ReadByte(), r.ReadSingle()));
            return s;
        }
    }

    /// <summary>A replicated thing: an enemy or its corpse, a gas can, a picnic food.</summary>
    public struct NetEntityState
    {
        public int id;
        public NetKind kind;
        public int def;
        public Vector3 pos, rootPos;
        public Quaternion rot, rootRot;
        public float rootScale, wet;
        public byte flags;          // enemy: 1 dead, 2 hit flash, 4 burning, 8 grounded, 16 attacking · gas can: 1 leaking
        public uint severed;        // bit i = limb i is gone
        public int value;           // corpse value

        public void Write(BinaryWriter w)
        {
            w.Write(id); w.Write((byte)kind); w.Write((short)def);
            w.Write(pos); w.Write(rot);
            if (kind == NetKind.Enemy) { w.Write(rootPos); w.Write(rootRot); w.Write(rootScale); w.Write(wet); w.Write(severed); w.Write(value); }
            w.Write(flags);
        }

        public static NetEntityState Read(BinaryReader r)
        {
            var s = new NetEntityState { id = r.ReadInt32(), kind = (NetKind)r.ReadByte(), def = r.ReadInt16(), pos = r.ReadVector3(), rot = r.ReadQuaternion(), rootScale = 1 };
            if (s.kind == NetKind.Enemy)
            {
                s.rootPos = r.ReadVector3(); s.rootRot = r.ReadQuaternion(); s.rootScale = r.ReadSingle(); s.wet = r.ReadSingle();
                s.severed = r.ReadUInt32(); s.value = r.ReadInt32();
            }
            s.flags = r.ReadByte();
            return s;
        }
    }

    public static class NetIO
    {
        public static BinaryWriter Begin(Msg type)
        {
            var w = new BinaryWriter(new MemoryStream(256));
            w.Write((byte)type);
            return w;
        }

        public static byte[] Bytes(this BinaryWriter w) => ((MemoryStream)w.BaseStream).ToArray();

        public static void Write(this BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        public static void Write(this BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        public static void Write(this BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        public static void Write(this BinaryWriter w, Color c) { w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }
        public static Vector3 ReadVector3(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public static Vector2 ReadVector2(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
        public static Quaternion ReadQuaternion(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public static Color ReadColor(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        public static void Write(this BinaryWriter w, in NetInput i)
        {
            w.Write((byte)i.playerIndex); w.Write(i.move); w.Write(i.held); w.Write(i.pressed);
            w.Write(i.yaw); w.Write(i.pitch); w.Write(i.firstPerson); w.Write(i.pos);
            w.Write(i.aim.ray.origin); w.Write(i.aim.ray.direction); w.Write(i.aim.dirHint); w.Write(i.aim.assist);
        }

        public static NetInput ReadInput(this BinaryReader r)
        {
            var i = new NetInput
            {
                playerIndex = r.ReadByte(), move = r.ReadVector2(), held = r.ReadUInt16(), pressed = r.ReadUInt16(),
                yaw = r.ReadSingle(), pitch = r.ReadSingle(), firstPerson = r.ReadBoolean(), pos = r.ReadVector3(),
            };
            var o = r.ReadVector3(); var d = r.ReadVector3();
            i.aim = new AimSample { ray = new Ray(o, d.sqrMagnitude > .0001f ? d : Vector3.forward), dirHint = r.ReadVector3(), assist = r.ReadBoolean() };
            return i;
        }

        public static void Write(this BinaryWriter w, in FxEvent e)
        {
            w.Write((byte)e.kind); w.Write(e.a); w.Write(e.b); w.Write(e.color); w.Write(e.f1); w.Write(e.f2); w.Write(e.f3); w.Write((short)e.n);
        }

        public static FxEvent ReadFx(this BinaryReader r) => new()
        {
            kind = (FxKind)r.ReadByte(), a = r.ReadVector3(), b = r.ReadVector3(), color = r.ReadColor(),
            f1 = r.ReadSingle(), f2 = r.ReadSingle(), f3 = r.ReadSingle(), n = r.ReadInt16(),
        };

        /// <summary>A menu's rows (labels and values only: picking happens on the host).</summary>
        public static void WriteMenu(this BinaryWriter w, Menu m)
        {
            w.Write(m != null);
            if (m == null) return;
            w.Write(m.Title ?? ""); w.Write((short)m.Selected); w.Write(m.System);
            w.Write((short)m.Items.Count);
            foreach (var it in m.Items)
            {
                w.Write(it.Label ?? ""); w.Write(it.Right ?? ""); w.Write(it.Detail ?? ""); w.Write(it.Header);
                w.Write(it.Blocked?.Invoke() != null);
            }
        }

        public static Menu ReadMenu(this BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var m = new Menu { Title = r.ReadString(), Selected = r.ReadInt16(), System = r.ReadBoolean() };
            int n = r.ReadInt16();
            for (int i = 0; i < n; i++)
            {
                var it = new MenuItem { Label = r.ReadString(), Right = r.ReadString(), Detail = r.ReadString(), Header = r.ReadBoolean() };
                if (r.ReadBoolean()) it.Blocked = () => "";
                m.Items.Add(it);
            }
            return m;
        }

        /// <summary>The bytes of one entity state (the host compares these to send only what changed).</summary>
        public static byte[] Pack(in NetEntityState s)
        {
            using var ms = new MemoryStream(96);
            using var w = new BinaryWriter(ms);
            s.Write(w);
            return ms.ToArray();
        }

        public static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
