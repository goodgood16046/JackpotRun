using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 플레이어 영속 상태 — 순수 C#(UnityEngine 참조 금지). 03_meta.md §3의 3개 Room 엔티티
    // (SlotV2RunRow=휘발성 런 상태라 RunState.cs 소관·SlotV2AchRow·SlotV2ScoreRow)를 "카톡 서버 1인당 여러
    // 링크방" 모델에서 "Unity 로컬 싱글플레이 1인 세이브" 모델로 좁혀 하나로 합쳤다(설계 원칙 6 — 저장은
    // 엔진 밖, 이 클래스는 인메모리 상태만).
    //
    // ── Stats 통합 결정 (작업 지시 명시) ──────────────────────────────────────────────
    // SlotV2AchRow는 전용 Long 컬럼 10개(cherryTotal·crownTotal·jackpots·bossClears·lastSpinClears·
    // exactClears·prismPicks·bestStage·runs·bestScore, 03_meta §3.2) + counters CSV(나머지 146개, §5.3
    // 156개 고유 key 사전)로 나뉘어 있었으나, C# 포팅은 이 구분을 유지할 이유가 없어(03_meta §6-4가 명시적으로
    // 허용) 단일 `Dictionary<string,long> Stats`로 통합했다. bld_<id>(테마빌드 25종 완성 플래그, StatTracker가
    // setMax)처럼 achievement 판정에 간접적으로만 쓰이는 원시 키도 이 딕셔너리에 함께 산다 — "156개 고유 key"
    // 더하기 이런 파생-원천 키까지 전부 여기 하나로 모인다. lic_dev_*·distinctCharS10·bldCat_*/bldTotal/
    // bldAllBasic/bldAllMaster·accountLevel은 여기 저장하지 않는다(Kotlin composeStat과 동일하게 매번
    // AchievementEngine.ComposeStat이 즉석 계산하는 파생값 — 저장하면 이중 갱신 위험).
    //
    // ── SlotV2ScoreRow(03_meta §3.3) 잔여 필드 ────────────────────────────────────────
    // bestScore/bestStage/runs는 SlotV2ScoreRow에도 있지만 Kotlin composeStat이 AchRow와 max()로 병합해
    // 단일 stat 값으로 취급하므로(§3.2 "중요" 각주), 여기서도 Stats["bestScore"]/["bestStage"]/["runs"]를
    // 단일 진실 공급원으로 삼는다(BestScore/BestStage/Runs 프로퍼티는 그 안전한 읽기 별칭일 뿐 별도 필드
    // 아님). ScoreRow에만 있고 achievement 판정에 전혀 쓰이지 않는 필드만 별도 스칼라로 둔다: TotalScore
    // (통산 누적 점수 합계 — bestScore와 다름), BestChar/BestMachine(최고점수 달성 조합), PinnedChallenge,
    // LastCombo. LastPlayedAtUnixMs는 원본에 없는 신규 필드(Unity 세이브 메타용, §3.1 startedAt/lastActionAt
    // 제외 목록과 같은 취지로 "카톡 서버 TTL" 로직 없이 순수 표시용).
    //
    // [카톡 전용 필드 제외 목록 — 이식하지 않음, 03_meta §3 근거]
    //   - linkId, ownerKey, ownerNick, ownerUserId, nickname, userId (§3.1~§3.3): 카카오톡 채팅방/유저
    //     식별자. Unity는 로컬 단일 세이브 파일 1개 = 플레이어 1명이라 식별자 자체가 불필요.
    //   - startedAt/lastActionAt(§3.1 RunRow용, RunState.cs가 이미 제외): 챗봇 서버 RUN_TTL_MS 자동만료 로직.
    //   - counters CSV 문자열 표현(§3.2): Dictionary<string,long> 통합으로 대체(위 설명).
    //   - unlocked CSV 문자열(§3.2): HashSet<string> AchievedIds로 대체.
    //   - ownedDevices CSV 문자열(§3.3): HashSet<string> OwnedDevices로 대체.
    //
    // [이 슬라이스 범위 밖 — 구현하지 않음, 보고 대상]
    //   - PinnedChallenge/LastCombo는 스키마 필드만 보존했다(DTO 왕복 대상). "도전판 고정"(allChallenges/
    //     reqProgress/bottleneck, 03_meta §2.4·§6-9) 자체의 진행률 계산 로직은 SlotV2Engine.kt 미포함 함수가
    //     많아 이 슬라이스(프로필·스탯트래킹·업적판정) 범위 밖이다 — "업적 달성 시 unlockAch가 가리키는
    //     장치 해금"만 AchievementEngine이 구현한다(WEB_PARITY P3-2부터 lic_* 접두 특례 없이 범용화 —
    //     Devices.cs의 unlockAch가 업적 id를 직접 담는다, AchievementEngine.cs 헤더 각주 참조).
    // 숙련도(mastery, P3, WEB_PARITY_DESIGN.md §1-A #11 — 웹 game.js:143-165/217-232 그대로) 성과 누적
    // 1건 — 웹 `{runs,bestStage,bossClears,bestScore,ascMax}`와 동일 필드. ascMax 기본값 -1은 웹
    // `{...,ascMax:-1}`과 동일(승천 미졸업 표식) — 승천 P6(WEB_PARITY_DESIGN.md §1-A #18)에서
    // PlayerProfile.BumpMastery(graduatedThisRun,asc)가 갱신한다(웹 game.js:226 `if (r.graduatedThisRun)
    // s.ascMax = Math.max(s.ascMax??-1, r.asc)` 그대로).
    public sealed class MasteryStats
    {
        public int Runs;
        public int BestStage;
        public int BossClears;
        public long BestScore;
        public int AscMax = -1;
    }

    // PlayerProfile.MasteryOf(kind,id) 조회 결과 — Level(충족 마일스톤 수, 0~5)·Total(5)만 노출한다
    // (PickView/DexView ★ 표기에 필요한 값만 — 원시 통계가 필요하면 MasteryStats를 별도로 조회할 것).
    public sealed class MasteryInfo
    {
        public int Level;
        public int Total;
    }

    public sealed class PlayerProfile
    {
        // 156개 고유 stat key(03_meta §5.3) + StatTracker가 쓰는 파생-원천 키(bld_<id> 25종 등) 통합 저장소.
        // 키가 없으면 0(Kotlin `stat[key] ?: 0L`과 동일 관례) — GetStat/Inc/SetMax/SetStat으로만 조작할 것
        // (SetStat은 누적/최댓값 규칙 없는 직접 대입 — 웹 "cnt[key]=value" 스냅샷 대입에 대응, 아래 참조).
        public readonly Dictionary<string, long> Stats = new Dictionary<string, long>();

        // 달성한 업적 id 집합 — Kotlin SlotV2AchRow.unlocked CSV 대응.
        public readonly HashSet<string> AchievedIds = new HashSet<string>();

        // 업적 달성 등으로 영구 보유가 인정된 장치 id 집합 — Kotlin SlotV2ScoreRow.ownedDevices 대응.
        // 두 경로로 채워진다: ① AchievementEngine.Evaluate가 "unlockAch==방금 달성한 업적 id"인 장치를
        // 범용으로 찾아 여기 추가(WEB_PARITY P3-2, 구 lic_* 접두 특례 제거). ② 런 중 장치 노드 드랍
        // (P1, NODE_RESOLVED.deviceGrantedId → StatTracker가 직접 추가) — unlockAch가 빈 문자열인
        // 드랍 전용 장치(dev_syllabus/dev_holdfile/dev_retake/dev_major, Devices.cs 헤더 각주)는 이
        // ②번 경로로만 영구 보유가 인정된다(①은 unlockAch가 비어 있으면 매치될 업적이 없어 자연히 스킵됨).
        public readonly HashSet<string> OwnedDevices = new HashSet<string>();

        // SlotV2ScoreRow 잔여 필드(achievement 판정 무관, 표시/기록 전용) — 03_meta §3.3.
        public long TotalScore;
        public string BestChar = "";
        public string BestMachine = "";
        public long LastPlayedAtUnixMs;
        public string PinnedChallenge = "";
        public string LastCombo = "";

        // ── 플레이어 레벨/XP (P3, WEB_PARITY_DESIGN.md §1-A #9) ────────────────────────────────────
        // 콘텐츠 해금 게이트일 뿐 영구 스탯 보정 없음(웹 game.js:105 defaultProfile: playerXp 0,
        // playerLevel 1). Formulas.AccountExp/AccountLevel(§9.3, 졸업레벨 1~25 — 퍽 게이트가 아직
        // 참조 중이라 이번 슬라이스에서 미변경)과는 완전히 별개 체계라 이름이 겹치지 않게 "Player"
        // 접두를 쓴다. 레벨업 계산은 PlayerLevelTracker.ApplyRunEnd(런 종료 훅)가 담당한다.
        public long PlayerXp;
        public int PlayerLevel = 1;

        // 웹 profile._xpInit(game.js:189-192) 대응 — 기존 세이브에 이력 XP를 1회만 시딩했는지 플래그.
        // ProfileDto.FromDto가 로드 시점에 관리한다(직접 조작하지 말 것).
        public bool PlayerXpSeeded;

        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15, 웹 game.js:200 `playerProgress()`) — 홈 화면
        // 레벨 카드/레벨 보상 화면/런종료 XP 블록이 공통으로 읽는 표시용 진행도. PlayerXp 하나에서
        // 매번 재계산한다(별도 캐시 없음 — 웹도 호출마다 levelInfo(xp)를 다시 돈다).
        public Formulas.PlayerLevelProgress LevelProgress() => Formulas.PlayerLevelProgressFromXp(PlayerXp);

        // WEB_PARITY_DESIGN.md §2-(L) — 업적 34종 교체(P3-2)로 런XP의 "신규업적×25" 항 인플레이션이
        // 정정되면서, 교체 이전(482종 시절) 세이브의 playerXp가 부풀어 있을 수 있어 1회 재시딩하는
        // Unity 전용 마이그레이션 플래그(웹에는 직접 대응물이 없다 — 웹은 처음부터 34종이라 이 인플레이션
        // 자체가 없었음). ProfileDto.FromDto가 로드 시점에 관리한다(직접 조작하지 말 것).
        public bool PlayerXpReseed34;

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 `profile.tutDone` + `markTutorialDone()`
        // game.js:2633 `this.profile.tutDone = true; this._saveProfile();`) — 튜토리얼 3단(스포트라이트
        // 투어→결과해설→라이브)을 마쳤는지. 저장은 엔진 밖(호출측이 ProfileStore.Save)에서 한다(설계
        // 원칙 6과 동일 — 이 메서드는 플래그만 세운다).
        public bool TutDone;
        public void MarkTutorialDone() => TutDone = true;

        // ── 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 defaultProfile ascMax:-1/bestAscScore:0/
        // bestAscLevel:0) — 승천(심화 학기) 진행도. AscMax=-1(웹과 동일)은 "한 번도 졸업(스테이지15
        // 클리어)하지 못함" — 이 상태에선 승천 자체가 잠겨 있다(AscUnlocked() 참조). StatTracker.
        // ApplyGameOverTracking이 런 종료(GAME_OVER) 시점에 run.GraduatedThisRun을 보고 갱신한다.
        public int AscMax = -1;
        public long BestAscScore;
        public int BestAscLevel;

        // ── 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 defaultProfile bestDeepScore:0/
        // bestDeepStage:0, game.js:2557 `if (finalScore > (p.bestDeepScore||0)) { p.bestDeepScore =
        // finalScore; p.bestDeepStage = r.stage; }`) — 심화모드(주머니 덱) 런의 최고 기록. 승천과
        // 마찬가지로 일반 bestScore/랭킹과 완전히 격리된다(주머니 덱은 확률 소스가 달라 별개 게임).
        // StatTracker.ApplyGameOverTracking이 run.DeepMode 분기에서 갱신한다.
        public long BestDeepScore;
        public int BestDeepStage;

        // 웹 game.js:213 `ascUnlocked() { return (this.profile.ascMax ?? -1) >= 0; }` — 승천 1회 이상
        // 졸업(일반 런 asc=0의 스테이지15 클리어도 포함 — AscMax가 -1에서 0으로 오른 시점부터 true).
        public bool AscUnlocked() => AscMax >= 0;

        // 웹 game.js:214 `maxPlayableAsc() { return Math.max(0, (this.profile.ascMax ?? -1) + 1); }`.
        public int MaxPlayableAsc() => Math.Max(0, AscMax + 1);

        // 웹 game.js:215 `ascInfo(a)` — 홈 승천 선택기(ascSelector)가 읽는 표시용 묶음.
        public readonly struct AscInfo
        {
            public readonly int A;
            public readonly double ScoreMul;
            public readonly string Rule;
            public readonly bool Cleared; // 이미 이 단계까지 졸업했는가(AscMax>=a)
            public readonly int Max;      // AscMods.AscMax(10)

            public AscInfo(int a, double scoreMul, string rule, bool cleared, int max)
            {
                A = a; ScoreMul = scoreMul; Rule = rule; Cleared = cleared; Max = max;
            }
        }

        public AscInfo GetAscInfo(int asc)
        {
            var m = AscMods.Get(asc);
            return new AscInfo(m.A, m.ScoreMul, AscMods.RuleOf(m.A), AscMax >= m.A, AscMods.AscMax);
        }

        // ── bestScore/bestStage/runs 읽기 별칭 (Stats 딕셔너리가 단일 진실 공급원) ──────────────────
        public long BestScore => GetStat("bestScore");
        public long BestStage => GetStat("bestStage");
        public long Runs => GetStat("runs");

        public long GetStat(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0L;
            return Stats.TryGetValue(key, out var v) ? v : 0L;
        }

        // 누적(+=) — Kotlin bumpAch의 inc 병합(cm[k] = (cm[k] ?: 0) + v)과 동일 규칙.
        public void Inc(string key, long delta = 1L)
        {
            if (string.IsNullOrEmpty(key) || delta == 0L) return;
            Stats[key] = GetStat(key) + delta;
        }

        // 최댓값 갱신 — Kotlin bumpAch의 setMax 병합(cm[k] = maxOf(cm[k] ?: 0, v))과 동일 규칙.
        public void SetMax(string key, long value)
        {
            if (string.IsNullOrEmpty(key)) return;
            long cur = GetStat(key);
            if (value > cur) Stats[key] = value;
            else if (!Stats.ContainsKey(key)) Stats[key] = cur; // 최초 기록(0으로라도 키를 만들어 존재를 표시)
        }

        // 직접 대입(Inc/SetMax와 달리 누적/최댓값 규칙 없이 그대로 덮어씀) — 웹의 "cnt[key] = value"
        // 스냅샷 대입(예: game.js:2578 playerLevel 1런 지연 기록)에 대응하는 세 번째 조작 방식.
        // Opus 1차 검수(P3-2) — StatTracker가 Stats 딕셔너리에 직접 인덱서로 대입하던 지점을
        // (Inc/SetMax와 나란한) 공개 계약으로 승격했다: "Stats는 GetStat/Inc/SetMax/SetStat으로만
        // 조작할 것"(Stats 필드 각주 갱신).
        public void SetStat(string key, long value)
        {
            if (string.IsNullOrEmpty(key)) return;
            Stats[key] = value;
        }

        // ── 해금 조회 — 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #13, game.js:259-277 charUnlocked/
        // machineUnlocked) OR 5축/4축 그대로. 기존 Unlocks.Meets(StatReq AND 리스트) 경유는 폐기했다
        // (Characters.cs/Machines.cs 헤더 각주 참조 — unlockReq 필드 자체를 제거했다). PlayerLevel은
        // 즉시 반영되는 실 필드를 직접 읽는다(Stats["playerLevel"]은 업적 lv20/lv40용 1런 지연
        // 스냅샷이라 여기 쓰면 레벨업 직후 1런 동안 게이트가 그릇되게 잠긴다).
        public bool IsCharUnlocked(Character c)
        {
            if (c == null) return false;
            if (c.unlockRuns <= 0 && c.unlockScore <= 0 && c.unlockStage <= 0 && c.unlockLevel <= 0 && string.IsNullOrEmpty(c.unlockAch))
                return true;
            if (c.unlockRuns > 0 && Runs >= c.unlockRuns) return true;
            if (c.unlockScore > 0 && BestScore >= c.unlockScore) return true;
            if (c.unlockStage > 0 && BestStage >= c.unlockStage) return true;
            if (c.unlockLevel > 0 && PlayerLevel >= c.unlockLevel) return true;
            if (!string.IsNullOrEmpty(c.unlockAch) && AchievedIds.Contains(c.unlockAch)) return true;
            return false;
        }

        public bool IsMachineUnlocked(Machine m)
        {
            if (m == null) return false;
            if (m.unlockRuns <= 0 && m.unlockScore <= 0 && m.unlockLevel <= 0 && string.IsNullOrEmpty(m.unlockAch))
                return true;
            if (m.unlockRuns > 0 && Runs >= m.unlockRuns) return true;
            if (m.unlockScore > 0 && BestScore >= m.unlockScore) return true;
            if (m.unlockLevel > 0 && PlayerLevel >= m.unlockLevel) return true;
            if (!string.IsNullOrEmpty(m.unlockAch) && AchievedIds.Contains(m.unlockAch)) return true;
            return false;
        }

        // 퍽(증강/유물/저주) 해금 — Shop.PerkUnlocked(internal, 같은 어셈블리)가 Unlocks.Meets + Schools
        // (BasePerkIds/SchoolReq/PerkGateOverrides/SchoolResearch)를 이미 결합해 두었다(Shop.cs 헤더 주석
        // "Schools.cs의 ... 단일 소스로 결합만 한다") — 그 로직을 그대로 재사용한다(중복 구현 금지).
        public bool IsPerkUnlocked(Perk p) => Shop.PerkUnlocked(p, Stats);

        // 장치 해금 = 영구보유(ownedDevices) ∪ achieved(dev.unlockAch). WEB_PARITY P3-2부터 unlockAch는
        // 업적 id를 직접 담는다(구 lic_dev_<id> 파생키 경유 판정은 제거됨 — AchievementEngine.cs 헤더
        // 각주) — 그 업적 자체가 신규 달성됐는지 여부(단일 key>=threshold)는 AchievementEngine.Evaluate가
        // 판정해 AchievedIds에 반영하고, 여기서는 "이미 달성된 업적 id 집합"만 본다(재구현하지 않음).
        // unlockAch가 빈 문자열인 드랍 전용 장치(dev_syllabus/dev_holdfile/dev_retake/dev_major, Devices.cs
        // 헤더 각주 — 웹에 대응 없는 Unity 전용, 업적 해금 없이 런 중 장치 드랍으로만 영구 획득)는
        // `AchievedIds.Contains("")`가 항상 false이므로(HashSet에 빈 문자열이 들어갈 일이 없음) 이 절이
        // 자연히 안전하게 false로 떨어진다 — OwnedDevices 경로만 유효하다.
        public bool IsDeviceUnlocked(DeviceDef d)
        {
            if (d == null) return false;
            if (OwnedDevices.Contains(d.id)) return true;
            return !string.IsNullOrEmpty(d.unlockAch) && AchievedIds.Contains(d.unlockAch);
        }

        // 현재 장착 가능한(해금된) 장치 목록 — devicesOwned 스탯과 동일한 모집합(unlockAch 있는 것만).
        public IEnumerable<DeviceDef> UnlockedDevices() => Devices.All.Where(IsDeviceUnlocked);

        // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #9/#14, game.js:172 LEVEL_DEVICE_REWARD) ──
        // 레벨 도달 시 자동 지급되는 후반 장치 3종(업적 무관 — Devices.cs의 dev_reaper/dev_abyss/
        // dev_reactor는 unlockAch=""). 웹과 동일한 레벨→장치 매핑을 단일 소스로 둔다.
        public static readonly IReadOnlyDictionary<int, string> LevelDeviceReward = new Dictionary<int, string>
        {
            [14] = "dev_reaper", [18] = "dev_abyss", [22] = "dev_reactor",
        };

        // 웹 game.js:246-254 `_grantLevelDevices()` 그대로 — 멱등(이미 보유하면 재지급 안 함). 호출측
        // (GameSession)이 ①프로필 로드 직후 ②런 종료(레벨업 가능 시점) 양쪽에서 호출한다. 반환값은
        // 이번 호출로 새로 지급된 장치 id 목록(로그/토스트용, 없으면 빈 리스트).
        public List<string> GrantLevelDevices()
        {
            var got = new List<string>();
            foreach (var kv in LevelDeviceReward)
            {
                if (PlayerLevel >= kv.Key && !OwnedDevices.Contains(kv.Value))
                {
                    OwnedDevices.Add(kv.Value);
                    got.Add(kv.Value);
                }
            }
            return got;
        }

        // ── 웹 파리티 P3(#9, WEB_PARITY_DESIGN.md §1-A #9, game.js:201-211 levelUnlocks) ──
        // P4(레벨 보상 화면) UI가 소비할 로드맵 데이터만 엔진에 준비해 둔다(이 슬라이스는 UI 없음).
        public sealed class LevelUnlockEntry
        {
            public int Level;
            public string Label;
            public bool Unlocked;
        }

        public List<LevelUnlockEntry> LevelUnlocks()
        {
            var raw = new List<LevelUnlockEntry>();
            foreach (var c in Characters.All)
                if (c.unlockLevel > 0) raw.Add(new LevelUnlockEntry { Level = c.unlockLevel, Label = $"🎭 {c.name} (캐릭터)" });
            foreach (var mc in Machines.All)
                if (mc.unlockLevel > 0) raw.Add(new LevelUnlockEntry { Level = mc.unlockLevel, Label = $"🎰 {mc.name} (슬롯)" });
            // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑧(WEB_PARITY_DESIGN.md §2) — Dictionary 순회 순서는
            // .NET/Mono/IL2CPP 구현 세부사항이라 보장되지 않는다. 레벨 오름차순으로 명시 정렬해 고정한다
            // (LevelDeviceReward는 이 함수 안에서만 순서가 문제 — GrantLevelDevices는 순서 무관 멱등).
            foreach (var kv in LevelDeviceReward.OrderBy(kv => kv.Key))
            {
                var d = Devices.ById(kv.Value);
                if (d != null) raw.Add(new LevelUnlockEntry { Level = kv.Key, Label = $"🔧 {d.name} (장치)" });
            }
            foreach (var a in Perks.Augments)
                if (a.unlockLevel > 0) raw.Add(new LevelUnlockEntry { Level = a.unlockLevel, Label = $"✨ {a.name} (증강)" });
            foreach (var r in Perks.Relics)
                if (r.unlockLevel > 0) raw.Add(new LevelUnlockEntry { Level = r.unlockLevel, Label = $"📜 {r.name} (유물)" });
            // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑧ — List.Sort는 불안정 정렬(동순위 상대순서 미보장)
            // 이라 같은 레벨 항목(예: Lv12의 bankrupt/throne/curse_grad/black_grad_photo 4건)의 순서가
            // 실행마다 달라질 수 있었다. LINQ OrderBy(안정 정렬)로 전환 — 동순위는 위 삽입 순서
            // (캐릭→머신→장치→증강→유물)를 그대로 유지한다.
            var sorted = raw.OrderBy(o => o.Level).ToList();
            foreach (var o in sorted) o.Unlocked = PlayerLevel >= o.Level;
            return sorted;
        }

        // ── 숙련도(mastery, P3, WEB_PARITY_DESIGN.md §1-A #11) — kind("char"/"mac"/"dev") -> id -> 성과.
        // 웹 profile.mastery = {char:{},mac:{},dev:{}} 대응(웹 game.js:143 MASTERY 표/217-232 _bumpMastery/
        // masteryOf). 최초엔 kind 3개가 비어 있고, BumpMastery가 처음 등장한 id를 그때그때 만든다(웹
        // `bag[id] || (bag[id] = {...})`와 동일 관례 — 미리 3개 빈 dict를 만들어 둘 필요 없음, ProfileDto
        // 왕복도 존재하는 (kind,id) 행만 직렬화한다).
        public readonly Dictionary<string, Dictionary<string, MasteryStats>> Mastery = new Dictionary<string, Dictionary<string, MasteryStats>>();

        // 런 종료(GAME_OVER) 시 사용한 캐릭/머신/장치 성과 누적 — 웹 game.js:217-227 `_bumpMastery` 그대로.
        // 호출측(MasteryTracker.ApplyRunEnd)이 kind별로("char"/"mac"/"dev") 호출한다(장치는 장착 시만 —
        // 웹 game.js:2627 `if (r.device) this._bumpMastery("dev", r.device)`).
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:226 `if (r.graduatedThisRun) s.ascMax =
        // Math.max(s.ascMax ?? -1, r.asc)`) — graduatedThisRun/asc 매개변수 추가.
        public void BumpMastery(string kind, string id, int stage, int bossClearsThisRun, long finalScore,
            bool graduatedThisRun = false, int asc = 0)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!Mastery.TryGetValue(kind, out var bag)) { bag = new Dictionary<string, MasteryStats>(); Mastery[kind] = bag; }
            if (!bag.TryGetValue(id, out var s)) { s = new MasteryStats(); bag[id] = s; }
            s.Runs += 1;
            if (stage > s.BestStage) s.BestStage = stage;
            s.BossClears += bossClearsThisRun;
            if (finalScore > s.BestScore) s.BestScore = finalScore;
            if (graduatedThisRun) s.AscMax = Math.Max(s.AscMax, asc);
        }

        // 조회 — PickView/DexView ★ 표기용(Level=충족 마일스톤 수 0~5, Total=5). 미기록 id는 빈
        // MasteryStats(전부 0/-1) 기준으로 계산되어 자연히 Level=0을 반환한다(웹과 동일, masteryOf
        // L228-232 `bag[id] || {runs:0,...}` 폴백).
        public MasteryInfo MasteryOf(string kind, string id)
        {
            MasteryStats s = null;
            if (!string.IsNullOrEmpty(id) && Mastery.TryGetValue(kind, out var bag)) bag.TryGetValue(id, out s);
            if (s == null) s = new MasteryStats();
            return new MasteryInfo
            {
                Level = Formulas.MasteryLevel(kind, s.Runs, s.BestStage, s.BossClears, s.BestScore, s.AscMax),
                Total = Formulas.MasteryTotal(kind),
            };
        }
    }
}
