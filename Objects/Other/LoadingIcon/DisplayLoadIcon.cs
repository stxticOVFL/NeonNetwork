using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NeonNetwork.Objects
{
    internal class DisplayLoadIcon : MonoBehaviour
    {
        RawImage camView;
        RawImage spinner;
        readonly RawImage[] bars = new RawImage[3];

        readonly Transition spinnerT = new(1, AxKEasing.EaseInOutQuart, []);
        readonly Transition[] barsT = new Transition[3];

        public CanvasGroup group;

        bool running = false;

        public Action onDone;

        public static readonly List<DisplayLoadIcon> icons = [];

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            camView = transform.Find("Actual Loading Icon").GetComponent<RawImage>();
            spinner = transform.Find("Spinner").GetComponent<RawImage>();
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = spinner.transform.Find($"Bar{i}").GetComponent<RawImage>();
                bars[i].color = bars[i].color.Alpha(0);
            }
        }

        void Update()
        {
            if (group.alpha <= 0)
                return;
            spinner.transform.localEulerAngles = new Vector3(0, 0, -(spinnerT.result - 0));

            for (int i = 0; i < bars.Length; i++)
            {
                if (barsT[i] != null)
                {
                    bars[i].color = bars[i].color.Alpha(barsT[i].result);
                    barsT[i].Process();
                }
                else
                    bars[i].color = bars[i].color.Alpha(1);
            }
            
            spinnerT.Process();
        }

        void OnEnable() => icons.Add(this);
        void OnDisable() => icons.Remove(this);

        public void StartAnimation()
        {
            running = true;
            spinnerT.timestamps = new() { { 10f,
                () =>
                {
                    if (running)
                        barsT[0].Start(0, 1);
                    else onDone?.Invoke();
                }
            } };

            for (int i = 0; i < bars.Length; i++)
            {
                int i2 = i; // C# sucks 
                barsT[i] = new(5, AxKEasing.EaseInOutCirc, new() { 
                    { 0.6f, 
                        () => { 
                            if (barsT[i2].goal == 1) 
                                barsT[i2 + 1].Start(0, 1); 
                            } 
                    }, 
                    { 10f, 
                        () => { 
                            if (barsT[i2].goal == 1) 
                                barsT[i2].Start(1, 0); 
                            } 
                    } 
                });
            }
            barsT[bars.Length - 1].timestamps = new() { { 10f, OutLastBar } };

            spinnerT.Start(0, 360);
        }

        void OutLastBar()
        {
            barsT[bars.Length - 1].Start(1, 0);
            barsT[bars.Length - 1].timestamps = new()
            {
                { 0.9f,
                    () => {
                        if (running)
                            spinnerT.Start(0, 360);
                    }
                },
                { 10f, 
                    () => {
                        barsT[bars.Length - 1].timestamps = new() { { 10f, OutLastBar } };
                        if (!running)
                            onDone?.Invoke();
                    } 
                }
            };
        }
    }
}
