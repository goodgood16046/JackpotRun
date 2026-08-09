using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // Opus 2차검수(P7-4b) [중대⑥] — 이번 UI 슬라이스(P7-4/P7-4b)가 새로 배선한 엔진 표면의 직접 테스트.
    // UI2/Editor 레이어(RunView/HudView/ShopPanel/DexView/UiSceneBuilder)는 이 하네스로 검증할 수 없지만
    // (UnityEngine 의존), 그 UI가 실제로 호출하는 엔진 API(StatTracker 심화 카운터 롤업·AchievementEngine
    // 심볼 해금 그랜트·RunState.SymUnlocked 필터·RepairShop.TargetsSym/Tag·ProfileDto 신규 필드 왕복·
    // BestAscStage)는 전부 여기서 결정론적으로 검증한다.
    internal static class Tests_P7_4_DeepUI
    {
        public static void Run(TestCtx t)
        {
            DeepAchievements13KeysTrigger(t);
            SymUnlockedFiltersOfferSymbolRewards(t);
            RunControllerSymUnlockedWiring(t);
            RepairShopTargetsSym(t);
            RepairShopTargetsTag(t);
            ProfileDtoRoundTripNewFields(t);
            BestAscStageTracking(t);
        }

        // ── ① 심화 업적 13종 — 각 (key,threshold) 트리거 + 심볼 해금(ACH_SYMBOL_UNLOCK 13매핑) ──────
        // Achievements.All에서 deep==true 13종을 직접 순회한다(하드코딩 목록 대신 데이터 자체를 단일
        // 진실 공급원으로 — Tests_S5_AchievementTrigger의 jackpot1/jackpot10 패턴 재사용).
        private static void DeepAchievements13KeysTrigger(TestCtx t)
        {
            var deepAchs = Achievements.All.Where(a => a.deep).ToList();
            t.Eq(13, deepAchs.Count, "[deep-ach] Achievements.All 중 deep==true 정확히 13종");
            t.Eq(13, DeepSymbolUnlock.ByAchId.Count, "[deep-ach] DeepSymbolUnlock.ByAchId 정확히 13매핑");

            foreach (var a in deepAchs)
            {
                t.True(a.req != null && a.req.Length == 1, $"[deep-ach] {a.id}: req 정확히 1원소");
                var req = a.req[0];

                var profile = new PlayerProfile();
                profile.Stats[req.key] = req.value - 1;
                var newlyBefore = AchievementEngine.Evaluate(profile);
                t.True(!newlyBefore.Any(x => x.id == a.id), $"[deep-ach] {a.id}: {req.key}={req.value - 1} — 미달성");
                t.True(!profile.AchievedIds.Contains(a.id), $"[deep-ach] {a.id}: 미달성 상태 AchievedIds 확인");
                t.True(!profile.SymUnlocked.Contains(DeepSymbolUnlock.ByAchId.TryGetValue(a.id, out var symBefore) ? symBefore : "__none__"),
                    $"[deep-ach] {a.id}: 미달성이면 심볼도 아직 미해금");

                profile.Stats[req.key] = req.value;
                var newlyAt = AchievementEngine.Evaluate(profile);
                t.True(newlyAt.Any(x => x.id == a.id), $"[deep-ach] {a.id}: {req.key}={req.value} — 신규 달성 목록에 포함");
                t.True(profile.AchievedIds.Contains(a.id), $"[deep-ach] {a.id}: AchievedIds 반영");

                if (DeepSymbolUnlock.ByAchId.TryGetValue(a.id, out var symId))
                    t.True(profile.SymUnlocked.Contains(symId), $"[deep-ach] {a.id} 달성 → 심볼 '{symId}' profile.SymUnlocked에 반영");

                // 재평가 시 중복 신규달성 없음(기존 관례 재확인).
                var newlyAgain = AchievementEngine.Evaluate(profile);
                t.True(!newlyAgain.Any(x => x.id == a.id), $"[deep-ach] {a.id}: 재평가 시 중복 신규달성 없음");
            }
        }

        // ── ② RunState.SymUnlocked가 PouchOffer.OfferSymbolRewards의 특수 카드 후보를 실제로 제한하는지 ──
        private static void SymUnlockedFiltersOfferSymbolRewards(TestCtx t)
        {
            var pouch = Pouch.NewStartPouch();

            // 완전 봉쇄(빈 집합) — Options.SymUnlocked가 비어 있으면 IsUnlocked(id)가 전부 false라
            // 어느 타이어에서도 후보가 없어 Skip 카드 1장만 남아야 한다(PouchOffer.cs:75 IsUnlocked·
            // chosen==null이면 continue 로직).
            var rngA = new Rng(70001L);
            var cardsLocked = PouchOffer.OfferSymbolRewards(rngA, pouch, 0, new PouchOffer.Options { SymUnlocked = new HashSet<string>() });
            t.Eq(1, cardsLocked.Count, "[symUnlocked] 빈 해금집합 — Skip 카드 1장만 생성");
            t.Eq(PouchCardType.Skip, cardsLocked[0].Type, "[symUnlocked] 빈 해금집합 — 유일한 카드는 Skip");

            // 완전 개방(58종 기본 해금) — 동일 시드로 다시 굴리면 특수 카드가 최소 1장 이상 나와야 한다
            // (SILVER 풀에 후보가 있으면 반드시 채워짐 — Pouch.DefaultUnlocked에 SILVER 등급 다수 포함).
            var rngB = new Rng(70001L);
            var cardsOpen = PouchOffer.OfferSymbolRewards(rngB, pouch, 0, new PouchOffer.Options { SymUnlocked = Pouch.DefaultUnlocked });
            t.True(cardsOpen.Count > 1, "[symUnlocked] 기본 58종 해금 — Skip 외 특수 카드가 최소 1장 이상 생성");
            t.True(cardsOpen.Any(c => c.Type == PouchCardType.Special), "[symUnlocked] 기본 58종 해금 — Special 카드 존재");
            foreach (var c in cardsOpen.Where(c => c.Type == PouchCardType.Special))
                t.True(Pouch.DefaultUnlocked.Contains(c.Id), $"[symUnlocked] 개방 카드 '{c.Id}'는 반드시 해금 집합 안");

            // 단일 해금(예: 잠금 심볼 1종만 허용) — 그 id만 Special로 나올 수 있고 그 외 잠금 심볼은
            // 절대 나오지 않는다(허용 집합 밖 id가 하나라도 섞이면 실패).
            var onlyCatalyst = new HashSet<string> { "catalyst" };
            var rngC = new Rng(70002L);
            var cardsSingle = PouchOffer.OfferSymbolRewards(rngC, pouch, 0, new PouchOffer.Options { SymUnlocked = onlyCatalyst });
            foreach (var c in cardsSingle.Where(c => c.Type == PouchCardType.Special))
                t.Eq("catalyst", c.Id, "[symUnlocked] 단일 해금 집합 — Special 카드는 반드시 그 id(catalyst)");
        }

        // ── RunController 생성자의 symUnlocked 트레일링 매개변수 배선 확인 ──────────────────────
        private static void RunControllerSymUnlockedWiring(TestCtx t)
        {
            // 명시 지정 — DefaultUnlocked보다 훨씬 좁은 집합을 넘기면 RunState.SymUnlocked가 정확히
            // 그 집합이어야 한다(내부적으로 몰래 DefaultUnlocked로 합치거나 폴백하면 안 됨).
            var restricted = new List<string> { "cherry" };
            var rc = new RunController("novice", "basic", "", 70101L, null, symUnlocked: restricted);
            t.Eq(1, rc.State.SymUnlocked.Count, "[symUnlocked-wiring] 명시 지정 시 정확히 그 집합 크기");
            t.True(rc.State.SymUnlocked.Contains("cherry"), "[symUnlocked-wiring] 'cherry' 포함");
            t.True(!rc.State.SymUnlocked.Contains("book"), "[symUnlocked-wiring] 'book'은 DefaultUnlocked에 있어도 미포함(폴백 없음)");

            // 미지정(null) — 기존 호출부 호환을 위해 Pouch.DefaultUnlocked로 안전 폴백해야 한다.
            var rcDefault = new RunController("novice", "basic", "", 70102L, null);
            t.Eq(Pouch.DefaultUnlocked.Count, rcDefault.State.SymUnlocked.Count, "[symUnlocked-wiring] 미지정 — DefaultUnlocked 크기와 동일");
            t.True(Pouch.DefaultUnlocked.All(id => rcDefault.State.SymUnlocked.Contains(id)), "[symUnlocked-wiring] 미지정 — DefaultUnlocked 전체 포함");
        }

        // ── ③ RepairShop.TargetsSym ─────────────────────────────────────────────────────────
        private static void RepairShopTargetsSym(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70201L);
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            run.SymUnlocked.UnionWith(Pouch.DefaultUnlocked);

            // addBasic — rarity=="기본"이고 해금된 것만.
            var svAddBasic = RepairServices.ById("sv_add_basic");
            var addBasicTargets = RepairShop.TargetsSym(run, svAddBasic, "id");
            t.True(addBasicTargets.Count > 0, "[targets-sym] addBasic 후보 존재");
            foreach (var target in addBasicTargets)
            {
                t.Eq("기본", target.Rarity, $"[targets-sym] addBasic 후보 '{target.Id}'는 희귀도=기본");
                t.True(run.SymUnlocked.Contains(target.Id), $"[targets-sym] addBasic 후보 '{target.Id}'는 해금됨");
            }

            // remove — 현재 보유 중(pouch>0)인 것만. 시작 덱(cherry/book/star/gem/coin/skull/flame/magnet/bomb) 9종.
            var svRemove = RepairServices.ById("sv_remove");
            var removeTargets = RepairShop.TargetsSym(run, svRemove, "id");
            t.Eq(9, removeTargets.Count, "[targets-sym] remove 후보 = 시작 덱 종류 수(9)");
            foreach (var target in removeTargets)
                t.True(run.Pouch.TryGetValue(target.Id, out var n) && n > 0, $"[targets-sym] remove 후보 '{target.Id}'는 실제 보유 중");

            // upgrade — 보유 중이면서 Pouch.Upgrade에 상위 매핑이 있는 것만(시작 덱 중 cherry/book/gem/
            // coin/skull/flame 6종 — magnet/bomb은 업그레이드 대상 아님).
            var svUpgrade = RepairServices.ById("sv_upgrade");
            var upgradeTargets = RepairShop.TargetsSym(run, svUpgrade, "id");
            t.Eq(6, upgradeTargets.Count, "[targets-sym] upgrade 후보 = 시작 덱 중 Pouch.Upgrade 매핑 보유(6)");
            foreach (var target in upgradeTargets)
                t.True(Pouch.Upgrade.ContainsKey(target.Id), $"[targets-sym] upgrade 후보 '{target.Id}'는 Pouch.Upgrade에 존재");

            // swap "from" — remove와 동일(보유분), "to" — 해금 풀 전체(addBasic보다 넓음, 전체 rarity 포함).
            var svSwap = RepairServices.ById("sv_swap");
            var swapFrom = RepairShop.TargetsSym(run, svSwap, "from");
            t.Eq(9, swapFrom.Count, "[targets-sym] swap-from = 보유 종류 수(9, remove와 동일 근거)");
            var swapTo = RepairShop.TargetsSym(run, svSwap, "to");
            t.True(swapTo.Count > addBasicTargets.Count, "[targets-sym] swap-to(전체 해금 풀)가 addBasic(기본 등급만)보다 후보가 많음");

            // 비심화 런(DeepMode=false) — 항상 빈 리스트(방어적 가드).
            var normalRun = S4TestHelpers.NewRun(70202L);
            t.Eq(0, RepairShop.TargetsSym(normalRun, svAddBasic, "id").Count, "[targets-sym] 비심화 런은 항상 빈 목록");
        }

        // ── ④ RepairShop.TargetsTag ─────────────────────────────────────────────────────────
        private static void RepairShopTargetsTag(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70301L);
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            run.DeepTagBuff["생명"] = 0.10; // cherry의 1차 태그(설계 로그 §2-(FF) 참조) — 버프 왕복 확인용.

            var tags = RepairShop.TargetsTag(run);
            t.True(tags.Count > 0, "[targets-tag] 시작 덱 기준 태그 후보 존재");

            var expected = Pouch.TagCounts(run.Pouch);
            t.Eq(expected.Count, tags.Count, "[targets-tag] Pouch.TagCounts와 동일 개수");
            foreach (var tag in tags)
            {
                t.True(expected.TryGetValue(tag.Tag, out var cnt), $"[targets-tag] '{tag.Tag}'가 Pouch.TagCounts에도 존재");
                t.Eq(cnt, tag.Cnt, $"[targets-tag] '{tag.Tag}' 개수 일치");
            }
            // 내림차순 정렬 확인.
            for (int i = 1; i < tags.Count; i++)
                t.True(tags[i - 1].Cnt >= tags[i].Cnt, "[targets-tag] 개수 내림차순 정렬");

            var cherryTag = tags.FirstOrDefault(x => x.Tag == "생명");
            if (cherryTag.Tag != null) t.EqTol(0.10, cherryTag.Buff, "[targets-tag] DeepTagBuff 값이 Buff 필드로 왕복");

            var normalRun = S4TestHelpers.NewRun(70302L);
            t.Eq(0, RepairShop.TargetsTag(normalRun).Count, "[targets-tag] 비심화 런은 항상 빈 목록");
        }

        // ── ⑤ ProfileDto 신규 필드(SymUnlocked/DeepRaresSeenIds/DeepLegendsSeenIds/BestAscStage) 왕복 ──
        private static void ProfileDtoRoundTripNewFields(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.SymUnlocked.Add("catalyst");
            profile.SymUnlocked.Add("hourglass");
            profile.DeepRaresSeenIds.Add("wild");
            profile.DeepLegendsSeenIds.Add("crown");
            profile.BestAscStage = 12;

            var dto = ProfileDto.ToDto(profile);
            var restored = ProfileDto.FromDto(dto);

            t.Eq(2, restored.SymUnlocked.Count, "[dto-roundtrip] SymUnlocked 개수 왕복");
            t.True(restored.SymUnlocked.Contains("catalyst") && restored.SymUnlocked.Contains("hourglass"), "[dto-roundtrip] SymUnlocked 내용 왕복");
            t.Eq(1, restored.DeepRaresSeenIds.Count, "[dto-roundtrip] DeepRaresSeenIds 개수 왕복");
            t.True(restored.DeepRaresSeenIds.Contains("wild"), "[dto-roundtrip] DeepRaresSeenIds 내용 왕복");
            t.Eq(1, restored.DeepLegendsSeenIds.Count, "[dto-roundtrip] DeepLegendsSeenIds 개수 왕복");
            t.True(restored.DeepLegendsSeenIds.Contains("crown"), "[dto-roundtrip] DeepLegendsSeenIds 내용 왕복");
            t.Eq(12, restored.BestAscStage, "[dto-roundtrip] BestAscStage 왕복");

            // 빈 프로필도 안전하게 왕복(마이그레이션 가드 불필요 확인 — 기본값 자연히 빈 집합/0).
            var emptyRestored = ProfileDto.FromDto(ProfileDto.ToDto(new PlayerProfile()));
            t.Eq(0, emptyRestored.SymUnlocked.Count, "[dto-roundtrip] 빈 프로필 — SymUnlocked 빈 집합");
            t.Eq(0, emptyRestored.DeepRaresSeenIds.Count, "[dto-roundtrip] 빈 프로필 — DeepRaresSeenIds 빈 집합");
            t.Eq(0, emptyRestored.DeepLegendsSeenIds.Count, "[dto-roundtrip] 빈 프로필 — DeepLegendsSeenIds 빈 집합");
            t.Eq(0, emptyRestored.BestAscStage, "[dto-roundtrip] 빈 프로필 — BestAscStage=0");
        }

        // ── ⑥ BestAscStage — 승천 최고기록 갱신 시 함께 기록되는지(StatTracker.ApplyGameOverTracking) ──
        private static void BestAscStageTracking(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(70401L);
            run.Asc = 5;
            run.Score = 50000;
            run.Stage = 9;
            var failure = StageFlow.ForceGameOver(run, 0);
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure } }, scratch);

            t.Eq(failure.finalScore, profile.BestAscScore, "[best-asc-stage] bestAscScore 갱신(기존 회귀 재확인)");
            t.Eq(9, profile.BestAscStage, "[best-asc-stage] run.Stage(9)가 BestAscStage로 반영");

            // 더 낮은 점수의 후속 런은 BestAscScore/BestAscStage 둘 다 갱신하지 않아야 함(strict > 게이트).
            var run2 = S4TestHelpers.NewRun(70402L);
            run2.Asc = 3;
            run2.Score = 1; // 확실히 더 낮게
            run2.Stage = 2;
            var failure2 = StageFlow.ForceGameOver(run2, 0);
            StatTracker.Apply(profile, run2, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure2 } }, new StatTracker.RunScratch());
            t.Eq(9, profile.BestAscStage, "[best-asc-stage] 더 낮은 점수의 후속 런은 BestAscStage 불변");
        }
    }
}
