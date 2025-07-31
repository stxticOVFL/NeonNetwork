using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.SidePanel.Contents
{
    internal class Contents : MonoBehaviour
    {
        public void ResolveComponents()
        {
            foreach (var button in GetComponentsInChildren<Button>())
            {
                if (button.transform.parent.Find("Graphic"))
                    SetupComponent(button.transform.parent.GetOrAddComponent<Popups.Components.Button>());
            }
        }

        virtual protected void SetupComponent<T>(T component) where T : MonoBehaviour { }
        virtual protected void Cleanup() { }
    }
}
