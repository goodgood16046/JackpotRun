using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19) — 심화모드(deepMode) 런 스코프 훅. AscRunHooks.cs와
    // 동일한 위치·역할(internal, "특정 게임모드 축이 실제 롤/quota에 개입하는 지점만 모아둔 파일") —
    // Pouch/PouchOps가 콘텐츠·순수 추출 로직이라면, 이 파일은 그 둘을 RunState에 실제로 배선한다.
    internal static class DeepRunHooks
    {
        // 배치F P2(웹 game.js `deepPity`) — 획득 심볼 2스핀 보장. add/upgrade(P7-2/3 보상 지급) 직후
        // 설정된 run.DeepPity를 첫 "fresh 굴림"(LockedNext 미사용 — 예언/고정 굴림은 대상 아님, 웹과
        // 동일)에서 소진한다: 5칸에 이미 있으면 자연 등장 소진, 없으면 무작위 1칸 강제 치환 후 소진.
        // spinsLeft는 방어용 안전망(정상 플로우에서는 미도달) — 웹 `_pityRoll` 그대로.
        public static List<Cell> ApplyDeepPity(RunState run, List<Cell> raw)
        {
            if (!run.DeepMode || run.DeepPity == null || raw == null || raw.Count == 0) return raw;
            var pity = run.DeepPity;
            var psym = Symbols.ById(pity.Id);
            if (psym == null) { run.DeepPity = null; return raw; } // 설정 가드 통과분이라 실질 미도달(방어)

            pity.SpinsLeft -= 1;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].sym != null && raw[i].sym.id == pity.Id) { run.DeepPity = null; return raw; } // 자연 등장 → 소진
            }
            if (pity.SpinsLeft < 0) { run.DeepPity = null; return raw; } // 만료 안전망

            raw[run.Rng.Next(raw.Count)] = new Cell(psym, "✨→");
            run.DeepPity = null;
            return raw;
        }

        // 심화모드 압축 패널티(요구치 배수) — 일반모드는 항상 1(무영향), 웹 `_deepPenalty()`.
        // [P7-1 범위] 총량 기반 압축 패널티(Pouch.CompressionPenalty) × 상점 '덱 압축' 누적 요구율
        // (run.DeepCompressExtra, +5%씩 — 정비소 자체는 P7-2/3라 이번 슬라이스는 항상 0) × EARLY_QUOTA
        // 램프(stage 1~4)만 적용한다. 웹의 나머지 두 항 — 심볼증강 penaltyMul 완화 클램프(sp?.penaltyMul,
        // "초과분에만 적용" 공식 Max(1, 1+(base-1)*penaltyMul))와 전설봉인함(legendSeal) 보스요구 감쇄 —
        // 는 심볼퍽(sp = 웹 `_symMods()`) 자체가 P7-2/3 범위라 이번 슬라이스엔 존재하지 않는다. 그
        // 두 항은 항상 "적용 안 함"(=배수 1)과 동치이므로 지금 이 함수는 웹 공식에서 sp가 아직 null인
        // 상태와 정확히 같은 값을 낸다 — P7-2/3가 심볼퍽을 추가하면 이 함수에 그 두 항을 이어붙이면
        // 된다(구조는 이미 "base → (심볼퍽 배수) → EARLY_QUOTA" 순서를 그대로 반영해 둠).
        public static double DeepPenalty(RunState run)
        {
            if (run == null || !run.DeepMode) return 1.0;
            double baseMul = Pouch.CompressionPenalty(Pouch.Total(run.Pouch)) * (1.0 + run.DeepCompressExtra);
            double mul = baseMul; // TODO(P7-2/3): 심볼퍽 penaltyMul 완화 클램프 + legendSeal 보스요구 감쇄.
            mul *= Pouch.EarlyQuotaMul(run.Stage);
            return mul;
        }

        // ── Phase 4 V3P4(웹 POUCH_USE) "instant" 소모 — 이 슬라이스의 단순화 버전(작업 지시 6번) ──
        // 웹은 붕대/매듭/에너지팩/가짜왕관/진화핵 5종 각각의 실제 효과(evaluate 내부 특수분기, P7-2/3
        // 범위)가 발동하는 순간 자기 자신을 덱에서 -1 한다. 이 슬라이스는 그 실제 효과를 아직 구현하지
        // 않으므로(Sp 신규 51종 전부 evaluate에 case 없음, Symbols.cs 헤더 각주 참조) "등장 즉시 덱
        // -1"만 일반화해 둔다. Opus 2차검수(P7-1, 2026-08-09) [MED]③ — 웹 game.js:814-828의 실제
        // 소비 방식은 evaluate()가 뽑아낸 "이번 스핀에 등장했는가"(`res.hasBandage` 등 불리언 플래그,
        // 개수 아님)를 보고 `{type:"remove", id, n:1}`을 **최대 1회만** 적용한다 — 5칸 중 같은 instant
        // 심볼이 2번 이상 등장해도 덱에서는 정확히 1개만 빠진다. `Pouch.Use[id]=="instant"`인 심볼이
        // 이번 스핀 raw 셀에 "등장했는지"(중복 등장은 1회로 취급)만 보고 최대 1개만 감산한다(0 하한,
        // 0이면 키 제거). 실제 게임 효과는 P7-2/3가 이 감산 자리 옆에 이어붙이면 된다 — 이중 소모
        // (효과 로직이 따로 또 -1 하는 것) 방지를 위해 P7-2/3 구현 시 이 함수를 대체/확장하는 쪽으로
        // 통합할 것(지금은 자리만 예약).
        public static void ConsumeInstantSymbols(RunState run, IReadOnlyList<Cell> raw)
        {
            if (!run.DeepMode || raw == null) return;
            var consumedThisSpin = new HashSet<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                var id = raw[i].sym?.id;
                if (string.IsNullOrEmpty(id)) continue;
                if (!Pouch.Use.TryGetValue(id, out var use) || use != "instant") continue;
                if (!consumedThisSpin.Add(id)) continue; // 이미 이번 스핀에 이 id를 처리함(웹: 등장 여부만 봄)
                if (run.Pouch.TryGetValue(id, out var n) && n > 0)
                {
                    if (n <= 1) run.Pouch.Remove(id);
                    else run.Pouch[id] = n - 1;
                }
            }
        }
    }

    // 배치F P2(웹 `r.deepPity = {id, spinsLeft}`) — 신규 심볼 등장 보장 상태. 참조형(클래스)이라
    // RunState.DeepPity는 null 가능(웹 `r.deepPity = null` 초기값과 동일 관례).
    public sealed class DeepPityState
    {
        public string Id;
        public int SpinsLeft;

        public DeepPityState(string id, int spinsLeft)
        {
            Id = id;
            SpinsLeft = spinsLeft;
        }
    }

    // 웹 game.js:295-304 `r.deepStats` — 심화 전용 추적(랭킹 오염 방지·요약 + P7-4 심화 업적 카운터
    // 소스, 전부 심화 런에서만 존재). 웹 필드 그대로 전사 — 이번 슬라이스(P7-1)는 이 클래스의 존재와
    // RunController가 심화 런 시작 시 MaxTotal을 초기화하는 것까지만 담당한다(작업 지시 8번 "deepStats
    // 상태 골격만"). RewardsPicked/Repairs/BossClears 증가, Compress*/Cherry50.../Skull0... 플래그
    // 세팅, RaresSeen/LegendsSeen 수집은 전부 P7-2/3(보상/정비소/보스클리어 훅이 아직 없음) 이후에
    // 이 필드들을 갱신하기 시작하면 된다 — StatTracker/AchievementEngine 소비(P7-4)도 마찬가지.
    public sealed class DeepStats
    {
        public int RewardsPicked;
        public int Repairs;
        public int MaxTotal; // 이번 런 최대 총량(대형주머니 업적) — 런 시작 시 시작 덱 총량으로 초기화.
        public int BossClears; // 심화 보스 클리어 수(심볼마스터)
        public bool Compress95Clear;
        public bool Compress85BossClear;
        public bool Cherry50BossClear;
        public bool Skull40BossClear;
        public bool Gem50Score30kBoss;
        public bool Crown2BossClear;
        public bool BalanceBossClear;
        public bool Skull0BossClear;
        public readonly HashSet<string> RaresSeen = new HashSet<string>();
        public readonly HashSet<string> LegendsSeen = new HashSet<string>();
    }
}
