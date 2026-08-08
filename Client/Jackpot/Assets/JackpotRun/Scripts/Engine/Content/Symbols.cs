using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 심볼 특수효과 종류 — Kotlin enum class Sp (SlotV2Engine.kt L98) 10종 + 웹 파리티 P7-1
    // (WEB_PARITY_DESIGN.md §1-A #19, 웹 data.js SYMS의 special 필드) 신규 51종. 신규 51종은 전부
    // 심화모드(주머니) 전용 심볼의 특수효과 표식일 뿐이라 이 슬라이스(P7-1)의 SpinResolver.Evaluate는
    // 이 값들에 대응하는 case를 하나도 추가하지 않는다 — 웹 evaluate()가 "미지 special은 NONE 취급
    // (exp/score/coin/tags만 흡수)"하는 것과 동일하게, switch에 없는 값은 그냥 아무 특수효과도 없이
    // 지나간다(진짜 효과 배선은 P7-2/3 각 계열 슬라이스 담당 — 아래 값 옆 주석에 소속 계열만 남긴다).
    public enum Sp
    {
        NONE,
        WILD,
        BOMB,
        MAGNET,
        SKULL,
        DICE,
        COIN,
        KEY,
        FLAME,
        SEED,
        // ── 웹 파리티 P7-1 신규 51종(알파벳순 — 선언 순서가 확률/평가에 영향 없는 표식 enum이라
        //    Sym/SymInfo와 달리 원본 배열 순서를 지킬 필요가 없다) ──
        ALARM,          // ⏰알람 — 시간(P7-2/3)
        AUGCHANCE,      // 🖍형광펜 — 증강(P7-2/3, 심화 증강 오퍼 자체가 없어 §1-A #19 각주대로 미소비)
        AUGLEVEL,       // 📚복습책 — 증강(P7-2/3)
        BANDAGE,        // 🩹붕대 — 실버 instant(P7-2/3)
        BELL_ECHO,      // 🔔울림종 — 종(P7-2/3 §9.2 J3)
        BELL_FEST,      // 🎊축제종 — 종(P7-2/3 §9.2 J3)
        BELL_GOLD,      // 🔔황금종 — 종(P7-2/3 §9.2 J3)
        BELL_SMALL,     // 🔔작은종 — 종(P7-2/3 §9.2 J3)
        BELL_TICKET,    // 🎟종소리티켓 — 종/fuse(P7-2/3 §9.2 J3)
        BIG_BOOM,       // 💥대폭죽 — 잭팟태그 증폭(P7-2/3 §9.2 J3)
        BLACKCARD,      // 💳검은카드 — 저주/fuse(P7-2/3)
        CART,           // 🛒장바구니 — 상점(P7-2/3)
        CATALYST,       // 🧪촉매 — 변환(P7-2/3)
        CHEER,          // 📣환호 — 잭팟태그 증폭(P7-2/3 §9.2 J3)
        COUPON,         // 🎟쿠폰 — 상점(P7-2/3)
        CRYSTAL,        // 🔮수정구 — 골드 fuse(P7-2/3)
        CURSE_BLOOD,    // 🩸피방울 — 저주(P7-2/3)
        CURSE_BOOM,     // 🧨불안정폭탄 — 저주(P7-2/3)
        CURSE_CANDLE,   // 🕯검은초 — 저주(P7-2/3)
        CURSE_EYE,      // 🧿저주눈 — 저주(P7-2/3)
        DEVCD,          // 🔋배터리 — 장치(P7-2/3)
        ENERGYPACK,     // 🧃에너지팩 — 실버 instant(P7-2/3)
        EVOCORE,        // 🧬진화핵 — 프리즘 instant(P7-2/3)
        EXEMPT,         // 📋시험지 — 보스(P7-2/3)
        FAKECROWN,      // 👑가짜왕관 — 프리즘 instant(P7-2/3)
        FATEVORTEX,     // 🌀운명의소용돌이 — 프리즘 fuse(P7-2/3)
        GEAR,           // ⚙톱니바퀴 — 장치(P7-2/3)
        HOURGLASS,      // ⏳모래시계 — 시간(P7-2/3)
        JACKPOT_CROWN,  // 👑잭팟왕관 — 잭팟태그(P7-2/3 §9.2 J3)
        JACKPOT_TICKET, // 🎟잭팟티켓 — 리치/fuse(P7-2/3 §9.2 J3)
        JACKPOT_WAND,   // 🪄잭팟마법봉 — 잭팟태그(P7-2/3 §9.2 J3)
        KIT,            // 🧰정비키트 — 장치(P7-2/3)
        KNOT,           // 🪢매듭 — 실버 instant(P7-2/3)
        LUCKY7,         // 7️⃣럭키7 — 전설(P7-2/3)
        MIRROR,         // 🪞거울 — 변환(P7-2/3)
        PRISM_SYM,      // 🌈프리즘 — 전설(P7-2/3)
        PURIFY,         // 🧹정화도구 — 변환(P7-2/3)
        PUZZLE5,        // 🧩퍼즐 — 위치(P7-2/3)
        REACH_MARK,     // 🎯리치표식 — 리치(P7-2/3 §9.2 J3)
        RECEIPT,        // 🧾영수증 — 상점(P7-2/3)
        RETRY_REEL,     // 🔁재도전릴 — 리치(P7-2/3 §9.2 J3)
        SAFEPIN,        // 🧷안전핀노트 — 실버 fuse(P7-2/3)
        SEED_ANY,       // 🌱씨앗 — 성장(P7-2/3)
        SEED_HIGH,      // 🌿새싹 — 성장(P7-2/3)
        SETFRAG,        // 🧩세트조각 — 세트(P7-2/3)
        SHACKLE,        // ⛓족쇄 — 저주 상주(P7-2/3)
        SHIELD,         // 🛡방패 — 보스(P7-2/3)
        SLOT_SHARD,     // 🎰슬롯조각 — 잭팟태그(P7-2/3 §9.2 J3)
        TARGET,         // 🎯표적 — 위치(P7-2/3)
        TEMPWILD,       // 🧲임시와일드 — 골드 fuse(P7-2/3)
        WANDWILD,       // 🪄마법봉 — 변환/조작(P7-2/3)
    }

    // 심볼 종류 — 01_engine.md §2.2, Kotlin SYMS(SlotV2Engine.kt L113-129) 선언 순서 그대로인 원본
    // 14종(LegacyCount) + 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 data.js SYMS L45-130) 신규
    // 58종 = 총 72종. 원본 14종의 선언 순서는 §10.1 weighted()의 누적가중치 스캔 순서를 결정하므로
    // 절대 바꾸지 말 것(동일 총합이라도 절단 지점이 달라져 다른 심볼이 뽑힐 수 있음) — 신규 58종은
    // 전부 weight=0(휴면, 심화모드 주머니로만 등장)이라 Weighted()의 누적합에 0을 더할 뿐 어떤 기존
    // 심볼의 절단 지점도 옮기지 않는다(끝에 추가만 — 순서 자체도 원본 배열 뒤에 그대로 이어붙임).
    public enum Sym
    {
        Cherry,
        Book,
        Star,
        Gem,
        Coin,
        Skull,
        Flame,
        Magnet,
        Bomb,
        Crown,
        Key,
        Dice,
        Seed,
        Wild,
        // ── 웹 파리티 P7-1 신규 58종(웹 data.js SYMS 선언 순서 그대로 — Symbols.All 배열 순서와 1:1) ──
        CherryRipe,
        Tome,
        GemCut,
        CoinBag,
        SkullBlack,
        Ember,
        SeedBasic,
        Sprout,
        Catalyst,
        Purifier,
        MagicWand,
        MirrorSym,
        Target,
        Jigsaw,
        Alarm,
        Hourglass,
        Receipt,
        Coupon,
        Cart,
        Highlighter,
        ReviewBook,
        BatterySym,
        Gear,
        RepairKit,
        SetPiece,
        KeyGold,
        Shield,
        ExamPaper,
        Bloodrop,
        BlackCandleSym,
        UnstableBomb,
        CurseEye,
        Lucky7,
        PrismSym,
        Bandage,
        Knot,
        Safepin,
        Energypack,
        Crystal,
        TempWild,
        FakeCrownSym,
        FateVortex,
        EvoCore,
        BlackCard,
        Shackle,
        SmallBell,
        EchoBell,
        GoldenBell,
        FestivalBell,
        BellTicket,
        ReachMark,
        RetryReel,
        JackpotTicket,
        SlotShard,
        JackpotWand,
        Cheer,
        BigBoom,
        JackpotCrown,
    }

    // 심볼 데이터 — Kotlin data class Sym(SlotV2Engine.kt L100-106) 전체 필드 전사.
    //
    // [설계 계약 결손 — 보고 대상] ENGINE_PORT_DESIGN.md 공유 타입 계약의 SymInfo는
    // "{ Sym sym; string emoji; long exp; long score; bool dormant; }" 4+1 필드만 명시한다.
    // 하지만 (a) 콘텐츠 완전 전사 원칙(원칙 3: "콘텐츠 테이블은 C# 코드로 전사"), (b) S1 골든 테스트
    // 요구사항 "Symbols 14종 값", (c) 머신 최종 가중치표 계산(§3, base weight 필요) 모두 Kotlin 원본의
    // id/name/coin/weight/special/rare/tags 필드 없이는 불가능하다. 따라서 이 필드들을 전부 추가해
    // Kotlin data class와 1:1로 확장했다 — 계약에 명시된 5개 필드는 이름·타입 그대로 보존.
    public sealed class SymInfo
    {
        public Sym sym;
        public string id;
        public string emoji;
        public string name;
        public long exp;
        public long score;
        public long coin;
        public int weight; // 기본 가중치 (Kotlin Int weight) — weighted()에서 double로 승격되어 사용됨
        public Sp special;
        public bool rare;
        public bool dormant; // weight == 0 (key/dice/seed/wild 휴면 4종)
        public string[] tags;
    }

    public static class Symbols
    {
        // 웹 파리티 P7-1 — 전체 72종(All.Length와 항상 일치, Tests_Core.cs가 골든으로 고정).
        public const int Count = 72;
        // 원본(kotlin-reference 이전부터 있던) 14종 — 일반모드에서 weight>0으로 실제 뽑히는 심볼
        // 집합은 이 14종(과 machine/perk weightAdd로 주입되는 휴면 4종)뿐이어야 한다는 불변식의 기준
        // 값. Tests_Core.cs가 "All의 앞 LegacyCount개 필드가 원본과 완전히 동일"·"All[LegacyCount..]는
        // 전부 weight==0"을 골든으로 고정해 일반모드 회귀를 막는다(WEB_PARITY_DESIGN.md §1-A #19 작업
        // 지시 "기존 14종의 필드 불변… weight>0 심볼 집합이 바뀌면 안 됨을 테스트로 고정").
        public const int LegacyCount = 14;

        // 01_engine.md §2.2 표 / SlotV2Engine.kt L113-129 그대로 전사(앞 14종) + 웹 파리티 P7-1
        // (WEB_PARITY_DESIGN.md §1-A #19, 웹 data.js SYMS L45-130) 신규 58종을 뒤에 그대로 이어붙임 —
        // 신규분은 전부 weight=0(dormant=true, 심화모드 주머니로만 등장)이라 앞 14종의 Weighted() 누적
        // 스캔 결과에 전혀 영향을 주지 않는다(Symbols.cs 헤더 Sym enum 각주 참조).
        public static readonly SymInfo[] All =
        {
            new SymInfo
            {
                sym = Sym.Cherry, id = "cherry", emoji = "🍒", name = "체리",
                exp = 3, score = 0, coin = 0, weight = 25, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "생명" },
            },
            new SymInfo
            {
                sym = Sym.Book, id = "book", emoji = "📘", name = "책",
                exp = 6, score = 0, coin = 0, weight = 18, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "학습" },
            },
            new SymInfo
            {
                sym = Sym.Star, id = "star", emoji = "⭐", name = "별",
                exp = 8, score = 0, coin = 0, weight = 13, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "콤보" },
            },
            new SymInfo
            {
                sym = Sym.Gem, id = "gem", emoji = "💎", name = "보석",
                exp = 1, score = 15, coin = 0, weight = 12, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "점수" },
            },
            new SymInfo
            {
                sym = Sym.Coin, id = "coin", emoji = "🪙", name = "코인",
                exp = 0, score = 0, coin = 1, weight = 10, special = Sp.COIN, rare = false, dormant = false,
                tags = new[] { "코인" },
            },
            new SymInfo
            {
                sym = Sym.Skull, id = "skull", emoji = "☠", name = "해골",
                exp = 0, score = 0, coin = 0, weight = 10, special = Sp.SKULL, rare = false, dormant = false,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.Flame, id = "flame", emoji = "🔥", name = "불꽃",
                exp = 0, score = 0, coin = 0, weight = 5, special = Sp.FLAME, rare = false, dormant = false,
                tags = new[] { "배율" },
            },
            new SymInfo
            {
                sym = Sym.Magnet, id = "magnet", emoji = "🧲", name = "자석",
                exp = 2, score = 0, coin = 0, weight = 4, special = Sp.MAGNET, rare = false, dormant = false,
                tags = new[] { "조작" },
            },
            new SymInfo
            {
                sym = Sym.Bomb, id = "bomb", emoji = "💣", name = "폭탄",
                exp = 5, score = 0, coin = 0, weight = 2, special = Sp.BOMB, rare = false, dormant = false,
                tags = new[] { "폭발" },
            },
            new SymInfo
            {
                sym = Sym.Crown, id = "crown", emoji = "👑", name = "왕관",
                exp = 20, score = 50, coin = 0, weight = 1, special = Sp.NONE, rare = true, dormant = false,
                tags = new[] { "왕관", "희귀" },
            },
            // ── 휴면(특수, weight=0) — 머신 weightAdd/perk wadd()로만 풀에 주입됨 ──
            new SymInfo
            {
                sym = Sym.Key, id = "key", emoji = "🗝", name = "열쇠",
                exp = 6, score = 0, coin = 0, weight = 0, special = Sp.KEY, rare = false, dormant = true,
                tags = new[] { "열쇠" },
            },
            new SymInfo
            {
                sym = Sym.Dice, id = "dice", emoji = "🎲", name = "주사위",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.DICE, rare = false, dormant = true,
                tags = new[] { "운" },
            },
            new SymInfo
            {
                sym = Sym.Seed, id = "seed", emoji = "🌱", name = "씨앗",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SEED, rare = false, dormant = true,
                tags = new[] { "생명", "성장" },
            },
            new SymInfo
            {
                sym = Sym.Wild, id = "wild", emoji = "🌀", name = "와일드",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.WILD, rare = true, dormant = true,
                tags = new[] { "희귀", "조작" },
            },
            // ── 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 data.js SYMS L45-130) 신규 58종 —
            //    전부 weight=0/dormant=true(심화모드 주머니로만 등장, 일반모드 확률 무영향). 웹 원본
            //    선언 순서 그대로 전사(POUCH_SYMBOLS/DEFAULT_UNLOCKED_SYMS 등 다른 웹 테이블과 대조가
            //    쉽도록). ──
            new SymInfo
            {
                sym = Sym.CherryRipe, id = "cherry_ripe", emoji = "🍑", name = "숙성체리",
                exp = 6, score = 0, coin = 0, weight = 0, special = Sp.NONE, rare = false, dormant = true,
                tags = new[] { "생명" },
            },
            new SymInfo
            {
                sym = Sym.Tome, id = "tome", emoji = "📖", name = "족보",
                exp = 12, score = 0, coin = 0, weight = 0, special = Sp.NONE, rare = false, dormant = true,
                tags = new[] { "학습" },
            },
            new SymInfo
            {
                sym = Sym.GemCut, id = "gem_cut", emoji = "💠", name = "연마보석",
                exp = 2, score = 32, coin = 0, weight = 0, special = Sp.NONE, rare = false, dormant = true,
                tags = new[] { "점수" },
            },
            new SymInfo
            {
                sym = Sym.CoinBag, id = "coin_bag", emoji = "💰", name = "돈주머니",
                exp = 0, score = 0, coin = 3, weight = 0, special = Sp.COIN, rare = false, dormant = true,
                tags = new[] { "코인" },
            },
            new SymInfo
            {
                sym = Sym.SkullBlack, id = "skull_black", emoji = "💀", name = "검은해골",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SKULL, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.Ember, id = "ember", emoji = "🎇", name = "불씨",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.FLAME, rare = false, dormant = true,
                tags = new[] { "배율" },
            },
            new SymInfo
            {
                sym = Sym.SeedBasic, id = "seed_basic", emoji = "🌱", name = "씨앗",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SEED_ANY, rare = false, dormant = true,
                tags = new[] { "생명", "성장" },
            },
            new SymInfo
            {
                sym = Sym.Sprout, id = "sprout", emoji = "🌿", name = "새싹",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SEED_HIGH, rare = false, dormant = true,
                tags = new[] { "생명", "성장" },
            },
            new SymInfo
            {
                sym = Sym.Catalyst, id = "catalyst", emoji = "🧪", name = "촉매",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CATALYST, rare = false, dormant = true,
                tags = new[] { "변환" },
            },
            new SymInfo
            {
                sym = Sym.Purifier, id = "purifier", emoji = "🧹", name = "정화도구",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.PURIFY, rare = false, dormant = true,
                tags = new[] { "변환", "정화" },
            },
            new SymInfo
            {
                sym = Sym.MagicWand, id = "magic_wand", emoji = "🪄", name = "마법봉",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.WANDWILD, rare = true, dormant = true,
                tags = new[] { "변환", "조작", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.MirrorSym, id = "mirror_sym", emoji = "🪞", name = "거울",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.MIRROR, rare = false, dormant = true,
                tags = new[] { "변환" },
            },
            new SymInfo
            {
                sym = Sym.Target, id = "target", emoji = "🎯", name = "표적",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.TARGET, rare = false, dormant = true,
                tags = new[] { "위치" },
            },
            new SymInfo
            {
                sym = Sym.Jigsaw, id = "jigsaw", emoji = "🧩", name = "퍼즐",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.PUZZLE5, rare = false, dormant = true,
                tags = new[] { "위치" },
            },
            new SymInfo
            {
                sym = Sym.Alarm, id = "alarm", emoji = "⏰", name = "알람",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.ALARM, rare = false, dormant = true,
                tags = new[] { "시간" },
            },
            new SymInfo
            {
                sym = Sym.Hourglass, id = "hourglass", emoji = "⏳", name = "모래시계",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.HOURGLASS, rare = true, dormant = true,
                tags = new[] { "시간", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.Receipt, id = "receipt", emoji = "🧾", name = "영수증",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.RECEIPT, rare = false, dormant = true,
                tags = new[] { "상점" },
            },
            new SymInfo
            {
                sym = Sym.Coupon, id = "coupon", emoji = "🎟", name = "쿠폰",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.COUPON, rare = true, dormant = true,
                tags = new[] { "상점", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.Cart, id = "cart", emoji = "🛒", name = "장바구니",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CART, rare = true, dormant = true,
                tags = new[] { "상점", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.Highlighter, id = "highlighter", emoji = "🖍", name = "형광펜",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.AUGCHANCE, rare = false, dormant = true,
                tags = new[] { "증강" },
            },
            new SymInfo
            {
                sym = Sym.ReviewBook, id = "review_book", emoji = "📚", name = "복습책",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.AUGLEVEL, rare = true, dormant = true,
                tags = new[] { "증강", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.BatterySym, id = "battery_sym", emoji = "🔋", name = "배터리",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.DEVCD, rare = false, dormant = true,
                tags = new[] { "장치" },
            },
            new SymInfo
            {
                sym = Sym.Gear, id = "gear", emoji = "⚙", name = "톱니바퀴",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.GEAR, rare = false, dormant = true,
                tags = new[] { "장치" },
            },
            new SymInfo
            {
                sym = Sym.RepairKit, id = "repair_kit", emoji = "🧰", name = "정비키트",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.KIT, rare = true, dormant = true,
                tags = new[] { "장치", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.SetPiece, id = "set_piece", emoji = "🧩", name = "세트조각",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SETFRAG, rare = false, dormant = true,
                tags = new[] { "세트" },
            },
            new SymInfo
            {
                sym = Sym.KeyGold, id = "key_gold", emoji = "🗝", name = "열쇠",
                exp = 6, score = 0, coin = 0, weight = 0, special = Sp.KEY, rare = false, dormant = true,
                tags = new[] { "세트", "열쇠" },
            },
            new SymInfo
            {
                sym = Sym.Shield, id = "shield", emoji = "🛡", name = "방패",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SHIELD, rare = false, dormant = true,
                tags = new[] { "보스" },
            },
            new SymInfo
            {
                sym = Sym.ExamPaper, id = "exam_paper", emoji = "📋", name = "시험지",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.EXEMPT, rare = false, dormant = true,
                tags = new[] { "보스" },
            },
            new SymInfo
            {
                sym = Sym.Bloodrop, id = "bloodrop", emoji = "🩸", name = "피방울",
                exp = 8, score = 0, coin = 0, weight = 0, special = Sp.CURSE_BLOOD, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.BlackCandleSym, id = "black_candle_sym", emoji = "🕯", name = "검은초",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CURSE_CANDLE, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.UnstableBomb, id = "unstable_bomb", emoji = "🧨", name = "불안정폭탄",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CURSE_BOOM, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.CurseEye, id = "curse_eye", emoji = "🧿", name = "저주눈",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CURSE_EYE, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.Lucky7, id = "lucky7", emoji = "7️⃣", name = "럭키7",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.LUCKY7, rare = true, dormant = true,
                tags = new[] { "전설", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.PrismSym, id = "prism_sym", emoji = "🌈", name = "프리즘",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.PRISM_SYM, rare = true, dormant = true,
                tags = new[] { "전설", "희귀" },
            },
            new SymInfo
            {
                sym = Sym.Bandage, id = "bandage", emoji = "🩹", name = "붕대",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BANDAGE, rare = false, dormant = true,
                tags = new[] { "보호" },
            },
            new SymInfo
            {
                sym = Sym.Knot, id = "knot", emoji = "🪢", name = "매듭",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.KNOT, rare = false, dormant = true,
                tags = new[] { "위치" },
            },
            new SymInfo
            {
                sym = Sym.Safepin, id = "safepin", emoji = "🧷", name = "안전핀노트",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SAFEPIN, rare = false, dormant = true,
                tags = new[] { "증강" },
            },
            new SymInfo
            {
                sym = Sym.Energypack, id = "energypack", emoji = "🧃", name = "에너지팩",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.ENERGYPACK, rare = false, dormant = true,
                tags = new[] { "배율" },
            },
            new SymInfo
            {
                sym = Sym.Crystal, id = "crystal", emoji = "🔮", name = "수정구",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CRYSTAL, rare = false, dormant = true,
                tags = new[] { "보상" },
            },
            new SymInfo
            {
                sym = Sym.TempWild, id = "temp_wild", emoji = "🧲", name = "임시와일드",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.TEMPWILD, rare = false, dormant = true,
                tags = new[] { "조작" },
            },
            new SymInfo
            {
                sym = Sym.FakeCrownSym, id = "fake_crown_sym", emoji = "👑", name = "가짜왕관",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.FAKECROWN, rare = true, dormant = true,
                tags = new[] { "희귀", "왕관" },
            },
            new SymInfo
            {
                sym = Sym.FateVortex, id = "fate_vortex", emoji = "🌀", name = "운명의소용돌이",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.FATEVORTEX, rare = true, dormant = true,
                tags = new[] { "희귀", "운" },
            },
            new SymInfo
            {
                sym = Sym.EvoCore, id = "evo_core", emoji = "🧬", name = "진화핵",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.EVOCORE, rare = true, dormant = true,
                tags = new[] { "희귀", "변환" },
            },
            new SymInfo
            {
                sym = Sym.BlackCard, id = "black_card", emoji = "💳", name = "검은카드",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BLACKCARD, rare = false, dormant = true,
                tags = new[] { "저주", "상점" },
            },
            new SymInfo
            {
                sym = Sym.Shackle, id = "shackle", emoji = "⛓", name = "족쇄",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SHACKLE, rare = false, dormant = true,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.SmallBell, id = "small_bell", emoji = "🔔", name = "작은종",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BELL_SMALL, rare = false, dormant = true,
                tags = new[] { "종" },
            },
            new SymInfo
            {
                sym = Sym.EchoBell, id = "echo_bell", emoji = "🔔", name = "울림종",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BELL_ECHO, rare = false, dormant = true,
                tags = new[] { "종" },
            },
            new SymInfo
            {
                sym = Sym.GoldenBell, id = "golden_bell", emoji = "🔔", name = "황금종",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BELL_GOLD, rare = false, dormant = true,
                tags = new[] { "종" },
            },
            new SymInfo
            {
                sym = Sym.FestivalBell, id = "festival_bell", emoji = "🎊", name = "축제종",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BELL_FEST, rare = false, dormant = true,
                tags = new[] { "종" },
            },
            new SymInfo
            {
                sym = Sym.BellTicket, id = "bell_ticket", emoji = "🎟", name = "종소리티켓",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BELL_TICKET, rare = false, dormant = true,
                tags = new[] { "종" },
            },
            new SymInfo
            {
                sym = Sym.ReachMark, id = "reach_mark", emoji = "🎯", name = "리치표식",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.REACH_MARK, rare = false, dormant = true,
                tags = new[] { "리치" },
            },
            new SymInfo
            {
                sym = Sym.RetryReel, id = "retry_reel", emoji = "🔁", name = "재도전릴",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.RETRY_REEL, rare = false, dormant = true,
                tags = new[] { "리치" },
            },
            new SymInfo
            {
                sym = Sym.JackpotTicket, id = "jackpot_ticket", emoji = "🎟", name = "잭팟티켓",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.JACKPOT_TICKET, rare = false, dormant = true,
                tags = new[] { "리치" },
            },
            new SymInfo
            {
                sym = Sym.SlotShard, id = "slot_shard", emoji = "🎰", name = "슬롯조각",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SLOT_SHARD, rare = false, dormant = true,
                tags = new[] { "잭팟" },
            },
            new SymInfo
            {
                sym = Sym.JackpotWand, id = "jackpot_wand", emoji = "🪄", name = "잭팟마법봉",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.JACKPOT_WAND, rare = false, dormant = true,
                tags = new[] { "잭팟" },
            },
            new SymInfo
            {
                sym = Sym.Cheer, id = "cheer", emoji = "📣", name = "환호",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.CHEER, rare = false, dormant = true,
                tags = new[] { "잭팟" },
            },
            new SymInfo
            {
                sym = Sym.BigBoom, id = "big_boom", emoji = "💥", name = "대폭죽",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.BIG_BOOM, rare = false, dormant = true,
                tags = new[] { "잭팟" },
            },
            new SymInfo
            {
                sym = Sym.JackpotCrown, id = "jackpot_crown", emoji = "👑", name = "잭팟왕관",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.JACKPOT_CROWN, rare = true, dormant = true,
                tags = new[] { "잭팟", "희귀" },
            },
        };

        // VALUE_IDS — 세트/잭팟/인접/양끝 판정 대상 (SlotV2Engine.kt L131, 원본 선언 순서 유지).
        public static readonly Sym[] ValueIds = { Sym.Cherry, Sym.Star, Sym.Book, Sym.Gem, Sym.Crown };

        // SET_EXP / SET_SCORE — 같은 심볼 N개(와일드 포함) 세트 보너스, index=개수(0~5) (SlotV2Engine.kt L133-135).
        public static readonly int[] SetExp = { 0, 0, 8, 18, 42, 100 };
        public static readonly int[] SetScore = { 0, 0, 3, 9, 24, 70 };

        public static SymInfo BySym(Sym sym)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].sym == sym) return All[i];
            throw new ArgumentOutOfRangeException(nameof(sym), sym, "Unknown Sym value.");
        }

        public static SymInfo ById(string id) => Array.Find(All, s => s.id == id);
    }
}
