using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Popups.Components
{
    public class Button : MenuButtonHolder
    {
        public TextMeshProUGUI label;
        public AxKLocalizedText localizer;

        public void SetKey(string key) => localizer.SetKey(key);

        protected void Awake()
        {
            onClickEvent = new();
            var graphic = transform.Find("Graphic");
            graphic.GetOrAddComponent<MenuButtonBase>();
            label = graphic.Find("Label").GetComponent<TextMeshProUGUI>();
            localizer = Localization.Setup(label);
            graphic.SetAsFirstSibling();
            // bleh
            typeof(MenuButtonHolder).GetMethod("Awake", HarmonyLib.AccessTools.all).Invoke(this, null);
        }
    }
}
