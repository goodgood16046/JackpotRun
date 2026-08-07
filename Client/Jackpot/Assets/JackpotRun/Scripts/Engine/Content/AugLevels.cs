using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — 증강 레벨업(Lv1~3) 델타. 웹 engine.js:19-33
    // AUG_LEVELS 그대로 이식(12종: study/greed/polymath/cherry_up/book_up/star_up/diligence/set_sense/
    // coin_luck/skull_study/gem_polish/lucky — 프리즘 제외, 등록된 증강만 레벨업 가능).
    //
    // Lv1 값은 이 표에 없다 — Perks.cs의 기존 fx(무보정)가 그대로 Lv1이다. 여기 담긴 건 Lv2/Lv3에서
    // "추가로" 적용되는 델타뿐이고, ModsBuilder.Build가 perkIds 루프 직후 이 델타를 웹과 동일하게
    // 누적 적용한다(웹 buildMods L368-375: `if (lv>=2 && def[2]) def[2](_ops); if (lv>=3 && def[3])
    // def[3](_ops);` — Lv3는 Lv2 델타 + Lv3 델타를 *둘 다* 적용, "최종 절대값 교체"가 아니다).
    //
    // 델타 표기는 Perks.cs/Sets.cs 등 기존 콘텐츠와 동일한 "fx 점표기 딕셔너리"를 그대로 재사용한다
    // (ModsBuilder.ApplyFx가 이미 곱연산/가산연산 필드를 구분해 해석하므로 새 해석기가 필요 없다) —
    // 웹 pse/pss(가산) → perSymbolExp.*/perSymbolScore.*, 웹 o.m.xxxMul *= v(곱연산) → 동일 필드명 스칼라.
    public static class AugLevels
    {
        // id -> { level(2 or 3) -> fx 델타 }.
        private static readonly Dictionary<string, Dictionary<int, Dictionary<string, double>>> Table =
            new Dictionary<string, Dictionary<int, Dictionary<string, double>>>
        {
            // study: 모든 EXP 10%→15%→20%(engine.js:20 — 1.10 * 1.0455 ≈ 1.15, * 1.043 ≈ 1.20).
            ["study"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["expMul"] = 1.0455 },
                [3] = new Dictionary<string, double> { ["expMul"] = 1.043 },
            },
            // greed: 25%→32%→40%(engine.js:21).
            ["greed"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["expMul"] = 1.056 },
                [3] = new Dictionary<string, double> { ["expMul"] = 1.061 },
            },
            // polymath: 20%→26%→32%(engine.js:22).
            ["polymath"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["expMul"] = 1.05 },
                [3] = new Dictionary<string, double> { ["expMul"] = 1.048 },
            },
            // cherry_up: 🍒체리 EXP +2→+3→+4(engine.js:23).
            ["cherry_up"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 1 },
                [3] = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 1 },
            },
            // book_up: 📘책 EXP +2→+3→+4(engine.js:24).
            ["book_up"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["perSymbolExp.book"] = 1 },
                [3] = new Dictionary<string, double> { ["perSymbolExp.book"] = 1 },
            },
            // star_up: ⭐별 EXP +2→+3→+4(engine.js:25).
            ["star_up"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["perSymbolExp.star"] = 1 },
                [3] = new Dictionary<string, double> { ["perSymbolExp.star"] = 1 },
            },
            // diligence: 스핀마다 EXP +3→+5→+7(engine.js:26).
            ["diligence"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["flatExp"] = 2 },
                [3] = new Dictionary<string, double> { ["flatExp"] = 2 },
            },
            // set_sense: 세트 보너스 30%→40%→50%(engine.js:27).
            ["set_sense"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["setExpMul"] = 1.077 },
                [3] = new Dictionary<string, double> { ["setExpMul"] = 1.071 },
            },
            // coin_luck: 코인 30%→40%→50%(engine.js:28 — set_sense와 동일 델타 수치, 별도 % 주석 없음).
            ["coin_luck"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["coinMul"] = 1.077 },
                [3] = new Dictionary<string, double> { ["coinMul"] = 1.071 },
            },
            // skull_study: ☠해골 EXP +6→+8→+10(engine.js:29).
            ["skull_study"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["skullExp"] = 2 },
                [3] = new Dictionary<string, double> { ["skullExp"] = 2 },
            },
            // gem_polish: 💎보석 점수 +10→+13→+16(engine.js:30).
            ["gem_polish"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["perSymbolScore.gem"] = 3 },
                [3] = new Dictionary<string, double> { ["perSymbolScore.gem"] = 3 },
            },
            // lucky: 희귀 등장 20%→~25%→~30%(engine.js:31).
            ["lucky"] = new Dictionary<int, Dictionary<string, double>>
            {
                [2] = new Dictionary<string, double> { ["rareWeightMul"] = 1.04 },
                [3] = new Dictionary<string, double> { ["rareWeightMul"] = 1.04 },
            },
        };

        // 웹 engine.js:33 `export const isAugLevelable = (id) => !!AUG_LEVELS[id];`
        public static bool IsLevelable(string id) => id != null && Table.ContainsKey(id);

        // level: 2 또는 3만 유효(그 외는 false) — ModsBuilder.Build/PickView 등 호출측이 lv<2를 미리 걸러야 함.
        internal static bool TryGetDelta(string id, int level, out IReadOnlyDictionary<string, double> fx)
        {
            fx = null;
            if (id == null || !Table.TryGetValue(id, out var byLevel)) return false;
            if (!byLevel.TryGetValue(level, out var d)) return false;
            fx = d;
            return true;
        }

        // 레벨업 가능한 보유 증강 id 목록 — 웹 game.js:238-243 `_levelableHeld()` 그대로(AUG_LEVELS 등록
        // & 보유 & Lv<3). 이 함수 자체는 전체 후보를 캡 없이 반환한다(웹 selectNode AUGLEVEL 분기가
        // r.options 전체를 카드로 그린다, game.js:1622 `r.options = this._levelableHeld().map(...)`).
        // Unity UI 표시상 3장 캡은 이 함수가 아니라 호출측(NodeEvents.ChooseNode AugLevel 분기)이
        // 적용한다 — Opus 1차검수 필수(2026-08-08, §2-(M)) — `PerkOfferPanel`이 320px 고정 카드 3장
        // 전용이라 4장 이상이면 화면 밖으로 잘리기 때문. 이 함수를 그대로 다른 용도(예: 통계 집계)로
        // 재사용해도 캡의 영향을 받지 않도록 의도적으로 여기서는 자르지 않는다.
        public static List<string> LevelableHeld(RunState run)
        {
            var outp = new List<string>();
            if (run == null) return outp;
            for (int i = 0; i < run.Perks.Count; i++)
            {
                var id = run.Perks[i];
                if (!IsLevelable(id)) continue;
                int lv = run.PerkLevels.TryGetValue(id, out var v) ? v : 1;
                if (lv < 3) outp.Add(id);
            }
            return outp;
        }
    }
}
