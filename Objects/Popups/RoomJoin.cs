using NeonNetwork.Online;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace NeonNetwork.Objects.Popups
{
    internal class RoomJoin : Popup
    {
        public Components.Input idInput;
        Components.Button submitButton;

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
            {
                var input = component as Components.Input;
                switch (component.name)
                {
                    case "ID":
                        idInput = input; return;
                }
                return;
            }

            switch (component.name)
            {
                case "Join":
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
            var len = idInput.input.text.Length;
            submitButton.ShouldBeInteractable = len == 4 || len == 8;
            //idInput.input.contentType = len <= 4 ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        }


        void Submit()
        {
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
            Rooms.JoinRoom(idInput.input.text.ToUpper());
            //Online.Online.state = "RoomCreate"; 
            //Online.Online.Send(Info.Opcodes.RoomsAdd, Encoding.Default.GetBytes(nameInput.input.text));
        }

    }
}
