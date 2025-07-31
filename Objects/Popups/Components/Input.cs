using I2.Loc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace NeonNetwork.Objects.Popups.Components
{
    internal class Input : MonoBehaviour
    {
        public TextMeshProUGUI label;
        TextMeshProUGUI limit;
        public TMP_InputField input;

        public AxKLocalizedText localLabel;
        public AxKLocalizedText localPlaceholder;

        void Awake()
        {
            label = transform.Find("Name").GetComponent<TextMeshProUGUI>();
            limit = label.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            input = transform.Find("Input").GetComponent<TMP_InputField>();
            input.asteriskChar = '•';
            limit.gameObject.SetActive(input.characterLimit != 0);

            localLabel = Localization.Setup(label);
            localPlaceholder = Localization.Setup(input.placeholder);
        }

        void Update()
        {
            limit.text = $"{input.text.Length}/{input.characterLimit}";
        }
    }
}
