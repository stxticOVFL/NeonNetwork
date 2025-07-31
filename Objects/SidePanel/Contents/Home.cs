using NeonNetwork.Objects.Popups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class Home : Contents
    {
        override protected void SetupComponent<T>(T component)
        {
            var button = component as Popups.Components.Button;
            switch (component.name)
            {
                case "Rooms":
                    button.onClickEvent.AddListener(() => SidePanel.LoadContent<RoomsList>("RoomsList", "NeonNetwork/ROOMS_LABEL"));
                    break;
                default:
                    button.ShouldBeInteractable = false;
                    break;
            }
        }
    }
}
