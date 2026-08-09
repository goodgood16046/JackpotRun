using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.UI2;
using UnityEngine;
using UnityEngine.Networking;

namespace JackpotRun.Game
{
    // 글로벌 랭킹 RTDB REST 어댑터 — 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #20 "랭킹 3노드") 전환.
    // 웹 rank.js와 정합: 일반/승천(심화 학기)/심화(심볼 덱) 3보드를 노드 slotrank/slotrank_asc/
    // slotrank_deep에 분리 기록한다(웹 submitScore/submitAscScore/submitDeepScore·topScores/
    // topAscScores/topDeepScores 그대로). 구 단일 노드 `jackpotrank`는 폐기 — 이 슬라이스부터 더는
    // 쓰지 않는다(기록이 사실상 없는 미출시 단계라 마이그레이션은 생략, WORKLOG.md에 기재).
    //
    // 게스트 키 = cid: 웹 myCid()는 브라우저 localStorage에 `Math.random().toString(36)+Date.now()...`
    // 형태의 임의 영숫자 문자열을 저장해 RTDB 키로 쓴다(로그인 시 u_<uid>로 이관). Unity는 이미
    // PlayerId()가 PlayerPrefs "jackpotrun_pid"에 GUID "N"(32자 16진수, 하이픈 없음)을 저장해 왔다 —
    // RTDB 키 제약(".", "#", "$", "[", "]", "/" 금지)에 16진수 GUID는 걸리는 문자가 전혀 없어 형식
    // 호환 확인 완료. Unity에는 계정 로그인(Google 등)이 없어(LoginView는 닉네임 저장 전용, S8 각주)
    // u_<uid> 경로는 대상이 아니다 — 항상 게스트(cid=PlayerId()) 키만 쓴다.
    //
    // Opus 2차검수(P7-4b) [중대⑤②] — 저장은 여전히 없다(원본은 항상 RTDB)지만, 로컬 PlayerPrefs
    // 캐시로 "직전에 성공 제출한 값"을 기억해 두고 그것만 보고 PUT 여부를 판단하던 이전 방식은 캐시가
    // 실제 원격 상태와 어긋날 수 있는 경로가 여럿이라(앱 재설치로 PlayerPrefs만 날아감·이전 PUT은
    // 성공했지만 그 직후 로컬 prefs 저장이 실패·기기 복제 등) 폐기했다 — 웹 rank.js submitScore/
    // submitAscScore/submitDeepScore와 완전히 동일하게 "매번 원격을 먼저 읽고, 그 값보다 높을 때만
    // 점수를 갱신하며, 낮거나 같으면 닉네임만(바뀌었을 때) 갱신"하는 방식으로 전환했다(GET 1회 +
    // 조건부 PUT 1회 — 로컬 캐시로 GET을 생략하던 이전 최적화는 정확성을 위해 포기).
    public static class RankingService
    {
        // 콘솔 표시명 "JackpotRun" = 프로젝트 ID jackpotrun-web(2026-08-03 실측, S15 배경 참조).
        private const string DbUrl = "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app";

        private const string PidPrefKey = "jackpotrun_pid";
        private const int TimeoutSeconds = 10;

        public enum RankBoard { Normal, Asc, Deep }

        private static string NodeOf(RankBoard board)
        {
            switch (board)
            {
                case RankBoard.Asc: return "slotrank_asc";
                case RankBoard.Deep: return "slotrank_deep";
                default: return "slotrank";
            }
        }

        /// <summary>기기(설치)별 고정 식별자 — 없으면 GUID "N"(32자, 하이픈 없음)을 생성해 저장한다.
        /// 3보드 전부 이 값을 게스트 키(=웹 myCid())로 공유한다.</summary>
        public static string PlayerId()
        {
            string pid = PlayerPrefs.GetString(PidPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(pid)) return pid;

            pid = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PidPrefKey, pid);
            PlayerPrefs.Save();
            return pid;
        }

