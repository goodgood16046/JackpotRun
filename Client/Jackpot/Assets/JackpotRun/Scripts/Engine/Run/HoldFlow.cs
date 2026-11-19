using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════
    // 재미 개편 R3 홀드 + 스테이지 시작 리셋 — 웹 game.js `toggleHold`/`_applyHold`/`_beginStage`의 R3·R6 부분(e6bde8d).
    //
    // 직전 스핀의 원시 착지 칸(RunState.LastRaw: 홀드 적용 후·cellOps/Evaluate 이전)을 최대 HOLD_MAX 칸 잡아 다음 스핀에
    // 그대로 유지하고 나머지 칸만 새로 굴린다(RNG 소비는 그대로 — 전부 굴린 뒤 홀드 칸만 덮어쓴다). 스핀 후 홀드 표시는
    // 해제되고 충전이 1 소모된다(스테이지당 충전 1, 집중 특수스핀이 +1). 예언 확정(LockedNext)·심화·첫 스핀 전엔 불가.
    // 일반 모드 전용 — 심화(DeepMode)에서는 아무것도 하지 않는다(격리).
    // ══════════════════════════════════════════════════════════════════════
    internal static class HoldFlow
    {
        private static readonly string[] CmdMarkers = { "FOCUS", "ALLIN", "PRAY", "LAST" };

        // 웹 _beginStage(): 스테이지 시작 리셋. 호출 지점 = 런 시작(RunController 생성자)·다음 스테이지 진입
        // (StageFlow.ClearStage 상태 반영)·A10 2페이즈 보스 재시작. 홀드는 "매 스테이지 충전 1(누적 없음)"로 리셋하고
        // (심화는 0), 특수스핀 종류별 사용 횟수(누진 비용 근거)와 앵콜 창도 함께 초기화한다. 피버(NFever*)는 런 단위 이월이라
        // 건드리지 않는다.
        public static void ResetForStage(RunState run)
        {
            run.HoldCharges = run.DeepMode ? 0 : Formulas.HOLD_START_CHARGES;
            run.Held.Clear();
            run.LastRaw.Clear();
            for (int i = 0; i < CmdMarkers.Length; i++) run.CmdUses[CmdMarkers[i]] = 0;
            EncoreFlow.Reset(run);
        }

        // 웹 toggleHold(i). 성공이면 HOLD_CHANGED, 아니면 REJECTED(reason). 판정 순서는 웹과 동일.
        public static List<RunEvent> Toggle(RunState run, int index)
        {
            if (run.DeepMode) return RunEvents.Rejected("HOLD_DEEP_MODE");              // 웹: "홀드는 일반 모드에서만"
            if (run.Phase != RunPhase.Spin) return RunEvents.Rejected("PHASE_NOT_SPIN");
            if (run.LockedNext.Count > 0) return RunEvents.Rejected("HOLD_LOCKED_NEXT");  // 웹: 예언이 확정된 스핀엔 홀드 불가
            if (run.LastRaw.Count == 0) return RunEvents.Rejected("HOLD_NO_LAST_SPIN");   // 웹: 첫 스핀 전엔 홀드 불가
            if (index < 0 || index >= run.LastRaw.Count) return RunEvents.Rejected("HOLD_INDEX_OUT_OF_RANGE");
            int pos = run.Held.IndexOf(index);
            if (pos >= 0)
            {
                run.Held.RemoveAt(pos);   // 이미 잡은 칸 → 해제(충전 소모는 스핀 시점)
                return RunEvents.One(new RunEvent { type = "HOLD_CHANGED" });
            }
            if (run.HoldCharges < 1) return RunEvents.Rejected("HOLD_NO_CHARGE");
            if (run.Held.Count >= Formulas.HOLD_MAX) return RunEvents.Rejected("HOLD_MAX_REACHED");
            run.Held.Add(index);
            run.Held.Sort();
            return RunEvents.One(new RunEvent { type = "HOLD_CHANGED" });
        }

        // 웹 _applyHold(raw): 새로 굴린 raw 의 홀드 칸을 직전 raw 로 덮어쓴다(tag 비움). 범위 밖/직전 칸 없음은 무시.
        // 1칸 이상 적용되면 충전 -1. 적용된 인덱스 목록을 돌려준다(일반 모드·fresh 굴림 전용).
        public static List<int> Apply(RunState run, List<Cell> raw)
        {
            var used = new List<int>();
            if (run.DeepMode || run.LastRaw.Count == 0 || run.Held.Count == 0 || run.HoldCharges < 1) return used;
            for (int k = 0; k < run.Held.Count; k++)
            {
                int i = run.Held[k];
                if (i >= 0 && i < raw.Count && i < run.LastRaw.Count && run.LastRaw[i] != null)
                {
                    raw[i] = new Cell(run.LastRaw[i].sym, "");
                    used.Add(i);
                }
            }
            if (used.Count > 0) run.HoldCharges -= 1;
            return used;
        }

        // 웹 `r.lastRaw = raw.map(c => ({sym:c.sym, tag:""})); r.held = []` — 다음 스핀 홀드용 원시 착지 칸 스냅샷
        // (홀드 적용 후·cellOps/Evaluate 이전). 홀드 표시는 스핀/조작마다 해제된다. 일반 모드 전용.
        public static void SnapshotRaw(RunState run, IReadOnlyList<Cell> raw)
        {
            if (run.DeepMode) return;
            run.LastRaw.Clear();
            for (int i = 0; i < raw.Count; i++) run.LastRaw.Add(new Cell(raw[i].sym, ""));
            run.Held.Clear();
        }

        // 재굴림/조작이 만든 결과를 새 '직전 raw' 로 삼는다(잡아둔 홀드 칸 표시 Held 는 유지) — 웹 manip/_freeReroll 의
        // `if (!r.deepMode) r.lastRaw = cells.map(...)`.
        public static void ReplaceLastRaw(RunState run, IReadOnlyList<Cell> cells)
        {
            if (run.DeepMode) return;
            run.LastRaw.Clear();
            for (int i = 0; i < cells.Count; i++) run.LastRaw.Add(new Cell(cells[i].sym, ""));
        }

        // 예언(PEEK) 확정: 이미 잡은 홀드는 해제(충전은 보존). 웹 oracle() 의 `if (r.held && r.held.length) r.held = []`.
        public static void ReleaseOnOracle(RunState run)
        {
            if (run.Held.Count > 0) run.Held.Clear();
        }

        public static HoldView View(RunState run) => run.DeepMode ? null : new HoldView
        {
            charges = run.HoldCharges,
            held = run.Held.ToArray(),
            max = Formulas.HOLD_MAX,
        };

        public static FeverView FeverOf(RunState run) => run.DeepMode ? null : new FeverView
        {
            gauge = run.NFeverGauge,
            spins = run.NFeverSpins,
        };
    }
}
