using NeonNetwork.Online;
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
    internal class Nametag : MonoBehaviour
    {
        RectTransform nameN;
        RectTransform pfp;

        Camera mainCam;

        static GameObject prefab;

        public TextMeshPro nameText;
        public SpriteRenderer backing;
        public MeshRenderer pfpMesh;

        public static void Setup() => prefab = NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/Nametag.prefab");

        public static Nametag Spawn(Transform parent, ulong steamID)
        {
            var ret = Instantiate(prefab, parent).AddComponent<Nametag>();
            ret.SetInfo(steamID);
            return ret;
        }

        public void SetInfo(ulong steamID)
        {
            transform.Find("Name").GetComponent<TextMeshPro>().text = Online.Online.GetName(steamID);
            transform.Find("PFP").GetComponent<MeshRenderer>().material.mainTexture = Online.Online.GetPFP(steamID);
        }

        void Awake()
        {
            nameN = transform.Find("Name") as RectTransform;
            pfp = transform.Find("PFP") as RectTransform;

            nameText = nameN.GetComponent<TextMeshPro>();
            backing = nameN.GetChild(0).GetComponent<SpriteRenderer>();
            pfpMesh = pfp.GetComponent<MeshRenderer>();
        }

        public void Start()
        {
            nameN.GetComponent<MeshRenderer>().sortingOrder = 3;
            backing.sortingOrder = 2;
            pfpMesh.sortingOrder = 1;
        }

        public void SetOpacity(float opacity)
        {
            nameText.alpha = opacity;
            backing.color = backing.color.Alpha(0.4f * opacity);
            pfpMesh.material.color = Color.white.Alpha(opacity);
        }

        void LateUpdate()
        {
            if (!mainCam)
                mainCam = Camera.main;
            if (!mainCam)
                return;

            nameN.GetChild(0).localScale = new Vector3(nameN.sizeDelta.x + 2.1f, nameN.sizeDelta.y, 1);
            var pos = nameN.localPosition.x - (nameN.sizeDelta.x * nameN.pivot.x);
            pfp.localPosition = new Vector3(pos - 2.5f, 0, 0);
            transform.localPosition = new Vector3(0, 2.1f, 0);
            transform.rotation = Quaternion.LookRotation(mainCam.transform.forward, mainCam.transform.up);
            //transform.eulerAngles = new(transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
        }
    }
}
