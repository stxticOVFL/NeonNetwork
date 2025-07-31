using I2.Loc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Popups
{
    internal class StageSelector : Popup
    {
        public object passback;

        Action onClose;
        public Action<StageSelector, LevelData> onSubmit;

        public static void Show(Action<Popup> onReady = null, Action onClose = null, Action<StageSelector, LevelData> onSubmit = null) {
            ShowPopup<StageSelector>("StageSelector", (popup) =>
            {
                onReady?.Invoke(popup);
                (popup as StageSelector).onClose = onClose;
                (popup as StageSelector).onSubmit = onSubmit;
                (popup as StageSelector).Ready();
            });
        }

        void Ready()
        {
            var scrollView = contents.transform.GetChild(0).GetChild(0).GetComponent<ScrollRect>();
            var content = scrollView.content;
            var chapterC = content.GetChild(0).GetOrAddComponent<ChapterContent>();
            chapterC.gameObject.SetActive(false);

            var gd = Singleton<Game>.Instance.GetGameData();
            foreach (var campaign in gd.campaigns)
            {
                foreach (var m in campaign.missionData)
                {
                    var newCh = Utils.InstantiateUI(chapterC.gameObject, m.missionID, content).GetComponent<ChapterContent>();
                    newCh.gameObject.SetActive(true);
                    newCh.Setup(this, m);
                }
            }

            scrollView.verticalScrollbar.value = 0;
        }

        class ChapterContent : MonoBehaviour
        {
            TextMeshProUGUI chapterText;
            StageSelector selector;
            Components.StageButton buttonC;
            Transform buttonGrid;

            void Awake()
            {
                chapterText = transform.Find("ChapName").GetComponent<TextMeshProUGUI>();
                buttonGrid = transform.Find("ButtonGrid");
                buttonC = buttonGrid.GetChild(0).GetOrAddComponent<Components.StageButton>();
                buttonC.gameObject.SetActive(false);
            }

            public void Setup(StageSelector s, MissionData chapter)
            {
                selector = s;
                chapterText.text = LocalizationManager.GetTranslation(chapter.missionDisplayName) ?? chapter.missionDisplayName;
                foreach (var l in chapter.levels)
                {
                    var l2 = l;
                    var button = Utils.InstantiateUI(buttonC.gameObject, l.levelID, buttonGrid).GetComponent<Components.StageButton>();
                    button.gameObject.SetActive(true);
                    button.SetStage(l);
                    button.onClickEvent.AddListener(() =>
                    {
                        selector.contentGroup.interactable = selector.contentGroup.blocksRaycasts = false;
                        selector.onSubmit.Invoke(selector, l2);
                    });
                }
            }
        }
    }
}
