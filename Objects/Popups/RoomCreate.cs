using NeonNetwork.Online;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeonNetwork.Objects.Popups
{
    internal class RoomCreate : Popup
    {
        public Components.Input nameInput;
        public Components.Input maxInput;
        Components.Button submitButton;

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
            {
                var input = component as Components.Input;
                switch (component.name)
                {
                    case "Name":
                        nameInput = input; return;
                    case "Limit":
                        maxInput = input; return;
                }
                return;
            }

            switch (component.name)
            {
                case "Create":
                    submitButton = button;
                    button.onClickEvent.AddListener(Submit);
                    break;
                case "Cancel":
                    button.onClickEvent.AddListener(Leave);
                    break;
            }
        }

        new void Update()
        {
            base.Update();
            submitButton.ShouldBeInteractable = (nameInput.input.text != "") && (maxInput.input.text == "" || int.Parse(maxInput.input.text) <= 250);
        }


        void Submit()
        {
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
            Rooms.CreateRoom(nameInput.input.text, maxInput.input.text == "" ? 64 : int.Parse(maxInput.input.text));
            //Online.Online.state = "RoomCreate"; 
            //Online.Online.Send(Info.Opcodes.RoomsAdd, Encoding.Default.GetBytes(nameInput.input.text));
        }

    }
}
