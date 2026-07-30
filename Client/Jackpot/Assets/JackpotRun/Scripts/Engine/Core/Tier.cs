using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 증강/유물/저주 등급 — Kotlin enum class Tier (SlotV2Engine.kt L376): SILVER, GOLD, PRISM.
    public enum Tier
    {
        SILVER,
        GOLD,
        PRISM,
    }

    // 퍽 대분류 — ENGINE_PORT_DESIGN.md 공유 타입 계약: "public enum PCat { AUGMENT, RELIC, CURSE, ITEM }".
    // [설계 계약 준수 — Kotlin과 차이] Kotlin의 실제 PCat(SlotV2Engine.kt L377)은
    // `enum class PCat { AUGMENT, RELIC, CURSE }` 3종뿐이다(ITEM 없음, 아이템은 별도 ItemDef/IKind 체계).
    // ENGINE_PORT_DESIGN.md가 ITEM을 4번째 값으로 명시했고 작업 지시서가 "설계 타입 계약 준수"를
    // 요구했으므로 여기서는 설계 문서를 따른다 — Fable 확인 필요(스펙-설계 불일치 보고 대상).
    public enum PCat
    {
        AUGMENT,
        RELIC,
        CURSE,
        ITEM,
    }

    // 해금 조건 판정 — Kotlin fun meetsReq(req, stat): Boolean (SlotV2Engine.kt L182-183).
    // req.all { (key, thr) -> (stat[key] ?: 0L) >= thr } — 전 항목 AND, 빈 리스트 = 무료(항상 true).
    public static class Unlocks
    {
        public static bool Meets(IReadOnlyList<StatReq> req, IReadOnlyDictionary<string, long> stat)
        {
            if (req == null || req.Count == 0) return true;
            for (int i = 0; i < req.Count; i++)
            {
                var r = req[i];
                long cur = 0L;
                if (stat != null && stat.TryGetValue(r.key, out var v)) cur = v;
                if (cur < r.value) return false;
            }
            return true;
        }
    }
}