        // PUT 바디용 DTO — JsonUtility.ToJson으로 그대로 직렬화된다. 일반/심화 보드는 {nick,score,
        // stage,ts}(웹 submitScore/submitDeepScore), 승천 보드만 asc 필드가 추가된다(웹 submitAscScore).
        [Serializable]
        private sealed class SubmitDto
        {
            public string nick;
            public long score;
            public long stage;
            public long ts;
        }

        [Serializable]
        private sealed class SubmitAscDto
        {
            public string nick;
            public long score;
            public long stage;
            public int asc;
            public long ts;
        }

        /// <summary>Fetch가 돌려주는 랭킹 한 줄. pid는 응답 JSON의 키(=RTDB 노드 이름)에서 채운다.
        /// asc는 승천 보드에서만 의미 있음(그 외 보드는 항상 0).</summary>
        public sealed class Entry
        {
            public string pid;
            public string nick;
            public long score;
            public long stage;
            public int asc;
            public long ts;
        }

        /// <summary>현재 프로필의 3보드(일반/승천/심화) 최고 기록을 각각의 랭킹 노드에 올린다 — host는
        /// 코루틴을 돌릴 MonoBehaviour(AppRoot.RegisterIntro가 DontDestroyOnLoad인 AppRoot 자신을
        /// 넘겨 씬 전환에도 끊기지 않는다). 점수가 0인 보드는 제출 대상에서 제외(웹 파리티 P7-4 §범위
        /// "런 종료 자동(각 보드 점수&gt;0)"). 실제 PUT 여부는 각 보드가 원격을 먼저 조회해(SubmitRoutine)
        /// "그 값보다 우리가 더 높은가/닉네임만 바뀌었는가"로 스스로 결정한다 — 이 메서드는 그 조회·판단을
        /// 매번 새로 트리거할 뿐 로컬 캐시로 미리 걸러내지 않는다(웹과 동일하게 항상 원격이 기준).</summary>
        public static void TrySubmitAll(MonoBehaviour host)
        {
            if (host == null) return;
            var profile = AppRoot.Instance?.Profile;
            if (profile == null) return;

            string nick = LoginView.SavedNick();
            if (string.IsNullOrEmpty(nick)) return;

            TrySubmitBoard(host, RankBoard.Normal, nick, profile.BestScore, profile.BestStage, 0);
            TrySubmitBoard(host, RankBoard.Asc, nick, profile.BestAscScore, profile.BestAscStage, profile.BestAscLevel);
            TrySubmitBoard(host, RankBoard.Deep, nick, profile.BestDeepScore, profile.BestDeepStage, 0);
        }

        private static void TrySubmitBoard(MonoBehaviour host, RankBoard board, string nick, long score, long stage, int asc)
        {
            if (score <= 0) return; // 웹 파리티 §범위 "각 보드 점수>0" — 미달성 보드는 제출하지 않는다.
            host.StartCoroutine(SubmitRoutine(board, PlayerId(), nick, score, stage, asc));
        }

