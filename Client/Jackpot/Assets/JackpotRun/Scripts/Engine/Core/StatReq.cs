using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 해금 조건 1개 항목 — Kotlin의 List<Pair<String, Long>> req 원소(key, threshold)에 대응.
    // ENGINE_PORT_DESIGN.md 공유 타입 계약: "public sealed class StatReq { public string key; public long value; }".
    public sealed class StatReq
    {
        public string key;
        public long value;

        public StatReq() { }

        public StatReq(string key, long value)
        {
            this.key = key;
            this.value = value;
        }
    }

    // StatReq 리스트 빌드 편의 헬퍼 — Machines.cs/Characters.cs에서 unlockReq 전사 시 반복되는
    // `new List<StatReq> { new StatReq("k1", v1), new StatReq("k2", v2) }` 패턴을 줄여준다.
    // 01_engine.md §9.1 meetsReq는 AND 조건(리스트 전 항목 충족), 빈 리스트 = 무료.
    internal static class Req
    {
        public static List<StatReq> None() => new List<StatReq>();

        public static List<StatReq> Of(string k1, long v1) =>
            new List<StatReq> { new StatReq(k1, v1) };

        public static List<StatReq> Of(string k1, long v1, string k2, long v2) =>
            new List<StatReq> { new StatReq(k1, v1), new StatReq(k2, v2) };
    }
}
