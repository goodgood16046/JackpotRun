using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JackpotRun.UI2;
using UnityEngine;
using UnityEngine.Networking;

namespace JackpotRun.Game
{
    // 글로벌 랭킹 RTDB REST 어댑터 — ENGINE_PORT_DESIGN.md S15 "RankingService.cs": 앱·웹 공용
    // 잭팟런 랭킹 보드(jackpotrank/$pid, 카톡 봇 리더보드와는 별개)에 개인 최고 기록을 올리고
    // (TrySubmitBest) 전체 랭킹을 읽어온다(Fetch). 저장은 없다 — 원본은 항상 RTDB, 로컬 PlayerPrefs는
    // "직전에 성공적으로 올린 값"만 캐시해 중복 PUT을 피한다.
    public static class RankingService
    {
        // 콘솔 표시명 "JackpotRun" = 프로젝트 ID jackpotrun-web(2026-08-03 실측, S15 배경 참조).
        // 프로젝트를 옮기면 이 상수 하나만 교체하면 된다.
        private const string DbUrl = "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app";
        private const string Node = "jackpotrank";

        private const string PidPrefKey = "jackpotrun_pid";
        private const string SentScorePrefKey = "jackpotrun_rank_sent_score";
        private const string SentNickPrefKey = "jackpotrun_rank_sent_nick";

        private const int TimeoutSeconds = 10;

        /// <summary>기기(설치)별 고정 식별자 — 없으면 GUID "N"(32자, 하이픈 없음)을 생성해 저장한다.</summary>
        public static string PlayerId()
        {
            string pid = PlayerPrefs.GetString(PidPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(pid)) return pid;

            pid = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PidPrefKey, pid);
            PlayerPrefs.Save();
            return pid;
        }

        // PUT 바디용 DTO — JsonUtility.ToJson으로 그대로 직렬화된다(DB 계약: nick/score/stage/ts).
        [Serializable]
        private sealed class SubmitDto
        {
            public string nick;
            public long score;
            public long stage;
            public long ts;
        }

        /// <summary>Fetch가 돌려주는 랭킹 한 줄. pid는 응답 JSON의 키(=RTDB 노드 이름)에서 채운다.</summary>
        public sealed class Entry
        {
            public string pid;
            public string nick;
            public long score;
            public long stage;
            public long ts;
        }

        /// <summary>현재 프로필의 BestScore/BestStage를 랭킹 보드에 올린다 — host는 코루틴을 돌릴
        /// MonoBehaviour(AppRoot.RegisterIntro가 DontDestroyOnLoad인 AppRoot 자신을 넘겨 씬 전환에도
        /// 끊기지 않는다). 마지막으로 성공 제출한 값(PlayerPrefs)보다 BestScore가 크거나 닉네임이
        /// 바뀌었을 때만 실제로 PUT한다 — 그 외에는 조용히 스킵.</summary>
        public static void TrySubmitBest(MonoBehaviour host)
        {
            if (host == null) return;
            var profile = AppRoot.Instance?.Profile;
            if (profile == null) return;

            string nick = LoginView.SavedNick();
            if (string.IsNullOrEmpty(nick)) return;
            if (profile.BestScore <= 0) return;

            long sentScore = 0;
            string sentScoreRaw = PlayerPrefs.GetString(SentScorePrefKey, string.Empty);
            if (!string.IsNullOrEmpty(sentScoreRaw)) long.TryParse(sentScoreRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out sentScore);
            string sentNick = PlayerPrefs.GetString(SentNickPrefKey, string.Empty);

            if (profile.BestScore <= sentScore && nick == sentNick) return; // 갱신할 게 없음

            host.StartCoroutine(SubmitRoutine(PlayerId(), nick, profile.BestScore, profile.BestStage));
        }

        private static IEnumerator SubmitRoutine(string pid, string nick, long score, long stage)
        {
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string json = JsonUtility.ToJson(new SubmitDto { nick = nick, score = score, stage = stage, ts = ts });

            using (var req = UnityWebRequest.Put($"{DbUrl}/{Node}/{pid}.json", json))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = TimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    PlayerPrefs.SetString(SentScorePrefKey, score.ToString(CultureInfo.InvariantCulture));
                    PlayerPrefs.SetString(SentNickPrefKey, nick);
                    PlayerPrefs.Save();
                }
                else
                {
                    // prefs를 건드리지 않으므로 다음 TrySubmitBest 호출(다음 화면 진입 등)에서 자동 재시도된다.
                    Debug.Log($"[RankingService] 랭킹 제출 실패: {req.error}");
                }
            }
        }

        /// <summary>전체 랭킹을 1회 조회한다(정렬: score 내림차순, 동점 ts 오름차순 — 먼저 세운 기록이
        /// 위). host가 파괴/비활성이면(화면을 이미 떠났으면) 콜백을 호출하지 않는다.</summary>
        public static void Fetch(MonoBehaviour host, Action<List<Entry>> onOk, Action<string> onError)
        {
            if (host == null) return;
            host.StartCoroutine(FetchRoutine(host, onOk, onError));
        }

        private static IEnumerator FetchRoutine(MonoBehaviour host, Action<List<Entry>> onOk, Action<string> onError)
        {
            using (var req = UnityWebRequest.Get($"{DbUrl}/{Node}.json"))
            {
                req.timeout = TimeoutSeconds;
                yield return req.SendWebRequest();

                if (host == null) yield break; // 응답이 오는 동안 화면이 꺼짐 — 콜백 호출하지 않음

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.error);
                    yield break;
                }

                string body = req.downloadHandler.text;
                if (string.IsNullOrEmpty(body) || body == "null")
                {
                    onOk?.Invoke(new List<Entry>());
                    yield break;
                }

                if (!(MiniJson.Parse(body) is Dictionary<string, object> root))
                {
                    onError?.Invoke("응답 형식 오류");
                    yield break;
                }

                var list = new List<Entry>();
                foreach (var kv in root)
                {
                    if (TryParseEntry(kv.Key, kv.Value, out var entry)) list.Add(entry);
                }

                list.Sort((a, b) =>
                {
                    int byScore = b.score.CompareTo(a.score); // 내림차순
                    return byScore != 0 ? byScore : a.ts.CompareTo(b.ts); // 동점 — 먼저 세운(ts 작은) 쪽 위
                });

                onOk?.Invoke(list);
            }
        }

        private static bool TryParseEntry(string pid, object raw, out Entry entry)
        {
            entry = null;
            if (!(raw is Dictionary<string, object> obj)) return false;
            if (!obj.TryGetValue("nick", out var nickObj) || !(nickObj is string nick)) return false;
            if (!obj.TryGetValue("score", out var scoreObj) || !(scoreObj is double score)) return false;
            if (!obj.TryGetValue("stage", out var stageObj) || !(stageObj is double stage)) return false;
            if (!obj.TryGetValue("ts", out var tsObj) || !(tsObj is double ts)) return false;

            entry = new Entry { pid = pid, nick = nick, score = (long)score, stage = (long)stage, ts = (long)ts };
            return true;
        }
    }
}
