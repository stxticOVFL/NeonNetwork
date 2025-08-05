using Harmony;
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
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonNetwork.Online
{
    internal class Online 
    {
        const bool priority = true;
        const bool active = true;

        public static ulong sessionID;

        public static CSteamID steamID;
        internal static event Action LoggedIn;

        //const string URL = "https://nw-custom-lb.onrender.com";
        const string URL = "https://nw-custom-lb-production.up.railway.app";

        public static void Setup()
        {
            steamID = SteamUser.GetSteamID();
            personaChangedCB = Callback<PersonaStateChange_t>.Create(OnPersonaChange);
#if DEBUG
            return;
            SetupEventHandler();
#endif
        }

        public static void Invoke() => LoggedIn?.Invoke();

        public static UnityWebRequest Get(string route, bool auth = true)
        {
            var req = UnityWebRequest.Get(URL + route);
            SetupHeaders(req, auth);
            return req;
        }

        public static UnityWebRequest Post(string route, object data, bool auth = true)
        {
            var req = new UnityWebRequest(URL + route, "POST", new DownloadHandlerBuffer(), null);
            var d = JSON.Dump(data, EncodeOptions.NoTypeHints);
            NeonNetwork.Logger.DebugMsg($"{route} {d}");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(d));
            req.uploadHandler.contentType = "application/json";
            SetupHeaders(req, auth);
            return req;
        }

        public static void SetupHeaders(UnityWebRequest req, bool auth)
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

        static readonly HashSet<ulong> cached = [];
        static readonly HashSet<ulong> expecting = [];

        static readonly Dictionary<ulong, Action<string>> nameCBs = [];
        static readonly Dictionary<ulong, List<Texture2D>> pfpCBs = [];

        static Callback<PersonaStateChange_t> personaChangedCB;

        static readonly MethodInfo getPFPAs2D = NeonLite.Helpers.Method(typeof(LeaderboardIntegrationSteam), "GetSteamImageAsTexture2D");

        static void OnPersonaChange(PersonaStateChange_t p)
        {
            if (!expecting.Remove(p.m_ulSteamID) || !cached.Add(p.m_ulSteamID))
                return;

            NeonNetwork.Logger.DebugMsg($"OnPersonaChange {p.m_ulSteamID}");

            if (nameCBs.ContainsKey(p.m_ulSteamID))
            {
                NeonNetwork.Logger.DebugMsg($"NameCB");

                nameCBs[p.m_ulSteamID]?.Invoke(GetName(p.m_ulSteamID));
                nameCBs.Remove(p.m_ulSteamID);
            }

            if (pfpCBs.ContainsKey(p.m_ulSteamID))
            {
                // gonna fetch the texture manually
                NeonNetwork.Logger.DebugMsg($"TextureCB");

                var iImage = SteamFriends.GetMediumFriendAvatar(new CSteamID(p.m_ulSteamID));
                if (SteamUtils.GetImageSize(iImage, out var pnWidth, out var pnHeight))
                {
                    int num = (int)(4 * pnWidth * pnHeight * 2);
                    byte[] array = new byte[num];

                    var newTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
                    if (SteamUtils.GetImageRGBA(iImage, array, num))
                        newTexture.LoadRawTextureData(array);

                    var pixels = newTexture.GetPixelData<Color32>(0);
                    int y = 0;
                    var pfps = pfpCBs[p.m_ulSteamID];
                    foreach (var og in pfps.Where(x => x))
                    {
                        foreach (var chunk in pixels.Chunk(og.width).Reverse())
                            og.SetPixels32(0, y++, og.width, 1, [.. chunk]);
                        og.Apply();
                    }
                    //flipTexture.Invoke(null, [og]);
                }
                pfpCBs.Remove(p.m_ulSteamID);
            }

        }

        public static string GetName(ulong id, Action<string> cb = null, bool special = false)
        {
            if (!cached.Contains(id) && SteamFriends.RequestUserInformation((CSteamID)id, false))
            {
                expecting.Add(id);
                if (cb != null) {
                    if (nameCBs.ContainsKey(id))
                        nameCBs[id] += cb;
                    else
                        nameCBs.Add(id, cb);
                }
                return "";
            }
            return SteamFriends.GetFriendPersonaName((CSteamID)id);
        }

        public static Texture2D GetPFP(ulong id)
        {
            if (!cached.Contains(id) && SteamFriends.RequestUserInformation((CSteamID)id, false))
            {
                expecting.Add(id);
                var cb = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                if (pfpCBs.ContainsKey(id))
                    pfpCBs[id].Add(cb);
                else
                    pfpCBs.Add(id, [cb]);
                return cb;
            }
            return (Texture2D)getPFPAs2D.Invoke(null, [SteamFriends.GetMediumFriendAvatar(new CSteamID(id))]);
        }
    }
}
