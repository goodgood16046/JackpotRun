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
    //     많아 이 슬라이스(프로필·스탯트래킹·업적판정) 범위 밖이다 — 작업 지시서에 명시된 "면허(lic_*) 달성 시
    //     장치 해금"만 AchievementEngine이 구현한다.
    public sealed class PlayerProfile
    {
        // 156개 고유 stat key(03_meta §5.3) + StatTracker가 쓰는 파생-원천 키(bld_<id> 25종 등) 통합 저장소.
        // 키가 없으면 0(Kotlin `stat[key] ?: 0L`과 동일 관례) — GetStat/Inc/SetMax로만 조작할 것.
        public readonly Dictionary<string, long> Stats = new Dictionary<string, long>();

        // 달성한 업적 id 집합 — Kotlin SlotV2AchRow.unlocked CSV 대응.
        public readonly HashSet<string> AchievedIds = new HashSet<string>();

        // 면허(lic_*) 달성 등으로 영구 보유가 인정된 장치 id 집합 — Kotlin SlotV2ScoreRow.ownedDevices 대응.
        // AchievementEngine.Evaluate가 lic_* 업적 신규 달성 시 여기 추가한다(작업 지시 3번 요구사항).
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

        // ── 해금 조회 — 기존 API 재사용만(Unlocks.Meets·Schools·Shop.PerkUnlocked), 재정의 금지 ──────
        // 캐릭터/머신은 공유 StatReq 계약(01_engine.md §9.1)이라 Unlocks.Meets를 직접 재사용.
        // M1(Opus 1차 검수, 2026-07-31): 원재료 Stats가 아니라 AchievementEngine.ComposeStat(this)로 판정한다
        // — 일부 캐릭터의 unlockReq가 파생키를 참조한다(예: prodigy = "distinctCharS10">=7, Kotlin
        // Character 정의 L362). 원재료 Stats만 보면 distinctCharS10 키 자체가 없어(StatTracker는 파생키를
        // 저장하지 않음) 항상 0으로 취급돼 영구 잠긴다.
        public bool IsCharUnlocked(Character c) => c != null && Unlocks.Meets(c.unlockReq, AchievementEngine.ComposeStat(this));
        public bool IsMachineUnlocked(Machine m) => m != null && Unlocks.Meets(m.unlockReq, AchievementEngine.ComposeStat(this));

        // 퍽(증강/유물/저주) 해금 — Shop.PerkUnlocked(internal, 같은 어셈블리)가 Unlocks.Meets + Schools
        // (BasePerkIds/SchoolReq/PerkGateOverrides/SchoolResearch)를 이미 결합해 두었다(Shop.cs 헤더 주석
        // "Schools.cs의 ... 단일 소스로 결합만 한다") — 그 로직을 그대로 재사용한다(중복 구현 금지).
        public bool IsPerkUnlocked(Perk p) => Shop.PerkUnlocked(p, Stats);

        // 장치 해금 — Kotlin deviceUnlocked(dev,stat) = achieved(dev.unlockAch) ∪ 영구보유(ownedDevices).
        // lic_* 업적 자체의 판정(면허 조건 AND)은 AchievementEngine.ComposeStat의 파생키(lic_dev_<id>)가
        // 담당하고, 여기서는 "이미 달성된 업적 id 집합"만 본다(달성 여부 자체의 판정 로직은 재구현하지 않음).
        public bool IsDeviceUnlocked(DeviceDef d)
        {
            if (d == null) return false;
            if (OwnedDevices.Contains(d.id)) return true;
            return !string.IsNullOrEmpty(d.unlockAch) && AchievedIds.Contains(d.unlockAch);
        }

        // 현재 장착 가능한(해금된) 장치 목록 — devicesOwned 스탯과 동일한 모집합(unlockAch 있는 것만).
        public IEnumerable<DeviceDef> UnlockedDevices() => Devices.All.Where(IsDeviceUnlocked);
    }
}
