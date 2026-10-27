// TideWall 3D — the network connection itself: Unity Transport (UDP) with two channels.
//   Reliable    resent until delivered, in order (handshake, joins, banners, game over…)
//   Unreliable  latest-wins, older packets dropped (snapshots, input, cosmetic effects): a lost packet never holds
//               anything else up, which is why action games use UDP rather than TCP.
// Both channels fragment big messages (up to MaxPayload). A host can listen directly on a port (LAN / port
// forwarding) or through Unity Relay (join codes, encrypted with DTLS, no port forwarding).
//
// Everything runs on the main thread: Update() pumps the driver and fills Inbox. Defensive limits live here too:
// oversized messages are refused, a peer that floods messages is dropped, and a peer that stops reading is dropped
// once its reliable backlog passes MaxBacklog instead of growing memory without limit.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

namespace TideWall
{
    public enum Channel : byte { Reliable, Unreliable }

    public sealed class NetLink : IDisposable
    {
        public enum EventKind { Connected, Data, Disconnected }

        public struct Received
        {
            public int Peer;
            public EventKind Kind;
            public Channel Channel;
            public byte[] Data;
        }

        /// <summary>Largest message either side may send (a full snapshot fits easily).</summary>
        public const int MaxPayload = 64 * 1024;
        /// <summary>Reliable messages waiting for room in the send window before the peer is considered stalled.</summary>
        public const int MaxBacklog = 512;
        /// <summary>Messages per second a peer may send before it's treated as a flood.</summary>
        public const int MaxMessagesPerSecond = 400;

        public readonly Queue<Received> Inbox = new();
        public bool IsServer { get; }
        public int PeerCount => peers.Count;

        NetworkDriver driver;
        NetworkPipeline reliable, unreliable;
        readonly Dictionary<int, NetworkConnection> peers = new();
        readonly Dictionary<NetworkConnection, int> ids = new();
        readonly Dictionary<int, Queue<byte[]>> backlog = new();
        readonly Dictionary<int, int> received = new();
        float rateWindow;
        int nextId = 1;
        bool disposed;