        // 웹 rank.js submitScore/submitAscScore/submitDeepScore 그대로 — ① 원격에서 이 pid의 현재
        // 행을 먼저 읽고 ② 없거나 우리 점수가 더 높으면 전체 갱신(ts도 새로) ③ 있고 우리 점수가 같거나
        // 낮으면 닉네임이 달라졌을 때만(웹 `if (prev.nick !== nick)`) 기존 score/stage/ts/asc를 그대로
        // 보존한 채 nick만 바꿔 다시 쓴다 ④ 닉네임도 같으면 아무 것도 하지 않는다.
        private static IEnumerator SubmitRoutine(RankBoard board, string pid, string nick, long score, long stage, int asc)
        {
            string url = $"{DbUrl}/{NodeOf(board)}/{pid}.json";

            bool prevExists = false;
            long prevScore = 0, prevStage = 0, prevTs = 0;
            int prevAsc = 0;
            string prevNick = null;

            using (var getReq = UnityWebRequest.Get(url))
            {
                getReq.timeout = TimeoutSeconds;
                yield return getReq.SendWebRequest();

                if (getReq.result != UnityWebRequest.Result.Success)
                {
                    // 조회 자체가 실패하면 잘못된 판단(예: "원격에 기록이 없다"고 오판해 실제로는 더
                    // 높을 수도 있는 기존 점수를 덮어씀)을 피하려고 아무 것도 하지 않는다 — 다음
                    // TrySubmitAll 호출(다음 화면 진입 등)에서 자연히 재시도된다.
                    Debug.Log($"[RankingService] 랭킹 조회 실패({board}): {getReq.error}");
                    yield break;
                }

                string body = getReq.downloadHandler.text;
                if (!string.IsNullOrEmpty(body) && body != "null" && MiniJson.Parse(body) is Dictionary<string, object> obj)
                {
                    prevExists = true;
                    if (obj.TryGetValue("nick", out var nO) && nO is string nS) prevNick = nS;
                    if (obj.TryGetValue("score", out var sO) && sO is double sD) prevScore = (long)sD;
                    if (obj.TryGetValue("stage", out var stO) && stO is double stD) prevStage = (long)stD;
                    if (obj.TryGetValue("ts", out var tO) && tO is double tD) prevTs = (long)tD;
                    if (obj.TryGetValue("asc", out var aO) && aO is double aD) prevAsc = (int)aD;
                }
            }

            string json;
            if (!prevExists || prevScore < score)
            {
                // 신규 등록 또는 갱신 — ts도 새로 찍는다(웹 `set(node, {..., ts: Date.now()})`).
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                json = board == RankBoard.Asc
                    ? JsonUtility.ToJson(new SubmitAscDto { nick = nick, score = score, stage = stage, asc = asc, ts = ts })
                    : JsonUtility.ToJson(new SubmitDto { nick = nick, score = score, stage = stage, ts = ts });
            }
            else if (prevNick != nick)
            {
                // 점수는 원격이 더 높거나 같음 — 닉네임만 갱신, 나머지 필드는 원격 값을 그대로 보존
                // (웹 `set(node, { ...prev, nick })`).
                json = board == RankBoard.Asc
                    ? JsonUtility.ToJson(new SubmitAscDto { nick = nick, score = prevScore, stage = prevStage, asc = prevAsc, ts = prevTs })
                    : JsonUtility.ToJson(new SubmitDto { nick = nick, score = prevScore, stage = prevStage, ts = prevTs });
            }
            else
            {
                yield break; // 점수도 낮거나 같고 닉네임도 동일 — 갱신할 것 없음(PUT 생략).
            }

            using (var putReq = UnityWebRequest.Put(url, json))
            {
                putReq.SetRequestHeader("Content-Type", "application/json");
                putReq.timeout = TimeoutSeconds;
                yield return putReq.SendWebRequest();
                if (putReq.result != UnityWebRequest.Result.Success)
                    Debug.Log($"[RankingService] 랭킹 제출 실패({board}): {putReq.error}");
            }
        }

        /// <summary>지정 보드의 전체 랭킹을 1회 조회한다(정렬: score 내림차순, 동점 ts 오름차순 — 먼저
        /// 세운 기록이 위). host가 파괴/비활성이면(화면을 이미 떠났으면) 콜백을 호출하지 않는다.</summary>
        public static void Fetch(MonoBehaviour host, RankBoard board, Action<List<Entry>> onOk, Action<string> onError)
        {
            if (host == null) return;
            host.StartCoroutine(FetchRoutine(host, board, onOk, onError));
        }

        private static IEnumerator FetchRoutine(MonoBehaviour host, RankBoard board, Action<List<Entry>> onOk, Action<string> onError)
        {
            using (var req = UnityWebRequest.Get($"{DbUrl}/{NodeOf(board)}.json"))
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
            int asc = 0;
            if (obj.TryGetValue("asc", out var ascObj) && ascObj is double ascD) asc = (int)ascD;

            entry = new Entry { pid = pid, nick = nick, score = (long)score, stage = (long)stage, asc = asc, ts = (long)ts };
            return true;
        }
    }
}
