using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════
    // 재미 개편(FUN_OVERHAUL_DESIGN §1 R3~R5·F1, 2026-10-28) UI 노출용 읽기 전용 뷰/결과 DTO.
    // 웹 game.js `state()` 의 hold / fever / encore / lastSpin 객체와 1:1 의미(필드명 동일). 필드는 SpinOutcome/
    // ClearOutcome 계열(결과 페이로드)의 관례대로 lowerCamelCase 공개 필드다. 일반 모드 전용 — 심화 런에서는
    // RunController.Hold/Fever/Encore 가 null 이다(웹 state().hold/fever/encore === null).
    // ══════════════════════════════════════════════════════════════════════

    // R3 홀드 — 웹 state().hold = { charges, held, max }.
    public sealed class HoldView
    {
        public int charges;               // 남은 홀드 충전(스테이지 시작 1, 집중 +1)
        public IReadOnlyList<int> held;   // 지금 잡은 칸 인덱스(오름차순)
        public int max;                   // 동시에 잡을 수 있는 최대 칸(Formulas.HOLD_MAX = 2)
    }

    // R4 일반 모드 피버 — 웹 state().fever = { gauge, spins }.
    public sealed class FeverView
    {
        public int gauge;   // 0~Formulas.FEVER_MAX(100) — 100 도달 즉시 spins=3 으로 바뀌고 0 으로 리셋
        public int spins;   // 남은 피버 스핀(최종 EXP ×1.5)
    }

    // R5 앵콜(클리어 점수 더블업) — 웹 state().encore = { available, active, k, pot, stake, lastBust, nextMul }.
    public sealed class EncoreView
    {
        public bool available;   // 지금 [앵콜 스핀] 을 누를 수 있는가(클리어 보드 + 남은 스핀≥1 + stake>0 + (진행 중 || 점수≥stake))
        public bool active;      // 첫 앵콜 스핀 이후 정산 전(판돈이 팟으로 걸려 있음)
        public int k;            // 성공 횟수
        public long pot;         // 현재 팟(성공마다 floor(stake×(1+0.5k)), BUST 시 0)
        public long stake;       // 판돈 = 이번 스테이지 클리어 점수
        public bool lastBust;    // 직전 앵콜 스핀이 BUST 였는가
        public double? nextMul;  // 다음 성공 시 stake 대비 배율(available 일 때만, 아니면 null)
    }

    // 앵콜 스핀 1회 결과 — 웹 lastSpin.encoreResult = { mulApplied, gained, bust, settled, settledPot, lostPot }.
    public sealed class EncoreResult
    {
        public double mulApplied;   // 이번에 시도한 배율(성공 시 적용, BUST 면 시도했던 배율)
        public long gained;         // 팟 증가분(성공: pot-potBefore, BUST: 0)
        public bool bust;
        public bool settled;        // 남은 스핀 0 으로 자동 정산됐는가
        public long settledPot;     // 자동 정산 시 점수에 더해진 팟
        public long lostPot;        // BUST 로 소멸한 팟(판돈 포함)
    }

    // 마지막 스핀(또는 앵콜 스핀) 요약 — 웹 state().lastSpin. RunState.LastSpin / RunController.LastSpin.
    public sealed class LastSpinInfo
    {
        public string kind;                  // "spin" | "encore"
        public SpinMode mode;                // 일반/특수 스핀 모드 (앵콜 스핀은 N)
        public IReadOnlyList<Cell> cells;    // 이번 스핀 최종 칸
        public long gained;                  // 이번 스핀 최종 EXP(앵콜은 0 — EXP 는 버려진다)
        public long score;                   // 이번 스핀이 만든 점수(앵콜 스핀의 점수는 run 에 반영되지 않는다)
        public int coins;                    // 이번 스핀이 만든 코인(앵콜은 실제 지급됨)
        public bool feverApplied;            // 이번 스핀에 피버 ×1.5 가 적용됐는가
        public bool feverTriggered;          // 이번 스핀으로 게이지가 가득 차 피버가 발동했는가(다음 스핀부터 적용)
        public IReadOnlyList<int> holdUsed;  // 실제로 직전 심볼을 유지한 칸 인덱스(없으면 빈 목록)
        public EncoreResult encoreResult;    // 앵콜 스핀일 때만 non-null
        public int bigWinTier;               // F1 빅윈 티어 0=없음 1=NICE 2=BIG 3=MEGA (앵콜은 0)
    }
}
