namespace JackpotRun.Engine
{
    // 승천(심화 학기) 단계 규칙 중 "런 상태에 실제로 적용"하는 배선 — 웹 파리티 P6(WEB_PARITY_DESIGN.md
    // §1-A #18). AscMods.cs(Core, 순수 숫자 공식)와 달리 이 파일은 Content(Symbols)와 RunState를
    // 참조하므로 Engine/Run 계층에 둔다(Core→Run 역방향 의존 금지 원칙 유지, Formulas.cs Quota() 주석과
    // 동일한 레이어링 원칙).
    internal static class AscRunHooks
    {
        // 웹 game.js:425 `r.rng.pick(["cherry","book","star","gem"])` — A8+ 스테이지 진입마다 재추첨.
        private static readonly string[] BannedSymPool = { "cherry", "book", "star", "gem" };

        // "스테이지 시작" 지점(런 1스테이지 시작·다음 스테이지 진입·A10 2페이즈 재시작) 3곳에서 호출한다
        // (웹 _beginStage()의 A8 분기, game.js:425). asc<8이면 항상 빈 문자열로 리셋.
        public static void RollBannedSym(RunState run)
        {
            run.BannedSym = run.Asc >= 8 ? run.Rng.Pick(BannedSymPool) : "";
        }

        // 웹 game.js:449-452 `_mods()`의 A2/A8 추가 규칙 — "실제 롤에 쓰이는 최종 mods"에만 적용한다
        // (weightAdd/symbolWeightMul은 Weighted()가 읽는 필드라 실제 심볼 추첨에 쓰이는 mods 객체
        // 자체를 건드려야 의미가 있다 — QuotaOf 등 수치 계산 전용 mods에는 적용할 필요 없음).
        //   A2+  weightAdd.skull += 0.5×(a-1)
        //   A8+  symbolWeightMul[bannedSym] = 0 (가산이 아니라 대입 — 웹 `{...,[bannedSym]:0}` 그대로)
        // 호출측: SpinResolver.ResolveSpin(주 스핀) · DeviceActions.HandlePeek(예언 미리보기)·
        // HandleManip(dev_reroll/dev_pin 재굴림)·GamblerReroll(도박꾼 무료재굴림) · ItemUse.
        // UseRetakeForm(재시험 재굴림)·timeline_ticket(타임라인 티켓 미리보기) — Weighted()를 실제로
        // 태우는 모든 지점(웹의 단일 this._mods() 진입점에 대응하는 Unity의 다중 mods 재구성 지점).
        public static void ApplyRunAscMods(Mods mods, RunState run)
        {
            if (mods == null || run == null) return;
            int a = run.Asc;
            if (a <= 0) return;
            if (a >= 2)
            {
                mods.weightAdd.TryGetValue(Sym.Skull, out var cur);
                mods.weightAdd[Sym.Skull] = cur + 0.5 * (a - 1);
            }
            if (a >= 8 && !string.IsNullOrEmpty(run.BannedSym))
            {
                var info = Symbols.ById(run.BannedSym);
                if (info != null) mods.symbolWeightMul[info.sym] = 0.0;
            }
        }
    }
}
