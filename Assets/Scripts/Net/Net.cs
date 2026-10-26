// TideWall 3D — online play, the entry points. A machine is offline, a host or a client:
//   host    runs the game exactly as offline, plus a NetHost that accepts clients, simulates their players
//           (Player kind Remote) and streams the world to them.
//   client  runs no simulation: GameManager mirrors the host's snapshots (NetClient) and only this machine's
//           players move and look locally (Player kind LocalProxy).
// Both objects survive scene reloads (a host's "try again" restarts everyone without dropping connections).
using UnityEngine;

namespace TideWall
{
    public static class Net
    {
        public static NetHost Host { get; private set; }
        public static NetClient Client { get; private set; }
        public static bool IsHost => Host != null;
        public static bool IsClient => Client != null;
        public static bool Online => Host != null || Client != null;

        /// <summary>Open the game to others on this port. Returns null (and logs why) if the port can't be used.</summary>
        public static NetHost StartHost(int port, string nick)
        {
            Shutdown(null);
            var go = new GameObject("Net Host");
            Object.DontDestroyOnLoad(go);
            var h = go.AddComponent<NetHost>();
            if (!h.Listen(port, nick)) { Object.Destroy(go); return null; }
            Host = h;
            return h;
        }

        /// <summary>Connect to a host. The result reports success or failure later (NetClient.Status).</summary>
        public static NetClient StartClient(string address, int port, string nick, UnityEngine.InputSystem.InputDevice[] firstPlayer)
        {
            Shutdown(null);
            var go = new GameObject("Net Client");
            Object.DontDestroyOnLoad(go);
            Client = go.AddComponent<NetClient>();
            Client.Connect(address, port, nick, firstPlayer);
            return Client;
        }

        /// <summary>Leave online play. A host tells its clients why.</summary>
        public static void Shutdown(string reason)
        {
            if (Host != null) { Host.Stop(reason ?? "The host left."); Object.Destroy(Host.gameObject); Host = null; }
            if (Client != null) { Client.Stop(); Object.Destroy(Client.gameObject); Client = null; }
        }
    }
}
