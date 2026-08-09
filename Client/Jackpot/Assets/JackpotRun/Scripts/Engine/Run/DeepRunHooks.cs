using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19) — 심화모드(deepMode) 런 스코프 훅. AscRunHooks.cs와
    // 동일한 위치·역할(internal, "특정 게임모드 축이 실제 롤/quota에 개입하는 지점만 모아둔 파일") —
    // Pouch/PouchOps가 콘텐츠·순수 추출 로직이라면, 이 파일은 그 둘을 RunState에 실제로 배선한다.
    internal static class DeepRunHooks
    {
        // 배치F P2(웹 game.js `deepPity`) — 획득 심볼 2스핀 보장. add/upgrade(P7-2/3 보상 지급) 직후
        // 설정된 run.DeepPity를 첫 "fresh 굴림"(LockedNext 미사용 — 예언/고정 굴림은 대상 아님, 웹과
        // 동일)에서 소진한다: 5칸에 이미 있으면 자연 등장 소진, 없으면 무작위 1칸 강제 치환 후 소진.
        // spinsLeft는 방어용 안전망(정상 플로우에서는 미도달) — 웹 `_pityRoll` 그대로.
        public static List<Cell> ApplyDeepPity(RunState run, List<Cell> raw)
        {
            if (!run.DeepMode || run.DeepPity == null || raw == null || raw.Count == 0) return raw;
            var pity = run.DeepPity;
            var psym = Symbols.ById(pity.Id);
            if (psym == null) { run.DeepPity = null; return raw; } // 설정 가드 통과분이라 실질 미도달(방어)

            pity.SpinsLeft -= 1;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].sym != null && raw[i].sym.id == pity.Id) { run.DeepPity = null; return raw; } // 자연 등장 → 소진
            }
            if (pity.SpinsLeft < 0) { run.DeepPity = null; return raw; } // 만료 안전망

            raw[run.Rng.Next(raw.Count)] = new Cell(psym, "✨→");
            run.DeepPity = null;
            return raw;
        }

        // 심화모드 압축 패널티(요구치 배수) — 일반모드는 항상 1(무영향), 웹 `_deepPenalty()` 그대로.
        // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 A) — P7-1이 TODO로 남겨 둔 나머지
        // 두 항(심볼퍽 penaltyMul 완화 클램프 + 전설봉인함 보스요구 25% 감쇄)을 채운다: base(총량 압축
        // 패널티 × 상점 '덱 압축' 누적 요구율) → 심볼퍽 penaltyMul을 "초과분에만" 적용(Max(1, 1+(base-1)
        // ×sp.PenaltyMul)) → 보스 스테이지면 sp.BossQuotaMul(legendSeal 보유 시 증가분 25% 감쇄) 곱 →
        // EARLY_QUOTA 램프. symPerkMods를 보유 perk가 없어도 항상 계산하므로(빈 리스트 → 전부 기본값
        // 1.0) 심볼퍽 미보유 런은 이전 슬라이스와 정확히 동일한 값을 낸다(무회귀).
        public static double DeepPenalty(RunState run)
        {
            if (run == null || !run.DeepMode) return 1.0;
            double baseMul = Pouch.CompressionPenalty(Pouch.Total(run.Pouch)) * (1.0 + run.DeepCompressExtra);
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            double mul = System.Math.Max(1.0, 1.0 + (baseMul - 1.0) * sp.PenaltyMul);
            bool boss = Bosses.For(run.Stage) != null;
            if (boss && sp.BossQuotaMul != 1.0)
            {
                double bq = sp.BossQuotaMul;
                if (sp.LegendSeal && bq > 1.0) bq = 1.0 + (bq - 1.0) * 0.75;
                mul *= bq;
            }
            mul *= Pouch.EarlyQuotaMul(run.Stage);
            return mul;
        }

        // 웹 파리티 P7-2 blocker(§0, WEB_PARITY_DESIGN.md §2-(AA) 선행 blocker) — mods.symbolWeightMul/
        // weightAdd/rareWeightMul(영구 perk·장치·저주·세트 기반, ModsBuilder.Build 산출)을 주머니 추출용
        // PouchBias로 변환한다. 웹 `_roll()`(game.js:657-680)의 bias 조립부 그대로 — 단, 웹의 `_reachBias`
        // (리치 태그 bias ×1.5)·RETRY_REEL 재도전은 잭팟 태그 실제 판정 자체가 P7-1 헤더 "웹 대비
        // 생략/이월" ④번(P7-3 범위)이라 이식하지 않는다(그 둘을 뺀 "영구 perk 기반 bias" 부분만).
        public static PouchBias BuildPouchBias(Mods mods)
        {
            if (mods == null) return null;
            bool hasMul = mods.symbolWeightMul.Count > 0;
            bool hasAdd = mods.weightAdd.Count > 0;
            bool hasRare = mods.rareWeightMul != 1.0;
            if (!hasMul && !hasAdd && !hasRare) return null;
            // Symbols.BySym은 미등록 enum 값이면 예외를 던진다(null을 반환하지 않음, Symbols.cs 계약) —
            // symbolWeightMul/weightAdd의 키는 전부 ModsBuilder(Wmul/Wadd/머신 weightMul 시딩)가 채운
            // 값이라 항상 유효한 Sym이다. id 기반 PouchBias로 옮기면서 Sym → id 변환만 하면 된다(pouch에
            // 없는 id가 섞여도 PouchOps.PouchDraw가 Pouch.Symbols71 기준 스캔이라 안전하게 무시함 —
            // PouchOps.cs "미지 id 방어" 각주 참조).
            var bias = new PouchBias();
            if (hasMul)
                foreach (var kv in mods.symbolWeightMul) bias.Mul[Symbols.BySym(kv.Key).id] = kv.Value;
            if (hasAdd)
                foreach (var kv in mods.weightAdd) bias.Add[Symbols.BySym(kv.Key).id] = kv.Value;
            if (hasRare) bias.RareMul = mods.rareWeightMul;
            return bias;
        }

        // 웹 파리티 P7-2(§1-A #19 A+B, 웹 game.js:454 태그강화 주입 + 461-475 심볼퍽 skullPenaltyMul/
        // deepTagMul 병합 + 483-505 "배치 G: 계열 아키타입(전공) 보너스 주입") 그대로 — 심화 런의
        // "최종" mods(모든 클론/저주/장치/승천 적용 이후, evaluate가 실제로 읽는 값)에 딱 1회 꽂는다.
        // 반드시 AscRunHooks.ApplyRunAscMods 직후(그 이상의 클론/오버레이가 없는 마지막 자리)에
        // 호출해야 웹과 동일한 "최종 mods 1회 주입" 타이밍이 재현된다 — 호출부 5곳: SpinResolver.
        // ResolveSpin·DeviceActions.HandleManip·DeviceActions.GamblerReroll·ItemUse.UseRetakeForm·
        // ItemUse.ApplyItemPurchase(timeline_ticket). 일반모드는 즉시 반환(무회귀).
        // Opus 2차검수(P7-2, 2026-08-09) 필수①③⑤ 반영:
        //  ①심볼퍽 skullPenaltyMul(sa_expand_build 0.7·sr_big_bag 0.8)이 이전엔 계산만 되고
        //   mods.skullPenaltyMul에 실제로 곱해지지 않았다 — 아키타입 skullPenaltyMul 곱과 나란히 추가.
        //  ③deepTagMul(정비소 '태그 강화' + 심볼퍽 tagBuff류)이 mods에 전혀 안 꽂혀 sv_tagbuff(22코인)가
        //   순수 장식이었다 — 신규 `Mods.deepTagMul` 필드에 웹 game.js:454(run.DeepTagBuff 직접 복사,
        //   pouch/심볼퍽과 무관하게 항상 우선 적용) → game.js:464-465(sp.DeepTagMul을 그 위에 가산 병합)
        //   순서 그대로 채운다. 소비 지점은 SpinResolver.Evaluate(웹 engine.js:679-683, ±50% 클램프).
        //  ⑤이중 호출 안전 — `Mods.DeepModsApplied` 가드로 같은 mods 인스턴스에 두 번 불려도(예: 향후
        //   호출부 추가 실수) 아키타입/심볼퍽 배수가 중복 누적되지 않는다. 빈 주머니 조기 반환을
        //   제거했다(웹 `if (r.pouch)`는 빈 객체 `{}`도 truthy라 항상 통과 — SymPerks.ComputeMods/
        //   Archetypes.PouchArchetype 둘 다 빈/0-총량 pouch를 안전하게 중립값으로 처리하므로 별도
        //   가드가 애초에 불필요했다).
        // [P7-2 범위 밖] hex_allornothing 저주의 dEff={archetypeMul:0.5}(전공 배율 절반, 심화 전용
        // 재설계)는 Content/Archetypes.cs 헤더 각주 참조 — dEff 메커니즘 자체가 없어 미이식.
        public static void ApplyDeepMods(Mods mods, RunState run)
        {
            if (mods == null || run == null || !run.DeepMode || mods.DeepModsApplied) return;
            mods.DeepModsApplied = true;
            // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스) — 웹 `_mods()` 심화블록이
            // 세우는 `mods.deepMode = true`. SpinResolver.Evaluate의 잭팟 태그 블록이 이 플래그로
            // 게이팅한다(일반모드는 이 함수 자체가 조기 반환해 항상 false).
            mods.deepMode = true;

            // 웹 game.js:454 — 정비소 '태그 강화'(run.DeepTagBuff) 직접 반영. pouch 상태와 무관하게 항상
            // 가장 먼저 적용(웹은 이 줄이 아래 심볼퍽 병합보다 앞선 별도 `if` 문).
            foreach (var kv in run.DeepTagBuff) mods.deepTagMul[kv.Key] = kv.Value;

            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            // 웹 game.js:464-465 — 심볼퍽 tagBuff(sa_tag_sense/sa_tag_major/sp_solo_major/sr_compass)
            // 결과를 위 정비소 태그강화 위에 태그별로 가산 병합(덮어쓰기 아님).
            foreach (var kv in sp.DeepTagMul)
                mods.deepTagMul[kv.Key] = (mods.deepTagMul.TryGetValue(kv.Key, out var e) ? e : 0) + kv.Value;
            // 웹 game.js:474 — 확장빌드(sa_expand_build)/확장가방(sr_big_bag) skullPenaltyMul 실배선.
            mods.skullPenaltyMul *= sp.SkullPenaltyMul;

            var arch = Archetypes.PouchArchetype(run.Pouch);
            var am = Archetypes.ArchetypeMods(arch);
            foreach (var kv in am.ExpMul) mods.deepFamilyExpMul[kv.Key] = kv.Value;
            foreach (var kv in am.ScoreMul) mods.deepFamilyScoreMul[kv.Key] = kv.Value;
            foreach (var kv in am.CoinMul) mods.deepFamilyCoinMul[kv.Key] = kv.Value;
            if (am.SkullPenaltyMul != 1.0) mods.skullPenaltyMul *= am.SkullPenaltyMul;
        }

        // 웹 파리티 P7-2(§1-A #19 B, 웹 game.js:548-558 `_checkArchetype()`) — 주머니 변경(정비소 구매)
        // 후 전공 발동/승급을 감지해 토스트용 RunEvent를 만든다. 발동(family 전환)·승급(같은 family
        // tier↑)만 알리고 소멸은 무음(웹과 동일, 과알림 방지). run.DeepArchFamily/DeepArchTier를 항상
        // 최신 상태로 갱신(변화 없어도)한다. UI가 이 이벤트를 실제로 토스트로 그리는 것은 P7-4.
        // Opus 2차검수(P7-2) 필수⑤ — 빈 주머니 조기 반환 제거(웹 `if (!r.pouch) return`는 빈 객체도
        // truthy라 통과 → `pouchArchetype({})`가 자연히 family=null/tier=0을 내어 DeepArchFamily/Tier가
        // 정상 리셋된다). 이전 구현은 `run.Pouch.Count==0`이면 통째로 조기 반환해, 주머니가 전부
        // 비워진 극단 케이스에서 직전 전공 상태(예 "cherry")가 스테일하게 남는 버그가 있었다.
        public static RunEvent CheckArchetypeChange(RunState run)
        {
            if (run == null || !run.DeepMode) return null;
            var arch = Archetypes.PouchArchetype(run.Pouch); // 빈/0-총량 pouch도 Family=null/Tier=0로 안전 처리.
            string fam = arch.Tier > 0 ? arch.Family : null;
            string prevFam = run.DeepArchFamily;
            int prevTier = run.DeepArchTier;
            RunEvent ev = null;
            if (!string.IsNullOrEmpty(fam) && (fam != prevFam || arch.Tier > prevTier))
            {
                ev = new RunEvent { type = "ARCHETYPE_CHANGED", archFamily = fam, archTier = arch.Tier, archUpgraded = fam == prevFam };
            }
            run.DeepArchFamily = fam;
            run.DeepArchTier = fam != null ? arch.Tier : 0;
            return ev;
        }

        // ── Phase 4 V3P4(웹 POUCH_USE) "instant" 소모 — 이 슬라이스의 단순화 버전(작업 지시 6번) ──
        // 웹은 붕대/매듭/에너지팩/가짜왕관/진화핵 5종 각각의 실제 효과(evaluate 내부 특수분기, P7-2/3
        // 범위)가 발동하는 순간 자기 자신을 덱에서 -1 한다. 이 슬라이스는 그 실제 효과를 아직 구현하지
        // 않으므로(Sp 신규 51종 전부 evaluate에 case 없음, Symbols.cs 헤더 각주 참조) "등장 즉시 덱
        // -1"만 일반화해 둔다. Opus 2차검수(P7-1, 2026-08-09) [MED]③ — 웹 game.js:814-828의 실제
        // 소비 방식은 evaluate()가 뽑아낸 "이번 스핀에 등장했는가"(`res.hasBandage` 등 불리언 플래그,
        // 개수 아님)를 보고 `{type:"remove", id, n:1}`을 **최대 1회만** 적용한다 — 5칸 중 같은 instant
        // 심볼이 2번 이상 등장해도 덱에서는 정확히 1개만 빠진다. `Pouch.Use[id]=="instant"`인 심볼이
        // 이번 스핀 raw 셀에 "등장했는지"(중복 등장은 1회로 취급)만 보고 최대 1개만 감산한다(0 하한,
        // 0이면 키 제거). 실제 게임 효과는 P7-2/3가 이 감산 자리 옆에 이어붙이면 된다 — 이중 소모
        // (효과 로직이 따로 또 -1 하는 것) 방지를 위해 P7-2/3 구현 시 이 함수를 대체/확장하는 쪽으로
        // 통합할 것(지금은 자리만 예약).
        public static void ConsumeInstantSymbols(RunState run, IReadOnlyList<Cell> raw)
        {
            if (!run.DeepMode || raw == null) return;
            var consumedThisSpin = new HashSet<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                var id = raw[i].sym?.id;
                if (string.IsNullOrEmpty(id)) continue;
                if (!Pouch.Use.TryGetValue(id, out var use) || use != "instant") continue;
                if (!consumedThisSpin.Add(id)) continue; // 이미 이번 스핀에 이 id를 처리함(웹: 등장 여부만 봄)
                if (run.Pouch.TryGetValue(id, out var n) && n > 0)
                {
                    if (n <= 1) run.Pouch.Remove(id);
                    else run.Pouch[id] = n - 1;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스) — §9.0 J1 잭팟태그 리치 bias.
        // ══════════════════════════════════════════════════════════════════════

        // 웹 game.js:663-672 `_roll()`의 리치 bias 병합부 — run.ReachBiasTag가 세팅돼 있으면(잭팟태그
        // 리치 발동, 1스핀) 그 태그를 가진 심볼 전부에 ×1.5를 곱하고 소진한다. bias는 non-null 보장
        // (RollCells가 항상 새 PouchBias를 넘김 — BuildPouchBias가 null이어도 `?? new PouchBias()`).
        public static void ApplyReachBias(PouchBias bias, RunState run)
        {
            if (bias == null || run == null || string.IsNullOrEmpty(run.ReachBiasTag) || run.ReachBiasSpinsLeft <= 0) return;
            string tag = run.ReachBiasTag;
            foreach (var kv in Pouch.JackpotTag)
            {
                if (kv.Value != tag) continue;
                bias.Mul[kv.Key] = (bias.Mul.TryGetValue(kv.Key, out var m) ? m : 1.0) * 1.5;
            }
            run.ReachBiasSpinsLeft -= 1;
            if (run.ReachBiasSpinsLeft <= 0) { run.ReachBiasTag = null; run.ReachBiasSpinsLeft = 0; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 웹 파리티 P7-3 — 스핀 결산 후속 처리(웹 game.js:960-1138 §9.0 J1/§9.1 J2/§9.2 J3 + 배치F P6
        // 퍼펙트 드로우). SpinResolver.ResolveSpin이 mode/보스/dev_safe/dev_bell 보정까지 끝난 "최종"
        // gained(웹 지역변수 exp — r.stageExp에 아직 안 더해진 값)를 ref로 넘긴다. res.score/res.coins도
        // 직접 가산(res는 참조형이라 호출측이 이후 newScore 계산 시 자동 반영). notes는 SpinOutcome에
        // 합류할 배너 문자열. mods는 "이번 스핀에 실제로 쓰인" 인스턴스(호출측 지역변수, run.LastMods로
        // 아직 대입되기 전) — feverReachFix 읽기/쓰기가 정확히 웹 `r.lastMods`(스핀 시작 시 이미 이번
        // mods로 갱신됨, WEB_PARITY_DESIGN.md 이 슬라이스 주석 "매 스핀 새 mods라 사실상 항상 0으로
        // 리셋" 참고 — feverReachFix는 웹 원본부터 사실상 매 스핀 신선한 기본값만 관측되는 필드다. 그대로 전사)와
        // 동일 타이밍이 되도록 run.LastMods가 아니라 이 로컬 mods를 직접 읽고 쓴다. run.DeepMode가
        // 아니면 즉시 반환(무회귀).
        public static void ProcessDeepSpinFollowups(RunState run, Mods mods, SpinResult res, ref long gained, List<string> notes)
        {
            if (run == null || res == null || !run.DeepMode) return;

            // Opus급 자체검수 정정 — 웹 game.js:943 `r.stageExp += exp; r.score += res.score;`가 이미
            // 커밋된 "뒤"에 이 블록 전체(960-1138)가 실행된다. 이후 rareFirstScore/퍼펙트드로우/승격
            // 심볼(bell_ticket 등)은 전부 `r.stageExp +=`/`r.score +=`(런 누적치)만 건드리고, 웹 지역변수
            // `exp`/`res.score` 자체는 그 시점 이후 단 한 번도 재대입되지 않는다(고정값) — feverExpExtra/
            // feverScoreExtra/fjScoreBonus(피버 배율 계산 3곳)는 전부 이 "고정된 원본"을 기준으로 삼는다.
            // 이 C# 포트는 `gained`/`res.score`를 그 자체가 누적 델타 역할을 하도록 설계했으므로(호출측
            // ResolveSpin이 함수 종료 후 한 번에 run.StageExp/run.Score에 더함), 뒤이은 블록들이 이미
            // `gained`/`res.score`를 웹의 "고정 원본" 지분만큼 불려 놓은 상태라 — 피버 배율 3곳만큼은
            // 반드시 "함수 진입 시점 스냅숏"을 따로 떠서 읽어야 웹과 동일한 수치가 나온다(스냅숏 없이
            // 라이브 값을 읽으면 피버잭팟 등이 앞선 보너스 위에 중복 복리로 얹혀 웹보다 과다 지급된다).
            long expSnapshot = gained;
            long scoreSnapshot = res.score;

            // ── 희귀표본상자(sr_rare_case, rewardBonus 아님 — rareFirstScore) — 웹 game.js:960-968.
            // RunSymCounts는 이 시점까지 "이번 스핀 반영 전"(BumpSymCounts는 ResolveSpin의 상태 반영
            // 블록에서 이 함수 뒤에 실행) — "이번 런 처음 발견"과 동치 판정 가능.
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            if (sp.RareFirstScore > 0)
            {
                bool newRare = false;
                for (int i = 0; i < res.cells.Count; i++)
                {
                    var id = res.cells[i].sym.id;
                    if (id == "empty" || Pouch.RarityOf(id) != "희귀") continue;
                    if (!run.RunSymCounts.ContainsKey(id)) { newRare = true; break; }
                }
                if (newRare)
                {
                    long bonus = (long)sp.RareFirstScore;
                    res.score += bonus;
                    notes.Add($"🧰 희귀표본 첫 발견 점수 +{bonus}");
                }
            }

            // ── 배치F P6 퍼펙트 드로우 — 5칸 전부 동일 계열(base 환산), 빈칸 불성립, 스테이지 1회 —
            // 웹 game.js:985-998. ★클리어 판정 전이므로 run.Stage는 여전히 "이번" 스테이지.
            if (run.PerfectDrawStage != run.Stage && res.cells.Count > 0)
            {
                bool allReal = true;
                for (int i = 0; i < res.cells.Count; i++)
                    if (res.cells[i].sym == null || res.cells[i].sym.id == "empty") { allReal = false; break; }
                if (allReal)
                {
                    string f0 = Pouch.UpgradeParent.TryGetValue(res.cells[0].sym.id, out var p0) ? p0 : res.cells[0].sym.id;
                    bool allSame = true;
                    for (int i = 1; i < res.cells.Count; i++)
                    {
                        string fi = Pouch.UpgradeParent.TryGetValue(res.cells[i].sym.id, out var pi) ? pi : res.cells[i].sym.id;
                        if (fi != f0) { allSame = false; break; }
                    }
                    if (allSame)
                    {
                        run.Coins += 1;
                        run.PerfectDrawStage = run.Stage;
                        var fs = Symbols.ById(f0);
                        notes.Add($"🎯 퍼펙트 드로우! 5칸 모두 {(fs != null ? fs.emoji + fs.name : f0)} 계열 — 코인 +1");
                    }
                }
            }

            // ── §9.0 J1 잭팟 태그 단계 보상 신호 소비 — EXP/점수는 Evaluate()가 이미 res.exp/res.score에
            // 반영(=gained/res.score에 이미 녹아 있음) — 여기선 런 플래그·bias 예약 + 배너만. 웹 game.js:1000-1067.
            string jtBanner = "";
            if (res.jackpotStage != null)
            {
                string tLabel = SpinResolver.JackpotTagLabel(res.jackpotTagHit);
                if (res.jackpotStage == "combo")
                {
                    jtBanner = $"🎯 {tLabel} 콤보! — EXP+8";
                }
                else if (res.jackpotStage == "reach")
                {
                    run.ReachBiasTag = res.jackpotTagHit;
                    run.ReachBiasSpinsLeft = 1;
                    jtBanner = $"🎯 {tLabel} 리치! — 다음 스핀 해당 태그 ×1.5";
                }
                else if (res.jackpotStage == "jackpot")
                {
                    run.JackpotPrismPending = true;
                    jtBanner = $"🎰 {tLabel} 잭팟!! — 다음 주머니 오퍼 프리즘 보장";
                }
            }

            // ── §9.2 J3 승격/보정 심볼 소모 — 웹 game.js:1019-1067 ──
            if (res.jackpotStage == "reach" && res.jackpotTagHit == "bell" && res.bellCount >= 4)
            {
                int bellTicketN = run.Pouch.TryGetValue("bell_ticket", out var bn) ? bn : 0;
                if (bellTicketN > 0 && run.BellTicketUses < 2)
                {
                    gained += 30; res.score += 1500; run.BellTicketUses += 1;
                    run.Pouch["bell_ticket"] = bellTicketN - 1;
                    if (run.Pouch["bell_ticket"] <= 0) run.Pouch.Remove("bell_ticket");
                    run.JackpotPrismPending = true;
                    jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + $"🎟 종소리티켓 — 종 4개 잭팟 승격! (런 {run.BellTicketUses}/2)";
                }
            }
            if (res.jackpotStage == "reach" && res.hasJpTicket)
            {
                int jpTicketN = run.Pouch.TryGetValue("jackpot_ticket", out var jn) ? jn : 0;
                if (jpTicketN > 0 && run.JpTicketUses < 2)
                {
                    gained += 30; res.score += 1500; run.JpTicketUses += 1;
                    run.Pouch["jackpot_ticket"] = jpTicketN - 1;
                    if (run.Pouch["jackpot_ticket"] <= 0) run.Pouch.Remove("jackpot_ticket");
                    run.JackpotPrismPending = true;
                    jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + $"🎟 잭팟티켓 — 리치 → 잭팟 승격! (런 {run.JpTicketUses}/2)";
                }
            }
            if (res.jackpotStage == "reach" && res.hasReachMark && !run.ReachMarkUsed)
            {
                double baseProb = 0.30 + (mods != null ? mods.feverReachFix : 0.0);
                if (run.Rng.NextDouble() < baseProb)
                {
                    gained += 30; res.score += 1500; run.ReachMarkUsed = true;
                    run.JackpotPrismPending = true;
                    jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + $"🎯 리치표식 — 부족 1칸 보정 잭팟 승격! (확률 {Math.Round(baseProb * 100)}%)";
                }
            }
            if (res.jackpotStage == "reach" && res.hasRetryReel && !run.RetryReelUsed)
            {
                run.RetryReelPending = true; run.RetryReelUsed = true;
                jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + "🔁 재도전릴 — 다음 스핀 1칸 재굴림 예약";
            }
            if (res.jackpotCrownSignal && !run.JackpotCrownUsed)
            {
                run.JackpotCrownUsed = true;
                run.JackpotCrownPending = true;
            }

            // ── §9.1 J2 피버 게이지 충전 + 진입/효과/종료 — 웹 game.js:1069-1138 ──
            string feverBanner = "";
            int feverDeltaEff = res.feverDelta;
            if (run.FeverSpins > 0)
            {
                if (res.hasBellFest && res.jackpotTagHit == "bell" && res.jackpotStage != null)
                {
                    long bellExpBonus = 0, bellScoreBonus = 0;
                    if (res.jackpotStage == "combo") bellExpBonus = 8;
                    else if (res.jackpotStage == "reach") bellScoreBonus = (res.jackpotSym != null ? 0 : 300) + (res.echoTriggered ? 200 : 0);
                    else if (res.jackpotStage == "jackpot") { bellExpBonus = 30; bellScoreBonus = res.jackpotSym != null ? 0 : 1500; }
                    double festMulExtra = Pouch.BellFestMul - 1.0; // 0.5
                    long festExpExtra = (long)Math.Floor(bellExpBonus * festMulExtra);
                    long festScoreExtra = (long)Math.Floor(bellScoreBonus * festMulExtra);
                    int festFeverExtra = (int)Math.Floor(feverDeltaEff * festMulExtra);
                    if (festExpExtra > 0) gained += festExpExtra;
                    if (festScoreExtra > 0) res.score += festScoreExtra;
                    if (festFeverExtra > 0) feverDeltaEff += festFeverExtra;
                    if (festExpExtra > 0 || festScoreExtra > 0 || festFeverExtra > 0)
                        jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + $"🎊 축제종 ×{FmtMul2(Pouch.BellFestMul)}";
                }
                long feverExpExtra = (long)Math.Floor(expSnapshot * (Pouch.FeverExpMul - 1.0));
                if (feverExpExtra > 0) gained += feverExpExtra;
                long feverScoreExtra = (long)Math.Floor(scoreSnapshot * (Pouch.FeverScoreMul - 1.0));
                if (feverScoreExtra > 0) res.score += feverScoreExtra;
                if (res.jackpotStage == "jackpot" && res.jackpotTagHit != null)
                {
                    long fjScoreBonus = (long)Math.Floor(scoreSnapshot * (Pouch.FeverJackpotScoreMul - 1.0));
                    if (fjScoreBonus > 0) res.score += fjScoreBonus;
                    run.FeverJackpotPrism = true;
                    jtBanner = (jtBanner.Length > 0 ? jtBanner + " · " : "") + "🔥 피버잭팟!! — 점수×2 추가 · 오퍼 프리즘+1";
                }
                if (mods != null) mods.feverReachFix = Pouch.FeverReachFixAmount;
                run.FeverSpins -= 1;
                if (run.FeverSpins <= 0)
                {
                    run.FeverSpins = 0;
                    feverBanner = "🔥 피버 종료!";
                    if (mods != null) mods.feverReachFix = 0;
                }
                else
                {
                    feverBanner = $"🔥 피버 {run.FeverSpins}스핀 남음";
                }
            }
            if (feverDeltaEff > 0)
            {
                run.FeverGauge += feverDeltaEff;
                if (run.FeverGauge >= Pouch.FeverMax)
                {
                    run.FeverGauge = 0;
                    run.FeverSpins = Pouch.FeverSpins;
                    feverBanner = $"🔥 피버 타임! {Pouch.FeverSpins}스핀";
                    if (mods != null) mods.feverReachFix = Pouch.FeverReachFixAmount;
                }
            }

            if (jtBanner.Length > 0) notes.Add(jtBanner);
            if (feverBanner.Length > 0) notes.Add(feverBanner);
        }

        // 배율 표기 — 소수 2자리·끝0제거(SpinResolver.FmtMul과 동일 규칙, private이라 재사용 대신 축소
        // 복제 — 이 파일은 축제종 배율(1.5) 표기 1곳에서만 쓴다).
        private static string FmtMul2(double v)
        {
            string s = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
    }

    // 배치F P2(웹 `r.deepPity = {id, spinsLeft}`) — 신규 심볼 등장 보장 상태. 참조형(클래스)이라
    // RunState.DeepPity는 null 가능(웹 `r.deepPity = null` 초기값과 동일 관례).
    public sealed class DeepPityState
    {
        public string Id;
        public int SpinsLeft;

        public DeepPityState(string id, int spinsLeft)
        {
            Id = id;
            SpinsLeft = spinsLeft;
        }
    }

    // 웹 game.js:295-304 `r.deepStats` — 심화 전용 추적(랭킹 오염 방지·요약 + P7-4 심화 업적 카운터
    // 소스, 전부 심화 런에서만 존재). 웹 필드 그대로 전사 — 이번 슬라이스(P7-1)는 이 클래스의 존재와
    // RunController가 심화 런 시작 시 MaxTotal을 초기화하는 것까지만 담당한다(작업 지시 8번 "deepStats
    // 상태 골격만"). RewardsPicked/Repairs/BossClears 증가, Compress*/Cherry50.../Skull0... 플래그
    // 세팅, RaresSeen/LegendsSeen 수집은 전부 P7-2/3(보상/정비소/보스클리어 훅이 아직 없음) 이후에
    // 이 필드들을 갱신하기 시작하면 된다 — StatTracker/AchievementEngine 소비(P7-4)도 마찬가지.
    public sealed class DeepStats
    {
        public int RewardsPicked;
        public int Repairs;
        public int MaxTotal; // 이번 런 최대 총량(대형주머니 업적) — 런 시작 시 시작 덱 총량으로 초기화.
        public int BossClears; // 심화 보스 클리어 수(심볼마스터)
        public bool Compress95Clear;
        public bool Compress85BossClear;
        public bool Cherry50BossClear;
        public bool Skull40BossClear;
        public bool Gem50Score30kBoss;
        public bool Crown2BossClear;
        public bool BalanceBossClear;
        public bool Skull0BossClear;
        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js:1538 `r.deepStats.autoDecays`)
        // — §3 V3P3 자동 소멸 발동 횟수(예고 제외, 실제 심볼 제거만 카운트).
        public int AutoDecays;
        public readonly HashSet<string> RaresSeen = new HashSet<string>();
        public readonly HashSet<string> LegendsSeen = new HashSet<string>();
    }
}
