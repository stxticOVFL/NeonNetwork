using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeonNetwork.Objects.Popups
{
    internal class Welcome : Popup
    {
        public static void Show() => ShowPopup<Welcome>("Welcome");
        void Start()
        {
            var load = contents.Find("2NN").Find("Icon").GetOrAddComponent<DisplayLoadIcon>();
            load.StartAnimation();
        }

        public static void NextTime()
        {
            TextButtons.Show("""
                The welcome popup will appear again on the next restart. NeonNetwork functionality will be disabled.
                """,
                (popup, _) => popup.AddComponent<Components.Button>("Button", "OK").onClickEvent.AddListener(popup.Leave)
            );
            NeonNetwork.connected = false;
        }

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            switch (component.name)
            {
                case "Yes":
                    button.onClickEvent.AddListener(() => UserEdit.Show(true));
                    break;
                case "No":
                    button.onClickEvent.AddListener(NextTime);
                    break;
            }
        }
    }
}
