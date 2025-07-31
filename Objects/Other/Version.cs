using NeonNetwork.Resources;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Other
{
    internal class Version : MonoBehaviour
    {
        Canvas c;
        CanvasGroup cg;
        static public void Setup()
        {
            var obj = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/Version.prefab");
            Utils.InstantiateUI(obj, "NNVersion", NeonNetwork.nnMMHolder).AddComponent<Version>();
        }
        public void Start()
        {
            transform.Find("Build").GetComponent<TextMeshProUGUI>().text = $"Build {NeonNetwork.Version}";
            c = GetComponentInParent<Canvas>();
            cg = GetComponent<CanvasGroup>();
        }

        void Update()
        {
            var mmstate = MainMenu.Instance().GetCurrentState();
            cg.alpha = mmstate == MainMenu.State.Title ? 1 : 0;
            transform.localPosition = c.ViewportToCanvasPosition(new Vector3(0.95f, 0.05f, 0));
        }
    }
}
