using NeonNetwork.Objects.Popups;
using NeonNetwork.Online;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class RoomHost : RoomBase
    {
        Popups.Components.Button readyButton;

        override protected void SetupComponent<T>(T component)
        {
            base.SetupComponent(component);
            var button = component as Popups.Components.Button;
            switch (component.name)
            {
                case "Disband":
                    button.onClickEvent.AddListener(() => TextButtons.Show($"NeonNetwork/ROOMS_WARN_DISBAND",
                            (popup, _) =>
                            {
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                {
                                    Rooms.LeaveRoom();
                                    Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_DISBAND", 10);
                                });
                                popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                            }
                        ));
                    break;
                case "StartRace":
                    readyButton = button;
                    button.onClickEvent.AddListener(() =>
                    {
                        // readied = !readied;
                        if (Rooms.raceTime == -1 && Rooms.raceWinnered)
                            Popup.ShowPopup<RaceStart>("RaceStart");
                        else
                        {
                            TextButtons.Show($"NeonNetwork/RACE_WARN_STOPRACE",
                                (popup, _) =>
                                {
                                    popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                    {
                                        popup.Leave();
                                        Rooms.StopRace();
                                        if (Rooms.game.GetCurrentLevel() == Rooms.raceLevel)
                                        {
                                            Rooms.game.CancelLevelSetup();
                                            Rooms.game.PlayLevel(Rooms.raceLevel, true, true);
                                        }
                                        Status.ShowStatus("NeonNetwork/RACE_NOTIF_STOPRACE", 10);
                                    });
                                    popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                                }
                            );
                            // button.label.text = "Start Race";
                        }
                    });
                    break;
                case "Public":
                    button.onClickEvent.AddListener(() =>
                        {
                            // readied = !readied;
                            if (Rooms.isPrivate)
                                TextButtons.Show("NeonNetwork/ROOMS_WARN_PUBLIC",
                                    (popup, _) =>
                                    {
                                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                        {
                                            popup.Leave();
                                            button.SetKey("NeonNetwork/ROOMS_BUTTON_PRIVATE");
                                            Rooms.MakeRoomPublic();
                                            GUIUtility.systemCopyBuffer = Rooms.EncodeID(Rooms.id);
                                            Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_PUBLIC", 10, pairs: [new("{0}", "NeonNetwork/ROOMS_NOTIF_IDCOPIED")]);
                                        });
                                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                                    }
                                );
                            else
                                TextButtons.Show("NeonNetwork/ROOMS_WARN_PRIVATE",
                                    (popup, _) =>
                                    {
                                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_008_YES").onClickEvent.AddListener(() =>
                                        {
                                            popup.Leave();
                                            button.SetKey("NeonNetwork/ROOMS_BUTTON_PUBLIC");
                                            Rooms.MakeRoomPrivate();
                                            GUIUtility.systemCopyBuffer = Rooms.EncodeID(Rooms.id) + Rooms.EncodeID(Rooms.secret);
                                            Status.ShowStatus("NeonNetwork/ROOMS_NOTIF_PRIVATE", 10);
                                        });
                                        popup.AddComponent<Popups.Components.Button>("Button", "Interface/INTERFACE_LABEL_010_NO").onClickEvent.AddListener(popup.Leave);
                                    }
                                );
                        });

                    break;
                default:
                    break;
            }
        }

        new void Update()
        {
            base.Update();
            if (Rooms.raceTime == -1 && Rooms.raceWinnered)
                readyButton.label.text = "Start Duel";
            else
                readyButton.label.text = "Call off Duel";
        }

        void DisbandRoom()
        {
            Popup.Finish();
            //await Online.Online.Send(Info.Opcodes.RoomsDelete);
        }
    }
}
