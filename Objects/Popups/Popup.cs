using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.Input;
using static MelonLoader.MelonLogger;

namespace NeonNetwork.Objects.Popups
{
    internal class Popup : MenuScreen
    {
        public static Popup instance;

        public bool shown;
        public bool transitioning;
        public bool leaving;
        public bool left;

        GameObject next;
        Type nextType;
        Action<Popup> onReady;

        public readonly Transition opacityT = new(2, Transition.opacityEase, []);
        public readonly Transition backgroundT = new(2, Transition.opacityEase, []);
        public readonly Transition popupTA = new(2, Transition.opacityEase, []);
        public readonly Transition popupTM = new(2, Transition.movementEase, []);
        public readonly Transition contentT = new(2, Transition.opacityEase, []);

        public readonly Transition popupTSX = new(3, Transition.movementEase, []);
        public readonly Transition popupTSY = new(3, Transition.movementEase, []);

        Canvas masterCanvas;

        public CanvasGroup allGroup;
        public CanvasGroup bgGroup;
        public CanvasGroup popupGroup;
        public CanvasGroup contentGroup;

        public RectTransform background;
        public RectTransform popupBack;
        public RectTransform contents;

        static bool paused;

        static bool ready;

        GameObject lastSelected;
        static public GameObject FromName(string name) => NeonNetwork.bundle.LoadAsset<GameObject>("Assets/Prefabs/Popups/" + name + ".prefab");
        static public T GetInstance<T>() where T : Popup => instance as T;

        static public void Setup()
        {
            if (!instance)
            {
                // create just to destroy
                // would find a better way to do this but :p
                var popup = FromName("Base");
                instance = Utils.InstantiateUI(popup, "Popup", NeonNetwork.nnMMHolder).AddComponent<Popup>();
            }
        }

        static readonly HashSet<MainMenu.State> dontPause = [MainMenu.State.Title, MainMenu.State.Map, MainMenu.State.Results];

        static public void ShowPopup<T>(string name, Action<Popup> ready = null) where T : Popup => ShowPopup(FromName(name), typeof(T), ready);
        static public void ShowPopup(GameObject popup, Type t, Action<Popup> ready = null)
        {
                NeonNetwork.Logger.DebugMsg($"ShowPopup {popup}");

            instance.lastSelected = EventSystem.current.currentSelectedGameObject;

            instance.SetVisible(true, false);

            if (instance.leaving)
            {
                // split the animations 
                var time = instance.opacityT.time;
                instance.backgroundT.Start(instance.opacityT.result, 1, forceT: time);
                instance.popupTA.Start(instance.opacityT.result, 0, forceT: time);
                instance.popupTA.timestamps = new() { { 10, () => instance.LeaveEnd() } };
                instance.opacityT.Stop(1);
                instance.next = popup;
                instance.nextType = t;
                instance.onReady = ready;
                instance.gameObject.SetActive(true);
                return;
            }

            RectTransform popupB = popup.transform.Find("Popup") as RectTransform;

            if (instance.shown && !instance.next)
            {
                // if shown but not leaving then we start the resize
                instance.contentT.Start(null, 0);
                instance.contentGroup.blocksRaycasts = false;
                instance.contentT.timestamps = new() { { 10, () => instance.Resize(popupB.sizeDelta.x, popupB.sizeDelta.y) } };
                instance.next = popup;
                instance.nextType = t;
                instance.onReady = ready;
                instance.transitioning = true;
                instance.gameObject.SetActive(true);
                return;
            }

            instance.contents.name = "-OLD";
            var canvas = instance.masterCanvas ?? instance.GetComponentInParent<Canvas>();
            Destroy(instance.contents.gameObject); // destroy the old contents GAMEOBJECT
            var newC = Utils.InstantiateUI(popupB.Find("Contents").gameObject, "Contents", instance.popupBack); // replace it with the NEW contents
            var obj = instance.gameObject;
            var wasShown = instance.shown;
            var wasLeft = instance.left;
            instance.Cleanup();
            Destroy(instance); // destroy the old popup OBJECT, not gameobject
            // this is ridiculous
            instance = (Popup)typeof(GameObject).GetMethod("AddComponent", []).MakeGenericMethod(t).Invoke(obj, null); // create the NEW popup OBJECT and set it
            instance.ResolveComponents();
            ready?.Invoke(instance);
                NeonNetwork.Logger.DebugMsg($"Spawned {instance}");

            instance.popupTSX.Set(popupB.sizeDelta.x);
            instance.popupTSY.Set(popupB.sizeDelta.y);
            instance.popupBack.sizeDelta = popupB.sizeDelta;

            instance.transform.localPosition = Vector3.zero;

            paused = MainMenu.Instance().GetIsPaused();
            if (!dontPause.Contains(MainMenu.Instance().GetCurrentState()) && !paused)
                MainMenu.Instance().PauseGame(true, true, true);

            instance.shown = true;
            if (!wasShown)
            {
                instance.opacityT.Start(0, 1);
                instance.popupTM.Start(20, 0);
                instance.popupBack.localPosition = canvas.ViewportToCanvasPosition(new Vector3(0.5f, 0.5f, 0)) + new Vector3(0, instance.popupTM.result, 0);
            }
            else
            {
                instance.opacityT.Set(1);
                instance.allGroup.alpha = 1;
                instance.allGroup.blocksRaycasts = instance.allGroup.interactable = true;
                if (wasLeft)
                {
                    instance.popupTA.Start(0, 1);
                    instance.popupTM.Start(20, 0);
                    instance.popupBack.localPosition = canvas.ViewportToCanvasPosition(new Vector3(0.5f, 0.5f, 0)) + new Vector3(0, instance.popupTM.result, 0);
                }
                else
                    instance.contentT.Start(0, 1);
            }

            Canvas.ForceUpdateCanvases();
        }

