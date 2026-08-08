using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 아이템 사용(NEXTSPIN arm / PHASE / INSTANT) + 코인 차감(가방 슬롯 소모일 뿐 사용 자체는 무료 — 코인은
    // 상점 구매 시점에 이미 지불됨, §4-D). Kotlin SlotV2Service의 handleItem(L1536-1603)/
    // applyItemPurchase(L1438-1518)/instantQuota(L1430-1436)를 전사. 02_service.md §7-D·§7-E.
    public static class ItemUse
    {
        // ITEM_SLOTS(Kotlin L45) 기본값 — 웹 파리티 P3-4 Opus 2차검수 필수③(WEB_PARITY_DESIGN.md §2):
        // 웹 game.js:2301 `_giveItem` cap은 `3 + ascMods(r.asc).itemCapDelta + mods.itemCapBonus`(item_bag
        // 증강 등)로 동적이다 — 승천(asc)은 P6 미구현이라 생략(곱연산 아니라 가산 항이라 나중에 그대로
        // 끼워 넣을 수 있음). 정적 상수는 "기본값" 의미로만 남기고, 실사용은 EffectiveSlots(run)를 거친다.
        public const int BaseItemSlots = 3;

        // 웹 game.js:2301 `_giveItem` cap 그대로 — Shop.Buy(가방 여유칸 확인)·BagPopup/RunView(표시)가 공용.
        public static int EffectiveSlots(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
            return BaseItemSlots + mods.itemCapBonus;
        }

        // INSTANT_CLEAR_ITEMS 6종 — SlotV2Engine.kt:1009-1012, S4 백로그 항목. 스테이지당 1회("ICLEAR" 마커,
        // 클리어 시 StageFlow가 usedCmds를 리셋하므로 자동 재충전).
        private static readonly HashSet<string> InstantClearItems = new HashSet<string>
        {
            "answer_sheet", "grad_cert", "grad_copy", "honor_roll", "grad_ring", "gold_grad_bell",
        };

        public static bool IsInstantClearItem(string id) => InstantClearItems.Contains(id);

        // instantQuota(run) — clearStage와 동일한 실제 쿼터(phase 아이템의 quotaMul까지 반영), Kotlin L1430-1436.
        private static long InstantQuota(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
            return SpinResolver.QuotaOf(run.Stage, mods);
        }

        // ══════════════════════════════════════════════════════════════════
        // Use — "아이템 N"(Kotlin handleItem, L1536-1603)의 typed 버전. id로 가방에서 찾는다(첫 매치,
        // ENGINE_PORT_DESIGN.md 계약 "UseItem(string id)"). SPIN 상태에서만 유효.
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> Use(RunState run, string itemId, IReadOnlyDictionary<string, long> stat)
        {
            if (run.Phase != RunPhase.Spin) return RunEvents.Rejected("PHASE_NOT_SPIN");
            int idx = run.Items.IndexOf(itemId);
            if (idx < 0) return RunEvents.Rejected("ITEM_NOT_IN_BAG");
            var itm = Items.ById(itemId);
            if (itm == null) return RunEvents.Rejected("ITEM_UNKNOWN");

            // (C1) 즉시클리어/대량스킵형 — 스테이지당 1회 캡.
            if (IsInstantClearItem(itemId) && run.UsedCmds.Contains("ICLEAR"))
                return RunEvents.Rejected("ICLEAR_ALREADY_USED");

            // 📄 재시험(retake_form) 사전 검증 — 거부 시 RNG 포함 아무 것도 변형하지 않는다(기존 Unity
            // "거부=완전 무변형" 불변식 유지, 아래 keepItem 롤보다 먼저 걸러야 한다). 웹엔 이 가드 자체가
            // 없다(Unity 전용 방어 — 02_service.md 계약 "거부 시 상태 불변" 그대로 보존).
            if (itemId == "retake_form" && (run.LastCells.Count == 0 || run.LastSpinNo < 0))
                return RunEvents.Rejected("NO_LAST_SPIN");

            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, 웹 game.js:1337 `const keep = r.perks.includes
            // ("refund") && rng.double() < 0.3`) — 🧾환불 정책 보유 시 30% 확률로 가방에서 사라지지
            // 않는다. 효과 자체(ApplyItemPurchase/UseRetakeForm)는 keep 여부와 무관하게 항상 발동 —
            // "재사용 가능한 슬롯" 개념일 뿐 효과를 두 번 주는 게 아니다. RNG 소비는 웹과 동일하게 효과
            // 적용 이전. Opus 2차검수 웹 이탈 정리⑦ — retake_form도 웹은 동일 useItem() 파이프라인
            // (단일 switch)을 타므로 예외 없이 적용한다(이전엔 조기 반환으로 keep 판정 자체를 건너뜀).
            bool keepItem = run.Perks.Contains("refund") && run.Rng.NextDouble() < 0.3;

            // 📄 재시험(retake_form) — 즉발 아님, 직전 스핀 전체 재굴림(별도 분기, Kotlin L1551-1575).
            if (itemId == "retake_form")
                return UseRetakeForm(run, idx, keepItem);

            if (!keepItem) run.Items.RemoveAt(idx);
            if (IsInstantClearItem(itemId)) run.UsedCmds.Add("ICLEAR");
            run.UsedItemThisRun = true;

            ApplyItemPurchase(run, itm, stat);

            var ev = new RunEvent { type = "ITEM_USED", itemId = itemId };

            // 💍🔔 졸업반지/황금졸업벨 — 즉시클리어 도달 시 클리어 확정(Kotlin L1584-1594).
            // [스펙-Kotlin 불일치 — 신규 발견, 보고 대상] Kotlin 원문은 이 판정/재계산에 쓰는 mods가
            // instantQuota()(device+phasePerks 포함)와 서로 다르게(device 생략) 한 번 더 buildMods를
            // 호출하는 내부 불일치가 있다(L1587-1590). 실질 영향이 미미하고(장치 fx가 크지 않음, 이 조합에
            // reqDevice 세트 게이트도 없음) 원본의 의도치 않은 결과로 판단해 instantQuota()와 동일한(더
            // 정확한) mods로 통일했다 — 값이 크게 갈리는 콘텐츠가 추가되면 재검토 필요.
            if ((itemId == "grad_ring" || itemId == "gold_grad_bell") && run.StageExp >= InstantQuota(run))
            {
                var combinedPerks = new List<string>(run.Perks);
                combinedPerks.AddRange(run.PhasePerks);
                var mods = ModsBuilder.ApplyItemMods(
                    ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                    run.PhaseItems);
                int spins = SpinResolver.EffSpins(run, mods);
                long quota = SpinResolver.QuotaOf(run.Stage, mods);
                var outcome = new SpinOutcome
                {
                    rejected = false, mode = SpinMode.N, result = null, gained = 0,
                    newExp = run.StageExp, newScore = run.Score, newCoins = run.Coins,
                    newSpinIndex = run.SpinIndex, quota = quota, spins = spins,
                };
                var clear = StageFlow.ClearStage(run, outcome);
                return new List<RunEvent> { ev, new RunEvent { type = "STAGE_CLEARED", spin = outcome, clear = clear } };
            }

            return RunEvents.One(ev);
        }

        // ── 📄재시험(retake_form) — 직전 스핀 전체 재굴림, INSTANT_CLEAR_ITEMS 아님(ICLEAR 무관). ──
        // NO_LAST_SPIN 검증은 호출측(Use)이 keepItem 롤보다 먼저 이미 마쳤다(중복 검증 아님 — 거부=
        // 완전 무변형 불변식을 keepItem RNG 소비보다 앞에 두기 위한 순서 요구).
        private static List<RunEvent> UseRetakeForm(RunState run, int bagIndex, bool keepItem)
        {
            if (!keepItem) run.Items.RemoveAt(bagIndex);
            run.UsedItemThisRun = true;

            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            // 웹 파리티 P3.5(WEB_PARITY_DESIGN.md §2-(T) 후속③) — 웹 _freeReroll()(game.js:1214-1224)은
            // this._mods() → this._ctx()를 그대로 거쳐 ctx 포함 buildMods를 호출한다(game.js:443-445
            // `E.buildMods(r.machineId, r.charId, r.perks, r.curses, r.device, this._ctx(), r.perkLevels)`).
            // 예전 구현은 ModsBuilder.Build를 ctx 없이(기본 `new RunCtx()`, coins=99) 호출해 ctx-조건부
            // 퍽(early_prep/growth_log/snowball/fortune_check/luck_accum/fate_burst/late_focus/
            // cliff_focus/sacrifice/black_diploma/bankrupt/abyss_scholar/curse_grad/black_grad_photo/
            // phoenix_thesis)이 재굴림 시점엔 전부 "기본값 컨텍스트"로 잘못 계산되는 공백이 있었다
            // (Mods.cs의 RunCtx.coins 주석 참조 — 99는 "무해 기본값"이 아니라 "우연히 안 읽혀서
            // 무해"였을 뿐이다). SpinResolver.ResolveSpin과 같은 모양의 2단계 패턴(ctx 없는 1차 mods로
            // spins/quota를 먼저 산출 → 그 값으로 RunCtx 구성 → ctx 포함 2차 mods)을 재사용한다.
            // [Opus 2차검수 권장⑧] 오늘 시점 콘텐츠 기준으로는 이 2단계가 ResolveSpin의 진짜 3단계
            // 재계산과 **결과값이 동치**다 — ctx-조건부 퍽 14종 중 quotaMul에 영향을 주는 것은 없고
            // bonusSpins에 영향을 주는 것은 black_diploma(curseCount>=5) 하나뿐인데, curseCount는
            // run.Curses.Count로 mods 계산과 무관하게 즉시 알 수 있어 1차(ctx 없는) mods로 뽑은
            // preSpins만으로도 정확하다. 그러나 **구조적으로 3단계와 동일한 것은 아니다** — 향후
            // ctx-조건부 퍽이 quotaMul이나 bonusSpins를 stage/spinIndex 등 "mods 계산 자체에 의존하는"
            // ctx 필드로 계산하게 되면, 이 1차 mods 기반 preSpins/preQuota가 실제 최종값과 어긋나는
            // 시나리오가 이론상 가능하다 — 그때는 ResolveSpin처럼 진짜 반복(고정점) 계산으로 확장해야
            // 한다.
            var pre0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels);
            var preSpins = SpinResolver.EffSpins(run, pre0);
            var preQuota = SpinResolver.QuotaOf(run.Stage, pre0);
            var ctx = SpinResolver.RunCtxOf(run, run.SpinIndex, preSpins, preQuota);
            var pmods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, ctx, run.PerkLevels);
            var pmods1 = ModsBuilder.ApplyItemMods(pmods0, run.PhaseItems);
            var devEq = Devices.ById(run.Device);
            var pmods = (devEq != null && devEq.kind == "PASSIVE") ? ModsBuilder.ApplyPassiveDevice(pmods1, devEq.id) : pmods1;
            int spins = SpinResolver.EffSpins(run, pmods);

            int n = run.LastCells.Count;
            var newRaw = new List<Cell>(n);
            for (int i = 0; i < n; i++) newRaw.Add(SpinResolver.RollOne(run.Rng, pmods));
            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): Evaluate capMul 인자 제거(총배율 캡 폐지).
            var res2 = SpinResolver.Evaluate(run.Rng, newRaw, pmods, run.LastSpinNo, spins, run.FlameNext);

            run.StageExp = Math.Max(run.StageExp - run.LastGain, 0) + res2.exp;
            run.Score = Math.Max(run.Score - run.LastScoreGain + res2.score, 0);
            run.Coins = Math.Max(run.Coins - run.LastCoinGain + res2.coins, 0);
            run.LastCells.Clear();
            run.LastCells.AddRange(newRaw.Select(c => c.sym.id));
            run.LastGain = res2.exp;
            run.LastScoreGain = res2.score;
            run.LastCoinGain = res2.coins;

            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = res2, gained = res2.exp,
                newExp = run.StageExp, newScore = run.Score, newCoins = run.Coins, newSpinIndex = run.SpinIndex,
            };
            return RunEvents.One(new RunEvent { type = "ITEM_USED", itemId = "retake_form", spin = outcome });
        }

        // ── applyItemPurchase (Kotlin L1438-1518) — NEXTSPIN/PHASE는 arm/phase 목록에 편성, INSTANT 23종 ──
        private static void ApplyItemPurchase(RunState run, ItemDef itm, IReadOnlyDictionary<string, long> stat)
        {
            switch (itm.kind)
            {
                case "NEXTSPIN": run.ArmItems.Add(itm.id); return;
                case "PHASE": run.PhaseItems.Add(itm.id); return;
            }

            switch (itm.id)
            {
                case "first_aid": run.StageBonusSpins += 1; break;
                case "double_aid": run.StageBonusSpins += 2; break;
                case "cram": run.StageExp += InstantQuota(run) * 15 / 100; break;
                case "cheat_sheet": run.StageExp += InstantQuota(run) * 30 / 100; break;
                case "answer_sheet": run.StageExp += InstantQuota(run) * 50 / 100; break;
                case "honor_roll": run.StageExp += InstantQuota(run) * 70 / 100; break;
                case "grad_cert": run.StageExp += InstantQuota(run); break;
                case "dev_battery": run.ArmItems.Add("dev_coin"); break; // 🔋 dev_coin 레버 재사용
                case "score_sticker": run.Score += 150; break;
                case "old_coin": run.Coins += 6; break;
                case "grad_copy":
                    run.StageExp += InstantQuota(run) * 80 / 100;
                    run.Score = Math.Max(run.Score * 9 / 10, 0);
                    break;
                case "score_calc": run.Score += run.Score * 30 / 100; break;
                case "mini_coupon": run.Coins += 9; break;
                case "price_hack": run.Coins += 18; break;
                case "grad_ring":
                {
                    long shortfall = InstantQuota(run) - run.StageExp;
                    if (shortfall >= 0 && shortfall <= 20) run.StageExp = InstantQuota(run);
                    break;
                }
                case "gold_grad_bell":
                {
                    long shortfall = InstantQuota(run) - run.StageExp;
                    if (shortfall >= 0 && shortfall <= 50) run.StageExp = InstantQuota(run);
                    break;
                }
                case "insurance_cert": run.Survive = true; break;
                case "debt_note": run.Coins += 30; run.DebtStages = 4; break;
                // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑥(WEB_PARITY_DESIGN.md §2, 웹 game.js:1383-1384
                // black_lottery/devil_contract) — 두 아이템 다 원본부터 raw RELICS(레벨게이트/GatedPool
                // 미적용)에서 뽑는다(웹 quirk 보존, held 제외 필터만 유지 — 이건 Kotlin 원본부터 있던
                // "이미 보유 중이면 재추첨 대상에서 제외"일 뿐 해금 게이트가 아니다).
                case "black_lottery":
                {
                    if (run.Rng.Next(2) == 0)
                    {
                        var held = new HashSet<string>(run.Perks);
                        var pool = Perks.Relics.Where(p => p.tier == Tier.GOLD && !held.Contains(p.id)).ToList();
                        var rel = run.Rng.PickOrDefault(pool);
                        if (rel != null) run.Perks.Add(rel.id); else run.Coins += 15;
                    }
                    else
                    {
                        var heldC = new HashSet<string>(run.Curses);
                        var c = run.Rng.PickOrDefault(Perks.Curses.Where(x => !heldC.Contains(x.id)).ToList());
                        if (c != null) run.Curses.Add(c.id);
                    }
                    break;
                }
                case "devil_contract":
                {
                    var held = new HashSet<string>(run.Perks);
                    var heldC = new HashSet<string>(run.Curses);
                    var rel = run.Rng.PickOrDefault(Perks.Relics.Where(p => !held.Contains(p.id)).ToList());
                    var c = run.Rng.PickOrDefault(Perks.Curses.Where(x => !heldC.Contains(x.id)).ToList());
                    if (rel != null) run.Perks.Add(rel.id);
                    if (c != null) run.Curses.Add(c.id);
                    run.Coins += 25;
                    break;
                }
                case "broken_prism":
                {
                    var safe = new[]
                    {
                        "overdrive", "supernova", "wild_world", "joker", "seed_garden",
                        "great_harvest", "jackpot", "mega_jackpot", "gamblers_dice", "key_master", "time_warp",
                    };
                    var heldSet = new HashSet<string>(run.Perks);
                    var avail = safe.Where(id => !heldSet.Contains(id) && Shop.PerkUnlocked(Perks.ById(id), stat)).ToList();
                    var pick = run.Rng.PickOrDefault(avail)
                               ?? run.Rng.PickOrDefault(safe.Where(id => !heldSet.Contains(id)).ToList())
                               ?? "overdrive";
                    // Kotlin L1496 run.copy(phasePerks = pick) — CSV 단일 필드 "대입"이라 기존 임시 퍼크를
                    // 덮어쓴다(스테이지 내 2회 사용해도 임시 프리즘은 항상 1개). Add로 누적하면 원본과 달라짐.
                    run.PhasePerks.Clear();
                    run.PhasePerks.Add(pick);
                    break;
                }
                case "timeline_ticket":
                {
                    var combinedPerks = new List<string>(run.Perks);
                    combinedPerks.AddRange(run.PhasePerks);
                    var pmods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels);
                    var pmods1 = ModsBuilder.ApplyItemMods(pmods0, run.PhaseItems);
                    var devEq = Devices.ById(run.Device);
                    var pmods = (devEq != null && devEq.kind == "PASSIVE") ? ModsBuilder.ApplyPassiveDevice(pmods1, devEq.id) : pmods1;
                    int reel = (devEq != null && devEq.id == "dev_subreel") ? Formulas.REEL + 1 : Formulas.REEL;
                    int spins = SpinResolver.EffSpins(run, pmods);
                    var a = SpinResolver.RollRaw(run.Rng, pmods, reel, run.SeedNext);
                    var b = SpinResolver.RollRaw(run.Rng, pmods, reel, run.SeedNext);
                    // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): Evaluate capMul 인자 제거(총배율 캡 폐지).
                    var ea = SpinResolver.Evaluate(run.Rng, a, pmods, run.SpinIndex, spins, false).exp;
                    var eb = SpinResolver.Evaluate(run.Rng, b, pmods, run.SpinIndex, spins, false).exp;
                    var win = eb > ea ? b : a;
                    run.LockedNext.Clear();
                    run.LockedNext.AddRange(win.Select(c => c.sym.id));
                    break;
                }
                case "retake_form": break; // 여기선 무효과(위 특수분기)
                // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #12/#14, 웹 game.js:1368-1372) — 증강
                // 레벨업 상점 상품 5종. AugLevels.LevelableHeld(보유&레벨업가능&Lv<3)를 그대로 재사용.
                case "study_note":
                {
                    var ids = AugLevels.LevelableHeld(run);
                    if (ids.Count > 0)
                    {
                        var t = LowestLevelHeld(run, ids);
                        run.PerkLevels[t] = Math.Min(3, LevelOf(run, t) + 1);
                    }
                    break;
                }
                case "aug_catalyst": run.AugLevelBoost += 0.15; break;
                case "gold_marker":
                {
                    var ids = AugLevels.LevelableHeld(run)
                        .Where(id => { var p = Perks.ById(id); return p != null && p.tier == Tier.GOLD; })
                        .ToList();
                    if (ids.Count > 0)
                    {
                        var t = LowestLevelHeld(run, ids);
                        run.PerkLevels[t] = Math.Min(3, LevelOf(run, t) + 1);
                    }
                    break;
                }
                case "prism_ink": run.PrismInkActive = true; break;
                case "overcharge":
                    run.PendingNextExpMul *= 1.5;
                    run.Score = Math.Max(0, run.Score - 200);
                    break;
                default: break;
            }
        }

        // 웹 game.js:1368/1370 `ids.sort((a,b) => (r.perkLevels[a]||1) - (r.perkLevels[b]||1)); ids[0]`
        // — 보유 레벨업가능 증강 중 현재 레벨이 가장 낮은 1개(동률이면 원본 나열 순서 유지, List.Sort는
        // 불안정 정렬이 아니라 OrderBy로 안정 정렬을 보장한다).
        private static string LowestLevelHeld(RunState run, List<string> ids) =>
            ids.OrderBy(id => LevelOf(run, id)).First();

        private static int LevelOf(RunState run, string perkId) =>
            run.PerkLevels.TryGetValue(perkId, out var lv) ? lv : 1;
    }
}
