
#if DEBUG
#define ENABLE_PROFILER
#endif

using I2.Loc;
using MelonLoader;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;


namespace NeonNetwork
{
    public class Pair<T1, T2>(T1 first, T2 second)
    {
        public T1 First { get; set; } = first;
        public T2 Second { get; set; } = second;
    }

    public static class Extensions
    {
        public static Vector3 ScreenToCanvasPosition(this Canvas canvas, Vector3 screenPosition)
        {
            var viewportPosition = new Vector3(screenPosition.x / Screen.width,
                                               screenPosition.y / Screen.height,
                                               0);
            return canvas.ViewportToCanvasPosition(viewportPosition);
        }
        public static Vector3 ViewportToCanvasPosition(this Canvas canvas, Vector3 viewportPosition)
        {
            var centerBasedViewPortPosition = viewportPosition - new Vector3(0.5f, 0.5f, 0);
            var canvasRect = canvas.GetComponent<RectTransform>();
            var scale = canvasRect.sizeDelta;
            return Vector3.Scale(centerBasedViewPortPosition, scale);
        }

        public static void OnAllChildren(this Transform transform, Action<Transform> callback)
        {
            if (transform.childCount == 0)
                callback(transform);
            else
            {
                foreach (Transform child in transform)
                    child.OnAllChildren(callback);
                callback(transform);
            }
        }

        // https://stackoverflow.com/a/12389412
        public static IEnumerable<IEnumerable<TValue>> Chunk<TValue>(
            this IEnumerable<TValue> values,
            int chunkSize)
        {
            return values
                   .Select((v, i) => new { v, groupIndex = i / chunkSize })
                   .GroupBy(x => x.groupIndex)
                   .Select(g => g.Select(x => x.v));
        }
    }

    public class Transition(float speed, Func<float, float, float, float> ease, Dictionary<float, Action> timestamps)
    {
        public Dictionary<float, Action> timestamps = timestamps;
        Func<float, float, float, float> easeFunc = ease;
        List<float> hit = [];

        public bool skip = false;
        public float start;
        public float goal;
        public float result;
        public bool running;
        public float speed = speed;
        public float time;

        float speedMult;

        public static readonly Func<float, float, float, float> opacityEase = AxKEasing.EaseOutQuart;
        public static readonly Func<float, float, float, float> movementEase = AxKEasing.EaseOutCubic;


        public void Start(float? s, float g, bool cont = true, float forceT = 0)
        {
            speedMult = 1;
            start = s ?? result;
            result = start;
            goal = g;
            hit = [];
            if ((forceT != 0) || (cont && !s.HasValue && running && time != 0))
                speedMult = 1 / (forceT == 0 ? time : forceT);
            time = 0;
            running = true;
        }

        public void Set(float value) => Stop(value);
        public void Stop(float? value = null)
        {
            time = 1;
            running = false;
            goal = value ?? result;
            Process();
        }

        void CheckTS()
        {
            foreach (var kp in timestamps.Where(kp => kp.Key <= time && !hit.Contains(kp.Key)))
            {
                hit.Add(kp.Key);
                kp.Value.Invoke();
            }
        }

        public void Process(bool skipTS = false)
        {
            if (time == 1f)
            {
                result = goal;
                bool wasRunning = running;
                running = false;
                if (wasRunning && timestamps.TryGetValue(10f, out var finish))
                    finish();
            }
            if (!running)
                return;
            time = Math.Min(1f, time + (Time.unscaledDeltaTime * speedMult * speed));
            result = easeFunc(start, goal, time);
            if (!skipTS)
                CheckTS();
        }

        public static void ProcessAll(object t)
        {
            var fields = t.GetType().GetFields(HarmonyLib.AccessTools.all);
            var transitions = fields.Where(f => f.FieldType == typeof(Transition));
            foreach (var transition in transitions.Select(f => (Transition)f.GetValue(t)))
            {
                //NeonNetwork.Logger.Msg($"{transition} {t}");
                transition.Process();
            }
        }
    }

    internal static class Localization
    {
        public static AxKLocalizedText Setup(Component component, int fontBase = -1, List<AxKReplacementPair> pairs = null)
        {
            var ret = NeonLite.Modules.Localization.SetupUI(component, fontBase);
            if (LocalizationManager.TryGetTranslation(ret.textMeshProUGUI.text, out _))
                ret.SetKey(ret.textMeshProUGUI.text, pairs?.ToArray() ?? []);
            return ret;
        }
        public static AxKLocalizedText Setup(GameObject obj, int fontBase = -1, List<AxKReplacementPair> pairs = null) => Setup(obj.transform, fontBase, pairs);
    }

    internal static class Helpers
    {
        public static int GetMedalIndexSafely(LevelData level, long time = -1)
        {
            if (CommunityMedals.medalTimes.ContainsKey(level.levelID))
                return CommunityMedals.GetMedalIndex(level.levelID, time);

            var stats = GameDataManager.GetLevelStats(level.levelID);

            if (time == -1)
            {
                if (!stats.GetCompleted())
                    return -1;
                time = stats._timeBestMicroseconds;
            }

            List<long> times = [
                long.MaxValue,
                Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeSilver()),
                Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeGold()),
                Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeAce()),
                Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeDev()),
            ];

            for (int i = times.Count - 1; i >= 0; i--)
            {
                if (time <= times[i])
                    return i;
            }
            return 0;

        }

        static readonly Stack<ProfilerMarker> currentMarkers = [];
        static readonly Stack<Tuple<string, Stopwatch>> currentWatches = [];

        static bool profiling = true;

        [Conditional("ENABLE_PROFILER")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableProfiling(bool enable) => profiling = enable;

        [Conditional("ENABLE_PROFILER")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartProfiling(string name)
        {
            if (!profiling)
                return;
            if (NeonNetwork.DEBUG)
                currentWatches.Push(new(name, new Stopwatch()));
            currentMarkers.Push(new(ProfilerCategory.Scripts, name));
            currentMarkers.Peek().Begin();
            if (NeonNetwork.DEBUG)
            {
                //NeonNetwork.Logger.Msg($"{name} - START");
                currentWatches.Peek().Item2.Start();
            }
        }
#if ENABLE_PROFILER
        public static IEnumerable<T> ProfileLoop<T>(this IEnumerable<T> loop, string name)
        {
            StartProfiling(name);
            int i = 0;
            foreach (T t in loop)
            {
                StartProfiling($"{name}#{++i}");
                yield return t;
                EndProfiling();
            }
            EndProfiling();
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> ProfileLoop<T>(this IEnumerable<T> loop, string _) => loop;
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_PROFILER")]
        public static void EndProfiling(string _) => EndProfiling();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_PROFILER")]
        public static void EndProfiling()
        {
            if (!profiling)
                return;

            currentMarkers.Pop().End();
            if (NeonNetwork.DEBUG)
            {
                (var name, var watch) = currentWatches.Pop();
                watch.Stop();
                //NeonNetwork.Logger.Msg($"{name} - {watch.Elapsed.TotalMilliseconds}ms");
            }
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, string msg)
        {
            if (NeonNetwork.DEBUG)
            {
                log.Msg(msg);
                UnityEngine.Debug.Log($"[NeonLite] {msg}");
            }
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, object obj) => DebugMsg(log, obj.ToString());
    }
}
