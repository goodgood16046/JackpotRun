using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 data.js:834-848 ACH_SYMBOL_UNLOCK) — 심화
    // 업적 달성 → POUCH_SYMBOLS 잠금 심볼 해금(13건). 업적 id → 해금되는 심볼 id 1:1 매핑 그대로 전사.
    // AchievementEngine.Evaluate가 신규 달성 업적마다 이 맵을 조회해 PlayerProfile.SymUnlocked에 추가한다
    // (웹 game.js:2610-2611 `const sym = ACH_SYMBOL_UNLOCK[a.id]; if (sym) { p.symUnlocked.push(sym); ... }`).
    public static class DeepSymbolUnlock
    {
        public static readonly Dictionary<string, string> ByAchId = new Dictionary<string, string>
        {
            { "d_ach_start", "catalyst" },              // 촉매(변환)
            { "d_ach_compress1", "purifier" },           // 정화(해골→빈칸)
            { "d_ach_risk_compress", "mirror_sym" },     // 거울
            { "d_ach_big_pouch", "target" },             // 표적
            { "d_ach_cherry_major", "jigsaw" },          // 퍼즐
            { "d_ach_curse_major", "black_candle_sym" }, // 검은양초(저주 코어)
            { "d_ach_gem_major", "prism_sym" },          // 프리즘(전설)
            { "d_ach_crown", "lucky7" },                 // 럭키7(전설)
            { "d_ach_balance", "magic_wand" },           // 마법봉(와일드)
            { "d_ach_purifier", "highlighter" },         // 형광펜(부활·증강 레벨업 확률↑)
            { "d_ach_rare10", "hourglass" },             // 모래시계(이월)
            { "d_ach_legend5", "review_book" },          // 복습책(부활·즉시 레벨업)
            { "d_ach_master", "shield" },                // 방패(보스 방어)
        };
    }
}
