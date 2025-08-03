using I2.Loc;
using NeonNetwork.Objects.Popups;
using NeonNetwork.Online;
using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static NeonNetwork.Objects.SidePanel.Contents.RoomBase;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class RoomBase : Contents
    {
        public static RoomBase instance;
        UserView userCopy;
        Transform scrollContent;

        public static HashSet<Rooms.User> preList = [];

        bool allGV = true;
        bool allNV = true;

        RawImage nametag;
        RawImage visible;

        TextMeshProUGUI roomCode;
        AxKLocalizedText roomCLoc;
        protected Popups.Components.Button inviteButton;

        bool wasPrivate;

        protected void Start()
        {
            visible = transform.Find("Header").Find("Vis").GetComponent<RawImage>();
            nametag = transform.Find("Header").Find("Tag").GetComponent<RawImage>();
            roomCode = transform.Find("Header").Find("RoomCode").GetComponent<TextMeshProUGUI>();
            roomCLoc = Localization.Setup(roomCode, pairs: [new("{0}", "••••", false)]);
            wasPrivate = true;

            instance = this;
            scrollContent = transform.Find("Scroll View").GetComponent<ScrollRect>().content;
            userCopy = scrollContent.GetChild(0).GetOrAddComponent<UserView>();
            userCopy.gameObject.SetActive(false);
            foreach (var id in preList)
                AddUser(id);
            preList.Clear();

            visible.GetComponent<Button>().onClick.AddListener(() =>
            {
                allGV = !allGV;
                foreach (UserView u in transform.GetComponentsInChildren<UserView>())
                {
                    u.user.syncedG = true;
                }
            });
            nametag.GetComponent<Button>().onClick.AddListener(() =>
            {
                allNV = !allNV;
                foreach (UserView u in transform.GetComponentsInChildren<UserView>())
                {
                    u.user.syncedT = true;
                }
            });

        }

        protected void Update()
        {
            inviteButton.ShouldBeInteractable = Rooms.raceTime == -1 && Rooms.raceWinnered; // bc this means no race
            if (Rooms.isPrivate && !wasPrivate)
                roomCLoc.SetKey("NeonNetwork/ROOMS_ID", [new("{0}", "••••", false)]);
            else if (!Rooms.isPrivate && wasPrivate)
                roomCLoc.SetKey("NeonNetwork/ROOMS_ID", [new("{0}", Rooms.EncodeID(Rooms.id), false)]);
            wasPrivate = Rooms.isPrivate;
            visible.color = Color.white.Alpha(allGV ? 1 : 0.3f);
            nametag.color = Color.white.Alpha(allNV ? 1 : 0.3f);
            foreach (UserView u in transform.GetComponentsInChildren<UserView>())
            {
                if (u.user.syncedG)
                    u.user.ghostVisible = allGV;
                if (u.user.syncedT)
                    u.user.tagVisible = allNV;
            }
        }

        override protected void Cleanup() => instance = null;

        public static void AddUser(Rooms.User user)
        {
            if (instance == null)
            {
                preList.Add(user);
                return;
            }
            if (GetUser(user))
                return;
            var obj = Utils.InstantiateUI(instance.userCopy.gameObject, $"{user}", instance.scrollContent);
            obj.GetComponent<UserView>().SetInfo(user);
            obj.gameObject.SetActive(true);
        }

        public static void RemoveUser(Rooms.User user)
        {
            if (instance == null)
            {
                preList.Remove(user);
                return;
            }
            var obj = instance.scrollContent.Find(user.steamID.ToString());
            if (obj)
                obj.GetComponent<UserView>().Remove();
        }

        public static UserView GetUser(Rooms.User user)
        {
            return instance?.scrollContent.Find(user.steamID.ToString())?.GetComponent<UserView>();
        }

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Popups.Components.Button;
            switch (component.name)
            {
                case "CopyCode":
                    button.onClickEvent.AddListener(() =>
                    {
                        GUIUtility.systemCopyBuffer = Rooms.EncodeID(Rooms.id);
                        if (Rooms.isPrivate)
                            GUIUtility.systemCopyBuffer += Rooms.EncodeID(Rooms.secret);
                        Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_IDCOPIED", 10);
                    });
                    break;
                case "Invite":
                    inviteButton = button;
                    button.onClickEvent.AddListener(Rooms.Invite);
                    break;
                default:
                    break;
            }
        }

        public class UserView : MonoBehaviour
        {
            CanvasGroup group;
            float opacity = 0;
            bool vanishing = false;

            readonly Transition scaleT = new(3, Transition.movementEase, []);
            readonly Transition readyT = new(3, Transition.opacityEase, []);

            public Rooms.User user;

            TextMeshProUGUI nameText;

            RawImage nametag;
            RawImage visible;
            RawImage kick;
            RawImage ready;
            RawImage join;

            public AxKLocalizedText location;

            public TextMeshProUGUI ping;
            float lastPing;

            void Awake()
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = opacity;
                scaleT.Set(1);
                scaleT.timestamps = new() { { 10, () => Destroy(gameObject) } };
                readyT.Set(0);

                transform.Find("Vis").GetComponent<Button>().onClick.AddListener(() =>
                {
                    SetVisibility(!user.ghostVisible);
                });
                transform.Find("Tag").GetComponent<Button>().onClick.AddListener(() =>
                {
                    SetTagVisibility(!user.tagVisible);
                });

                nameText = transform.Find("Name").GetComponent<TextMeshProUGUI>();

                visible = transform.Find("Vis").GetComponent<RawImage>();
                nametag = transform.Find("Tag").GetComponent<RawImage>();
                ready = transform.Find("Ready").GetComponent<RawImage>();
                kick = transform.Find("Kick")?.GetComponent<RawImage>();
                join = transform.Find("Join").GetComponent<RawImage>();

                location = Localization.Setup(transform.Find("InLevel"));

                ping = transform.Find("Ping").GetComponent<TextMeshProUGUI>();

                if (kick)
                {
                    kick.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        TextButtons.Show("NeonNetwork/ROOMS_WARN_KICK",
                            (popup, _) =>
                            {
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                {
                                    popup.Leave();
                                    Rooms.KickUser(user.steamID.m_SteamID);
                                });
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                            },
                            [new("{0}", Online.Online.GetName(user.steamID.m_SteamID), false)]
                        );
                    });
                }
            }

            void Start() => SetLocation(); //just make sure

            void Update()
            {
                if (!vanishing)
                {
                    group.alpha = opacity;
                    opacity = Math.Min(opacity + Time.unscaledDeltaTime * 3, 1);
                }
                else
                {
                    group.alpha = opacity;
                    group.interactable = false;
                    opacity = Math.Max(opacity - Time.unscaledDeltaTime * 3, 0);
                    if (opacity == 0 && !scaleT.running)
                        scaleT.Start(1, 0);
                }

                if (user != null)
                {
                    visible.color = Color.white.Alpha(user.ghostVisible ? 1 : 0.3f);
                    nametag.color = Color.white.Alpha(user.tagVisible ? 1 : 0.3f);
                    if (user.raceReady && !Rooms.isRacing)
                    {
                        if (readyT.goal != 1)
                            readyT.Start(null, 1);
                    }
                    else
                    {
                        if (readyT.goal != 0)
                            readyT.Start(null, 0);
                    }
                    ready.color = ready.color.Alpha(readyT.result);
                }
                else
                {
                    if (Rooms.raceAccepted && !Rooms.isRacing)
                    {
                        if (readyT.goal != 1)
                            readyT.Start(null, 1);
                    }
                    else
                    {
                        if (readyT.goal != 0)
                            readyT.Start(null, 0);
                    }
                    ready.color = ready.color.Alpha(readyT.result);
                }

                if (user.ghost && user.ghost.offset != lastPing)
                {
                    if (!ping.isActiveAndEnabled)
                        ping.gameObject.SetActive(true);

                    lastPing = user.ghost.offset;
                    ping.text = $"{(int)(lastPing * 1000)}ms";
                }

                transform.localScale = new Vector3(1, scaleT.result, 1);
                Transition.ProcessAll(this);
            }

            public void SetVisibility(bool vis)
            {
                user.ghostVisible = vis;
                user.syncedG = false;
            }

            public void SetTagVisibility(bool vis)
            {
                user.tagVisible = vis;
                user.syncedT = false;
            }

            public void Remove() => vanishing = true;

            public void SetLocation()
            {
                if (user.raceError == null) {
                    location.textMeshProUGUI.color = Color.white;
                    if (!user.level)
                        location.SetKey("NeonNetwork/ROOMS_LOCATION_IDK");
                    else if (user.level.type == LevelData.LevelType.Hub)
                        location.SetKey("NeonNetwork/ROOMS_LOCATION_HUB");
                    else if (LocalizationManager.TryGetTranslation(user.level.GetLevelDisplayName(), out _))
                        location.SetKey(user.level.GetLevelDisplayName());
                    else
                    {
                        location.SetKey("");
                        location.textMeshProUGUI.text = user.level.levelDisplayName;
                    }

                    if (join)
                    {
                        if (Rooms.isRacing)
                            join.gameObject.SetActive(false);
                        else if (user.level && user.level.type != LevelData.LevelType.Hub)
                        {
                            // setup join

                            var joinb = join.GetComponent<Button>();
                            joinb.gameObject.SetActive(Rooms.selfUser != user);
                            joinb.onClick.RemoveAllListeners();
                            joinb.onClick.AddListener(() =>
                            {
                                Rooms.game.PlayLevel(user.level, true);
                            });
                        }
                        else
                            join.gameObject.SetActive(false);
                    }
                }
                else
                {
                    location.textMeshProUGUI.color = Status.Colors.error;
                    location.SetKey(user.raceError);
                }
            }

            public void SetInfo(Rooms.User user)
            {
                this.user = user;
                var steamID = user.steamID.m_SteamID;
                name = steamID.ToString();

                GetComponentInChildren<RawImage>().texture = Online.Online.GetPFP(steamID);
                nameText.text = Online.Online.GetName(steamID, x => nameText.text = x);

                if (Rooms.selfUser == user)
                {
                    foreach (var b in GetComponentsInChildren<Button>())
                        b.gameObject.SetActive(false);
                    ping.gameObject.SetActive(false);
                }
                else
                {
                    ping.gameObject.SetActive(user.ghost.offset != 0);
                    lastPing = user.ghost.offset;
                }

                SetLocation();
            }
        }
    }
}
