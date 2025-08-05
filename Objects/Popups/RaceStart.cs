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
                    var tuple = ((string, string, string))selector.passback;
                    (popup as RaceStart).time.input.text = tuple.Item1;
                    (popup as RaceStart).leniency.input.text = tuple.Item2;
                    (popup as RaceStart).countdown.input.text = tuple.Item3;
                    (popup as RaceStart).stageButton.SetStage(level);
                });
            });
        }

        void Submit()
        {
            var raceDuration = float.Parse(time.input.text) * 60;
            var raceLeniency = leniency.input.text == "" ? 0 : int.Parse(leniency.input.text);
            var raceCountdown = countdown.input.text == "" ? 10 : int.Parse(countdown.input.text);
            Rooms.CallRace(stageButton.level, raceDuration, raceLeniency, raceCountdown);
            Leave();
        }

    }
}
