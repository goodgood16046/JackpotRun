using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — RewardDone 화면(보상 획득 → 다음 스테이지 인트로)이
    // 읽는 표시 전용 산출물 2종. 게임 규칙에는 전혀 관여하지 않는다(Mods를 읽기만 하고 RunState를
    // 변형하지 않음) — 웹 game.js:1573-1614 `_enterRewardDone`/`currentStats` 그대로 옮겼다.
    //
    // 파일명(RewardDoneInfo.cs)과 주 타입명(RewardDoneView)이 다르다 — Opus 2차검수 LOW⑥ 확인,
    // 리네임은 하지 않는다(파일명은 "이 화면이 다루는 데이터 묶음"을, 타입명은 기존 관례(GainPanel/
    // *View 계열과 달리 여기는 UI 컴포넌트가 아니라 순수 조회 static class라 Kotlin-이식 코드베이스의
    // "View"= "표시용 조회 함수 묶음" 명명 관례를 그대로 따름)를 반영한다 — CurrentStatsInfo/
    // NextStagePreviewInfo 등 데이터 레코드 타입까지 전부 이 한 파일에 있다는 사실은 이 주석으로 충분).
    public static class RewardDoneView
    {
        // ── 다음 스테이지 프리뷰 (웹 r.nextPreview, game.js:1577-1580) ─────────────────────────
        // StageFlow.ClearStage가 이미 run.Stage를 다음 스테이지로 갱신해 두었으므로(Unity는 spins/quota를
        // 스테이지마다 캐시하지 않고 매번 SpinResolver.EffSpins/QuotaOf로 즉석 계산하는 구조라, 웹의
        // "_beginStage()가 실행되기 전까지는 r.spins/r.quota가 이전 스테이지 값"이라는 제약이 애초에
        // 없다) 이 함수는 run.Stage/현재 보유 Perks/Curses/Device/PerkLevels 그대로 다음 스테이지의
        // 실제 요구 EXP·스핀 수를 정확히 계산한다 — RunController.ProceedToStage가 실제로 Spin에
        // 진입할 때와 동일한 값(그 사이 상태가 바뀌지 않으므로).
        public static NextStagePreviewInfo NextPreview(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            // Opus 2차검수 필수④(2026-08-09) — ApplyPassiveDevice 누락으로 dev_reactor(quotaMul×1.15)
            // 장착 시 프리뷰 요구 EXP가 실제 값보다 15% 낮게 보였다. CurrentStats(이 파일)와 동일한
            // 3단 조성(Build→ApplyPassiveDevice→ApplyItemMods, 웹 `_mods()` 순서 그대로)으로 통일한다.
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.ApplyPassiveDevice(
                    ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                    run.Device),
                run.PhaseItems);

            var boss = Bosses.For(run.Stage);
            return new NextStagePreviewInfo
            {
                stage = run.Stage,
                quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2),
                spins = SpinResolver.EffSpins(run, mods),
                bossId = boss?.id ?? "",
            };
        }

        // ── 현재 능력치 (웹 r.statsView, game.js:1589-1614 currentStats) ───────────────────────
        // 웹 _mods()와 동일 조성(buildMods→applyPassiveDevice→applyItemMods)이되, ctx는 "다음 스테이지
        // 시작 직전"을 뜻하는 값(stage=run.Stage(이미 다음 스테이지)·spinIndex=0·growthStack/snowStack/
        // curseCount/boss/coins=현재 run 값)으로 채운다 — 웹의 `_ctx()`가 실제로는 "아직 갱신 안 된
        // 직전 스테이지의 r.spins/r.quota"를 참조하는 스테일 값 버그(§0 결정 원칙 "웹의 명백한 회귀
        // 버그"에 해당하지 않는 사소한 차이라 굳이 재현하지 않음)를 갖고 있는 것과 달리, 이 값들은
        // 항상 "지금 이 시점의 진짜 다음 스테이지" 기준이라 daredevil/cliff_focus류 ctx-조건부 퍽도
        // 정확히 반영된다.
        public static CurrentStatsInfo CurrentStats(RunState run)
        {
            var ctx = new RunCtx
            {
                stage = run.Stage,
                spinIndex = 0,
                spinsPerStage = Formulas.SPINS_PER_STAGE,
                stageExp = 0,
                quota = 0,
                growthStack = run.GrowthStack,
                snowStack = run.SnowStack,
                curseCount = run.Curses.Count,
                unluckyGauge = run.UnluckyGauge,
                boss = Bosses.For(run.Stage) != null,
                coins = run.Coins,
            };
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, ctx, run.PerkLevels);
            mods = ModsBuilder.ApplyPassiveDevice(mods, run.Device);
            if (run.PhaseItems.Count > 0) mods = ModsBuilder.ApplyItemMods(mods, run.PhaseItems);

            var info = new CurrentStatsInfo();
            void Mul(string label, double v, bool lowerIsBetter = false)
            {
                if (v == 1.0) return;
                info.rows.Add(new StatRow { label = label, value = "×" + FmtMul(v), up = lowerIsBetter ? v < 1.0 : v > 1.0 });
            }
            void Add(string label, long v, string suffix = "")
            {
                if (v == 0) return;
                info.rows.Add(new StatRow { label = label, value = (v > 0 ? "+" : "") + v + suffix, up = v > 0 });
            }

            Mul("모든 EXP", mods.expMul);
            Add("스핀당 EXP", mods.flatExp);
            Mul("점수", mods.scoreMul);
            Mul("코인", mods.coinMul);
            Mul("첫 스핀 EXP", mods.firstSpinExpMul);
            Mul("막 스핀 EXP", mods.lastSpinExpMul);
            Mul("세트 보너스", mods.setExpMul);
            Mul("가운데 칸 EXP", mods.centerExpMul);
            Mul("양끝 일치 EXP", mods.endsMatchExpMul);
            Add("인접쌍 EXP", mods.adjacentSameExp, "/쌍");
            Mul("희귀 등장", mods.rareWeightMul);
            Add("해골 EXP", mods.skullExp);
            Add("스테이지 스핀", mods.bonusSpins);
            Mul("요구 EXP", mods.quotaMul, lowerIsBetter: true); // 낮을수록 이득(웹 game.js:1607)
            Add("클리어 코인", mods.clearCoinBonus);

            foreach (var kv in mods.perSymbolExp)
            {
                if (kv.Value == 0) continue;
                var s = Symbols.BySym(kv.Key);
                info.symExp.Add($"{(s != null ? s.name : kv.Key.ToString())}{(kv.Value > 0 ? "+" : "")}{kv.Value}");
            }
            foreach (var kv in mods.perSymbolScore)
            {
                if (kv.Value == 0) continue;
                var s = Symbols.BySym(kv.Key);
                info.symScore.Add($"{(s != null ? s.name : kv.Key.ToString())}{(kv.Value > 0 ? "+" : "")}{kv.Value}");
            }
            foreach (var kv in mods.tagExpBonus)
            {
                if (kv.Value == 0) continue;
                info.tags.Add($"#{kv.Key}{(kv.Value > 0 ? "+" : "")}{kv.Value}");
            }

            return info;
        }

        // 배율 표기 — 소수 2자리·끝0 제거(CLAUDE.md 표기 규칙 / SpinResolver.FmtMul와 동일 규칙).
        private static string FmtMul(double v)
        {
            string s = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
    }

    // 다음 스테이지 프리뷰(웹 r.nextPreview) — bossId가 빈 문자열이면 보스 스테이지 아님.
    public sealed class NextStagePreviewInfo
    {
        public int stage;
        public long quota;
        public int spins;
        public string bossId;
    }

    public sealed class StatRow
    {
        public string label;
        public string value;
        public bool up; // true=파랑/좋음 색, false=페널티 색(웹 up 클래스와 동일 의미)
    }

    public sealed class CurrentStatsInfo
    {
        public readonly List<StatRow> rows = new List<StatRow>();
        public readonly List<string> symExp = new List<string>();
        public readonly List<string> symScore = new List<string>();
        public readonly List<string> tags = new List<string>();
    }
}
