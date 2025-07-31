using I2.Loc;
using NeonNetwork.Online;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MelonLoader.MelonLogger;

namespace NeonNetwork.Objects.SidePanel
{
    internal class SidePanel : MonoBehaviour
    {
        static SidePanel instance;

        CanvasGroup entireCG;
        float entireOP;

        CanvasGroup contentCG;
        readonly Transition contentT = new(3, Transition.opacityEase, []);

        Transform sideButton;
        Transform panel;
        Vector3 sideP;
        Vector3 panelP;
        readonly Transition posT = new(3, Transition.movementEase, []);

        DisplayLoadIcon loadIcon;

        public static bool attention;

        public static void Setup()
        {
            NeonNetwork.Logger.Msg("SidePanel Setup");
            var obj = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/SidePanel.prefab");
            Utils.InstantiateUI(obj, "SidePanel", NeonNetwork.nnMMHolder).AddComponent<SidePanel>();
        }

        void Awake()
        {
            instance = this;
            entireCG = GetComponent<CanvasGroup>();
            entireCG.alpha = 0;
            entireCG.blocksRaycasts = entireCG.interactable = false;

            sideButton = transform.Find("Side Button");
            loadIcon = sideButton.Find("LoadingIcon").GetOrAddComponent<DisplayLoadIcon>();
            panel = transform.Find("Panel");
            sideP = sideButton.transform.localPosition;
            panelP = panel.transform.localPosition;
            posT.Set(0);

            contentCG = transform.Find("Panel").GetChild(0).GetComponent<CanvasGroup>();
            contentT.Set(0);

            sideButton.GetComponent<Button>().onClick.AddListener(
                () =>
                {
                    if (posT.goal == 0)
                        posT.Start(null, -420);
                    else
                        posT.Start(null, 0);
                }
            );

            LoadContent<Contents.Home>("Home", "NeonNetwork/HOME_LABEL");
        }

        public static void LoadContent<T>(string prefab, string title) where T : Contents.Contents
        {
            if (instance.contentT.result != 0)
            {
                instance.contentCG.interactable = instance.contentCG.blocksRaycasts = false;
                if (!instance.contentT.running || instance.contentT.goal != 0)
                    instance.contentT.Start(null, 0);
                instance.contentT.timestamps = new() {
                    {
                        10,
                        () => LoadContent<T>(prefab, title)
                    }
                };
                return;
            }

            instance.contentT.Start(0, 1);
            instance.contentT.timestamps = [];

            var obj = NeonNetwork.bundle.LoadAsset<GameObject>($"Assets/Prefabs/SidePanel/Contents/{prefab}.prefab");
            var contentHolder = instance.panel.GetChild(0).GetChild(1);
            if (contentHolder.childCount != 0)
            {
                Destroy(contentHolder.GetChild(0).gameObject);

            }
            obj = Utils.InstantiateUI(obj, "Contents", contentHolder);
            obj.AddComponent<T>().ResolveComponents();
            if (LocalizationManager.TryGetTranslation(title, out _))
                Localization.Setup(instance.panel.GetChild(0).GetChild(0)).SetKey(title);
            else
                instance.panel.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = title;
            instance.contentCG.interactable = instance.contentCG.blocksRaycasts = true;
        }

        static readonly HashSet<MainMenu.State> hideStates = [MainMenu.State.None, MainMenu.State.None_NoPause, MainMenu.State.Dialogue];

        const float LOADING_TIMER = 5f;
        float loadingTime = 0;

        MainMenu.State lastState = MainMenu.State.Title;

        void Update()
        {
            var mmstate = MainMenu.Instance().GetCurrentState();
            if (hideStates.Contains(lastState))
            {
                hideStates.Add(MainMenu.State.Loading);
                loadingTime = 0;
            }
            lastState = mmstate;

            if (hideStates.Contains(mmstate))
            {
                entireCG.blocksRaycasts = entireCG.interactable = false;
                entireOP = 0;
                posT.Set(attention ? -420 : 0);

                if (mmstate == MainMenu.State.Loading)
                {
                    loadingTime += Time.unscaledDeltaTime;
                    if (loadingTime > LOADING_TIMER)
                        hideStates.Remove(mmstate);
                }
            }
            else
            {
                if (attention && posT.goal != -420)
                    posT.Start(null, -420);

                attention = false;
                entireCG.blocksRaycasts = entireCG.interactable = NeonNetwork.logged || Rooms.setup;
                if (NeonNetwork.logged || Rooms.setup)
                    entireOP = Math.Min(entireOP + Time.unscaledDeltaTime * 4, 1);
                else
                    entireOP = Math.Max(entireOP - Time.unscaledDeltaTime * 4, 0);

                hideStates.Remove(MainMenu.State.Loading);
            }

            entireCG.alpha = entireOP;
            loadIcon.group.alpha = entireOP == 0 ? 0 : 1;

            var c = GetComponentInParent<Canvas>();
            transform.localPosition = c.ViewportToCanvasPosition(new Vector3(1f, 0.8f, 0));

            sideButton.localPosition = new Vector3(sideP.x + posT.result, sideP.y, 0);
            panel.localPosition = new Vector3(panelP.x + posT.result, panelP.y, 0);

            contentCG.alpha = contentT.result;

            Transition.ProcessAll(this);
        }
    }
}
