using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonNetwork.Objects
{
    internal class RenderLoadIcon : MonoBehaviour
    {
        public static RenderLoadIcon i;
        public Camera cam;

        internal static void Setup()
        {
            NeonNetwork.Logger.Msg("RenderLoadIcon Setup");
            var prefab = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/RenderLoadIcon.prefab");
            i = Utils.InstantiateUI(prefab, "RenderLoadIcon", NeonNetwork.nnHolder).AddComponent<RenderLoadIcon>();
        }

        void Start()
        {
            cam = GetComponentInChildren<Camera>();
            var holder = transform.Find("Insight Holder");
            var ball = UnityEngine.Resources.Load<GameObject>("pickups/CardInsightPickupEmpty").transform.Find("Holder").GetChild(0).gameObject;
            ball = Utils.InstantiateUI(ball, "Insight Ball", holder);
            var layerUI = LayerMask.NameToLayer("UI");
            ball.transform.OnAllChildren(t => t.gameObject.layer = layerUI);
            ball.transform.GetChild(0).Find("OBJ_Insight_Ball_Particle").gameObject.layer = 0;
            ball.GetComponentInChildren<RotateAroundAxis>().GetOrAddComponent<AlwaysSpin>();
            ball.transform.localPosition = Vector3.zero;
        }

        void LateUpdate()
        {
            cam.enabled = DisplayLoadIcon.icons.Any(x => x.group.alpha > 0);
        }

        internal class AlwaysSpin : MonoBehaviour
        {
            void Awake()
            {
                transform.GetComponent<RotateAroundAxis>().enabled = false;
            }
            void Update()
            {
                transform.Rotate(new(0, 0, 1), 40 * Time.unscaledDeltaTime);
            }
        }
    }
}
