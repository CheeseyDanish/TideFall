// TideWall 3D — the host side of online play. Accepts TCP clients, gives each joining player a slot (a Remote
// Player simulated here), applies their input every frame, and streams snapshots (20 Hz, deltas) and events
// (every frame) back. Also broadcasts a LAN beacon so the Join screen can list the game.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TideWall
{
    [DefaultExecutionOrder(-100)]   // inputs land before players update
    public class NetHost : MonoBehaviour
    {
        class Client
        {
            public NetConnection conn;
            public string nick = "Sailor";
            public bool hello;
            public readonly Dictionary<int, string> tokens = new();     // join token → device name (kept across "try again")
            public readonly Dictionary<int, int> players = new();       // join token → player index
            public readonly Dictionary<int, byte[]> sent = new();       // entity id → last bytes sent
            public BinaryWriter events = NewEvents();
            public int eventCount;
        }

        public int Port { get; private set; }
        public string Nick { get; private set; }
        public int ClientCount { get { int n = 0; foreach (var c in clients) if (c.hello) n++; return n; } }
        public string Addresses => $"Friends can join at {NetDiscovery.LocalAddresses()} (port {Port}), or find you under Join on the same network.";

        TcpListener listener;
        readonly ConcurrentQueue<TcpClient> accepted = new();
        readonly List<Client> clients = new();
        int nextId = 1;
        float snapT, beaconT;
        BinaryWriter allEvents = NewEvents();
        int allCount;
        volatile bool stopping;

        static BinaryWriter NewEvents() => new(new MemoryStream(512));

        public bool Listen(int port, string nick)
        {
            Port = port; Nick = string.IsNullOrWhiteSpace(nick) ? "Host" : nick;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
            }
            catch (Exception e) { Debug.LogWarning($"TideWall: can't host on port {port}: {e.Message}"); return false; }
            var l = listener;
            new Thread(() =>
            {
                try { while (!stopping) accepted.Enqueue(l.AcceptTcpClient()); }
                catch (Exception) { }
            }) { IsBackground = true, Name = "TideWall accept" }.Start();
            return true;
        }

        public void Stop(string reason)
        {
            stopping = true;
            var bye = NetIO.Begin(Msg.Bye); bye.Write(reason ?? "");
            foreach (var c in clients) c.conn.SendAndClose(bye.Bytes());
            clients.Clear();
            try { listener?.Stop(); } catch (Exception) { }
        }

        void OnDestroy() { if (!stopping) Stop("The host closed the game."); }

        // ------------------------------------------------------------------ receiving
        void Update()
        {
            var gm = GameManager.I;
            while (accepted.TryDequeue(out var tcp))
                clients.Add(new Client { conn = new NetConnection(nextId++, tcp) });
            for (int i = clients.Count - 1; i >= 0; i--)
            {
                var c = clients[i];
                while (c.conn.Inbox.TryDequeue(out var data)) Handle(c, data, gm);
                if (c.conn.Closed) Drop(c, gm);
            }
        }

        void Handle(Client c, byte[] data, GameManager gm)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            var type = (Msg)r.ReadByte();
            switch (type)
            {
                case Msg.Hello:
                {
                    int version = r.ReadInt32();
                    c.nick = Trim(r.ReadString());
                    if (version != NetProtocol.Version) { Kick(c, "Your TideWall is a different version from the host's."); return; }
                    c.hello = true;
                    var w = NetIO.Begin(Msg.Welcome); w.Write(c.conn.Id); w.Write(Nick);
                    c.conn.Send(w.Bytes());
                    break;
                }
                case Msg.Join:
                {
                    if (!c.hello || gm == null) return;
                    int token = r.ReadInt32();
                    string device = r.ReadString();
                    c.tokens[token] = device;
                    JoinPlayer(c, token, gm);
                    break;
                }
                case Msg.Input:
                {
                    if (gm == null) return;
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++)
                    {
                        var input = r.ReadInput();
                        var p = gm.Players.ByIndex(input.playerIndex);
                        if (p == null || p.Kind != PlayerKind.Remote || p.Owner != c.conn.Id) continue;
                        p.Remote.Receive(input.move, input.held, input.pressed, input.yaw, input.pitch, input.firstPerson, input.aim, input.pos);
                    }
                    break;
                }
                case Msg.Leave:
                {
                    int index = r.ReadByte();
                    var p = gm?.Players.ByIndex(index);
                    if (p != null && p.Kind == PlayerKind.Remote && p.Owner == c.conn.Id)
                    {
                        foreach (var kv in new List<KeyValuePair<int, int>>(c.players)) if (kv.Value == index) { c.players.Remove(kv.Key); c.tokens.Remove(kv.Key); }
                        gm.Players.Remove(p);
                    }
                    break;
                }
            }
        }

        static string Trim(string s) { s = (s ?? "").Trim(); return s.Length == 0 ? "Sailor" : s.Length > 16 ? s.Substring(0, 16) : s; }

        void JoinPlayer(Client c, int token, GameManager gm)
        {
            Player p = gm.State == GameState.Playing ? gm.Players.JoinRemote(c.conn.Id, c.nick) : null;
            if (p == null)
            {
                var w = NetIO.Begin(Msg.Refused); w.Write(token); w.Write(gm.State != GameState.Playing ? "The game isn't running." : "The game is full (4 players).");
                c.conn.Send(w.Bytes());
                c.tokens.Remove(token);
                return;
            }
            c.players[token] = p.Index;
            var a = NetIO.Begin(Msg.Assigned); a.Write(token); a.Write(p.Index);
            c.conn.Send(a.Bytes());
        }

        void Kick(Client c, string reason)
        {
            var w = NetIO.Begin(Msg.Bye); w.Write(reason);
            c.conn.SendAndClose(w.Bytes());
        }

        void Drop(Client c, GameManager gm)
        {
            clients.Remove(c);
            if (gm == null) return;
            foreach (var index in c.players.Values)
            {
                var p = gm.Players.ByIndex(index);
                if (p != null && p.Kind == PlayerKind.Remote && p.Owner == c.conn.Id) gm.Players.Remove(p);
            }
            if (c.hello) gm.Banner($"{c.nick.ToUpperInvariant()} LEFT", "They can rejoin from the Join menu.");
        }

        /// <summary>After "try again" reloaded the scene: give every connected player a slot again.</summary>
        public void Rebind(GameManager gm)
        {
            foreach (var c in clients)
            {
                c.sent.Clear();
                c.players.Clear();
                foreach (var token in new List<int>(c.tokens.Keys)) JoinPlayer(c, token, gm);
            }
        }

        // ------------------------------------------------------------------ events
        void AddGlobal(NetEvent e, Action<BinaryWriter> body)
        {
            if (clients.Count == 0) return;
            allEvents.Write((byte)e); body(allEvents); allCount++;
        }

        void AddFor(Player p, NetEvent e, Action<BinaryWriter> body)
        {
            var c = clients.Find(x => x.conn.Id == p.Owner);
            if (c == null) return;
            c.events.Write((byte)e); body(c.events); c.eventCount++;
        }

        public void Banner(string title, string sub) => AddGlobal(NetEvent.Banner, w => { w.Write(title ?? ""); w.Write(sub ?? ""); });
        public void Sound(string name, float volume) => AddGlobal(NetEvent.Sound, w => { w.Write(name); w.Write(volume); });
        public void Fx(FxEvent e) => AddGlobal(NetEvent.Fx, w => w.Write(e));
        public void Toast(Player p, string msg, Color color) => AddFor(p, NetEvent.Toast, w => { w.Write((byte)p.Index); w.Write(msg ?? ""); w.Write(color); });
        public void Shake(Player p, float amount) => AddFor(p, NetEvent.Shake, w => { w.Write((byte)p.Index); w.Write(amount); });
        public void HitMarker(Player p, bool crit) => AddFor(p, NetEvent.Hit, w => { w.Write((byte)p.Index); w.Write(crit); });
        public void Push(Player p, Vector3 delta) => AddFor(p, NetEvent.Push, w => { w.Write((byte)p.Index); w.Write(delta); });
        public void Teleport(Player p, Vector3 at) => AddFor(p, NetEvent.Teleport, w => { w.Write((byte)p.Index); w.Write(at); });

        public void SendGameOver(string reason)
        {
            FlushEvents();
            var w = NetIO.Begin(Msg.GameOver); w.Write(reason ?? "");
            foreach (var c in clients) if (c.hello) c.conn.Send(w.Bytes());
        }

        /// <summary>The host is restarting the run: clients reload too and wait for their slots.</summary>
        public void SendReload()
        {
            FlushEvents();
            var w = NetIO.Begin(Msg.Reload);
            foreach (var c in clients) if (c.hello) c.conn.Send(w.Bytes());
        }

        void FlushEvents()
        {
            var global = ((MemoryStream)allEvents.BaseStream).ToArray();
            foreach (var c in clients)
            {
                if (!c.hello || allCount + c.eventCount == 0) { ResetClientEvents(c); continue; }
                var w = NetIO.Begin(Msg.Events);
                w.Write((ushort)(allCount + c.eventCount));
                w.Write(global);
                w.Write(((MemoryStream)c.events.BaseStream).ToArray());
                c.conn.Send(w.Bytes());
                ResetClientEvents(c);
            }
            allEvents = NewEvents(); allCount = 0;
        }

        static void ResetClientEvents(Client c) { c.events = NewEvents(); c.eventCount = 0; }

        // ------------------------------------------------------------------ snapshots
        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            FlushEvents();
            beaconT -= Time.unscaledDeltaTime;
            if (beaconT <= 0)
            {
                beaconT = 1;
                NetDiscovery.Beacon(Nick, Port, $"Day {gm.Phases.Day} · {gm.Players.Count}/{PlayerManager.MaxPlayers} players");
            }
            snapT -= Time.unscaledDeltaTime;
            if (snapT > 0 || clients.Count == 0) return;
            snapT = NetProtocol.SnapshotInterval;
            SendSnapshots(gm);
        }

        void SendSnapshots(GameManager gm)
        {
            // Shared parts, built once
            var shared = new BinaryWriter(new MemoryStream(1024));
            shared.Write((byte)gm.State); shared.Write(gm.Phases.Index); shared.Write(gm.Phases.Day); shared.Write(gm.Phases.Elapsed);
            shared.Write(gm.Coins); shared.Write(gm.Wall.Hp); shared.Write(gm.Wall.MaxHp);
            shared.Write(gm.Waves.Wave); shared.Write(gm.Waves.WaveCount); shared.Write(gm.EnemiesLeft);
            shared.Write((byte)gm.Players.Count);
            foreach (var p in gm.Players.All) NetPlayerState.From(p).Write(shared);
            var sharedBytes = ((MemoryStream)shared.BaseStream).ToArray();

            var current = new Dictionary<int, byte[]>();
            foreach (var n in NetId.All)
            {
                if (n == null) continue;
                var s = new NetEntityState { id = n.Id, kind = n.Kind, def = n.Def, pos = n.transform.position, rot = n.transform.rotation, rootScale = 1 };
                if (n.Kind == NetKind.Enemy && n.TryGetComponent<Enemy>(out var e)) e.WriteNet(ref s);
                else if (n.Kind == NetKind.GasCan && n.TryGetComponent<GasCan>(out var g)) s.flags = (byte)(g.Leaking ? 1 : 0);
                current[n.Id] = NetIO.Pack(s);
            }

            foreach (var c in clients)
            {
                if (!c.hello) continue;
                var w = NetIO.Begin(Msg.Snapshot);
                w.Write(sharedBytes);
                // This client's own players: what Interact does and any open menu
                var mine = gm.Players.All.FindAll(p => p.Kind == PlayerKind.Remote && p.Owner == c.conn.Id);
                w.Write((byte)mine.Count);
                foreach (var p in mine) { w.Write((byte)p.Index); w.Write(p.PromptText()); w.WriteMenu(p.OpenedMenu); }
                // Things that changed or appeared, then things that are gone
                var changed = new List<byte[]>();
                foreach (var kv in current)
                    if (!c.sent.TryGetValue(kv.Key, out var old) || !NetIO.Same(old, kv.Value)) { changed.Add(kv.Value); c.sent[kv.Key] = kv.Value; }
                var removed = new List<int>();
                foreach (var id in c.sent.Keys) if (!current.ContainsKey(id)) removed.Add(id);
                foreach (var id in removed) c.sent.Remove(id);
                w.Write(changed.Count);
                foreach (var b in changed) w.Write(b);
                w.Write(removed.Count);
                foreach (var id in removed) w.Write(id);
                c.conn.Send(w.Bytes());
            }
        }
    }
}
