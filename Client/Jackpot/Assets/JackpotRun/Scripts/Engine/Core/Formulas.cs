using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 밸런스 상수 + 코어 공식 — 01_engine.md §0(상수)·§1(코어 공식)·§9.3(계정 EXP/레벨) 전사.
    // Kotlin 원본: SlotV2Engine.kt object SlotV2Engine (L19 이하). 스펙과 Kotlin이 다르면 Kotlin이 정답
    // (ENGINE_PORT_DESIGN.md 원칙 — 이 파일 작성 중 발견된 불일치는 없었음).
    public static class Formulas
    {
        // ── 밸런스 상수 (01_engine.md §0, SlotV2Engine.kt L21-95) ──────────────
        // 이름은 Kotlin const val과 동일하게 유지(스펙/원본과 그렙 가능하도록).
        public const int REEL = 5;
        public const int SPINS_PER_STAGE = 5;
        public const int MIN_SPINS = 3;
        public const int COIN_BASE = 0;
        public const int SKULL_PENALTY = 3;
        public const int BOMB_EXP_PER = 8;
        public const int KEY_COIN_PER = 4;
        public const double MAX_SPIN_EXP_MUL = 8.0;
        public const int UNLUCKY_MAX = 5;

        public const int CMD_COST_FOCUS = 1;
        public const int CMD_COST_LAST = 2;
        public const int CMD_COST_PRAY = 3;
        public const int CMD_COST_ALLIN = 4;
        public const int CMD_COST_BOSS_SURCHARGE = 1;
        public const int CMD_COST_MAX = 5;

        // 엔진 내부 미사용(서비스 전용) — 01_engine.md §0 각주. 노출만 하고 로직 의존 없음.
        public const int CLEAR_COIN = 5;
        public const int BOSS_COIN = 12;
        public const int ELITE_COIN = 8;
        public const int SCORE_PER_LEFTOVER = 2;
        public const int SCORE_PER_LEFTSPIN = 100;
        public const long CLOSE_CLEAR_BONUS = 150L;
        public const long BOSS_CLEAR_SCORE = 500L;

        public const int RETAKE_COIN_COST = 8;
        public const double SECONDARY_MUL = 0.6;
        public const int MEDAL_BRONZE_S = 5;
        public const int MEDAL_SILVER_S = 10;
        public const int MEDAL_GOLD_S = 15;

        // ── 1.1 quota(stage) — 스테이지 요구 EXP (SlotV2Engine.kt L41-51) ──────
        private static readonly long[] Quotas =
        {
            110, 120, 130, 140, 150, 170, 200, 235, 280, 330, 390, 460, 540, 640, 755,
        };

        public static long Quota(int stage)
        {
            int i = stage - 1;
            if (i < 0) return Quotas[0];
            if (i < Quotas.Length) return Quotas[i];
            // stage 16+: 755.0에서 시작해 (i-14)회 *1.2를 "반복 곱셈"으로 누적(거듭제곱 한 방 계산 금지 —
            // 01_engine.md §11-5, 반복곱과 Math.Pow는 부동소수 반올림 경로가 달라 결과가 어긋날 수 있음).
            double q = Quotas[Quotas.Length - 1];
            int repeats = i - Quotas.Length + 1;
            for (int k = 0; k < repeats; k++) q *= 1.2;
            return (long)q; // 항상 양수 → Kotlin Double.toLong()과 C# (long) 캐스트 결과 동일(0 방향 절삭).
        }

        // ── 1.2 보스 시스템 (SlotV2Engine.kt L53-68) ────────────────────────────
        // Bosses.All != null 이며 4종 고정이므로 "bossFor(stage) != null" 조건은 "stage % 5 == 0"과 동치.
        // Core는 Content(Bosses)에 의존하지 않도록 이 동치 관계로 직접 구현한다(디렉터리 의존 방향 유지).
        public static bool IsBossStage(int stage) => stage % 5 == 0;

        // ── 1.3 capMulFor — 스핀 총배율 상한 (SlotV2Engine.kt L70-76) ──────────
        public static double CapMulFor(int stage, bool hasPrism)
        {
            if (IsBossStage(stage)) return hasPrism ? 5.0 : 4.0;
            return hasPrism ? MAX_SPIN_EXP_MUL : 5.0;
        }

        // ── 1.4 stageClearScore (SlotV2Engine.kt L87-95) ────────────────────────
        public static long StageClearScore(int stage, long leftoverExp, int leftSpins, int curses, bool boss)
        {
            double s = stage * 50.0;
            s += leftoverExp * SCORE_PER_LEFTOVER;
            s += leftSpins * SCORE_PER_LEFTSPIN;
            if (boss) s += BOSS_CLEAR_SCORE;
            s *= 1.0 + 0.05 * curses;
            return (long)s;
        }

        // ── 1.5 tierForClearedStage / tierUp (SlotV2Engine.kt L672-680) ─────────
        // when 분기 순서 그대로: 5의 배수가 3의 배수보다 우선(15는 PRISM, GOLD 분기 도달 안 함).
        public static Tier TierForClearedStage(int stage)
        {
            if (stage <= 0) return Tier.SILVER;
            if (stage % 5 == 0) return Tier.PRISM;
            if (stage % 3 == 0) return Tier.GOLD;
            return Tier.SILVER;
        }

        public static Tier TierUp(Tier t)
        {
            if (t == Tier.SILVER) return Tier.GOLD;
            if (t == Tier.GOLD) return Tier.PRISM;
            return Tier.PRISM; // PRISM -> PRISM
        }

        // ── 기타 도파민 공식 (SlotV2Engine.kt L2033-2053) ───────────────────────
        public static (string emoji, string title) ScoreTitle(long best)
        {
            if (best >= 100_000) return ("🌈", "잭팟의 지배자");
            if (best >= 50_000) return ("👑", "슬롯 마스터");
            if (best >= 25_000) return ("🔥", "하이롤러");
            if (best >= 12_000) return ("🏅", "슬롯 숙련자");
            if (best >= 6_000) return ("💰", "단골 도박꾼");
            if (best >= 3_000) return ("🎲", "도전자");
            if (best >= 1_000) return ("🎰", "슬롯 입문자");
            return ("🐣", "잭팟 새내기");
        }

        public static long StreakBonus(int stage)
        {
            if (stage >= 15) return 600;
            if (stage >= 10) return 350;
            if (stage >= 7) return 200;
            if (stage >= 4) return 100;
            if (stage >= 2) return 40;
            return 0;
        }

        // ── 9.1 meetsReq — Unlocks.Meets와 동일 로직의 (string,long) 튜플 리스트 버전.
        // StatReq 기반 판정은 Unlocks.Meets(Tier.cs)를 사용하고, 이 오버로드는 §9.3 accountExp 등
        // 튜플 기반 임시 목록이 더 간단한 곳에서 쓴다.
        public static bool MeetsReq(IReadOnlyList<(string key, long threshold)> req, IReadOnlyDictionary<string, long> stat)
        {
            if (req == null || req.Count == 0) return true;
            for (int i = 0; i < req.Count; i++)
            {
                long cur = 0L;
                if (stat != null && stat.TryGetValue(req[i].key, out var v)) cur = v;
                if (cur < req[i].threshold) return false;
            }
            return true;
        }

        // ── 9.3 accountExp / expToLevel / expForLevel (SlotV2Engine.kt L840-880) ─
        //
        // [S1 범위 제약 — 보고 대상] Kotlin accountExp()의 ④ 컴포넌트("for (a in ACHIEVEMENTS) ...")는
        // 전역 ACHIEVEMENTS(482종, SlotV2AchievementsExt 포함)를 순회하는데, Achievements.cs는 S5 슬라이스
        // 산출물이라 S1 시점에는 존재하지 않는다("S1 파일은 S2 타입을 참조하지 말 것" 지시를 확장 적용 —
        // S1은 S2/S5 어떤 것도 참조하지 않는다). 그래서 achievements 매개변수를 선택적으로 받도록 시그니처를
        // 조정했다: null/빈 리스트면 ④ 컴포넌트는 0으로 계산되고, 나머지 ①②③⑤⑥ 컴포넌트는 Kotlin과 완전히
        // 동일하게 동작한다. S5에서 실제 482종 목록을 (key, threshold, tier) 튜플로 매핑해 전달하면 된다.
        public static long AccountExp(
            IReadOnlyDictionary<string, long> stat,
            IReadOnlyList<(string key, long threshold, string tier)> achievements = null)
        {
            long exp = 0L;

            // ① 마일스톤 합산 — 4개 조건 각각 독립 if (else-if 아님, 중첩 누적).
            long bs = StatOrZero(stat, "bestStage");
            if (bs >= 3) exp += 10;
            if (bs >= 5) exp += 30;
            if (bs >= 10) exp += 80;
            if (bs >= 15) exp += 150;

            // ② 보스클리어×8, 상한120
            exp += Math.Min(StatOrZero(stat, "bossClears") * 8L, 120L);

            // ③ 런×3, 상한90
            exp += Math.Min(StatOrZero(stat, "runs") * 3L, 90L);

            // ④ 달성 업적 tier합 (achievements가 없으면 0 — 위 주석 참조)
            if (achievements != null)
            {
                for (int i = 0; i < achievements.Count; i++)
                {
                    var a = achievements[i];
                    if (StatOrZero(stat, a.key) >= a.threshold) exp += AchTierExp(a.tier);
                }
            }

            // ⑤ 빌드도감 완성 1개당 +40 (bc_/bld_ 접두 키 중 값>0인 것 카운트)
            if (stat != null)
            {
                foreach (var kv in stat)
                {
                    if (kv.Value > 0L &&
                        (kv.Key.StartsWith("bc_", StringComparison.Ordinal) ||
                         kv.Key.StartsWith("bld_", StringComparison.Ordinal)))
                    {
                        exp += 40L;
                    }
                }

                // ⑥ 숙련 메달 — cstage_/mstage_ 접두 키의 값(도달 스테이지)을 메달 등급으로 환산해 가산.
                // medalFor(stage): >=15 GOLD(100) / >=10 SILVER(50) / >=5 BRONZE(20) / else NONE(0)
                // (SlotV2Engine.kt L1230-1239 medalFor + L1000 medalExp를 하나로 합친 것 — Medal enum은
                // S1 공유 타입 계약에 없고 다른 곳에서 쓰이지 않아 별도 공개 타입 없이 내부 헬퍼로 처리).
                foreach (var kv in stat)
                {
                    if (kv.Key.StartsWith("cstage_", StringComparison.Ordinal) ||
                        kv.Key.StartsWith("mstage_", StringComparison.Ordinal))
                    {
                        exp += MedalExpForStage(kv.Value);
                    }
                }
            }

            return exp;
        }

        public static int AccountLevel(
            IReadOnlyDictionary<string, long> stat,
            IReadOnlyList<(string key, long threshold, string tier)> achievements = null)
            => ExpToLevel(AccountExp(stat, achievements));

        public static int ExpToLevel(long exp)
        {
            double e = Math.Max(exp, 0L);
            int lvl = 1 + (int)Math.Floor(Math.Sqrt(e / 22.0));
            if (lvl < 1) lvl = 1;
            if (lvl > 25) lvl = 25;
            return lvl;
        }

        public static long ExpForLevel(int level)
        {
            if (level <= 1) return 0L;
            double l = level - 1;
            return (long)Math.Ceiling(l * l * 22.0);
        }

        private static long StatOrZero(IReadOnlyDictionary<string, long> stat, string key)
        {
            if (stat != null && stat.TryGetValue(key, out var v)) return v;
            return 0L;
        }

        private static long AchTierExp(string tier)
        {
            switch (tier)
            {
                case "프리즘": return 250L;
                case "골드": return 120L;
                case "실버": return 50L;
                default: return 20L; // 브론즈=20 (및 그 외 미지정 tier 문자열의 안전한 폴백)
            }
        }

        private static long MedalExpForStage(long stage)
        {
            if (stage >= MEDAL_GOLD_S) return 100L;
            if (stage >= MEDAL_SILVER_S) return 50L;
            if (stage >= MEDAL_BRONZE_S) return 20L;
            return 0L;
        }
    }
}
