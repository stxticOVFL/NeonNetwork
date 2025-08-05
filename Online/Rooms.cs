using HarmonyLib;
using I2.Loc;
using NeonLite;
using NeonLite.Modules;
using NeonLite.Modules.Optimization;
using NeonNetwork.Objects;
using NeonNetwork.Objects.Other;
using NeonNetwork.Objects.Popups;
using NeonNetwork.Objects.SidePanel;
using NeonNetwork.Objects.SidePanel.Contents;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;
using UnityEngine;
using UniverseLib.Utility;

namespace NeonNetwork.Online
{
    internal class RoomsModule : IModule
    {
#pragma warning disable CS0414
        const bool priority = true;
        const bool active = true;

        static void Activate(bool _)
        {
            Patching.PerformHarmonyPatches(typeof(Rooms));
        }

        static bool stallOnce = false;

        static bool OnLevelLoad(LevelData level)
        {
            if (!level || level.type == LevelData.LevelType.Hub)
                return true;

            if (stallOnce)
            {
                stallOnce = false;
                Rooms.SetupRace();
                return true;
            }

            Rooms.lastFrameT = DateTime.MinValue;
            Rooms.fakeIndex = 0;

            stallOnce = Rooms.connected;
            return !stallOnce;
        }
    }

    class GhostsManager : MonoBehaviour, IModule
    {
        public static GhostsManager i;

#pragma warning disable CS0414
        const bool priority = true;
        const bool active = true;

        static bool disableAwake;

        static void Activate(bool _)
        {
            Patching.AddPatch(typeof(GhostPlayback), "Awake", CheckAwake, Patching.PatchTarget.Prefix);
        }

        static bool CheckAwake() => !disableAwake;

        internal class PlayerGhostPlayback : MonoBehaviour // entirely custom version of GhostPlayback
        {
            static readonly int _MainColor = Shader.PropertyToID("_MainColor");
            static readonly int _GlowColor = Shader.PropertyToID("_GlowColor");
            static readonly int _DitherMax = Shader.PropertyToID("_DitherMax");

            class TriggerData
            {
                public int trigger;
                public float time;
            }

            static GameObject prefab;

            GameObject ghostObject;
            public GhostActor ghostActor;
            Vector3 lastPos;

            public Rooms.User user;

            const int MAX_FRAMES = 60;
            readonly GhostFrame[] _frames = new GhostFrame[MAX_FRAMES];
            readonly TriggerData[] _triggers = new TriggerData[MAX_FRAMES];
            int frameCount = 0;
            int triggerCount = 0;
            IEnumerable<GhostFrame> Frames { get { return _frames.Take(frameCount); } }
            IEnumerable<TriggerData> Triggers { get { return _triggers.Take(triggerCount); } }

            public DateTime lastPing;
            public float offset;

            Nametag nametag;

            void Start()
            {
                if (!prefab)
                    prefab = UnityEngine.Resources.Load("PlayerGhost") as GameObject;

                ghostObject = Instantiate(prefab, transform);
                ghostActor = ghostObject.GetComponent<GhostActor>();

                // make a ghostplayback to make it happy
                disableAwake = true;
                var playback = gameObject.AddComponent<GhostPlayback>();
                playback.actor = GhostPlayback.Actor.White;
                ghostActor.SetGhostPlayback(playback);
                playback.enabled = false;
                disableAwake = false;

                var meshrender = ghostActor.GetComponentInChildren<SkinnedMeshRenderer>();
                meshrender.material.SetFloat("_DitherMax", 1.1f);
                SetColor();

                ghostObject.SetActive(true);
                nametag = Nametag.Spawn(ghostActor.transform, user.steamID.m_SteamID);
                SetOpacity(0, true);
            }

            public void Clear()
            {
                frameCount = 0;
                triggerCount = 0;
            }

            void Update()
            {
                Parse();

                if (!user.ghostVisible)
                    SetOpacity(0, true);
                else if (RM.mechController)
                {
                    const float MAX_DIST = 8f;
                    var dist = Vector3.Distance(RM.drifter.m_cameraHolder.position, nametag.transform.position);
                    if (dist < MAX_DIST)
                        SetOpacity(1 - ((MAX_DIST - dist) / MAX_DIST), user.tagVisible);
                    else
                        SetOpacity(1, user.tagVisible);
                }
                if (!user.tagVisible)
                    nametag.SetOpacity(0);
            }

            static FieldInfo actorLastPos = NeonLite.Helpers.Field(typeof(GhostActor), "m_lastPos");

            float SecondsTillLand()
            {
                foreach (var f in Frames)
                {
                    if (f.grounded)
                        return f.time;
                }
                return float.MaxValue;
            }

            void Parse()
            {
                enabled = true;

                // first, do the offset
                Frames.Do(x => x.time -= Time.unscaledDeltaTime);
                Triggers.Do(x => x.time -= Time.unscaledDeltaTime);
                // then, find the index of negative if any
                GhostFrame lastFrame = null;
                int index = 0;
                bool performed = false;
                for (int i = 0; i < frameCount; ++i)
                {
                    var frame = _frames[i];

                    if (frame.time > 0)
                    {
                        if (lastFrame == null)
                            break; // we don't have a frame yet

                        if (frame.index <= lastFrame.index)
                            break; // this new frame is from a new batch

                        // we have a frame!
                        performed = true;

                        // do fuckass math to lerp it (essentially lerp in reverse)
                        var interp = GhostFrame.Interpolate(frame, lastFrame, frame.time / (frame.time - lastFrame.time));
                        ghostActor.transform.position = lastPos = interp.pos;
                        ghostActor.transform.rotation = Quaternion.Euler(0, interp.angle, 0);
                        ghostActor.SetFrame(interp);

                        break;
                    }
                    lastFrame = frame;
                    index = i;
                }

                NeonNetwork.Logger.DebugMsg($"{index} {user.steamID}");

                if (!performed)
                {
                    if (lastFrame == null)
                        SetOpacity(0, true);
                    else
                    {
                        actorLastPos.SetValue(ghostActor, lastPos);
                        ghostActor.transform.position = lastFrame.pos;
                        ghostActor.transform.rotation = Quaternion.Euler(0, lastFrame.angle, 0);
                        ghostActor.SetFrame(lastFrame);
                    }
                }

                if (index != 0)
                {
                    // shift the array
                    frameCount -= index;
                    Array.Copy(_frames, index, _frames, 0, frameCount);
                }

                index = 0;

                for (int i = 0; i < triggerCount; ++i)
                {
                    var trigger = _triggers[i];
                    if (trigger.time > 0)
                        break;

                    switch (trigger.trigger)
                    {
                        case 1:
                            ghostActor.Jump();
                            break;
                        case 2:
                            {
                                var d = Math.Max(0, SecondsTillLand() - .25f);
                                if (d > 0.5f)
                                    ghostActor.Fall(d);
                                break;
                            }
                        case 3:
                            ghostActor.Land();
                            break;
                    }
                    index++;
                }

                if (index != 0)
                {
                    // shift the array
                    triggerCount -= index;
                    Array.Copy(_triggers, index, _triggers, 0, triggerCount);
                }
            }

            public void Add(GhostFrame frame)
            {
                if (frameCount + 1 >= MAX_FRAMES)
                    return;

                if (!enabled)
                {
                    Clear();
                    enabled = true;
                }

                frame.time += offset;
                _frames[frameCount++] = frame;
                // do trigger stuff

                if (frame.triggerEvent > 0 && frame.triggerEvent != 2000)
                {
                    TriggerData td = new()
                    {
                        trigger = frame.triggerEvent,
                        time = frame.time
                    };

                    if (td.trigger == 3)
                        td.time -= .4f;
                    else if (td.trigger == 1)
                        td.time -= .1f;

                    _triggers[triggerCount++] = td;

                    var iter = Triggers.OrderBy(x => x.time).ToArray();
                    Array.Copy(iter, _triggers, triggerCount);
                }
            }

            public void DoOffset(DateTime final)
            {
                if (isActiveAndEnabled)
                {
                    Frames.Do(x => x.time -= offset);
                    Triggers.Do(x => x.time -= offset);
                }
                offset = (float)(final - lastPing).TotalSeconds;
                if (isActiveAndEnabled)
                {
                    Frames.Do(x => x.time += offset);
                    Triggers.Do(x => x.time += offset);
                }
            }

            public void SetColor()
            {
                var meshrender = ghostActor.GetComponentInChildren<SkinnedMeshRenderer>();

                meshrender.materials[0].SetColor(_MainColor, SteamMatchmaking.GetLobbyMemberData(Rooms.roomSID, user.steamID, "color").ToColor());
                meshrender.materials[0].SetColor(_GlowColor, SteamMatchmaking.GetLobbyMemberData(Rooms.roomSID, user.steamID, "colorsub").ToColor());
                meshrender.materials[1].SetColor(_MainColor, SteamMatchmaking.GetLobbyMemberData(Rooms.roomSID, user.steamID, "incolor").ToColor());
                meshrender.materials[1].SetColor(_GlowColor, SteamMatchmaking.GetLobbyMemberData(Rooms.roomSID, user.steamID, "incolorsub").ToColor());
            }

            public void SetOpacity(float opacity, bool nt)
            {
                var ghostactor = ghostActor.GetComponentInChildren<SkinnedMeshRenderer>();

                ghostactor.materials[0].SetFloat(_DitherMax, 1.1f * opacity);
                ghostactor.materials[1].SetFloat(_DitherMax, 1.1f * opacity);

                if (nt)
                    nametag.SetOpacity(opacity);
            }
        }

