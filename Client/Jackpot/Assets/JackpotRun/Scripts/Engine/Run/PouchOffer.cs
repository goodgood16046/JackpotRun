using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════
    // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스) — POUCH 오퍼 v3(engine.js:1725-1863
    // offerSymbolRewards) + 2-step 커밋(game.js:1890-2037 POUCH_COST/POUCH_REMOVE/POUCH pickPerk 분기)
    // + JACKPOT 노드(game.js:1697-1733) + SYMAUG/SYMREL 혼합 오퍼(game.js:1764-1813) + REST/GAMBLE
    // 심화 2택(game.js:1624-1663·1846-1889) + 3스테이지 연계 보너스 픽 커밋(game.js:1829-1844, 오퍼
    // 생성 자체는 NodeEvents.PickOffer 참조).
    //
    // NodeEvents.cs(퍽 오퍼/EVENT 테이블)와 동일한 위치·역할 — "노드 선택 후 사용자 응답을 기다리는
    // 오퍼"를 만들고, 그 응답(RunController.PickOffer 액션)을 커밋한다. RunController.DispatchPickOffer가
    // RunState.Phase로 이 파일의 함수들로 라우팅한다(웹 `pickPerk(idx)`의 `_pickKind` 분기와 동일 설계).
    // ══════════════════════════════════════════════════════════════════════

    public enum PouchCardType { Special, Skip }

    // POUCH 오퍼 카드 — 웹 offerSymbolRewards/JACKPOT 노드/3스테이지 연계 보너스가 공유하는 카드 계약
    // (type/id/tier/cost). Special 카드의 교체 비용 규칙(§2 V3P2): 실버=기본1개 제거·골드=기본2개
    // (이득<3이면 1개)·프리즘=기본2개 또는 저주+1 선택·저주=무료(+1, DECK_MAX만 검사).
    public sealed class PouchOfferCard
    {
        public PouchCardType Type;
        public string Id;      // Special 전용 — 획득할 심볼 id
        public string Tier;    // "SILVER"|"GOLD"|"PRISM"|"CURSE" — Special 전용
        public int RemoveN;    // 기본 제거 개수(실버1/골드·프리즘2)
        public int LowRemoveN; // 골드 한정: 기본 이득 총량<3이면 이 값(1)으로 완화
        public bool OrCurse;   // 프리즘 한정: 저주 경로(기본 심볼 대신 ☠해골+1) 선택 가능
        public bool Free;      // 저주 카드 또는 3스테이지 연계 보너스 — 무비용 즉시 추가
        public int CoinBonus;  // Skip 카드 전용(+5)
    }

    // 프리즘 특수 카드 2-step 커밋 중 임시 보관 — 웹 `r._pendingSpecial`.
    public sealed class PouchPendingSpecial
    {
        public string Id;
        public string Tier;
        public int RemoveN;
    }

    public static class PouchOffer
    {
        // ── §2 V3P2 offerSymbolRewards(v3) 옵션 — 웹 opts 객체 그대로(§1 bounds는 함수 본문에서 실제로
        // 참조되지 않는 죽은 매개변수라 이식하지 않는다, 웹 원문 grep 확인). ──
        public sealed class Options
        {
            // HashSet<string>(IReadOnlySet<T> 대신 — Unity apiCompatibilityLevel=NET_Standard_2_1엔
            // IReadOnlySet<T>가 없다. .Contains만 필요해 구체 타입으로 충분·다른 파일 관례와도 일치).
            public HashSet<string> SymUnlocked; // null = 전체 허용. profile.symUnlocked(P7-4 이전)는
                                                       // Pouch.DefaultUnlocked(58종)로 근사한다(아래 EnterPouchOffer 각주).
            public bool NoCurseAdds;
            public int LegendWeight;
            public int ExtraCards;
            public double GoldBonus;
            public double CurseChance = 0.05;
            public bool ForcePrismFirst;
        }

        private static int TierCap(string tier)
        {
            for (int i = 0; i < Pouch.RarityMaxSpecial.Length; i++)
                if (Pouch.RarityMaxSpecial[i].tier == tier) return Pouch.RarityMaxSpecial[i].cap;
            return int.MaxValue; // CURSE 등 상한 없음 — 웹 DEEP.RARITY_MAX에 키 없음 = Infinity.
        }

        // 웹 `offerSymbolRewards(rng, pouch, stage, opts)` 그대로(engine.js:1725-1863). stage는 호출측이
        // "clearedStage-1"(웹 `r.stage - 1`)을 넘긴다 — 함수 내부에서 realStage = stage+1로 복원.
        public static List<PouchOfferCard> OfferSymbolRewards(Rng rng, IReadOnlyDictionary<string, int> pouch, int stage, Options opts)
        {
            opts ??= new Options();
            var p = pouch ?? new Dictionary<string, int>();
            bool IsUnlocked(string id) => opts.SymUnlocked == null || opts.SymUnlocked.Contains(id);
            bool noCurse = opts.NoCurseAdds;
            int legendWgt = Math.Max(0, opts.LegendWeight);
            int extraCards = Math.Max(0, opts.ExtraCards);
            double goldBonus = Math.Max(0, opts.GoldBonus);
            double curseChance = noCurse ? 0 : opts.CurseChance;
            bool forcePrismFirst = opts.ForcePrismFirst;

            int realStage = stage + 1;
            bool isBoss5 = realStage % 5 == 0;
            bool is3x = !isBoss5 && realStage % 3 == 0;
            int baseCards = 2 + extraCards;

            var specByTier = new Dictionary<string, List<string>>
            {
                ["SILVER"] = new List<string>(), ["GOLD"] = new List<string>(),
                ["PRISM"] = new List<string>(), ["CURSE"] = new List<string>(),
            };
            var curTierSpec = new Dictionary<string, int>();
            foreach (var kv in p)
            {
                if (kv.Value <= 0 || Pouch.CatOf(kv.Key) != "special") continue;
                var tk = Pouch.TierOf(kv.Key) + "_special";
                curTierSpec[tk] = curTierSpec.TryGetValue(tk, out var c) ? c + kv.Value : kv.Value;
            }
            for (int i = 0; i < Pouch.Symbols71.Length; i++)
            {
                var id = Pouch.Symbols71[i];
                if (Pouch.CatOf(id) != "special") continue;
                if (!IsUnlocked(id)) continue;
                var tier = Pouch.TierOf(id);
                if (tier == "CURSE" && noCurse) continue;
                var tk = tier + "_special";
                int tierCap = TierCap(tk);
                int curTier = curTierSpec.TryGetValue(tk, out var c2) ? c2 : 0;
                if (curTier >= tierCap) continue;
                if (id == "crown" && (p.TryGetValue("crown", out var cn) ? cn : 0) >= Pouch.CrownMax) continue;
                if (id == "wild" && (p.TryGetValue("wild", out var wn) ? wn : 0) >= Pouch.WildMax) continue;
                specByTier[tier].Add(id);
            }

            if (legendWgt > 0)
            {
                var weighted = new List<string>();
                foreach (var id in specByTier["PRISM"])
                {
                    int times = Pouch.RarityOf(id) == "전설" ? 1 + legendWgt : 1;
                    for (int k = 0; k < times; k++) weighted.Add(id);
                }
                specByTier["PRISM"] = weighted;
            }
            if (realStage <= 2 && specByTier["SILVER"].Count > 0)
            {
                var weighted = new List<string>();
                foreach (var id in specByTier["SILVER"])
                {
                    int times = Array.IndexOf(Pouch.EarlyExpBoostIds, id) >= 0 ? 3 : 2;
                    for (int k = 0; k < times; k++) weighted.Add(id);
                }
                specByTier["SILVER"] = weighted;
            }

            var tierSeq = new List<string>();
            if (isBoss5)
            {
                tierSeq.Add("PRISM");
                for (int i = 1; i < baseCards; i++) tierSeq.Add("GOLD");
            }
            else if (forcePrismFirst)
            {
                tierSeq.Add("PRISM");
                if (is3x)
                {
                    for (int i = 1; i < baseCards; i++) tierSeq.Add("SILVER");
                }
                else
                {
                    for (int i = 1; i < baseCards; i++)
                        tierSeq.Add(rng.NextDouble() < goldBonus ? "GOLD" : "SILVER");
                }
            }
            else if (is3x)
            {
                tierSeq.Add("GOLD");
                for (int i = 1; i < baseCards; i++) tierSeq.Add("SILVER");
            }
            else
            {
                for (int i = 0; i < baseCards; i++)
                    tierSeq.Add(rng.NextDouble() < goldBonus ? "GOLD" : "SILVER");
            }

            var cards = new List<PouchOfferCard>();
            var usedIds = new HashSet<string>();
            for (int i = 0; i < tierSeq.Count; i++)
            {
                string tier = tierSeq[i];
                if (curseChance > 0 && specByTier["CURSE"].Count > 0 && rng.NextDouble() < curseChance) tier = "CURSE";
                string[] tierOrder = tier == "PRISM" ? new[] { "PRISM", "GOLD", "SILVER" }
                    : tier == "GOLD" ? new[] { "GOLD", "SILVER", "PRISM" }
                    : tier == "CURSE" ? new[] { "CURSE" }
                    : new[] { "SILVER", "GOLD" };
                string chosen = null; string chosenTier = tier;
                for (int t = 0; t < tierOrder.Length; t++)
                {
                    var pool = specByTier[tierOrder[t]].Where(id => !usedIds.Contains(id)).ToList();
                    if (pool.Count > 0) { chosen = rng.Pick(pool); chosenTier = tierOrder[t]; break; }
                }
                if (chosen == null) continue;
                usedIds.Add(chosen);
                var card = new PouchOfferCard { Type = PouchCardType.Special, Id = chosen, Tier = chosenTier };
                if (chosenTier == "CURSE") card.Free = true;
                else if (chosenTier == "PRISM") { card.RemoveN = 2; card.OrCurse = true; }
                else if (chosenTier == "GOLD") { card.RemoveN = 2; card.LowRemoveN = 1; }
                else card.RemoveN = 1;
                cards.Add(card);
            }
            cards.Add(new PouchOfferCard { Type = PouchCardType.Skip, Tier = "SILVER", CoinBonus = 5 });
            return cards;
        }

        // ── 기본 이득(cat=base && !harmful) 심볼 후보 — 교체 대상 목록. 웹 `Object.entries(pouch).filter(isAutoDecayTarget)`. ──
        private static List<(string id, int n)> BaseSymbolCandidates(IReadOnlyDictionary<string, int> pouch)
        {
            var list = new List<(string, int)>();
            foreach (var kv in pouch)
                if (kv.Value > 0 && Pouch.IsAutoDecayTarget(kv.Key)) list.Add((kv.Key, kv.Value));
            return list;
        }

        private static string Label(string id)
        {
            var s = Symbols.ById(id);
            return s != null ? s.emoji + s.name : id;
        }

        // 주머니 커밋(항상 새 draft로 교체) + 전공 발동/승급 감지 — 웹 `r.pouch = draft; this._checkArchetype();` 묶음.
        private static RunEvent CommitPouch(RunState run, IReadOnlyDictionary<string, int> draft)
        {
            run.Pouch.Clear();
            foreach (var kv in draft) if (kv.Value > 0) run.Pouch[kv.Key] = kv.Value;
            return DeepRunHooks.CheckArchetypeChange(run);
        }

        // node — Opus 2차검수(P7-3) 필수② 지원용 선택 인자. REST/GAMBLE 심화 경로처럼 결과 이벤트가
        // 특정 NodeKind에 귀속돼야 하는 호출부(StatTracker "gambles" 카운트·P4 RewardDone 문구 템플릿이
        // RunEvent.node를 읽는다)만 채워 넣는다 — 기본 null(POUCH/JACKPOT 2-step의 중간 커밋 이벤트처럼
        // 아직 특정 소비처가 없는 곳은 그대로 null 유지).
        private static List<RunEvent> Resolved(RunEvent archEv, long coinsDelta = 0, NodeKind? node = null)
        {
            var events = new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = node, coinsDelta = coinsDelta } };
            if (archEv != null) events.Add(archEv);
            return events;
        }

        // 커밋 성공 공통 처리 — deepPity 예약(add/upgrade 계열만, 웹 game.js:1904/1922/1954/1988/2007) +
        // deepStats.rewardsPicked 증가 + RewardFlow.Enter + 전공 이벤트 동반.
        private static List<RunEvent> CommitAndFinish(RunState run, IReadOnlyDictionary<string, int> draft, string pityId, string message, NodeKind? node = null)
        {
            var archEv = CommitPouch(run, draft);
            if (!string.IsNullOrEmpty(pityId)) run.DeepPity = new DeepPityState(pityId, 2);
            if (run.DeepStats != null) run.DeepStats.RewardsPicked += 1;
            RewardFlow.Enter(run, message);
            return Resolved(archEv, node: node);
        }

        // ══════════════════════════════════════════════════════════════════
        // POUCH / JACKPOT 노드 진입 — 오퍼 생성.
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> EnterPouchOffer(RunState run)
        {
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            bool useJtPrism = run.JackpotPrismPending; if (useJtPrism) run.JackpotPrismPending = false;
            bool useFeverPrism = run.FeverJackpotPrism; if (useFeverPrism) run.FeverJackpotPrism = false;
            bool useJpCrownPrism = run.JackpotCrownPending; if (useJpCrownPrism) run.JackpotCrownPending = false;

            var opts = new Options
            {
                // 웹 파리티 이전 단계(profile.symUnlocked, ACH_SYMBOL_UNLOCK 13종) — P7-1/P7-2가 "옮기지
                // 않음"으로 명시한 P7-4 범위다. 이번 슬라이스는 Pouch.DefaultUnlocked(58종 기본 해금)로
                // 근사한다 — 웹의 "unlockedSet 없으면 전체 허용" 폴백보다 더 정확한 근사(실제 웹도 해금
                // 전엔 58종만 통과)이므로 프로필 시스템이 붙기 전까지 합리적인 대체다. 최종 보고에 명시.
                SymUnlocked = Pouch.DefaultUnlocked,
                NoCurseAdds = sp.CurseChance < 0,
                LegendWeight = (int)sp.LegendWeight,
                ExtraCards = (int)(sp.RewardBonus + sp.AddBasicDelta),
                GoldBonus = sp.RareChance,
                ForcePrismFirst = useJtPrism || useFeverPrism || useJpCrownPrism,
            };
            var cards = OfferSymbolRewards(run.Rng, run.Pouch, run.Stage - 1, opts);
            run.PouchOptions.Clear();
            run.PouchOptions.AddRange(cards);
            run.Phase = RunPhase.EventPouch;
            return RunEvents.One(new RunEvent { type = "POUCH_OFFER", node = NodeKind.Pouch, pouchOptions = run.PouchOptions });
        }

        // 웹 §9.2 J3 JACKPOT 노드(game.js:1697-1733) — 현재 덱 최다 잭팟태그 기준 특수심볼 3택+스킵.
        // §2 교체 비용 플로우(POUCH_COST/POUCH_REMOVE)를 그대로 재사용한다(공짜·무검증 획득 금지).
        public static List<RunEvent> EnterJackpotNode(RunState run)
        {
            var jtagCount = new Dictionary<string, int>();
            foreach (var kv in run.Pouch)
            {
                if (kv.Value <= 0) continue;
                var t = Pouch.JackpotTagOf(kv.Key);
                if (t != null) jtagCount[t] = jtagCount.TryGetValue(t, out var c) ? c + kv.Value : kv.Value;
            }
            string topTag = null; int topTagN = 0;
            foreach (var kv in jtagCount) if (kv.Value > topTagN) { topTagN = kv.Value; topTag = kv.Key; }

            // 웹 `SYMS.filter(s => s.id && s.special && JACKPOT_TAG[s.id] === topTag)` — s.special 진리값
            // 기준이라 crown/coin(값심볼, special=NONE)은 제외되지만 그 자체가 Sp.COIN인 "coin"은 포함된다
            // (Pouch.CatOf("coin")=="base"인 것과는 별개 축 — 아래 필터는 Sp.NONE 여부만 본다).
            var jtAllSyms = topTag == null
                ? new List<SymInfo>()
                : Symbols.All.Where(s => !string.IsNullOrEmpty(s.id) && s.special != Sp.NONE && Pouch.JackpotTagOf(s.id) == topTag).ToList();
            var jtUnowned = jtAllSyms.Where(s => !(run.Pouch.TryGetValue(s.id, out var c) && c >= 1)).ToList();
            var jtCandPool = jtUnowned.Count >= 3 ? jtUnowned : jtAllSyms;
            var shuffled = new List<SymInfo>(jtCandPool);
            run.Rng.Shuffle(shuffled);
            var jtPicked = shuffled.Take(Math.Min(3, jtCandPool.Count)).ToList();

            if (jtPicked.Count == 0)
            {
                run.Coins += 5;
                RewardFlow.Enter(run, "잭팟 노드 — 해당 태그 심볼 없음, 코인 +5");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Jackpot, coinsDelta = 5 });
            }

            var cards = new List<PouchOfferCard>();
            foreach (var s in jtPicked)
            {
                var tier = Pouch.TierOf(s.id);
                var card = new PouchOfferCard { Type = PouchCardType.Special, Id = s.id, Tier = tier };
                if (tier == "CURSE") card.Free = true;
                else if (tier == "PRISM") { card.RemoveN = 2; card.OrCurse = true; }
                else if (tier == "GOLD") { card.RemoveN = 2; card.LowRemoveN = 1; }
                else card.RemoveN = 1;
                cards.Add(card);
            }
            cards.Add(new PouchOfferCard { Type = PouchCardType.Skip, Tier = "SILVER", CoinBonus = 5 });
            run.PouchOptions.Clear();
            run.PouchOptions.AddRange(cards);
            run.Phase = RunPhase.EventPouch;
            return RunEvents.One(new RunEvent { type = "POUCH_OFFER", node = NodeKind.Jackpot, pouchOptions = run.PouchOptions });
        }

        // ══════════════════════════════════════════════════════════════════
        // EventPouch — 오퍼 카드 픽(POUCH/JACKPOT 공용).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> PickCard(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventPouch) return RunEvents.Rejected("PHASE_NOT_EVENT_POUCH");
            if (index < 0 || index >= run.PouchOptions.Count) return RunEvents.Rejected("INVALID_INDEX");
            var card = run.PouchOptions[index];
            run.PouchOptions.Clear();

            if (card.Type == PouchCardType.Skip)
            {
                run.Coins += card.CoinBonus;
                if (run.DeepStats != null) run.DeepStats.RewardsPicked += 1;
                RewardFlow.Enter(run, "건너뛰기 — 코인 +5");
                return Resolved(null, card.CoinBonus);
            }

            string label = Label(card.Id);
            if (card.Tier == "CURSE")
            {
                var bounds = RepairShop.Bounds(run);
                var draft = new Dictionary<string, int>(run.Pouch);
                draft[card.Id] = (draft.TryGetValue(card.Id, out var c) ? c : 0) + 1;
                if (Pouch.Total(draft) <= bounds.totalMax)
                    return CommitAndFinish(run, draft, card.Id, $"{label} 저주 카드 추가 (무료)");
                RewardFlow.Enter(run, "저주 카드 추가 실패 (덱 만원)");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED" });
            }

            var baseSymbols = BaseSymbolCandidates(run.Pouch);
            if (baseSymbols.Count == 0)
            {
                var bounds = RepairShop.Bounds(run);
                var draft = new Dictionary<string, int>(run.Pouch);
                draft[card.Id] = (draft.TryGetValue(card.Id, out var c) ? c : 0) + 1;
                var pv = Pouch.Validate(draft, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                if (pv.Ok) return CommitAndFinish(run, draft, card.Id, $"{label} 추가 (기본 이득 없어 무료)");
                RewardFlow.Enter(run, $"{label} 추가 실패 (상한 초과)");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED" });
            }

            int baseTotal = baseSymbols.Sum(x => x.n);
            int removeN = card.Tier == "SILVER" ? 1
                : card.Tier == "GOLD" ? (baseTotal < 3 ? card.LowRemoveN : card.RemoveN)
                : card.RemoveN; // PRISM

            if (card.Tier == "PRISM")
            {
                run.PendingSpecial = new PouchPendingSpecial { Id = card.Id, Tier = card.Tier, RemoveN = removeN };
                run.Phase = RunPhase.EventPouchCost;
                return RunEvents.One(new RunEvent { type = "POUCH_COST_OFFER" });
            }

            run.PendingSpecial = new PouchPendingSpecial { Id = card.Id, Tier = card.Tier, RemoveN = removeN };
            run.RemoveCandidateIds.Clear();
            foreach (var (id, _) in baseSymbols) run.RemoveCandidateIds.Add(id);
            run.Phase = RunPhase.EventPouchRemove;
            return RunEvents.One(new RunEvent { type = "POUCH_REMOVE_OFFER", removeCandidateIds = run.RemoveCandidateIds });
        }

        // ══════════════════════════════════════════════════════════════════
        // EventPouchCost — 프리즘 전용 교체 비용 방식 선택. index 0 = "기본 심볼 2개 제거"(→
        // EventPouchRemove로 이어짐), index 1 = "저주 심볼 +1 추가"(즉시 커밋).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> PickCost(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventPouchCost) return RunEvents.Rejected("PHASE_NOT_EVENT_POUCH_COST");
            var ps = run.PendingSpecial;
            if (ps == null) return RunEvents.Rejected("NO_PENDING_SPECIAL");
            string label = Label(ps.Id);

            if (index == 0) // cost_remove
            {
                var baseSymbols = BaseSymbolCandidates(run.Pouch);
                if (baseSymbols.Count == 0)
                {
                    var draft = new Dictionary<string, int>(run.Pouch);
                    draft[ps.Id] = (draft.TryGetValue(ps.Id, out var c) ? c : 0) + 1;
                    run.PendingSpecial = null;
                    return CommitAndFinish(run, draft, ps.Id, $"{label} 획득! (기본 심볼 없어 무료 추가)");
                }
                ps.RemoveN = 2; // 웹 game.js:1910 `ps.removeN = 2`(고정, PRISM 원래 값과 동일).
                run.RemoveCandidateIds.Clear();
                foreach (var (id, _) in baseSymbols) run.RemoveCandidateIds.Add(id);
                run.Phase = RunPhase.EventPouchRemove;
                return RunEvents.One(new RunEvent { type = "POUCH_REMOVE_OFFER", removeCandidateIds = run.RemoveCandidateIds });
            }

            if (index == 1) // cost_curse
            {
                var bounds = RepairShop.Bounds(run);
                var draftCurse = new Dictionary<string, int>(run.Pouch);
                draftCurse["skull"] = (draftCurse.TryGetValue("skull", out var s) ? s : 0) + 1;
                draftCurse[ps.Id] = (draftCurse.TryGetValue(ps.Id, out var c2) ? c2 : 0) + 1;
                var pv = Pouch.Validate(draftCurse, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                run.PendingSpecial = null;
                if (pv.Ok) return CommitAndFinish(run, draftCurse, ps.Id, $"{label} 획득! (☠해골 +1 저주 경로)");
                RewardFlow.Enter(run, "저주 경로 총량 초과 — 교체 취소됨");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED" });
            }

            return RunEvents.Rejected("INVALID_INDEX");
        }

        // ══════════════════════════════════════════════════════════════════
        // EventPouchRemove — 제거할 기본 심볼 선택 + 원자적 커밋(검증 실패 시 롤백).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> PickRemove(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventPouchRemove) return RunEvents.Rejected("PHASE_NOT_EVENT_POUCH_REMOVE");
            var ps = run.PendingSpecial;
            if (ps == null) return RunEvents.Rejected("NO_PENDING_SPECIAL");
            if (index < 0 || index >= run.RemoveCandidateIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            string removeId = run.RemoveCandidateIds[index];
            run.RemoveCandidateIds.Clear();
            run.PendingSpecial = null;

            string label = Label(ps.Id);
            string remLabel = Label(removeId);
            var bounds = RepairShop.Bounds(run);
            var draft = new Dictionary<string, int>(run.Pouch);
            int remAmt = Math.Min(ps.RemoveN, draft.TryGetValue(removeId, out var c) ? c : 0);
            if (remAmt > 0)
            {
                draft[removeId] -= remAmt;
                if (draft[removeId] <= 0) draft.Remove(removeId);
            }
            draft[ps.Id] = (draft.TryGetValue(ps.Id, out var c2) ? c2 : 0) + 1;
            var pv = Pouch.Validate(draft, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
            if (pv.Ok) return CommitAndFinish(run, draft, ps.Id, $"{label} +1 / {remLabel} -{remAmt}");
            RewardFlow.Enter(run, "주머니 규칙 위반 — 교체 취소됨");
            return RunEvents.One(new RunEvent { type = "NODE_RESOLVED" });
        }

        // ══════════════════════════════════════════════════════════════════
        // REST 심화 2택 — 웹 game.js:1624-1634(진입)·1872-1889(픽).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> EnterRest(RunState run)
        {
            bool hasSkull = (run.Pouch.TryGetValue("skull", out var s1) ? s1 : 0) + (run.Pouch.TryGetValue("skull_black", out var s2) ? s2 : 0) > 0;
            if (!run.DeepMode || !hasSkull)
            {
                run.Coins += 12;
                RewardFlow.Enter(run, "휴식 — 코인 +12 획득");
                // Opus 2차검수(P7-3) 필수② — node=NodeKind.Rest 복구(1차 구현에서 NodeEvents.ChooseNode의
                // 기존 `case NodeKind.Rest: ... node = node, ...` 배선이 이 함수 호출로 대체되며 누락됐다
                // — 웹 파리티 P4가 이미 구축한 UI2/Run/RunView.cs:775 `switch (e.node) { case
                // NodeKind.Rest: ... }` 완료 화면 문구 템플릿이 이 필드에 의존한다).
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Rest, coinsDelta = 12 });
            }
            run.DeepChoiceIds.Clear();
            run.DeepChoiceIds.Add("rest_coin");
            run.DeepChoiceIds.Add("rest_purify");
            run.Phase = RunPhase.EventRestDeep;
            return RunEvents.One(new RunEvent { type = "DEEP_CHOICE_OFFER", node = NodeKind.Rest, deepChoiceIds = run.DeepChoiceIds });
        }

        public static List<RunEvent> PickRestDeep(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventRestDeep) return RunEvents.Rejected("PHASE_NOT_EVENT_REST_DEEP");
            if (index < 0 || index >= run.DeepChoiceIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            string id = run.DeepChoiceIds[index];
            run.DeepChoiceIds.Clear();

            if (id == "rest_coin")
            {
                run.Coins += 12;
                RewardFlow.Enter(run, "휴식 — 코인 +12 획득");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Rest, coinsDelta = 12 });
            }
            if (id == "rest_purify")
            {
                string target = (run.Pouch.TryGetValue("skull", out var sc) && sc > 0) ? "skull" : "skull_black";
                if (!(run.Pouch.TryGetValue(target, out var tc) && tc > 0))
                {
                    run.Coins += 12;
                    RewardFlow.Enter(run, "해골 없음 — 코인 +12 대신 지급");
                    return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Rest, coinsDelta = 12 });
                }
                var bounds = RepairShop.Bounds(run);
                var draft = new Dictionary<string, int>(run.Pouch);
                draft[target] -= 1; if (draft[target] <= 0) draft.Remove(target);
                var pv = Pouch.Validate(draft, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                if (pv.Ok)
                {
                    var archEv = CommitPouch(run, draft);
                    RewardFlow.Enter(run, $"정화 — {Label(target)} 1개 제거");
                    return Resolved(archEv, node: NodeKind.Rest);
                }
                run.Coins += 12;
                RewardFlow.Enter(run, "정화 실패(총량 하한) — 코인 +12 대신 지급");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Rest, coinsDelta = 12 });
            }
            return RunEvents.Rejected("UNKNOWN_CHOICE");
        }

        // ══════════════════════════════════════════════════════════════════
        // GAMBLE 심화 2택 — 웹 game.js:1636-1663(진입)·1847-1870(픽).
        // ══════════════════════════════════════════════════════════════════
        public static readonly string[] BaseFamilies = { "cherry", "book", "star", "gem", "coin", "flame", "magnet", "bomb" };

        public static List<RunEvent> EnterGamble(RunState run)
        {
            if (!run.DeepMode)
            {
                // 일반 런(비심화) — 기존 NodeEvents.ResolveGamble 그대로 재사용(무회귀, node/coinsDelta
                // 필드까지 정확히 보존).
                var ev = NodeEvents.ResolveGamble(run);
                RewardFlow.Enter(run, ev.gambleWon ? "도박 성공 — 코인 2배!" : "도박 실패 — 코인 유지");
                return RunEvents.One(ev);
            }
            var heldBase = BaseFamilies.Where(id => run.Pouch.TryGetValue(id, out var c) && c > 0).ToList();
            bool symGambleOk = heldBase.Count > 0;
            if (symGambleOk)
            {
                var bounds = RepairShop.Bounds(run);
                if (Pouch.Total(run.Pouch) >= bounds.totalMax) symGambleOk = false;
            }
            run.DeepChoiceIds.Clear();
            run.DeepChoiceIds.Add("gamble_coin");
            if (symGambleOk) run.DeepChoiceIds.Add("gamble_sym");
            run.Phase = RunPhase.EventGambleDeep;
            return RunEvents.One(new RunEvent { type = "DEEP_CHOICE_OFFER", node = NodeKind.Gamble, deepChoiceIds = run.DeepChoiceIds });
        }

        public static List<RunEvent> PickGambleDeep(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventGambleDeep) return RunEvents.Rejected("PHASE_NOT_EVENT_GAMBLE_DEEP");
            if (index < 0 || index >= run.DeepChoiceIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            string id = run.DeepChoiceIds[index];
            run.DeepChoiceIds.Clear();

            if (id == "gamble_coin")
            {
                // 웹 game.js:1848-1850 — 일반 GAMBLE(NodeEvents.ResolveGamble)과 달리 실패해도 코인을
                // 잃지 않는다("코인 유지"), coins<=0 방어 분기도 없다(웹 원문 그대로 전사).
                bool win = run.Rng.NextDouble() < 0.5;
                if (win) run.Coins *= 2;
                RewardFlow.Enter(run, win ? "도박 성공 — 코인 2배!" : "도박 실패 — 코인 유지");
                // Opus 2차검수(P7-3) 필수② — node=NodeKind.Gamble 설정. Profile/StatTracker.cs:264
                // `if (e.node == NodeKind.Gamble) p.Inc("gambles");`가 이 필드로 "gambles" 통계를
                // 집계한다 — 미설정 시 심화 도박은 이 카운터에서 누락된다(1차 구현 결손, 정정).
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble, gambleWon = win });
            }
            if (id == "gamble_sym")
            {
                var heldBase = BaseFamilies.Where(bid => run.Pouch.TryGetValue(bid, out var c) && c > 0).ToList();
                var bounds = RepairShop.Bounds(run);
                if (run.Rng.NextDouble() < 0.5 && heldBase.Count > 0)
                {
                    var chosen = run.Rng.Pick(heldBase);
                    var draft = new Dictionary<string, int>(run.Pouch);
                    draft[chosen] = (draft.TryGetValue(chosen, out var c2) ? c2 : 0) + 1;
                    var pv = Pouch.Validate(draft, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                    if (pv.Ok)
                    {
                        var archEv = CommitPouch(run, draft);
                        RewardFlow.Enter(run, $"심볼 도박 성공 — {Label(chosen)} +1");
                        return Resolved(archEv, node: NodeKind.Gamble);
                    }
                    RewardFlow.Enter(run, "심볼 도박 — 총량 초과로 실패");
                    return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble });
                }
                var draftSkull = new Dictionary<string, int>(run.Pouch);
                draftSkull["skull"] = (draftSkull.TryGetValue("skull", out var sc) ? sc : 0) + 1;
                var pvSkull = Pouch.Validate(draftSkull, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                if (pvSkull.Ok)
                {
                    var archEv = CommitPouch(run, draftSkull);
                    RewardFlow.Enter(run, "심볼 도박 실패 — ☠해골 +1");
                    return Resolved(archEv, node: NodeKind.Gamble);
                }
                RewardFlow.Enter(run, "심볼 도박 — 총량 초과로 해골 추가 불가");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble });
            }
            return RunEvents.Rejected("UNKNOWN_CHOICE");
        }

        // ══════════════════════════════════════════════════════════════════
        // 3스테이지 증강 연계 보너스 픽 — 오퍼 생성은 NodeEvents.PickOffer(isAugPick 성공 직후)가 담당.
        // 웹 game.js:1829-1844.
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> PickSynAugBonus(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventSynAugBonus) return RunEvents.Rejected("PHASE_NOT_EVENT_SYNAUG_BONUS");
            if (index < 0 || index >= run.PouchOptions.Count) return RunEvents.Rejected("INVALID_INDEX");
            var card = run.PouchOptions[index];
            run.PouchOptions.Clear();

            if (card.Type == PouchCardType.Skip)
            {
                run.Coins += card.CoinBonus;
                RewardFlow.Enter(run, "연계 보너스 건너뜀 — 코인 +5");
                return Resolved(null, card.CoinBonus);
            }
            var bounds = RepairShop.Bounds(run);
            var draft = new Dictionary<string, int>(run.Pouch);
            draft[card.Id] = (draft.TryGetValue(card.Id, out var c) ? c : 0) + 1;
            var pv = Pouch.Validate(draft, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
            if (pv.Ok)
            {
                var archEv = CommitPouch(run, draft);
                RewardFlow.Enter(run, $"연계 심볼 획득: {Label(card.Id)}!");
                return Resolved(archEv);
            }
            run.Coins += 5;
            RewardFlow.Enter(run, "덱 초과 — 코인 +5");
            return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", coinsDelta = 5 });
        }

        // ══════════════════════════════════════════════════════════════════
        // SYMAUG/SYMREL 노드 — 심볼증강/심볼유물 + 관련 일반 증강·유물(deepCompatPool) 혼합 오퍼.
        // 웹 game.js:1764-1791. 실제 그랜트(픽 확정)는 NodeEvents.PickOffer가 그대로 처리한다(Phase를
        // 일반 EventAugment/EventRelic과 공유 — RunState.Perks/PerkLevels가 sa_/sp_/sr_ id도 함께
        // 저장하는 구조라 안전, Content/SymPerks.cs 헤더 각주 참조).
        // ══════════════════════════════════════════════════════════════════
        private static Tier ParseTier(string t) => t switch { "GOLD" => Tier.GOLD, "PRISM" => Tier.PRISM, _ => Tier.SILVER };

        private static Perk WrapSymPerk(SymPerkDef sp, PCat cat) => new Perk
        {
            id = sp.id, name = sp.name, emoji = sp.emoji, desc = sp.desc,
            cat = cat, tier = ParseTier(sp.tier), price = 0, school = "", fx = null, unlockLevel = 0,
        };

        // 웹 `_ensureSymPerkCard` — 오퍼가 전부 일반 퍽이면 심볼퍽 풀에서 최소 1장 보장(동티어 우선).
        private static void EnsureSymPerkCard(RunState run, List<Perk> picks, IReadOnlyList<SymPerkDef> symPool, HashSet<string> held, string synPerkId)
        {
            if (picks.Count == 0 || picks.Any(pk => SymPerks.Get(pk.id) != null)) return;
            var inOffer = new HashSet<string>(picks.Select(pk => pk.id));
            var avail = symPool.Where(sp => !held.Contains(sp.id) && !inOffer.Contains(sp.id)).ToList();
            if (avail.Count == 0) return;
            Tier resultTier = picks[0].tier;
            var sameTier = avail.Where(sp => ParseTier(sp.tier) == resultTier).ToList();
            var chosen = run.Rng.Pick(sameTier.Count > 0 ? sameTier : avail);
            int idx = picks.FindIndex(pk => pk.id != synPerkId);
            if (idx < 0) idx = 0;
            picks[idx] = WrapSymPerk(chosen, picks[idx].cat);
        }

        public static List<RunEvent> EnterSymAugOrRel(RunState run, bool isAug, IReadOnlyDictionary<string, long> stat)
        {
            var symPool = isAug ? (IReadOnlyList<SymPerkDef>)SymPerks.Augments : SymPerks.Relics;
            var normalPool = isAug ? (IReadOnlyList<Perk>)Perks.Augments : Perks.Relics;
            var deepPool = DeepPerkMeta.DeepCompatPool(normalPool, run.Pouch);
            var combined = new List<Perk>();
            var cat = isAug ? PCat.AUGMENT : PCat.RELIC;
            foreach (var sp in symPool) combined.Add(WrapSymPerk(sp, cat));
            combined.AddRange(deepPool);
            var pool = Shop.GatedPool(combined, stat);

            int clearedStage = run.Stage - 1;
            bool bossClear = clearedStage > 0 && Formulas.IsBossStage(clearedStage);
            var held = new HashSet<string>(run.Perks);
            var baseTier = Formulas.TierForClearedStage(clearedStage);
            bool tierBumped = false;
            Tier nodeTier;
            if (run.Rng.Next(100) < 10)
            {
                var up = Formulas.TierUp(baseTier);
                if (up != baseTier) { tierBumped = true; nodeTier = up; } else nodeTier = baseTier;
            }
            else nodeTier = baseTier;

            // 프리즘잉크 — AUGMENT 전용, 웹 game.js:1771/1775. 결과 티어가 실제 PRISM이 아니면(폴백)
            // 잉크 보존(안전 폴백) — 성공(PRISM 확정)했을 때만 소비.
            bool prismInkActive = isAug && run.PrismInkActive;
            if (prismInkActive) { nodeTier = Tier.PRISM; tierBumped = nodeTier != baseTier; }

            bool lucky = run.UnluckyGauge >= Formulas.UNLUCKY_MAX;
            if (lucky && nodeTier == Tier.SILVER) { nodeTier = Tier.GOLD; tierBumped = true; }

            string favCat = isAug ? Shop.MajorFavoredCat(run) : null;
            var picks = Shop.PickPerksByTier(run.Rng, pool, held, nodeTier, bossClear, favCat);

            // Opus 2차검수(P7-3) 필수 — 프리즘잉크 소비 판정은 "실제 결과 티어"를 봐야 한다: picks가
            // 비어있으면 웹 offerPerks의 조기반환(meta.tier=nodeTier, 아래 참조)과 동치이므로 요청
            // 티어(nodeTier) 기준으로, picks가 있으면 실제로 뽑힌 티어(picks[0].tier — 폴백으로 낮아질
            // 수 있음) 기준으로 판정한다.
            if (prismInkActive)
            {
                var resultTierForInk = picks.Count > 0 ? picks[0].tier : nodeTier;
                run.PrismInkActive = resultTierForInk != Tier.PRISM;
            }

            NodeKind thisNode = isAug ? NodeKind.SymAug : NodeKind.SymRel;

            // Opus 2차검수(P7-3) 필수(항목5a) — 웹 engine.js:1258 `if (!picks.length) return { options: [],
            // meta: {...} };` 그대로 조기반환. NodeEvents.OfferPerks의 기존 `if (picks.Count == 0) return
            // picks;` 관례와 동일 위치(세트시너지 5% 롤·lucky 리셋·EnsureSymPerkCard *이전*) — 후보가
            // 없으면 이 셋 다 건드리지 않는다(특히 세트시너지 5% 롤은 RNG를 소비하므로, 조기반환 없이
            // `picks.Count>=2`만으로 가드하면 picks가 비어도 `rng.Next(100)`이 먼저 평가돼 웹에 없는
            // RNG 소비가 생긴다 — `&&` 좌변 우선 평가 때문).
            if (picks.Count == 0)
            {
                run.PerkOfferIds.Clear();
                RewardFlow.Enter(run, isAug ? "이번엔 새 심볼 증강이 없어요 — 다음 스테이지로!" : "이번엔 새 심볼 유물이 없어요 — 다음 스테이지로!");
                return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = thisNode });
            }

            string synPerkId = null;
            if (run.Rng.Next(100) < 5 && picks.Count >= 2)
            {
                var excludeSet = new HashSet<string>(picks.Select(pk => pk.id));
                excludeSet.UnionWith(held);
                var syn = Shop.SetSynergyAug(held, excludeSet, run.Rng, PCat.AUGMENT);
                // Opus 2차검수(P7-3) 필수(항목1) — 웹 engine.js:1265-1267·game.js:1773/1786
                // `opts.compatFilter` 그대로: 주입 후보가 심볼퍽(SYM_PERK_BY_ID)이거나 deepCompatPool
                // 통과(dSym 참조 계열 보유수 충족) 조건을 만족할 때만 주입한다 — 미통과면 주입 취소하고
                // 원래 마지막 칸을 그대로 유지한다(일반 AUGMENT/RELIC의 offerPerks는 compatFilter가
                // 없어 이 검사를 안 하지만, SYMAUG/SYMREL은 "심화 관련성 있는 후보만" 원칙이 오퍼
                // 전체에 적용돼야 하므로 시너지 주입도 예외가 아니다).
                bool synCompat = syn != null && (SymPerks.Get(syn.id) != null || DeepPerkMeta.IsDeepCompat(syn, run.Pouch));
                if (synCompat && !picks.Any(pk => pk.id == syn.id))
                {
                    picks = picks.Take(picks.Count - 1).Append(syn).ToList();
                    synPerkId = syn.id;
                }
            }

            if (lucky) run.UnluckyGauge = 0;

            EnsureSymPerkCard(run, picks, symPool, held, synPerkId);

            run.PerkOfferIds.Clear();
            run.PerkOfferIds.AddRange(picks.Select(pk => pk.id));
            run.Phase = isAug ? RunPhase.EventAugment : RunPhase.EventRelic;
            return RunEvents.One(new RunEvent
            {
                type = "PERK_OFFER", node = thisNode, perkOfferIds = run.PerkOfferIds, offerTier = picks[0].tier,
                offerBossPrism = bossClear, offerTierBumped = tierBumped, offerSynergyPerkId = synPerkId,
            });
        }
    }
}
