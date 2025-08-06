using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonNetwork.Objects
{
    internal class StatusHandler : MonoBehaviour
    {
        static GameObject statusPrefab;
        static StatusHandler instance;

        internal static void Setup()
        {
            statusPrefab = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/Status.prefab");

            GameObject obj = new("Status Holder", typeof(RectTransform));
            obj.transform.SetParent(NeonNetwork.nnMMHolder);
            obj.SetActive(true);
            instance = obj.AddComponent<StatusHandler>();
        }

        void Awake()
        {
            Utils.InstantiateUI(statusPrefab, "Status", transform).AddComponent<Status>();
        }

        void Update()
        {
            var c = GetComponentInParent<Canvas>();
            transform.localPosition = c.ViewportToCanvasPosition(new Vector3(1f, 1f, 0));
            transform.localScale = Vector3.one;
        }
    }
}
