// TideWall 3D — online play, the entry points. A machine is offline, a host or a client:
//   host    runs the game exactly as offline, plus a NetHost that accepts clients, simulates their players
//           (Player kind Remote) and streams the world to them.
//   client  mirrors the host's snapshots (NetClient); this machine's own players predict their movement locally
//           (Player kind LocalProxy) and are corrected by the host.
// Two ways to connect: directly (LAN, or a forwarded UDP port; optional room password) or through Unity Relay
// (a join code; encrypted; no port forwarding — needs the project linked to Unity Cloud).
// Both objects survive scene reloads (a host's "try again" restarts everyone without dropping connections).
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    public static class Net
    {
        public static NetHost Host { get; private set; }
        public static NetClient Client { get; private set; }
        public static bool IsHost => Host != null;
        public static bool IsClient => Client != null;
        public static bool Online => Host != null || Client != null;

        /// <summary>Host directly on a UDP port. Returns null (with the reason) if the port can't be used.</summary>
        public static NetHost StartHost(int port, string nick, string password, out string error)
        {
            Shutdown(null);
            var h = NewHost();
            if (!h.Listen(port, nick, password, out error)) { UnityEngine.Object.Destroy(h.gameObject); return null; }
            return Host = h;
        }

        /// <summary>Host through Unity Relay. Signs in, makes an allocation and a join code, then calls back
        /// (host, null) or (null, why it failed).</summary>
        public static async void StartRelayHost(string nick, string password, Action<NetHost, string> done)
        {
            Shutdown(null);
            try
            {
                var (relay, code) = await NetRelay.Host(PlayerManager.MaxPlayers - 1);
                var h = NewHost();
                if (!h.ListenRelay(relay, code, nick, password, out var error)) { UnityEngine.Object.Destroy(h.gameObject); done(null, error); return; }
                Host = h;
                done(h, null);
            }
            catch (Exception e) { done(null, NetRelay.Explain(e)); }
        }

        static NetHost NewHost()
        {
            var go = new GameObject("Net Host");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<NetHost>();
        }

        /// <summary>Connect to a host by address. The result reports success or failure later (NetClient.Status).</summary>
        public static NetClient StartClient(string address, int port, string nick, string password, InputDevice[] firstPlayer)
        {
            var c = NewClient();
            c.Connect(address, port, nick, password, firstPlayer);
            return c;
        }

        /// <summary>Join a Relay game with its code. The result reports success or failure later (NetClient.Status).</summary>
        public static NetClient StartRelayClient(string code, string nick, string password, InputDevice[] firstPlayer)
        {
            var c = NewClient();
            Join(c, code, nick, password, firstPlayer);
            return c;
        }

        static async void Join(NetClient c, string code, string nick, string password, InputDevice[] firstPlayer)
        {
            try
            {
                var relay = await NetRelay.Join(code);
                if (c != null && Client == c) c.ConnectRelay(relay, code, nick, password, firstPlayer);
            }
            catch (Exception e) { if (c != null) c.Fail(NetRelay.Explain(e)); }
        }

        static NetClient NewClient()
        {
            Shutdown(null);
            var go = new GameObject("Net Client");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return Client = go.AddComponent<NetClient>();
        }

        /// <summary>Leave online play. A host tells its clients why.</summary>
        public static void Shutdown(string reason)
        {
            if (Host != null) { Host.Stop(reason ?? "The host left."); UnityEngine.Object.Destroy(Host.gameObject); Host = null; }
            if (Client != null) { Client.Stop(); UnityEngine.Object.Destroy(Client.gameObject); Client = null; }
        }
    }
}
