using NeonNetwork.Online;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Popups
{
    internal class PWInput : Popup
    {
        public Components.Input password;

        Components.Button submit;
        TextMeshProUGUI status;

        float statusTimer;

        Transition statusT = new(2, Transition.opacityEase, []);

        public static void Show() => ShowPopup<PWInput>("PWInput");

        new void Awake()
        {
            base.Awake();
            status = contents.Find("Status").GetComponent<TextMeshProUGUI>();
            statusT.timestamps = new() { { 10, StatusEnd } };
            statusT.Set(0);
            status.alpha = 0;
        }

        new void Update()
        {
            base.Update();
            status.alpha = statusT.result;

            statusT.Process();
            if (statusTimer > 0)
            {
                statusTimer = Math.Max(0, statusTimer - Time.unscaledDeltaTime);
                if (statusTimer == 0)
                {
                    statusT.Start(1, 0);
                    statusTimer = -2;
                }
            }

        }

        public void SetStatus(string text)
        {
            statusTimer = -1;
            status.text = text;
            statusT.Start(null, 1);
            submit.ShouldBeInteractable = true;
        }

        void StatusEnd()
        {
            if (statusTimer == -1)
                statusTimer = 5;
        }

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
            {
                var input = component as Components.Input;
                switch (component.name)
                {
                    case "PassInput":
                        password = input; return;
                }
                return;
            }
            switch (component.name)
            {
                case "Submit":
                    submit = button;
                    button.onClickEvent.AddListener(() =>
                    {
                        //Online.Online.Send(Info.Opcodes.MetaCLogin, Encryption.Encrypt(password.input.text));
                        button.ShouldBeInteractable = false;
                    });
                    break;
            }
        }


    }
}
