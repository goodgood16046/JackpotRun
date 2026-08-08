using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 심화모드(deepMode) 심볼 주머니(pouch) 콘텐츠 데이터 + 순수 판정 함수 — 웹 파리티 P7-1
    // (WEB_PARITY_DESIGN.md §1-A #19 "심화모드(심볼 덱/주머니)" 1/4 슬라이스: 주머니 코어 + 심볼
    // 카탈로그). 웹 `public/play/data.js`(POUCH_SYMBOLS·DEFAULT_UNLOCKED_SYMS·POUCH_CAT·POUCH_RARITY·
    // POUCH_USE·TIER_BY_RARITY·DEEP 상수·JACKPOT_TAG·JACKPOT_TAG_DECK_MAX)와 `engine.js`
    // (pouchTotal·compressionPenalty·symCatOf/symTierOf·pouchValidate)를 그대로 전사한다.
    //
    // 이 파일은 Content 계층(Run/RunState 비의존) — Symbols.cs(같은 계층)만 참조한다. 주머니에서 실제
    // 릴 칸을 뽑는 PouchDraw(Rng+Cell 필요)는 Run 계층(Engine/Run/PouchOps.cs)에 있다.
    //
    // [P7-1 범위 밖 — 데이터는 옮기지 않음, 보고 대상] REPAIR_SERVICES(정비소)·RANDPACK_DIST/COUNT/
    // PRISM_BONUS·DESIGNATED_UPGRADE_CHANCE·PACKAGE_CHANCE(오퍼 2-step)·REL_MIN/REL_MIN_BY_SYM(전공
    // 게이트)·ARCH_T1/T2(전공 발동)·FEVER_*(피버 게이지)·BELL_FEST_MUL·EARLY_EXP_BOOST_IDS(오퍼 가중)·
    // ACH_SYMBOL_UNLOCK(심볼 해금)·POUCH_UPGRADE(상위계열 매핑, upgrade 서비스 전용) — 전부 P7-2/3
    // (심볼퍽/정비소/전공/잭팟태그/피버/오퍼) 또는 P7-4(업적) 범위. 작업 지시 §범위 그대로.
    public static class Pouch
    {
        // ── §1.1 V3P1 심볼 카테고리 — base(9)/harmful(해골·빈칸·저주 계열)/special(나머지) 전수 분류.
        // 웹 data.js POUCH_CAT(71개 키, empty/random 포함) 그대로. 미등록 id는 symCatOf()가 폴백 처리.
        public static readonly Dictionary<string, string> Cat = new Dictionary<string, string>
        {
            { "cherry", "base" },
            { "book", "base" },
            { "star", "base" },
            { "gem", "base" },
            { "coin", "base" },
            { "flame", "base" },
            { "magnet", "base" },
            { "bomb", "base" },
            { "skull", "harmful" },
            { "skull_black", "harmful" },
            { "empty", "harmful" },
            { "random", "harmful" },
            { "bloodrop", "harmful" },
            { "black_candle_sym", "harmful" },
            { "unstable_bomb", "harmful" },
            { "curse_eye", "harmful" },
            { "cherry_ripe", "special" },
            { "tome", "special" },
            { "gem_cut", "special" },
            { "coin_bag", "special" },
            { "ember", "special" },
            { "crown", "special" },
            { "wild", "special" },
            { "lucky7", "special" },
            { "prism_sym", "special" },
            { "seed_basic", "special" },
            { "sprout", "special" },
            { "alarm", "special" },
            { "catalyst", "special" },
            { "purifier", "special" },
            { "mirror_sym", "special" },
            { "target", "special" },
            { "jigsaw", "special" },
            { "receipt", "special" },
            { "coupon", "special" },
            { "cart", "special" },
            { "battery_sym", "special" },
            { "gear", "special" },
            { "set_piece", "special" },
            { "key_gold", "special" },
            { "shield", "special" },
            { "exam_paper", "special" },
            { "magic_wand", "special" },
            { "hourglass", "special" },
            { "highlighter", "special" },
            { "review_book", "special" },
            { "repair_kit", "special" },
            { "bandage", "special" },
            { "knot", "special" },
            { "safepin", "special" },
            { "energypack", "special" },
            { "crystal", "special" },
            { "temp_wild", "special" },
            { "fake_crown_sym", "special" },
            { "fate_vortex", "special" },
            { "evo_core", "special" },
            { "black_card", "harmful" },
            { "shackle", "harmful" },
            { "small_bell", "special" },
            { "echo_bell", "special" },
            { "golden_bell", "special" },
            { "festival_bell", "special" },
            { "bell_ticket", "special" },
            { "reach_mark", "special" },
            { "retry_reel", "special" },
            { "jackpot_ticket", "special" },
            { "slot_shard", "special" },
            { "jackpot_wand", "special" },
            { "cheer", "special" },
            { "big_boom", "special" },
            { "jackpot_crown", "special" },
        };

        // 희귀도(기본|고급|희귀|전설|저주) — 웹 data.js POUCH_RARITY(71개 키) 그대로. 미등록 id는
        // pouchRarityOf()가 "기본"으로 폴백(웹 `POUCH_RARITY[id] || "기본"`과 동일).
        public static readonly Dictionary<string, string> Rarity = new Dictionary<string, string>
        {
            { "cherry", "기본" },
            { "book", "기본" },
            { "star", "기본" },
            { "gem", "기본" },
            { "coin", "기본" },
            { "magnet", "기본" },
            { "empty", "기본" },
            { "random", "고급" },
            { "cherry_ripe", "고급" },
            { "tome", "고급" },
            { "gem_cut", "고급" },
            { "coin_bag", "고급" },
            { "bomb", "고급" },
            { "flame", "희귀" },
            { "ember", "희귀" },
            { "wild", "희귀" },
            { "crown", "전설" },
            { "skull", "저주" },
            { "skull_black", "저주" },
            { "seed_basic", "기본" },
            { "alarm", "기본" },
            { "battery_sym", "기본" },
            { "purifier", "고급" },
            { "mirror_sym", "고급" },
            { "sprout", "고급" },
            { "receipt", "고급" },
            { "set_piece", "고급" },
            { "key_gold", "고급" },
            { "shield", "고급" },
            { "highlighter", "고급" },
            { "magic_wand", "희귀" },
            { "hourglass", "희귀" },
            { "coupon", "희귀" },
            { "cart", "희귀" },
            { "review_book", "희귀" },
            { "repair_kit", "희귀" },
            { "catalyst", "희귀" },
            { "gear", "희귀" },
            { "exam_paper", "희귀" },
            { "jigsaw", "희귀" },
            { "target", "희귀" },
            { "lucky7", "전설" },
            { "prism_sym", "전설" },
            { "bloodrop", "저주" },
            { "black_candle_sym", "저주" },
            { "unstable_bomb", "저주" },
            { "curse_eye", "저주" },
            { "bandage", "고급" },
            { "knot", "고급" },
            { "safepin", "고급" },
            { "energypack", "고급" },
            { "crystal", "희귀" },
            { "temp_wild", "희귀" },
            { "fake_crown_sym", "전설" },
            { "fate_vortex", "전설" },
            { "evo_core", "전설" },
            { "black_card", "저주" },
            { "shackle", "저주" },
            { "small_bell", "고급" },
            { "echo_bell", "고급" },
            { "bell_ticket", "고급" },
            { "golden_bell", "희귀" },
            { "festival_bell", "희귀" },
            { "reach_mark", "고급" },
            { "jackpot_ticket", "고급" },
            { "retry_reel", "희귀" },
            { "slot_shard", "고급" },
            { "jackpot_wand", "희귀" },
            { "cheer", "고급" },
            { "big_boom", "희귀" },
            { "jackpot_crown", "전설" },
        };

        // ── §1.2 V3P1 심볼 티어 축 — 기본/고급→SILVER, 희귀→GOLD, 전설→PRISM, 저주→CURSE.
        // 웹 data.js TIER_BY_RARITY 그대로. pouchRarityOf 경유(드리프트 방지) — symTierOf()가 소비.
        public static readonly Dictionary<string, string> TierByRarity = new Dictionary<string, string>
        {
            { "기본", "SILVER" },
            { "고급", "SILVER" },
            { "희귀", "GOLD" },
            { "전설", "PRISM" },
            { "저주", "CURSE" },
        };

        // 소모형/일회용 속성 — 웹 data.js POUCH_USE(12개 키) 그대로. "instant"=등장 즉시 발동+덱 -1,
        // "fuse"=덱 상주, 조건 충족 시 발동+제거. 미등록 id는 use 없음(영구 상주, C# 조회는 null/미존재).
        // [P7-1 범위] 이번 슬라이스는 "instant" 값만 소비한다(DeepRunHooks.ConsumeInstantSymbols) —
        // "fuse"는 실제 발동 조건(P7-2/3 각 심볼 효과)이 없어 이번 슬라이스에서 아직 아무 것도 하지
        // 않는다(작업 지시 6번 "소모 대상은 P7-3에서 본격화" 그대로).
        public static readonly Dictionary<string, string> Use = new Dictionary<string, string>
        {
            { "bandage", "instant" },
            { "knot", "instant" },
            { "energypack", "instant" },
            { "safepin", "fuse" },
            { "crystal", "fuse" },
            { "temp_wild", "fuse" },
            { "fake_crown_sym", "instant" },
            { "evo_core", "instant" },
            { "fate_vortex", "fuse" },
            { "black_card", "fuse" },
            { "bell_ticket", "fuse" },
            { "jackpot_ticket", "fuse" },
        };

        // MVP 기본(실제로는 71종) — 주머니에 담길 수 있는 심볼 id 후보 풀(표시/오퍼 후보). 웹 data.js
        // POUCH_SYMBOLS 선언 순서 그대로 — RunState.Pouch(Dictionary)는 삽입 순서를 보장하지 않으므로
        // (C# Dictionary 열거 순서는 언어 계약이 아니다), PouchOps.PouchDraw의 누적가중치 스캔은 이
        // 배열의 순서를 단일 진실 공급원으로 삼는다(웹은 JS 객체 삽입 순서를 그대로 쓰지만, START_POUCH
        // 9종은 이 배열에서도 동일한 상대 순서로 먼저 나와 초기 상태의 스캔 순서는 웹과 사실상 같다 —
        // 이후 보상/상점으로 새 종류가 추가돼도 이 고정 순서로 스캔하므로 "같은 seed → 같은 결과"라는
        // 자체 재현성 계약(ENGINE_PORT_DESIGN.md 원칙 2)은 그대로 유지된다).
        public static readonly string[] Symbols71 =
        {
            "cherry", "book", "star", "gem", "coin", "skull",
            "flame", "magnet", "bomb", "crown", "wild", "cherry_ripe",
            "tome", "gem_cut", "coin_bag", "skull_black", "ember", "empty",
            "random", "seed_basic", "sprout", "catalyst", "purifier", "magic_wand",
            "mirror_sym", "target", "jigsaw", "alarm", "hourglass", "receipt",
            "coupon", "cart", "battery_sym", "gear", "set_piece", "key_gold",
            "shield", "exam_paper", "highlighter", "review_book", "repair_kit", "bloodrop",
            "black_candle_sym", "unstable_bomb", "curse_eye", "lucky7", "prism_sym", "bandage",
            "knot", "safepin", "energypack", "crystal", "temp_wild", "fake_crown_sym",
            "fate_vortex", "evo_core", "black_card", "shackle", "small_bell", "echo_bell",
            "golden_bell", "festival_bell", "bell_ticket", "reach_mark", "retry_reel", "jackpot_ticket",
            "slot_shard", "jackpot_wand", "cheer", "big_boom", "jackpot_crown",
        };

        // 처음부터 개방(기본 해금)되는 심볼 집합 — 웹 data.js DEFAULT_UNLOCKED_SYMS(58개) 그대로.
        // Symbols71 중 이 집합에 없는 나머지 13종(ACH_SYMBOL_UNLOCK 값)은 심화 업적 해금 전용(P7-4
        // 범위) — 이번 슬라이스는 데이터만 옮기고 해금 판정 로직(profile.symUnlocked 병합)은 아직
        // 배선하지 않는다(오퍼/보상 자체가 P7-2/3 범위라 소비처가 없음).
        public static readonly HashSet<string> DefaultUnlocked = new HashSet<string>
        {
            "cherry", "book", "star", "gem", "coin", "skull",
            "flame", "magnet", "bomb", "crown", "cherry_ripe", "tome",
            "gem_cut", "coin_bag", "skull_black", "ember", "empty", "random",
            "wild", "seed_basic", "sprout", "alarm", "receipt", "coupon",
            "cart", "battery_sym", "gear", "set_piece", "key_gold", "exam_paper",
            "bloodrop", "curse_eye", "unstable_bomb", "repair_kit", "bandage", "knot",
            "safepin", "energypack", "crystal", "temp_wild", "fake_crown_sym", "fate_vortex",
            "evo_core", "black_card", "shackle", "small_bell", "echo_bell", "golden_bell",
            "festival_bell", "bell_ticket", "reach_mark", "retry_reel", "jackpot_ticket", "slot_shard",
            "jackpot_wand", "cheer", "big_boom", "jackpot_crown",
        };

        // 잭팟 태그 맵 — 웹 data.js JACKPOT_TAG(§9.0 V3 J1) 그대로. 아이콘이 달라도 같은 태그면 잭팟
        // 판정(심화모드 전용, P7-2/3 evaluate 확장 범위) — 이 슬라이스는 PouchValidate 7규칙 중
        // "잭팟태그 특수심볼 ≤ JackpotTagDeckMax" 판정에만 쓴다(태그 발동 로직 자체는 P7-2/3).
        public static readonly Dictionary<string, string> JackpotTag = new Dictionary<string, string>
        {
            { "crown", "crown" },
            { "fake_crown_sym", "crown" },
            { "jackpot_crown", "crown" },
            { "lucky7", "seven" },
            { "coin", "coin" },
            { "coin_bag", "coin" },
            { "prism_sym", "prism" },
            { "bloodrop", "curse" },
            { "curse_eye", "curse" },
            { "black_candle_sym", "curse" },
            { "unstable_bomb", "curse" },
            { "black_card", "curse" },
            { "shackle", "curse" },
            { "small_bell", "bell" },
            { "echo_bell", "bell" },
            { "golden_bell", "bell" },
            { "festival_bell", "bell" },
            { "bell_ticket", "bell" },
        };

        // 같은 jackpotTag를 가진 특수(cat=special)심볼의 덱 상한 — 웹 data.js JACKPOT_TAG_DECK_MAX(8).
        public const int JackpotTagDeckMax = 8;

        // ── §1.3 V3P1 시작 덱 — 웹 data.js DEEP.START_POUCH(총 30, 왕관/empty/random 시작 제외).
        // 선언 순서 = Symbols71의 앞 9개와 동일 순서(주석 참조 — PouchOps.PouchDraw 스캔 순서 일치).
        public static readonly (string id, int n)[] StartPouchEntries =
        {
            ("cherry", 6), ("book", 5), ("star", 4), ("gem", 4), ("coin", 4),
            ("skull", 3), ("flame", 2), ("magnet", 1), ("bomb", 1),
        };

        // 매 런 시작 시 호출 — 항상 새 Dictionary를 반환한다(런 간 공유 금지, 웹 `startPouch = () =>
        // ({ ...DEEP.START_POUCH })`와 동일한 얕은 복사 계약).
        public static Dictionary<string, int> NewStartPouch()
        {
            var p = new Dictionary<string, int>(StartPouchEntries.Length);
            foreach (var (id, n) in StartPouchEntries) p[id] = n;
            return p;
        }

        // ── DEEP 상수(덱 용량/상한/압축 패널티/초반 완화 램프) — 웹 data.js DEEP{} 그대로 이 슬라이스
        // 범위분만 전사(REPAIR_SERVICES 등 P7-2/3 전용 항목은 파일 헤더 각주에 명시하고 옮기지 않음).
        public const int DeckMin = 20;
        public const int DeckMax = 40;
        public const int DeckBase = 30;
        public const int MinKinds = 7;
        public const double TagMaxRatio = 0.60;
        public const int CrownMax = 2;
        public const int WildMax = 4;

        // 특수 티어 상한(§1.5 V3P1 RARITY_MAX 재구조화) — base/harmful 심볼은 상한 없음(∞), cat=special
        // 심볼만 티어별 개수 합산 상한. 웹 "<TIER>_special" 키 대신 튜플 배열로 고정 순서를 둔다(에러
        // 메시지 순서 결정론 — PouchValidate가 이 배열 순서로 순회).
        public static readonly (string tier, int cap)[] RarityMaxSpecial =
        {
            ("SILVER", 10), ("GOLD", 6), ("PRISM", 2),
        };

        // 압축 패널티 표 — 웹 data.js DEEP.COMPRESSION(le 오름차순, total<=le 첫 매치가 배수).
        public static readonly (int le, double mul)[] Compression =
        {
            (22, 1.15), (24, 1.09), (26, 1.06), (28, 1.03),
        };

        // §11 초반 밸런스 완화 — stage 1~4 요구경험치 완화 램프(5+는 무배율 1.0). 웹 data.js
        // DEEP.EARLY_QUOTA 그대로.
        public static readonly Dictionary<int, double> EarlyQuota = new Dictionary<int, double>
        {
            { 1, 0.75 }, { 2, 0.85 }, { 3, 0.90 }, { 4, 0.95 },
        };

        // ── 순수 함수 (웹 engine.js pouchTotal/compressionPenalty/symCatOf/symTierOf/pouchValidate) ──

        // 주머니 총량(양수 카운트 합) — 웹 `pouchTotal`.
        public static int Total(IReadOnlyDictionary<string, int> pouch)
        {
            if (pouch == null) return 0;
            int sum = 0;
            foreach (var kv in pouch) if (kv.Value > 0) sum += kv.Value;
            return sum;
        }

        // 압축 패널티 → 요구경험치 배수(≥1). 총량 높으면 1(패널티 없음) — 웹 `compressionPenalty`.
        public static double CompressionPenalty(int total)
        {
            for (int i = 0; i < Compression.Length; i++)
                if (total <= Compression[i].le) return Compression[i].mul;
            return 1.0;
        }

        // stage 1~4 요구경험치 완화 배수(5+는 1.0) — 웹 `DEEP.EARLY_QUOTA[r.stage] ?? 1.0`.
        public static double EarlyQuotaMul(int stage) => EarlyQuota.TryGetValue(stage, out var m) ? m : 1.0;

        // 심볼 id → 희귀도(기본|고급|희귀|전설|저주). 미등록은 "기본" — 웹 `pouchRarityOf`.
        public static string RarityOf(string id) => Rarity.TryGetValue(id, out var r) ? r : "기본";

        // 심볼 id → 티어(SILVER|GOLD|PRISM|CURSE). RarityOf 경유(드리프트 방지) — 웹 `symTierOf`.
        public static string TierOf(string id) =>
            TierByRarity.TryGetValue(RarityOf(id), out var t) ? t : "SILVER";

        // 심볼 id → 카테고리(base|special|harmful). 미등록은 저주 희귀도면 harmful, 그 외 special —
        // 웹 `symCatOf`.
        public static string CatOf(string id)
        {
            if (Cat.TryGetValue(id, out var c)) return c;
            return RarityOf(id) == "저주" ? "harmful" : "special";
        }

        // Phase 3 자동 소멸 대상(base && !harmful) — 웹 `isAutoDecayTarget`. skull은 Cat에서 harmful로
        // 재정의돼 있어(base 출신이지만 harmful 우선) 자동으로 제외된다.
        public static bool IsAutoDecayTarget(string id) => CatOf(id) == "base";

        // 심볼 id → 잭팟 태그("crown"|"seven"|"coin"|"prism"|"curse"|"bell"|null). 미등록=null —
        // 웹 `jackpotTagOf`.
        public static string JackpotTagOf(string id) => JackpotTag.TryGetValue(id, out var jt) ? jt : null;

        // 덱 검증 결과 — 웹 `pouchValidate` 반환 { ok, errors[], total, kinds }.
        public sealed class ValidateResult
        {
            public bool Ok;
            public readonly List<string> Errors = new List<string>();
            public int Total;
            public int Kinds;
        }

        // 주머니 유효성 검증 7규칙(웹 engine.js `pouchValidate`, DEEP 기본값 — opts로 상하한/태그비중을
        // 오버라이드할 수 있으나 P7-2/3(정비소 확장/압축·전공 태그완화) 전까지는 항상 기본값): 총량
        // 20~40 · 종류≥7 · 왕관≤2 · 와일드≤4 · 특수 티어 상한(SILVER≤10/GOLD≤6/PRISM≤2) · 같은 태그
        // ≤60% · 잭팟태그 특수심볼≤8.
        public static ValidateResult Validate(
            IReadOnlyDictionary<string, int> pouch,
            int? totalMaxOverride = null, int? totalMinOverride = null, double? tagMaxRatioOverride = null)
        {
            var result = new ValidateResult();
            var p = pouch ?? new Dictionary<string, int>();
            int total = Total(p);
            int tMax = totalMaxOverride ?? DeckMax;
            int tMin = totalMinOverride ?? DeckMin;
            double tagMax = tagMaxRatioOverride ?? TagMaxRatio;

            var kindEntries = new List<KeyValuePair<string, int>>();
            foreach (var kv in p) if (kv.Value > 0) kindEntries.Add(kv);
            int kinds = kindEntries.Count;

            result.Total = total;
            result.Kinds = kinds;

            if (total < tMin) result.Errors.Add($"총량 {total} < 최소 {tMin}");
            if (total > tMax) result.Errors.Add($"총량 {total} > 최대 {tMax}");
            if (kinds < MinKinds) result.Errors.Add($"심볼 종류 {kinds} < 최소 {MinKinds}");

            int crown = p.TryGetValue("crown", out var cv) ? cv : 0;
            if (crown > CrownMax) result.Errors.Add($"👑왕관 {crown} > 상한 {CrownMax}");
            int wild = p.TryGetValue("wild", out var wv) ? wv : 0;
            if (wild > WildMax) result.Errors.Add($"🌀와일드 {wild} > 상한 {WildMax}");

            // 특수 티어 상한 — cat=special 심볼만 티어별 합산.
            var byTierSpecial = new Dictionary<string, int>();
            foreach (var kv in kindEntries)
            {
                if (CatOf(kv.Key) != "special") continue;
                var tk = TierOf(kv.Key);
                byTierSpecial[tk] = byTierSpecial.TryGetValue(tk, out var c) ? c + kv.Value : kv.Value;
            }
            for (int i = 0; i < RarityMaxSpecial.Length; i++)
            {
                var (tier, cap) = RarityMaxSpecial[i];
                int cur = byTierSpecial.TryGetValue(tier, out var c2) ? c2 : 0;
                if (cur > cap) result.Errors.Add($"특수 {tier} {cur} > 상한 {cap}");
            }

            // 잭팟태그 특수심볼 상한 — base 심볼(왕관 등)은 제외, cat=special만 카운트.
            var byJtag = new Dictionary<string, int>();
            foreach (var kv in kindEntries)
            {
                if (CatOf(kv.Key) != "special") continue;
                var jt = JackpotTagOf(kv.Key);
                if (jt == null) continue;
                byJtag[jt] = byJtag.TryGetValue(jt, out var c3) ? c3 + kv.Value : kv.Value;
            }
            foreach (var kv in byJtag)
                if (kv.Value > JackpotTagDeckMax)
                    result.Errors.Add($"잭팟태그 #{kv.Key} 특수심볼 {kv.Value} > 상한 {JackpotTagDeckMax}");

            // 같은 태그 최대 60%(태그별 개수 / 총량). empty/random은 태그 없어 무관.
            if (total > 0)
            {
                var byTag = new Dictionary<string, int>();
                foreach (var kv in kindEntries)
                {
                    var info = Symbols.ById(kv.Key);
                    if (info?.tags == null) continue;
                    for (int i = 0; i < info.tags.Length; i++)
                    {
                        var tag = info.tags[i];
                        byTag[tag] = byTag.TryGetValue(tag, out var c4) ? c4 + kv.Value : kv.Value;
                    }
                }
                foreach (var kv in byTag)
                {
                    double ratio = (double)kv.Value / total;
                    if (ratio > tagMax + 1e-9)
                        result.Errors.Add($"#{kv.Key} {JsRound(ratio * 100)}% > {JsRound(tagMax * 100)}%");
                }
            }

            result.Ok = result.Errors.Count == 0;
            return result;
        }

        // JS `Math.round`(항상 +Infinity 방향 반올림) 동치 — C# `Math.Round`의 기본 은행가 반올림
        // (MidpointRounding.ToEven)은 정확히 .5인 값에서 웹과 어긋날 수 있어(예: 62.5 → C#은 62,
        // JS는 63) 에러 메시지 표시 문구에서만 이 헬퍼로 통일한다(ok/errors 존재 여부 판정 자체는
        // ratio > tagMax 비교로만 결정되므로 이 반올림 방식과 무관 — 순수 표시 정확도 문제).
        private static int JsRound(double x) => (int)System.Math.Floor(x + 0.5);
    }
}
