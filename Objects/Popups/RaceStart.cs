using NeonNetwork.Online;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.EventSystems;

namespace NeonNetwork.Objects.Popups
{
    internal class RaceStart : Popup
    {
        public Components.Input time;
        public Components.Input leniency;
        public Components.Input countdown;
        public Components.Button startButton;
        public Components.StageButton stageButton;

        static float savedTime = -1;
        static int savedLeniency = -1;
        static int savedCountdown = -1;

        bool ignore = false;

        override protected void SetupComponent<T>(T component)
        {
            var button = component as Components.Button;
            if (button == null)
            {
                var input = component as Components.Input;
                switch (component.name)
                {
                    case "Time":
                        time = input; return;
                    case "Leniency":
                        leniency = input; return;
                    case "Countdown":
                        countdown = input; return;
                }
                return;
            }

            switch (component.name)
            {
                case "StageButton":
                    stageButton = button as Components.StageButton;
                    stageButton.SetStage(null);
                    button.onClickEvent.AddListener(PrepareStageSelect);
                    break;
                case "Start":
                    startButton = button;
                    button.onClickEvent.AddListener(Submit);
                    break;
                case "Cancel":
                    button.onClickEvent.AddListener(Leave);
                    break;
            }
        }

        void Start()
        {
            if (ignore)
                return;
            if (savedTime != -1)
                time.input.text = savedTime.ToString();
            if (savedLeniency != -1)
                leniency.input.text = savedLeniency.ToString();
            if (savedCountdown != -1)
                countdown.input.text = savedCountdown.ToString();
        }

        new void Update()
        {
            base.Update();
            startButton.ShouldBeInteractable = stageButton.level != null &&
                                                (time.input.text == "" ||
                                                int.Parse(time.input.text) >= 0) &&
                                                (countdown.input.text == "" ||
                                                int.Parse(countdown.input.text) >= 0);
        }

        void PrepareStageSelect()
        {
            StageSelector.Show((popup) =>
            {
                (popup as StageSelector).passback = (time.input.text, leniency.input.text, countdown.input.text);
            }, null, (selector, level) =>
            {
                ShowPopup<RaceStart>("RaceStart", (popup) =>
                {
                    var r = popup as RaceStart;
                    var tuple = ((string, string, string))selector.passback;
                    r.time.input.text = tuple.Item1;
                    r.leniency.input.text = tuple.Item2;
                    r.countdown.input.text = tuple.Item3;
                    r.ignore = true;
                    r.stageButton.SetStage(level);
                });
            });
        }

        void Submit()
        {
            var raceDuration = float.Parse(time.input.text) * 60;
            var raceLeniency = leniency.input.text == "" ? 0 : int.Parse(leniency.input.text);
            var raceCountdown = countdown.input.text == "" ? 10 : int.Parse(countdown.input.text);
            Rooms.CallRace(stageButton.level, raceDuration, raceLeniency, raceCountdown);

            savedTime = raceDuration / 60;
            savedLeniency = raceLeniency;
            savedCountdown = raceCountdown;

            Leave();
        }

    }
}