        void Awake()
        {
            i = this;
            gameObject.SetActive(false);
        }

        static readonly byte[] tickBufferA = new byte[1 + Rooms.GhostFrameHandler.SIZEOF];
        static readonly BinaryReader tickReader = new(new MemoryStream(tickBufferA, false));

        public void Update()
        {
            var level = Rooms.game.GetCurrentLevel();
            var users = Rooms.inRoom.Values.Where(user => user.level == level && user.accepted).Select(user => user.steamID);

            while (SteamNetworking.IsP2PPacketAvailable(out var size))
            {
                SteamNetworking.ReadP2PPacket(tickBufferA, size, out var read, out var from);

                tickReader.BaseStream.Position = 0;
                var type = tickReader.ReadByte();
                var user = Rooms.inRoom[from.m_SteamID];

                // read the first byte *anyway*
                if (type >= 0x10)
                    NeonNetwork.Logger.DebugMsg($"recieve {type} {from}");

                switch (type)
                {
                    case 0x80: // ping *request* from user 1
                        {
                            byte[] frameBytes = [0x81];

                            user.ghost.lastPing = DateTime.UtcNow;
                            SteamNetworking.SendP2PPacket(from, frameBytes, 1, EP2PSend.k_EP2PSendReliable);
                            continue;
                        }
                    case 0x81: // ping from user 2
                        {
                            byte[] frameBytes = [0x82];

                            //user.ghost.lastPing = DateTime.UtcNow;
                            SteamNetworking.SendP2PPacket(from, frameBytes, 1, EP2PSend.k_EP2PSendReliable);
                            continue;
                        }
                    case 0x82: // ping response from user 1
                        {
                            //byte[] frameBytes = [0x83];
                            user.ghost.DoOffset(DateTime.UtcNow);
                            //SteamNetworking.SendP2PPacket(from, frameBytes, 1, EP2PSend.k_EP2PSendReliable);
                            continue;
                        }
                    case 0x83: // ping response from user 2
                        {
                            user.ghost.DoOffset(DateTime.UtcNow);
                            continue;
                        }
                }

                if (!users.Contains(from))
                    continue; // don't handle

                var gp = user.ghost;

                user.responded = type != 2;
                if (type == 2)
                {
                    Hide(user); // hide me! i'm restarting!
                    continue;
                }

                var frame = Rooms.GhostFrameHandler.FromReader(tickReader);
                if (!user.responded)
                {
                    NeonNetwork.Logger.DebugMsg($"Handle new {from} {type}");
                    user.attempted = Rooms.WriteCurrentFrame(from);
                }

                user.ghost.Add(frame);
            }
        }

        static readonly List<PlayerGhostPlayback> playbacks = [];

        public static void Add(Rooms.User user)
        {
            i.gameObject.SetActive(true);

            var g = new GameObject(user.steamID.ToString(), typeof(PlayerGhostPlayback)).GetComponent<PlayerGhostPlayback>();
            g.transform.parent = i.transform;
            g.user = user;
            user.ghost = g;
            playbacks.Add(g);
        }

        public static void Hide(Rooms.User user)
        {
            user.ghost.enabled = false;
            user.ghost.SetOpacity(0, true);
        }

        public static void Remove(Rooms.User user)
        {
            playbacks.Remove(user.ghost);
            Destroy(user.ghost.gameObject);
        }

        public static void Clear()
        {
            foreach (var g in playbacks)
                Destroy(g.gameObject);
            playbacks.Clear();
            i.gameObject.SetActive(false);
        }
    }

    internal static class Rooms
    {
        public static Game game;
        public static bool setup = false;

        public static User selfUser;

        public static bool isPrivate = true;
        public static bool isRacing = false;
        public static bool autoroom = false;

        const int NETCODE_VERSION = 4;
        static readonly string AUTOROOM_NAME = $"!!!!!!!!!!AUTOROOM!!!!!!!!!!{NETCODE_VERSION}";

        #region Exposed

        public static void Setup()
        {
            game = Singleton<Game>.Instance;
            lobbyCreatedCB = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyListCB = Callback<LobbyMatchList_t>.Create(OnListCallback);
            lobbyEnterCB = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            lobbyDataUpdateCB = Callback<LobbyDataUpdate_t>.Create(OnDataUpdate);
            lobbyChatUpdateCB = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
            lobbyChatMSGCB = Callback<LobbyChatMsg_t>.Create(OnChatMsg);
            P2PreqCB = Callback<P2PSessionRequest_t>.Create(OnP2PReq);
            lobbyJoinReqCB = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinReq);

            Nametag.Setup();

            game.OnLevelLoadComplete += OnStageStartRace;
            new GameObject("Ghosts", typeof(GhostsManager)).transform.parent = NeonNetwork.nnHolder;

            setup = SteamUser.BLoggedOn();
            if (!setup)
                return;

            selfUser = new User { steamID = Online.steamID };

            // SteamApps.GetLaunchCommandLine(out var cmdLine, 260);
            // if (NeonNetwork.DEBUG)
            //     NeonNetwork.Logger.Msg(cmdLine);

            // https://forum.unity.com/threads/steam-invites-getlaunchcommandline-not-working.1152140/
            bool cmdline = false;
            var args = Environment.GetCommandLineArgs();

