using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace NeonNetwork.Objects.Popups
{
    internal class Loading : Popup
    {
        public static void Show(string text, Action<TextButtons, TextMeshProUGUI> onReady) => ShowPopup<Loading>("Loading", (popup) =>
        {
            var tmp = popup.contents.GetChild(0).Find("text").GetComponent<TextMeshProUGUI>();
            popup.contents.GetChild(0).Find("icon").GetOrAddComponent<DisplayLoadIcon>().StartAnimation();
            tmp.text = text;
            onReady?.Invoke((TextButtons)popup, tmp);
        });

    }
}
