// TideWall 3D — the online protocol: message types, the state that travels in snapshots, and binary read/write
// helpers. Every message is [byte type][payload], sent on one of NetLink's two channels:
//
//   client → host   Hello(version, nick, nonce) R · Auth(proof) R · Join(token, device) R · Leave(player) R
//                   · Input(ack, per player: aim + the last few commands) U
//   host → client   Challenge(salt, nonce) R · Welcome(clientId, hostNick, scene) R · Assigned(token, player) R
//                   · Refused(token, reason) R · Notices(banners, toasts, shards, endings) R · GameOver(reason) R
//                   · Reload(scene) R · Bye(reason) R
//                   · Snapshot(seq, …) U · Effects(sounds, particles, shakes, hit markers) U
//   (R = reliable, U = unreliable)
//
// Movement is the host's: each client sends numbered input commands (NetCommand), the host simulates them and
// tells the client which one it got to; the client predicts its own movement and replays what the host hasn't
// confirmed yet (Player.Reconcile). Commands are sent several times over (every unconfirmed one, up to
// MaxCommandsPerPacket), so a lost packet costs nothing.
//
// Snapshots (20 Hz) carry the shared state, every player, the client's own players' private data (prompt, menu,
// confirmed movement), and replicated things as a delta against the last snapshot that client confirmed
// (Quake-style): what changed since then, plus the ids of everything that exists now.
//
// Security: version check; an optional room password checked by challenge-response (PBKDF2 + HMAC-SHA256, the
// password never crosses the network); Relay games are encrypted (DTLS). The host checks everything it reads
// (sizes, ownership, finite numbers, positions inside the world) and simulates movement itself.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace TideWall
{
    public enum Msg : byte
    {
        Hello = 1, Join = 2, Input = 3, Leave = 4, Auth = 5,
        Welcome = 10, Assigned = 11, Refused = 12, Snapshot = 13, Effects = 14, GameOver = 15, Reload = 16, Bye = 17,
        Challenge = 18, Notices = 19,
    }

    /// <summary>Things that happen once. Banners and toasts go reliably (Notices); the rest are cosmetic (Effects).</summary>
    public enum NetEvent : byte { Banner = 1, Sound, Fx, Toast, Shake, Hit, Shard, Ending }

    public static class NetProtocol
    {
        public const int Version = 6;
        public const int DefaultPort = 7777;
        public const int DiscoveryPort = 47777;
        public const string DiscoveryTag = "TIDEWALL2";
        public const float SnapshotInterval = .05f;     // 20 Hz
        public const float InputInterval = 1 / 60f;     // clients send input at most 60 times a second
        public const int MaxCommandsPerPacket = 24;
        public const int MaxLocalPlayers = 4;
        public const int MaxConnections = 12;           // including ones still saying hello
        public const float HelloTimeout = 10;           // seconds to finish the handshake
    }

    /// <summary>One frame of a player's controls, numbered. Quantized exactly as it travels, so the client predicts
    /// with the same numbers the host simulates.</summary>
    public struct NetCommand
    {
        public uint seq;
        public float dt;
        public Vector2 move, nav;
        public ushort held, pressed;
        public float yaw, pitch;
        public bool firstPerson;

        public const float MaxDt = .1f;

        public bool Has(Btn b) => (pressed & (1 << (int)b)) != 0;

        public NetCommand Quantized()
        {
            var c = this;
            c.dt = Mathf.Round(Mathf.Clamp(dt, 0, MaxDt) * 10000) / 10000;
            c.move = Q(move); c.nav = Q(nav);
            return c;
        }

        static Vector2 Q(Vector2 v) => new(Mathf.Round(Mathf.Clamp(v.x, -1, 1) * 127) / 127, Mathf.Round(Mathf.Clamp(v.y, -1, 1) * 127) / 127);

        public void Write(BinaryWriter w)
        {
            w.Write(seq); w.Write((ushort)Mathf.RoundToInt(Mathf.Clamp(dt, 0, MaxDt) * 10000));
            w.Write((sbyte)Mathf.RoundToInt(Mathf.Clamp(move.x, -1, 1) * 127)); w.Write((sbyte)Mathf.RoundToInt(Mathf.Clamp(move.y, -1, 1) * 127));
            w.Write((sbyte)Mathf.RoundToInt(Mathf.Clamp(nav.x, -1, 1) * 127)); w.Write((sbyte)Mathf.RoundToInt(Mathf.Clamp(nav.y, -1, 1) * 127));
            w.Write(held); w.Write(pressed); w.Write(yaw); w.Write(pitch); w.Write(firstPerson);
        }

        public static NetCommand Read(BinaryReader r) => new()
        {
            seq = r.ReadUInt32(), dt = r.ReadUInt16() / 10000f,
            move = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f), nav = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f),
            held = r.ReadUInt16(), pressed = r.ReadUInt16(), yaw = r.ReadSingle(), pitch = r.ReadSingle(), firstPerson = r.ReadBoolean(),
        };
    }

    /// <summary>The room password, checked without ever sending it: the host sends a random salt and nonce, the client
    /// answers with HMAC-SHA256(PBKDF2(password, salt), host nonce + client nonce).</summary>
    public static class NetAuth
    {
        public const int Iterations = 20000;

        public static byte[] Nonce()
        {
            var b = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(b);
            return b;
        }

        public static byte[] Key(string password, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(password ?? "", salt, Iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(32);
        }

        public static byte[] Proof(byte[] key, byte[] hostNonce, byte[] clientNonce)
        {
            var msg = new byte[hostNonce.Length + clientNonce.Length];
            Buffer.BlockCopy(hostNonce, 0, msg, 0, hostNonce.Length);
            Buffer.BlockCopy(clientNonce, 0, msg, hostNonce.Length, clientNonce.Length);
            using var mac = new HMACSHA256(key);
            return mac.ComputeHash(msg);
        }

        /// <summary>Compare without leaking how many bytes matched.</summary>
        public static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    public struct FxEvent
    {
        public FxKind kind;
        public Vector3 a, b;
        public Color color;
        public float f1, f2, f3;
        public int n;
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
        /// <summary>The reload minigame to draw at the reticle (null: none), the Perfect bonus, the kill combo.</summary>
        public ReloadGame reloadGame;
        public float perfect, comboLeft;
        public int combo;

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
                reloadGame = p.ReloadView, perfect = p.PerfectBonus, combo = p.Combo, comboLeft = p.ComboLeft,
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
            w.Write(reloadGame != null);
            reloadGame?.Write(w);
            w.Write(perfect); w.Write((short)combo); w.Write(comboLeft);
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
            if (r.ReadBoolean()) s.reloadGame = ReloadGame.Read(r);
            s.perfect = r.ReadSingle(); s.combo = r.ReadInt16(); s.comboLeft = r.ReadSingle();
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
        public byte flags;          // enemy: 1 dead, 2 hit flash, 4 burning, 8 grounded, 16 attacking, 32 surfing · gas can: 1 leaking
        public uint severed;        // bit i = limb i is gone
        public int value;           // corpse value · structure HP

        public void Write(BinaryWriter w)
        {
            w.Write(id); w.Write((byte)kind); w.Write((short)def);
            w.Write(pos); w.Write(rot);
            if (kind == NetKind.Enemy) { w.Write(rootPos); w.Write(rootRot); w.Write(rootScale); w.Write(wet); w.Write(severed); w.Write(value); }
            else if (kind == NetKind.Structure) { w.Write(rootRot); w.Write(value); }
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
            else if (s.kind == NetKind.Structure) { s.rootRot = r.ReadQuaternion(); s.value = r.ReadInt32(); }
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

        public static void Write(this BinaryWriter w, in AimSample a)
        {
            w.Write(a.ray.origin); w.Write(a.ray.direction); w.Write(a.dirHint); w.Write(a.assist);
        }

        public static AimSample ReadAim(this BinaryReader r)
        {
            var o = r.ReadVector3(); var d = r.ReadVector3();
            return new AimSample { ray = new Ray(o, d.sqrMagnitude > .0001f ? d : Vector3.forward), dirHint = r.ReadVector3(), assist = r.ReadBoolean() };
        }

        /// <summary>A string from the network, refused if it claims to be longer than `max` characters.</summary>
        public static string ReadText(this BinaryReader r, int max)
        {
            var s = r.ReadString();
            if (s.Length > max) throw new InvalidDataException($"text longer than {max}");
            return s;
        }

        public static byte[] ReadBlob(this BinaryReader r, int length)
        {
            var b = r.ReadBytes(length);
            if (b.Length != length) throw new EndOfStreamException();
            return b;
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
            w.Write((byte)m.Layout); w.Write((byte)m.Tab);
            w.Write((byte)(m.Tabs?.Length ?? 0));
            if (m.Tabs != null) foreach (var t in m.Tabs) w.Write(t);
            w.Write((short)m.Items.Count);
            foreach (var it in m.Items)
            {
                w.Write(it.Label ?? ""); w.Write(it.Right ?? ""); w.Write(it.Detail ?? ""); w.Write(it.Header);
                w.Write(it.Blocked?.Invoke() != null);
                w.Write(it.Icon ?? ""); w.Write(it.Info ?? ""); w.Write(it.Tint); w.Write(it.Owned); w.Write(it.Equipped);
            }
        }

        public static Menu ReadMenu(this BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var m = new Menu { Title = r.ReadString(), Selected = r.ReadInt16(), System = r.ReadBoolean() };
            m.Layout = (MenuLayout)r.ReadByte(); m.Tab = r.ReadByte();
            int tabs = r.ReadByte();
            if (tabs > 0) { m.Tabs = new string[tabs]; for (int i = 0; i < tabs; i++) m.Tabs[i] = r.ReadString(); }
            int n = r.ReadInt16();
            for (int i = 0; i < n; i++)
            {
                var it = new MenuItem { Label = r.ReadString(), Right = r.ReadString(), Detail = r.ReadString(), Header = r.ReadBoolean() };
                if (r.ReadBoolean()) it.Blocked = () => "";
                it.Icon = r.ReadString(); it.Info = r.ReadString(); it.Tint = r.ReadColor(); it.Owned = r.ReadBoolean(); it.Equipped = r.ReadBoolean();
                if (it.Icon.Length == 0) it.Icon = null;
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
