using MelonLoader;
using UnityEngine;
using NeonNetwork.Objects;
using System.Collections;
using System;
using System.IO;
using NeonNetwork.Objects.Popups;
using Steamworks;
using NeonNetwork.Online;
using NeonNetwork.Objects.SidePanel;
using NeonNetwork.Objects.Other;
using NeonNetwork.Resources;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace NeonNetwork
{
    internal class NeonNetwork : MelonMod
    {
        internal static AssetBundle bundle;
        internal static NeonNetwork instance;
        internal static Transform nnMMHolder;
        internal static Transform nnHolder;
        internal static bool connected = false;
        internal static bool logged = false;

        internal static string Version => instance.MelonAssembly.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

#if DEBUG
        internal static bool DEBUG { get { return Settings.debug.Value; } }
#else
        internal const bool DEBUG = false;
#endif
        public override void OnInitializeMelon()
        {
            instance = this;
            Logger.Msg($"Version {Version}");

#if DEBUG
            NeonLite.Modules.Anticheat.Register(MelonAssembly);
#endif
            NeonLite.NeonLite.LoadModules(MelonAssembly);
            NeonLite.Patching.AddPatch(typeof(MainMenu), "SetState", Init, NeonLite.Patching.PatchTarget.Prefix);

            Settings.Register();
        }

        public override void OnLateInitializeMelon()
        {
            GameObject obj = new("NeonNetwork");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            obj.transform.position = new Vector3(0, -1000, 0);
            obj.transform.localScale = Vector3.one;
            nnHolder = obj.transform;

            bundle = AssetBundle.LoadFromMemory(Resources.r.assetbundle);
        }

        public override void OnUpdate()
        {
            Rooms.Tick();
            if (nnMMHolder)
            {
                var cg = nnMMHolder.GetComponent<CanvasGroup>();
                cg.alpha = Settings.hidden.Value ? 0 : 1;
                cg.interactable = cg.blocksRaycasts = !Settings.hidden.Value;
            }
        }

        public static MelonLogger.Instance Logger => instance.LoggerInstance;

        public static void Init(MainMenu.State newState)
        {
            if (newState != MainMenu.State.Title)
                return;
            instance.Initialize();

            NeonLite.Patching.RemovePatch(typeof(MainMenu), "SetState", Init);
        }
        public void Initialize()
        {
            //yield return new WaitForSeconds(0.5f);
            Logger.Msg("Starting NeonNetwork..");

            GameObject obj = new("NeonNetwork", typeof(CanvasGroup));

            //obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            obj.transform.SetParent(MainMenu.Instance().transform.Find("Canvas"), false);
            obj.transform.localScale = Vector3.one;
            nnMMHolder = obj.transform;

            //Encryption.Setup();

            //yield return new WaitForSeconds(0.25f);
            Objects.Other.Version.Setup();
            StatusHandler.Setup();
            RenderLoadIcon.Setup();
            Popup.Setup();
            RaceSidebar.Setup();
            SidePanel.Setup();
            //Loading.Show("Connecting...", null);
            Online.Online.Setup();

            //yield return new WaitForSeconds(0.25f);

            Rooms.Setup();
            // ROOMS RELEASE

            // TODO: beh. temporary
            Online.Online.Invoke();
            //yield return null;
        }
    }

    internal static class Settings
    {
        public const string h = "NeonNetwork";
        public static MelonPreferences_Entry<bool> debug;
        public static MelonPreferences_Entry<bool> hasSetup;

        public static MelonPreferences_Entry<Color> ghostColor;
        public static MelonPreferences_Entry<Color> ghostColorS;
        public static MelonPreferences_Entry<Color> inColor;
        public static MelonPreferences_Entry<Color> inColorS;

        public static MelonPreferences_Entry<bool> hidden;
        public static MelonPreferences_Entry<bool> hiddenLB;

        public static MelonPreferences_Entry<bool> autoJoin;
        public static MelonPreferences_Entry<bool> autoJoinF;

        public static void Register()
        {
            NeonLite.Settings.AddHolder(h);

            autoJoin = NeonLite.Settings.Add(h, "Rooms", "autoJoin", "Public auto-room popup", null, true);
            autoJoinF = NeonLite.Settings.Add(h, "Rooms", "autoJoinForce", "Actually auto-join the auto-room", null, false, true);

            debug = NeonLite.Settings.Add(h, "Misc", "debug", "Debug Mode", null, false, true);
            hasSetup = NeonLite.Settings.Add(h, "Misc", "hasSetup", "Completed Setup", null, false, true);
            ghostColor = NeonLite.Settings.Add(h, "Misc", "mainColor", "Main Ghost Color", null, Color.white);
            ghostColor.OnEntryValueChanged.Subscribe((before, after) => Rooms.SetColor());
            ghostColorS = NeonLite.Settings.Add(h, "Misc", "subColor", "Secondary Ghost Color", null, Color.white);
            ghostColorS.OnEntryValueChanged.Subscribe((before, after) => Rooms.SetColor());
            inColor = NeonLite.Settings.Add(h, "Misc", "inmainColor", "Main In-ghost Color", null, Color.white);
            inColor.OnEntryValueChanged.Subscribe((before, after) => Rooms.SetColor());
            inColorS = NeonLite.Settings.Add(h, "Misc", "insubColor", "Secondary In-ghost Color", null, Color.white);
            inColorS.OnEntryValueChanged.Subscribe((before, after) => Rooms.SetColor());

            hidden = NeonLite.Settings.Add(h, "Misc", "hidden", "Hide UI", null, false);
            hiddenLB = NeonLite.Settings.Add(h, "Misc", "hiddenLB", "Hide Duel Leaderboard until Finish", null, false);
        }
    }
}
