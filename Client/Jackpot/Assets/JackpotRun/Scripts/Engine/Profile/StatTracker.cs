using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    // StatTracker — Kotlin track()/bumpAch() 호출 지점 전수 이식 (kotlin-reference\game\SlotV2Service.kt,
    // 커밋 c73452c). RunController.Do(...)/생성자가 반환하는 RunEvent 목록 + 그 시점의 RunState를 받아
    // PlayerProfile.Stats를 Kotlin과 동일한 "사건→키→증분" 규칙으로 갱신한다.
    //
    // ── 호출 지점 전수표 (열: 우리 RunEvent.type | Kotlin 함수:라인 | 갱신 키 | 연산) ───────────────
    //
    // RUN_STARTED
    //   launchRun L481-506 (dvuse L499-500, track 호출)
    //     dvuse_dev_<run.Device>, dvuse_dev_<run.Device2>          inc 1 (해당 슬롯 장착 시)
    //   seen_$startPerks(honor 시작증강, L497) — 이 값도 우리 엔진에선 생성자가 낸 PERK_GRANTED 이벤트로
    //     관측 가능하다(RunController 생성자, honor 분기) → PERK_GRANTED 공용 핸들러(seen_<perkId>)가 그대로
    //     처리한다(아래 PERK_GRANTED 절 참조), 이 사건 전용 코드는 없다.
    //   [미구현 — 무해 확인] seen_<charId>/seen_<machineId>/seen_<device>/seen_<device2> — 도감 발견기록.
    //     Shop.PerkUnlocked는 "seen_" + Perk.id(증강/유물/저주)만 검사한다 — 캐릭/머신/장치 id는 Perks.ById로
    //     조회되지 않으므로 이 seen_ 키들은 어떤 게이트에도 관여하지 않는다(Characters/Machines 해금은
    //     unlockReq/StatReq, Devices 해금은 unlockAch 업적 — 둘 다 seen_ 무관). 156-key 사전(03_meta §5.3)
    //     에도 없고 어떤 AchDef.req도 참조하지 않는다(전수 확인) — 순수 표시용 도감 기록이라 미구현.
    //
    // SPIN_RESULT / POST_SPIN / REVIVED / STAGE_CLEARED(실제 스핀) / GAME_OVER(실제 스핀)
    //   handleSpin L511-715, incMap 구성 L644-678 · bumpAch 호출 L679 (ApplySpinIncrements)
    //     totalSpins                                                inc 1 (매 스핀 무조건)
    //     bookTotal/starTotal/gemTotal/skullTotal/coinTotal          inc n (이번 스핀 등장수, n>0일 때만, L648)
    //     cherryTotal/crownTotal (bumpAch 위치인자 cherry/crown)     inc n (전용 컬럼, L645-646/679)
    //     set4Plus                                                  inc 1 (res.bestSetCount>=4, L649)
    //     focusUses/lastUses/allinWins                              inc 1 (모드별, allin은 skulls<2, L650)
    //     cmdCoin_focus/cmdCoin_pray/cmdCoin_allin/cmdCoin_last      inc cmdCost (cmdCost>0일 때만, L651-660)
    //     cmdCoinTotal                                               inc cmdCost
    //     wildTotal/seedTotal/diceTotal/keyTotal/flameTotal/         inc n (특수심볼 등장수>0, L662-667)
    //       magnetTotal/bombTotal
    //     jackpots (bumpAch 위치인자 jackpot, 전용 컬럼)               inc 1 (jackpotSym!=null, L645/679)
    //     crownJackpots                                              inc 1 (jackpotSym=="crown", L669)
    //     wildJackpots                                               inc 1 (잭팟 && 와일드 포함, L670)
    //     allinBusts                                                 inc 1 (ALLIN && skulls>=2, L672)
    //     maxSkullSpin/maxCoinSpin/maxCherrySpin/maxBookSpin/         setMax(이번 스핀 등장수, n>0, L674-678)
    //       maxGemSpin
    //
    // STAGE_CLEARED (실제 스핀이 클리어를 낸 경우 위 항목도 함께 적용된 뒤 아래 추가)
    //   clearStage L820-984, bumpAch 호출 L967 (ApplyClearTracking)
    //     bestStage                                                  setMax(clearedStage) — stageReached 인자
    //     lastSpinClears                                             inc 1 (newIdx>=spins) — lastClear 인자
    //     exactClears                                                inc 1 (newExp==quota) — exact 인자
    //     bossClears                                                 inc 1 (boss) — boss 인자
    //   clearInc L895-913
    //     closeClears                                                inc 1 (leftover<=10)
    //     prayClears / lastClears / bossAllinClear                   inc 1 (usedCmds PRAY/LAST, boss+ALLIN — RunScratch 재구성, 아래 절 참조)
    //     minimalistS10                                              inc 1 (relicN<=3 && stage>=10)
    //     richBossClears                                             inc 1 (boss && coinsAtClear>=50)
    //     noItemS8                                                   inc 1 (stage>=8 && !usedItemThisRun)
    //     noShopS10                                                  inc 1 (stage>=10 && !usedCmds.Contains("RUNSHOP"))
    //     curseBossClears                                            inc 1 (boss && curseCount>=3)
    //   addAch4ClearTracking L773-817
    //     bossClear_<bossId> / bossCounterClear_<bossId>              inc 1 (보스별 + 카운터조건, L787-804)
    //     bossNoItemClears / bossNoDeviceClears / bossOverkillClears  inc 1 (boss 한정, L805-808)
    //     bossStreak3                                                 setMax(1) (boss && stage>=15, L810)
    //     zeroCoinClears                                              inc 1 (coinsAtClear<=0, 보스 무관, L814)
    //     debtBossClears                                              inc 1 (boss && inDebt, L816)
    //     noPrismBestStage / noRelicBestStage / noGoldBestStage       setMax(clearedStage) (해당 티어 0개, L783-785)
    //     basicOnlyBestStage                                          setMax(clearedStage) (novice+basic, L786)
    //   clearSetMax L916-937
    //     curseMax / relicsMax                                        setMax(현재 보유수)
    //     cstage_<charId> / mstage_<machineId> / bc_<char>_<machine>  setMax(clearedStage)
    //     maxRunJackpots / maxOverPct                                 setMax(runJackpots / overPct)
    //     noDevStage / noItemMaxS / curse5Stage                       setMax(clearedStage) (조건부)
    //     dvstage_dev_<mainDevice> / dvstage_dev_<device2>            setMax(clearedStage) (장착 시)
    //     noCommandBestStage / noRerollBestStage                      setMax(clearedStage) (runUsedCmd/runRerolled==0)
    //   evalThemeBuilds 호출 L960-961 (정의는 SlotV2Engine.kt L1392-1433, THEME_BUILDS 카탈로그 L1277-1308)
    //     bld_<id> (25종)                                             setMax(1) (조건 충족 시)
    //
    // GAME_OVER (실제 스핀이 게임오버를 낸 경우 위 SPIN 항목도 함께 적용된 뒤 아래 추가; Continue(포기)로
    //            즉시 게임오버가 된 경우 spin 이벤트가 없어 SPIN 항목은 생략됨 — Kotlin도 동일, §3-C step4)
    //   gameOver L2003-2085, bumpAch 호출 L2048 (ApplyGameOverTracking)
    //     runs                                                        inc 1 — runDone 인자
    //     bestStage                                                   setMax(run.stage) — stageReached 인자
    //     bestScore                                                   setMax(finalScore) — scoreReached 인자
    //   goInc L2046-2047
    //     prayFails                                                   inc 1 (usedCmds PRAY로 게임오버 — RunScratch)
    //   goSetMax L2018-2027
    //     devicesOwned                                                setMax(해금+영구보유 장치 수, L2012/2019 —
    //       ComputeDevicesOwned가 AchievedIds/OwnedDevices뿐 아니라 "지금 이 시점 Stats로 해당 장치의
    //       unlockAch 업적 req가 충족되는지"도 함께 본다 — M4, Opus 1차 검수 반영, 이번 런 취득분 1런
    //       지연 버그 수정. WEB_PARITY P3-2부터 unlockAch는 업적 id를 직접 가리킨다(구 lic_* 접두
    //       특례 제거) — ComputeDevicesOwned 함수 각주 참조)
    //     curseMax / relicsMax                                        setMax(현재 보유수)
    //     cstage_<charId> / mstage_<machineId> / bc_<char>_<machine>  setMax(run.stage)
    //     maxRunJackpots                                               setMax(runJackpots)
    //     noDevStage / noItemMaxS / curse5Stage                       setMax(run.stage) (조건부, L2025-2027)
    //   evalThemeBuilds 호출 L2042-2044 (isBossClear/isLastSpinClear/clearSpin* 등은 게임오버라 전부 기본값)
    //     bld_<id>                                                     setMax(1) (조건 충족 시)
    //
    // NODE_RESOLVED (node==Gamble)  — handleNodeSelect L1243, track 호출
    //   gambles                                                        inc 1 (승패 무관, 도박장 노드 이용 자체)
    //
    // NODE_RESOLVED (node==Event, EVENT 10종표 case 7/8) — handleNodeSelect L1189-1245, grantedPerks 누적 후
    //                                                       L1244 track 호출 (MarkPerkSeen)
    //   seen_<relicGrantedId> / seen_<augmentGrantedId>                inc 1 (case 7 유물발견 / case 8 증강발견
    //     — case 8의 25% "🎉특별 이벤트" 분기는 relicGrantedId도 함께 채워지므로 둘 다 마킹된다, L1214-1227)
    //   [원본 그대로 — 마킹 안 함] node==Risk(TryGrantRisk)의 augmentGrantedId — Kotlin handleNodeSelect의
    //     RISK 분기(L1160-1174)는 run2를 직접 구성해 즉시 Reply.Msg를 반환하고, EVENT 분기의 grantedPerks/
    //     L1244 track 호출을 전혀 거치지 않는다(코드 흐름상 도달 불가) — 즉 원본에서 RISK로 얻은 프리즘/골드
    //     증강은 seen_ 처리가 안 된다(재추첨/그랜드파더 해금에 기여하지 않음). [원본 버그로 보이나 확인된
    //     동작이라 그대로 유지] — node==Risk일 때는 augmentGrantedId가 있어도 마킹하지 않는다.
    //   [원본 그대로 — 마킹 안 함] node==Curse의 curseGrantedId — 저주는애초 Shop.PerkUnlocked 게이트를
    //     타지 않는(TryGrantCurse/TryGrantRisk 둘 다 저주 풀을 직접 순회, 미해금 필터 없음) 콘텐츠라 Kotlin도
    //     추적하지 않는다(L1141-1157에 track 호출 없음).
    //
    // PERK_GRANTED (EVENT_AUGMENT/EVENT_RELIC 오퍼에서 선택 — NodeEvents.PickOffer, 또는 RunController
    // 생성자의 honor 시작증강 지급) — handlePerkPick L1371-1388, bumpAch 호출 L1385 (+ 위 RUN_STARTED 절의
    // seen_$startPerks, L497)
    //   prismPicks                                                     inc 1 (선택한 퍽 tier==PRISM) — prism 인자
    //   seen_<perkId>                                                  inc 1 (L1383, Shop.PerkUnlocked
    //     1순위 그랜드파더 게이트 — 한 번 획득한 증강/유물은 이후 이 프로필의 모든 런에서 해금 취급된다)
    //   [단순화 — 무해] Kotlin L1384의 activeSets(perkList).forEach{seen_<setId>}는 이식하지 않았다. Sets는
    //     Perks.All(증강/유물/저주)과 별도 콘텐츠 타입이라 Shop.PerkUnlocked가 "seen_"+SetEffect.id를
    //     검사할 일이 없다(어떤 게이트도 seen_<setId>를 읽지 않음, 코드 전수 확인) — 순수 도감 표시용이라
    //     생략해도 해금 동작에 영향이 없다.
    //
    // PERK_HELD (🗂️보류파일, dev_holdfile) — handleHoldAug L1336-1351, track 호출 L1349
    //   deviceUses                                                     inc 1
    //
    // RETAKE_EMPTY (🔁재추첨, dev_retake — 재뽑을 후보 없음) — handleRetake L1354-1369, track 호출 L1365
    //   deviceUses                                                     inc 1
    //
    // PERK_OFFER (offerRetake==true — 🔁재추첨 성공, 새 오퍼 발급) — handleRetake L1354-1369, track 호출 L1365
    //   deviceUses                                                     inc 1 (offerRetake==false인 최초
    //     ChooseNode 오퍼는 미증가 — RunEvent.offerRetake 필드로 두 경로가 구분되어 더 이상 모호하지 않다.
    //     이전 보고의 "커버리지 갭"은 RunController.cs/NodeEvents.cs에 offerRetake 필드가 추가되며 해소됨.)
    //
    // SHOP_PURCHASED — handleShop L1629-1681, track 호출 L1678
    //   shopBuys                                                       inc 1
    //   seen_<shopBought.id>                                           inc 1 (kind=='A'||'R'인 경우만,
    //     L1677 "p[0]==R||A" 조건 — 아이템(kind=='I') 구매는 seen_ 대상 아님)
    //
    // ITEM_USED — handleItem L1536-1603, track 호출 L1573(재시험)/L1596(그 외)
    //   itemsUsed                                                      inc 1
    //
    // DEVICE_ARMED / DEVICE_PEEK — handleDevice L1684-1751, track 호출 L1716(PEEK)/L1749(ARMED)
    //   deviceUses                                                     inc 1
    //
    // DEVICE_MANIP_RESULT, 또는 STAGE_CLEARED/GAME_OVER 중 deviceId 필드가 채워진 경우(MANIP이 직접
    // 클리어/게임오버를 낸 경우) — handleManipulator L1815-1893, track 호출 L1829-1832
    //   deviceUses                                                     inc 1 (dev.id != "GREROL")
    //   rerollUses / pinUses                                           inc 1 (dev_reroll / dev_pin 한정)
    //   handleGamblerReroll L1760-1812, track 호출 L1764 (deviceId=="GREROL")
    //     rerollUses                                                   inc 1 (deviceUses는 증가 안 함 — 장치 아님)
    //
    // ── PRAY/LAST/ALLIN "이번 스테이지 사용 여부" 재구성 (RunScratch) ────────────────────────────────
    // Kotlin은 clearStage/gameOver 시점에 run.usedCmds(CSV) 안에 "PRAY"/"LAST"/"ALLIN" 마커가 남아있는지를
    // 직접 검사한다(L897-900, L2047). 이 마커는 스테이지 내내 누적되다가 StageFlow.ClearStage가 클리어
    // *순간* "RUNSHOP"/"RUNORACLE"만 남기고 필터링한다(RunState.UsedCmds.RemoveWhere) — 그런데 StatTracker는
    // RunController.Do()가 반환한 뒤(=필터링이 이미 끝난 뒤) RunState를 관찰하므로, STAGE_CLEARED 이벤트
    // 시점의 RunState.UsedCmds에서는 이 마커를 더 이상 볼 수 없다(RunEvent 자체에도 "이번 스테이지 내내
    // 사용한 모드 목록" 같은 필드가 없다). 이는 우회가 아니라 이미 노출된 데이터(각 스핀 RunEvent의
    // spin.mode)를 스테이지 경계에서 StatTracker가 직접 누적하는 것으로 해결한다 — 매 SPIN_RESULT/
    // POST_SPIN/REVIVED/STAGE_CLEARED/GAME_OVER 이벤트의 spin.mode를 볼 때마다 RunScratch에 기록해 두고,
    // 클리어/게임오버 시점에 그 값을 읽은 뒤 다음 스테이지를 위해 리셋한다. dev_pin 사용 여부(빌드도감
    // bld_pinned_fate 판정용, Kotlin "고정" 마커)도 동일한 이유로 RunScratch에 재구성한다("RUNSHOP"/
    // "RUNORACLE"는 ClearStage가 필터링에서 제외해 주므로 RunState.UsedCmds에서 바로 읽어도 안전하다).
    //
    // ── 커버리지 요약 (전체 156 고유 key, 03_meta §5.3 알파벳순 기준) ───────────────────────────────
    // 156개 전부 관측 가능한 사건에 연결했다 — 이 파일이 StatTracker.Apply 호출마다 직접 inc/setMax
    // 한다(482종 Kotlin 스냅샷 시절엔 그중 12종(lic_dev_*)·bldCat_*/bldTotal/bldAllBasic/bldAllMaster를
    // AchievementEngine.ComposeStat이 즉석 파생했었지만, WEB_PARITY P3-2 업적 34종 교체로 그 파생 소비처
    // 자체가 사라져 ComposeStat에서 제거됐다 — AchievementEngine.cs 헤더 각주 참조, 원시 카운터 수집
    // 자체는 이 파일에서 계속 그대로 한다). 새로 추가된 "graduations"/"playerLevel"도 이 파일이 직접
    // 기록한다(웹 파리티 P3-2, ApplyClearTracking/ApplyGameOverTracking 각 절 참조).
    //
    // ── seen_<perkId> (Shop.PerkUnlocked 1순위 게이트, 156-key 사전 밖이지만 해금 동작에 실질적 영향) ──
    // 156-key 사전은 "업적 판정" 키만 나열한 목록이라 seen_*를 포함하지 않지만, Shop.PerkUnlocked(Shop.cs
    // L70-78)가 "seen_"+Perk.id를 그랜드파더 게이트 1순위로 검사하므로 이걸 추적하지 않으면 런을 넘나드는
    // 퍽 해금 동작이 원본과 갈라진다(Fable 지적, 2026-07-31 반영) — 그래서 이 값도 이 파일이 추적한다.
    // 증강/유물/저주 id 전부에 대해 캐릭/머신/장치/아이템 id의 seen_(도감 전용, 게이트 무관)와 달리
    // *반드시* 원본과 동일한 사건에서만 마킹해야 한다 — 위 NODE_RESOLVED/PERK_GRANTED/PERK_OFFER/
    // SHOP_PURCHASED 절의 "원본 그대로" 각주(특히 RISK 노드는 마킹 안 함, CURSE는 애초 게이트 대상 아님)
    // 참조. 호출측이 PlayerProfile.Stats를 RunController 생성자의 stat 인자로 "같은 딕셔너리 참조"로
    // 넘기면(RunController.cs L93 계약 그대로) 이 마킹이 같은 런 안에서도 즉시 게이트에 반영된다.
    //   - seen_<charId>/seen_<machineId>/seen_<deviceId>/seen_<itemId>/seen_<setId>: 미구현(무해 확인,
    //     위 RUN_STARTED/PERK_GRANTED 절 각주 참조 — 어떤 게이트도 읽지 않는 순수 도감 표시용).
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public static class StatTracker
    {
        // 스테이지 경계에서 사라지는 usedCmds 마커(PRAY/LAST/ALLIN/dev_pin 사용여부)를 RunEvent만으로
        // 재구성하기 위한 런 1개 범위의 임시 상태. 호출측이 런 시작 시 1개 생성해 그 런의 모든 Apply
        // 호출에 동일 인스턴스를 넘겨야 한다(PlayerProfile과 달리 영속하지 않음 — 런이 끝나면 버린다).
        public sealed class RunScratch
        {
            internal bool PrayUsedThisStage;
            internal bool LastUsedThisStage;
            internal bool AllinUsedThisStage;
            internal bool PinUsedThisStage;

            public void ResetStage()
            {
                PrayUsedThisStage = false;
                LastUsedThisStage = false;
                AllinUsedThisStage = false;
                PinUsedThisStage = false;
            }
        }

        public static void Apply(PlayerProfile profile, RunState run, IReadOnlyList<RunEvent> events, RunScratch scratch)
        {
            if (profile == null || run == null || events == null || scratch == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var next = (i + 1 < events.Count) ? events[i + 1] : null;
                ApplyOne(profile, run, events[i], next, scratch);
            }
        }

        // next: 같은 Do() 호출 배치 안의 바로 다음 이벤트(없으면 null) — ITEM_USED가 grad_ring/gold_grad_bell
        // 즉시클리어를 유발했는지(다음 이벤트==STAGE_CLEARED) 판별하는 데만 쓰인다(M2, 아래 ITEM_USED 절 참조).
        private static void ApplyOne(PlayerProfile p, RunState run, RunEvent e, RunEvent next, RunScratch scratch)
        {
            switch (e.type)
            {
                case "RUN_STARTED":
                    OnRunStarted(p, run);
                    break;

                case "SPIN_RESULT":
                case "POST_SPIN":
                case "REVIVED":
                    ApplySpinIncrements(p, e.spin, scratch);
                    break;

                case "STAGE_CLEARED":
                {
                    bool realSpin = string.IsNullOrEmpty(e.deviceId) && e.spin?.result != null;
                    if (realSpin) ApplySpinIncrements(p, e.spin, scratch);
                    else if (!string.IsNullOrEmpty(e.deviceId)) ApplyManipDeviceInc(p, e.deviceId, scratch);
                    ApplyClearTracking(p, run, e.clear, e.spin, scratch);
                    break;
                }

                case "GAME_OVER":
                {
                    bool realSpin = string.IsNullOrEmpty(e.deviceId) && e.spin?.result != null;
                    if (realSpin) ApplySpinIncrements(p, e.spin, scratch);
                    else if (!string.IsNullOrEmpty(e.deviceId)) ApplyManipDeviceInc(p, e.deviceId, scratch);
                    ApplyGameOverTracking(p, run, e.failure, scratch);
                    break;
                }

                case "NODE_RESOLVED":
                    if (e.node == NodeKind.Gamble) p.Inc("gambles");
                    // EVENT 10종표 case 7/8 유물·증강 무료획득만 seen_ 마킹(Kotlin L1244) — RISK(case에
                    // 도달하지 않는 별도 조기반환 경로, L1160-1174)와 CURSE(게이트 대상 아님)는 제외한다.
                    // 위 헤더 "NODE_RESOLVED (node==Event)" 절 참조.
                    if (e.node == NodeKind.Event)
                    {
                        MarkPerkSeen(p, e.relicGrantedId);
                        MarkPerkSeen(p, e.augmentGrantedId);
                    }
                    // WEB_PARITY P1 ④: 장치 영구 지급(EVENT-6 장치획득 / DEVICE 노드) — 엔진은
                    // Engine/Profile을 모르므로(설계 원칙 6) 이 글루 레이어가 RunEvent.deviceGrantedId를
                    // 보고 PlayerProfile.OwnedDevices에 반영한다(웹 profile.ownedDevices.push 대응).
                    if (!string.IsNullOrEmpty(e.deviceGrantedId)) p.OwnedDevices.Add(e.deviceGrantedId);
                    break;

                case "PERK_GRANTED":
                {
                    var perk = Perks.ById(e.perkId);
                    if (perk != null && perk.tier == Tier.PRISM) p.Inc("prismPicks");
                    MarkPerkSeen(p, e.perkId); // Kotlin handlePerkPick L1383 (+ RUN_STARTED의 honor 시작증강도 이 이벤트로 온다)
                    break;
                }

                case "PERK_HELD":
                    p.Inc("deviceUses"); // dev_holdfile("보류") — Kotlin handleHoldAug L1349
                    break;

                case "RETAKE_EMPTY":
                    p.Inc("deviceUses"); // dev_retake("재추첨") 실패 케이스 — Kotlin handleRetake L1365
                    break;

                case "PERK_OFFER":
                    if (e.offerRetake) p.Inc("deviceUses"); // dev_retake("재추첨") 성공 케이스 — Kotlin handleRetake L1365
                    break;

                case "SHOP_PURCHASED":
                    p.Inc("shopBuys");
                    // 상점에서 산 증강/유물(kind 'A'/'R')만 seen_ 마킹(Kotlin L1677) — 아이템(kind 'I')은 대상 아님.
                    if (e.shopBought != null && (e.shopBought.kind == 'A' || e.shopBought.kind == 'R'))
                        MarkPerkSeen(p, e.shopBought.id);
                    break;

                case "ITEM_USED":
                {
                    // [원본 동작] Kotlin handleItem L1584-1594: grad_ring/gold_grad_bell 사용이 즉시클리어를
                    // 유발하면(stageExp >= instantQuota) clearStage(...)로 조기 반환되어 L1596의
                    // track("itemsUsed"...) 호출에 도달하지 못한다 — itemsUsed가 증가하지 않는다. 이
                    // 엔진에서는 ItemUse.Use()가 같은 Do() 배치에서 [ITEM_USED, STAGE_CLEARED] 두 이벤트를
                    // 순서대로 반환하므로, "다음 이벤트가 STAGE_CLEARED"인 그 두 아이템 조합으로 조기 반환
                    // 상황을 재구성해 동일하게 미집계한다(M2, Opus 1차 검수 반영, 2026-07-31).
                    bool instantClearNoTrack = (e.itemId == "grad_ring" || e.itemId == "gold_grad_bell")
                                                && next != null && next.type == "STAGE_CLEARED";
                    if (!instantClearNoTrack) p.Inc("itemsUsed");
                    break;
                }

                case "DEVICE_ARMED":
                case "DEVICE_PEEK":
                    p.Inc("deviceUses");
                    break;

                case "DEVICE_MANIP_RESULT":
                    ApplyManipDeviceInc(p, e.deviceId, scratch);
                    break;

                default:
                    break; // REJECTED / SHOP_OFFER / SHOP_REROLLED / SHOP_LEFT — 스탯 영향 없음
            }
        }

        // ── 능동/조작 장치 사용 카운트 — HandleManip(dev.id)/GamblerReroll("GREROL") 공용 ──────────
        // scratch: dev_pin 사용 시 PinUsedThisStage를 세운다(H1, Opus 1차 검수 반영, 2026-07-31) — 이
        // 마커가 없으면 bld_pinned_fate가 영구 미달성이었다(RunScratch가 생성만 되고 아무도 true로
        // 만들지 않던 버그 — bdx_master_combo/bdx_total25/bdx_all_master 3종의 조합형 카테고리 마스터/
        // 총합 달성도 이 경로로 막혀 있었다).
        private static void ApplyManipDeviceInc(PlayerProfile p, string deviceId, RunScratch scratch)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            if (deviceId == "GREROL")
            {
                p.Inc("rerollUses"); // 도박꾼 무료재굴림 — 장치 아님(deviceUses 미증가), Kotlin L1764
                return;
            }
            p.Inc("deviceUses");
            if (deviceId == "dev_reroll") p.Inc("rerollUses");
            else if (deviceId == "dev_pin")
            {
                p.Inc("pinUses");
                scratch.PinUsedThisStage = true; // Kotlin "고정" 마커(usedCmds) 상당 — bld_pinned_fate 판정용
            }
        }

        // 그랜드파더 게이트 마킹 — Shop.PerkUnlocked(Shop.cs L70-78)의 1순위 검사("seen_"+Perk.id > 0이면
        // 무조건 해금)가 읽는 키. perkId가 비어있으면 아무것도 하지 않는다(null 그랜트 안전 가드).
        private static void MarkPerkSeen(PlayerProfile p, string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return;
            p.Inc("seen_" + perkId);
        }

        private static void OnRunStarted(PlayerProfile p, RunState run)
        {
            if (!string.IsNullOrEmpty(run.Device)) p.Inc("dvuse_" + run.Device);
            if (!string.IsNullOrEmpty(run.Device2)) p.Inc("dvuse_" + run.Device2);
        }

        // ── handleSpin의 incMap/spinMax/cherry/crown 이식 (Kotlin L644-679) ────────────────────────
        private static readonly (Sp sp, string key)[] SpecialTotalKeys =
        {
            (Sp.WILD, "wildTotal"), (Sp.SEED, "seedTotal"), (Sp.DICE, "diceTotal"), (Sp.KEY, "keyTotal"),
            (Sp.FLAME, "flameTotal"), (Sp.MAGNET, "magnetTotal"), (Sp.BOMB, "bombTotal"),
        };

        private static readonly string[] IncTotalSymbolIds = { "book", "star", "gem", "skull", "coin" };
        private static readonly string[] MaxSpinSymbolIds = { "skull", "coin", "cherry", "book", "gem" };

        private static void ApplySpinIncrements(PlayerProfile p, SpinOutcome spin, RunScratch scratch)
        {
            if (spin == null) return;
            var res = spin.result;
            if (res == null) return; // 아이템 즉시클리어의 더미 outcome(result=null) — Kotlin도 이 경로에선 incMap을 안 만든다.

            p.Inc("totalSpins");

            long CellCount(string id) => res.cells.Count(c => c.sym.id == id);

            for (int i = 0; i < IncTotalSymbolIds.Length; i++)
            {
                long n = CellCount(IncTotalSymbolIds[i]);
                if (n > 0) p.Inc(IncTotalSymbolIds[i] + "Total", n);
            }
            p.Inc("cherryTotal", CellCount("cherry"));
            p.Inc("crownTotal", CellCount("crown"));

            if (res.bestSetCount >= 4) p.Inc("set4Plus");

            switch (spin.mode)
            {
                case SpinMode.Focus:
                    p.Inc("focusUses");
                    break;
                case SpinMode.Last:
                    p.Inc("lastUses");
                    scratch.LastUsedThisStage = true;
                    break;
                case SpinMode.Allin:
                    if (res.skulls < 2) p.Inc("allinWins");
                    scratch.AllinUsedThisStage = true;
                    break;
                case SpinMode.Pray:
                    scratch.PrayUsedThisStage = true;
                    break;
            }

            if (spin.cmdCost > 0)
            {
                string cmdKey = spin.mode switch
                {
                    SpinMode.Focus => "cmdCoin_focus",
                    SpinMode.Pray => "cmdCoin_pray",
                    SpinMode.Allin => "cmdCoin_allin",
                    SpinMode.Last => "cmdCoin_last",
                    _ => null,
                };
                if (cmdKey != null) p.Inc(cmdKey, spin.cmdCost);
                p.Inc("cmdCoinTotal", spin.cmdCost);
            }

            for (int i = 0; i < SpecialTotalKeys.Length; i++)
            {
                var (sp, key) = SpecialTotalKeys[i];
                long n = res.cells.Count(c => c.sym.special == sp);
                if (n > 0) p.Inc(key, n);
            }

            if (res.jackpotSym != null) p.Inc("jackpots"); // bumpAch 위치인자 jackpot (전용 컬럼, L679)
            if (res.jackpotSym == "crown") p.Inc("crownJackpots");
            if (res.jackpotSym != null && res.cells.Any(c => c.sym.special == Sp.WILD)) p.Inc("wildJackpots");
            if (spin.mode == SpinMode.Allin && res.skulls >= 2) p.Inc("allinBusts");

            for (int i = 0; i < MaxSpinSymbolIds.Length; i++)
            {
                long n = CellCount(MaxSpinSymbolIds[i]);
                if (n > 0) p.SetMax("max" + Capitalize(MaxSpinSymbolIds[i]) + "Spin", n);
            }
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ── clearStage(clearInc/clearSetMax/addAch4ClearTracking/evalThemeBuilds) 이식 (L820-984) ──
        private static void ApplyClearTracking(PlayerProfile p, RunState run, ClearOutcome clear, SpinOutcome spin, RunScratch scratch)
        {
            if (clear == null) return;
            int stage = clear.clearedStage;
            var res = spin?.result; // MANIP/아이템 즉시클리어 경로는 null일 수 있음 — 보스 카운터 판정만 이 값을 필요로 함(null 가드).

            // ── bumpAch 위치인자 (boss/lastClear/exact/stageReached) ──
            p.SetMax("bestStage", stage);
            if (clear.lastSpinClear) p.Inc("lastSpinClears");
            if (spin != null && spin.newExp == spin.quota) p.Inc("exactClears");
            if (clear.boss) p.Inc("bossClears");
            // WEB_PARITY P3-2(업적 34종 "grad1") — 웹 game.js:1401 "if (stage === 15) r.graduatedThisRun
            // = true" 대응. 웹은 이 플래그를 게임오버 시점에 카운터로 승격하지만(game.js:2562-2563),
            // Unity는 승천(asc) 개념이 아직 없어(P6 미착수) "스테이지 15 클리어" 자체를 곧바로 졸업
            // 카운터로 집계한다 — 승천이 이식되면 asc>0 분기를 얹을 자리다(현재는 항상 asc==0과 동치).
            if (stage == 15) p.Inc("graduations");

            // ── clearInc (L895-913) ──
            int relicN = run.Perks.Count(id => Perks.ById(id)?.cat == PCat.RELIC);
            int curseN = run.Curses.Count;
            long coinsAtClear = spin?.newCoins ?? run.Coins; // Kotlin coinsAtClear = spun.coins(가산 전, 이 스핀 코인반영 후)

            if (clear.leftover <= 10) p.Inc("closeClears");
            if (scratch.PrayUsedThisStage) p.Inc("prayClears");
            if (scratch.LastUsedThisStage) p.Inc("lastClears");
            if (clear.boss && scratch.AllinUsedThisStage) p.Inc("bossAllinClear");
            if (relicN <= 3 && stage >= 10) p.Inc("minimalistS10");
            if (clear.boss && coinsAtClear >= 50) p.Inc("richBossClears");
            if (stage >= 8 && !run.UsedItemThisRun) p.Inc("noItemS8");
            if (stage >= 10 && !run.UsedCmds.Contains("RUNSHOP")) p.Inc("noShopS10");
            if (clear.boss && curseN >= 3) p.Inc("curseBossClears");

            // ── addAch4ClearTracking (L773-817) ──
            int prismN = run.Perks.Count(id => Perks.ById(id)?.tier == Tier.PRISM);
            int goldPlusN = run.Perks.Count(id =>
            {
                var pp = Perks.ById(id);
                return pp != null && (pp.tier == Tier.GOLD || pp.tier == Tier.PRISM);
            });
            if (prismN == 0) p.SetMax("noPrismBestStage", stage);
            if (relicN == 0) p.SetMax("noRelicBestStage", stage);
            if (goldPlusN == 0) p.SetMax("noGoldBestStage", stage);
            if (run.CharId == "novice" && run.MachineId == "basic") p.SetMax("basicOnlyBestStage", stage);

            if (clear.boss)
            {
                var boss = Bosses.For(stage);
                if (boss != null)
                {
                    p.Inc("bossClear_" + boss.id);
                    // L3(Opus 1차 검수): null 가드는 실제로 res를 쓰는 분기(strict/luck)에만 걸어야 한다 —
                    // 예전엔 "res != null &&"가 switch 전체를 감싸 res가 필요 없는 finals/grad까지 아이템
                    // 즉시클리어(res==null) 경로에서 통째로 미집계됐다. finals(막스핀 여부)와 grad(무장치
                    // 여부)는 res 없이도 원본 그대로 판정 가능하므로 그렇게 되돌린다.
                    bool counterMet = boss.id switch
                    {
                        "finals" => clear.lastSpinClear,
                        "strict" => res != null && res.bestSetCount >= 3,
                        "luck" => res != null && res.cells.Any(c => c.sym.id == "star" || c.sym.id == "crown" || c.sym.id == "wild"),
                        "grad" => string.IsNullOrEmpty(run.Device) && string.IsNullOrEmpty(run.Device2),
                        _ => false,
                    };
                    if (counterMet) p.Inc("bossCounterClear_" + boss.id);
                }
                if (!run.UsedItemThisRun) p.Inc("bossNoItemClears");
                if (string.IsNullOrEmpty(run.Device) && string.IsNullOrEmpty(run.Device2)) p.Inc("bossNoDeviceClears");
                if (clear.overPct >= 500) p.Inc("bossOverkillClears");
                if (stage >= 15) p.SetMax("bossStreak3", 1);
            }
            if (coinsAtClear <= 0) p.Inc("zeroCoinClears");
            if (clear.boss && clear.inDebt) p.Inc("debtBossClears");

            // ── clearSetMax (L916-937) ──
            p.SetMax("curseMax", curseN);
            p.SetMax("relicsMax", relicN);
            p.SetMax("cstage_" + run.CharId, stage);
            p.SetMax("mstage_" + run.MachineId, stage);
            p.SetMax("bc_" + run.CharId + "_" + run.MachineId, stage);
            p.SetMax("maxRunJackpots", run.RunJackpots);
            p.SetMax("maxOverPct", clear.overPct);
            bool noDevice = string.IsNullOrEmpty(run.Device) && string.IsNullOrEmpty(run.Device2);
            if (noDevice) p.SetMax("noDevStage", stage);
            if (!run.UsedItemThisRun) p.SetMax("noItemMaxS", stage);
            if (curseN >= 5) p.SetMax("curse5Stage", stage);
            if (!string.IsNullOrEmpty(run.Device)) p.SetMax("dvstage_" + run.Device, stage);
            if (!string.IsNullOrEmpty(run.Device2)) p.SetMax("dvstage_" + run.Device2, stage);
            if (!run.RunUsedCmd) p.SetMax("noCommandBestStage", stage);
            if (!run.RunRerolled) p.SetMax("noRerollBestStage", stage);

            // ── 빌드도감(evalThemeBuilds, L960-961 호출 / SlotV2Engine.kt L1392-1433 정의) ──
            var ctx = new BuildCtx
            {
                stage = stage,
                machineId = run.MachineId,
                deviceId = run.Device,
                device2Id = run.Device2,
                perks = run.Perks,
                curses = run.Curses,
                runFastClears = run.RunFastClears,
                runLastSpinClears = run.RunLastSpinClears,
                runPrayWins = run.RunPrayWins,
                runAdjPairs = run.RunAdjPairs,
                runSet4 = run.RunSet4,
                runCrowns = run.RunSymCounts.TryGetValue("crown", out var cc) ? cc : 0,
                isBossClear = clear.boss,
                isLastSpinClear = clear.lastSpinClear,
                clearSpinRareCount = res?.cells.Count(c => c.sym.rare) ?? 0,
                clearSpinSkullCount = res?.skulls ?? 0,
                clearSpinWildJackpot = res != null && res.jackpotSym != null && res.cells.Any(c => c.sym.special == Sp.WILD),
                jackpotThisRun = run.RunJackpots > 0,
                oracleUsedThisRun = run.UsedCmds.Contains("RUNORACLE"),
                pinUsedThisStage = scratch.PinUsedThisStage,
                copyMadeSet4 = Shop.HasDevice(run, "dev_copy") && run.RunSet4 >= 1,
                bellUsedThisClear = spin != null && spin.destroyDevice,
                skullTotal = p.GetStat("skullTotal"),
                closeClears = p.GetStat("closeClears"),
            };
            foreach (var id in EvalThemeBuilds(ctx)) p.SetMax(id, 1);

            scratch.ResetStage();
        }

        // ── gameOver(goInc/goSetMax/evalThemeBuilds) 이식 (L2003-2085) ──────────────────────────────
        private static void ApplyGameOverTracking(PlayerProfile p, RunState run, FailureOutcome failure, RunScratch scratch)
        {
            long finalScore = failure?.finalScore ?? 0L;
            long priorBest = p.GetStat("bestScore");

            // WEB_PARITY P3-2(작업 지시 3번) — 웹 game.js:2578 "cnt.playerLevel = p.playerLevel || 1"
            // (XP 부여 *직전* 스냅샷, "후반 업적용... playerLevel 은 1런 지연" 웹 주석 그대로). Unity 호출
            // 순서(GameSession.FinishAction: StatTracker.Apply → AchievementEngine.Evaluate →
            // PlayerLevelTracker.ApplyRunEnd)가 이 대입을 PlayerLevelTracker보다 먼저 실행하므로, 여기서
            // 직접 대입하는 것만으로 웹과 동일한 "이번 런 XP 반영 전" 1런 지연이 재현된다(lv20/lv40 업적 대상).
            // Opus 1차 검수(P3-2): 원재료 Dictionary에 직접 인덱서로 쓰던 걸 PlayerProfile.SetStat(공개
            // 계약, Inc/SetMax와 나란한 세 번째 조작 방식)으로 승격.
            p.SetStat("playerLevel", p.PlayerLevel);

            p.Inc("runs");
            p.SetMax("bestStage", run.Stage);
            p.SetMax("bestScore", finalScore);
            if (scratch.PrayUsedThisStage) p.Inc("prayFails");

            int relicN = run.Perks.Count(id => Perks.ById(id)?.cat == PCat.RELIC);
            int curseN = run.Curses.Count;
            bool noDevice = string.IsNullOrEmpty(run.Device) && string.IsNullOrEmpty(run.Device2);
            long devCount = ComputeDevicesOwned(p);

            p.SetMax("devicesOwned", devCount);
            p.SetMax("curseMax", curseN);
            p.SetMax("relicsMax", relicN);
            p.SetMax("cstage_" + run.CharId, run.Stage);
            p.SetMax("mstage_" + run.MachineId, run.Stage);
            p.SetMax("bc_" + run.CharId + "_" + run.MachineId, run.Stage);
            p.SetMax("maxRunJackpots", run.RunJackpots);
            if (noDevice) p.SetMax("noDevStage", run.Stage);
            if (!run.UsedItemThisRun) p.SetMax("noItemMaxS", run.Stage);
            if (curseN >= 5) p.SetMax("curse5Stage", run.Stage);

            var ctx = new BuildCtx
            {
                stage = run.Stage,
                machineId = run.MachineId,
                deviceId = run.Device,
                device2Id = run.Device2,
                perks = run.Perks,
                curses = run.Curses,
                runFastClears = run.RunFastClears,
                runLastSpinClears = run.RunLastSpinClears,
                runPrayWins = run.RunPrayWins,
                runAdjPairs = run.RunAdjPairs,
                runSet4 = run.RunSet4,
                runCrowns = run.RunSymCounts.TryGetValue("crown", out var cc) ? cc : 0,
                jackpotThisRun = run.RunJackpots > 0,
                oracleUsedThisRun = run.UsedCmds.Contains("RUNORACLE"),
                copyMadeSet4 = Shop.HasDevice(run, "dev_copy") && run.RunSet4 >= 1,
                skullTotal = p.GetStat("skullTotal"),
                closeClears = p.GetStat("closeClears"),
                // isBossClear/isLastSpinClear/clearSpin*/bellUsedThisClear: 게임오버는 클리어가 아니므로
                // Kotlin buildCtxGo와 동일하게 전부 기본값(false/0) — BuildCtx 기본 이니셜라이저 그대로 둔다.
            };
            foreach (var id in EvalThemeBuilds(ctx)) p.SetMax(id, 1);

            // ── SlotV2ScoreRow 갱신(recordRun 상당) — achievement 판정과 무관한 표시/기록 필드 ──
            p.TotalScore += finalScore;
            // L1(Opus 1차 검수): Kotlin recordRun L2179-2180 "finalScore >= (existing.bestScore ?: 0L)" —
            // >가 아니라 >=다. 동점(첫 런의 finalScore==0==priorBest 포함)도 이번 런의 캐릭/머신으로 갱신된다.
            if (finalScore >= priorBest)
            {
                p.BestChar = run.CharId;
                p.BestMachine = run.MachineId;
            }
            p.LastCombo = string.Join(",", run.CharId, run.MachineId, run.Device ?? "", run.Device2 ?? "");

            scratch.ResetStage();
        }

        // M4(Opus 1차 검수, S5): devicesOwned가 "이번 런에서 막 충족된 업적 해금 장치"를 누락하던 1런
        // 지연 버그 수정 — Kotlin gameOver의 devCount = equipableDeviceList(prev, composeStat(prevAch,
        // prev)).size는 prevAch가 "이번 런 도중의 이전 clearStage들이 이미 반영한" 최신 DB 읽기이므로,
        // 이번 런의 앞선 클리어에서 이미 충족된 업적은 포함되지만 이번 gameOver 자체가 막 충족시킨
        // 업적은 원본에서도 포함되지 않는다(자기참조 없음, bumpAch 계산 이전 시점 읽기). 우리 엔진은
        // AchievementEngine.Evaluate를 "호출측이 언제 부르는가"에 따라 이미 반영된 AchievedIds 여부가
        // 달라지므로, 여기서는 Kotlin과 동등하게 "이미 AchievedIds에 있는 업적" OR "그 업적의 req가
        // *지금 이 시점의* 원재료 Stats(이 함수가 위에서 이미 갱신한 runs/bestStage/bestScore 등 포함)로
        // 충족"을 함께 판정한다 — AchievementEngine.Evaluate가 아직 호출되지 않았어도(즉 AchievedIds가
        // 아직 갱신되지 않았어도) "이번 런 취득분"이 devicesOwned에 누락되지 않는다.
        //
        // WEB_PARITY P3-2(Opus 2차 검수, Fable 결정 4번) — dev_syllabus/dev_holdfile/dev_retake/
        // dev_major(unlockAch="", 웹에 없는 Unity 전용 드랍 전용 장치, Devices.cs 헤더 각주)는 업적
        // 경로로는 절대 충족되지 않지만, 런 중 드랍으로 이미 OwnedDevices에 들어있다면 devicesOwned에
        // 여전히 포함돼야 한다 — "OwnedDevices 소속 여부" 체크를 unlockAch 공백 체크보다 먼저 둬서,
        // 드랍 전용 장치도 보유 시 정상 카운트되게 한다(이전엔 unlockAch가 비면 무조건 continue라
        // 드랍으로 얻어도 devicesOwned에서 누락됐다).
        private static long ComputeDevicesOwned(PlayerProfile p)
        {
            var composed = AchievementEngine.ComposeStat(p);
            long count = 0;
            for (int i = 0; i < Devices.All.Length; i++)
            {
                var d = Devices.All[i];
                if (p.OwnedDevices.Contains(d.id)) { count++; continue; }
                if (string.IsNullOrEmpty(d.unlockAch)) continue; // 드랍 전용 장치 — 업적 경로 없음(OwnedDevices만 유효)
                if (p.AchievedIds.Contains(d.unlockAch)) { count++; continue; }

                var achDef = Achievements.ById(d.unlockAch);
                if (achDef?.req == null || achDef.req.Length == 0) continue;
                var req = achDef.req[0];
                if (composed.TryGetValue(req.key, out var v) && v >= req.value) count++;
            }
            return count;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════
        // 빌드도감(테마빌드 25종) 완성판정 — SlotV2Engine.kt THEME_BUILDS(L1277-1308)/evalThemeBuilds
        // (L1392-1433) 이식. bldCat_*/bldTotal/bldAllBasic/bldAllMaster 파생 집계는 AchievementEngine.
        // ComposeStat(composeStat 이식 담당)이 이 함수가 setMax한 bld_<id> raw 플래그를 읽어 계산한다.
        // ══════════════════════════════════════════════════════════════════════════════════════
        private sealed class BuildCtx
        {
            public int stage;
            public string machineId = "", deviceId = "", device2Id = "";
            public IReadOnlyList<string> perks = Array.Empty<string>();
            public IReadOnlyList<string> curses = Array.Empty<string>();
            public int runFastClears, runLastSpinClears, runPrayWins, runAdjPairs, runSet4, runCrowns;
            public bool isBossClear, isLastSpinClear, clearSpinWildJackpot, jackpotThisRun, oracleUsedThisRun, pinUsedThisStage, copyMadeSet4, bellUsedThisClear;
            public int clearSpinRareCount, clearSpinSkullCount;
            public long skullTotal, closeClears;
        }

        private static int PerkDescCount(IReadOnlyList<string> perks, Func<string, bool> predicate)
        {
            int c = 0;
            for (int i = 0; i < perks.Count; i++)
            {
                var p = Perks.ById(perks[i]);
                if (p != null && p.desc != null && predicate(p.desc)) c++;
            }
            return c;
        }

        private static HashSet<string> EvalThemeBuilds(BuildCtx ctx)
        {
            var outp = new HashSet<string>();
            var perks = ctx.perks;
            int nCurses = ctx.curses.Count;
            int cherryPerks = PerkDescCount(perks, d => d.Contains("🍒"));
            int bookPerks = PerkDescCount(perks, d => d.Contains("📘"));
            int lastSpinPerks = PerkDescCount(perks, d => d.Contains("마지막 스핀") || d.Contains("막스핀") || d.Contains("막 스핀"));
            int prismPerks = 0;
            for (int i = 0; i < perks.Count; i++)
            {
                var p = Perks.ById(perks[i]);
                if (p != null && p.tier == Tier.PRISM) prismPerks++;
            }
            bool Dev(string id) => ctx.deviceId == id || ctx.device2Id == id;

            // ── 성장형 ──
            if (ctx.runFastClears >= 3) outp.Add("bld_fast_start");
            if (ctx.stage >= 5 && perks.Count >= 3) outp.Add("bld_model_growth");
            if (cherryPerks >= 2 && ctx.stage >= 7) outp.Add("bld_cherry_sprout");
            if (bookPerks >= 2 && ctx.stage >= 7) outp.Add("bld_library_start");
            if (prismPerks == 0 && ctx.stage >= 10) outp.Add("bld_foundation");
            // ── 운명형 ──
            if (ctx.runPrayWins >= 2) outp.Add("bld_fate_hand");
            if (ctx.machineId == "casino" && ctx.stage >= 10) outp.Add("bld_dice_grad");
            if (ctx.runCrowns >= 10) outp.Add("bld_crown_caller");
            if (ctx.isBossClear && ctx.clearSpinRareCount >= 5) outp.Add("bld_prob_hacker");
            if (ctx.oracleUsedThisRun && ctx.jackpotThisRun) outp.Add("bld_jackpot_seer");
            // ── 역전형 ──
            if (ctx.runLastSpinClears >= 3) outp.Add("bld_cliff_pass");
            if (ctx.closeClears >= 5) outp.Add("bld_heartbreaker");
            if (lastSpinPerks >= 3 && ctx.isBossClear) outp.Add("bld_cram_grad");
            if (ctx.bellUsedThisClear && ctx.isBossClear) outp.Add("bld_miracle_cert");
            if (ctx.stage >= 10 && ctx.isLastSpinClear) outp.Add("bld_last_candle");
            // ── 조합형 ──
            if (ctx.machineId == "magnet" && ctx.runSet4 >= 1) outp.Add("bld_magnet_grad");
            if (ctx.clearSpinWildJackpot) outp.Add("bld_wild_puzzle");
            if (ctx.pinUsedThisStage) outp.Add("bld_pinned_fate");
            if (ctx.copyMadeSet4) outp.Add("bld_copy_answer");
            if (ctx.runAdjPairs >= 5) outp.Add("bld_chain");
            // ── 위험형 ──
            if (ctx.skullTotal >= 100) outp.Add("bld_skull_intro");
            if (nCurses >= 3 && ctx.isBossClear) outp.Add("bld_black_grad");
            if (ctx.clearSpinSkullCount >= 5) outp.Add("bld_ossuary");
            if (nCurses >= 7 && ctx.stage >= 10) outp.Add("bld_curse_vessel");
            if (Dev("dev_overheat") && nCurses >= 3 && ctx.stage >= 10) outp.Add("bld_ominous_overheat");

            return outp;
        }

        // AchievementEngine.ComposeStat이 bldCat_* 집계 시 재사용하는 카테고리→id 매핑(THEME_BUILDS 선언
        // 순서 그대로, SlotV2Engine.kt L1277-1308). internal로 노출해 AchievementEngine과만 공유한다.
        internal static readonly IReadOnlyDictionary<string, string[]> ThemeBuildCategoryIds = new Dictionary<string, string[]>
        {
            ["성장형"] = new[] { "bld_fast_start", "bld_model_growth", "bld_cherry_sprout", "bld_library_start", "bld_foundation" },
            ["운명형"] = new[] { "bld_fate_hand", "bld_dice_grad", "bld_crown_caller", "bld_prob_hacker", "bld_jackpot_seer" },
            ["역전형"] = new[] { "bld_cliff_pass", "bld_heartbreaker", "bld_cram_grad", "bld_miracle_cert", "bld_last_candle" },
            ["조합형"] = new[] { "bld_magnet_grad", "bld_wild_puzzle", "bld_pinned_fate", "bld_copy_answer", "bld_chain" },
            ["위험형"] = new[] { "bld_skull_intro", "bld_black_grad", "bld_ossuary", "bld_curse_vessel", "bld_ominous_overheat" },
        };
    }
}
