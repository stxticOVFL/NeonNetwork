using HarmonyLib;
using JetBrains.Annotations;
using MelonLoader.TinyJSON;
using NeonLite;
using NeonLite.Modules;
using Poly2Tri;
using SSUnity;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static LevelRush;

namespace NeonNetwork.Online
{
    internal class Leaderboard : IModule
    {
        // this class is going to both serve as a "LeaderboardIntegration" substitute and also a patcher for it
#pragma warning disable CS0414
#pragma warning disable CS0649
        const bool priority = false;
        const bool active = true;

        static bool customs = false;

        static Sprite texNNOnly;

        static void Activate(bool _)
        {
            Patching.AddPatch(typeof(Leaderboards), "GetUsername", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "SetModeLevelRush", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "SetModeGlobalNeonScore", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "SetLevel", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "DisplayScores_AsyncMakeRequest", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "GetScoreDataAtRank", ApplyReplacement, Patching.PatchTarget.Transpiler);
            Patching.AddPatch(typeof(Leaderboards), "OnLeaderboardUploaded", ApplyReplacement, Patching.PatchTarget.Transpiler);

            Patching.AddPatch(typeof(LeaderboardScore), "SetScore", PostSetScore, Patching.PatchTarget.Postfix);
            Patching.AddPatch(typeof(Leaderboards), "UpdateFilterButtons", PreFilter, Patching.PatchTarget.Prefix);
            Patching.AddPatch(typeof(Leaderboards), "DisableAllButtons", UpdateGUI, Patching.PatchTarget.Postfix);

            try
            {
                customs = typeof(CustomLevelData) != null;
            }
            catch { }

            texNNOnly = NeonNetwork.bundle.LoadAsset<Sprite>("Assets/Sprites/LBNetworkOnly.png");
        }

#if !XBOX
        static readonly Type target = typeof(LeaderboardIntegrationSteam);
#else
#endif

        static IEnumerable<CodeInstruction> ApplyReplacement(IEnumerable<CodeInstruction> instructions)
        {

            return new CodeMatcher(instructions)
                .MatchForward(true, new CodeMatch(x => x.opcode == OpCodes.Call && ((MethodInfo)x.operand).DeclaringType == target))
                .Repeat(x =>
                {
                    var m = (MethodInfo)x.Operand;
                    NeonNetwork.Logger.DebugMsg(m);
                    x.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, NeonLite.Helpers.Method(typeof(Leaderboard), m.Name)));
                    //x.Advance(1);
                })
                .InstructionEnumeration();
        }

        [Serializable]
        struct FetchRequest // "/"
        {
            public string level_id;

            public int skip;  // one or the other
            public string steam_id;

            public int take;
        }

        [Serializable]
        struct SubmitRequest // "/submit/"
        {
            public string steam_id;
            public string level_id;
            public long time;

            public int take;

            public string name;
            public string image;
        }

        [Serializable]
        class ScoreInfo
        {
            //public string id;

            public string steam_id;
            public string level_id;
            public long time;
            public int index;

            internal bool ourScore;
            internal int oldIndex;

            public string name;
            public string image;

            public ScoreData ToScoreData()
            {
                var ret = new ScoreData();
                ret._ranking = index;
                ret._scoreValueMilliseconds = Utils.ConvertMicrosecondsToMilliseconds(time);
                ret._oldRanking = -1;
                ret._username = Online.GetName(ulong.Parse(steam_id));
                ret._profilePicture = Online.GetPFP(ulong.Parse(steam_id));

                ret._userScore = ourScore;
                if (ourScore)
                    ret._oldRanking = oldIndex;
                else
                    ret._oldRanking = -1;

                ret._numLevelsCompleted = -1;
                ret._init = true;

                ret._medalValue = Helpers.GetMedalIndexSafely(currentLevel, time);

                return ret;
            }
        }


        [Serializable]
        struct FetchResponse // "/"
        {
            public int count;
            public ScoreInfo[] scores;
        }

        [Serializable]
        struct SubmitResponse // "/submit/"
        {
            public int old_index;
            public int count;
            public ScoreInfo[] scores;
        }

        static ScoreInfo[] currentScores;
        static Leaderboards currentLB = null;
        static LevelData currentLevel = null;
        static int currentStartIndex = -1;
        static int currentOldIndex = -1;

        static bool justUploaded = false;

        static bool lastCustomStage = false;
        static bool lastLBState = false;
        static bool IsCustomStage(LevelData level, Leaderboards lb)
        {
            bool ret = false;
            if (customs)
                ret = level is CustomLevelData;

            // convert the lb
            if (lb)
            {
                if (!lastCustomStage)
                    lastLBState = lb.bg.sprite == lb.bgGlobal;

                if (ret)
                    lb.OnFriendsButtonPressed();
                else if (lastCustomStage)
                {
                    if (lastLBState)
                        lb.OnGlobalButtonPressed();
                    else
                        lb.OnFriendsButtonPressed();
                }

                lastCustomStage = ret;
            }

            return ret;
        }
        static void SetVariables(Leaderboards lb, LevelData level, ScoreInfo[] scores = null, int startIndex = -1)
        {
            currentLB = lb;
            currentLevel = level;
            currentScores = scores;
            currentStartIndex = startIndex;
            currentOldIndex = -1;
        }

        static bool PreFilter(Leaderboards __instance, LevelData ___currentLevelData)
        {
            UpdateGUI(__instance, ___currentLevelData);
            return !IsCustomStage(___currentLevelData, null);
        }

        static void UpdateGUI(Leaderboards __instance, LevelData ___currentLevelData)
        {
            if (!IsCustomStage(___currentLevelData, null))
            {
                __instance.globalButton.gameObject.SetActive(true);
                __instance.friendsButton.gameObject.SetActive(true);
            }
            else
            {
                __instance.globalButton.gameObject.SetActive(false);
                __instance.friendsButton.gameObject.SetActive(false);
                __instance.bg.sprite = texNNOnly;
            }
        }

        static void PostSetScore(LeaderboardScore __instance, ScoreData newData)
        {
            if (!currentLB)
                return;

            var score = currentScores.FirstOrDefault(x => x.index == int.Parse(__instance._ranking.text));
            if (score == null)
                return;
            __instance._username.text = Online.GetName(ulong.Parse(score.steam_id), x => __instance._username.text = x);
            __instance._medal.sprite = CommunityMedals.Medals[newData._medalValue];
            CommunityMedals.AdjustMaterial(__instance._medal);
        }

