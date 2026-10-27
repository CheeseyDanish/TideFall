// TideWall 3D — LAN discovery: a host broadcasts a small UDP beacon every second ("TIDEWALL2|port|code|name|info");
// the Join screen lists whatever it hears. A relay host includes its join code, so LAN friends can join either way.
// Beacons are untrusted text: names and info are stripped of rich-text tags and length-limited before display.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TideWall
{
    /// <summary>LAN games: the host's beacon and the joiner's listener.</summary>
    public static class NetDiscovery
    {
        public class Found { public string address, nick, info, code; public int port; public float seen; }

        static UdpClient listener, sender;
        static readonly ConcurrentQueue<Found> heard = new();
        public static readonly List<Found> Games = new();

        public static void Beacon(string nick, int port, string code, string info)
        {
            try
            {
                sender ??= new UdpClient { EnableBroadcast = true };
                var data = Encoding.UTF8.GetBytes($"{NetProtocol.DiscoveryTag}|{port}|{Clean(code)}|{Clean(nick)}|{Clean(info)}");
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, NetProtocol.DiscoveryPort));
                sender.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, NetProtocol.DiscoveryPort));   // same machine
            }
            catch (Exception) { }
        }

        static string Clean(string s) => (s ?? "").Replace("|", "/");

        /// <summary>Text heard from the network, made safe to show: no rich-text tags, no control characters, short.</summary>
        static string Safe(string s, int max)
        {
            var sb = new StringBuilder();
            foreach (char ch in s ?? "") if (ch != '<' && ch != '>' && !char.IsControl(ch)) sb.Append(ch);
            var t = sb.ToString().Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }

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
                            if (data.Length > 512) continue;
                            var parts = Encoding.UTF8.GetString(data).Split('|');
                            if (parts.Length < 5 || parts[0] != NetProtocol.DiscoveryTag || !int.TryParse(parts[1], out int port) || port < 0 || port > 65535) continue;
                            heard.Enqueue(new Found { address = any.Address.ToString(), port = port, code = Safe(parts[2], 12), nick = Safe(parts[3], 16), info = Safe(parts[4], 60) });
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
                if (old == null) { if (Games.Count < 20) Games.Add(f); }   // a flood of fake beacons can't fill the screen
                else { old.seen = now; old.info = f.info; old.code = f.code; if (old.address == "127.0.0.1" && f.address != "127.0.0.1") old.address = f.address; }
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