            if (args.Length >= 2)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].ToLower() == "+connect_lobby")
                    {
                        if (ulong.TryParse(args[i + 1], out ulong lobbyID))
                        {
                            if (lobbyID > 0)
                            {
                                var fakeReq = new GameLobbyJoinRequested_t
                                {
                                    m_steamIDLobby = (CSteamID)lobbyID
                                };
                                Online.LoggedIn += () => OnLobbyJoinReq(fakeReq);
                                cmdline = true;
                            }
                        }
                        break;
                    }
                }
            }
            if (!cmdline)
                Online.LoggedIn += PromptAutojoin;
        }

        public static void PromptAutojoin()
        {
            if (!Settings.autoJoin.Value)
                return;

            TextButtons.Show("NeonNetwork/ROOMS_WARN_AUTOROOM_AUTO",
                (popup, _) =>
                {
                    popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                    {
                        popup.Leave();
                        JoinAutoroom(0);
                    });
                    popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                });
        }

        public static void CreateRoom(string name, int max)
        {
            SteamMatchmaking.CreateLobby(autoroom ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeInvisible, max);
            roomName = name;
            owner = Online.steamID;
        }

        public static void JoinRoom(string ID)
        {
            id = (ushort)DecodeID(new string(ID.Take(4).ToArray()));
            if (ID.Length == 4)
                secret = 0;
            else
                secret = (ushort)DecodeID(new string(ID.Skip(4).ToArray()));

            roomSearch = true;
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter("id", EncodeID(id), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("version", NETCODE_VERSION.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.RequestLobbyList();
        }

        public static void ListPublicRooms()
        {
            roomSearch = false;
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(100);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter("public", "y", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("version", NETCODE_VERSION.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.RequestLobbyList();
        }

        public static void MakeRoomPublic()
        {
            if (owner != Online.steamID)
                return;
            isPrivate = false;
            SteamMatchmaking.SetLobbyData(roomSID, "public", "y");
            SteamMatchmaking.SetLobbyType(roomSID, ELobbyType.k_ELobbyTypePublic);
            chatMsgWriter.Flush();
            chatMsgWriter.Write((byte)LobbyChatOp.Public);
            chatMsgWriter.Write(id);
            SendLobbyMsg();
        }

        public static void MakeRoomPrivate()
        {
            if (owner != Online.steamID)
                return;
            isPrivate = true;
            GenerateID();
            SteamMatchmaking.SetLobbyData(roomSID, "public", "n");
            SteamMatchmaking.SetLobbyType(roomSID, ELobbyType.k_ELobbyTypeInvisible);
            chatMsgWriter.Flush();
            chatMsgWriter.Write((byte)LobbyChatOp.Private);
            chatMsgWriter.Write(id);
            chatMsgWriter.Write(secret);
            SendLobbyMsg();
        }

        public static void KickUser(ulong steamID)
        {
            if (owner != Online.steamID)
                return;
            chatMsgWriter.Flush();
            chatMsgWriter.Write((byte)LobbyChatOp.Kick);
            chatMsgWriter.Write(steamID);
            SendLobbyMsg();
        }

        public static void LeaveRoom(bool forced = false)
        {
            Unready(true);
            if (!forced)
            {
                chatMsgWriter.Flush();
                chatMsgWriter.Write((byte)LobbyChatOp.Leave);
                SendLobbyMsg();
            }
            Popup.Finish();
            RaceSidebar.Clear();
            SteamMatchmaking.LeaveLobby(roomSID);
            SidePanel.LoadContent<RoomsList>("RoomsList", "NeonNetwork/ROOMS_LABEL");
            GhostsManager.Clear();
            connected = false;
            autoroom = false;
            inRoom.Clear();
            SetRP();
        }

        public static void SetColor()
        {
            if (connected)
            {
                SteamMatchmaking.SetLobbyMemberData(roomSID, "color", Settings.ghostColor.Value.ToHex());
                SteamMatchmaking.SetLobbyMemberData(roomSID, "colorsub", Settings.ghostColorS.Value.ToHex());
                SteamMatchmaking.SetLobbyMemberData(roomSID, "incolor", Settings.inColor.Value.ToHex());
                SteamMatchmaking.SetLobbyMemberData(roomSID, "incolorsub", Settings.inColorS.Value.ToHex());
            }
        }

        public static void Invite()
        {
            Popup.ShowPopup<FriendSelect>("FriendSelect");
        }

        public static void SetRP()
        {
            if (!connected)
            {
                SteamFriends.SetRichPresence("status", null);
                SteamFriends.SetRichPresence("connect", null);
                SteamFriends.SetRichPresence("steam_player_group", null);
                SteamFriends.SetRichPresence("steam_player_group_size", null);

            }
            else if (isRacing)
            {
                string localized = LocalizationManager.GetTranslation(raceLevel.GetLevelDisplayName());
                if (string.IsNullOrEmpty(localized))
                    localized = raceLevel.levelDisplayName;

                SteamFriends.SetRichPresence("status", $"Dueling in a lobby ({localized})");
                SteamFriends.SetRichPresence("connect", null);
            }
            else if (isPrivate)
            {
                SteamFriends.SetRichPresence("status", "In a private lobby");
                SteamFriends.SetRichPresence("connect", null);
                // SteamFriends.SetRichPresence("steam_player_group", null);
                // SteamFriends.SetRichPresence("steam_player_group_size", null);
                SteamFriends.SetRichPresence("steam_player_group", roomSID.ToString());
                SteamFriends.SetRichPresence("steam_player_group_size", SteamMatchmaking.GetNumLobbyMembers(roomSID).ToString());
            }
            else
            {
                if (autoroom)
                    SteamFriends.SetRichPresence("status", $"In the public auto-room");
                else
                    SteamFriends.SetRichPresence("status", $"In a lobby ({roomName})");

                if (raceTime != -1)
                    SteamFriends.SetRichPresence("connect", null);
                else
                    SteamFriends.SetRichPresence("connect", null);
                SteamFriends.SetRichPresence("steam_player_group", roomSID.ToString());
                SteamFriends.SetRichPresence("steam_player_group_size", SteamMatchmaking.GetNumLobbyMembers(roomSID).ToString());
            }
        }

        const float PING_TIMER = 1f;
        static float pingTimer = 0;

        public static void Tick()
        {
            if (!connected)
                return;

            if (isRacing && !raceAccepted)
                raceTime = Math.Max(0, raceTime - Time.deltaTime);

            foreach (CSteamID p in invited.Keys.ToList())
            {
                if (invited[p] > 0f)
                    invited[p] = invited[p] - Time.unscaledDeltaTime;
            }

            if (inRoom.Count < 1)
                return;

            pingTimer += Time.unscaledDeltaTime;
            if (pingTimer > PING_TIMER)
            {
                pingTimer -= PING_TIMER;

                chatMsgWriter.Flush();
                chatMsgWriter.Write((byte)LobbyChatOp.Ping);
                SendLobbyMsg();
            }
        }

        public static void JoinAutoroom(ulong id)
        {
            autoroom = true;
            if (id != 0)
                SteamMatchmaking.JoinLobby((CSteamID)id);
            else
            {
                SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
                SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
                SteamMatchmaking.AddRequestLobbyListStringFilter("name", AUTOROOM_NAME, ELobbyComparison.k_ELobbyComparisonEqual);
                SteamMatchmaking.AddRequestLobbyListStringFilter("version", NETCODE_VERSION.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
                SteamMatchmaking.RequestLobbyList();
            }
        }


        #endregion Exposed

        #region ExposedRace
        public static void CallRace(LevelData level, float time, int leniency, int countdown)
        {
            if (owner != Online.steamID)
                return;
            if (level)
                raceLevel = level;
            // isRacing = true;
            raceWinnered = true;
            raceTime = time;
            raceLeniency = leniency;
            raceCountdown = countdown;
            racePB = long.MaxValue;

            chatMsgWriter.Flush();
            chatMsgWriter.Write((byte)LobbyChatOp.RaceCall);
            chatMsgWriter.Write(raceLevel.levelID);
            chatMsgWriter.Write(time);
            chatMsgWriter.Write(leniency);
            chatMsgWriter.Write(countdown);
            SendLobbyMsg();

            SteamMatchmaking.SetLobbyJoinable(roomSID, false);

            ReadyUp();
        }

        public static void StopRace()
        {
            Unready(true);

            if (owner != Online.steamID)
                return;
            chatMsgWriter.Write((byte)LobbyChatOp.RaceCancel);
            SendLobbyMsg();

            SteamMatchmaking.SetLobbyJoinable(roomSID, true);
        }

        public static void ReadyUp()
        {
            raceSent = false;
            raceAccepted = true;
            SuperRestart.ForceStagingNextRestart();
            game.PlayLevel(raceLevel, true);
        }

        public static void Unready(bool fromStop = false)
        {
            isRacing = false;
            raceJustStopped = false;
            raceSent = false;
            raceAccepted = false;
            raceWinnered = true;
            raceHappening = false;
            if (!fromStop)
            {
                chatMsgWriter.Write((byte)LobbyChatOp.RaceUnready);
                SendLobbyMsg();
            }
            else
                raceTime = -1;
        }

        static void OnStageStartRace()
        {
            if (isRacing)
            {
                raceWinnered = false;
                if (ForceCountdown)
                    raceAccepted = false;
                RaceSidebar.shown = false;
                leniencyChecked = false;
                MainMenu.Instance().PauseGameNoStateChange(false);
            }
        }

        static public bool ForceCountdown { get { return raceAccepted && isRacing; } }
        static float countdownStorage = -1f;

        #endregion ExposedRace

        // !!!!!!!!!  STEAM HANDLING  !!!!!!!!!

        #region Steam

        static Callback<LobbyCreated_t> lobbyCreatedCB;
        static Callback<LobbyMatchList_t> lobbyListCB;
        static Callback<LobbyEnter_t> lobbyEnterCB;
        static Callback<LobbyDataUpdate_t> lobbyDataUpdateCB;
        static Callback<LobbyChatUpdate_t> lobbyChatUpdateCB;
        static Callback<LobbyChatMsg_t> lobbyChatMSGCB;
        static Callback<GameLobbyJoinRequested_t> lobbyJoinReqCB;

        static Callback<P2PSessionRequest_t> P2PreqCB;

        public static readonly Dictionary<CSteamID, float> invited = [];

        public static CSteamID roomSID;
        static string roomName;
        public static ushort id;
        public static ushort secret;
        static CSteamID owner;
        internal static bool connected = false;

        public static LevelData raceLevel;
        public static double raceTime = -1;
        static int raceLeniency;
        public static bool raceAccepted;
        static float raceCountdown;
        static bool raceSent;
        static long racePB = long.MaxValue;
        static long raceLastFinish = long.MaxValue;
        public static bool raceWinnered = true;
        static bool raceHappening = false;

        public class User
        {
            public CSteamID steamID;
            public LevelData level;

            public bool attempted;
            public bool responded;
            public bool accepted;

            public GhostsManager.PlayerGhostPlayback ghost;
            public bool ghostVisible = true;
            public bool tagVisible = true;
            public bool syncedG = true;
            public bool syncedT = true;

            public bool raceReady;
            public string raceError = null;
            public long racePB;
            public bool racing;
        }

        public static readonly Dictionary<ulong, User> inRoom = [];

        static bool roomSearch = false;

        static void OnLobbyCreated(LobbyCreated_t lobby)
        {
            NeonNetwork.Logger.DebugMsg($"OnLobbyCreated {lobby.m_ulSteamIDLobby} {lobby.m_eResult}");

            Popup.Finish();
            if (lobby.m_eResult != EResult.k_EResultOK)
            {
                Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_ERROR", 10, (text, _) => text.color = Status.Colors.error);
                return;
            }

            roomSID = (CSteamID)lobby.m_ulSteamIDLobby;
            owner = autoroom ? (CSteamID)0 : Online.steamID;
            connected = true;
            pingTimer = 0;
            isPrivate = !autoroom;
            SteamMatchmaking.SetLobbyData(roomSID, "name", roomName);
            SteamMatchmaking.SetLobbyData(roomSID, "ownername", SteamFriends.GetPersonaName());
            SteamMatchmaking.SetLobbyData(roomSID, "version", NETCODE_VERSION.ToString());
            if (autoroom)
            {
                SteamMatchmaking.SetLobbyData(roomSID, "public", "y");
                SidePanel.attention = true;
            }
            GenerateID();

            if (autoroom)
                Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_JOIN_SELF");
            else
            {
                GUIUtility.systemCopyBuffer = EncodeID(id);
                if (isPrivate)
                    GUIUtility.systemCopyBuffer += EncodeID(secret);
                Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_CREATED", 10, pairs: [new("{0}", "NeonNetwork/ROOMS_NOTIF_IDCOPIED")]);
            }
            if (autoroom)
                SidePanel.LoadContent<RoomVisit>("RoomVisit", "NeonNetwork/ROOMS_AUTOROOM");
            else
                SidePanel.LoadContent<RoomHost>("RoomHost", roomName);

            PreLevelSetup(game.GetCurrentLevel());
        }

        static void GenerateID()
        {
            byte[] buffer = new byte[2];
            id = 0;
            secret = 0;
            var rng = new System.Random();
            while (id == 0)
            {
                rng.NextBytes(buffer);
                id = BitConverter.ToUInt16(buffer, 0);
            }
            while (secret == 0)
            {
                rng.NextBytes(buffer);
                secret = BitConverter.ToUInt16(buffer, 0);
            }
            SteamMatchmaking.SetLobbyData(roomSID, "id", EncodeID(id));
        }

        static void OnListCallback(LobbyMatchList_t list)
        {
            NeonNetwork.Logger.DebugMsg($"[STEAM] OnListCallback {list.m_nLobbiesMatching}");

            if (autoroom)
            {
                if (list.m_nLobbiesMatching == 0)
                {
                    CreateRoom(AUTOROOM_NAME, 250);
                    owner = (CSteamID)0;
                }
                else
                    SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
            }
            else if (roomSearch)
            {
                Popup.Finish();
                if (list.m_nLobbiesMatching == 0)
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_NOROOM", 10);
                else
                {
                    var sid = SteamMatchmaking.GetLobbyByIndex(0);
                    if (SteamMatchmaking.GetLobbyData(sid, "name") == AUTOROOM_NAME)
                        autoroom = true;
                    // try joining it and await a response from the owner to know if we're ok to go
                    SteamMatchmaking.JoinLobby(sid);
                }
            }
            else if (RoomsList.instance)
            {
                RoomsList.RoomView autoroom = null;
                foreach (Transform t in RoomsList.instance.scrollContent.transform)
                {
                    if (long.TryParse(t.name, out _))
                        UnityEngine.Object.Destroy(t.gameObject);
                }

                for (int i = 0; i < list.m_nLobbiesMatching; ++i)
                {
                    var id = SteamMatchmaking.GetLobbyByIndex(i);
                    var room = RoomsList.AddRoom(id.m_SteamID);
                    if (SteamMatchmaking.GetLobbyData(id, "name") == AUTOROOM_NAME)
                    {
                        autoroom = room;
                        room.SetAutoroom(id.m_SteamID);
                    }
                }

                if (autoroom == null)
                {
                    autoroom = RoomsList.AddRoom(0);
                    autoroom.SetAutoroom(0);
                }
                autoroom.transform.SetAsFirstSibling();
                RoomsList.instance.Ready();
            }
        }

        static void SendLobbyMsg()
        {
            var arr = chatMsgWStream.ToArray();
            NeonNetwork.Logger.DebugMsg($"[STEAM] Sending opcode {arr[0]}");
            SteamMatchmaking.SendLobbyChatMsg(roomSID, arr, arr.Length);
            chatMsgWStream.SetLength(0);
        }

        static void OnLobbyEnter(LobbyEnter_t lobby)
        {
            NeonNetwork.Logger.DebugMsg($"[STEAM] OnLobbyEnter {lobby.m_bLocked} {lobby.m_EChatRoomEnterResponse} {lobby.m_rgfChatPermissions}");

            if (lobby.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                if (autoroom)
                    JoinAutoroom(0);
                else
                {
                    Popup.Finish();
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_ERROR_JOIN", 10, (text, _) => text.color = Status.Colors.error);
                }
                return;
            }


            roomSID = (CSteamID)lobby.m_ulSteamIDLobby;
            owner = autoroom ? (CSteamID)0 : SteamMatchmaking.GetLobbyOwner(roomSID);
            roomName = SteamMatchmaking.GetLobbyData(roomSID, "name");
            inRoom.Clear();
            invited.Clear();

            NeonNetwork.Logger.Msg($"[STEAM] Connected to lobby {roomName}! Owner: {owner}");

            selfUser.level = game.GetCurrentLevel();
            RoomBase.AddUser(selfUser);

            if (autoroom)
            {
                isPrivate = false;

                chatMsgWriter.Write((byte)LobbyChatOp.Welcome);
                chatMsgWriter.Write(Online.steamID.m_SteamID);
                SendLobbyMsg();
            }
        }

        static void OnDataUpdate(LobbyDataUpdate_t update)
        {
            NeonNetwork.Logger.DebugMsg($"[STEAM] OnDataUpdate {update.m_ulSteamIDMember} {update.m_bSuccess}");

            if (inRoom.ContainsKey(update.m_ulSteamIDMember))
                inRoom[update.m_ulSteamIDMember].ghost?.SetColor();
        }

        static void OnChatUpdate(LobbyChatUpdate_t update)
        {
            NeonNetwork.Logger.DebugMsg($"[STEAM] OnChatUpdate {update.m_ulSteamIDUserChanged} {update.m_ulSteamIDMakingChange} {update.m_rgfChatMemberStateChange}");
            
            var member = (CSteamID)update.m_ulSteamIDUserChanged;

            switch ((EChatMemberStateChange)update.m_rgfChatMemberStateChange)
            {
                case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                    if (owner != Online.steamID)
                        return;
                    if (invited.ContainsKey(member) && invited[member] >= 0)
                    {
                        chatMsgWriter.Write((byte)(isPrivate ? LobbyChatOp.WelcomeP : LobbyChatOp.Welcome));
                        chatMsgWriter.Write(member.m_SteamID);
                    }
                    else if (isPrivate)
                        chatMsgWriter.Write((byte)LobbyChatOp.AskSecret);
                    else
                    {
                        chatMsgWriter.Write((byte)LobbyChatOp.Welcome);
                        chatMsgWriter.Write(member.m_SteamID);
                    }
                    SendLobbyMsg();
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeLeft:
                    if (owner == member)
                    {
                        // room is being disbanded
                        LeaveRoom(true);
                        Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_DISBAND", 10);
                    }
                    else if (inRoom.ContainsKey(member.m_SteamID))
                    {
                        RoomBase.RemoveUser(inRoom[member.m_SteamID]);
                        GhostsManager.Remove(inRoom[member.m_SteamID]);
                        inRoom.Remove(member.m_SteamID);
                        var raceuser = RaceSidebar.GetUser(member.m_SteamID);
                        if (raceuser)
                        {
                            UnityEngine.Object.Destroy(raceuser.gameObject);

                            // welllll uh
                            if (!raceWinnered && raceHappening && !isRacing && inRoom.Values.Where(x => x.racing).All(user => !user.raceReady))
                                FinishRace();

                            if (!isRacing && raceAccepted && inRoom.Values.All(user => user.raceReady))
                                StartRace();
                        }
                        // Status.ShowStatus($"{GetName(member.m_SteamID)} has left the room.");
                        SteamNetworking.CloseP2PSessionWithUser(member);
                        SetRP();
                    }
                    break;
            }
        }

        static void OnLobbyJoinReq(GameLobbyJoinRequested_t joinReq)
        {
            var sid = joinReq.m_steamIDLobby;

            TextButtons.Show(connected ? "NeonNetwork/ROOMS_WARN_JOINSTEAM_INONE" : "NeonNetwork/ROOMS_WARN_JOINSTEAM",
                (popup, _) =>
                {
                    popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                    {
                        popup.Leave();
                        if (connected)
                        {
                            LeaveRoom();
                            RoomBase.instance = null;
                        }
                        SteamMatchmaking.JoinLobby(sid);
                    });
                    popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                }
            );
        }

        static void MemberJoin(CSteamID steamID)
        {
            var level = game.GetGameData().GetLevelData(SteamMatchmaking.GetLobbyMemberData(roomSID, steamID, "level"));
            var u = new User { steamID = steamID, level = level };
            inRoom.Add(steamID.m_SteamID, u);
            GhostsManager.Add(u);
            RoomBase.AddUser(u);
        }

        static void StartRace(bool halfSetup = false)
        {
            inRoom.Values.Do(x =>
            {
                x.racing = true;
                RoomBase.GetUser(x).SetLocation();
            });
            raceHappening = true;
            isRacing = true;
            if (!halfSetup)
            {
                SuperRestart.ForceStagingNextRestart();
                game.CancelLevelSetup();
                game.PlayLevel(raceLevel, true);
            }

            SetRP();

            raceGradient = raceLeniency == 0 ? Color.red : new Color(0.969f, 0.459f, 0);

            if (owner == Online.steamID)
            {
                SteamMatchmaking.SetLobbyJoinable(roomSID, true);
                SteamMatchmaking.SetLobbyData(roomSID, "racing", "y");
            }
        }
        static void FinishRace()
        {
            raceWinnered = true;
            raceHappening = false;
            var winner = inRoom.Values.Where(x => x.racing).OrderBy(user => user.racePB).DefaultIfEmpty(null).First();
            if (winner == null)
                return;
            if (winner.racePB > racePB)
                Status.ShowStatus("NeonNetwork/RACE_NOTIF_WINNER_SELF", 10);
            else
                Status.ShowStatus("NeonNetwork/RACE_NOTIF_WINNER_OTHER", 10, pairs: [new("{0}", Online.GetName(winner.steamID.m_SteamID), false)]);
            inRoom.Values.Do(x => x.racing = false);
            SetRP();

            if (owner == Online.steamID)
                SteamMatchmaking.SetLobbyData(roomSID, "racing", "n");
        }

        enum LobbyChatOp
        {
            // meta
            Level = 0x00,
            Public = 0x01,
            Private = 0x02,
            Ping = 0x08,
            // login
            Welcome = 0x80,
            TrySecret = 0x81,

            WelcomeP = 0x89,
            AskSecret = 0x88,

            Leave = 0x8E,
            Kick = 0x8F,
            // races
            RaceCall = 0x10,
            RaceReady = 0x11,
            RaceUnready = 0x12,
            RaceError = 0x17,
            RacePB = 0x18,
            RaceLastRun = 0x1E,
            RaceCancel = 0x1F,

            TextChat = 0x40,
        }

        const int CHAT_BUFFER_SIZE = 4096;
        static readonly byte[] chatMsgBuffer = new byte[CHAT_BUFFER_SIZE];
        static readonly BinaryReader chatMsgReader = new(new MemoryStream(chatMsgBuffer, false));
        static readonly MemoryStream chatMsgWStream = new(CHAT_BUFFER_SIZE);
        static readonly BinaryWriter chatMsgWriter = new(chatMsgWStream);

        static void OnChatMsg(LobbyChatMsg_t msg)
        {
            NeonNetwork.Logger.DebugMsg($"[STEAM] OnChatMsg {msg.m_ulSteamIDUser} {msg.m_iChatID} {msg.m_eChatEntryType}");

            int size = SteamMatchmaking.GetLobbyChatEntry(roomSID, (int)msg.m_iChatID, out var sentID, chatMsgBuffer, CHAT_BUFFER_SIZE, out _);

            bool fromOwner = owner.m_SteamID == sentID.m_SteamID;
            bool fromSelf = Online.steamID.m_SteamID == sentID.m_SteamID;

            chatMsgReader.BaseStream.Position = 0;

            var opcode = (LobbyChatOp)chatMsgReader.ReadByte();

            NeonNetwork.Logger.DebugMsg($"Opcode {opcode}");

            switch (opcode)
            {
                #region Meta
                case LobbyChatOp.Level:
                    {
                        if (fromSelf)
                            break;
                        var level = game.GetGameData().GetLevelData(chatMsgReader.ReadString());
                        var same = level == game.GetCurrentLevel();
                        var u = inRoom[sentID.m_SteamID];
                        u.attempted = false;
                        u.responded = false;
                        u.level = level;

                        var userview = RoomBase.GetUser(u);
                        if (userview)
                            userview.SetLocation();

                        if (!same)
                            GhostsManager.Hide(u);
                    }

                    break;
                case LobbyChatOp.Public: // become public
                    SetRP();
                    if (!fromOwner || fromSelf)
                        return;
                    isPrivate = false;
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_PUBLIC_GUEST");
                    id = chatMsgReader.ReadUInt16();
                    break;
                case LobbyChatOp.Private: // become private
                    SetRP();
                    if (!fromOwner || fromSelf)
                        return;
                    isPrivate = true;
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_PRIVATE_GUEST");
                    id = chatMsgReader.ReadUInt16();
                    secret = chatMsgReader.ReadUInt16();
                    break;

                case LobbyChatOp.Ping:
                    if (!fromSelf)
                    {
                        byte[] frameBytes = [0x80];
                        SteamNetworking.SendP2PPacket(sentID, frameBytes, 1, EP2PSend.k_EP2PSendReliable);
                    }
                    break;

                #endregion
                #region Login
                case LobbyChatOp.Welcome: // welcome in
                case LobbyChatOp.WelcomeP:
                    if (!fromOwner && !autoroom)
                        break;
                    ulong newUID = chatMsgReader.ReadUInt64();
                    if (newUID != Online.steamID.m_SteamID)
                    {
                        MemberJoin((CSteamID)newUID);
                        Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_JOIN_OTHER", pairs: [new("{0}", Online.GetName(newUID), false)]);
                        SetRP();
                    }
                    else if (!connected)
                    {
                        id = (ushort)DecodeID(SteamMatchmaking.GetLobbyData(roomSID, "id"));
                        connected = true;
                        pingTimer = 0;
                        isPrivate = opcode == LobbyChatOp.WelcomeP;
                        SetRP();
                        Popup.Finish();

                        SidePanel.LoadContent<RoomVisit>("RoomVisit", autoroom ? "NeonNetwork/ROOMS_AUTOROOM" : roomName);

                        var racing = SteamMatchmaking.GetLobbyData(roomSID, "racing") == "y";
                        Status.ShowStatus(racing ? "NeonNetwork/RACE_NOTIF_MIDJOIN" : "NeonNetwork/ROOMS_NOTIF_JOIN_SELF");
                        if (autoroom)
                            SidePanel.attention = true;

                        var count = SteamMatchmaking.GetNumLobbyMembers(roomSID);
                        NeonNetwork.Logger.DebugMsg($"count {count}");
                        for (int i = 0; i < count; ++i)
                        {
                            var member = SteamMatchmaking.GetLobbyMemberByIndex(roomSID, i);
                            if (member != Online.steamID)
                                MemberJoin(member);
                        }

                        PreLevelSetup(game.GetCurrentLevel());
                    }

                    break;
                case LobbyChatOp.Kick: // kick
                    if (!fromOwner)
                        break;
                    ulong UID = chatMsgReader.ReadUInt64();
                    if (!inRoom.ContainsKey(UID))
                    {
                        if (UID == Online.steamID.m_SteamID)
                        {
                            // uhhhhhhhhhhh uhoh
                            if (!connected)
                            {
                                Popup.Finish();
                                Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_WANTPRIVATE", 10, (text, _) => text.color = Status.Colors.error);
                            }
                            else
                            {
                                LeaveRoom(true);
                                Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_KICKED_SELF", 10, (text, _) => text.color = Status.Colors.error);
                            }
                        }
                    }
                    // inRoom.Remove(UID);
                    // SteamNetworking.CloseP2PSessionWithUser((CSteamID)UID);
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_KICKED_OTHER", pairs: [new("{0}", Online.GetName(UID), false)]);
                    break;
                case LobbyChatOp.AskSecret: // secret requestd
                    if (!fromOwner || connected)
                        break;
                    if (secret == 0)
                    {
                        SteamMatchmaking.LeaveLobby(roomSID);
                        Popup.Finish();
                        Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_WANTPRIVATE", 10, (text, _) => text.color = Status.Colors.error);
                    }
                    else
                    {
                        isPrivate = true;
                        chatMsgWriter.Write((byte)LobbyChatOp.TrySecret);
                        chatMsgWriter.Write(secret);
                        SendLobbyMsg();
                    }
                    break;
                case LobbyChatOp.TrySecret: // secret response
                    if (owner == Online.steamID)
                    {
                        if (secret == chatMsgReader.ReadUInt16())
                        {
                            // ur good welcome to the team
                            chatMsgWriter.Write((byte)LobbyChatOp.WelcomeP);
                            chatMsgWriter.Write(msg.m_ulSteamIDUser);
                            SendLobbyMsg();
                        }
                        else
                        {
                            // NAHHH bro get tf outta here
                            chatMsgWriter.Write((byte)LobbyChatOp.Kick);
                            chatMsgWriter.Write(msg.m_ulSteamIDUser);
                            SendLobbyMsg();
                        }
                    }
                    break;
                case LobbyChatOp.Leave: // natural leave **ALL THIS DOES IS NOTIFY THE USER** THIS DOES NOT PERFORM A LEAVE
                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_LEFT_OTHER", pairs: [new("{0}", Online.GetName(sentID.m_SteamID), false)]);
                    break;
                #endregion
                #region Races
                case LobbyChatOp.RaceCall: // start
                    {
                        if (!fromOwner || fromSelf)
                            return;

                        raceLevel = game.GetGameData().GetLevelData(chatMsgReader.ReadString());
                        if (!raceLevel)
                        {
                            Status.ShowStatus("NeonNetwork/RACE_NOTIF_NULLEVEL", 10, (text, _) => text.color = Status.Colors.error);
                            chatMsgWriter.Write((byte)LobbyChatOp.RaceError); // we can't :( custom level we don't have, prolly
                            chatMsgWriter.Write("NeonNetwork/RACE_ERROR_NULLEVEL");
                            SendLobbyMsg();
                            return;
                        }
                        raceTime = chatMsgReader.ReadSingle();
                        raceLeniency = chatMsgReader.ReadInt32();
                        raceCountdown = chatMsgReader.ReadInt32();
                        SidePanel.attention = true;
                        RaceSidebar.Clear();
                        SetRP();

                        string localized = LocalizationManager.GetTranslation(raceLevel.GetLevelDisplayName());
                        if (string.IsNullOrEmpty(localized))
                            localized = raceLevel.levelDisplayName;

                        string min = string.Format("{0:0.##}", raceTime / 60);
                        Status.ShowStatus("NeonNetwork/RACE_NOTIF_STARTRACE", 10, pairs: [new("{0}", localized, false), new("{1}", min, false)]);

                        break;
                    }
                case LobbyChatOp.RaceReady: // ready
                    {
                        User u;
                        if (fromSelf)
                            u = selfUser;
                        else
                            u = inRoom[sentID.m_SteamID];

                        u.raceReady = true;
                        u.raceError = null;

                        if (!fromSelf && raceAccepted && inRoom.Values.All(user => user.raceReady))
                            StartRace();

                        break;
                    }
                case LobbyChatOp.RaceUnready: // unready
                    {
                        User u;
                        if (fromSelf)
                            u = selfUser;
                        else
                            u = inRoom[sentID.m_SteamID];

                        u.raceReady = false;

                        break;
                    }
                case LobbyChatOp.RaceError: // erm..
                    {
                        User u;
                        if (fromSelf)
                            u = selfUser;
                        else
                            u = inRoom[sentID.m_SteamID];
                        u.raceReady = false;
                        u.raceError = chatMsgReader.ReadString();

                        RoomBase.GetUser(u).SetLocation();

                        break;
                    }

                case LobbyChatOp.RacePB: // new PB
                    {
                        if (!raceHappening || fromSelf)
                            return;

                        var u = inRoom[sentID.m_SteamID];
                        u.racePB = chatMsgReader.ReadInt64();
                        RaceSidebar.SetTime(sentID.m_SteamID, u.racePB);

                        break;
                    }

                case LobbyChatOp.RaceLastRun: // last run finished
                    {
                        if (!raceHappening)
                            return;

                        if (!fromSelf)
                        {
                            var u = inRoom[sentID.m_SteamID];
                            u.raceReady = false;
                            u.racePB = chatMsgReader.ReadInt64();

                            RaceSidebar.SetTime(sentID.m_SteamID, u.racePB);
                            RaceSidebar.GetUser(sentID.m_SteamID).LockIn();
                        }

                        if (!isRacing && inRoom.Values.Where(x => x.racing).All(user => !user.raceReady))
                            FinishRace();

                        break;
                    }

                case LobbyChatOp.RaceCancel: // race is called off 
                    {
                        foreach (var user in inRoom.Values.AddItem(selfUser))
                        {
                            user.raceReady = false;
                            user.raceError = null;

                            var listuser = RoomBase.GetUser(user);
                            if (listuser)
                                listuser.SetLocation();
                        }

                        if (!fromOwner || fromSelf)
                            return;

                        Unready(true);
                        if (raceLevel && game.GetCurrentLevel() == raceLevel)
                        {
                            game.CancelLevelSetup();
                            game.PlayLevel(raceLevel, true, true);
                        }
                        Status.ShowStatus("NeonNetwork/RACE_NOTIF_STOPRACE", 10);

                        break;
                    }
                    #endregion
            }
        }

        static void OnP2PReq(P2PSessionRequest_t pReq)
        {
            var user = inRoom.Values
                .DefaultIfEmpty(null)
                .FirstOrDefault(user => user.steamID == pReq.m_steamIDRemote);

            if (user == null)
                return;

            SteamNetworking.AcceptP2PSessionWithUser(pReq.m_steamIDRemote);
            user.accepted = true;
            user.attempted = WriteCurrentFrame(user.steamID);
        }

        static readonly MemoryStream ghostWStream = new((int)GhostFrameHandler.SIZEOF);
        static readonly BinaryWriter ghostWriter = new(ghostWStream);

        public static bool WriteCurrentFrame(CSteamID sID)
        {
            if (lastFrame != null)
            {
                var t = lastFrame.time;
                if (lastFrameT == DateTime.MinValue)
                {
                    lastFrameT = DateTime.UtcNow;
                    lastFrame.time = 0;
                }
                else
                {
                    var now = DateTime.UtcNow;
                    lastFrame.time = (float)((now - lastFrameT).TotalSeconds);
                    lastFrameT = now;
                }

                ghostWStream.SetLength(0);
                GhostFrameHandler.ToWriter(lastFrame, ghostWriter);
                byte[] frameBytes = [1, .. ghostWStream.GetBuffer()];

                SteamNetworking.SendP2PPacket(sID, frameBytes, 1 + GhostFrameHandler.SIZEOF, EP2PSend.k_EP2PSendReliable);
                //NeonNetwork.Logger.DebugMsg($"Sending frame to {sID}");

                lastFrame.time = t;
                return true;
            }
            else
            {
                NeonNetwork.Logger.DebugMsg($"No frame");
                return false;
            }
        }
        #endregion Steam

        // !!!!!!!!! ROOM ID HANDLING !!!!!!!!!
        #region RoomID

        public static readonly string IDchars = "EARIOTNSLCUDPMHGK";
        public static readonly uint IDbase = 17;

        public static string EncodeID(uint id)
        {
            StringBuilder result = new();

            while (id > 0)
            {
                result.Append(IDchars[(int)(id % IDbase)]);
                id /= IDbase;
            }

            return result.ToString().PadRight(4, IDchars[0]);
        }

        public static uint DecodeID(string id)
        {
            uint result = 0;

            foreach (char c in id.ToCharArray().Reverse())
            {
                result *= IDbase;
                var dec = IDchars.IndexOf(c);
                if (dec == -1)
                    return 0;
                result += (uint)dec;
            }
            return result;
        }

        #endregion RoomID

        // !!!!!!!!!     PATCHES      !!!!!!!!!
        #region Ghosts

        public static class GhostFrameHandler
        {
            class CloneableGhostFrame : GhostFrame
            {
                public GhostFrame Clone() => (GhostFrame)MemberwiseClone();
            }

            public const uint SIZEOF = sizeof(float) * 6 + sizeof(int) * 2 + sizeof(bool) * 3; 

            static readonly CloneableGhostFrame buffer = new();

            public static GhostFrame FromReader(BinaryReader reader)
            {
                buffer.index = reader.ReadInt32(); // 4
                buffer.triggerEvent = reader.ReadInt32(); // 4
                //buffer.playShotAnimation = reader.ReadBoolean(); // 1
                buffer.time = reader.ReadSingle(); // 4

                buffer.pos.x = reader.ReadSingle(); // 4
                buffer.pos.y = reader.ReadSingle(); // 4
                buffer.pos.z = reader.ReadSingle(); // 4

                buffer.angle = reader.ReadSingle(); // 4
                buffer.cameraPitch = reader.ReadSingle(); // 4
                buffer.grounded = reader.ReadBoolean(); // 1
                buffer.stomping = reader.ReadBoolean(); // 1
                buffer.zipLining = reader.ReadBoolean(); // 1
                //buffer.bulletId = reader.ReadInt32();
                return buffer.Clone();
            }

            public static void ToWriter(GhostFrame frame, BinaryWriter writer)
            {
                writer.Write(frame.index);
                writer.Write(frame.triggerEvent);
                //writer.Write(frame.playShotAnimation);
                writer.Write(frame.time);

                writer.Write(frame.pos.x);
                writer.Write(frame.pos.y);
                writer.Write(frame.pos.z);

                writer.Write(frame.angle);
                writer.Write(frame.cameraPitch);
                writer.Write(frame.grounded);
                writer.Write(frame.stomping);
                writer.Write(frame.zipLining);
                //writer.Write(frame.bulletId);
            }
        }

        static bool importantUp = false;
        static bool firstFrame = false;
        static int usedFrame = 0;
        static GhostFrame lastFrame = null;
        public static int fakeIndex = 0;
        public static DateTime lastFrameT = DateTime.MinValue;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GhostRecorder), "RecordDiscardAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordFireAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordJumpAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordLand")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordBulletHit")]
        static void UploadFrame(GhostFrame[] ___m_recordingFrames, bool ___m_dontRecord, ref int ___m_recordingIndex)
        {
            if (!connected || ___m_recordingFrames == null)
                return;
            lastFrame = ___m_recordingFrames[usedFrame];
            if (lastFrame == null)
                return;

            var t = lastFrame.time;
            if (lastFrameT == DateTime.MinValue)
            {
                lastFrameT = DateTime.UtcNow;
                lastFrame.time = 0;
            }
            else
            {
                var now = DateTime.UtcNow;
                lastFrame.time = (float)((now - lastFrameT).TotalSeconds);
                lastFrameT = now;
            }
            lastFrame.index = fakeIndex++;

            if (___m_dontRecord)
                --___m_recordingIndex;
            //if (NeonNetwork.DEBUG)
            //    NeonNetwork.Logger.Msg($"u{importantUp} t{lastFrame.time} f{usedFrame} c{___m_recordingFrames.Length}");

            Helpers.StartProfiling("UploadFrame");

            ghostWStream.SetLength(0);
            GhostFrameHandler.ToWriter(lastFrame, ghostWriter);
            byte[] frameBytes = [0, .. ghostWStream.GetBuffer()];

            var users = inRoom.Values.Where(user =>
            {
                return user.level == game.GetCurrentLevel() && (importantUp || user.responded || !user.attempted);
            });

            foreach (var user in Helpers.ProfileLoop(users, "Upload to Users"))
            {
                user.attempted = true;
                user.accepted = true; // we don't actually know yet but this is so we actually check it, *we've* accepted them
                frameBytes[0] = (byte)((firstFrame || !user.attempted) ? 1 : (importantUp ? 2 : 0));
                SteamNetworking.SendP2PPacket(user.steamID, frameBytes, 1 + GhostFrameHandler.SIZEOF, (importantUp || !user.attempted) ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay);
            }

            lastFrame.time = t;
            if (importantUp && !firstFrame)
            {
                lastFrame = null;
                lastFrameT = DateTime.MinValue;
            }

            firstFrame = false;
            importantUp = false;

            Helpers.EndProfiling("UploadFrame");
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GhostRecorder), "LateUpdate")]
        [HarmonyPatch(typeof(GhostRecorder), "OnDestroy")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordDiscardAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordFireAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordJumpAbility")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordBulletHit")]
        [HarmonyPatch(typeof(GhostRecorder), "RecordLand")]
        static IEnumerable<CodeInstruction> SkipDontRecord(IEnumerable<CodeInstruction> instructions)
        {
            bool hit = false;

            yield return new(OpCodes.Ldarg_0);
            yield return new(OpCodes.Ldfld, NeonLite.Helpers.Field(typeof(GhostRecorder), "m_recordingIndex"));
            yield return new(OpCodes.Stsfld, NeonLite.Helpers.Field(typeof(Rooms), "usedFrame"));

            foreach (var code in instructions)
            {
                if (!hit)
                {
                    if (code.opcode == OpCodes.Brfalse || code.opcode == OpCodes.Brfalse_S)
                    {
                        hit = true;
                        yield return new(OpCodes.Pop);
                        yield return new(OpCodes.Br, (Label)code.operand);
                    }
                    else if (code.opcode == OpCodes.Brtrue || code.opcode == OpCodes.Brtrue_S)
                    {
                        hit = true;
                        yield return new(OpCodes.Pop);
                    }
                    else
                        yield return code;
                }
                else
                    yield return code;
            }
        }

        //[HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SkipDontRecordL(IEnumerable<CodeInstruction> instructions)
        {
            int hit = 0;
            Label? label = null;
            foreach (var code in instructions)
            {
                if (code.Branches(out label) && (++hit == 2))
                    break;
            }

            yield return new(OpCodes.Ldarg_0);
            yield return new(OpCodes.Ldfld, NeonLite.Helpers.Field(typeof(GhostRecorder), "m_recordingIndex"));
            yield return new(OpCodes.Stsfld, NeonLite.Helpers.Field(typeof(Rooms), "usedFrame"));
            yield return new(OpCodes.Br, label.Value);

            foreach (var code in instructions)
                yield return code;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GhostRecorder), "LateUpdate")]
        [HarmonyPatch(typeof(GhostRecorder), "SaveLevelData")]
        static IEnumerable<CodeInstruction> UploadInject(IEnumerable<CodeInstruction> instructions)
        {
            var method = NeonLite.Helpers.Method(typeof(GhostRecorder), "CreateBaseFrame");
            foreach (var (code, i) in instructions.Select((value, i) => (value, i)))
            {
                if (code.Calls(method))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldfld, NeonLite.Helpers.Field(typeof(GhostRecorder), "m_recordingIndex"));
                    yield return new(OpCodes.Stsfld, NeonLite.Helpers.Field(typeof(Rooms), "usedFrame")); // store the frame b4 
                }
                yield return code;
                if (code.Calls(method))
                {
                    if (i + 2 < instructions.Count())
                    {
                        // this is SaveLevelData mark that shit as important
                        yield return new(OpCodes.Ldc_I4_1);
                        yield return new(OpCodes.Stsfld, NeonLite.Helpers.Field(typeof(Rooms), "importantUp"));
                    }
                    yield return new(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(typeof(GhostRecorder), "m_recordingFrames");
                    yield return new(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(typeof(GhostRecorder), "m_dontRecord");
                    yield return new(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(typeof(GhostRecorder), "m_recordingIndex", true);
                    yield return new(OpCodes.Call, NeonLite.Helpers.Method(typeof(Rooms), "UploadFrame"));
                }
            }
        }

        static void MaybeSaveCompressed(this GhostRecorder record, bool dontRecord)
        {
            if (!LevelRush.IsLevelRush() && !dontRecord)
                record.SaveCompressed();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GhostRecorder), "SaveLevelData")]
        static IEnumerable<CodeInstruction> SkipLRBranch(IEnumerable<CodeInstruction> instructions)
        {
            bool hit = false;
            var method = NeonLite.Helpers.Method(typeof(GhostRecorder), "SaveCompressed");
            foreach (var code in instructions)
            {
                if (!hit)
                {
                    if (code.opcode == OpCodes.Ret)
                        hit = true;
                    continue;
                }
                if (code.Calls(method))
                {
                    yield return new(OpCodes.Dup);
                    yield return CodeInstruction.LoadField(typeof(GhostRecorder), "m_dontRecord");
                    yield return new(OpCodes.Call, NeonLite.Helpers.Method(typeof(Rooms), "MaybeSaveCompressed"));
                }
                else
                    yield return code;
            }
        }

        #endregion Ghosts

        #region RacePatches
        static void SetupRaceStaging(this MenuScreenStaging staging)
        {
            var rushT = staging.levelRushDisplayHolder.transform;
            if (!isRacing)
            {
                if (rushT.localPosition.y != -8)
                    rushT.localPosition = new(0, -8, 0);
                staging.levelRushDisplayHolder.SetActive(false);
                staging.startButton_Localized.gameObject.SetActive(true);
                NeonLite.Helpers.Field(staging.GetType(), "timerMax").SetValue(staging, 3f);
                staging.levelRushDisplayHolder.transform.Find("LevelRushProgress (1)").GetComponent<AxKLocalizedText>().Localize();
                return;
            }

            staging.levelRushDisplayHolder.transform.Find("LevelRushProgress (1)").GetComponent<AxKLocalizedText>().SetKey("NeonNetwork/RACE_TITLE");
            string localized = LocalizationManager.GetTranslation(raceLevel.GetLevelDisplayName());
            staging.levelRushText.text = localized;
            var countdown = 3f;
            if (ForceCountdown)
            {
                racePB = long.MaxValue;
                raceLastFinish = long.MaxValue;
                if (countdownStorage <= 0)
                    countdownStorage = raceCountdown;
                else
                    countdownStorage = (float)NeonLite.Helpers.Field(staging.GetType(), "timerCurrent").GetValue(staging);
                countdown = countdownStorage;
                // populate the RaceSidebar
                RaceSidebar.Clear();
                RaceSidebar.AddUser(Online.steamID.m_SteamID);
                RaceSidebar.shown = true;
                foreach (var user in inRoom.Values.Where(x => x.racing))
                    RaceSidebar.AddUser(user.steamID.m_SteamID);
            }
            else if (GameDataManager.levelStats[game.GetCurrentLevel().levelID].IsNewBest())
                countdown = 5f;
            NeonLite.Helpers.Field(staging.GetType(), "timerMax").SetValue(staging, countdown);
            rushT.localPosition = new(0, staging._leaderboardsAndLevelInfoRef.transform.localPosition.y + 200, 0);
            staging.levelRushDisplayHolder.SetActive(true);
            staging.startButton_Localized.gameObject.SetActive(!ForceCountdown);
        }

        static void LBSetLevelRace(this LeaderboardsAndLevelInfo lb, LevelData level, bool fromStore, bool isNewScore, bool skipNewScoreInitalDelay, bool justRequestingScores)
        {
            if (!isRacing)
            {
                lb.SetLevel(level, fromStore, isNewScore, skipNewScoreInitalDelay, justRequestingScores);
                return;
            }
            lb.SetLevel(level, fromStore, true, skipNewScoreInitalDelay, !GameDataManager.levelStats[game.GetCurrentLevel().levelID].IsNewBest());
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MenuScreenStaging), "OnSetVisible")]
        static IEnumerable<CodeInstruction> SetupRaceStagingHook(IEnumerable<CodeInstruction> instructions)
        {
            int hits = 0;
            var rushHolder = NeonLite.Helpers.Field(typeof(MenuScreenStaging), "levelRushDisplayHolder");
            var setLevel = NeonLite.Helpers.Method(typeof(LeaderboardsAndLevelInfo), "SetLevel");
            foreach (var code in instructions)
            {
                if (code.LoadsField(rushHolder) && ++hits == 2)
                    yield return new CodeInstruction(OpCodes.Call, NeonLite.Helpers.Method(typeof(Rooms), "SetupRaceStaging")).MoveLabelsFrom(code);
                else if (hits == 2 || hits == 3)
                    hits++;
                else if (code.Calls(setLevel))
                    yield return new CodeInstruction(OpCodes.Call, NeonLite.Helpers.Method(typeof(Rooms), "LBSetLevelRace")).MoveLabelsFrom(code);
                else
                    yield return code;
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MenuScreenStaging), "Update")]
        static IEnumerable<CodeInstruction> StagingUpdate(IEnumerable<CodeInstruction> instructions)
        {
            bool hit = false;
            var getButtonUp = NeonLite.Helpers.Method(typeof(GameInput), "GetButtonUp");
            var isLevelRush = NeonLite.Helpers.Method(typeof(LevelRush), "IsLevelRush");
            foreach (var code in instructions)
            {
                yield return code;
                if (hit)
                {
                    hit = false;
                    yield return new(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rooms), "ForceCountdown"));
                    // yield return new(OpCodes.Ldc_I4_1);
                    yield return new(OpCodes.Brtrue, code.operand);
                }
                else if (code.Calls(getButtonUp))
                    hit = true;
                else if (code.Calls(isLevelRush))
                {
                    // kinda lazy
                    yield return CodeInstruction.LoadField(typeof(Rooms), "isRacing");
                    yield return new(OpCodes.Or);
                }
            }
        }

        static bool leniencyChecked = false;
        static bool raceJustStopped = false;

        internal static void SetupRace()
        {
            NeonNetwork.Logger.DebugMsg("SetupRace");

            if (raceAccepted)
            {
                if (!raceSent)
                {
                    raceSent = true;
                    chatMsgWriter.Write((byte)LobbyChatOp.RaceReady);
                    SendLobbyMsg();
                }

                if (inRoom.Values.All(user => user.raceReady))
                    StartRace(true);
            }
            else if (isRacing)
                CheckRaceFinish();
        }

        static void CheckRaceFinish()
        {
            NeonNetwork.Logger.DebugMsg($"CheckRaceFinish {racePB} {raceLastFinish} {game.GetCurrentLevelTimerMicroseconds()}");

            if (!isRacing)
                return;

            if (raceTime <= 0)
            {
                if (raceLeniency == 0)
                {
                    if (racePB > raceLastFinish)
                        racePB = raceLastFinish;
                    raceJustStopped = true;
                    isRacing = false;
                    raceTime = -1;
                    selfUser.raceReady = false;

                    chatMsgWriter.Write((byte)LobbyChatOp.RaceLastRun);
                    chatMsgWriter.Write(racePB);
                    SendLobbyMsg();
                    RaceSidebar.SetTime(Online.steamID.m_SteamID, racePB);
                    RaceSidebar.GetUser(Online.steamID.m_SteamID).LockIn();
                    RaceSidebar.shown = true;

                    foreach (var user in inRoom.Values)
                    {
                        var listuser = RoomBase.GetUser(user);
                        if (listuser)
                            listuser.SetLocation();
                    }
                }
                else if (!leniencyChecked)
                {
                    leniencyChecked = true;
                    raceLeniency--;
                }
            }
        }

        static void PlayJingles()
        {
            if (!isRacing && !raceJustStopped)
                return;
            AudioController.Play("LEVEL_COMPLETE", MainMenu.Instance().transform);
            if (GameDataManager.levelStats[game.GetCurrentLevel().levelID].IsNewBest())
                AudioController.Play("MUSIC_JINGLE_LEVEL_COMPLETE_MEDAL", MainMenu.Instance().transform);
            else if (racePB > raceLastFinish)
                AudioController.Play("MUSIC_JINGLE_LEVEL_COMPLETE_NEW_BEST", MainMenu.Instance().transform);
            else
                AudioController.Play("MUSIC_JINGLE_LEVEL_COMPLETE", MainMenu.Instance().transform);
        }

        static bool RaceIntrimHandler()
        {
            if (isRacing || raceJustStopped)
                return true;
            return LevelRush.IsLevelRush();
        }

        static void LoadRaceLevel()
        {
            if (raceTime <= 0)
                raceLeniency = 0;
            raceLastFinish = game.GetCurrentLevelTimerMicroseconds();
            PlayJingles();
            CheckRaceFinish();
            if (!isRacing)
            {
                MainMenu.Instance().SetState(MainMenu.State.Results, true, true, false, false);
                return;
            }

            if (racePB > raceLastFinish)
            {
                chatMsgWriter.Write((byte)LobbyChatOp.RacePB);
                chatMsgWriter.Write(raceLastFinish);
                SendLobbyMsg();
                RaceSidebar.SetTime(Online.steamID.m_SteamID, raceLastFinish);
                racePB = raceLastFinish;
            }

            game.PlayLevel(raceLevel, true, true);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MenuScreenResults), "LevelCompleteRoutine", MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> RaceLevelComplete(IEnumerable<CodeInstruction> instructions)
        {
            int hitLR = 0;
            var isLevelRush = NeonLite.Helpers.Method(typeof(LevelRush), "IsLevelRush");
            var intrim = NeonLite.Helpers.Method(typeof(Rooms), "RaceIntrimHandler");

            foreach (var code in instructions)
            {
                if (code.Calls(isLevelRush) && ++hitLR == 2)
                    yield return new CodeInstruction(OpCodes.Call, intrim).MoveLabelsFrom(code);
                else
                    yield return code;
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Game), "WinRoutine", MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> RaceWinPatch(IEnumerable<CodeInstruction> instructions)
        {
            int hit = 0;
            bool skip = false;

            var mainInstance = NeonLite.Helpers.Method(typeof(MainMenu), "Instance");
            var setState = NeonLite.Helpers.Method(typeof(MainMenu), "SetState");
            foreach (var code in instructions)
            {
                if (code.Calls(mainInstance) && ++hit == 4)
                {
                    yield return new CodeInstruction(OpCodes.Call, NeonLite.Helpers.Method(typeof(Rooms), "LoadRaceLevel"));
                    skip = true;
                }

                if (skip)
                {
                    skip = !code.Calls(setState);
                    continue;
                }
                yield return code;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelInfo), "SetLevel")]
        static void LevelInfoRace(ref LevelInfo __instance, ref LevelData level, bool fromStore, bool isNewScore)
        {
            if ((!isRacing && !raceJustStopped) || racePB == long.MaxValue || level.levelID != raceLevel.levelID)
                return;

            GameData gameData = Singleton<Game>.Instance.GetGameData();
            LevelStats levelStats = gameData.GetLevelStats(level.levelID);

            __instance._levelBestTime.text = NeonLite.Helpers.FormatTime(racePB / 1000, null, '.', true);

            if (fromStore || !isNewScore || !levelStats.IsNewBest())
                __instance._levelBestTimeDescription_Localized.SetKey("NeonNetwork/RACE_PB");

            int medal = Helpers.GetMedalIndexSafely(level, racePB);
            __instance._levelMedal.sprite = CommunityMedals.Medals[medal];
            CommunityMedals.AdjustMaterial(__instance._levelMedal);
        }

        static TextMeshPro raceTimer;

        static readonly Color raceWarningTimer = Color.yellow;
        static Color raceGradient;
        static bool raceFinalWarning;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerUI), "UpdateTimerText")]
        static void PostUpdateTimerText(ref TextMeshPro ___timerText)
        {
            if (!isRacing)
            {
                if (raceTimer != null)
                    GameObject.Destroy(raceTimer);
                raceFinalWarning = false;
                return;
            }
            // graciously yoinked from neonlite
            if (raceTimer == null)
            {
                var obj = Utils.InstantiateUI(___timerText.gameObject, "RaceText", ___timerText.transform.parent);
                raceTimer = obj.GetComponent<TextMeshPro>();
                var underline = raceTimer.transform.parent.Find("TimerUnderline");
                raceTimer.transform.localPosition = new(0, underline.localPosition.y - 13, 0);
            }

            var timeMS = Math.Max(0, (long)(raceTime * 1000));

            const int SECONDS_REMAIN = 30;
            if (timeMS < SECONDS_REMAIN * 1000)
            {
                if (timeMS == 0)
                {
                    raceTimer.fontStyle = FontStyles.Bold;
                    if (raceLeniency == 0 && !raceFinalWarning)
                    {
                        raceFinalWarning = true;
                        raceTimer.color = Color.red;
                    }
                }
                else
                {
                    var t = 1 - (float)raceTime / SECONDS_REMAIN;
                    raceTimer.color = Color.Lerp(raceWarningTimer, raceGradient, t);
                }
            }
            raceTimer.text = NeonLite.Helpers.FormatTime(timeMS);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonNextLevel")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonJobArchive")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonJobArchiveFromLocation")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonSidequestShop")]
        private static bool PopupOnLeave(MainMenu __instance, MethodBase __originalMethod)
        {
            if (!isRacing)
                return true;

            Warnings(() => __originalMethod.Invoke(__instance, []));

            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonReturnToHub")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonQuitLevel")]
        static IEnumerable<CodeInstruction> PopupOnLeaveT(IEnumerable<CodeInstruction> instructions)
        {
            var setPopup = NeonLite.Helpers.Method(typeof(MenuScreenPopup), "SetPopup");
            var popupOverride = NeonLite.Helpers.Method(typeof(Rooms), "PopupOnLeaveTH");

            foreach (var code in instructions)
            {
                if (code.Calls(setPopup))
                    yield return new CodeInstruction(OpCodes.Call, popupOverride).MoveLabelsFrom(code);
                else
                    yield return code;
            }
        }

        static void PopupOnLeaveTH(this MenuScreenPopup popup, string label, Action yes, Action no, Action cancel, MenuScreenPopup.Style style)
        {
            if (!isRacing)
            {
                popup.SetPopup(label, yes, no, cancel, style);
                return;
            }
            Warnings(yes);
        }

        static void Warnings(Action callback)
        {
            if (owner == Online.steamID)
            {
                TextButtons.Show($"NeonNetwork/RACE_WARN_LEAVE_HOST",
                    (popup, _) =>
                    {
                        popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                        {
                            popup.Leave();
                            StopRace();
                            Status.ShowStatus("NeonNetwork/RACE_NOTIF_STOPRACE", 10);
                            callback?.Invoke();
                        });
                        popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                    }
                );
            }
            else
            {
                TextButtons.Show($"NeonNetwork/RACE_WARN_LEAVE_GUEST",
                    (popup, _) =>
                    {
                        popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                        {
                            LeaveRoom();
                            popup.Leave();
                            callback?.Invoke();
                        });
                        popup.AddComponent<Objects.Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                    }
                );
            }

        }

        #endregion

        #region OtherPatches
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "LevelSetupRoutine")]
        static void PreLevelSetup(LevelData newLevel)
        {
            if (RaceSidebar.shown && newLevel != raceLevel)
                RaceSidebar.Clear();
            if (!connected || !newLevel)
                return;

            selfUser.level = newLevel;

            SteamMatchmaking.SetLobbyMemberData(roomSID, "level", newLevel.levelID);

            if (isRacing)
                SuperRestart.ForceStagingNextRestart();

            importantUp = true;
            firstFrame = true;
            usedFrame = 0;
            lastFrame = null;
            raceTimer = null;
            raceJustStopped = false;
            countdownStorage = -1;
            foreach (var user in inRoom.Values.Where(user => user.level == newLevel))
            {
                user.attempted = false;
                user.responded = false;
            }
            SetColor();

            var userview = RoomBase.GetUser(selfUser);
            if (userview)
                userview.SetLocation();

            chatMsgWriter.Write((byte)LobbyChatOp.Level);
            chatMsgWriter.Write(newLevel.levelID);
            SendLobbyMsg();
        }
        #endregion
    }
}
