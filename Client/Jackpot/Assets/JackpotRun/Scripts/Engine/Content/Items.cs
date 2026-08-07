using System;

namespace JackpotRun.Engine
{
    // 아이템 73종 — SlotV2Engine.kt L917-1002 (ITEMS) 전사.
    // kind 값은 Kotlin IKind enum 이름 그대로("NEXTSPIN"|"PHASE"|"INSTANT").
    // fx 딕셔너리 키는 SlotV2Engine.kt buildMods()/applyItemMods()가 다루는 Mods 필드명을 그대로 사용한다:
    //   스칼라 필드 → 필드명 그대로("expMul","flatExp","rareWeightMul","scoreMul","coinMul",
    //     "lastSpinExpMul","quotaMul","clearCoinBonus","skullScoreBonus" 등)
    //   심볼별 가중치 오버레이(Mods.symbolWeightMul / Mods.weightAdd) → "symbolWeightMul.<symId>" / "weightAdd.<symId>"
    // applyItemMods(L1494-1559)에 case가 없는 아이템(셀 조작계 eraser_old/eraser_fine/eraser_god/wild_temp/fake_crown은
    // applyCellOps 소관, INSTANT 23종 전부는 "엔진 미구현·서비스 처리")은 fx를 빈 딕셔너리로 둔다 — 원본에 수치가 없다.
    public static class Items
    {
        public const int Count = 78;

