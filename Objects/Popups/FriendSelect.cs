using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Popups
{
    internal class FriendSelect : Popup
    {
        Transform roomCopy;
        public Transform scrollContent;
        readonly List<CSteamID> invite = [];
        Components.Button okb;

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
                return;

            switch (component.name)
            {
                case "OK":
                    okb = button;
                    button.onClickEvent.AddListener(() =>
                    {
                        foreach (var id in invite)
                        {
                            if (SteamMatchmaking.InviteUserToLobby(Online.Rooms.roomSID, id))
                                Online.Rooms.invited[id] = 5 * 60; // 5 mins
                        }
                        Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_INVITED");
                        Leave();
                    });
                    break;
                case "Cancel":
                    button.onClickEvent.AddListener(Leave);
                    break;
            }
        }

        new void Update()
        {
            base.Update();
            okb.ShouldBeInteractable = invite.Count != 0;
        }

        protected void Start()
        {
            instance = this;
            var scrollview = contents.Find("Scroll View").GetComponent<ScrollRect>();
            scrollContent = scrollview.content;
            roomCopy = scrollContent.GetChild(0);
            roomCopy.gameObject.SetActive(false);

            var count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            int playC = 0;
            int onliC = 0;
            int awayC = 0;

            for (int i = 0; i < count; ++i)
            {
                var ID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);

                var obj = Utils.InstantiateUI(roomCopy.gameObject, ID.ToString(), scrollContent);
                var loc = Localization.Setup(obj.transform.Find("Status")); 

                obj.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = SteamFriends.GetFriendPersonaName(ID);
                obj.GetComponentInChildren<RawImage>().texture = Online.Online.GetPFP(ID.m_SteamID);
                if (Online.Rooms.inRoom.ContainsKey(ID.m_SteamID))
                {
                    obj.GetComponent<Button>().interactable = false;
                    loc.SetKey("NeonNetwork/ROOMS_INVITE_WITHYOU");
                    obj.transform.SetAsLastSibling();
                }
                else if (SteamFriends.GetFriendGamePlayed(ID, out var game) && game.m_gameID.AppID() == SteamUtils.GetAppID())
                {
                    obj.transform.SetAsFirstSibling();
                    playC++;
                    loc.SetKey("NeonNetwork/ROOMS_INVITE_PLAYING");
                }
                else
                {
                    string status = "NeonNetwork/ROOMS_INVITE_ONLINE";
                    switch (SteamFriends.GetFriendPersonaState(ID))
                    {
                        case EPersonaState.k_EPersonaStateOnline:
                        case EPersonaState.k_EPersonaStateLookingToPlay:
                        case EPersonaState.k_EPersonaStateLookingToTrade:
                            obj.transform.SetSiblingIndex(playC);
                            onliC++;
                            //status = "NeonNetwork/ROOMS_INVITE_ONLINE";
                            break;
                        case EPersonaState.k_EPersonaStateAway:
                        case EPersonaState.k_EPersonaStateBusy:
                        case EPersonaState.k_EPersonaStateSnooze:
                            obj.transform.SetSiblingIndex(playC + onliC);
                            awayC++;
                            status = "NeonNetwork/ROOMS_INVITE_AWAY";
                            break;
                        case EPersonaState.k_EPersonaStateInvisible:
                        case EPersonaState.k_EPersonaStateOffline:
                            obj.transform.SetSiblingIndex(playC + onliC + awayC);
                            status = "NeonNetwork/ROOMS_INVITE_OFFLINE";
                            break;
                    }
                    loc.SetKey(status);
                }

                obj.GetComponent<Button>().onClick.AddListener(() =>
                {
                    var add = !obj.transform.Find("Ready").gameObject.activeSelf;
                    obj.transform.Find("Ready").gameObject.SetActive(add);
                    if (add)
                        invite.Add(ID);
                    else
                        invite.Remove(ID);

                });

                obj.gameObject.SetActive(true);
            }
        }

    }
}