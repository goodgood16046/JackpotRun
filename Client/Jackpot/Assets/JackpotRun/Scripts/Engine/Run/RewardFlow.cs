namespace JackpotRun.Engine
{
    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 노드/상점 처리가 끝난 뒤 곧장 다음 스테이지 SPIN으로
    // 돌아가지 않고 REWARD_DONE 화면(보상 메시지 + 다음 스테이지 프리뷰 + 현재 능력치)을 거친다(웹
    // `_enterRewardDone(msg)`, game.js:1573-1585). 이 슬라이스 전까지 NodeEvents.cs/Shop.cs의 모든
    // 노드 해소 분기는 처리 직후 `run.Phase = RunPhase.Spin`으로 곧장 돌아갔다 — 그 대입을 전부 이
    // 헬퍼 호출로 교체한다. 예외 1곳: NodeEvents.TakeDevice(DEVICE 노드 확정)는 웹 deviceNodeTake
    // (game.js:2523-2529)가 `_enterRewardDone`을 거치지 않고 곧장 `_beginStage()`로 가는 것과 동일하게
    // REWARD_DONE을 건너뛴다(NodeEvents.TakeDevice 주석 참조) — 이 헬퍼를 쓰지 않는다.
    internal static class RewardFlow
    {
        internal static void Enter(RunState run, string message)
        {
            run.RewardMessage = message ?? "";
            run.Phase = RunPhase.RewardDone;
        }
    }
}
