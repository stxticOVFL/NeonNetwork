using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace NeonNetwork.Objects.Popups
{
    internal class TextButtons : Popup
    {
        public static void Show(string text, Action<TextButtons, TextMeshProUGUI> onReady, List<AxKReplacementPair> pairs = null) => ShowPopup<TextButtons>("TextButtons", (popup) =>
        {
            var tmp = popup.contents.Find("text").GetComponent<TextMeshProUGUI>();
            Localization.Setup(tmp).SetKey(text, pairs?.ToArray() ?? []);
            onReady?.Invoke((TextButtons)popup, tmp);
        });
    }
}