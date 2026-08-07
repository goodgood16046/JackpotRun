using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 해금 게이트 1건 — Kotlin `data class UnlockGate(val minLevel: Int, val req: List<Pair<String,Long>>, val school: String)`
    // (SlotV2Engine.kt L699-703)의 이식. req는 S1 공유 타입 StatReq(key,value) 배열로 표현한다(AND 조건, 01_engine.md §9.1).
    public sealed class UnlockGate
    {
        public int minLevel;
        public StatReq[] req;
        public string school;
    }

    // 전공 연구 입문 항목 1건 — Kotlin `data class ResearchEntry(val achId: String, val key: String, val threshold: Long)`
    // (SlotV2Engine.kt L732). achId는 참조용 문자열(그 자체로 조회 X, 01_engine.md §9.2), 실제 판정은 key/threshold만 본다.
    public sealed class ResearchEntry
    {
        public string achId;
        public string key;
        public long threshold;
    }

    // 전공(school) 10종 + PERK_GATE_OVERRIDES 45건 — SlotV2Engine.kt L716-807 전사.
    // SchoolReq = SCHOOL_REQ(전공 기본 게이트, L716-727), SchoolResearch = SCHOOL_RESEARCH(전공 연구 입문, L733-744),
    // PerkGateOverrides = PERK_GATE_OVERRIDES(개별 고위험 게이트, L753-807). perkGate()의 최종 결합 순서
    // (①BASE_PERK_IDS 빈게이트 → ②PERK_GATE_OVERRIDES 개별 → ③inferSchool+티어 보정)는 S3 Mods/해금 로직에서
    // 구현한다 — 이 파일은 콘텐츠 테이블만 담는다.
    public static class Schools
    {
        public const int SchoolCount = 10;
        public const int PerkGateOverrideCount = 45;

        // BASE_PERK_IDS 22종 — 게이트 없음(항상 등장). perkGate ①단계와 gatedPool 폴백(SlotV2Engine.kt:1608)에서 사용.
        public static readonly HashSet<string> BasePerkIds = new HashSet<string>
        {
            "study", "preview", "review", "diligence",
            "cherry_up", "book_up", "star_up", "gem_polish",
            "coin_luck", "set_sense",
            "old_book", "cherry_candy", "rusty_coin", "pencil",
            "coffee", "eraser", "ruler", "desk_lamp",
            "cherry_jam", "bookmark", "coin_pouch", "calculator",
        };

        public static readonly string[] All =
        {
            "성장학", "계산학", "경제학", "운명학", "왕관학",
            "저주학", "시간학", "프리즘공학", "씨앗학", "와일드학",
        };

        // SCHOOL_REQ — 전공 기본 게이트 (L716-727)
        public static readonly IReadOnlyDictionary<string, UnlockGate> SchoolReq = new Dictionary<string, UnlockGate>
        {
            ["성장학"] = new UnlockGate
            {
                minLevel = 5,
                req = new[] { new StatReq { key = "cherryTotal", value = 200 }, new StatReq { key = "bestStage", value = 5 } },
                school = "성장학",
            },
            ["계산학"] = new UnlockGate
            {
                minLevel = 7,
                req = new[] { new StatReq { key = "set4Plus", value = 3 }, new StatReq { key = "exactClears", value = 1 } },
                school = "계산학",
            },
            ["경제학"] = new UnlockGate
            {
                minLevel = 8,
                req = new[] { new StatReq { key = "coinTotal", value = 300 }, new StatReq { key = "shopBuys", value = 5 } },
                school = "경제학",
            },
            ["운명학"] = new UnlockGate
            {
                minLevel = 9,
                req = new[] { new StatReq { key = "prayClears", value = 1 }, new StatReq { key = "gambles", value = 3 } },
                school = "운명학",
            },
            ["왕관학"] = new UnlockGate
            {
                minLevel = 10,
                req = new[] { new StatReq { key = "crownTotal", value = 30 }, new StatReq { key = "jackpots", value = 1 } },
                school = "왕관학",
            },
            ["저주학"] = new UnlockGate
            {
                minLevel = 11,
                req = new[] { new StatReq { key = "skullTotal", value = 100 }, new StatReq { key = "curseMax", value = 1 } },
                school = "저주학",
            },
            ["시간학"] = new UnlockGate
            {
                minLevel = 8,
                req = new[] { new StatReq { key = "lastSpinClears", value = 3 }, new StatReq { key = "closeClears", value = 5 } },
                school = "시간학",
            },
            ["프리즘공학"] = new UnlockGate
            {
                minLevel = 12,
                req = new[]
                {
                    new StatReq { key = "prismPicks", value = 3 },
                    new StatReq { key = "bossClears", value = 3 },
                    new StatReq { key = "bestStage", value = 10 },
                },
                school = "프리즘공학",
            },
            ["씨앗학"] = new UnlockGate
            {
                minLevel = 12,
                req = new[] { new StatReq { key = "mstage_garden", value = 8 } },
                school = "씨앗학",
            },
            ["와일드학"] = new UnlockGate
            {
                minLevel = 13,
                req = new[] { new StatReq { key = "set4Plus", value = 10 } },
                school = "와일드학",
            },
        };

        // SCHOOL_RESEARCH — 전공 연구 입문 (ACH-5a, L733-744)
        public static readonly IReadOnlyDictionary<string, ResearchEntry> SchoolResearch = new Dictionary<string, ResearchEntry>
        {
            ["성장학"] = new ResearchEntry { achId = "cherry300", key = "cherryTotal", threshold = 300 },
            ["계산학"] = new ResearchEntry { achId = "pc_set4_3", key = "set4Plus", threshold = 3 },
            ["경제학"] = new ResearchEntry { achId = "coin300", key = "coinTotal", threshold = 300 },
            ["운명학"] = new ResearchEntry { achId = "gamble3", key = "gambles", threshold = 3 },
            ["왕관학"] = new ResearchEntry { achId = "crown30ext", key = "crownTotal", threshold = 30 },
            ["저주학"] = new ResearchEntry { achId = "skull100", key = "skullTotal", threshold = 100 },
            ["시간학"] = new ResearchEntry { achId = "lastspin3", key = "lastSpinClears", threshold = 3 },
            ["프리즘공학"] = new ResearchEntry { achId = "prismPick3", key = "prismPicks", threshold = 3 },
            ["씨앗학"] = new ResearchEntry { achId = "sp_seed10", key = "seedTotal", threshold = 10 },
            ["와일드학"] = new ResearchEntry { achId = "sp_wild10", key = "wildTotal", threshold = 10 },
        };

        // PERK_GATE_OVERRIDES — 개별 고위험 게이트 45건 (L753-807)
        public static readonly IReadOnlyDictionary<string, UnlockGate> PerkGateOverrides = new Dictionary<string, UnlockGate>
        {
            // 증강
            ["overdrive"] = new UnlockGate { minLevel = 12, req = new[] { new StatReq { key = "prismPicks", value = 5 }, new StatReq { key = "bossClears", value = 3 } }, school = "프리즘공학" },
            ["short_day"] = new UnlockGate { minLevel = 15, req = new[] { new StatReq { key = "bestStage", value = 15 }, new StatReq { key = "exactClears", value = 3 } }, school = "프리즘공학" },
            ["glass_cannon"] = new UnlockGate { minLevel = 15, req = new[] { new StatReq { key = "bestScore", value = 30000 }, new StatReq { key = "bestStage", value = 10 } }, school = "프리즘공학" },
            ["supernova"] = new UnlockGate { minLevel = 17, req = new[] { new StatReq { key = "bestScore", value = 50000 }, new StatReq { key = "bossClears", value = 8 } }, school = "프리즘공학" },
            ["endgame_rush"] = new UnlockGate { minLevel = 14, req = new[] { new StatReq { key = "lastSpinClears", value = 10 }, new StatReq { key = "closeClears", value = 10 } }, school = "시간학" },
            ["wild_world"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "set4Plus", value = 10 } }, school = "와일드학" },
            ["joker"] = new UnlockGate { minLevel = 16, req = new[] { new StatReq { key = "jackpots", value = 5 }, new StatReq { key = "set4Plus", value = 20 } }, school = "와일드학" },
            ["jackpot"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "crownTotal", value = 100 }, new StatReq { key = "jackpots", value = 3 } }, school = "왕관학" },
            ["mega_jackpot"] = new UnlockGate { minLevel = 16, req = new[] { new StatReq { key = "crownTotal", value = 300 }, new StatReq { key = "jackpots", value = 10 } }, school = "왕관학" },
            ["seed_garden"] = new UnlockGate { minLevel = 12, req = new[] { new StatReq { key = "mstage_garden", value = 8 } }, school = "씨앗학" },
            ["great_harvest"] = new UnlockGate { minLevel = 15, req = new[] { new StatReq { key = "mstage_garden", value = 8 } }, school = "씨앗학" },
            ["key_master"] = new UnlockGate { minLevel = 12, req = new[] { new StatReq { key = "coinTotal", value = 500 }, new StatReq { key = "shopBuys", value = 10 } }, school = "경제학" },
            ["gamblers_dice"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "allinWins", value = 5 }, new StatReq { key = "gambles", value = 10 } }, school = "운명학" },
            // 유물
            ["crystal_ball"] = new UnlockGate { minLevel = 9, req = new[] { new StatReq { key = "prayClears", value = 1 } }, school = "운명학" },
            ["fate_handle"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "prayClears", value = 3 } }, school = "운명학" },
            ["gamblers_eye"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "allinWins", value = 5 } }, school = "운명학" },
            ["piggy_bank"] = new UnlockGate { minLevel = 8, req = new[] { new StatReq { key = "coinTotal", value = 300 }, new StatReq { key = "shopBuys", value = 5 } }, school = "경제학" },
            ["hourglass_r"] = new UnlockGate { minLevel = 10, req = new[] { new StatReq { key = "lastSpinClears", value = 5 } }, school = "시간학" },
            ["skull_idol"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "skullTotal", value = 300 } }, school = "저주학" },
            ["ominous_skull"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "curseMax", value = 3 } }, school = "저주학" },
            ["black_report"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "curseBossClears", value = 1 } }, school = "저주학" },
            ["crown_jewel"] = new UnlockGate { minLevel = 10, req = new[] { new StatReq { key = "crownTotal", value = 50 } }, school = "왕관학" },
            ["crown_stand"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "crownTotal", value = 100 } }, school = "왕관학" },
            ["kings_ledger"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "jackpots", value = 3 } }, school = "왕관학" },
            ["focus_ring"] = new UnlockGate { minLevel = 8, req = new[] { new StatReq { key = "exactClears", value = 1 } }, school = "계산학" },
            ["silver_mirror"] = new UnlockGate { minLevel = 9, req = new[] { new StatReq { key = "set4Plus", value = 5 } }, school = "계산학" },
            ["greed_goblet"] = new UnlockGate { minLevel = 12, req = new[] { new StatReq { key = "bestScore", value = 20000 } }, school = "성장학" },
            ["flame_canister"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "mstage_flame", value = 8 } }, school = "저주학" },
            ["cursed_wallet"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "coinTotal", value = 500 }, new StatReq { key = "curseMax", value = 3 } }, school = "저주학" },
            // 신규 16종 (초반성장=성장학 / 운빨=운명학 / 막판역전=시간학 / 세트콤보=계산학 / 해골저주=저주학)
            ["early_prep"] = new UnlockGate { minLevel = 3, req = new[] { new StatReq { key = "cherryTotal", value = 100 } }, school = "성장학" },
            ["growth_log"] = new UnlockGate { minLevel = 5, req = new[] { new StatReq { key = "cherryTotal", value = 120 } }, school = "성장학" },
            ["early_adapt"] = new UnlockGate { minLevel = 6, req = new[] { new StatReq { key = "cherryTotal", value = 200 }, new StatReq { key = "bestStage", value = 5 } }, school = "성장학" },
            ["snowball"] = new UnlockGate { minLevel = 12, req = new[] { new StatReq { key = "cherryTotal", value = 400 }, new StatReq { key = "bestStage", value = 10 } }, school = "성장학" },
            ["fortune_check"] = new UnlockGate { minLevel = 7, req = new[] { new StatReq { key = "prayClears", value = 1 } }, school = "운명학" },
            ["luck_accum"] = new UnlockGate { minLevel = 9, req = new[] { new StatReq { key = "prayClears", value = 1 }, new StatReq { key = "gambles", value = 3 } }, school = "운명학" },
            ["fate_burst"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "prayClears", value = 3 }, new StatReq { key = "jackpots", value = 3 } }, school = "운명학" },
            ["late_focus"] = new UnlockGate { minLevel = 6, req = new[] { new StatReq { key = "lastSpinClears", value = 3 } }, school = "시간학" },
            ["cliff_focus"] = new UnlockGate { minLevel = 8, req = new[] { new StatReq { key = "lastSpinClears", value = 3 }, new StatReq { key = "closeClears", value = 5 } }, school = "시간학" },
            ["fate_bell"] = new UnlockGate { minLevel = 14, req = new[] { new StatReq { key = "closeClears", value = 10 }, new StatReq { key = "bossClears", value = 5 } }, school = "시간학" },
            ["pair_match"] = new UnlockGate { minLevel = 5, req = new[] { new StatReq { key = "set4Plus", value = 3 } }, school = "계산학" },
            ["puzzle_sense"] = new UnlockGate { minLevel = 7, req = new[] { new StatReq { key = "set4Plus", value = 3 }, new StatReq { key = "exactClears", value = 1 } }, school = "계산학" },
            ["perfect_shape"] = new UnlockGate { minLevel = 13, req = new[] { new StatReq { key = "set4Plus", value = 10 }, new StatReq { key = "exactClears", value = 3 } }, school = "계산학" },
            ["skull_watch"] = new UnlockGate { minLevel = 9, req = new[] { new StatReq { key = "skullTotal", value = 100 } }, school = "저주학" },
            ["sacrifice"] = new UnlockGate { minLevel = 11, req = new[] { new StatReq { key = "skullTotal", value = 200 }, new StatReq { key = "curseMax", value = 3 } }, school = "저주학" },
            ["black_diploma"] = new UnlockGate { minLevel = 14, req = new[] { new StatReq { key = "skullTotal", value = 300 }, new StatReq { key = "curseBossClears", value = 1 } }, school = "저주학" },
        };
    }
}
