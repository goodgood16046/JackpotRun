using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // PlayerProfile ↔ 직렬화 가능 DTO 매핑 — 순수 C#(UnityEngine 참조 금지). Assets\JackpotRun\Scripts\Game\
    // ProfileStore.cs(Unity 어댑터)가 이 DTO를 UnityEngine.JsonUtility로 텍스트화한다. ENGINE_PORT_DESIGN.md
    // 원칙 6(저장은 엔진 밖) — 이 파일은 "DTO 매핑"만 담당하고 파일 I/O는 하지 않는다.
    //
    // JsonUtility는 Dictionary를 직렬화하지 못한다(Unity 제약) — PlayerProfile.Stats(Dictionary<string,long>)를
    // 키/값 배열 쌍(statKeys[]/statValues[])으로 풀어 담는다. [System.Serializable]은 순수 BCL 속성이라
    // (UnityEngine이 아님) 여기 붙여도 "Engine엔 UnityEngine 금지" 규칙을 어기지 않는다 — JsonUtility가 이
    // 속성을 보고 필드를 리플렉션 직렬화한다.
    [Serializable]
    public sealed class PlayerProfileDto
    {
        // PlayerProfile.Stats 전체(156개 정식 키 + bld_* 파생 원시 플래그 등) — 순서는 무관, 인덱스로 1:1 대응.
        public string[] statKeys = Array.Empty<string>();
        public long[] statValues = Array.Empty<long>();

        // PlayerProfile.AchievedIds(달성 업적 id 집합) — Kotlin SlotV2AchRow.unlocked CSV 대응.
        public string[] achievedIds = Array.Empty<string>();

        // PlayerProfile.OwnedDevices(영구 보유로 인정된 장치 id) — Kotlin SlotV2ScoreRow.ownedDevices
        // CSV 대응("grandfathered" 포함 개념이나 이 포팅은 순수 신규 저장). 두 경로로 채워진다:
        // 업적 달성 시 AchievementEngine.Evaluate가 unlockAch 매칭 장치를 추가(WEB_PARITY P3-2부터
        // lic_* 접두 특례 없이 범용화), 또는 런 중 장치 노드 드랍으로 StatTracker가 직접 추가
        // (PlayerProfile.cs OwnedDevices 필드 각주 참조).
        public string[] ownedDevices = Array.Empty<string>();

        // SlotV2ScoreRow 잔여 필드(03_meta §3.3) — achievement 판정에 쓰이지 않는 "기록/표시" 전용 필드.
        public long totalScore;      // 통산 누적 점수(리더보드 합산용, bestScore와 별개)
        public string bestChar = ""; // 최고점수 달성 시 캐릭터 id
        public string bestMachine = ""; // 최고점수 달성 시 머신 id
        public long lastPlayedAtUnixMs; // 카톡 전용 startedAt/lastActionAt(§3.1)은 제외 — 이건 프로필(영속) 전용 메타
        public string pinnedChallenge = ""; // 고정한 도전 id(§3.3) — 도전판 진행률 로직 자체는 이 슬라이스 범위 밖(보고 대상)
        public string lastCombo = "";       // 직전 런 조합 CSV "char,machine,device,device2"(§3.3)

        // ── 플레이어 레벨/XP (P3, 웹 파리티) — PlayerProfile.PlayerXp/PlayerLevel/PlayerXpSeeded 대응.
        // 웹 game.js:105 defaultProfile()의 playerXp:0/playerLevel:1, game.js:189-192 _xpInit 마이그레이션.
        public long playerXp;
        public int playerLevel = 1;
        public bool playerXpSeeded; // 웹 profile._xpInit 대응 — 이력 XP 시딩(seedXpFromHistory) 1회성 플래그.

        // WEB_PARITY_DESIGN.md §2-(L) — 업적 34종 교체 XP 재시딩(1회) 완료 여부. PlayerProfile.
        // PlayerXpReseed34 대응, Unity 전용(웹에 직접 대응물 없음).
        public bool playerXpReseed34;

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — PlayerProfile.TutDone 대응(웹 profile.tutDone).
        public bool tutDone;

        // ── 승천(심화 학기, P6, WEB_PARITY_DESIGN.md §1-A #18) — PlayerProfile.AscMax/BestAscScore/
        // BestAscLevel 대응(웹 defaultProfile ascMax:-1/bestAscScore:0/bestAscLevel:0).
        public int ascMax = -1;
        public long bestAscScore;
        public int bestAscLevel;

        // ── 숙련도(mastery, P3, WEB_PARITY_DESIGN.md §1-A #11) — PlayerProfile.Mastery(kind->id->
        // MasteryStats) 대응. JsonUtility가 중첩 Dictionary를 직렬화하지 못해(헤더 각주 참조) (kind,id)
        // 조합당 1행으로 완전히 펼친 병렬 배열 5+2개로 담는다 — 인덱스로 1:1 대응(statKeys/Values와
        // 동일한 패턴을 컬럼 5개로 확장).
        public string[] masteryKind = Array.Empty<string>();
        public string[] masteryId = Array.Empty<string>();
        public int[] masteryRuns = Array.Empty<int>();
        public int[] masteryBestStage = Array.Empty<int>();
        public int[] masteryBossClears = Array.Empty<int>();
        public long[] masteryBestScore = Array.Empty<long>();
        public int[] masteryAscMax = Array.Empty<int>();
    }

    public static class ProfileDto
    {
        public static PlayerProfileDto ToDto(PlayerProfile p)
        {
            if (p == null) return new PlayerProfileDto();

            int n = p.Stats.Count;
            var keys = new string[n];
            var vals = new long[n];
            int i = 0;
            foreach (var kv in p.Stats)
            {
                keys[i] = kv.Key;
                vals[i] = kv.Value;
                i++;
            }

            var achieved = new string[p.AchievedIds.Count];
            p.AchievedIds.CopyTo(achieved);

            var owned = new string[p.OwnedDevices.Count];
            p.OwnedDevices.CopyTo(owned);

            // 숙련도 — Mastery[kind][id]를 (kind,id)당 1행으로 펼친다(존재하는 조합만, 순서는 무관).
            var mKind = new List<string>();
            var mId = new List<string>();
            var mRuns = new List<int>();
            var mBestStage = new List<int>();
            var mBossClears = new List<int>();
            var mBestScore = new List<long>();
            var mAscMax = new List<int>();
            foreach (var kindBag in p.Mastery)
            {
                foreach (var kv in kindBag.Value)
                {
                    mKind.Add(kindBag.Key);
                    mId.Add(kv.Key);
                    mRuns.Add(kv.Value.Runs);
                    mBestStage.Add(kv.Value.BestStage);
                    mBossClears.Add(kv.Value.BossClears);
                    mBestScore.Add(kv.Value.BestScore);
                    mAscMax.Add(kv.Value.AscMax);
                }
            }

            return new PlayerProfileDto
            {
                statKeys = keys,
                statValues = vals,
                achievedIds = achieved,
                ownedDevices = owned,
                totalScore = p.TotalScore,
                bestChar = p.BestChar ?? "",
                bestMachine = p.BestMachine ?? "",
                lastPlayedAtUnixMs = p.LastPlayedAtUnixMs,
                pinnedChallenge = p.PinnedChallenge ?? "",
                lastCombo = p.LastCombo ?? "",
                playerXp = p.PlayerXp,
                playerLevel = p.PlayerLevel,
                playerXpSeeded = p.PlayerXpSeeded,
                playerXpReseed34 = p.PlayerXpReseed34,
                tutDone = p.TutDone,
                ascMax = p.AscMax,
                bestAscScore = p.BestAscScore,
                bestAscLevel = p.BestAscLevel,
                masteryKind = mKind.ToArray(),
                masteryId = mId.ToArray(),
                masteryRuns = mRuns.ToArray(),
                masteryBestStage = mBestStage.ToArray(),
                masteryBossClears = mBossClears.ToArray(),
                masteryBestScore = mBestScore.ToArray(),
                masteryAscMax = mAscMax.ToArray(),
            };
        }

        public static PlayerProfile FromDto(PlayerProfileDto dto)
        {
            var p = new PlayerProfile();
            if (dto == null) return p;

            if (dto.statKeys != null && dto.statValues != null)
            {
                int n = Math.Min(dto.statKeys.Length, dto.statValues.Length);
                for (int i = 0; i < n; i++)
                {
                    var k = dto.statKeys[i];
                    if (string.IsNullOrEmpty(k)) continue;
                    p.Stats[k] = dto.statValues[i];
                }
            }
            if (dto.achievedIds != null)
                foreach (var id in dto.achievedIds)
                    if (!string.IsNullOrEmpty(id)) p.AchievedIds.Add(id);
            if (dto.ownedDevices != null)
                foreach (var id in dto.ownedDevices)
                    if (!string.IsNullOrEmpty(id)) p.OwnedDevices.Add(id);

            p.TotalScore = dto.totalScore;
            p.BestChar = dto.bestChar ?? "";
            p.BestMachine = dto.bestMachine ?? "";
            p.LastPlayedAtUnixMs = dto.lastPlayedAtUnixMs;
            p.PinnedChallenge = dto.pinnedChallenge ?? "";
            p.LastCombo = dto.lastCombo ?? "";

            // ── 숙련도(mastery) 왕복 — masteryKind[i]/masteryId[i]가 기준 행 개수. 나머지 컬럼은
            // 길이가 짧아도(구버전 세이브 등 방어) 인덱스 초과분은 기본값(0/-1)으로 채운다.
            if (dto.masteryKind != null && dto.masteryId != null)
            {
                int n = Math.Min(dto.masteryKind.Length, dto.masteryId.Length);
                for (int i = 0; i < n; i++)
                {
                    var kind = dto.masteryKind[i];
                    var id = dto.masteryId[i];
                    if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id)) continue;
                    if (!p.Mastery.TryGetValue(kind, out var bag)) { bag = new Dictionary<string, MasteryStats>(); p.Mastery[kind] = bag; }
                    bag[id] = new MasteryStats
                    {
                        Runs = (dto.masteryRuns != null && i < dto.masteryRuns.Length) ? dto.masteryRuns[i] : 0,
                        BestStage = (dto.masteryBestStage != null && i < dto.masteryBestStage.Length) ? dto.masteryBestStage[i] : 0,
                        BossClears = (dto.masteryBossClears != null && i < dto.masteryBossClears.Length) ? dto.masteryBossClears[i] : 0,
                        BestScore = (dto.masteryBestScore != null && i < dto.masteryBestScore.Length) ? dto.masteryBestScore[i] : 0,
                        AscMax = (dto.masteryAscMax != null && i < dto.masteryAscMax.Length) ? dto.masteryAscMax[i] : -1,
                    };
                }
            }

            // ── 플레이어 레벨/XP 마이그레이션 (웹 game.js:189-192 _loadProfile 그대로) ──────────────
            // 이 필드 도입 전 세이브(playerXp/playerXpSeeded가 DTO 기본값 0/false로 역직렬화됨)를
            // 로드했을 때만 이력 시딩이 발동한다 — 이미 seeded이거나 이미 XP가 쌓여 있으면(이 필드
            // 도입 후 첫 런을 이미 마친 세이브) 건드리지 않는다("runs>0 && !(playerXp>0)"만 시딩,
            // 웹과 동일 조건). Runs/TotalScore/BestStage는 위에서 이미 채워진 뒤라 안전하게 읽는다.
            p.PlayerXp = dto.playerXp;
            p.PlayerXpSeeded = dto.playerXpSeeded;
            if (!p.PlayerXpSeeded)
            {
                if (p.Runs > 0 && p.PlayerXp <= 0)
                    p.PlayerXp = Formulas.PlayerSeedXpFromHistory(p.Runs, p.TotalScore, p.GetStat("bossClears"), p.BestStage);
                p.PlayerXpSeeded = true;
            }

            // ── §2-(L) XP 재시딩 마이그레이션(업적 34종 교체 인플레이션 정정, 1회) ─────────────────────
            // 웹에는 직접 대응물이 없다 — 웹은 애초에 34종 체계로 시작해 "신규업적×25" 인플레이션이
            // 없었다(Unity 전용 마이그레이션). 482종이 살아 있던 동안의 세이브는 런XP의 신규업적×25 항이
            // 34종 대비 크게 부풀어 있을 수 있다(WEB_PARITY_DESIGN.md §2-(L), P3-1 Opus 1차 검수 지적).
            // 시딩 공식(PlayerSeedXpFromHistory — 웹 seedXpFromHistory와 동일식)으로 현재 이력 기준
            // "정상 시딩값"을 재산출해, 그 값이 현재 playerXp보다 *작을 때만* 덮어쓴다.
            // Opus 2차 검수·Fable 결정(2026-08-08, §2-(L)) — 이 조건은 "482종 시절 세이브만 골라 정정"이
            // 아니라 **이력 기반 재산출로 통일**한 것이다(재산출값이 작을 때만 적용). 실제로는 런XP
            // 적립 공식(PlayerRunXp — 스핀마다 정밀 가산)이 이력 시딩 근사식(PlayerSeedXpFromHistory —
            // runs×30+... 거친 평균)보다 항상 크거나 같으므로, 정상적으로 플레이해 쌓은 세이브에서도
            // 이 마이그레이션은 사실상 항상 발동해 playerXp를 시딩값으로 낮춘다("작을 때만 덮어쓴다"는
            // 조건 자체는 정확하지만, 대상을 "인플레이션된 세이브만"으로 좁히지 못하는 블랭킷 정정이다).
            // 앱이 아직 미출시라 실사용 세이브가 0건인 지금 단계에서는 그 부작용이 실질적으로 없으므로,
            // 표적 정정(예: 마이그레이션 시점 기록·달성 업적 이력 대조)을 새로 설계하는 대신 단순
            // 통일을 그대로 채택한다 — §2-(L) 문언("재산출값이 작을 때만 덮어써라")을 문자 그대로 구현.
            // 위 playerXpSeeded 마이그레이션과 별개의 독립 1회성 플래그(PlayerXpReseed34)라, 이미
            // seeded된 세이브에도 정확히 한 번만 적용된다.
            p.PlayerXpReseed34 = dto.playerXpReseed34;
            if (!p.PlayerXpReseed34)
            {
                long reseeded = Formulas.PlayerSeedXpFromHistory(p.Runs, p.TotalScore, p.GetStat("bossClears"), p.BestStage);
                if (reseeded < p.PlayerXp) p.PlayerXp = reseeded;
                p.PlayerXpReseed34 = true;
            }

            p.PlayerLevel = Formulas.PlayerLevelFromXp(p.PlayerXp); // 웹 game.js:193 — 로드마다 XP로부터 재산출.

            p.TutDone = dto.tutDone;

            // 승천(P6) — dto 기본값(int/long 미도입 세이브)도 defaultProfile과 동일한 -1/0/0으로
            // 자연히 떨어진다(PlayerProfileDto 필드 기본값 자체가 -1/0/0).
            p.AscMax = dto.ascMax;
            p.BestAscScore = dto.bestAscScore;
            p.BestAscLevel = dto.bestAscLevel;

            // Opus 2차검수(P6) 마이그레이션 가드 — Unity `JsonUtility`는 구현에 따라 필드 부재를 항상
            // C# 필드 초기값(-1)으로 채운다고 보장할 수 없다(관측 근거: 0으로 채워지는 경로가 있음).
            // 이 필드 도입 이전 세이브가 "ascMax=0"(=asc0 졸업 완료)으로 잘못 로드되면 아직 한 번도
            // 졸업하지 못한 플레이어에게 승천 선택기가 부당하게 해금된다 — 정당한 ascMax=0은 반드시
            // "graduations>=1"(asc0 졸업이 선행돼야만 StatTracker.ApplyClearTracking이 이 카운터를
            // 올림)을 동반해야 하므로 논리적으로 무결한 판별식이다. graduations==0인데 ascMax==0으로
            // 읽혔다면 마이그레이션 이전 세이브의 빈 필드일 뿐이므로 -1(미졸업)로 강제 정정한다.
            if (p.AscMax == 0 && p.GetStat("graduations") == 0) p.AscMax = -1;

            return p;
        }
    }
}
