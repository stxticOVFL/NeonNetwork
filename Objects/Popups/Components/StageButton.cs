using I2.Loc;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects.Popups.Components
{
    internal class StageButton : Button
    {
        Image bgImage;
        public LevelData level;

        new void Awake()
        {
            base.Awake();
            transform.Find("Graphic").SetAsLastSibling();
            bgImage = transform.Find("BGMask").GetChild(0).GetComponent<Image>();
        }

        public void SetStage(LevelData l)
        {
            level = l;
            if (level == null)
            {
                label.text = "Select Level";
                bgImage.color = Color.clear;
            }
            else
            {
                string localized = LocalizationManager.GetTranslation(level.GetLevelDisplayName());
                if (string.IsNullOrEmpty(localized))
                    localized = level.levelDisplayName;
                label.text = localized;
                bgImage.color = Color.white.Alpha(0.3f);
                bgImage.sprite = level.GetPreviewImage();
            }
        }
    }
}
