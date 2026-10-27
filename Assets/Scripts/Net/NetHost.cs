// TideWall 3D — the host side of online play. Accepts clients (directly or through Unity Relay), checks the
// version and optional room password, gives each joining player a slot (a Remote Player simulated here), runs their
// input commands, and streams snapshots (20 Hz, deltas against what each client confirmed) and events back. The
// host also broadcasts a LAN beacon so the Join screen can list the game.
//
// Nothing a client sends is trusted: messages are size-checked by NetLink, malformed ones drop that client, players
// can only be steered by the connection that owns them, positions come from the host's own simulation, and
// unfinished handshakes time out.
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace TideWall
{
    [DefaultExecutionOrder(-100)]   // inputs land before players update
    public class NetHost : MonoBehaviour
    {
        class Client
        {
            public int peer;
            public string nick = "Sailor";
            public bool hello, welcomed;
            public float connectedAt;
            public byte[] clientNonce, hostNonce;
            public float closeAt = -1;                                  // goodbye sent: disconnect once it's had time to leave
            public readonly Dictionary<int, string> tokens = new();     // join token → device name (kept across "try again")
            public readonly Dictionary<int, int> players = new();       // join token → player index
            // Snapshot deltas: what each recent snapshot contained, and the newest one this client confirmed
            public uint snapSeq, acked;
            public readonly Dictionary<int, byte[]>[] history = new Dictionary<int, byte[]>[64];
            public BinaryWriter notices = NewEvents(), effects = NewEvents();
            public int noticeCount, effectCount;
        }

        public int Port { get; private set; }
        public string Nick { get; private set; }
        /// <summary>Relay games: the code friends type to join.</summary>
        public string JoinCode { get; private set; }
        public bool HasPassword => passwordKey != null;
        public int ClientCount { get { int n = 0; foreach (var c in clients) if (c.welcomed) n++; return n; } }
        public string Addresses => JoinCode != null
            ? $"Join code: {JoinCode}"
            : $"Friends on your network can find you under Join, or connect to {NetDiscovery.LocalAddresses()} (port {Port}).";

        NetLink link;
        readonly List<Client> clients = new();
        byte[] passwordKey, passwordSalt;
        // Direct games: an address that got the password wrong waits before trying again (doubling each time)
        readonly Dictionary<string, (float until, float wait)> lockout = new();
        float snapT, beaconT;
        BinaryWriter allNotices = NewEvents(), allEffects = NewEvents();
        int allNoticeCount, allEffectCount;

        static BinaryWriter NewEvents() => new(new MemoryStream(512));

        /// <summary>Host directly on a UDP port. An empty password lets anyone who can reach the port join.</summary>
        public bool Listen(int port, string nick, string password, out string error)
        {
            Port = port; Nick = string.IsNullOrWhiteSpace(nick) ? "Host" : nick;
            SetPassword(password);
            link = NetLink.Listen((ushort)port, out error);
            return link != null;
        }

        /// <summary>Host through Unity Relay (encrypted, no port forwarding): friends join with the code.</summary>
        public bool ListenRelay(RelayServerData relay, string code, string nick, string password, out string error)
        {
            Nick = string.IsNullOrWhiteSpace(nick) ? "Host" : nick;
            JoinCode = code;
            SetPassword(password);
            link = NetLink.ListenRelay(relay, out error);
            return link != null;
        }

        void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return;
            passwordSalt = NetAuth.Nonce();
            passwordKey = NetAuth.Key(password, passwordSalt);
        }

        public void Stop(string reason)
        {
            if (link == null) return;
            var bye = NetIO.Begin(Msg.Bye); bye.Write(reason ?? "");
            foreach (var c in clients) link.Send(c.peer, Channel.Reliable, bye.Bytes());
            link.Update();   // let the goodbyes leave
            link.Dispose();
            link = null;
            clients.Clear();
        }

        void OnDestroy() => Stop("The host closed the game.");

        // ------------------------------------------------------------------ receiving
        void Update()
        {
            if (link == null) return;
            var gm = GameManager.I;
            link.Update();
            while (link.Inbox.Count > 0)
            {
                var e = link.Inbox.Dequeue();
                switch (e.Kind)
                {
                    case NetLink.EventKind.Connected:
                        if (clients.Count >= NetProtocol.MaxConnections || LockedOut(e.Peer)) { link.Disconnect(e.Peer); break; }
                        clients.Add(new Client { peer = e.Peer, connectedAt = Time.unscaledTime });
                        Debug.Log($"TideWall net: client {e.Peer} connected from {link.Address(e.Peer)}");
                        break;
                    case NetLink.EventKind.Disconnected:
                    {
                        var c = clients.Find(x => x.peer == e.Peer);
                        if (c != null) Drop(c, gm);
                        break;
                    }
                    case NetLink.EventKind.Data:
                    {
                        var c = clients.Find(x => x.peer == e.Peer);
                        if (c == null || c.closeAt >= 0) break;
                        // A malformed message from one client drops that client, not the host's frame
                        try { Handle(c, e.Data, e.Channel, gm); }
                        catch (Exception ex) { Debug.LogWarning($"TideWall: dropping client {c.peer} after a bad message ({ex.GetType().Name})"); link.Disconnect(c.peer); }
                        break;
                    }
                }
            }

            float now = Time.unscaledTime;
            foreach (var c in clients.ToArray())
            {
                if (c.closeAt >= 0) { if (now >= c.closeAt) link.Disconnect(c.peer); }
                else if (!c.welcomed && now - c.connectedAt > NetProtocol.HelloTimeout) Kick(c, "Took too long to say hello.");
            }

            // Online players' movement: simulate their new commands now, before the players update
            if (gm != null)
                foreach (var p in gm.Players.All.ToArray())
                    if (p.Kind == PlayerKind.Remote) p.RunRemoteCommands(Time.deltaTime);
        }

        void Handle(Client c, byte[] data, Channel channel, GameManager gm)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            var type = (Msg)r.ReadByte();
            // Input is the only message that travels unreliably; anything else on that channel is ignored
            if ((channel == Channel.Unreliable) != (type == Msg.Input)) return;
            switch (type)
            {
                case Msg.Hello:
                {
                    if (c.hello) return;
                    int version = r.ReadInt32();
                    c.nick = Trim(r.ReadText(64));
                    c.clientNonce = r.ReadBlob(16);
                    c.hello = true;
                    if (version != NetProtocol.Version) { Kick(c, "Your TideWall is a different version from the host's."); return; }
                    if (passwordKey == null) { Welcome(c); return; }
                    c.hostNonce = NetAuth.Nonce();
                    var w = NetIO.Begin(Msg.Challenge); w.Write(passwordSalt); w.Write(c.hostNonce);
                    link.Send(c.peer, Channel.Reliable, w.Bytes());
                    break;
                }
                case Msg.Auth:
                {
                    if (!c.hello || c.welcomed || c.hostNonce == null) return;
                    var proof = r.ReadBlob(32);
                    var expected = NetAuth.Proof(passwordKey, c.hostNonce, c.clientNonce);
                    c.hostNonce = null;   // one try per connection
                    if (NetAuth.Same(proof, expected)) Welcome(c);
                    else { LockOut(c.peer); Kick(c, "Wrong password."); }
                    break;
                }
                case Msg.Join:
                {
                    if (!c.welcomed || gm == null) return;
                    int token = r.ReadInt32();
                    string device = r.ReadText(64);
                    if (!c.tokens.ContainsKey(token) && c.tokens.Count >= NetProtocol.MaxLocalPlayers) return;
                    c.tokens[token] = device;
                    JoinPlayer(c, token, gm);
                    break;
                }
                case Msg.Input:
                {
                    if (!c.welcomed || gm == null) return;
                    uint ack = r.ReadUInt32();
                    if (ack <= c.snapSeq && ack > c.acked) c.acked = ack;
                    int n = r.ReadByte();
                    if (n > NetProtocol.MaxLocalPlayers) throw new InvalidDataException("too many players");
                    for (int i = 0; i < n; i++)
                    {
                        int index = r.ReadByte();
                        var aim = r.ReadAim();
                        int count = r.ReadByte();
                        if (count > NetProtocol.MaxCommandsPerPacket) throw new InvalidDataException("too many commands");
                        var p = gm.Players.ByIndex(index);
                        bool ours = p != null && p.Kind == PlayerKind.Remote && p.Owner == c.peer;
                        if (ours) p.Remote.ReceiveAim(aim);
                        for (int k = 0; k < count; k++)
                        {
                            var cmd = NetCommand.Read(r);
                            if (ours) p.Remote.Receive(cmd);
                        }
                    }
                    break;
                }
                case Msg.Leave:
                {
                    if (!c.welcomed) return;
                    int index = r.ReadByte();
                    var p = gm?.Players.ByIndex(index);
                    if (p != null && p.Kind == PlayerKind.Remote && p.Owner == c.peer)
                    {
                        foreach (var kv in new List<KeyValuePair<int, int>>(c.players)) if (kv.Value == index) { c.players.Remove(kv.Key); c.tokens.Remove(kv.Key); }
                        gm.Players.Remove(p);
                    }
                    break;
                }
            }
        }

        /// <summary>The address without the port (relay games all come from the relay, so they're never locked out).</summary>
        string Host(int peer)
        {
            if (JoinCode != null) return null;
            var a = link.Address(peer);
            int colon = a.LastIndexOf(':');
            return colon > 0 ? a.Substring(0, colon) : a;
        }

        bool LockedOut(int peer)
        {
            var h = Host(peer);
            return h != null && lockout.TryGetValue(h, out var l) && Time.unscaledTime < l.until;
        }

        void LockOut(int peer)
        {
            var h = Host(peer);
            if (h == null) return;
            float wait = lockout.TryGetValue(h, out var l) ? Mathf.Min(l.wait * 2, 60) : 2;
            lockout[h] = (Time.unscaledTime + wait, wait);
        }

        void Welcome(Client c)
        {
            c.welcomed = true;
            var w = NetIO.Begin(Msg.Welcome); w.Write(c.peer); w.Write(Nick);
            link.Send(c.peer, Channel.Reliable, w.Bytes());
        }

        /// <summary>A client's chosen name: no rich-text tags (it's shown in everyone's HUD and banners), 16 characters at most.</summary>
        static string Trim(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s ?? "") if (ch != '<' && ch != '>' && !char.IsControl(ch)) sb.Append(ch);
            s = sb.ToString().Trim();
            return s.Length == 0 ? "Sailor" : s.Length > 16 ? s.Substring(0, 16) : s;
        }

        void JoinPlayer(Client c, int token, GameManager gm)
        {
            if (c.players.ContainsKey(token)) return;
            Player p = gm.State == GameState.Playing ? gm.Players.JoinRemote(c.peer, c.nick) : null;
            if (p == null)
            {
                var w = NetIO.Begin(Msg.Refused); w.Write(token); w.Write(gm.State != GameState.Playing ? "The game isn't running." : "The game is full (4 players).");
                link.Send(c.peer, Channel.Reliable, w.Bytes());
                c.tokens.Remove(token);
                return;
            }
            c.players[token] = p.Index;
            var a = NetIO.Begin(Msg.Assigned); a.Write(token); a.Write(p.Index);
            link.Send(c.peer, Channel.Reliable, a.Bytes());
        }

        /// <summary>Tell a client why, then disconnect it a moment later (so the reason arrives).</summary>
        void Kick(Client c, string reason)
        {
            var w = NetIO.Begin(Msg.Bye); w.Write(reason);
            link.Send(c.peer, Channel.Reliable, w.Bytes());
            c.closeAt = Time.unscaledTime + .5f;
        }

        void Drop(Client c, GameManager gm)
        {
            clients.Remove(c);
            if (gm == null) return;
            foreach (var index in c.players.Values)
            {
                var p = gm.Players.ByIndex(index);
                if (p != null && p.Kind == PlayerKind.Remote && p.Owner == c.peer) gm.Players.Remove(p);
            }
            if (c.welcomed) gm.Banner($"{c.nick.ToUpperInvariant()} LEFT", "They can rejoin from the Join menu.");
        }

        /// <summary>After "try again" reloaded the scene: give every connected player a slot again.</summary>
        public void Rebind(GameManager gm)
        {
            foreach (var c in clients)
            {
                Array.Clear(c.history, 0, c.history.Length);
                c.acked = 0;
                c.players.Clear();
                foreach (var token in new List<int>(c.tokens.Keys)) JoinPlayer(c, token, gm);
            }
        }

        // ------------------------------------------------------------------ events
        static void Add(BinaryWriter w, NetEvent e, Action<BinaryWriter> body) { w.Write((byte)e); body(w); }

        void Global(bool notice, NetEvent e, Action<BinaryWriter> body)
        {
            if (clients.Count == 0) return;
            if (notice) { Add(allNotices, e, body); allNoticeCount++; }
            else { Add(allEffects, e, body); allEffectCount++; }
        }

        void For(Player p, bool notice, NetEvent e, Action<BinaryWriter> body)
        {
            var c = clients.Find(x => x.peer == p.Owner);
            if (c == null) return;
            if (notice) { Add(c.notices, e, body); c.noticeCount++; }
            else { Add(c.effects, e, body); c.effectCount++; }
        }

        public void Banner(string title, string sub) => Global(true, NetEvent.Banner, w => { w.Write(title ?? ""); w.Write(sub ?? ""); });
        public void Sound(string name, float volume) => Global(false, NetEvent.Sound, w => { w.Write(name); w.Write(volume); });
        public void Fx(FxEvent e) => Global(false, NetEvent.Fx, w => w.Write(e));
        public void Toast(Player p, string msg, Color color) => For(p, true, NetEvent.Toast, w => { w.Write((byte)p.Index); w.Write(msg ?? ""); w.Write(color); });
        public void Shake(Player p, float amount) => For(p, false, NetEvent.Shake, w => { w.Write((byte)p.Index); w.Write(amount); });
        public void HitMarker(Player p, bool crit) => For(p, false, NetEvent.Hit, w => { w.Write((byte)p.Index); w.Write(crit); });

        public void SendGameOver(string reason)
        {
            FlushEvents();
            var w = NetIO.Begin(Msg.GameOver); w.Write(reason ?? "");
            foreach (var c in clients) if (c.welcomed) link.Send(c.peer, Channel.Reliable, w.Bytes());
        }

        /// <summary>The host is restarting the run: clients reload too and wait for their slots.</summary>
        public void SendReload()
        {
            FlushEvents();
            var w = NetIO.Begin(Msg.Reload);
            foreach (var c in clients) if (c.welcomed) link.Send(c.peer, Channel.Reliable, w.Bytes());
        }

        void FlushEvents()
        {
            if (link == null) return;
            var notices = ((MemoryStream)allNotices.BaseStream).ToArray();
            var effects = ((MemoryStream)allEffects.BaseStream).ToArray();
            foreach (var c in clients)
            {
                if (c.welcomed)
                {
                    SendBatch(c, Msg.Notices, Channel.Reliable, allNoticeCount + c.noticeCount, notices, c.notices);
                    SendBatch(c, Msg.Effects, Channel.Unreliable, allEffectCount + c.effectCount, effects, c.effects);
                }
                c.notices = NewEvents(); c.effects = NewEvents(); c.noticeCount = c.effectCount = 0;
            }
            allNotices = NewEvents(); allEffects = NewEvents(); allNoticeCount = allEffectCount = 0;
        }

        void SendBatch(Client c, Msg type, Channel channel, int count, byte[] shared, BinaryWriter own)
        {
            if (count == 0) return;
            var w = NetIO.Begin(type);
            w.Write((ushort)count);
            w.Write(shared);
            w.Write(((MemoryStream)own.BaseStream).ToArray());
            link.Send(c.peer, channel, w.Bytes());
        }

        // ------------------------------------------------------------------ snapshots
        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || link == null) return;
            FlushEvents();
            beaconT -= Time.unscaledDeltaTime;
            if (beaconT <= 0)
            {
                beaconT = 1;
                NetDiscovery.Beacon(Nick, Port, JoinCode ?? "", $"Day {gm.Phases.Day} · {gm.Players.Count}/{PlayerManager.MaxPlayers} players{(HasPassword ? " · password" : "")}");
            }
            snapT -= Time.unscaledDeltaTime;
            if (snapT <= 0 && clients.Count > 0)
            {
                snapT = NetProtocol.SnapshotInterval;
                SendSnapshots(gm);
            }
            link.Update();   // send what this frame produced right away
        }

        void SendSnapshots(GameManager gm)
        {
            // Shared parts, built once
            var shared = new BinaryWriter(new MemoryStream(1024));
            shared.Write((byte)gm.State); shared.Write(gm.Phases.Index); shared.Write(gm.Phases.Day); shared.Write(gm.Phases.Elapsed);
            shared.Write(gm.SandDollars); shared.Write(gm.Pearls);
            shared.Write((byte)gm.Wall.Sections.Count);
            foreach (var sec in gm.Wall.Sections) { shared.Write(sec.hp); shared.Write(sec.max); }
            shared.Write(gm.Plant.Hp); shared.Write(gm.Plant.MaxHp);
            // Tidal cards: the active ones, and any vote in progress
            shared.Write((byte)gm.Cards.Actives.Count);
            foreach (var a in gm.Cards.Actives) { shared.Write(a.def.id); shared.Write((byte)a.wavesLeft); }
            var draft = gm.Cards.Draft;
            shared.Write((byte)(draft?.Count ?? 0));
            if (draft != null)
            {
                foreach (var card in draft) shared.Write(card.id);
                shared.Write(gm.Cards.DraftLeft);
                shared.Write(gm.Cards.RerollOffered);
                shared.Write((byte)gm.Cards.Votes.Count);
                foreach (var kv in gm.Cards.Votes) { shared.Write((byte)kv.Key); shared.Write((byte)kv.Value); }
            }
            shared.Write(gm.Waves.Wave); shared.Write(gm.Waves.WaveCount); shared.Write(gm.EnemiesLeft); shared.Write(gm.Waves.RunWave); shared.Write(gm.Waves.Preview);
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
                else if (n.Kind == NetKind.Structure && n.TryGetComponent<Structure>(out var st)) { s.value = Mathf.CeilToInt(st.Hp); s.rootRot = st.HeadRotation; }
                current[n.Id] = NetIO.Pack(s);
            }

            foreach (var c in clients)
            {
                if (!c.welcomed) continue;
                uint seq = ++c.snapSeq;
                // Delta against the newest snapshot this client confirmed; a full one if that's too old (and every 2 s anyway)
                Dictionary<int, byte[]> baseline = null;
                if (c.acked > 0 && seq - c.acked < c.history.Length && seq % 40 != 0) baseline = c.history[c.acked % c.history.Length];
                c.history[seq % c.history.Length] = current;

                var w = NetIO.Begin(Msg.Snapshot);
                w.Write(seq);
                w.Write(sharedBytes);
                // This client's own players: what Interact does, any open menu, and their movement as simulated here
                var mine = gm.Players.All.FindAll(p => p.Kind == PlayerKind.Remote && p.Owner == c.peer);
                w.Write((byte)mine.Count);
                foreach (var p in mine)
                {
                    w.Write((byte)p.Index); w.Write(p.PromptText()); w.WriteMenu(p.OpenedMenu); w.Write(p.Building?.id ?? ""); w.Write(p.BuildYaw);
                    p.WriteMoveState(w);
                }
                // Things that changed since the baseline…
                int changed = 0;
                var body = new BinaryWriter(new MemoryStream(1024));
                foreach (var kv in current)
                    if (baseline == null || !baseline.TryGetValue(kv.Key, out var old) || !NetIO.Same(old, kv.Value)) { body.Write(kv.Value); changed++; }
                w.Write(changed);
                w.Write(((MemoryStream)body.BaseStream).ToArray());
                // …and everything that exists (the client removes whatever isn't listed)
                w.Write(current.Count);
                foreach (var id in current.Keys) w.Write(id);
                link.Send(c.peer, Channel.Unreliable, w.Bytes());
            }
        }
    }
}
