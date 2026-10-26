// TideWall 3D — TCP framing for the online protocol. Each connection has a reader thread (frames go into Inbox,
// handled on the main thread) and a writer thread (so a slow peer can never stall the game). Plus LAN discovery:
// hosts broadcast a small UDP beacon every second; the Join screen lists whatever it hears.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TideWall
{
    public class NetConnection
    {
        public readonly int Id;
        public readonly ConcurrentQueue<byte[]> Inbox = new();
        public bool Closed => closed;
        public string Remote { get; }
        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly BlockingCollection<byte[]> outbox = new(new ConcurrentQueue<byte[]>());
        volatile bool closed;

        /// <summary>Clients only ever send small messages (hello, join, input): anything bigger is refused.</summary>
        public const int MaxClientMessage = 64 * 1024;
        /// <summary>Frames waiting to be written; a connection that falls this far behind is closed rather than queued forever.</summary>
        public const int MaxQueued = 600;
        readonly int maxMessage;

        public NetConnection(int id, TcpClient client, int maxMessage = 8 * 1024 * 1024)
        {
            Id = id; tcp = client; this.maxMessage = maxMessage;
            tcp.NoDelay = true;
            stream = tcp.GetStream();
            Remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
            new Thread(ReadLoop) { IsBackground = true, Name = $"TideWall net read {id}" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = $"TideWall net write {id}" }.Start();
        }

        void ReadLoop()
        {
            try
            {
                var br = new BinaryReader(stream);
                while (!closed)
                {
                    int len = br.ReadInt32();
                    if (len <= 0 || len > maxMessage) break;
                    var data = br.ReadBytes(len);
                    if (data.Length < len) break;
                    Inbox.Enqueue(data);
                }
            }
            catch (Exception) { }
            Close();
        }

        void WriteLoop()
        {
            try
            {
                foreach (var data in outbox.GetConsumingEnumerable())
                {
                    if (closed) break;
                    var len = BitConverter.GetBytes(data.Length);
                    stream.Write(len, 0, 4);
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception) { }
            Close();
        }

        public void Send(byte[] payload)
        {
            if (closed) return;
            // A stalled peer (frozen game, dead Wi-Fi) would otherwise grow this queue without limit
            if (outbox.Count > MaxQueued) { Close(); return; }
            try { outbox.Add(payload); } catch (InvalidOperationException) { }
        }

        /// <summary>Send, then close once it's written (goodbyes).</summary>
        public void SendAndClose(byte[] payload)
        {
            Send(payload);
            try { outbox.CompleteAdding(); } catch (Exception) { }
            ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(300); Close(); });
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            try { outbox.CompleteAdding(); } catch (Exception) { }
            try { tcp.Close(); } catch (Exception) { }
        }
    }

    /// <summary>LAN games: the host's beacon and the joiner's listener.</summary>
    public static class NetDiscovery
    {
        public class Found { public string address, nick, info; public int port; public float seen; }

        static UdpClient listener, sender;
        static readonly ConcurrentQueue<Found> heard = new();
        public static readonly List<Found> Games = new();

        public static void Beacon(string nick, int port, string info)
        {
            try
            {
                sender ??= new UdpClient { EnableBroadcast = true };
                var data = Encoding.UTF8.GetBytes($"{NetProtocol.DiscoveryTag}|{port}|{Clean(nick)}|{Clean(info)}");
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, NetProtocol.DiscoveryPort));
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, NetProtocol.DiscoveryPort));   // same machine
            }
            catch (Exception) { }
        }

        static string Clean(string s) => (s ?? "").Replace("|", "/");

        public static void StartListening()
        {
            if (listener != null) return;
            try
            {
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, NetProtocol.DiscoveryPort));
                var l = listener;
                new Thread(() =>
                {
                    var any = new IPEndPoint(IPAddress.Any, 0);
                    try
                    {
                        while (true)
                        {
                            var data = l.Receive(ref any);
                            var parts = Encoding.UTF8.GetString(data).Split('|');
                            if (parts.Length < 4 || parts[0] != NetProtocol.DiscoveryTag || !int.TryParse(parts[1], out int port)) continue;
                            heard.Enqueue(new Found { address = any.Address.ToString(), port = port, nick = parts[2], info = parts[3] });
                        }
                    }
                    catch (Exception) { }
                }) { IsBackground = true, Name = "TideWall discovery" }.Start();
            }
            catch (Exception e) { Debug.LogWarning($"TideWall: LAN discovery unavailable ({e.Message})"); listener = null; }
        }

        public static void StopListening()
        {
            try { listener?.Close(); } catch (Exception) { }
            listener = null;
            Games.Clear();
        }

        /// <summary>Main thread: fold in what was heard, forget games silent for 4 s.</summary>
        public static void Poll()
        {
            float now = Time.unscaledTime;
            while (heard.TryDequeue(out var f))
            {
                f.seen = now;
                // The same host can be heard on loopback and on the LAN: keep one entry per port+nick, prefer the LAN address.
                var old = Games.Find(g => g.port == f.port && g.nick == f.nick);
                if (old == null) Games.Add(f);
                else { old.seen = now; old.info = f.info; if (old.address == "127.0.0.1" && f.address != "127.0.0.1") old.address = f.address; }
            }
            Games.RemoveAll(g => now - g.seen > 4);
        }

        /// <summary>This machine's IPv4 addresses (shown to the host so friends can type one in).</summary>
        public static string LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) list.Add(a.ToString());
            }
            catch (Exception) { }
            return list.Count > 0 ? string.Join(", ", list) : "127.0.0.1";
        }
    }
}
