using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // NODE_SELECT 3택 진행 + 증강/유물 노드 + REST/GAMBLE/EVENT/CURSE/RISK 인라인 해결 + EVENT_SHOP 진입/이탈.
    // Kotlin SlotV2Service의 handleNodeSelect(L1125-1246)/offerPerks(L1257-1316)/handleHoldAug(L1336-1351)/
    // handleRetake(L1354-1369)/handlePerkPick(L1371-1388)를 전사. 02_service.md §5 그대로.
    public static class NodeEvents
    {
        // ══════════════════════════════════════════════════════════════════
        // ChooseNode — NODE_SELECT의 3택 중 하나 선택. AUGMENT/RELIC/CURSE/RISK는 풀 소진 시
        // EVENT 10종 랜덤표로 폴백한다(§5 표 각주, Kotlin이 CURSE/RISK/AUGMENT/RELIC 모두 이 폴백 경로를
        // 공유 — when(node)의 else 분기가 곧 EVENT 테이블).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> ChooseNode(RunState run, int index, IReadOnlyDictionary<string, long> stat)
        {
            if (run.Phase != RunPhase.NodeSelect) return RunEvents.Rejected("PHASE_NOT_NODE_SELECT");
            if (index < 0 || index >= run.NodeOptions.Count) return RunEvents.Rejected("INVALID_INDEX");
            var node = run.NodeOptions[index];
            run.NodeOptions.Clear();

            switch (node)
            {
                case NodeKind.Augment:
                case NodeKind.Relic:
                {
                    var picks = OfferPerks(run, node, stat, reoffer: false, out bool bossClear, out bool tierBumped, out string synPerkId, out bool heldInc);
                    if (picks.Count > 0)
                    {
                        run.PerkOfferIds.Clear();
                        run.PerkOfferIds.AddRange(picks.Select(p => p.id));
                        run.Phase = node == NodeKind.Augment ? RunPhase.EventAugment : RunPhase.EventRelic;
                        return RunEvents.One(new RunEvent
                        {
                            type = "PERK_OFFER", node = node, perkOfferIds = run.PerkOfferIds,
                            offerTier = picks[0].tier, offerBossPrism = bossClear, offerTierBumped = tierBumped,
                            offerSynergyPerkId = synPerkId, offerHeldIncluded = heldInc,
                        });
                    }
                    break; // 풀 소진 → EVENT 테이블 폴백
                }
                case NodeKind.Curse:
                {
                    var ev = TryGrantCurse(run);
                    if (ev != null) return RunEvents.One(ev);
                    break;
                }
                case NodeKind.Risk:
                {
                    var ev = TryGrantRisk(run, stat);
                    if (ev != null) return RunEvents.One(ev);
                    break;
                }
                case NodeKind.Shop:
                {
                    var offer = Shop.FreshOffer(run, stat);
                    run.ShopOffer.Clear();
                    run.ShopOffer.AddRange(offer);
                    run.Phase = RunPhase.EventShop;
                    return RunEvents.One(new RunEvent { type = "SHOP_OFFER", node = node, shopOffer = run.ShopOffer });
                }
                case NodeKind.Rest:
                {
                    run.Coins += 8;
                    run.Phase = RunPhase.Spin;
                    return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = node, coinsDelta = 8 });
                }
                case NodeKind.Gamble:
                {
                    var ev = ResolveGamble(run);
                    run.Phase = RunPhase.Spin;
                    return RunEvents.One(ev);
                }
                case NodeKind.Event:
                    break; // 바로 아래 공용 EVENT 테이블로
            }

            var evEvent = ResolveEventTable(run, stat);
            run.Phase = RunPhase.Spin;
            return RunEvents.One(evEvent);
        }

        // ── 증강/유물 오퍼 생성 (offerPerks, Kotlin L1257-1316) ──
        // dev_syllabus 정보성 힌트는 S6 UI로 이관(스코프 제외, Fable 승인 2026-07-31). 세트 시너지 5%
        // off-tier 주입(Kotlin L1281-1295)은 이식했다 — synPerkId(out)로 주입된 퍽 id를 노출한다(주입 없으면
        // null).
        private static List<Perk> OfferPerks(
            RunState run, NodeKind node, IReadOnlyDictionary<string, long> stat, bool reoffer,
            out bool bossClear, out bool tierBumped, out string synPerkId, out bool heldIncluded)
        {
            synPerkId = null;
            heldIncluded = false;
            var held = new HashSet<string>(run.Perks);
            IReadOnlyList<Perk> pool = node == NodeKind.Augment ? (IReadOnlyList<Perk>)Perks.Augments : Perks.Relics;
            string favCat = node == NodeKind.Augment ? Shop.MajorFavoredCat(run) : null;

            string heldAugId = null;
            if (node == NodeKind.Augment && !reoffer && !string.IsNullOrEmpty(run.HeldAug) && !held.Contains(run.HeldAug))
                heldAugId = run.HeldAug;

            int clearedStage = run.Stage - 1;
            bossClear = Formulas.IsBossStage(clearedStage);
            var heldPerk = heldAugId != null ? Perks.ById(heldAugId) : null;
            var baseTier = Formulas.TierForClearedStage(clearedStage);
            tierBumped = false;
            Tier nodeTier;
            if (heldPerk != null)
            {
                nodeTier = heldPerk.tier; // 🗂️보류파일 — 보류 티어 우선(결정형/등급업 무시)
            }
            else if (run.Rng.Next(100) < 10) // 10% "행운! 등급업"
            {
                var up = Formulas.TierUp(baseTier);
                if (up != baseTier) { tierBumped = true; nodeTier = up; } else nodeTier = baseTier;
            }
            else
            {
                nodeTier = baseTier;
            }

            bool lucky = run.UnluckyGauge >= Formulas.UNLUCKY_MAX;
            var picks = Shop.PickPerksByTier(run.Rng, pool, run.Stage, held, lucky, favCat, stat, bossClear, nodeTier);
            if (heldPerk != null)
            {
                picks = new[] { heldPerk }.Concat(picks.Where(p => p.id != heldPerk.id)).Take(3).ToList();
                heldIncluded = true; // 0번 칸 = 보류분 (원본 배너 "🗂️ 보류 후보 포함!" 대응 신호)
            }
            if (picks.Count == 0) return picks;

            // 🧩 세트 시너지 off-tier 조각 주입 (Kotlin offerPerks L1281-1295) — 보류파일(heldPerk) 미사용
            // 시에만, 5% 확률로 마지막 칸을 "짓는 중인 세트의 빠진 조각"으로 교체(메인 티어와 다를 수
            // 있음 — 세트 완성 유도가 목적). RNG 소비 순서 주의: heldPerk==null이면 이 100-roll은
            // "picks.Count>=2인지"·"syn이 실제로 발견되는지"와 무관하게 항상 1회 소비된다(Kotlin과 동일 —
            // r.nextInt(100)이 조건식 안에 있어 heldPerk==null이기만 하면 매번 평가됨).
            if (heldPerk == null && run.Rng.Next(100) < 5)
            {
                var synCat = node == NodeKind.Augment ? PCat.AUGMENT : PCat.RELIC;
                var excludeSet = new HashSet<string>(picks.Select(p => p.id));
                excludeSet.UnionWith(held);
                var syn = Shop.SetSynergyAug(held, excludeSet, run.Rng, synCat);
                if (syn != null && picks.Count >= 2 && !picks.Any(p => p.id == syn.id))
                {
                    picks = picks.Take(picks.Count - 1).Append(syn).ToList(); // 항상 마지막 칸 교체(메인 티어 칸 보존)
                    synPerkId = syn.id;
                }
            }

            if (lucky && !reoffer) run.UnluckyGauge = 0;
            if (heldAugId != null) run.HeldAug = "";

            return picks;
        }

        // ── CURSE 노드 (Kotlin L1141-1157) ──
        private static RunEvent TryGrantCurse(RunState run)
        {
            var held = new HashSet<string>(run.Curses);
            var avail = Perks.Curses.Where(c => !held.Contains(c.id)).ToList();
            var curse = run.Rng.PickOrDefault(avail);
            if (curse == null) return null;
            run.Curses.Add(curse.id);
            run.Coins += 15;
            run.Phase = RunPhase.Spin;
            return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Curse, curseGrantedId = curse.id, coinsDelta = 15 };
        }

        // ── RISK 노드 (Kotlin L1160-1174) — 프리즘 증강(소진 시 골드 폴백) + 저주 동시 지급 ──
        private static RunEvent TryGrantRisk(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            var heldP = new HashSet<string>(run.Perks);
            var heldC = new HashSet<string>(run.Curses);
            var augPool = Shop.UnlockedPerks(Perks.Augments, stat);
            var aug = run.Rng.PickOrDefault(augPool.Where(p => p.tier == Tier.PRISM && !heldP.Contains(p.id)).ToList())
                      ?? run.Rng.PickOrDefault(augPool.Where(p => p.tier == Tier.GOLD && !heldP.Contains(p.id)).ToList());
            var curse = run.Rng.PickOrDefault(Perks.Curses.Where(c => !heldC.Contains(c.id)).ToList());
            if (aug == null || curse == null) return null;
            run.Perks.Add(aug.id);
            run.Curses.Add(curse.id);
            run.Phase = RunPhase.Spin;
            return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Risk, augmentGrantedId = aug.id, curseGrantedId = curse.id };
        }

        // ── GAMBLE 노드 (Kotlin L1193-1197) ──
        private static RunEvent ResolveGamble(RunState run)
        {
            if (run.Coins <= 0)
                return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble, gambleWon = false, coinsDelta = 0 };
            if (run.Rng.Next(2) == 0) // 50/50 — 원본 nextBoolean()과 비트열 일치는 불필요(설계 원칙 2)
            {
                long gained = run.Coins;
                run.Coins *= 2;
                return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble, gambleWon = true, coinsDelta = gained };
            }
            long lost = run.Coins;
            run.Coins = 0;
            return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Gamble, gambleWon = false, coinsDelta = -lost };
        }

        // ── EVENT 10종 랜덤표 (Kotlin L1198-1233, §5-A) — AUGMENT/RELIC/CURSE/RISK 풀 소진 폴백 겸용 ──
        private static RunEvent ResolveEventTable(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            int roll = run.Rng.Next(10);
            var ev = new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, eventRoll = roll };
            switch (roll)
            {
                case 0: run.Coins += 15; ev.coinsDelta = 15; break;
                case 1: run.Score += 200; ev.scoreDelta = 200; break;
                case 2: run.Coins += 30; ev.coinsDelta = 30; break;
                case 3: run.Score += 100; run.Coins += 12; ev.scoreDelta = 100; ev.coinsDelta = 12; break;
                case 4: run.StageBonusSpins += 1; ev.bonusSpinsDelta = 1; break;
                case 5:
                {
                    var nextspin = Items.All.Where(i => i.kind == "NEXTSPIN").ToList();
                    var gift = run.Rng.PickOrDefault(nextspin);
                    if (gift != null) { run.ArmItems.Add(gift.id); ev.itemGrantedId = gift.id; }
                    else { run.Coins += 15; ev.coinsDelta = 15; }
                    break;
                }
                case 6: run.Coins += 15; ev.coinsDelta = 15; break; // 구버전 장치드롭 자리, 코인으로 대체(레거시)
                case 7:
                {
                    var held = new HashSet<string>(run.Perks);
                    var pool = Shop.UnlockedPerks(Perks.Relics, stat).Where(p => !held.Contains(p.id)).ToList();
                    var relic = run.Rng.PickOrDefault(pool);
                    if (relic != null) { run.Perks.Add(relic.id); ev.relicGrantedId = relic.id; }
                    else { run.Coins += 25; ev.coinsDelta = 25; }
                    break;
                }
                case 8:
                {
                    var held = new HashSet<string>(run.Perks);
                    var augPool = Shop.UnlockedPerks(Perks.Augments, stat).Where(p => !held.Contains(p.id)).ToList();
                    var aug = run.Rng.PickOrDefault(augPool);
                    if (aug != null)
                    {
                        if (run.Rng.Next(4) == 0) // 25% "🎉특별 이벤트"
                        {
                            run.Perks.Add(aug.id);
                            run.Coins += 10;
                            ev.augmentGrantedId = aug.id;
                            ev.coinsDelta = 10;
                            var held2 = new HashSet<string>(run.Perks);
                            var relicPool = Shop.UnlockedPerks(Perks.Relics, stat).Where(p => !held2.Contains(p.id)).ToList();
                            var relic2 = run.Rng.PickOrDefault(relicPool);
                            if (relic2 != null) { run.Perks.Add(relic2.id); ev.relicGrantedId = relic2.id; }
                        }
                        else
                        {
                            run.Perks.Add(aug.id);
                            ev.augmentGrantedId = aug.id;
                        }
                    }
                    else
                    {
                        run.Coins += 25;
                        ev.coinsDelta = 25;
                    }
                    break;
                }
                default: // 9 — 정화의 샘 / 꽝
                {
                    if (run.Curses.Count > 0)
                    {
                        var removed = run.Rng.Pick(run.Curses);
                        run.Curses.Remove(removed);
                        ev.curseRemovedId = removed;
                    }
                    else
                    {
                        run.Coins += 10;
                        ev.coinsDelta = 10;
                    }
                    break;
                }
            }
            return ev;
        }

        // ── EVENT_AUGMENT/EVENT_RELIC 후보 선택 (handlePerkPick, Kotlin L1371-1388) ──
        public static List<RunEvent> PickOffer(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventAugment && run.Phase != RunPhase.EventRelic) return RunEvents.Rejected("PHASE_NOT_PERK_OFFER");
            if (index < 0 || index >= run.PerkOfferIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            var perkId = run.PerkOfferIds[index];
            run.Perks.Add(perkId);
            run.PerkOfferIds.Clear();
            run.Phase = RunPhase.Spin;
            return RunEvents.One(new RunEvent { type = "PERK_GRANTED", perkId = perkId });
        }

        // ── 🗂️보류파일 (handleHoldAug, Kotlin L1336-1351) — EVENT_AUGMENT 전용 ──
        public static List<RunEvent> HoldAugment(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventAugment) return RunEvents.Rejected("PHASE_NOT_EVENT_AUGMENT");
            if (!Shop.HasDevice(run, "dev_holdfile")) return RunEvents.Rejected("DEVICE_NOT_EQUIPPED");
            if (!string.IsNullOrEmpty(run.HeldAug)) return RunEvents.Rejected("ALREADY_HOLDING");
            if (index < 0 || index >= run.PerkOfferIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            var perkId = run.PerkOfferIds[index];
            run.HeldAug = perkId;
            run.PerkOfferIds.Clear();
            run.Phase = RunPhase.Spin;
            return RunEvents.One(new RunEvent { type = "PERK_HELD", perkId = perkId });
        }

        // ── 🔁재추첨 (handleRetake, Kotlin L1354-1369) — EVENT_AUGMENT/EVENT_RELIC 둘 다 동작
        // (원본 동작 유지 — UI 힌트만 증강 노드 전용이던 비대칭은 텍스트 조립을 안 하므로 재현 불필요). ──
        public static List<RunEvent> Retake(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            if (run.Phase != RunPhase.EventAugment && run.Phase != RunPhase.EventRelic) return RunEvents.Rejected("PHASE_NOT_PERK_OFFER");
            if (!Shop.HasDevice(run, "dev_retake")) return RunEvents.Rejected("DEVICE_NOT_EQUIPPED");
            if (run.UsedCmds.Contains("RETAKE")) return RunEvents.Rejected("ALREADY_USED");
            if (run.Coins < Formulas.RETAKE_COIN_COST) return RunEvents.Rejected("INSUFFICIENT_COINS");

            run.Coins -= Formulas.RETAKE_COIN_COST;
            run.UsedCmds.Add("RETAKE"); // 스테이지당 1회(클리어 시 리셋)
            var node = run.Phase == RunPhase.EventAugment ? NodeKind.Augment : NodeKind.Relic;
            var picks = OfferPerks(run, node, stat, reoffer: true, out bool bossClear, out bool tierBumped, out string synPerkId, out _);
            if (picks.Count == 0)
            {
                // Kotlin L1364-1369: spent 사본은 offerPerks가 null이면 upsert되지 않고 버려진다
                // → 코인·RETAKE 마커 원복(재추첨 기회 유지). 차감 확정은 오퍼 성공 시에만.
                run.Coins += Formulas.RETAKE_COIN_COST;
                run.UsedCmds.Remove("RETAKE");
                return RunEvents.One(new RunEvent { type = "RETAKE_EMPTY", node = node, perkOfferIds = run.PerkOfferIds });
            }

            run.PerkOfferIds.Clear();
            run.PerkOfferIds.AddRange(picks.Select(p => p.id));
            return RunEvents.One(new RunEvent
            {
                type = "PERK_OFFER", node = node, perkOfferIds = run.PerkOfferIds,
                offerTier = picks[0].tier, offerBossPrism = bossClear, offerTierBumped = tierBumped,
                offerSynergyPerkId = synPerkId,
            });
        }
    }
}
