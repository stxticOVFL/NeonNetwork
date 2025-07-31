using MelonLoader.TinyJSON;
using NeonLite.Modules;
using NeonNetwork.Online;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Other
{
    internal class RaceSidebar : MonoBehaviour
    {
        public static RaceSidebar instance;
        Canvas c;
        CanvasGroup canvasGroup;
        Transform holder;
        Scrollbar scrollbar;

        static User baseUser;

        Transition moveT = new(5, Transition.movementEase, []);
        Transition opacityT = new(4, Transition.opacityEase, []);

        public static bool shown = false;

        public static void Setup()
        {
            NeonNetwork.Logger.Msg("RaceSidebar Setup");
            var obj = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/RaceSidebar.prefab");
            obj = Utils.InstantiateUI(obj, "RaceSidebar", NeonNetwork.nnMMHolder);
            obj.transform.GetChild(0).GetChild(0).GetChild(0).GetOrAddComponent<RaceSidebar>();
        }

        void Awake()
        {
            instance = this;
            baseUser = transform.GetChild(0).GetOrAddComponent<User>();
            baseUser.gameObject.SetActive(false);
            moveT.Set(0.68f);
        }
        void Start()
        {
            holder = transform.parent.parent.parent;
            canvasGroup = holder.GetComponent<CanvasGroup>();
            scrollbar = holder.GetChild(0).GetComponent<ScrollRect>().verticalScrollbar;
            c = GetComponentInParent<Canvas>();
        }
        void Update()
        {
            var mmstate = MainMenu.Instance().GetCurrentState();
            if (Rooms.game.GetCurrentLevel() != Rooms.raceLevel)
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
            }
            else
            {
                if (!Settings.hiddenLB.Value || shown)
                {
                    if (opacityT.goal != 1)
                        opacityT.Start(null, 1);
                }
                else
                {
                    if (opacityT.goal != 0)
                        opacityT.Set(0); // so we can avoid spamming 0
                }
                canvasGroup.alpha = opacityT.result;
            }
            if (mmstate == MainMenu.State.None || mmstate == MainMenu.State.None_NoPause)
            {
                scrollbar.value = 1;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
                if (moveT.goal != 0.8f)
                    moveT.Start(null, 0.8f);
            }
            else
            {
                canvasGroup.interactable = canvasGroup.blocksRaycasts = true;
                if (moveT.goal != 0.68f)
                    moveT.Start(null, 0.68f);
            }
            holder.localPosition = c.ViewportToCanvasPosition(new Vector3(1f, moveT.result, 0));
            Transition.ProcessAll(this);
        }

        public static void Clear()
        {
            foreach (Transform t in instance.transform)
            {
                if (t != baseUser.transform)
                    Destroy(t.gameObject);
            }
        }

        public static void AddUser(ulong steamID)
        {
            var obj = Utils.InstantiateUI(baseUser.gameObject, steamID.ToString(), instance.transform);
            obj.SetActive(true);
            var user = obj.GetComponent<User>();
            user.SetInfo(steamID);
        }

        public static User GetUser(ulong user)
        {
            return instance?.transform.Find(user.ToString())?.GetComponent<User>();
        }

        public static void SetTime(ulong steamID, long time)
        {
            int moveTo = 0;
            bool found = false;
            foreach (var user in instance.transform.GetComponentsInChildren<User>())
            {
                if (user.time > time)
                    found = true;
                if (user.steamID == steamID)
                {   
                    int current = user.transform.GetSiblingIndex();
                    user.transform.SetSiblingIndex(moveTo);

                    user.timeText.color = user.timeText.color.Alpha(1);

                    user.time = time;

                    if (time != long.MaxValue)
                        user.timeText.text = NeonLite.Helpers.FormatTime(time / 1000, true, '.');
                    else
                    {
                        user.timeText.text = "---DNF---";
                        return;
                    }

                    int medal = Helpers.GetMedalIndexSafely(Rooms.raceLevel, time);
                    user.medal.texture = CommunityMedals.Medals[medal].texture;
                    CommunityMedals.AdjustMaterial(user.medal);

                    if (current != moveTo)
                        user.animTimer = 0.5f;
                    break;
                }
                if (!found)
                    ++moveTo;
            }
        }

        internal class User : MonoBehaviour
        {
            public ulong steamID;
            public long time = long.MaxValue;
            public float animTimer = -1;
            public TextMeshProUGUI timeText;
            bool locked = false;

            RawImage hilight;
            public RawImage medal;
            RawImage up;

            void Start()
            {
                hilight = transform.Find("Highlight").GetComponent<RawImage>();
                medal = transform.Find("Medal").GetComponent<RawImage>();
                up = medal.transform.GetChild(0).GetComponent<RawImage>();
                timeText = transform.Find("Time").GetComponent<TextMeshProUGUI>();
            }

            public void SetInfo(ulong steamID)
            {
                this.steamID = steamID;
                transform.Find("PFP").GetComponent<RawImage>().texture = Online.Online.GetPFP(steamID);
                if (steamID == Online.Online.steamID.m_SteamID)
                {
                    if (!timeText)
                        timeText = transform.Find("Time").GetComponent<TextMeshProUGUI>();
                    timeText.color = new(1, 0.835f, 0.467f, timeText.color.a);
                }
            }

            void Update()
            {
                if (animTimer > -1)
                {
                    animTimer -= Time.unscaledDeltaTime;
                    medal.color = Color.white.Alpha(1 - (animTimer * 2));
                    up.color = up.color.Alpha(animTimer * 2);
                    if (!locked)
                        hilight.color = Color.white.Alpha(.3f * (animTimer * 2));
                    if (animTimer < 0)
                        animTimer = -1;
                }
            }

            public void LockIn()
            {
                locked = true;
                hilight.color = Color.white.Alpha(0.2f);
            }
        }
    }
}
