using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects
{
    internal class Status : MonoBehaviour
    {
        static Status instance;

        public DisplayLoadIcon loadIcon;
        public RawImage backdrop;
        public TextMeshProUGUI text;
        public AxKLocalizedText localizer;

        float textTimer = -1;
        bool displaying = false;

        string queuedText;
        float queuedTime;
        Action<TextMeshProUGUI, bool> queueShow;
        List<AxKReplacementPair> queuedPairs;

        Animator animator;

        public static class Colors
        {
            public static Color normal = new Color32(240, 240, 240, 255);

            public static Color error = new Color32(237, 67, 55, 255);
        }

        void Awake()
        {
            loadIcon = transform.Find("LoadingIcon").gameObject.AddComponent<DisplayLoadIcon>();
            loadIcon.StartAnimation();
            backdrop = transform.Find("Backdrop").GetComponent<RawImage>();
            text = transform.GetComponentInChildren<TextMeshProUGUI>();
            localizer = Localization.Setup(text);
            animator = GetComponent<Animator>();
        }

        void Start()
        {
            NeonNetwork.Logger.DebugMsg("Status Start");

            animator.keepAnimatorControllerStateOnDisable = true;
            var showText = animator.runtimeAnimatorController.animationClips.First(x => x.name == "animShowText");
            var animE = new AnimationEvent
            {
                functionName = "OnShowText",
                time = 0
            };
            showText.AddEvent(animE);

            showText = animator.runtimeAnimatorController.animationClips.First(x => x.name == "animHideText");
            animE = new AnimationEvent
            {
                functionName = "OnExit",
                time = 0
            };
            showText.AddEvent(animE);

            instance = this;
        }

        void Update()
        {
            if (textTimer >= 0)
            {
                textTimer = Math.Max(0, textTimer - Time.unscaledDeltaTime);
                if (textTimer == 0)
                    Stop();
            }
        }

        void OnShowText()
        {
            NeonNetwork.Logger.DebugMsg("OnShowText");

            localizer.SetKey(queuedText, queuedPairs?.ToArray() ?? []);
            if (localizer.textMeshProUGUI.text == "")
                localizer.textMeshProUGUI.text = queuedText;
            text.color = Colors.normal;
            textTimer = queuedTime;
            queueShow?.Invoke(text, false);

            instance.displaying = true;
            animator.SetBool("New Text", false);
        }

        void OnExit() => instance.displaying = false;

        public static void ShowStatus(string text, float time = 5, Action<TextMeshProUGUI, bool> onShow = null, List<AxKReplacementPair> pairs = null)
        {
            if (!instance)
                return;

            instance.ShowStatus(text, time, onShow, pairs);
        }
        public static void Stop()
        {
            if (!instance)
                return;

            NeonNetwork.Logger.DebugMsg("Status Stop");

            instance.textTimer = -1;
            instance.animator.SetTrigger("Exit");
        }

        public void ShowStatus(string text, float time, Action<TextMeshProUGUI, bool> onShow, List<AxKReplacementPair> pairs = null, bool _ = false)
        {
            if (!instance)
                return;

            NeonNetwork.Logger.DebugMsg($"ShowStatus {text}");


            queuedText = text;
            queuedPairs = pairs;
            queueShow = onShow;
            queuedTime = time;

            animator.SetBool("New Text", true);
            if (instance.displaying)
                animator.SetTrigger("Exit");
            else
                animator.ResetTrigger("Exit");
        }
    }
}
