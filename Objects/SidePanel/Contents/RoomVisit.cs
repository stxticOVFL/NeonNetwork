using NeonNetwork.Objects.Popups;
using NeonNetwork.Objects.Popups.Components;
using NeonNetwork.Online;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class RoomVisit : RoomBase
    {
        Button readyButton;
        bool readied = false;
        Color readyColor;
        override protected void SetupComponent<T>(T component)
        {
            base.SetupComponent(component);
            var button = component as Popups.Components.Button;
            switch (component.name)
            {
                case "Leave":
                    button.onClickEvent.AddListener(() => TextButtons.Show("NeonNetwork/ROOMS_WARN_LEAVE",
                            (popup, _) =>
                            {
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                {
                                    Rooms.LeaveRoom();
                                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_LEFT_SELF", sound: "HINT_RESET");
                                });
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                            }
                        ));
                    break;
                case "Ready":
                    readyColor = button.ButtonRef.image.color;
                    readyButton = button;
                    button.onClickEvent.AddListener(() =>
                    {
                        readied = !readied;
                        if (readied)
                        {
                            Rooms.ReadyUp();
                            button.ButtonRef.image.color = Color.red;
                            button.label.text = "Unready";
                        }
                        else
                        {
                            Rooms.Unready();
                            button.ButtonRef.image.color = readyColor;
                            button.label.text = "Ready";
                        }
                    });
                    break;
                default:
                    break;
            }
        }

        new void Update()
        {
            base.Update();
            inviteButton.ShouldBeInteractable &= !Rooms.isPrivate; // bc private room visitors can't send invites
            if (Rooms.raceTime != -1 && !Rooms.isRacing)
                readyButton.ShouldBeInteractable = true;
            else
            {
                if (readied)
                {
                    readyButton.ButtonRef.image.color = readyColor;
                    readyButton.label.text = "Ready";
                }
                readyButton.ShouldBeInteractable = false;
                readied = false;
            }
        }

    }
}