        public static readonly ItemDef[] All =
        {
            // ── NEXTSPIN — 다음 1스핀만 (23종, applyItemMods L1502-1533/1548 발췌) ──
            new ItemDef
            {
                id = "energy_drink", name = "에너지드링크", emoji = "🥤", desc = "다음 스핀 EXP 2배",
                kind = "NEXTSPIN", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "magnify", name = "확대경", emoji = "🔎", desc = "다음 스핀 희귀심볼 4배",
                kind = "NEXTSPIN", coinCost = 15,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["rareWeightMul"] = 4.0 },
            },
            new ItemDef
            {
                id = "loaded_dice", name = "조작주사위", emoji = "🎲", desc = "다음 스핀 👑왕관 주입·점수 2배",
                kind = "NEXTSPIN", coinCost = 22,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["weightAdd.crown"] = 5.0, ["scoreMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "ward_charm", name = "액막이부적", emoji = "🧿", desc = "다음 스핀 ☠해골 미등장",
                kind = "NEXTSPIN", coinCost = 10,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 0.0 },
            },
            new ItemDef
            {
                id = "adrenaline", name = "아드레날린", emoji = "💉", desc = "다음 스핀 EXP 3배",
                kind = "NEXTSPIN", coinCost = 30,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 3.0 },
            },
            new ItemDef
            {
                id = "rare_scope", name = "정밀스코프", emoji = "🔭", desc = "다음 스핀 희귀심볼 3배",
                kind = "NEXTSPIN", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["rareWeightMul"] = 3.0 },
            },
            new ItemDef
            {
                id = "crown_inject", name = "왕관주입", emoji = "👑", desc = "다음 스핀 👑왕관 대량 주입",
                kind = "NEXTSPIN", coinCost = 24,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["weightAdd.crown"] = 8.0 },
            },
            new ItemDef
            {
                id = "wild_inject", name = "와일드주입", emoji = "🌀", desc = "다음 스핀 🌀와일드 주입",
                kind = "NEXTSPIN", coinCost = 22,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["weightAdd.wild"] = 6.0 },
            },
            new ItemDef
            {
                id = "cherry_juice", name = "체리주스", emoji = "🧃", desc = "다음 스핀 🍒체리 확률 ↑",
                kind = "NEXTSPIN", coinCost = 5,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.cherry"] = 2.5 },
            },
            new ItemDef
            {
                id = "bookmark2", name = "책갈피", emoji = "🔖", desc = "다음 스핀 📘책 확률 ↑",
                kind = "NEXTSPIN", coinCost = 5,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.book"] = 2.5 },
            },
            new ItemDef
            {
                id = "sparkle_dust", name = "반짝이가루", emoji = "✨", desc = "다음 스핀 💎보석 확률 ↑",
                kind = "NEXTSPIN", coinCost = 6,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.gem"] = 2.5 },
            },
            new ItemDef
            {
                id = "gold_chalk", name = "황금분필", emoji = "🖍️", desc = "이번 스핀 EXP ×2",
                kind = "NEXTSPIN", coinCost = 13,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "focus_candy", name = "집중사탕", emoji = "🍬", desc = "다음 스핀 EXP +15%",
                kind = "NEXTSPIN", coinCost = 5,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 1.15 },
            },
            new ItemDef
            {
                id = "small_snack", name = "작은간식", emoji = "🍪", desc = "다음 스핀 ☠해골 미등장",
                kind = "NEXTSPIN", coinCost = 4,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 0.0 },
            },
            new ItemDef
            {
                id = "cherry_basket", name = "체리바구니", emoji = "🧺", desc = "다음 스핀 🍒체리 대량 등장",
                kind = "NEXTSPIN", coinCost = 7,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["weightAdd.cherry"] = 6.0 },
            },
            new ItemDef
            {
                id = "gem_loupe", name = "감정확대경", emoji = "💎", desc = "다음 스핀 💎보석 확률↑·점수 2배",
                kind = "NEXTSPIN", coinCost = 10,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.gem"] = 2.0, ["scoreMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "seal_tape", name = "봉인테이프", emoji = "🩹", desc = "다음 스핀 ☠해골 미등장",
                kind = "NEXTSPIN", coinCost = 9,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 0.0 },
            },
            new ItemDef
            {
                id = "skull_sticker", name = "해골스티커", emoji = "💯", desc = "다음 스핀 ☠해골 1개당 점수 +100(무페널티)",
                kind = "NEXTSPIN", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["skullScoreBonus"] = 100.0 },
            },
            new ItemDef
            {
                id = "eraser_old", name = "낡은지우개", emoji = "🧽", desc = "다음 스핀 가장 낮은 칸 1개 제거",
                kind = "NEXTSPIN", coinCost = 8,
                fx = new System.Collections.Generic.Dictionary<string, double>(), // applyCellOps 소관, Mods 오버레이 없음
            },
            new ItemDef
            {
                id = "eraser_fine", name = "고급지우개", emoji = "🧼", desc = "다음 스핀 가장 낮은 칸 1개 제거(정밀)",
                kind = "NEXTSPIN", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double>(), // applyCellOps 소관(eraser_old와 동일 case)
            },
            new ItemDef
            {
                id = "eraser_god", name = "신의지우개", emoji = "✨", desc = "다음 스핀 낮은 칸 최대 2개 제거",
                kind = "NEXTSPIN", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double>(), // applyCellOps 소관(최저가치 칸 제거 2회 반복)
            },
            new ItemDef
            {
                id = "wild_temp", name = "임시와일드", emoji = "🌀", desc = "다음 스핀 랜덤 1칸 → 🌀와일드",
                kind = "NEXTSPIN", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double>(), // applyCellOps 소관(RNG 1회 소비)
            },
            new ItemDef
            {
                id = "fake_crown", name = "가짜왕관", emoji = "👑", desc = "다음 스핀 가장 높은 칸 → 👑왕관",
                kind = "NEXTSPIN", coinCost = 24,
                fx = new System.Collections.Generic.Dictionary<string, double>(), // applyCellOps 소관
            },

            // ── PHASE — 이번 스테이지 내내 (27종, applyItemMods L1506-1546 발췌) ──
            new ItemDef
            {
                id = "espresso", name = "에스프레소", emoji = "☕", desc = "이번 스테이지 스핀마다 EXP +15",
                kind = "PHASE", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["flatExp"] = 15.0 },
            },
            new ItemDef
            {
                id = "study_streak", name = "집중모드", emoji = "✍️", desc = "이번 스테이지 스핀마다 EXP +6",
                kind = "PHASE", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["flatExp"] = 6.0 },
            },
            new ItemDef
            {
                id = "rare_lure", name = "행운미끼", emoji = "🍀", desc = "이번 스테이지 희귀심볼 2배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["rareWeightMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "coin_magnet", name = "코인자석", emoji = "🧲", desc = "이번 스테이지 코인 2배·클리어코인+8",
                kind = "PHASE", coinCost = 14,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["coinMul"] = 2.0, ["clearCoinBonus"] = 8.0 },
            },
            new ItemDef
            {
                id = "dbl_nothing", name = "올인학습", emoji = "🎰", desc = "이번 스테이지 스핀마다 EXP+30·요구치+20%",
                kind = "PHASE", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["flatExp"] = 30.0, ["quotaMul"] = 1.2 },
            },
            new ItemDef
            {
                id = "last_minute", name = "막판스퍼트", emoji = "⏰", desc = "이번 스테이지 마지막 스핀 EXP 2배",
                kind = "PHASE", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["lastSpinExpMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "tutor", name = "과외", emoji = "👨‍🏫", desc = "이번 스테이지 스핀마다 EXP +10",
                kind = "PHASE", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["flatExp"] = 10.0 },
            },
            new ItemDef
            {
                id = "fortune_incense", name = "행운향", emoji = "🍀", desc = "이번 스테이지 희귀심볼 1.6배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["rareWeightMul"] = 1.6 },
            },
            new ItemDef
            {
                id = "coin_press", name = "주화압인", emoji = "🪙", desc = "이번 스테이지 코인 3배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["coinMul"] = 3.0 },
            },
            new ItemDef
            {
                id = "overtime", name = "야근", emoji = "⏰", desc = "이번 스테이지 마지막 스핀 EXP 2배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["lastSpinExpMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "cram_note", name = "벼락치기노트", emoji = "📓", desc = "이번 스테이지 마지막 스핀 EXP ×2",
                kind = "PHASE", coinCost = 14,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["lastSpinExpMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "rich_lure", name = "큰행운미끼", emoji = "🍀", desc = "이번 스테이지 희귀심볼 3배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["rareWeightMul"] = 3.0 },
            },
            new ItemDef
            {
                id = "prof_bribe", name = "교수매수봉투", emoji = "🧧", desc = "이번 스테이지 요구치 -15%",
                kind = "PHASE", coinCost = 24,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["quotaMul"] = 0.85 },
            },
            new ItemDef
            {
                id = "sugar_powder", name = "설탕가루", emoji = "🍚", desc = "이번 스테이지 🍒체리 1.6배·EXP+8",
                kind = "PHASE", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.cherry"] = 1.6, ["flatExp"] = 8.0 },
            },
            new ItemDef
            {
                id = "cherry_cracker", name = "체리폭죽", emoji = "🧨", desc = "이번 스테이지 🍒체리 2배·점수+20%",
                kind = "PHASE", coinCost = 14,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.cherry"] = 2.0, ["scoreMul"] = 1.2 },
            },
            new ItemDef
            {
                id = "book_copy", name = "족보사본", emoji = "📄", desc = "이번 스테이지 📘책 2배·EXP+8",
                kind = "PHASE", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.book"] = 2.0, ["flatExp"] = 8.0 },
            },
            new ItemDef
            {
                id = "allnight_note", name = "밤샘노트", emoji = "🌙", desc = "이번 스테이지 📘책 1.8배·EXP+12",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.book"] = 1.8, ["flatExp"] = 12.0 },
            },
            new ItemDef
            {
                id = "summary_note", name = "요약노트", emoji = "🗒️", desc = "이번 스테이지 스핀마다 EXP+9",
                kind = "PHASE", coinCost = 13,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["flatExp"] = 9.0 },
            },
            new ItemDef
            {
                id = "gem_pouch", name = "보석주머니", emoji = "👝", desc = "이번 스테이지 💎보석 2배·점수+25%",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.gem"] = 2.0, ["scoreMul"] = 1.25 },
            },
            new ItemDef
            {
                id = "greed_lens", name = "탐욕의렌즈", emoji = "🔍", desc = "이번 스테이지 점수 1.5배",
                kind = "PHASE", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["scoreMul"] = 1.5 },
            },
            new ItemDef
            {
                id = "black_candle_i", name = "검은양초", emoji = "🕯️", desc = "이번 스테이지 ☠해골 2배·EXP 1.3배",
                kind = "PHASE", coinCost = 14,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 2.0, ["expMul"] = 1.3 },
            },
            new ItemDef
            {
                id = "curse_amp", name = "저주증폭제", emoji = "🩸", desc = "이번 스테이지 ☠해골 1.6배·점수 1.4배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 1.6, ["scoreMul"] = 1.4 },
            },
            new ItemDef
            {
                id = "gold_chalk_box", name = "황금분필세트", emoji = "✏️", desc = "이번 스테이지 EXP 1.5배",
                kind = "PHASE", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 1.5 },
            },
            new ItemDef
            {
                id = "skull_shield", name = "해골방패", emoji = "🛡️", desc = "이번 스테이지 ☠해골 미등장",
                kind = "PHASE", coinCost = 14,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["symbolWeightMul.skull"] = 0.0 },
            },
            new ItemDef
            {
                id = "combo_mega", name = "콤보확성기", emoji = "📢", desc = "이번 스테이지 마지막 스핀 EXP 2배·점수 1.2배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["lastSpinExpMul"] = 2.0, ["scoreMul"] = 1.2 },
            },
            new ItemDef
            {
                id = "cram_note_x2", name = "벼락치기노트+", emoji = "📔", desc = "이번 스테이지 마지막 스핀 EXP 2배",
                kind = "PHASE", coinCost = 16,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["lastSpinExpMul"] = 2.0 },
            },
            new ItemDef
            {
                id = "overload_potion", name = "폭주물약", emoji = "🧪", desc = "이번 스테이지 EXP 2배·요구치+20%",
                kind = "PHASE", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double> { ["expMul"] = 2.0, ["quotaMul"] = 1.2 },
            },

            // ── INSTANT — 즉발 (23종, L933-1001). applyItemMods에 case 없음(엔진 미구현·서비스 처리) → fx 전부 빈 값. ──
            new ItemDef
            {
                id = "first_aid", name = "응급처치", emoji = "🩹", desc = "이번 스테이지 스핀 +1",
                kind = "INSTANT", coinCost = 30,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "cram", name = "벼락치기", emoji = "📚", desc = "즉시 게이지 +요구치 15%",
                kind = "INSTANT", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "answer_sheet", name = "족보", emoji = "📝", desc = "즉시 게이지 +요구치 50%",
                kind = "INSTANT", coinCost = 40,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "grad_cert", name = "졸업장", emoji = "🎓", desc = "즉시 게이지 +요구치 100% (돌파)",
                kind = "INSTANT", coinCost = 100,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "double_aid", name = "특급처치", emoji = "🚑", desc = "이번 스테이지 스핀 +2",
                kind = "INSTANT", coinCost = 55,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "cheat_sheet", name = "커닝페이퍼", emoji = "📋", desc = "즉시 게이지 +요구치 30%",
                kind = "INSTANT", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "honor_roll", name = "우등생증", emoji = "🏅", desc = "즉시 게이지 +요구치 70%",
                kind = "INSTANT", coinCost = 60,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "dev_battery", name = "배터리부스트", emoji = "🔋", desc = "다음 스핀 EXP +30%",
                kind = "INSTANT", coinCost = 8,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "score_sticker", name = "점수스티커", emoji = "💯", desc = "사용 즉시 점수 +150",
                kind = "INSTANT", coinCost = 5,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "old_coin", name = "낡은동전", emoji = "🪙", desc = "즉시 코인 +6",
                kind = "INSTANT", coinCost = 4,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "grad_copy", name = "졸업장복사본", emoji = "🎓", desc = "즉시 게이지 +요구치 80%·점수 -10%",
                kind = "INSTANT", coinCost = 70,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "score_calc", name = "점수계산기", emoji = "🧮", desc = "즉시 현재 점수 +30%",
                kind = "INSTANT", coinCost = 22,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "mini_coupon", name = "미니쿠폰", emoji = "🎟️", desc = "즉시 코인 +9",
                kind = "INSTANT", coinCost = 5,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "price_hack", name = "가격표조작기", emoji = "🏷️", desc = "즉시 코인 +18",
                kind = "INSTANT", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "grad_ring", name = "졸업반지", emoji = "💍", desc = "부족 EXP ≤20이면 즉시 클리어",
                kind = "INSTANT", coinCost = 50,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "gold_grad_bell", name = "황금졸업벨", emoji = "🔔", desc = "부족 EXP ≤50이면 즉시 클리어",
                kind = "INSTANT", coinCost = 90,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "insurance_cert", name = "보험증서", emoji = "📋", desc = "이번 스테이지 실패 시 1회 생존(스핀+2)",
                kind = "INSTANT", coinCost = 45,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "debt_note", name = "빚문서", emoji = "🧾", desc = "코인 +30 / 이후 4스테이지 클리어보상 0",
                kind = "INSTANT", coinCost = 0,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "retake_form", name = "재시험신청서", emoji = "📄", desc = "직전 스핀 전체 다시 굴림",
                kind = "INSTANT", coinCost = 28,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "black_lottery", name = "검은복권", emoji = "🎫", desc = "50% 골드유물 / 50% 저주 1개",
                kind = "INSTANT", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "devil_contract", name = "악마의계약서", emoji = "😈", desc = "유물 1개 + 저주 1개(코인+25)",
                kind = "INSTANT", coinCost = 20,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "timeline_ticket", name = "세계선티켓", emoji = "🎟️", desc = "다음 스핀 2번 굴려 유리한 쪽 자동확정",
                kind = "INSTANT", coinCost = 26,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "broken_prism", name = "깨진프리즘", emoji = "🔮", desc = "이번 스테이지 랜덤 프리즘 증강효과 1개",
                kind = "INSTANT", coinCost = 22,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #12/#14, data.js:765-770) — 증강 레벨업 상점
            // 상품 5종. 전부 INSTANT·엔진 fx 없음(ItemUse.ApplyItemPurchase가 game.js:1368-1372 그대로
            // 직접 처리 — study_note/gold_marker=AugLevels.LevelableHeld 최저레벨 강화, aug_catalyst=
            // RunState.AugLevelBoost 가산, prism_ink=RunState.PrismInkActive 플래그, overcharge=
            // PendingNextExpMul·Score 직접 조작).
            new ItemDef
            {
                id = "study_note", name = "강화노트", emoji = "📓", desc = "보유 증강 중 가장 낮은 레벨 1개를 즉시 레벨업",
                kind = "INSTANT", coinCost = 18,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "aug_catalyst", name = "증강촉매", emoji = "🧪", desc = "다음 증강 레벨업 발생 확률 +15%",
                kind = "INSTANT", coinCost = 12,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "gold_marker", name = "골드형광펜", emoji = "📙", desc = "보유 골드 증강 1개를 즉시 레벨업",
                kind = "INSTANT", coinCost = 30,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "prism_ink", name = "프리즘잉크", emoji = "💧", desc = "다음 증강 선택을 프리즘 등급으로 (런당 1회 구매)",
                kind = "INSTANT", coinCost = 35,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
            new ItemDef
            {
                id = "overcharge", name = "과충전배터리", emoji = "⚡", desc = "다음 스핀 EXP +50% · 점수 -200 (고위험)",
                kind = "INSTANT", coinCost = 26,
                fx = new System.Collections.Generic.Dictionary<string, double>(),
            },
        };

        public static ItemDef ById(string id) => Array.Find(All, x => x.id == id);
    }
}
