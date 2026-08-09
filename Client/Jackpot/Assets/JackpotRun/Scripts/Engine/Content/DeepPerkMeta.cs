using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 심화 노드 풀) — 일반 증강/유물의
    // 심화(deepMode) 관련성 메타. 웹 data.js AUGMENTS/RELICS 각 항목의 `deep`/`dSym`/`dDesc` 3필드
    // (§229-235 헤더 주석: "deep: 1=심화 전부 유효/2=부분 유효(dDesc 필수)/없음=심화 제외, dSym: 심볼직결
    // 퍽의 참조 계열, dDesc: 심화 전용 설명")를 그대로 옮긴다.
    //
    // Perk 클래스(ContentTypes.cs) 자체에 필드를 추가하지 않고 POUCH_CAT/JACKPOT_TAG 선례(Content/
    // Pouch.cs 헤더 주석 "별도 맵" 관례)를 그대로 따라 id→메타 별도 테이블로 둔다 — Perk는 이 3개 얕은
    // 어셈블리(Content/Run/Profile) 전역에서 공유되는 핵심 타입이라 필드를 늘리면 매 슬라이스 초기화
    // 부담이 커지고, 이 메타는 오직 심화 SYMAUG/SYMREL 노드 오퍼(deepCompatPool)만 읽는 좁은 소비처라
    // 별도 맵이 더 안전하다.
    //
    // 데이터는 python 스크립트로 data.js를 직접 정규식 파싱해 생성한 뒤(AUGMENTS 89종·RELICS 73종 —
    // 육안 대조로 카운트 일치 확인) id 순서를 Perks.cs Augments/Relics 선언 순서와 대조했다(교차검증
    // 스크립트 — 89/73 전량 id 집합 일치 확인, 순서는 딕셔셔너리 조회라 무관). "deep 없음"(0) 2종
    // (seed_garden/fate_bell)은 웹 원문에도 deep 필드 자체가 없어 심화 오퍼에서 영구 제외된다(isDeepCompat
    // "!p.deep → false" 그대로).
    public static class DeepPerkMeta
    {
        private static readonly (string id, int deep, string dSym, string dDesc)[] AugMeta =
        {
            ("study", 1, null, null),
            ("preview", 1, null, null),
            ("review", 1, null, null),
            ("diligence", 1, null, null),
            ("cherry_up", 1, "cherry", null),
            ("book_up", 1, "book", null),
            ("star_up", 1, "star", null),
            ("gem_polish", 1, "gem", null),
            ("coin_luck", 1, null, null),
            ("set_sense", 1, null, null),
            ("discount", 1, null, null),
            ("thrifty", 1, null, null),
            ("item_bag", 1, null, null),
            ("vip", 1, null, null),
            ("refund", 1, null, null),
            ("lucky", 1, null, "희귀 심볼(🌀와일드·👑왕관) 주머니 드로우 확률 +20%"),
            ("study_tag", 1, "tag:학습", null),
            ("cherry_farm", 2, "cherry", "🍒체리 EXP +4"),
            ("library", 1, "book", null),
            ("gem_invest", 1, "gem", null),
            ("skull_study", 1, "skull", null),
            ("center", 1, null, null),
            ("twins", 1, null, null),
            ("chain", 1, null, null),
            ("crown_seek", 2, "crown", "👑왕관 점수 +30"),
            ("greed", 1, null, null),
            ("insurance", 1, null, null),
            ("overdrive", 1, null, null),
            ("short_day", 1, null, null),
            ("wild_world", 1, null, "🌀와일드 주머니 보유 시 드로우 확률 대폭↑(세트 합류)"),
            ("seed_garden", 0, null, null),
            ("jackpot", 2, "crown", "👑왕관 점수 +50"),
            ("all_in", 1, null, null),
            ("cram", 1, null, null),
            ("high_roller", 1, null, null),
            ("all_or_nothing", 1, null, null),
            ("focus_fire", 1, null, null),
            ("symmetry", 1, null, null),
            ("crammer_tag", 2, "tag:학습", "학습태그 1개당 EXP +7"),
            ("gamblers_dice", 2, null, "EXP +15% — 🎲주사위 등장↑은 심화 주머니 미적용"),
            ("key_master", 2, null, "코인 +25% — 🗝열쇠 등장↑은 심화 주머니 미적용"),
            ("glass_cannon", 1, null, null),
            ("rich_richer", 1, null, null),
            ("endgame_rush", 1, null, null),
            ("deep_read", 1, "tag:학습", null),
            ("morning", 1, null, null),
            ("evening", 1, null, null),
            ("note_take", 1, null, null),
            ("star_up2", 1, "star", null),
            ("magnet_up", 1, "magnet", null),
            ("gem_buff", 1, "gem", null),
            ("combo_note", 1, null, null),
            ("polymath", 1, null, null),
            ("necromancer", 1, "skull", null),
            ("bullseye", 1, null, null),
            ("mirror", 1, null, null),
            ("domino", 1, null, null),
            ("honor_student", 1, "tag:학습", null),
            ("lapidary", 1, "gem", null),
            ("royal_decree", 2, "crown", "👑왕관 점수 +20"),
            ("supernova", 1, null, null),
            ("joker", 1, null, "🌀와일드 주머니 보유 시 드로우 확률 대폭↑"),
            ("great_harvest", 2, "cherry", "🍒체리 EXP +3"),
            ("mega_jackpot", 2, "crown", "👑왕관 점수 +40"),
            ("time_warp", 1, null, null),
            ("red_safetynet", 1, "cherry", null),
            ("polish_work", 1, "gem", null),
            ("greed_calc", 1, null, null),
            ("overheat_formula", 1, null, null),
            ("early_prep", 1, null, null),
            ("growth_log", 1, null, null),
            ("early_adapt", 1, null, null),
            ("snowball", 1, null, null),
            ("fortune_check", 1, null, "첫 스핀 희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +20%"),
            ("luck_accum", 1, null, "불운 3+ 스핀 시 다음 스핀 희귀 심볼 드로우 확률 +30%"),
            ("fate_burst", 1, null, "희귀(👑/🌀) 2개+ 스핀 EXP +80%·점수 +50% (보스전 70%) — 주머니에 👑/🌀 필요"),
            ("late_focus", 1, null, null),
            ("cliff_focus", 1, null, null),
            ("fate_bell", 0, null, null),
            ("pair_match", 1, null, null),
            ("puzzle_sense", 1, null, null),
            ("perfect_shape", 1, null, "양끝 같고 가운데 같은계열 EXP +120% (와일드충족 70%) — 🌀와일드 보유 시 유리"),
            ("skull_watch", 1, "skull", null),
            ("sacrifice", 1, null, "저주당 EXP +6%(심화 저주 경로 유효)"),
            ("black_diploma", 1, null, "저주5+ EXP +60%·점수 +30%·스핀 -1(심화 저주 경로 유효)"),
            ("crown_burst", 2, "crown", "👑왕관 점수 +100"),
            ("curse_grad", 1, null, "저주당 점수 +15%(심화 저주 경로 유효)"),
            ("extreme_overload", 1, null, null),
            ("abyss_lore", 1, null, null),
        };

        private static readonly (string id, int deep, string dSym, string dDesc)[] RelMeta =
        {
            ("old_book", 1, "book", null),
            ("cherry_candy", 1, "cherry", null),
            ("rusty_coin", 1, null, null),
            ("pencil", 1, null, null),
            ("coffee", 1, null, null),
            ("magnifier", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%"),
            ("star_sticker", 1, "star", null),
            ("black_candle", 1, "skull", null),
            ("gem_cert", 1, "gem", null),
            ("clover", 1, null, null),
            ("set_charm", 1, null, null),
            ("wide_lens", 1, null, null),
            ("eraser", 1, "book", null),
            ("ruler", 1, null, null),
            ("desk_lamp", 1, null, null),
            ("cherry_jam", 1, "cherry", null),
            ("bookmark", 1, "tag:학습", null),
            ("coin_pouch", 1, null, null),
            ("mini_scope", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%"),
            ("gem_dust", 1, "gem", null),
            ("magnet_chip", 1, "magnet", null),
            ("star_chart", 1, "star", null),
            ("paperclip", 1, null, null),
            ("small_candle", 1, "skull", null),
            ("thick_tome", 1, "book", null),
            ("crystal_ball", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +30%"),
            ("skull_idol", 1, "skull", null),
            ("gem_tiara", 1, "gem", null),
            ("focus_ring", 1, null, null),
            ("silver_mirror", 1, null, null),
            ("iron_chain", 1, null, null),
            ("diploma_relic", 1, "tag:학습", null),
            ("four_clover", 1, null, null),
            ("combo_trophy", 1, null, null),
            ("crown_jewel", 1, "crown", null),
            ("piggy_bank", 1, null, null),
            ("spare_token", 1, null, null),
            ("hourglass_r", 1, null, null),
            ("battery", 1, null, null),
            ("charm_relic", 1, null, null),
            ("cherry_press", 1, "cherry", null),
            ("cherry_can", 1, "cherry", null),
            ("auto_pen", 1, "book", null),
            ("library_card", 1, "book", null),
            ("greed_goblet", 1, null, null),
            ("ominous_skull", 1, "skull", null),
            ("black_report", 1, "skull", null),
            ("bloody_coupon", 1, null, null),
            ("crown_stand", 1, "crown", null),
            ("broken_crown", 1, "crown", null),
            ("kings_ledger", 2, "crown", "👑왕관 점수 +20"),
            ("flame_canister", 1, null, null),
            ("hot_handle", 1, null, null),
            ("fate_handle", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +25%"),
            ("gamblers_eye", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +20%"),
            ("old_wallet", 1, null, null),
            ("crumpled_coupon", 1, null, null),
            ("cursed_wallet", 1, null, null),
            ("practice_pad", 1, "book", null),
            ("calculator", 1, "gem", null),
            ("lucky_eraser", 1, null, "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%"),
            ("prism_diploma", 1, null, null),
            ("golden_ratio", 1, null, null),
            ("starlight_crown", 2, "crown", "👑왕관 점수 +60·코인 +30%"),
            ("endless_recess", 1, null, null),
            ("fortunes_wheel", 2, null, "희귀(👑/🌀) 2개+ 스핀 EXP +60%·점수 +30% — 주머니에 👑/🌀 필요"),
            ("set_resonator", 1, null, null),
            ("reapers_pact", 1, "skull", null),
            ("phoenix_thesis", 1, null, null),
            ("crown_monolith", 2, "crown", "👑왕관 점수 +80"),
            ("black_grad_photo", 1, null, "저주당 점수 +12%(심화 저주 경로 유효)"),
            ("last_roll", 1, null, null),
            ("nameless_cup", 1, null, null),
        };

        private sealed class Meta
        {
            public int Deep;
            public string DSym;
            public string DDesc;
        }

        private static readonly Dictionary<string, Meta> ById = Build();

        private static Dictionary<string, Meta> Build()
        {
            var d = new Dictionary<string, Meta>();
            foreach (var (id, deep, dSym, dDesc) in AugMeta) d[id] = new Meta { Deep = deep, DSym = dSym, DDesc = dDesc };
            foreach (var (id, deep, dSym, dDesc) in RelMeta) d[id] = new Meta { Deep = deep, DSym = dSym, DDesc = dDesc };
            return d;
        }

        public static int DeepOf(string id) => (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var m)) ? m.Deep : 0;
        public static string DSymOf(string id) => (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var m)) ? m.DSym : null;

        // 심화 전용 설명 — deep:2(부분 유효) 항목만 갖는다. 없으면 일반 desc를 그대로 쓴다(웹 UI의
        // "dDesc || d" 관례 — 이 슬라이스는 데이터·판정만 제공, 실제 문구 조합은 P7-4 UI 몫).
        public static string DDescOr(string id, string fallback)
        {
            if (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var m) && !string.IsNullOrEmpty(m.DDesc)) return m.DDesc;
            return fallback;
        }

        // ── 웹 engine.js isDeepCompat/deepCompatPool 그대로 — 참조 계열 보유수 게이트(REL_MIN, 왕관은
        // REL_MIN_BY_SYM=2) 이상일 때만 "주머니 빌드에 관련 있다"고 판정한다. deep 미설정(0)은 항상 false.
        public static bool IsDeepCompat(Perk p, IReadOnlyDictionary<string, int> pouch)
        {
            if (p == null) return false;
            int deep = DeepOf(p.id);
            if (deep <= 0) return false;
            string dSym = DSymOf(p.id);
            if (string.IsNullOrEmpty(dSym)) return true;
            int min = Pouch.RelMinBySym.TryGetValue(dSym, out var m) ? m : Pouch.RelMin;
            return Pouch.FamilyCount(pouch, dSym) >= min;
        }

        public static List<Perk> DeepCompatPool(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, int> pouch)
        {
            if (pool == null) return new List<Perk>();
            return pool.Where(p => IsDeepCompat(p, pouch)).ToList();
        }
    }
}
