using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 세트 효과 33종 — SlotV2Engine.kt L576-611 (SETS) 전사.
    // fx 딕셔너리 키는 buildMods() 세트 루프(L1961-1997)가 조작하는 Mods 필드명을 그대로 사용한다
    // (Items.cs/Devices.cs와 동일 규약): 스칼라 필드는 필드명 그대로, 심볼별 오버레이는
    // "perSymbolExp.<symId>"(pse)/"perSymbolScore.<symId>"(pss)/"symbolWeightMul.<symId>"(wmul)/
    // "weightAdd.<symId>"(wadd), 태그 가산은 "tagExpBonus.<tag>"(tag).
    //
    // reqChar/reqMachine/reqDevice — Fable 최종검수로 ContentTypes.cs의 SetEffect 계약에 추가됨(2026-07-30).
    // Kotlin SetEffect(L572-575)와 SETS 정의(L577-610)를 그대로 옮겼다: 33종 중 14종만 게이트가 있고
    // (reqChar/reqMachine/reqDevice 중 1개 이상 비어있지 않음), 나머지 19종은 셋 다 null(미지정) —
    // activeSets()(L612-618)/buildMods 세트 루프(L1958-1960)가 "비어있지 않으면 정확히 일치해야 발동"으로
    // 판정하므로 null/""은 무조건(게이트 없음)과 동일하다.
    public static class Sets
    {
        public const int Count = 33;

        public static readonly SetEffect[] All =
        {
            new SetEffect
            {
                id = "set_orchard", name = "체리 과수원", requires = new[] { "cherry_up", "cherry_farm" },
                desc = "🍒체리 EXP+3·등장↑",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 3, ["symbolWeightMul.cherry"] = 1.25 },
            },
            new SetEffect
            {
                id = "set_library", name = "도서관 회원증", requires = new[] { "book_up", "library", "study_tag" },
                desc = "📘책 EXP+3·학습+3",
                fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 3, ["tagExpBonus.학습"] = 3 },
            },
            new SetEffect
            {
                id = "set_necro", name = "강령술", requires = new[] { "skull_study", "black_candle" },
                desc = "☠해골 EXP+4",
                fx = new Dictionary<string, double> { ["skullExp"] = 4 },
            },
            new SetEffect
            {
                id = "set_appraiser", name = "감정사", requires = new[] { "gem_polish", "gem_invest", "gem_cert" },
                desc = "💎보석 점수+20",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 20 },
            },
            new SetEffect
            {
                id = "set_royal", name = "왕실 알현", requires = new[] { "crown_seek", "jackpot" },
                desc = "👑왕관 점수+40·등장↑",
                fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 40, ["weightAdd.crown"] = 2.0 },
            },
            new SetEffect
            {
                id = "set_align", name = "정렬의 묘", requires = new[] { "center", "twins", "chain" },
                desc = "인접쌍 EXP+10",
                // [원본 버그 유지] set_perfect_calc와 requires가 완전히 동일(center,twins,chain)한데
                // 상호배제가 없어 둘 다 동시 발동 → adjacentSameExp가 +10/+14로 이중 가산(01_engine.md 부록A-4).
                fx = new Dictionary<string, double> { ["adjacentSameExp"] = 10 },
            },
            new SetEffect
            {
                id = "set_combo", name = "콤보 마스터", requires = new[] { "set_sense", "set_charm" },
                desc = "세트 보너스+20%",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.2 },
            },
            new SetEffect
            {
                id = "set_diurnal", name = "주야겸행", requires = new[] { "morning", "evening" },
                desc = "첫·막 스핀 EXP+15%",
                fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.15, ["lastSpinExpMul"] = 1.15 },
            },
            new SetEffect
            {
                id = "set_necro2", name = "사령술 비전", requires = new[] { "necromancer", "skull_idol" },
                desc = "☠해골 EXP+5",
                fx = new Dictionary<string, double> { ["skullExp"] = 5 },
            },
            new SetEffect
            {
                id = "set_jewels", name = "보석 왕가", requires = new[] { "gem_buff", "lapidary", "gem_tiara" },
                desc = "💎보석 점수+20",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 20 },
            },
            new SetEffect
            {
                id = "set_combo2", name = "콤보 장인", requires = new[] { "combo_note", "combo_trophy" },
                desc = "세트 보너스+20%",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.20 },
            },
            new SetEffect
            {
                id = "set_royal2", name = "대관식", requires = new[] { "royal_decree", "crown_jewel" },
                desc = "👑왕관 점수+30·등장↑",
                fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 30, ["weightAdd.crown"] = 2.0 },
            },
            new SetEffect
            {
                id = "set_cherry_net", name = "체리 안전망", requires = new[] { "cherry_up", "cherry_jam" },
                desc = "🍒체리 EXP+2·점수+12",
                reqChar = "farmer",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 2, ["perSymbolScore.cherry"] = 12 },
            },
            new SetEffect
            {
                id = "set_red_harvest", name = "붉은 수확", requires = new[] { "cherry_farm", "great_harvest" },
                desc = "🍒체리 EXP+3·등장↑",
                reqMachine = "cherry",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 3, ["symbolWeightMul.cherry"] = 1.25 },
            },
            new SetEffect
            {
                id = "set_student", name = "모범생", requires = new[] { "study", "diligence", "note_take" },
                desc = "스핀마다 EXP+4",
                fx = new Dictionary<string, double> { ["flatExp"] = 4 },
            },
            new SetEffect
            {
                id = "set_lib_bless", name = "도서관의 축복", requires = new[] { "book_up", "library", "thick_tome" },
                desc = "📘책 EXP+4·학습+3",
                reqMachine = "library",
                fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 4, ["tagExpBonus.학습"] = 3 },
            },
            new SetEffect
            {
                id = "set_greed", name = "탐욕", requires = new[] { "greed", "rich_richer" },
                desc = "모든 점수+12%·코인+10%",
                fx = new Dictionary<string, double> { ["scoreMul"] = 1.12, ["coinMul"] = 1.10 },
            },
            new SetEffect
            {
                id = "set_glory_grad", name = "빛나는 졸업식", requires = new[] { "diploma_relic", "honor_student" },
                desc = "학습태그당 EXP+4·막스핀+15%",
                reqChar = "honor",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 4, ["lastSpinExpMul"] = 1.15 },
            },
            new SetEffect
            {
                id = "set_skull_lab", name = "해골 연구", requires = new[] { "skull_study", "skull_idol" },
                desc = "☠해골 EXP+6",
                reqChar = "cultist",
                fx = new Dictionary<string, double> { ["skullExp"] = 6 },
            },
            new SetEffect
            {
                id = "set_black_grad", name = "검은 졸업", requires = new[] { "necromancer", "black_candle", "skull_idol" },
                desc = "☠해골 EXP+5·점수+12%",
                reqMachine = "skull",
                fx = new Dictionary<string, double> { ["skullExp"] = 5, ["scoreMul"] = 1.12 },
            },
            new SetEffect
            {
                id = "set_curse_cycle", name = "저주 순환", requires = new[] { "set_charm" },
                desc = "세트 보너스+30%",
                reqDevice = "dev_seal",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.30 },
            },
            new SetEffect
            {
                id = "set_crown_rite", name = "왕관 의식", requires = new[] { "crown_seek", "crown_jewel" },
                desc = "👑왕관 점수+40·등장↑",
                reqChar = "crowncol",
                fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 40, ["weightAdd.crown"] = 2.0 },
            },
            new SetEffect
            {
                id = "set_kings_order", name = "왕의 명령", requires = new[] { "royal_decree", "jackpot" },
                desc = "👑왕관 점수+50·등장↑",
                reqMachine = "crown",
                fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 50, ["weightAdd.crown"] = 2.0 },
            },
            new SetEffect
            {
                id = "set_flame_lab", name = "불꽃 실험", requires = new[] { "all_or_nothing" },
                desc = "🔥불꽃 EXP+5·점수+12%",
                reqMachine = "flame", reqDevice = "dev_flame",
                fx = new Dictionary<string, double> { ["perSymbolExp.flame"] = 5, ["scoreMul"] = 1.12 },
            },
            new SetEffect
            {
                id = "set_last_ignite", name = "마지막 점화", requires = new[] { "review", "endgame_rush" },
                desc = "막 스핀 EXP+25%·점수+10%",
                fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 1.25, ["scoreMul"] = 1.10 },
            },
            new SetEffect
            {
                id = "set_mechanic", name = "정비공", requires = new[] { "set_sense" },
                desc = "세트 보너스+25%",
                reqDevice = "dev_subreel",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.25 },
            },
            new SetEffect
            {
                id = "set_battery", name = "배터리", requires = new[] { "battery", "diligence" },
                desc = "스핀마다 EXP+6",
                fx = new Dictionary<string, double> { ["flatExp"] = 6 },
            },
            new SetEffect
            {
                id = "set_gambler", name = "도박사", requires = new[] { "high_stakes", "high_roller" },
                desc = "희귀등장↑·💎보석 점수+25",
                reqChar = "gambler",
                fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.3, ["perSymbolScore.gem"] = 25 },
            },
            new SetEffect
            {
                id = "set_shop_reg", name = "상점 단골", requires = new[] { "coin_luck", "piggy_bank" },
                desc = "코인+20%·클리어코인+3",
                fx = new Dictionary<string, double> { ["coinMul"] = 1.20, ["clearCoinBonus"] = 3 },
            },
            new SetEffect
            {
                id = "set_scholarship", name = "장학금", requires = new[] { "study_tag", "diploma_relic" },
                desc = "학습태그당 EXP+4·클리어코인+2",
                reqChar = "scholar",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 4, ["clearCoinBonus"] = 2 },
            },
            new SetEffect
            {
                id = "set_bomb_calc", name = "폭탄마", requires = new[] { "center", "focus_fire" },
                desc = "가운데 칸 EXP+50%·점수+10%",
                reqMachine = "bomb",
                fx = new Dictionary<string, double> { ["centerExpMul"] = 1.5, ["scoreMul"] = 1.10 },
            },
            new SetEffect
            {
                id = "set_perfect_calc", name = "완벽한 계산", requires = new[] { "center", "twins", "chain" },
                desc = "인접쌍 EXP+14·가운데 +30%",
                // [원본 버그 유지] set_align과 requires가 완전히 동일(center,twins,chain) — §set_align 주석 참조.
                fx = new Dictionary<string, double> { ["adjacentSameExp"] = 14, ["centerExpMul"] = 1.3 },
            },
            new SetEffect
            {
                id = "set_safe_grad", name = "안전 졸업", requires = new[] { "insurance", "clover" },
                desc = "스핀마다 EXP+3·모든 점수+8%",
                fx = new Dictionary<string, double> { ["flatExp"] = 3, ["scoreMul"] = 1.08 },
            },
        };

        public static SetEffect ById(string id) => Array.Find(All, x => x.id == id);
    }
}
