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
        // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): 이 상수는 이제 이 엔진 안에서 CapMulFor(제거됨, 아래
        // 주석) 용도로는 쓰이지 않는다. 웹 engine.js:582 comment("누적 특수배수(럭키7/검은초/불안정폭탄/
        // 프리즘) — 전역배 직전 C.MAX_SPIN_EXP_MUL(8.0)로 캡")와 동일하게, 심화모드 specialMul 캡
        // 전용으로 데이터만 보존한다(심화모드 이식은 P7 — 아직 이 상수를 쓰는 코드가 없다).
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

        // ── 1.3 capMulFor — 웹 파리티 P2로 제거(WEB_PARITY_DESIGN §2-B) ──────────
        // 웹 engine.js에는 스핀 총배율(=위치/불꽃/첫막스핀/전역배수 곱) 상한이 없다(grep 결과: 웹
        // MAX_SPIN_EXP_MUL(8.0)은 럭키7/검은초/불안정폭탄/프리즘의 specialMul 누적에만 쓰이는 별개
        // 캡이고, engine.js:1009-1011에서만 등장 — SpinResolver.Evaluate가 하던 "총배율 캡"과는 다른
        // 메커니즘이며 그 specialMul 캡 자체는 아직 이식되지 않음, P7 심화모드 범위). Kotlin 원본에만
        // 있던 이 함수와 SpinResolver.Evaluate의 capMul 클램프·ResolveSpin의 lastSpinExpMul 5.0 상한을
        // 함께 제거했다 — 원본 함수/테스트는 git 이력으로만 남는다.

        // ── 1.4 stageClearScore (웹 engine.js:73-80 stageClearScore) ────────────
        // 웹 파리티 P2: 저주 배수(옛 Kotlin s*=(1+0.05*curses)) 제거 — 웹은 저주에 클리어 점수 보너스가
        // 없다("저주는 패널티 전용" 주석, engine.js:79). curses 매개변수는 웹 함수 시그니처와 그대로
        // 맞추기 위해 유지하되(웹도 동일하게 미사용 인자로 남겨둠) 계산에는 관여하지 않는다. streakBonus
        // 는 이 함수에 포함되지 않는다 — 웹도 game.js:1412 실사용부에서 stageClearScore 동등 계산과
        // streakBonus(stage)를 별도로 더한다(engine.js의 stageClearScore 자체는 streak 미포함 — 호출부가
        // 전혀 없는 죽은 함수이지만 시그니처/동작을 그대로 이식). Formulas.StreakBonus를 별도로 더할 것.
        public static long StageClearScore(int stage, long leftoverExp, int leftSpins, int curses, bool boss)
        {
            double s = stage * 50.0;
            s += leftoverExp * SCORE_PER_LEFTOVER;
            s += leftSpins * SCORE_PER_LEFTSPIN;
            if (boss) s += BOSS_CLEAR_SCORE;
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

        // ── 9.2 Player 레벨/XP (P3, WEB_PARITY_DESIGN.md §1-A #9 — 웹 game.js:107-120 그대로 이식) ──
        // 레벨은 "콘텐츠 해금 게이트"일 뿐 영구 스탯 보정이 없다(웹 주석 game.js:107). 아래 §9.3
        // AccountExp/AccountLevel(졸업레벨 1~25, 퍽 게이트가 아직 참조 중이라 이번 슬라이스에서 손대지
        // 않음)과는 완전히 별개의 체계다 — 혼동 방지를 위해 이 섹션은 전부 "Player" 접두로 명명한다.
        public const int PLAYER_LEVEL_MAX = 100; // 웹 game.js:108 PLV_MAX

        // 레벨 lvl → lvl+1 에 필요한 XP (웹 game.js:109 `xpReq = (lvl) => 120 + (lvl - 1) * 60`).
        public static long PlayerXpReq(int lvl) => 120L + (lvl - 1) * 60L;

        // 누적 XP → 레벨 (웹 game.js:110-115 levelInfo) — 닫힌 형태 공식이 아니라 "레벨별 요구치를
        // 순차 차감"하는 누적식이다: while(lvl<MAX && rem>=xpReq(lvl)) { rem-=xpReq(lvl); lvl++; } 그대로.
        public static int PlayerLevelFromXp(long totalXp)
        {
            int lvl = 1;
            long rem = Math.Max(0L, totalXp);
            while (lvl < PLAYER_LEVEL_MAX && rem >= PlayerXpReq(lvl))
            {
                rem -= PlayerXpReq(lvl);
                lvl++;
            }
            return lvl;
        }

        // 런 종료 XP (웹 game.js:2619 `runXp = 40 + Math.min(20, r.stage) * 12 + Math.floor(finalScore
        // / 250) + r.stats.bossClears * 20 + newly.length * 25`). stage는 실패/포기 시점의 현재 스테이지
        // (아직 클리어 전 값 — 웹 r.stage), bossClearsThisRun/newAchievementCount는 이번 런 한정(통산
        // 누적 아님 — 웹 r.stats.bossClears·newly.length). finalScore/250의 정수나눗셈은 finalScore>=0
        // 전제하에 Math.floor와 동일한 결과.
        public static long PlayerRunXp(int stage, long finalScore, int bossClearsThisRun, int newAchievementCount)
        {
            long xp = 40L + Math.Min(20, stage) * 12L;
            xp += finalScore / 250L;
            xp += bossClearsThisRun * 20L;
            xp += newAchievementCount * 25L;
            return xp;
        }

        // 기존 플레이어(이력 보유)에게 최초 1회만 부여하는 초기 XP(웹 game.js:117-120 seedXpFromHistory)
        // — 그동안의 플레이를 레벨에 소급 반영한다. 1회성 적용 자체는 ProfileDto.FromDto가
        // PlayerProfile.PlayerXpSeeded(웹 profile._xpInit 대응)로 관리한다.
        public static long PlayerSeedXpFromHistory(long runs, long totalScore, long bossClears, long bestStage)
        {
            return runs * 30L + totalScore / 300L + bossClears * 15L + bestStage * 8L;
        }

        // ── 9.3 accountExp / expToLevel / expForLevel (SlotV2Engine.kt L840-880) ─
        // ⚠️ 웹 파리티 P3(§9.2 위) PlayerXpReq/PlayerLevelFromXp/PlayerRunXp와는 다른 시스템이다 —
        // 이쪽(AccountExp/AccountLevel, "졸업레벨" 1~25)은 Kotlin 원본 이식이고 Shop.PerkGate가 아직
        // 참조 중이라 건드리지 않았다(WEB_PARITY_DESIGN.md §1-B "업적 482종... 폐기 → 웹 34종 체계로
        // 교체(P3)" — 그 업적 교체 슬라이스에서 함께 정리 예정).
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

        // ── 9.4 숙련도(mastery, P3, WEB_PARITY_DESIGN.md §1-A #11 — 웹 game.js:143-165 MASTERY 그대로) ──
        // kind: "char"/"mac"/"dev". 5개 마일스톤 각각 독립 판정 후 충족 개수를 레벨로 삼는다(웹
        // masteryLevel L166-170: `for (const d of defs) { if (d.test(s)) lv++; ... }` — else-if 게이트가
        // 아니라 매 마일스톤을 항상 개별 평가한다. 예: bossClears가 낮아도 bestScore만 높으면 그
        // 마일스톤만 별도로 인정된다). ascMax는 승천(P6) 미구현이라 항상 -1로 들어와 char[4]가 영구
        // 미충족이다(코드 변경 불필요 — 기본값 자체가 자연히 막는다).
        public static int MasteryTotal(string kind) => kind == "char" || kind == "mac" || kind == "dev" ? 5 : 0;

        public static int MasteryLevel(string kind, int runs, int bestStage, int bossClears, long bestScore, int ascMax)
        {
            int lv = 0;
            switch (kind)
            {
                case "char": // 웹 game.js:144-150
                    if (bestStage >= 3) lv++;
                    if (bossClears >= 3) lv++;
                    if (bestStage >= 15) lv++;
                    if (bestScore >= 50000) lv++;
                    if (ascMax >= 5) lv++;
                    break;
                case "mac": // 웹 game.js:151-157
                    if (runs >= 3) lv++;
                    if (bestStage >= 8) lv++;
                    if (bossClears >= 5) lv++;
                    if (bestScore >= 40000) lv++;
                    if (bestStage >= 15) lv++;
                    break;
                case "dev": // 웹 game.js:158-164
                    if (runs >= 5) lv++;
                    if (runs >= 15) lv++;
                    if (runs >= 30) lv++;
                    if (runs >= 60) lv++;
                    if (runs >= 100) lv++;
                    break;
            }
            return lv;
        }
    }
}
