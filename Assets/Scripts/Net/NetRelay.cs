// TideWall 3D — Unity Relay: internet games without port forwarding. The host asks Unity's relay service for an
// allocation and a short join code; friends join the allocation with the code. All traffic goes through the relay,
// encrypted (DTLS). Signing in is anonymous (no account for players), but the project itself must be linked to a
// Unity Cloud project (Edit > Project Settings > Services) for the service to answer.
using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace TideWall
{
    public static class NetRelay
    {
        /// <summary>DTLS: the connection to the relay is encrypted.</summary>
        const string Protocol = "dtls";

        /// <summary>Anonymous sign-in with a profile of its own for this launch, so two copies of the game on one computer
        /// (or a quick restart) are different players to the relay.</summary>
        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                options.SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                Log("starting Unity services");
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Log("signing in");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        static void Log(string what) => UnityEngine.Debug.Log($"TideWall relay: {what}");

        /// <summary>Make an allocation for this many other players and get its join code.</summary>
        public static async Task<(RelayServerData relay, string code)> Host(int others)
        {
            await SignIn();
            Log("creating an allocation");
            var allocation = await RelayService.Instance.CreateAllocationAsync(others);
            var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Log($"join code {code} (region {allocation.Region})");
            return (allocation.ToRelayServerData(Protocol), code);
        }

        public static async Task<RelayServerData> Join(string code)
        {
            await SignIn();
            Log($"joining {Clean(code)}");
            var allocation = await RelayService.Instance.JoinAllocationAsync(Clean(code));
            Log($"joined (region {allocation.Region}); connecting to the host");
            return allocation.ToRelayServerData(Protocol);
        }

        /// <summary>Join codes are letters and digits; people type them in any case, with spaces.</summary>
        public static string Clean(string code)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in code ?? "") if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        /// <summary>A failure, in words a player can act on.</summary>
        public static string Explain(Exception e)
        {
            UnityEngine.Debug.LogWarning($"TideWall relay: {e}");
            string m = e.Message ?? "";
            if (e is RelayServiceException rse && rse.Reason == RelayExceptionReason.JoinCodeNotFound) return "No game with that code. Check it and try again.";
            if (m.Contains("project") || m.Contains("Project") || e is ServicesInitializationException)
                return "Join codes need this game linked to Unity Cloud (Edit > Project Settings > Services). Use LAN / address instead.";
            if (e is RequestFailedException) return "Couldn't reach Unity's relay service. Check your internet connection.";
            return "Couldn't set up the relay: " + m;
        }
    }
}
