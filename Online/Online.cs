using HarmonyLib;
using MelonLoader.TinyJSON;
using NeonLite;
using NeonLite.Modules;
using NeonNetwork.Objects;
using NeonNetwork.Objects.Popups;
using NeonNetwork.Objects.SidePanel;
using NeonNetwork.Objects.SidePanel.Contents;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonNetwork.Online
{
    internal class Online : IModule
    {
        const bool priority = true;
        const bool active = true;

        public static ulong sessionID;

        public static CSteamID steamID;
        internal static event Action LoggedIn;

#if DEBUG
        const string URL = "https://nwio-sse-test.onrender.com/";
#else
        const string URL = "";
#endif

        static void Activate(bool _)
        {
            steamID = SteamUser.GetSteamID();
#if DEBUG
            return;
            SetupEventHandler();
#endif
        }

        static void SetupHeaders(UnityWebRequest req, bool auth)
        {
            if (auth)
            {

            }

            req.SetRequestHeader("User-Agent", $"NeonNetwork/{NeonNetwork.Version}");
        }

        static void SetupEventHandler()
        {
            UnityWebRequest req = new(URL, "GET", new NWIOEventHandler(), null);
            SetupHeaders(req, false);

            int retries = 5;
            var res = req.SendWebRequest();
            res.completed += e =>
            {
                if (req.result == UnityWebRequest.Result.Success && retries-- != 0)
                {
                    NeonNetwork.Logger.Warning("Lost connection to NWIO, retrying...");
                    SetupEventHandler();
                }
                else
                    NeonNetwork.Logger.Error("Error connecting to NWIO.");
            };
        }

        internal static void OnEvent(string eventType, Variant json)
        {
            switch (eventType)
            {
                case "data":
                    {
                        var beer = (int)json["beersConsumed"];
                        Status.ShowStatus($"beer,, {beer}");
                        break;
                    }
            }
        }

        public static string GetName(ulong id, bool special = false)
        {
            return SteamFriends.GetFriendPersonaName((CSteamID)id);
        }

        public static Texture2D GetPFP(ulong id)
        {
            return (Texture2D)AccessTools.Method(typeof(LeaderboardIntegrationSteam), "GetSteamImageAsTexture2D")
                .Invoke(null, [SteamFriends.GetMediumFriendAvatar(new CSteamID(id))]);
        }
    }
}
