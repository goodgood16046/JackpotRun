using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P3.5(WEB_PARITY_DESIGN.md §2-(T) 후속① "PERK_FAMILY 랭크 게이팅") — 퍽 패밀리(비슷한
    // 효과 계열) 데이터. 웹 data.js:345-375(AUG_FAMILY 51종) + data.js:603-621(REL_FAMILY 45종) =
    // PERK_FAMILY(data.js:624 `{ ...AUG_FAMILY, ...REL_FAMILY }`) 96종을 손전사(1:1, id·패밀리키·
    // 랭크 전부 원문 그대로). 값 = (패밀리키, 랭크) — 같은 패밀리는 "약→강" 순서로 랭크가 오르고,
    // Shop.PickPerksByTier가 "보유한 같은 패밀리 개수+1" 랭크만 후보로 세운다(§2-(P)의 unlockLevel
    // 게이트와는 다른 축 — 이건 "오퍼 후보 필터", unlockLevel은 "해금 여부"). 미등록 증강/유물은
    // 웹 engine.js:15 `famOf = (p) => PERK_FAMILY[p.id] || [p.id, 1]`와 동일하게 "자기 id가 곧 고유
    // 패밀리, 랭크1(항상 후보)"로 취급한다 — PerkFamily.FamOf가 그 폴백을 담당.
    public static class PerkFamily
    {
        public static readonly IReadOnlyDictionary<string, (string Fam, int Rank)> Map =
            new Dictionary<string, (string Fam, int Rank)>
            {
                // ══════════════════════════════════════════════════════════════
                // 증강 51종 (data.js:345-375 AUG_FAMILY)
                // ══════════════════════════════════════════════════════════════
                // 첫/마지막 스핀 EXP% (SILVER)
                ["preview"] = ("first_spin", 1), ["morning"] = ("first_spin", 2),
                ["review"] = ("last_spin", 1), ["evening"] = ("last_spin", 2),
                // 스핀마다 EXP (SILVER)
                ["diligence"] = ("per_spin", 1), ["note_take"] = ("per_spin", 2),
                // 심볼 강화 (SILVER)
                ["cherry_up"] = ("cherry_s", 1), ["red_safetynet"] = ("cherry_s", 1),
                ["star_up"] = ("star_s", 1), ["star_up2"] = ("star_s", 2),
                ["gem_polish"] = ("gem_s", 1), ["gem_buff"] = ("gem_s", 2),
                ["combo_note"] = ("setbonus_s", 1), ["set_sense"] = ("setbonus_s", 2),
                ["deep_read"] = ("studytag_s", 1), ["study_tag"] = ("studytag_s", 2),
                // 모든 EXP% (GOLD)
                ["overheat_formula"] = ("exp_g", 1), ["greed_calc"] = ("exp_g", 2),
                ["polymath"] = ("exp_g", 3), ["greed"] = ("exp_g", 4),
                // 심볼/콤보 강화 (GOLD)
                ["gem_invest"] = ("gem_g", 1), ["polish_work"] = ("gem_g", 1), ["lapidary"] = ("gem_g", 2),
                ["skull_study"] = ("skull_g", 1), ["necromancer"] = ("skull_g", 2),
                ["bullseye"] = ("center_g", 1), ["center"] = ("center_g", 2), ["focus_fire"] = ("center_g", 3),
                ["mirror"] = ("ends_g", 1), ["twins"] = ("ends_g", 2), ["symmetry"] = ("ends_g", 3),
                ["domino"] = ("chain_g", 1), ["chain"] = ("chain_g", 2),
                ["honor_student"] = ("studytag_g", 1), ["crammer_tag"] = ("studytag_g", 2),
                ["royal_decree"] = ("crown_g", 1), ["crown_seek"] = ("crown_g", 2),
                // 모든 EXP% / 특수 (PRISM)
                ["overdrive"] = ("exp_p", 1), ["supernova"] = ("exp_p", 2), ["extreme_overload"] = ("exp_p", 3),
                ["wild_world"] = ("wild_p", 1), ["joker"] = ("wild_p", 2),
                ["seed_garden"] = ("seed_p", 1), ["great_harvest"] = ("seed_p", 2),
                ["mega_jackpot"] = ("jackpot_p", 1), ["jackpot"] = ("jackpot_p", 2), ["crown_burst"] = ("jackpot_p", 3),
                // 후반 저주 프리즘 계열 / 상점 할인 계열
                ["black_diploma"] = ("curse_p", 1), ["curse_grad"] = ("curse_p", 2),
                ["discount"] = ("disc_s", 1), ["thrifty"] = ("disc_s", 2),

                // ══════════════════════════════════════════════════════════════
                // 유물 45종 (data.js:603-621 REL_FAMILY)
                // ══════════════════════════════════════════════════════════════
                // SILVER
                ["eraser"] = ("rel_book_s", 1), ["auto_pen"] = ("rel_book_s", 1),
                ["practice_pad"] = ("rel_book_s", 1), ["old_book"] = ("rel_book_s", 2),
                ["cherry_candy"] = ("rel_cherry_s", 1), ["cherry_press"] = ("rel_cherry_s", 1),
                ["cherry_jam"] = ("rel_cherry_s", 2), ["cherry_can"] = ("rel_cherry_s", 2),
                ["rusty_coin"] = ("rel_coin_s", 1), ["coin_pouch"] = ("rel_coin_s", 1),
                ["old_wallet"] = ("rel_coin_s", 1), ["crumpled_coupon"] = ("rel_coin_s", 1),
                ["ruler"] = ("rel_first_s", 1), ["pencil"] = ("rel_first_s", 2),
                ["desk_lamp"] = ("rel_last_s", 1), ["coffee"] = ("rel_last_s", 2),
                ["magnifier"] = ("rel_rare_s", 1), ["mini_scope"] = ("rel_rare_s", 1), ["lucky_eraser"] = ("rel_rare_s", 1),
                ["gem_dust"] = ("rel_gem_s", 1), ["calculator"] = ("rel_gem_s", 2),
                // GOLD
                ["library_card"] = ("rel_book_g", 1), ["thick_tome"] = ("rel_book_g", 2),
                ["clover"] = ("rel_exp_g", 1), ["flame_canister"] = ("rel_exp_g", 1),
                ["hot_handle"] = ("rel_exp_g", 2), ["four_clover"] = ("rel_exp_g", 3),
                ["greed_goblet"] = ("rel_exp_g", 3), ["charm_relic"] = ("rel_exp_g", 4),
                ["set_charm"] = ("rel_setbonus_g", 1), ["combo_trophy"] = ("rel_setbonus_g", 1),
                ["wide_lens"] = ("rel_center_g", 1), ["focus_ring"] = ("rel_center_g", 2),
                ["black_candle"] = ("rel_skull_g", 1), ["black_report"] = ("rel_skull_g", 1),
                ["ominous_skull"] = ("rel_skull_g", 2), ["skull_idol"] = ("rel_skull_g", 3),
                ["gem_cert"] = ("rel_gem_g", 1), ["gem_tiara"] = ("rel_gem_g", 2),
                ["gamblers_eye"] = ("rel_rare_g", 1), ["fate_handle"] = ("rel_rare_g", 2), ["crystal_ball"] = ("rel_rare_g", 3),
                ["kings_ledger"] = ("rel_crown_g", 1), ["crown_stand"] = ("rel_crown_g", 2), ["crown_jewel"] = ("rel_crown_g", 3),
            };

        // 웹 engine.js:15 famOf 그대로 — 미등록은 (자기 id, 랭크1).
        public static (string Fam, int Rank) FamOf(string id) => Map.TryGetValue(id, out var f) ? f : (id, 1);
    }
}
