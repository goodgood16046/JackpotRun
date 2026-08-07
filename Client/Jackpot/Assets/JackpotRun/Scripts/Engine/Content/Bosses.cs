using System;

namespace JackpotRun.Engine
{
    // 보스 — Kotlin data class Boss(SlotV2Engine.kt L57-58) 전사.
    // ENGINE_PORT_DESIGN.md 공유 타입 계약: "{ id,name; bonusSpins; quotaMul; string[] counterTags }".
    //
    // [설계 계약 결손 — 보고 대상] 계약에 emoji/desc 필드가 없다. 01_engine.md §1.2 표는 4종 전부
    // emoji·desc(원문)를 갖고 있고 콘텐츠 완전 전사 원칙(원칙 3)에 부합하므로 두 필드를 추가했다
    // — 계약이 명시한 필드는 그대로 보존.
    //
    // [원본 버그 유지] desc 원문은 finals/strict/luck 3종 모두 "요구↑"라고 적혀 있지만 실제 quotaMul은
    // 기본값 1.0(무보정)이다. grad만 실제로 1.15. 또한 desc에 적힌 "막스핀 EXP×2", "같은심볼 3개 없으면
    // ×0.5", "⭐👑🌀 배수" 같은 보스별 개별 전투 규칙은 SlotV2Engine.kt 어디에도 실행 코드가 없다
    // (01_engine.md §1.2·부록A-1). desc는 원문 그대로 옮기되 quotaMul/bonusSpins는 실제 코드값을 썼다.
    public sealed class Boss
    {
        public string id, name, emoji, desc;
        public int bonusSpins;
        public double quotaMul;
        public string[] counterTags;
    }

    // 보스 4종 — 01_engine.md §1.2 표 / SlotV2Engine.kt L60-65 (BOSSES) 전사.
    // 5스테이지마다 ((stage/5)-1) % 4 로 순환 등장.
    public static class Bosses
    {
        public const int Count = 4;

        public static readonly Boss[] All =
        {
            new Boss
            {
                id = "finals", emoji = "📝", name = "기말고사",
                desc = "스핀+1·요구↑ · 막스핀 EXP ×2 · 첫스핀 -10%",
                bonusSpins = 1, quotaMul = 1.0,
                counterTags = new[] { "막판형(복습·벼락치기)", "⏰최후" },
            },
            new Boss
            {
                id = "strict", emoji = "👨‍🏫", name = "꼰대교수",
                desc = "스핀+1·요구↑ · 같은심볼 3개↑ 없는 스핀 ×0.5",
                bonusSpins = 1, quotaMul = 1.0,
                counterTags = new[] { "세트·콤보(자석·체리)", "📌고정·📑복사" },
            },
            new Boss
            {
                id = "luck", emoji = "🎲", name = "운빨심판관",
                desc = "스핀+1·요구↑ · ⭐👑🌀 있으면 ×1.8/없으면 ×0.8",
                bonusSpins = 1, quotaMul = 1.0,
                counterTags = new[] { "⭐👑🌀 등장↑", "🔮예언·🎲올인" },
            },
            new Boss
            {
                id = "grad", emoji = "🎓", name = "졸업심사",
                desc = "스핀+1·요구↑↑ (빡센 관문)",
                bonusSpins = 1, quotaMul = 1.15,
                counterTags = new[] { "EXP 총량(과부하·탐욕)", "🚑응급처치·아이템" },
            },
        };

        public static Boss ById(string id) => Array.Find(All, b => b.id == id);

        // Kotlin bossFor(stage) (SlotV2Engine.kt L66): stage % 5 == 0 일 때만 순환 인덱스로 보스 반환.
        public static Boss For(int stage) =>
            Formulas.IsBossStage(stage) ? All[((stage / 5) - 1) % All.Length] : null;

        public static int Spins(int stage) => For(stage)?.bonusSpins ?? 0;

        public static double QuotaMulFor(int stage) => For(stage)?.quotaMul ?? 1.0;
    }
}
