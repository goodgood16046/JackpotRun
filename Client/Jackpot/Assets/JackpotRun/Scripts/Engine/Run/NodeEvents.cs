using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // NODE_SELECT 3택 진행 + 증강/유물 노드 + REST/GAMBLE/EVENT/CURSE/RISK 인라인 해결 + EVENT_SHOP 진입/이탈.
    // Kotlin SlotV2Service의 handleNodeSelect(L1125-1246)/offerPerks(L1257-1316)/handleHoldAug(L1336-1351)/
    // handleRetake(L1354-1369)/handlePerkPick(L1371-1388)를 전사. 02_service.md §5 그대로.
    public static class NodeEvents
    {
        // ── WEB_PARITY P1 ④ Opus 1차검수 수정③(2026-08-07) — 장치 무작위 추첨(rare 가중) ──────────
        // 웹 engine.js pickDevices(rng,stage,owned,n=1)(±L1296-1309)의 rare/non-rare 분리 로직을
        // 단일 추첨(n=1, 이 엔진의 두 실사용처 EVENT-6·보스드랍 모두 n=1)에 맞게 이식:
        //   rareChance = min(0.6, 0.15+stage*0.03) 확률로 rare 풀에서, 아니면 non-rare 풀에서 우선 시도
        //   → 그 등급의 미보유 후보가 없으면 등급 무관 미보유 전체로 폴백.
        // [WEB_PARITY_DESIGN.md §2-F 결정 — 웹과 의도적 차이] 웹 원문의 세 번째 폴백(그래도 없으면 owned
        // 포함 전체 base에서 뽑아 허탕 나는 경우까지 허용)은 두 호출부(game.js:1438 보스드랍·2292
        // EVENT-6) 모두 owned 인자에 실제 ownedDevices가 아니라 curses 집합을 잘못 넘기는 버그성
        // 코드라 사실상 이 세 번째 폴백에 거의 도달하지 않는다 — Unity는 이 버그를 재현하지 않고,
        // "미보유 후보가 하나도 없으면 null"로 끝내 호출측이 코인 폴백을 쓰게 한다(기존 P1 설계 그대로,
        // 웹 회귀버그 예외 조항 적용).
        internal static DeviceDef PickDevice(Rng rng, int stage, IReadOnlyCollection<string> owned)
        {
            double rareChance = Math.Min(0.6, 0.15 + stage * 0.03);
            bool wantRare = rng.NextDouble() < rareChance;
            var pool = Devices.All.Where(d => d.rare == wantRare && !owned.Contains(d.id)).ToList();
            if (pool.Count == 0) pool = Devices.All.Where(d => !owned.Contains(d.id)).ToList();
            return pool.Count > 0 ? rng.Pick(pool) : null;
        }

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
                // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — AUGLEVEL 노드: 레벨업 가능한 보유
                // 증강(AugLevels.LevelableHeld) 전부를 후보로 오퍼한다(웹 game.js:1622 `r.options =
                // this._levelableHeld().map(...)` — 웹은 전량 오퍼, CSS 그리드가 알아서 줄바꿈).
                // Opus 1차검수 필수(2026-08-08, §2-(M) 결정) — Unity `PerkOfferPanel`은 320px 고정
                // 카드 3장 전용 레이아웃이라 4장 이상이면 화면 밖으로 잘린다. 오퍼를 최대 3장으로
                // 캡한다 — 3장 이하면 전량 그대로(RNG 미소비, 기존 시드 스트림 영향 없음), 4장
                // 이상이면 `run.Rng.Shuffle`로 등록 순서 편향 없이 섞은 뒤 앞 3장만 선발한다
                // (`RollNextNodes`의 "풀 셔플 후 GetRange" 관례와 동일 패턴 — RNG 소비는 4장 이상일
                // 때만 발생). PickOffer가 EventAugLevel phase에서 perkId를 "새로 획득"이 아니라
                // PerkLevels[id]+1로 해석한다.
                case NodeKind.AugLevel:
                {
                    var candidates = AugLevels.LevelableHeld(run);
                    if (candidates.Count > 0)
                    {
                        var offerIds = candidates;
                        if (candidates.Count > 3)
                        {
                            offerIds = new List<string>(candidates);
                            run.Rng.Shuffle(offerIds);
                            offerIds = offerIds.GetRange(0, 3);
                        }
                        run.PerkOfferIds.Clear();
                        run.PerkOfferIds.AddRange(offerIds);
                        run.Phase = RunPhase.EventAugLevel;
                        return RunEvents.One(new RunEvent { type = "PERK_OFFER", node = node, perkOfferIds = run.PerkOfferIds });
                    }
                    // 이론상 도달 불가 — StageFlow.ClearStage가 후보 있을 때만 이 노드를 생성하고, 노드
                    // 롤과 선택 사이에 후보를 바꿀 수단이 없다(웹도 동일 전제, game.js:1622 else 분기가
                    // EVENT 테이블이 아니라 "강화할 증강이 없어요" 무보상 종료다 — AUGMENT/RELIC 풀
                    // 소진 폴백과는 다른 케이스라 아래 공용 EVENT 테이블로 떨어뜨리지 않는다).
                    // 웹 파리티 P4 — 이 무보상 종료도 웹처럼 REWARD_DONE을 거친다(웹 game.js:1622
                    // `this._enterRewardDone("강화할 증강이 없어요")` 그대로).
                    RewardFlow.Enter(run, "강화할 증강이 없어요");
                    return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = node });
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
                    // 웹 파리티 P4 — 웹 game.js:2305 `r.shopBought = []`(상점 진입 시 구매 이력 리셋).
                    // Shop.Leave가 REWARD_DONE 메시지 조립에 쓴다.
                    run.ShopBoughtLabels.Clear();
                    run.Phase = RunPhase.EventShop;
                    return RunEvents.One(new RunEvent { type = "SHOP_OFFER", node = node, shopOffer = run.ShopOffer });
                }
                case NodeKind.Rest:
                {
                    // WEB_PARITY P1 ④: 코인 8 → 12(웹 game.js:1633 "코인 +12").
                    run.Coins += 12;
                    // 웹 파리티 P4 — 웹 game.js:1633 `this._enterRewardDone("🛌 휴식 — 코인 +12 획득")`.
                    RewardFlow.Enter(run, "휴식 — 코인 +12 획득");
                    return RunEvents.One(new RunEvent { type = "NODE_RESOLVED", node = node, coinsDelta = 12 });
                }
                case NodeKind.Gamble:
                {
                    var ev = ResolveGamble(run);
                    // 웹 파리티 P4 — 웹 game.js:1849-1850 "도박 성공 — 코인 2배!" / "도박 실패 — 코인 유지".
                    RewardFlow.Enter(run, ev.gambleWon ? "도박 성공 — 코인 2배!" : "도박 실패 — 코인 유지");
                    return RunEvents.One(ev);
                }
                case NodeKind.Event:
                    break; // 바로 아래 공용 EVENT 테이블로
                // WEB_PARITY P1 ④: DEVICE 노드 — 오퍼 확정은 TakeDevice(RunController.Do)가 담당.
                // PendingDeviceDrop이 비어 있으면(이론상 도달 불가 — RollNextNodes가 드랍이 있을 때만
                // 이 노드를 얹는다, StageFlow.ClearStage 참조) 방어적으로 EVENT 폴백.
                case NodeKind.Device:
                {
                    if (!string.IsNullOrEmpty(run.PendingDeviceDrop))
                    {
                        run.Phase = RunPhase.DeviceNode;
                        return RunEvents.One(new RunEvent { type = "DEVICE_OFFER", node = node, deviceId = run.PendingDeviceDrop });
                    }
                    break;
                }
            }

            var evEvent = ResolveEventTable(run, stat);
            // 웹 파리티 P4 — EVENT 10종 표(및 AUGMENT/RELIC/CURSE/RISK 풀 소진 폴백 공유 경로) 결과도
            // REWARD_DONE을 거친다(웹 game.js:2299 `this._enterRewardDone(msg)`). Unity의 실제 지급
            // 내역(case4=스테이지 스핀+1, case6=장치 획득 등, §1-A #4/(F) 결정으로 웹과 이미 갈라진 값)을
            // 그대로 문구화한다 — 웹 리터럴 문자열을 그대로 베끼면 실제 지급과 어긋나므로 RunEvent
            // 필드에서 재구성한다(EventRewardMessage).
            RewardFlow.Enter(run, EventRewardMessage(evEvent));
            return RunEvents.One(evEvent);
        }

        // ResolveEventTable이 채운 RunEvent 필드로부터 REWARD_DONE 메시지를 조립한다 — RunView.
        // EventTableText(UI 로그용, astral 이모지 포함)와 같은 데이터 소스를 쓰지만 이쪽은 표시 계층
        // TextSanitize 없이도 안전하도록 이모지를 쓰지 않는다(엔진 산출 문자열 규약).
        private static string EventRewardMessage(RunEvent ev)
        {
            var parts = new List<string>();
            if (ev.coinsDelta != 0) parts.Add($"코인{(ev.coinsDelta > 0 ? "+" : "")}{ev.coinsDelta}");
            if (ev.scoreDelta != 0) parts.Add($"점수{(ev.scoreDelta > 0 ? "+" : "")}{ev.scoreDelta}");
            if (ev.bonusSpinsDelta != 0) parts.Add($"스테이지 스핀+{ev.bonusSpinsDelta}");
            if (!string.IsNullOrEmpty(ev.itemGrantedId)) parts.Add($"아이템 {NameOf(Items.ById(ev.itemGrantedId)?.name, ev.itemGrantedId)}");
            if (!string.IsNullOrEmpty(ev.relicGrantedId)) parts.Add($"유물 {NameOf(Perks.ById(ev.relicGrantedId)?.name, ev.relicGrantedId)}");
            if (!string.IsNullOrEmpty(ev.augmentGrantedId)) parts.Add($"증강 {NameOf(Perks.ById(ev.augmentGrantedId)?.name, ev.augmentGrantedId)}");
            if (!string.IsNullOrEmpty(ev.curseRemovedId)) parts.Add($"정화: {NameOf(Perks.ById(ev.curseRemovedId)?.name, ev.curseRemovedId)} 제거");
            if (!string.IsNullOrEmpty(ev.deviceGrantedId)) parts.Add($"장치 {NameOf(Devices.ById(ev.deviceGrantedId)?.name, ev.deviceGrantedId)}");
            string body = parts.Count > 0 ? string.Join(" · ", parts) : "보상 없음";
            return "이벤트 — " + body;
        }

        private static string NameOf(string name, string fallbackId) => !string.IsNullOrEmpty(name) ? name : fallbackId;

        // ── 증강/유물 오퍼 생성 (offerPerks, 웹 engine.js:1248-1284 offerPerks 리터럴 포팅 — 웹 파리티
        // P3.5, WEB_PARITY_DESIGN.md §2-(T) 후속①②) ──
        // dev_syllabus 정보성 힌트는 S6 UI로 이관(스코프 제외, Fable 승인 2026-07-31). 세트 시너지 5%
        // off-tier 주입(engine.js:1260-1271)은 이식했다 — synPerkId(out)로 주입된 퍽 id를 노출한다(주입
        // 없으면 null).
        private static List<Perk> OfferPerks(
            RunState run, NodeKind node, IReadOnlyDictionary<string, long> stat, bool reoffer,
            out bool bossClear, out bool tierBumped, out string synPerkId, out bool heldIncluded)
        {
            synPerkId = null;
            heldIncluded = false;
            var held = new HashSet<string>(run.Perks);
            // 웹 game.js:234-235 `_augPool()`/`_relicPool()` — unlockLevel 게이트는 호출자가 먼저 거른
            // "이미 필터된 풀"을 pickPerksByTier에 넘긴다(§2-(T) 후속① 정리 — 예전엔 Shop.PickPerksByTier
            // 내부에서 GatedPool을 돌렸다. 웹 pickPerksByTier 자체엔 게이트 개념이 없다).
            IReadOnlyList<Perk> rawPool = node == NodeKind.Augment ? (IReadOnlyList<Perk>)Perks.Augments : Perks.Relics;
            var pool = Shop.GatedPool(rawPool, stat);
            string favCat = node == NodeKind.Augment ? Shop.MajorFavoredCat(run) : null;

            string heldAugId = null;
            if (node == NodeKind.Augment && !reoffer && !string.IsNullOrEmpty(run.HeldAug) && !held.Contains(run.HeldAug))
                heldAugId = run.HeldAug;

            int clearedStage = run.Stage - 1;
            // 웹 engine.js:1250 `bossClear = opts.bossClear ?? (clearedStage > 0 && clearedStage % 5 === 0)`
            // — clearedStage>0 조건 포함(0을 boss로 오판하지 않음). 실사용 경로에선 forceTier가 항상
            // 확정돼 넘어가 이 값 자체는 죽은 파라미터지만(RunEvent.offerBossPrism 표시용으로만 관측됨),
            // 문자 그대로 맞춰 둔다.
            bossClear = clearedStage > 0 && Formulas.IsBossStage(clearedStage);
            var heldPerk = heldAugId != null ? Perks.ById(heldAugId) : null;
            var baseTier = Formulas.TierForClearedStage(clearedStage);
            tierBumped = false;
            Tier nodeTier;
            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, 웹 game.js:1620/2350 · engine.js:1256
            // "opts.forceTier — 프리즘 잉크 등 강제 티어") — 💧프리즘잉크 사용 후 다음 AUGMENT 노드
            // 오퍼를 강제로 PRISM으로. 🗂️보류파일(heldPerk, Unity 전용)이 이미 결정형 우선순위 1위라
            // 그 아래(2위)로 둔다 — 웹엔 holdfile 개념이 없어 상호작용 규정이 없으므로 합리적 절충.
            bool prismInkForced = node == NodeKind.Augment && run.PrismInkActive;
            // 웹 파리티 P3.5 [Fable 결정 — Opus 2차검수 필수①②](WEB_PARITY_DESIGN.md §2-(A)/§2-(U)) —
            // 불운 게이지 만땅(forceRare)은 웹 pickPerksByTier/offerPerks에 대응 개념이 없는 Unity 전용
            // 카논 규칙이다. 원본(Kotlin) 의도는 "silverW=0 = GOLD 이상 보장"이므로, **SILVER 노드일
            // 때만 GOLD로 승급**하고 GOLD/PRISM 노드는 무승급(이미 "희귀↑ 보장" 조건을 자연히 만족한
            // 것으로 간주). 🗂️보류파일(heldPerk)이 이미 결정형 우선순위 1위이므로 이 승급은 heldPerk==
            // null(보류 미사용) 분기 안에서만 적용한다 — heldPerk 분기 밖에 두면 보류 티어까지 밀어
            // 올려 "보류 티어 결정형 우선" 원칙이 깨진다(Opus 2차검수 필수① — 보류파일 오퍼 티어 혼용
            // 회귀 제거).
            bool lucky = run.UnluckyGauge >= Formulas.UNLUCKY_MAX;
            if (heldPerk != null)
            {
                nodeTier = heldPerk.tier; // 🗂️보류파일 — 보류 티어 우선(결정형/등급업/불운승급 전부 무시, RNG 없음)
            }
            else
            {
                // Opus 2차검수 필수① — 웹 engine.js:1254-1256 offerPerks는 forceTier 유무와 무관하게
                // 10% 등급업 롤을 항상 먼저 소비한 뒤(RNG 스트림 파리티) forceTier로 덮어쓴다:
                //   `if (rng.n(100) < 10) {...} if (opts.forceTier) { nodeTier = opts.forceTier; ... }`
                // 이전 구현은 prismInkForced일 때 롤 자체를 건너뛰어(else-if) 시드 스트림이 웹과 어긋났다.
                if (run.Rng.Next(100) < 10) // 10% "행운! 등급업" — 무조건 먼저 굴린다
                {
                    var up = Formulas.TierUp(baseTier);
                    if (up != baseTier) { tierBumped = true; nodeTier = up; } else nodeTier = baseTier;
                }
                else
                {
                    nodeTier = baseTier;
                }
                if (prismInkForced) // 굴림 결과와 무관하게 덮어쓰기(웹 engine.js:1256 forceTier 우선)
                {
                    nodeTier = Tier.PRISM;
                    tierBumped = nodeTier != baseTier;
                }

                // forceRare — SILVER 노드일 때만 GOLD로 승급(RNG 미소비, 결정적 후처리). 10%등급업/
                // forceTire 처리가 모두 끝난 "최종 nodeTier"를 기준으로 판정한다 — 이미 GOLD/PRISM이면
                // 손대지 않는다.
                if (lucky && nodeTier == Tier.SILVER)
                {
                    nodeTier = Tier.GOLD;
                    tierBumped = true;
                }
            }
            if (node == NodeKind.Augment) run.PrismInkActive = false; // 소비(오퍼 생성 시도 시 무조건 리셋, 웹과 동일)

            var picks = Shop.PickPerksByTier(run.Rng, pool, held, nodeTier, bossClear, favCat);
            if (heldPerk != null)
            {
                picks = new[] { heldPerk }.Concat(picks.Where(p => p.id != heldPerk.id)).Take(3).ToList();
                heldIncluded = true; // 0번 칸 = 보류분 (원본 배너 "🗂️ 보류 후보 포함!" 대응 신호)
            }
            if (picks.Count == 0) return picks;

            // 🧩 세트 시너지 off-tier 조각 주입 (웹 engine.js:1260-1271) — 보류파일(heldPerk) 미사용
            // 시에만, 5% 확률로 마지막 칸을 "짓는 중인 세트의 빠진 조각"으로 교체(메인 티어와 다를 수
            // 있음 — 세트 완성 유도가 목적). RNG 소비 순서 주의: heldPerk==null이면 이 100-roll은
            // "syn이 실제로 발견되는지"와 무관하게 항상 1회 소비된다(웹과 동일 — `rng.n(100) < 5`가
            // `&&` 좌변이라 heldPerk==null이기만 하면 매번 평가됨). 단 `picks.Count>=2`는 Opus 2차검수
            // 필수③(WEB_PARITY_DESIGN.md §2-(U)) 반영 — 웹 engine.js:1262 `if (rng.n(100) < 5 &&
            // picks.length >= 2) { ... setSynergyPick(...) ... }`처럼 SetSynergyAug 호출 *앞*의 조건절에
            // 있어야 한다(1~2장짜리 오퍼에서는 100-roll이 성공해도 SetSynergyAug 자체를 호출하지 않아
            // RNG를 추가 소비하지 않는다) — 이전엔 SetSynergyAug를 먼저 호출한 뒤에야 picks.Count>=2를
            // 검사해 1장 오퍼에서도 웹에 없는 RNG 소비가 발생했다.
            if (heldPerk == null && run.Rng.Next(100) < 5 && picks.Count >= 2)
            {
                // Shop.SetSynergyAug는 웹 setSynergyPick과 동일하게 cat 인자와 무관하게 항상 AUGMENT
                // 조각만 찾는다(§2-(T) 후속② — RELIC 노드 오퍼라도 주입 조각은 AUGMENT일 수 있음, 웹
                // 원문 그대로). 인자는 시그니처 패리티용으로만 남긴다.
                var excludeSet = new HashSet<string>(picks.Select(p => p.id));
                excludeSet.UnionWith(held);
                var syn = Shop.SetSynergyAug(held, excludeSet, run.Rng, PCat.AUGMENT);
                if (syn != null && !picks.Any(p => p.id == syn.id))
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
            // WEB_PARITY P1 ④: 코인 15 → 30(웹 game.js:1673 "코인 +30").
            run.Coins += 30;
            // 웹 파리티 P4 — 웹 game.js:1674 "🌑 저주 ${e}${n} 획득 — ${d} · 코인 +30".
            RewardFlow.Enter(run, $"저주 {curse.name} 획득 — {curse.desc} · 코인 +30");
            return new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Curse, curseGrantedId = curse.id, coinsDelta = 30 };
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
            // 웹 파리티 P4 — 웹 game.js:1693 "🎲 위험거래 — ${e}${n}(${d}) + 저주 ${ce}${cn}(${cd})".
            RewardFlow.Enter(run, $"위험거래 — {aug.name}({aug.desc}) + 저주 {curse.name}({curse.desc})");
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
                case 6:
                {
                    // WEB_PARITY P1 ④: 레거시 코인+15 → 웹처럼 장치 획득(game.js:2292 _randomEvent case6).
                    // 미보유 장치 중 rare 가중 추첨 1개를 영구 보유로 지급(PickDevice, Opus 수정③ —
                    // deviceGrantedId로 StatTracker가 PlayerProfile.OwnedDevices에 미러링). 웹은 미장착
                    // 상태일 때만 자동 장착한다(`if (d && !r.device) r.device = d.id`) — 이미 뭔가 장착
                    // 중이면 손대지 않는다. 전부 보유 중이면 코인+15 폴백(§2-F 결정 — 웹 원문 owned
                    // 필터 버그를 재현하지 않고 "미보유 없으면 null" 규칙 그대로).
                    var picked = PickDevice(run.Rng, run.Stage, run.OwnedDeviceIds);
                    if (picked != null)
                    {
                        run.OwnedDeviceIds.Add(picked.id);
                        if (string.IsNullOrEmpty(run.Device)) run.Device = picked.id;
                        ev.deviceGrantedId = picked.id;
                    }
                    else
                    {
                        run.Coins += 15;
                        ev.coinsDelta = 15;
                    }
                    break;
                }
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
        // 웹 파리티 P3-3: EventAugLevel(AUGLEVEL 노드 오퍼)도 이 진입점을 공유한다 — 웹 game.js:2142-2145
        // `if (r._pickKind === "LVL") { r.perkLevels[p.id] = Math.min(3, ...+1); ... }`와 동일하게 "새
        // 퍽 획득"이 아니라 "보유 증강 레벨+1"로 분기한다(perks에 중복 추가하지 않음).
        public static List<RunEvent> PickOffer(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventAugment && run.Phase != RunPhase.EventRelic && run.Phase != RunPhase.EventAugLevel)
                return RunEvents.Rejected("PHASE_NOT_PERK_OFFER");
            if (index < 0 || index >= run.PerkOfferIds.Count) return RunEvents.Rejected("INVALID_INDEX");
            var perkId = run.PerkOfferIds[index];

            if (run.Phase == RunPhase.EventAugLevel)
            {
                int before = run.PerkLevels.TryGetValue(perkId, out var lv) ? lv : 1;
                int after = Math.Min(3, before + 1);
                run.PerkLevels[perkId] = after;
                run.PerkOfferIds.Clear();
                // 웹 파리티 P4 — 웹 game.js:2144 "⬆️ ${e} ${n} Lv.${lvl} 강화 완료!".
                var leveledPerk = Perks.ById(perkId);
                RewardFlow.Enter(run, $"{(leveledPerk != null ? leveledPerk.name : perkId)} Lv.{after} 강화 완료!");
                return RunEvents.One(new RunEvent { type = "PERK_LEVELED", perkId = perkId, perkLevelBefore = before, perkLevelAfter = after });
            }

            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:2147-2151 pickPerk) — 이 시점의
            // run.Phase는 EventAugLevel이 이미 위에서 빠져나갔으므로 EventAugment/EventRelic 둘 중
            // 하나다("_pickKind === 'AUG'"에 대응). A7+ 프리즘 "증강"(node==Augment 한정 — 프리즘
            // "유물" 픽은 웹도 대상이 아니다) 픽 시 저주 1개가 자동으로 붙는다.
            bool isAugPick = run.Phase == RunPhase.EventAugment;
            run.Perks.Add(perkId);
            run.PerkOfferIds.Clear();
            var grantedPerk = Perks.ById(perkId);

            string attachedCurseId = null;
            if (isAugPick && grantedPerk != null && grantedPerk.tier == Tier.PRISM && run.Asc >= 7)
            {
                var curse = run.Rng.Pick(Perks.Curses); // 웹 `this.rng.pick(CURSES)` — Perks.Curses는 항상 16종 비어있지 않음.
                run.Curses.Add(curse.id);
                attachedCurseId = curse.id;
            }

            // 웹 파리티 P4 — 웹 game.js:2185 "${e} ${n} 획득!" (+ A7 저주 동반 시 game.js:2150 안내 병기).
            string msg = $"{(grantedPerk != null ? grantedPerk.name : perkId)} 획득!";
            if (attachedCurseId != null)
            {
                var curseInfo = Perks.ById(attachedCurseId);
                msg += $" · 심화 규칙 — 저주 {(curseInfo != null ? curseInfo.name : attachedCurseId)} 동반";
            }
            RewardFlow.Enter(run, msg);
            return RunEvents.One(new RunEvent { type = "PERK_GRANTED", perkId = perkId, curseGrantedId = attachedCurseId });
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
            // 웹 파리티 P4 — 웹에 없는 Unity 전용 기능(dev_holdfile)이라 대응 리터럴이 없다. 다른 노드
            // 해소 분기와 동일하게 REWARD_DONE을 거치는 편이 일관적이라(§작업 지시 "노드 처리 완료 →
            // REWARD_DONE" 원칙) 이 분기도 포함시켰다(Fable 최종검수 대상 — 이탈 아님, 확장 판단).
            var heldPerk = Perks.ById(perkId);
            RewardFlow.Enter(run, $"{(heldPerk != null ? heldPerk.name : perkId)} 보류 — 다음 증강 오퍼에 포함됩니다");
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
                offerSynergyPerkId = synPerkId, offerRetake = true,
            });
        }

        // ── WEB_PARITY P1 ④: DEVICE 노드 오퍼 확정(deviceNodeTake, 웹 game.js:2523-2529) ──────────
        // equip=true → 현재 런의 Device 슬롯을 교체(장착). equip=false → 코인+15만. 어느 쪽이든 장치는
        // 영구 보유로 지급(deviceGrantedId — StatTracker가 PlayerProfile.OwnedDevices에 반영).
        //
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 이 분기만 RewardFlow.Enter를 거치지 않고 곧장
        // RunPhase.Spin으로 간다. 웹 deviceNodeTake(game.js:2523-2529)가 `_enterRewardDone`이 아니라
        // `_beginStage()`를 직접 호출하는 것과 동일 파리티(§WEB_PARITY_DESIGN.md 대조 확인 — 유일한
        // 예외). 다른 모든 노드 해소 분기가 RewardFlow.Enter로 바뀐 뒤에도 이 함수만 원래 동작 그대로
        // 유지한다.
        public static List<RunEvent> TakeDevice(RunState run, bool equip)
        {
            if (run.Phase != RunPhase.DeviceNode) return RunEvents.Rejected("PHASE_NOT_DEVICE_NODE");
            var devId = run.PendingDeviceDrop;
            if (string.IsNullOrEmpty(devId)) return RunEvents.Rejected("NO_PENDING_DEVICE_DROP");

            run.OwnedDeviceIds.Add(devId);
            run.PendingDeviceDrop = "";
            var ev = new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Device, deviceGrantedId = devId, deviceId = devId };
            if (equip)
            {
                run.Device = devId;
            }
            else
            {
                run.Coins += 15;
                ev.coinsDelta = 15;
            }
            run.Phase = RunPhase.Spin;
            return RunEvents.One(ev);
        }
    }
}