        public override void OnSetVisible(bool _) { }

        protected void Awake()
        {
            allGroup = GetComponent<CanvasGroup>();
            _canvasGroup = allGroup;

            if (ready)
                SetVisible(true, false);
            else
                ready = true;

                NeonNetwork.Logger.DebugMsg($"Popup Awake {this}");

            background = transform.Find("BG") as RectTransform;
            bgGroup = background.GetComponent<CanvasGroup>();

            popupBack = transform.Find("Popup") as RectTransform;
            popupGroup = popupBack.GetComponent<CanvasGroup>();

            contents = popupBack.Find("Contents") as RectTransform;
            contentGroup = contents.GetComponent<CanvasGroup>();

            opacityT.Set(0);
            allGroup.alpha = opacityT.result;
            allGroup.interactable = allGroup.blocksRaycasts = false;

            backgroundT.Set(1);
            popupTA.Set(1);
            contentT.Set(1);
            popupTSX.Set(popupBack.sizeDelta.x);
            popupTSY.Set(popupBack.sizeDelta.y);

            shown = false;

            NeonLite.Helpers.Field(typeof(MenuScreen), "_sortingOrder").SetValue(this, 10000);
        }

        protected void Update()
        {
            masterCanvas = GetComponentInParent<Canvas>();

            allGroup.alpha = opacityT.result;
            bgGroup.alpha = backgroundT.result;
            popupGroup.alpha = popupTA.result;
            contentGroup.alpha = contentT.result;

            allGroup.interactable = allGroup.blocksRaycasts = !(opacityT.result == 0 || (opacityT.goal == 0 && opacityT.running));

            popupBack.localPosition = masterCanvas.ViewportToCanvasPosition(new Vector3(0.5f, 0.5f, 0)) + new Vector3(0, popupTM.result, 0);
            popupBack.sizeDelta = new Vector2(popupTSX.result, popupTSY.result);
            background.localPosition = masterCanvas.ViewportToCanvasPosition(Vector3.zero);
            background.sizeDelta = masterCanvas.ViewportToCanvasPosition(Vector3.one * 2);

            contentT.speed = transitioning ? 3 : 2;

            Transition.ProcessAll(this);
        }

        public static void Finish() => instance.Leave();
        public void Leave()
        {
            if (leaving || !shown)
                return;
            if (!dontPause.Contains(MainMenu.Instance().GetCurrentState()) && !paused)
                MainMenu.Instance().PauseGame(false, false, false);

            leaving = true;
            SetVisible(false, false);
            opacityT.Start(null, 0);
            popupTM.Start(null, popupTM.result - 20);
            opacityT.timestamps = new() { { 10, LeaveEnd } };
        }

        void LeaveEnd()
        {
            leaving = false;

            if (next)
            {
                left = true;
                ResizeEnd();
                return;
            }

            shown = false;
            SetVisible(false, false, false);
            EventSystem.current.SetSelectedGameObject(lastSelected);
        }

        void Resize(float x, float y)
        {
            popupTSX.Start(null, x);
            popupTSY.Start(null, y);
            popupTSX.timestamps = new() { { 10, ResizeEnd } };
        }

        void ResizeEnd() => ShowPopup(next, nextType, onReady);

        public T AddComponent<T>(string comp, string name, Transform parent = null) where T : MonoBehaviour
        {
            var prefab = NeonNetwork.bundle.LoadAsset<GameObject>($"Assets/Prefabs/Popups/Components/{comp}.prefab");
            T ret = Utils.InstantiateUI(prefab, name, parent).AddComponent<T>();
            if (typeof(T) == typeof(Components.Button))
            {
                (ret as Components.Button).SetKey(name);
                if (parent == null)
                    ret.transform.SetParent(contents.Find("ButtonsHolder").Find("B"), false);
            }
            return ret;
        }

        void ResolveComponents()
        {
            foreach (var button in contents.GetComponentsInChildren<Button>())
            {
                if (button.transform.parent.Find("BGMask"))
                    SetupComponent(button.transform.parent.GetOrAddComponent<Components.StageButton>());
                else
                    SetupComponent(button.transform.parent.GetOrAddComponent<Components.Button>());
            }
            foreach (var input in contents.GetComponentsInChildren<TMP_InputField>())
                SetupComponent(input.transform.parent.GetOrAddComponent<Components.Input>());
        }

        virtual protected void SetupComponent<T>(T component) where T : MonoBehaviour { }
        virtual protected void Cleanup() { }
    }
}
