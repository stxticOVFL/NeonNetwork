using NeonNetwork.Objects.Popups;
using NeonNetwork.Online;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class RoomsList : Contents
    {
        public static RoomsList instance;
        RoomView roomCopy;
        public Transform scrollContent;

        Transition loadT = new(3, Transition.opacityEase, []);
        Transition contentT = new(3, Transition.opacityEase, []);

        CanvasGroup loadCG;
        CanvasGroup contentCG;
        //public GameObject noRooms;
        Popups.Components.Button refreshButton;

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Popups.Components.Button;
            switch (component.name)
            {
                case "Create":
                    button.onClickEvent.AddListener(() => Popup.ShowPopup<RoomCreate>("RoomCreate"));
                    break;
                case "JoinID":
                    button.onClickEvent.AddListener(() => Popup.ShowPopup<RoomJoin>("RoomJoinID"));
                    break;
                case "Refresh":
                    refreshButton = button;
                    button.onClickEvent.AddListener(Refresh);
                    break;
                case "Back":
                    button.onClickEvent.AddListener(() => SidePanel.LoadContent<Home>("Home", "NeonNetwork/HOME_LABEL"));
                    break;
                default:
                    break;
            }
        }

        DisplayLoadIcon loadicon;

        protected void Start()
        {
            instance = this;
            var scrollview = transform.Find("Scroll View").GetComponent<ScrollRect>();
            scrollContent = scrollview.content;
            roomCopy = scrollContent.GetChild(0).GetOrAddComponent<RoomView>();
            roomCopy.gameObject.SetActive(false);

            loadCG = scrollview.transform.Find("Loading").GetComponent<CanvasGroup>();
            contentCG = scrollview.viewport.GetComponent<CanvasGroup>();
            //noRooms = scrollview.viewport.Find("NoRooms").gameObject;
            //Localization.Setup(noRooms);

            loadicon = loadCG.transform.GetChild(0).GetOrAddComponent<DisplayLoadIcon>();
            loadicon.StartAnimation();

            loadT.Set(1);
            contentT.Set(0);

            Refresh();
        }

        override protected void Cleanup() => instance = null;

        void Refresh()
        {
            loadT.Start(null, 1);
            contentT.Start(null, 0);
            loadCG.interactable = loadCG.blocksRaycasts = true;
            refreshButton.ShouldBeInteractable = false;
            Rooms.ListPublicRooms();
        }

        public void Ready()
        {
            loadT.Start(null, 0);
            contentT.Start(null, 1);
            loadCG.interactable = loadCG.blocksRaycasts = false;
            refreshButton.ShouldBeInteractable = true;
        }

        void Update()
        {
            loadCG.alpha = loadT.result;
            contentCG.alpha = contentT.result;

            loadicon.group.alpha = (loadCG.alpha == 0) ? 0 : 1;

            Transition.ProcessAll(this);
        }

        public static RoomView AddRoom(ulong id)
        {
            if (!instance)
                return null;

            var obj = Utils.InstantiateUI(instance.roomCopy.gameObject, id.ToString(), instance.scrollContent);
            obj.GetComponent<RoomView>().Setup(id);
            obj.gameObject.SetActive(true);
            return obj.GetComponent<RoomView>();
        }

        public class RoomView : MonoBehaviour
        {
            public ulong id;
            public void Setup(ulong id)
            {
                this.id = id;
                if (id == 0) 
                    return;
                var sid = (CSteamID)id;
                var name = SteamMatchmaking.GetLobbyData(sid, "name");
                var owner = SteamMatchmaking.GetLobbyData(sid, "ownername");
                var count = SteamMatchmaking.GetNumLobbyMembers(sid);

                transform.Find("Name").GetComponent<TextMeshProUGUI>().text = name;
                Localization.Setup(transform.Find("Owner").GetComponent<TextMeshProUGUI>()).SetKey("NeonNetwork/ROOMS_OWNER", [new("{0}", owner, false)]);
                transform.Find("Count").GetComponent<TextMeshProUGUI>().text = $"{count}/{SteamMatchmaking.GetLobbyMemberLimit(sid)}";

                GetComponent<Button>().onClick.RemoveAllListeners();
                GetComponent<Button>().onClick.AddListener(() => TextButtons.Show("NeonNetwork/ROOMS_WARN_JOINLISTING",
                    (popup, _) =>
                    {
                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                        {
                            popup.Leave();
                            SteamMatchmaking.JoinLobby(sid);
                        });
                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                    },
                    [new("{0}", name, false)]));
            }
            public void SetAutoroom(ulong id)
            {
                var sid = (CSteamID)id;
                var rid = id == 0 ? "" : SteamMatchmaking.GetLobbyData(sid, "id");

                Localization.Setup(transform.Find("Name").GetComponent<TextMeshProUGUI>()).SetKey("NeonNetwork/ROOMS_AUTOROOM");
                Localization.Setup(transform.Find("Owner").GetComponent<TextMeshProUGUI>()).SetKey("NeonNetwork/ROOMS_ID", [new("{0}", rid, false)]);
                transform.Find("Owner").gameObject.SetActive(id != 0);
                if (id == 0)
                    transform.Find("Count").GetComponent<TextMeshProUGUI>().text = "0/250";

                GetComponent<Button>().onClick.RemoveAllListeners();
                GetComponent<Button>().onClick.AddListener(() => TextButtons.Show("NeonNetwork/ROOMS_WARN_AUTOROOM",
                    (popup, _) =>
                    {
                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                        {
                            popup.Leave();
                            Rooms.JoinAutoroom(id);
                        });
                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                    }));
            }
        }

    }
}
