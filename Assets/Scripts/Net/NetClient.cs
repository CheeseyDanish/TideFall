// TideWall 3D — the client side of online play. Connects to a host (by address or Relay join code), answers the
// password challenge if there is one, asks for a player slot for each local player who joins, sends their input
// commands, and turns the host's snapshots and events into the world on this screen: mirrored players (Player kinds
// LocalProxy / Proxy), enemy proxies, corpses, gas cans, food, loot, defences and breakers.
//
// Our own players move straight away (prediction) and are corrected against the host's simulation whenever a
// snapshot confirms how far it got (Player.Reconcile).
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    [DefaultExecutionOrder(-100)]   // snapshots apply before players and enemies update
    public class NetClient : MonoBehaviour
    {
        public enum State { Connecting, Connected, Failed, Closed }
        public State Status { get; private set; } = State.Connecting;
        public string Error { get; private set; }
        public bool Connected => Status == State.Connected;
        public int ClientId { get; private set; } = -1;
        public string HostNick { get; private set; } = "";
        public string Address { get; private set; }

        NetLink link;
        int hostPeer = -1;
        string nick, password;
        byte[] nonce;
        int nextToken = 1;
        uint lastSnapshot;
        float sendT, connectT;
        readonly Dictionary<int, InputDevice[]> tokens = new();     // join token → this machine's devices
        readonly Dictionary<int, InputDevice[]> owned = new();      // player index → devices
        readonly Dictionary<int, GameObject> entities = new();
        readonly HashSet<int> alive = new();
        InputDevice[] first;
        bool reloading;

        /// <summary>Connect by IP address (or name) and port.</summary>
        public void Connect(string address, int port, string nickname, string roomPassword, InputDevice[] firstPlayer)
        {
            Address = $"{address}:{port}";
            Begin(nickname, roomPassword, firstPlayer);
            link = NetLink.Connect(address, (ushort)port, out var error);
            if (link == null) Fail(error);
        }

        /// <summary>Connect through Unity Relay (the allocation was joined with the code already).</summary>
        public void ConnectRelay(RelayServerData relay, string code, string nickname, string roomPassword, InputDevice[] firstPlayer)
        {
            Address = $"code {code}";
            Begin(nickname, roomPassword, firstPlayer);
            link = NetLink.ConnectRelay(relay);
        }

        void Begin(string nickname, string roomPassword, InputDevice[] firstPlayer)
        {
            nick = nickname; password = roomPassword; first = firstPlayer;
            nonce = NetAuth.Nonce();
            connectT = Time.unscaledTime;
        }

        /// <summary>Failed before it started (bad address, relay error).</summary>
        public void Fail(string why) { Error = why; Status = State.Failed; }

        public void Stop()
        {
            link?.Dispose();
            link = null;
            if (Status != State.Failed) Status = State.Closed;
        }

        void OnDestroy() => Stop();

        void Send(Channel ch, BinaryWriter w) { if (link != null && hostPeer >= 0) link.Send(hostPeer, ch, w.Bytes()); }

        /// <summary>Ask the host for a player slot for these devices (a local player pressed a button).</summary>
        public void RequestJoin(InputDevice[] devices)
        {
            if (!Connected) return;
            int token = nextToken++;
            tokens[token] = devices;
            var name = devices.Length > 0 && (devices[0] is Keyboard || devices[0] is Mouse) ? "Keyboard & mouse" : devices[0].displayName ?? devices[0].name;
            var w = NetIO.Begin(Msg.Join); w.Write(token); w.Write(name ?? "Controller");
            Send(Channel.Reliable, w);
        }

        // ------------------------------------------------------------------ receiving
        void Update()
        {
            if (link == null) return;
            link.Update();
            reloading = false;
            while (!reloading && link != null && link.Inbox.Count > 0)
            {
                var e = link.Inbox.Dequeue();
                switch (e.Kind)
                {
                    case NetLink.EventKind.Connected:
                    {
                        Debug.Log($"TideWall net: connected to {Address}");
                        hostPeer = e.Peer;
                        var hello = NetIO.Begin(Msg.Hello); hello.Write(NetProtocol.Version); hello.Write(nick ?? "Sailor"); hello.Write(nonce);
                        Send(Channel.Reliable, hello);
                        break;
                    }
                    case NetLink.EventKind.Disconnected:
                        Debug.Log($"TideWall net: disconnected from {Address} ({Status})");
                        if (Status == State.Connecting) Fail(Error ?? $"No answer from {Address}.");
                        else if (Status == State.Connected) Lost(Error ?? "Lost the connection to the host.");
                        break;
                    case NetLink.EventKind.Data:
                        // A message we can't read means we and the host disagree: leave rather than guess
                        try { Handle(e.Data, e.Channel); }
                        catch (Exception ex) { Debug.LogWarning($"TideWall: bad message from the host ({ex.GetType().Name})"); Lost("The host sent something this game couldn't read."); }
                        break;
                }
            }
            if (Status == State.Connecting && Time.unscaledTime - connectT > 12) Fail($"No answer from {Address}.");
        }

        void Lost(string why)
        {
            bool wasPlaying = Status == State.Connected;
            Status = State.Failed;
            Error = why;
            if (wasPlaying)
            {
                MainMenu.Notice = why;
                Net.Shutdown(null);
                GameManager.Reload();
                reloading = true;
            }
        }

        void Handle(byte[] data, Channel channel)
        {
            var gm = GameManager.I;
            using var r = new BinaryReader(new MemoryStream(data));
            var type = (Msg)r.ReadByte();
            switch (type)
            {
                case Msg.Challenge:
                {
                    var salt = r.ReadBlob(16); var hostNonce = r.ReadBlob(16);
                    if (string.IsNullOrEmpty(password)) { Fail("This game needs a password."); Stop(); return; }
                    var w = NetIO.Begin(Msg.Auth); w.Write(NetAuth.Proof(NetAuth.Key(password, salt), hostNonce, nonce));
                    Send(Channel.Reliable, w);
                    break;
                }
                case Msg.Welcome:
                    ClientId = r.ReadInt32(); HostNick = r.ReadText(64);
                    Status = State.Connected;
                    gm?.BeginClient();
                    if (first != null) { gm?.Players.PendingJoins.Add(first); RequestJoin(first); first = null; }
                    break;
                case Msg.Assigned:
                {
                    int token = r.ReadInt32(), index = r.ReadInt32();
                    if (tokens.TryGetValue(token, out var devs))
                    {
                        owned[index] = devs;
                        var p = gm?.Players.ByIndex(index);
                        if (p != null && p.Kind != PlayerKind.LocalProxy) gm.Players.Remove(p);   // recreated as ours on the next snapshot
                    }
                    break;
                }
                case Msg.Refused:
                {
                    int token = r.ReadInt32(); string why = r.ReadText(200);
                    tokens.Remove(token);
                    gm?.Players.JoinRefused();
                    if (gm != null) foreach (var p in gm.Players.All) if (p.HasView) p.Toast(why, new Color(1, .6f, .6f));
                    MainMenu.Notice = why;
                    break;
                }
                case Msg.Snapshot:
                {
                    uint seq = r.ReadUInt32();
                    if (seq <= lastSnapshot || gm == null) return;   // the unreliable channel never delivers older ones, but be sure
                    lastSnapshot = seq;
                    ApplySnapshot(r, gm);
                    break;
                }
                case Msg.Notices:
                case Msg.Effects: if (gm != null) ApplyEvents(r, gm); break;
                case Msg.GameOver: gm?.NetGameOver(r.ReadText(200)); break;
                case Msg.Reload:
                    // The host is trying again: start a fresh mirror; our slots are re-assigned after the reload.
                    owned.Clear(); entities.Clear(); lastSnapshot = 0;
                    reloading = true;
                    GameManager.Reload();
                    break;
                case Msg.Bye:
                {
                    string why = r.ReadText(200);
                    bool wasPlaying = Status == State.Connected;
                    Error = string.IsNullOrEmpty(why) ? "The host closed the game." : why;
                    Status = State.Failed;
                    if (wasPlaying)
                    {
                        MainMenu.Notice = Error;
                        Net.Shutdown(null);
                        GameManager.Reload();
                    }
                    reloading = true;
                    break;
                }
            }
        }

        void ApplySnapshot(BinaryReader r, GameManager gm)
        {
            var state = (GameState)r.ReadByte();
            int phase = r.ReadInt32(), day = r.ReadInt32(); float elapsed = r.ReadSingle();
            int sandDollars = r.ReadInt32(), pearls = r.ReadInt32();
            int sections = r.ReadByte();
            var hps = new List<float>(sections); var maxes = new List<float>(sections);
            for (int i = 0; i < sections; i++) { hps.Add(r.ReadSingle()); maxes.Add(r.ReadSingle()); }
            float plantHp = r.ReadSingle(), plantMax = r.ReadSingle();
            int activeCount = r.ReadByte();
            var actives = new List<(string, int)>();
            for (int i = 0; i < activeCount; i++) actives.Add((r.ReadString(), r.ReadByte()));
            int draftCount = r.ReadByte();
            List<string> draft = null; float draftLeft = 0; bool reroll = false; var votes = new Dictionary<int, int>();
            if (draftCount > 0)
            {
                draft = new List<string>();
                for (int i = 0; i < draftCount; i++) draft.Add(r.ReadString());
                draftLeft = r.ReadSingle();
                reroll = r.ReadBoolean();
                int vc = r.ReadByte();
                for (int i = 0; i < vc; i++) votes[r.ReadByte()] = r.ReadByte();
            }
            int wave = r.ReadInt32(), waveCount = r.ReadInt32(), left = r.ReadInt32(), runWave = r.ReadInt32(); string preview = r.ReadString();
            if (gm.State == GameState.MainMenu) gm.BeginClient();
            if (phase >= 0) gm.Phases.NetFollow(phase, day, elapsed);
            gm.ApplyNet(sandDollars, pearls, left);
            gm.Wall.Restore(hps, maxes);
            gm.Plant.Restore(plantHp, plantMax);
            gm.Cards.ApplyNet(actives, draft, draftLeft, votes, reroll);
            gm.Waves.ApplyNet(wave, waveCount, preview); gm.Waves.RunWave = runWave;
            if (state == GameState.GameOver && gm.State == GameState.Playing) gm.NetGameOver("The wall has fallen.");

            // Players
            int n = r.ReadByte();
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                var s = NetPlayerState.Read(r);
                seen.Add(s.index);
                bool ours = s.owner == ClientId && owned.ContainsKey(s.index);
                var p = gm.Players.ByIndex(s.index);
                if (p != null && (p.Kind == PlayerKind.LocalProxy) != ours) { gm.Players.Remove(p); p = null; }
                if (p == null) p = gm.Players.AddProxy(s.index, ours ? owned[s.index] : null, s.nick, s.pos);
                p.ApplyNet(s);
            }
            foreach (var p in gm.Players.All.ToArray()) if (!seen.Contains(p.Index)) { owned.Remove(p.Index); gm.Players.Remove(p); }

            // Our players: prompts, menus, and where the host has them (we reconcile our prediction against it)
            int mine = r.ReadByte();
            for (int i = 0; i < mine; i++)
            {
                int index = r.ReadByte(); string prompt = r.ReadString(); var menu = r.ReadMenu(); string building = r.ReadString(); float buildYaw = r.ReadSingle();
                var move = Player.ReadMoveState(r);
                var p = gm.Players.ByIndex(index);
                if (p == null) continue;
                p.ApplyNetMenu(menu, prompt, building, buildYaw);
                if (p.Kind == PlayerKind.LocalProxy) p.Reconcile(move);
            }

            // Replicated things: what changed, then the full list of what exists
            int changed = r.ReadInt32();
            for (int i = 0; i < changed; i++) ApplyEntity(NetEntityState.Read(r), gm);
            int count = r.ReadInt32();
            alive.Clear();
            for (int i = 0; i < count; i++) alive.Add(r.ReadInt32());
            var gone = new List<int>();
            foreach (var id in entities.Keys) if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in gone) { if (entities[id]) Destroy(entities[id]); entities.Remove(id); }
        }

        void ApplyEntity(in NetEntityState s, GameManager gm)
        {
            entities.TryGetValue(s.id, out var go);
            if (go == null)
            {
                switch (s.kind)
                {
                    case NetKind.Enemy:
                        if (s.def < 0 || s.def >= gm.DB.enemies.Count) return;
                        go = Enemy.SpawnProxy(gm.DB.enemies[s.def], s.id, s.pos, s.rot.eulerAngles.y).gameObject;
                        break;
                    case NetKind.GasCan: go = GasCan.SpawnProxy(s.id, s.pos, s.rot).gameObject; break;
                    case NetKind.Food:
                        if (s.def < 0 || s.def >= gm.DB.foods.Count) return;
                        go = FoodPickup.Spawn(gm.DB.foods[s.def], s.pos, s.id).gameObject;
                        break;
                    case NetKind.Breaker: go = Breaker.SpawnProxy(s.id, s.pos.x, s.def, s.pos.z).gameObject; break;
                    case NetKind.Structure:
                        if (s.def < 0 || s.def >= gm.DB.structures.Count) return;
                        go = Structure.SpawnProxy(gm.DB.structures[s.def], s.id, s.pos, s.rot).gameObject;
                        break;
                    case NetKind.SandDollar: go = SandDollarPickup.SpawnProxy(s.id, s.def, s.pos).gameObject; break;
                    default: return;
                }
                entities[s.id] = go;
            }
            switch (s.kind)
            {
                case NetKind.Enemy:
                    var e = go.GetComponent<Enemy>();
                    e.ApplyNet(s);
                    if (e.Dead) e.ApplyCorpsePose(s);
                    break;
                case NetKind.GasCan:
                    go.GetComponent<NetFollow>()?.Set(s.pos, s.rot);
                    go.GetComponent<GasCan>().Leaking = (s.flags & 1) != 0;
                    break;
                case NetKind.Food:
                    go.transform.position = new Vector3(s.pos.x, go.transform.position.y, s.pos.z);
                    break;
                case NetKind.Structure:
                    go.GetComponent<Structure>().ApplyNet(s.value, s.rootRot);
                    break;
                case NetKind.Breaker:
                case NetKind.SandDollar:
                    go.GetComponent<NetFollow>()?.Set(s.pos, s.rot);
                    break;
            }
        }

        void ApplyEvents(BinaryReader r, GameManager gm)
        {
            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                var e = (NetEvent)r.ReadByte();
                switch (e)
                {
                    case NetEvent.Banner: gm.Banner(r.ReadString(), r.ReadString()); break;
                    case NetEvent.Sound: { var name = r.ReadString(); Sfx.Play(name, r.ReadSingle()); break; }
                    case NetEvent.Fx: Fx.Play(r.ReadFx()); break;
                    case NetEvent.Toast: { var p = gm.Players.ByIndex(r.ReadByte()); var msg = r.ReadString(); var c = r.ReadColor(); p?.Toast(msg, c); break; }
                    case NetEvent.Shake: { var p = gm.Players.ByIndex(r.ReadByte()); float a = r.ReadSingle(); p?.Shake(a); break; }
                    case NetEvent.Hit: { var p = gm.Players.ByIndex(r.ReadByte()); bool crit = r.ReadBoolean(); if (p?.Hud != null) p.Hud.HitMarker(crit); break; }
                    default: return;   // unknown event: the rest of this batch can't be read
                }
            }
        }

        // ------------------------------------------------------------------ sending
        void LateUpdate()
        {
            var gm = GameManager.I;
            if (!Connected || gm == null || link == null) return;
            sendT -= Time.unscaledDeltaTime;
            if (sendT > 0) return;
            sendT = NetProtocol.InputInterval;
            var mine = gm.Players.All.FindAll(p => p.Kind == PlayerKind.LocalProxy);
            if (mine.Count == 0) return;
            // Every command the host hasn't confirmed yet (up to a limit): a lost packet is covered by the next one
            var w = NetIO.Begin(Msg.Input);
            w.Write(lastSnapshot);
            w.Write((byte)mine.Count);
            foreach (var p in mine) p.WriteCommands(w, NetProtocol.MaxCommandsPerPacket);
            Send(Channel.Unreliable, w);
            link.Update();
        }

        /// <summary>A local player left (their controller was unplugged for good, or they quit).</summary>
        public void Leave(Player p)
        {
            var w = NetIO.Begin(Msg.Leave); w.Write((byte)p.Index);
            Send(Channel.Reliable, w);
        }
    }
}