        NetLink(bool server, RelayServerData? relay)
        {
            IsServer = server;
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 10, disconnectTimeoutMS: 10000, heartbeatTimeoutMS: 500);
            settings.WithFragmentationStageParameters(payloadCapacity: MaxPayload);
            settings.WithReliableStageParameters(windowSize: 64);
            if (relay.HasValue) { var r = relay.Value; settings.WithRelayParameters(ref r); }
            driver = NetworkDriver.Create(settings);
            settings.Dispose();
            reliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            unreliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(UnreliableSequencedPipelineStage));
        }

        // ------------------------------------------------------------------ opening
        /// <summary>Host directly on a UDP port (LAN, or the internet with the port forwarded).</summary>
        public static NetLink Listen(ushort port, out string error)
        {
            var link = new NetLink(true, null);
            if (link.driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0 || link.driver.Listen() != 0)
            {
                link.Dispose();
                error = $"Couldn't host on port {port}: is another game using it?";
                return null;
            }
            error = null;
            return link;
        }

        /// <summary>Host through Unity Relay: friends connect with the join code, traffic is encrypted (DTLS).</summary>
        public static NetLink ListenRelay(RelayServerData relay, out string error)
        {
            var link = new NetLink(true, relay);
            if (link.driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || link.driver.Listen() != 0)
            {
                link.Dispose();
                error = "Couldn't open the relay connection.";
                return null;
            }
            error = null;
            return link;
        }

        /// <summary>Connect to a host by IP address or name and port.</summary>
        public static NetLink Connect(string address, ushort port, out string error)
        {
            error = null;
            if (!NetworkEndpoint.TryParse(address, port, out var endpoint))
            {
                // A host name: look up its IPv4 address
                try
                {
                    foreach (var a in Dns.GetHostAddresses(address))
                        if (a.AddressFamily == AddressFamily.InterNetwork && NetworkEndpoint.TryParse(a.ToString(), port, out endpoint)) break;
                }
                catch (Exception) { }
                if (endpoint == default) { error = $"Couldn't find {address}."; return null; }
            }
            var link = new NetLink(false, null);
            link.Track(link.driver.Connect(endpoint));
            return link;
        }

        /// <summary>Connect to a host through Unity Relay (after joining its allocation with the code).</summary>
        public static NetLink ConnectRelay(RelayServerData relay)
        {
            var link = new NetLink(false, relay);
            link.driver.Bind(NetworkEndpoint.AnyIpv4);
            link.Track(link.driver.Connect(relay.Endpoint));
            return link;
        }

        int Track(NetworkConnection c)
        {
            int id = nextId++;
            peers[id] = c; ids[c] = id;
            return id;
        }

        // ------------------------------------------------------------------ pumping
        /// <summary>Main thread, once a frame: send and receive, then read Inbox.</summary>
        public void Update()
        {
            if (disposed) return;
            driver.ScheduleUpdate().Complete();

            if (IsServer)
            {
                NetworkConnection c;
                while ((c = driver.Accept()) != default)
                    Inbox.Enqueue(new Received { Peer = Track(c), Kind = EventKind.Connected });
            }

            rateWindow += Time.unscaledDeltaTime;
            if (rateWindow >= 1) { rateWindow = 0; received.Clear(); }

            NetworkEvent.Type type;
            while ((type = driver.PopEvent(out var conn, out var reader, out var pipe)) != NetworkEvent.Type.Empty)
            {
                if (!ids.TryGetValue(conn, out int id)) continue;
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        Inbox.Enqueue(new Received { Peer = id, Kind = EventKind.Connected });
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        received.TryGetValue(id, out int n);
                        received[id] = ++n;
                        if (n > MaxMessagesPerSecond) { Warn($"peer {id} sent more than {MaxMessagesPerSecond} messages in a second"); Disconnect(id); continue; }
                        if (reader.Length <= 0 || reader.Length > MaxPayload) continue;
                        var data = new byte[reader.Length];
                        reader.ReadBytes(new Span<byte>(data));
                        Inbox.Enqueue(new Received { Peer = id, Kind = EventKind.Data, Channel = pipe == reliable ? Channel.Reliable : Channel.Unreliable, Data = data });
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        Forget(id);
                        Inbox.Enqueue(new Received { Peer = id, Kind = EventKind.Disconnected });
                        break;
                }
            }

            // Reliable messages that didn't fit in the send window last time
            foreach (var kv in backlog)
            {
                var q = kv.Value;
                while (q.Count > 0 && TrySend(kv.Key, reliable, q.Peek()) == 0) q.Dequeue();
            }
        }

        // ------------------------------------------------------------------ sending
        /// <summary>Queue a message for a peer. Unreliable messages that can't go right now are simply dropped.</summary>
        public void Send(int peer, Channel channel, byte[] data)
        {
            if (disposed || !peers.ContainsKey(peer)) return;
            if (data.Length > MaxPayload) { Warn($"message of {data.Length} bytes is over the {MaxPayload} limit"); return; }
            if (channel == Channel.Unreliable) { TrySend(peer, unreliable, data); return; }

            if (!backlog.TryGetValue(peer, out var q)) backlog[peer] = q = new Queue<byte[]>();
            if (q.Count == 0 && TrySend(peer, reliable, data) == 0) return;
            q.Enqueue(data);   // keep reliable messages in order behind anything already waiting
            if (q.Count > MaxBacklog) { Warn($"peer {peer} stopped reading (backlog {q.Count})"); Disconnect(peer); }
        }

        int TrySend(int peer, NetworkPipeline pipe, byte[] data)
        {
            if (!peers.TryGetValue(peer, out var c)) return -1;
            int status = driver.BeginSend(pipe, c, out var writer, data.Length);
            if (status != 0) return status;
            writer.WriteBytes(new Span<byte>(data));
            status = driver.EndSend(writer);
            return status < 0 ? status : 0;
        }

        public void Disconnect(int peer)
        {
            if (!peers.TryGetValue(peer, out var c)) return;
            driver.Disconnect(c);
            Forget(peer);
            Inbox.Enqueue(new Received { Peer = peer, Kind = EventKind.Disconnected });
        }

        void Forget(int peer)
        {
            if (peers.TryGetValue(peer, out var c)) ids.Remove(c);
            peers.Remove(peer); backlog.Remove(peer); received.Remove(peer);
        }

        public string Address(int peer) => peers.TryGetValue(peer, out var c) ? driver.GetRemoteEndpoint(c).ToFixedString().ToString() : "?";

        /// <summary>Say goodbye: disconnect everyone (after one last pump so final messages leave), then free the driver.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            foreach (var c in peers.Values) driver.Disconnect(c);
            driver.ScheduleUpdate().Complete();
            driver.Dispose();
            peers.Clear(); ids.Clear(); backlog.Clear();
        }

        static void Warn(string what) => Debug.LogWarning($"TideWall net: {what}");
    }
}