#if !XBOX

        static bool GetUsername(out string username)
        {
            username = Online.GetName(Online.steamID.m_SteamID);
            return true;
        }

        static void UploadScore(LevelData level, Leaderboards lb, LeaderboardIntegrationSteam.LeaderboardLoadedCallback cb)
        {
            if (!IsCustomStage(level, lb))
            {
                SetVariables(null, null);
                LeaderboardIntegrationSteam.UploadScore(level, lb, cb);
                return;
            }
#if !DEBUG
            if (Anticheat.Active)
            {
                cb?.Invoke(true);
                return;
            }
#endif

            NeonNetwork.Logger.DebugMsg($"UploadScore {cb}");

            SetVariables(lb, level);

            SubmitRequest sub = new()
            {
                level_id = level.levelID,
                steam_id = Online.steamID.ToString(),
                time = GameDataManager.GetLevelStats(level.levelID).GetTimeBestMicroseconds(),
                take = lb.simultaneousScoreCount
            };

            var req = Online.Post("/submit", sub);
            var res = req.SendWebRequest();
            res.completed += _ =>
            {
                NeonNetwork.Logger.DebugMsg($"{req.result} {req.responseCode}");

                if (req.result != UnityWebRequest.Result.Success)
                {
                    cb?.Invoke(false, req.result == UnityWebRequest.Result.ConnectionError);
                    return;
                }

                NeonNetwork.Logger.DebugMsg(req.downloadHandler.text);

                var resp = JSON.Load(req.downloadHandler.text).Make<SubmitResponse>();

                lb.SetScoreCountGlobal(resp.count);
                lb.SetScoreCountFriends(resp.count);
                if (resp.count > 0)
                {
                    currentScores = resp.scores;
                    currentStartIndex = currentScores[0].index;
                    currentOldIndex = resp.old_index;

                    foreach (var score in currentScores)
                    {
                        if (score.steam_id == Online.steamID.ToString())
                        {
                            currentLB.SetFriendUserRanking(score.index);
                            currentLB.SetUserRanking(score.index);

                            score.ourScore = true;

                            if (score.index <= currentOldIndex)
                                currentOldIndex = -1;
                            score.oldIndex = currentOldIndex;
                            break;
                        }
                    }
                }

                justUploaded = true;
                cb?.Invoke(true);
            };
        }

        static void SetupLeaderboardForLevel(LevelData level, Leaderboards lb, LeaderboardIntegrationSteam.LeaderboardLoadedCallback cb)
        {
            if (!IsCustomStage(level, lb))
            {
                SetVariables(null, null);
                LeaderboardIntegrationSteam.SetupLeaderboardForLevel(level, lb, cb);
                return;
            }

            NeonNetwork.Logger.DebugMsg($"SetupLeaderboardForLevel {cb}");

            if (justUploaded)
            {
                // use that data!
                cb?.Invoke(true);
                justUploaded = false;
                return;
            }

            SetVariables(lb, level);
 
            FetchRequest sub = new()
            {
                level_id = currentLevel.levelID,
                steam_id = Online.steamID.ToString(),
                take = lb.simultaneousScoreCount
            };

            var req = Online.Post("/", sub);
            var res = req.SendWebRequest();

            res.completed += _ =>
            {
                NeonNetwork.Logger.DebugMsg($"{req.result} {req.responseCode}");

                if (req.result != UnityWebRequest.Result.Success)
                {
                    cb?.Invoke(false, req.result == UnityWebRequest.Result.ConnectionError);
                    return;
                }

                NeonNetwork.Logger.DebugMsg(req.downloadHandler.text);

                var resp = JSON.Load(req.downloadHandler.text).Make<FetchResponse>();
                lb.SetScoreCountGlobal(resp.count);
                lb.SetScoreCountFriends(resp.count);

                if (resp.count > 0)
                {
                    NeonNetwork.Logger.DebugMsg(resp.scores);

                    if (currentStartIndex == resp.scores[0].index)
                    {
                        cb?.Invoke(true);
                        return; // if upload already populated
                    }

                    currentScores = resp.scores;
                    currentStartIndex = currentScores[0].index;

                    foreach (var score in currentScores)
                    {
                        if (score.steam_id == Online.steamID.ToString())
                        {
                            currentLB.SetFriendUserRanking(score.index);
                            currentLB.SetUserRanking(score.index);

                            score.ourScore = true;
                            score.oldIndex = currentOldIndex;
                            break;
                        }
                    }
                }
                cb?.Invoke(true);
            };
        }

        static readonly MethodInfo onLBFound = NeonLite.Helpers.Method(typeof(Leaderboards), "OnLeaderboardFound");

        static void DownloadEntries(int start, int end, bool friend, bool globalNeonRankings)
        {
            if (!IsCustomStage(currentLevel, currentLB))
            {
                SetVariables(null, null);
                LeaderboardIntegrationSteam.DownloadEntries(start, end, friend, globalNeonRankings);
                return;
            }

            NeonNetwork.Logger.DebugMsg($"{currentStartIndex} {start} {currentScores}");

            if (currentScores == null)
                currentLB.DisplayScores_AsyncRecieve([], false); // honestly save the trouble
            else if (currentStartIndex == start)
            {
                // we don't have to fetch 
                currentLB.DisplayScores_AsyncRecieve([.. currentScores.Select(x => x.ToScoreData())], currentScores.Length > 0);
                return;
            }

            NeonNetwork.Logger.DebugMsg("DownloadEntries");

            currentScores = null;
            currentStartIndex = -1;

            FetchRequest sub = new()
            {
                level_id = currentLevel.levelID,
                skip = start - 1,
                take = end - start + 1
            };

            var req = Online.Post("/", sub);
            var res = req.SendWebRequest();

            res.completed += _ =>
            {
                NeonNetwork.Logger.DebugMsg($"{req.result} {req.responseCode}");
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onLBFound.Invoke(currentLB, [false, req.result == UnityWebRequest.Result.ConnectionError]);
                    return;
                }
                NeonNetwork.Logger.DebugMsg(req.downloadHandler.text);

                var resp = JSON.Load(req.downloadHandler.text).Make<FetchResponse>();
                currentLB.SetScoreCountGlobal(resp.count);
                currentLB.SetScoreCountFriends(resp.count);

                if (resp.count > 0)
                {
                    currentScores = resp.scores;
                    currentStartIndex = currentScores[0].index;

                    foreach (var score in currentScores)
                    {
                        if (score.steam_id == Online.steamID.ToString())
                        {
                            currentLB.SetFriendUserRanking(score.index);
                            currentLB.SetUserRanking(score.index);
                            score.ourScore = true;
                            score.oldIndex = currentOldIndex;
                            break;
                        }
                    }
                    currentLB.DisplayScores_AsyncRecieve([.. currentScores.Select(x => x.ToScoreData())], true);
                }
                else
                    currentLB.DisplayScores_AsyncRecieve([], false);

                //cb?.Invoke(true);
            };
        }

        static void SetupLeaderboardForLevelRush(Leaderboards lb, LevelRushType rush, bool heaven, LeaderboardIntegrationSteam.LeaderboardLoadedCallback cb)
        {
            IsCustomStage(null, lb);
            SetVariables(null, null);
            LeaderboardIntegrationSteam.SetupLeaderboardForLevelRush(lb, rush, heaven, cb);
        }
        static void UploadScore_GlobalNeonRank(Leaderboards lb, LeaderboardIntegrationSteam.LeaderboardLoadedCallback cb)
        {
            IsCustomStage(null, lb);
            SetVariables(null, null);
            LeaderboardIntegrationSteam.UploadScore_GlobalNeonRank(lb, cb);
        }
        static void UploadScore_LevelRush(LevelRush.LevelRushType rush, bool heaven, Leaderboards lb, LeaderboardIntegrationSteam.LeaderboardLoadedCallback cb)
        {
            IsCustomStage(null, lb);
            SetVariables(null, null);
            LeaderboardIntegrationSteam.UploadScore_LevelRush(rush, heaven, lb, cb);
        }
        static ScoreData GetScoreDataAtGlobalRank(int globalRank, bool friendsOnly, bool globalNeonRanking)
        {
            SetVariables(null, null);
            return LeaderboardIntegrationSteam.GetScoreDataAtGlobalRank(globalRank, friendsOnly, globalNeonRanking);
        }
#else
#endif

    }
}
