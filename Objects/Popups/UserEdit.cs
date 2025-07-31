using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NeonNetwork.Online;
using NeonNetwork.Objects.Popups.Components;

namespace NeonNetwork.Objects.Popups
{
    internal class UserEdit : Popup
    {
        public bool newUser;
        RawImage pfp;

        Components.Input nickname;
        Components.Input bio;
        Components.Input password;

        public static void Show(bool newUser) => ShowPopup<UserEdit>("UserEdit", (popup) => (popup as UserEdit).newUser = newUser);

        new void Awake()
        {
            base.Awake();
            var content = contents.GetChild(0);
            pfp = content.Find("PFP").GetComponent<RawImage>();
        }

        void Start()
        {
            pfp.texture =
                (Texture2D)HarmonyLib.AccessTools.Method(typeof(LeaderboardIntegrationSteam), "GetSteamImageAsTexture2D")
                    .Invoke(null, [SteamFriends.GetLargeFriendAvatar(SteamUser.GetSteamID())]);
            (nickname.input.placeholder as TextMeshProUGUI).text = SteamFriends.GetPersonaName();
        }

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
            {
                var input = component as Components.Input;
                switch (component.name) {
                    case "Nickname":
                        nickname = input; break;
                    case "Bio":
                        bio = input; break;
                    case "PassInput":
                        password = input; break;
                }
                return;
            }
            switch (component.name)
            {
                case "Submit":
                    button.onClickEvent.AddListener(Submit);
                    break;
                case "Cancel":
                    button.onClickEvent.AddListener(Welcome.NextTime);
                    break;
            }
        }

        void Submit()
        {
            try
            {
                NeonNetwork.Logger.Msg($"hiii {newUser}");
                if (newUser)
                    _ = "";// Online.Online.Send(Info.Opcodes.UserSetPS, Encryption.Encrypt(password.input.text));
                else
                    Leave();
            }
            catch (Exception e)
            {
                NeonNetwork.Logger.Error(e);
            }
        }
    }
}
