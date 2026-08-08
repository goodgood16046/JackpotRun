using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════
    // RunAction — 카톡 텍스트 명령을 대체하는 typed action API(설계 원칙 5). 02_service.md §7 명령 전수
    // 매핑. 인덱스는 전부 0-base(C# List 관례 — 카톡 1-base 번호 표시는 UI 몫).
    // ══════════════════════════════════════════════════════════════════════
    public abstract class RunAction { }

    // §7-B 스핀(N/FOCUS/ALLIN/PRAY/LAST).
    public sealed class Spin : RunAction
    {
        public readonly SpinMode mode;
        public Spin(SpinMode mode = SpinMode.N) { this.mode = mode; }
    }

    // §5 NODE_SELECT 3택 중 하나 선택.
    public sealed class ChooseNode : RunAction
    {
        public readonly int index;
        public ChooseNode(int index) { this.index = index; }
    }

    // §5-B/§4-A EVENT_AUGMENT·EVENT_RELIC 후보 선택.
    public sealed class PickOffer : RunAction
    {
        public readonly int index;
        public PickOffer(int index) { this.index = index; }
    }

    // §5-C dev_holdfile — 증강 후보 보관("보류 N", EVENT_AUGMENT 전용).
    public sealed class HoldAugment : RunAction
    {
        public readonly int index;
        public HoldAugment(int index) { this.index = index; }
    }

    // §5-C dev_retake — 재추첨("재추첨", EVENT_AUGMENT/EVENT_RELIC 둘 다 동작).
    public sealed class Retake : RunAction { }

    // §4-D 상점 구매.
    public sealed class BuyOffer : RunAction
    {
        public readonly int index;
        public BuyOffer(int index) { this.index = index; }
    }

    // §4-C 상점 리롤(정액 6코인).
    public sealed class RerollShop : RunAction { }

    // §4 상점 나가기("0").
    public sealed class LeaveShop : RunAction { }

    // §7-D 아이템 사용 — id로 가방에서 찾는다(첫 매치).
    public sealed class UseItem : RunAction
    {
        public readonly string itemId;
        public UseItem(string itemId) { this.itemId = itemId; }
    }

    // §7-C 장치 명령 — deviceId로 대상 장치 지정(메인/보조 어느 슬롯이든), arg는 dev_pin/copy/swap 전용
    // 칸번호(1-base, Kotlin argOf() 관례 유지 — Device.needsArg 4종은 DeviceActions.DeviceNeedsArg 참조).
    public sealed class DeviceCmd : RunAction
    {
        public readonly string deviceId;
        public readonly int? arg;
        public DeviceCmd(string deviceId, int? arg = null) { this.deviceId = deviceId; this.arg = arg; }
    }

    // §7-C 도박꾼 전용 무료 재굴림("재굴림", charId=="gambler" 게이트) — 장치 무관이라 DeviceCmd와 분리.
    public sealed class GamblerReroll : RunAction { }

    // §3-C POST_SPIN 포기("포기"/"넘어가기"/"그만"/"0") — 만회 수단을 쓰지 않고 즉시 게임오버 확정.
    public sealed class Continue : RunAction { }

    // WEB_PARITY P1 ④: DEVICE 노드 오퍼(RunState.PendingDeviceDrop) 확정 — equip=true면 장착(현재 런의
    // Device 슬롯 교체), false면 코인+15만 받는다. 어느 쪽이든 장치는 영구 보유로 지급된다(웹 game.js:
    // 2523-2529 deviceNodeTake). RunPhase.DeviceNode 전용.
    public sealed class TakeDevice : RunAction
    {
        public readonly bool equip;
        public TakeDevice(bool equip) { this.equip = equip; }
    }

    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — RewardDone 화면의 "스테이지 N 시작" 탭(웹
    // proceedToStage(), game.js:1586 `if (this.run.phase === PHASE.REWARD_DONE) this._beginStage();`).
    // RunPhase.RewardDone 전용 — Unity는 spins/quota를 스테이지마다 캐시하지 않고 매번 SpinResolver.
    // EffSpins/QuotaOf로 즉석 계산하는 구조라(StageFlow.ClearStage 헤더 주석) 웹 `_beginStage()`가 하는
    // 일(스핀수/요구치 재계산 등) 중 실제로 필요한 상태 변경은 이미 StageFlow.ClearStage 시점에 전부
    // 끝나 있다 — 이 액션은 순수 phase 게이트일 뿐이다.
    public sealed class ProceedToStage : RunAction { }

    // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 C) — 심화모드 상점 '심볼 정비' 서비스
    // 구매(11종, Content/RepairServices.cs). args는 targetPick 서비스(addBasic/addHigh/addRare/remove/
    // swap/upgrade/tagbuff)에서만 필요 — 나머지(purify/expand/compress/curseCleanse)는 null로 충분.
    public sealed class RepairBuy : RunAction
    {
        public readonly string serviceId;
        public readonly RepairArgs args;
        public RepairBuy(string serviceId, RepairArgs args = null) { this.serviceId = serviceId; this.args = args; }
    }

    // ══════════════════════════════════════════════════════════════════════
    // RunEvent — UI가 연출로 번역할 구조화 이벤트. 카톡 출력 문자열을 조립하지 않는다(설계 원칙 5) —
    // 기존 S3 산출물(SpinOutcome/ClearOutcome/FailureOutcome)을 그대로 페이로드로 재사용하고, S4가 새로
    // 다루는 노드/상점/아이템/장치 결과는 최소한의 타입 있는 필드로 담는다. `type` 문자열 상수는 이 파일
    // 헤더 표에 정리(호출측이 switch(type)으로 분기).
    //
    // type 값 목록:
    //   REJECTED · SPIN_RESULT · STAGE_CLEARED · REVIVED · POST_SPIN · GAME_OVER · DEVICE_MANIP_RESULT
    //   NODE_RESOLVED · PERK_OFFER · PERK_GRANTED · PERK_HELD · PERK_LEVELED · RETAKE_EMPTY
    //   SHOP_OFFER · SHOP_PURCHASED · SHOP_REROLLED · SHOP_LEFT
    //   ITEM_USED · DEVICE_ARMED · DEVICE_PEEK · RUN_STARTED
    //   STAGE_STARTED — 웹 파리티 P4(§1-A #15): RewardDone → Spin 전이(ProceedToStage) 완료 신호.
    //   BOSS_PHASE2 — 웹 파리티 P6(§1-A #18): A10 승천 최종보스(15) 1페이즈 클리어(진짜 클리어 아님,
    //     요구치↑ 후 같은 스테이지 재시작). StatTracker는 이 타입을 STAGE_CLEARED와 다르게 취급한다
    //     (스핀 통계만 반영, 클리어/졸업 통계는 건너뜀 — StageFlow.BuildClearEvent/ClearStage 참조).
    //   REPAIR_DONE / ARCHETYPE_CHANGED — 웹 파리티 P7-2(§1-A #19 2/4): 정비소 구매 성공 / 전공
    //     발동·승급. RepairBuy 성공 시 REPAIR_DONE 1개 + (전공 변화가 있으면) ARCHETYPE_CHANGED 1개,
    //     최대 2개 이벤트가 함께 반환된다(Run/RepairShop.cs 참조).
    //
    // ⚠️ UI 계약 주의:
    //   1) STAGE_CLEARED의 spin.result는 즉시클리어 아이템(grad_ring/gold_grad_bell) 경로에선 null이다
    //      (릴 연출 생략 — Kotlin은 더미 evaluate를 만들지만 RNG 소비를 피하려 생략). null 가드 필수.
    //   2) 생성자에 넘긴 stat 딕셔너리는 "참조"로 보관된다 — seen_<id> 등 런 중 갱신되는 스탯 게이트는
    //      호출측(프로필 계층)이 같은 딕셔너리를 갱신해야 원본처럼 같은 런 안에서 해금이 반영된다.
    // ══════════════════════════════════════════════════════════════════════
    public sealed class RunEvent
    {
        public string type;

        // 스핀/클리어/실패 계열 — S3 산출 타입 그대로 페이로드로 사용.
        public SpinOutcome spin;
        public ClearOutcome clear;
        public FailureOutcome failure;

        // 노드(NODE_RESOLVED) 결과 델타.
        public NodeKind? node;
        public long coinsDelta;
        public long scoreDelta;
        public int bonusSpinsDelta;
        public string curseGrantedId;
        public string curseRemovedId;
        public string augmentGrantedId;
        public string relicGrantedId;
        public string itemGrantedId;
        public bool gambleWon;
        public int eventRoll = -1; // EVENT 10종 랜덤표 중 어느 case였는지(§5-A), 비EVENT면 -1

        // 퍽 오퍼/픽(PERK_OFFER/PERK_GRANTED/PERK_HELD/RETAKE_EMPTY).
        public IReadOnlyList<string> perkOfferIds;
        public Tier offerTier;
        public bool offerBossPrism;
        public bool offerTierBumped;
        // 🧩 세트 시너지 5% off-tier 주입(Kotlin offerPerks L1281-1295, Shop.SetSynergyAug)으로 마지막 칸이
        // 교체된 경우 그 퍽 id, 주입이 없었으면 null.
        public string offerSynergyPerkId;
        // 🗂️ 보류파일 포함 오퍼 — true면 perkOfferIds[0]이 보류해 둔 증강(원본 배너 "🗂️ 보류 후보 포함!").
        public bool offerHeldIncluded;
        // 🔁 dev_retake 재추첨으로 생성된 오퍼 — 최초 노드 오퍼(false)와 구분(스탯 트래킹 deviceUses 귀속용).
        public bool offerRetake;
        public string perkId;
        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — PERK_LEVELED 전용(AUGLEVEL 노드 선택 결과).
        // perkId가 레벨업된 증강 id, before/after가 레벨 전이(예: Lv1→Lv2). 웹 game.js:2143-2144
        // `r.perkLevels[p.id] = Math.min(3, ...+1)` toast 문구(Lv.N → Lv.N+1 강화 완료) 대응.
        public int perkLevelBefore;
        public int perkLevelAfter;

        // 상점(SHOP_*).
        public IReadOnlyList<ShopEntry> shopOffer;
        public ShopEntry shopBought;

        // 아이템/장치.
        public string itemId;
        public string deviceId;
        public bool secondary;
        public IReadOnlyList<string> peekCells;

        // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 C) — REPAIR_DONE(정비소 구매 성공)
        // 전용. curseCleanse는 curseRemovedId(위 노드 델타 필드, 기존 재사용)에 제거된 저주 id를 채운다.
        public string repairServiceId;
        public int repairPrice;

        // 웹 파리티 P7-2(§1-A #19 B) — ARCHETYPE_CHANGED(전공 발동/승급) 전용. archUpgraded=false면
        // "발동"(새 계열), true면 "승급"(같은 계열 tier↑) — 웹 game.js:554 `fam !== prevFam ? "발동" : "승급"`.
        public string archFamily;
        public int archTier;
        public bool archUpgraded;
        // WEB_PARITY P1 ④: 장치가 "영구 보유"로 지급된 이벤트(EVENT 10분기표 6번 / DEVICE 노드)의
        // 장치id — 엔진은 Engine/Profile을 참조하지 않으므로(설계 원칙 6) PlayerProfile.OwnedDevices
        // 반영은 이 필드를 관찰하는 StatTracker(호출측 글루 레이어)가 담당한다. 비어있으면 이 이벤트로
        // 새로 지급된 장치 없음(예: EVENT 6번의 "전부 보유 → 코인 폴백" 분기).
        public string deviceGrantedId;

        // 거부.
        public string reason;
    }

    // 내부 이벤트 리스트 생성 헬퍼 — Shop/NodeEvents/ItemUse/DeviceActions가 공용으로 사용.
    internal static class RunEvents
    {
        public static List<RunEvent> One(RunEvent e) => new List<RunEvent> { e };
        public static List<RunEvent> Rejected(string reason) => new List<RunEvent> { new RunEvent { type = "REJECTED", reason = reason } };
    }

    // ══════════════════════════════════════════════════════════════════════
    // RunController — façade. 생성자에서 캐릭터/머신/장치 확정 + 스테이지1 SPIN 직행(Kotlin launchRun,
    // L481-504 — CHAR_SELECT/MACHINE_SELECT/DEVICE_SELECT(2) 웹훅/카톡 전용 단계는 전부 생략, 설계
    // 원칙 5·02_service.md §12-5). `Do(RunAction)` 단일 진입점으로 이후 모든 상호작용을 처리한다.
    //
    // "해금 스탯 뷰"(stat: IReadOnlyDictionary<string,long>)는 Kotlin playerStat(...)에 대응 — 상점/노드의
    // perkGate 판정에 쓰인다. PlayerProfile(S5)이 아직 없어 호출측이 직접 구성해 넘긴다(예: achievement
    // 카운터 + bestScore/bestStage/runs 등을 합친 맵). null이면 빈 맵으로 취급(Kotlin stat.isEmpty() 분기
    // 그대로 — gatedPool은 무필터, unlockedPerks 계열은 사실상 BASE_PERK_IDS만 통과).
    //
    // deviceId2(보조 슬롯)는 작업 지시 문면상 생성자 시그니처엔 없었으나, DeviceActions.cs가 요구하는
    // "보조 슬롯 SECONDARY_MUL 약화"를 지원하려면 RunState.Device2를 채울 경로가 필요하다 — 선택적 트레일링
    // 매개변수로 추가했다(기본값 "" = 보조 미장착, 기존 호출부와 100% 호환). 최종 보고에 명시.
    // ══════════════════════════════════════════════════════════════════════
    public sealed class RunController
    {
        public RunState State { get; }
        public IReadOnlyList<RunEvent> LaunchEvents { get; }

        private readonly IReadOnlyDictionary<string, long> _stat;

        // WEB_PARITY P1 ④: ownedDeviceIds — 호출측(GameSession)이 PlayerProfile.OwnedDevices를 넘겨
        // RunState.OwnedDeviceIds를 시드한다("미보유 장치" 판정 재료, NodeEvents EVENT-6/DEVICE 노드
        // 전용). deviceId2와 동일한 취지로 선택적 트레일링 매개변수로 추가했다(기존 호출부 100% 호환,
        // 최종 보고에 명시 — RunController.cs 헤더의 deviceId2 각주 참조).
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — asc: 승천(심화 학기) 단계(0=일반). 해금 상한
        // (profile.ascMax+1) 클램프는 호출측(GameSession, Engine/Profile을 아는 계층)이 먼저 끝내고
        // 넘긴다 — 여기서는 웹 ascMods(asc)와 동일하게 [0,ASC_MAX] 방어적 재클램프만 한다(설계 원칙 6,
        // deviceId2/ownedDeviceIds와 동일하게 선택적 트레일링 매개변수로 추가해 기존 호출부 100% 호환).
        // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 game.js:285-289 startRun) — deep: 심화모드
        // (심볼 덱/주머니) 진입 여부. deep이면 asc는 무조건 0으로 강제한다(웹 "wantDeep이면 asc 강제
        // 0" — 요구치 이중 가중 방지, 승천과 심화는 상호 배제). 호출측 클램프(GameSession)가 이미 0을
        // 넘기더라도 여기서 다시 한번 방어적으로 강제해 RunController를 직접 호출하는 테스트 등에서도
        // 실수로 asc>0+deep=true 조합이 새지 않게 한다.
        public RunController(string charId, string machineId, string deviceId, long seed,
            IReadOnlyDictionary<string, long> stat, string deviceId2 = "",
            IReadOnlyCollection<string> ownedDeviceIds = null, int asc = 0, bool deep = false)
        {
            _stat = stat ?? new Dictionary<string, long>();
            var run = new RunState(seed);
            var events = new List<RunEvent>();

            var ch = Characters.ById(charId); // 미해금/미지정 id는 BASE_CHAR 폴백(Characters.cs 계약)
            var m = Machines.ById(machineId); // 동일하게 BASE_MACHINE 폴백

            run.CharId = ch.id;
            run.MachineId = m.id;
            run.Device = string.IsNullOrEmpty(deviceId) ? "" : deviceId;
            if (ownedDeviceIds != null) run.OwnedDeviceIds.UnionWith(ownedDeviceIds);
            run.DeepMode = deep;
            run.Asc = deep ? 0 : AscMods.Clamp(asc);
            if (deep)
            {
                foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
                run.DeepStats = new DeepStats { MaxTotal = Pouch.Total(run.Pouch) };
            }
            // 보조 슬롯 입력 검증 — 원본 secondaryCandidates(SlotV2Service.kt:461-462): ARMED/PEEK 전용,
            // 메인과 동일 장치 금지. 조건 위반은 조용히 미장착 처리(선택 화면이 막는 게 정상 경로).
            var d2 = string.IsNullOrEmpty(deviceId2) ? null : Devices.ById(deviceId2);
            bool d2Ok = d2 != null && d2.id != run.Device && (d2.kind == "ARMED" || d2.kind == "PEEK");
            run.Device2 = d2Ok ? d2.id : "";
            // 웹 game.js:392 `r.coins = Math.max(0, (ch?.startCoins||0) + ascMods(r.asc).startCoinDelta);`
            run.Coins = System.Math.Max(0, ch.startCoins + AscMods.Get(run.Asc).StartCoinDelta);
            run.Stage = 1;
            run.SpinIndex = 0;
            run.StageExp = 0;
            run.Phase = RunPhase.Spin;
            // 웹 _beginStage()의 A8 금지 심볼 롤(game.js:425) — 스테이지1 시작(=_launch() 끝 _beginStage()
            // 호출)과 동일 파리티. StageFlow.ClearStage(다음 스테이지 진입)/A10 2페이즈 재시작에서도 재롤한다.
            AscRunHooks.RollBannedSym(run);

            // honor(수석졸업생) — 실버 증강 1개로 시작(launchRun L483-489).
            // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑥(WEB_PARITY_DESIGN.md §2, 웹 game.js:393-397
            // `E.pickAugments(this.rng, 1, new Set(), 1)` → `offerPerks(AUGMENTS, ...)`) — 이 경로는
            // 원본부터 raw AUGMENTS(레벨게이트/GatedPool 미적용)를 그대로 쓴다(웹 quirk 보존 — offerPerks/
            // pickPerksByTier 자체엔 "해금" 개념이 아예 없다, Shop.cs 헤더 각주 참조). Unity가 임의로
            // Shop.GatedPool을 씌우면 레벨 미달 시 신규 4종 증강이 후보에서 빠지는 등 웹과 달라진다 —
            // 게이트 없이 raw Perks.Augments에서 SILVER만 추린다.
            if (run.CharId == "honor")
            {
                var pool = Perks.Augments.Where(p => p.tier == Tier.SILVER).ToList();
                var aug = run.Rng.PickOrDefault(pool);
                if (aug != null)
                {
                    run.Perks.Add(aug.id);
                    events.Add(new RunEvent { type = "PERK_GRANTED", perkId = aug.id });
                }
            }

            State = run;
            events.Add(new RunEvent { type = "RUN_STARTED", coinsDelta = run.Coins });
            LaunchEvents = events;
        }

        public IReadOnlyList<RunEvent> Do(RunAction a)
        {
            switch (a)
            {
                case Spin s: return WrapSpin(StageFlow.ProcessSpin(State, s.mode));
                case ChooseNode c: return NodeEvents.ChooseNode(State, c.index, _stat);
                case PickOffer p: return NodeEvents.PickOffer(State, p.index);
                case HoldAugment h: return NodeEvents.HoldAugment(State, h.index);
                case Retake _: return NodeEvents.Retake(State, _stat);
                case BuyOffer b: return Shop.Buy(State, b.index);
                case RerollShop _: return Shop.Reroll(State, _stat);
                case LeaveShop _: return Shop.Leave(State);
                case UseItem u: return ItemUse.Use(State, u.itemId, _stat);
                case DeviceCmd d: return DeviceActions.Handle(State, d.deviceId, d.arg);
                case GamblerReroll _: return DeviceActions.GamblerReroll(State, fromPost: State.Phase == RunPhase.PostSpin);
                case Continue _: return HandleContinue();
                case TakeDevice td: return NodeEvents.TakeDevice(State, td.equip);
                case ProceedToStage _: return HandleProceedToStage();
                case RepairBuy r: return RepairShop.Execute(State, r.serviceId, r.args);
                default: return RunEvents.Rejected("UNKNOWN_ACTION");
            }
        }

        // 웹 파리티 P4 — RewardDone → Spin. State가 아니라면 거부(웹은 이 가드를 `if` 조건으로 조용히
        // 무시하지만, Unity는 다른 모든 액션과 동일하게 REJECTED 이벤트로 UI에 알린다 — 기존 관례).
        private List<RunEvent> HandleProceedToStage()
        {
            if (State.Phase != RunPhase.RewardDone) return RunEvents.Rejected("PHASE_NOT_REWARD_DONE");
            State.Phase = RunPhase.Spin;
            State.RewardMessage = "";
            return RunEvents.One(new RunEvent { type = "STAGE_STARTED" });
        }

        // POST_SPIN 포기(§3-C step4의 "만회 수단 없음"과 동일한 최종 게임오버, 다만 여기선 플레이어가
        // 만회 수단이 있어도 명시적으로 포기하는 경우) — Kotlin handlePostSpin GIVEUP 분기(L1898-1902).
        // WEB_PARITY P1 ⑤와는 별개 기능이다 — 이쪽은 "POST_SPIN 만회를 안 쓰고 그대로 실패"이고,
        // GiveUp()은 SPIN 단계에서도 언제든 부를 수 있는 "자발적 조기종료·즉시 결산"이다(voluntary=true
        // 프레이밍 차이, 아래 GiveUp() 헤더 참조).
        private List<RunEvent> HandleContinue()
        {
            if (State.Phase != RunPhase.PostSpin) return RunEvents.Rejected("PHASE_NOT_POST_SPIN");
            // [원본 버그 유지] Kotlin GIVEUP 분기(L1897)는 buildMods에 device·phasePerks를 넣지 않는다
            // — deficit 표시값만 갈리는 생략이지만 DeviceActions의 동일 계열 보존과 일관되게 유지.
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(State.MachineId, State.CharId, State.Perks, State.Curses, "", levels: State.PerkLevels),
                State.PhaseItems);
            long quota = SpinResolver.QuotaOf(State.Stage, mods, State.Asc, State.BossPhase2, DeepRunHooks.DeepPenalty(State));
            long deficit = quota - State.StageExp;
            var fail = StageFlow.ForceGameOver(State, deficit);
            return RunEvents.One(new RunEvent { type = "GAME_OVER", failure = fail });
        }

        // ══════════════════════════════════════════════════════════════════════
        // WEB_PARITY P1 ⑤: 자발적 포기(웹 game.js:1228-1231 giveUp(voluntary=true) / ui.js:849-871
        // "게임 포기 (즉시 결산)" 액션바 버튼 + 확인 시트). SPIN·POST_SPIN 어느 시점이든 호출 가능 —
        // 즉시 게임오버로 결산하되 최종점수 공식은 손대지 않는다(StageFlow.ForceGameOver 그대로 재사용,
        // §8-A scoreModifier 동일 적용). deficitAtFailure는 웹과 동일하게 0으로 표시한다(game.js:2538
        // "shortBy = voluntary ? 0 : short" — 실패가 아니므로 부족분을 보여줄 이유가 없다는 취지).
        // FailureOutcome.Voluntary=true만 다르고 그 외 결과 구조(GAME_OVER RunEvent)는 통상 게임오버와
        // 동일 — GameOverPanel(UI)이 이 플래그로 "실패" 대신 "포기 — 즉시 결산" 문구를 고른다.
        // ══════════════════════════════════════════════════════════════════════
        public IReadOnlyList<RunEvent> GiveUp()
        {
            if (State.Phase != RunPhase.Spin && State.Phase != RunPhase.PostSpin)
                return RunEvents.Rejected("PHASE_NOT_SPIN_OR_POST_SPIN");
            var fail = StageFlow.ForceGameOver(State, 0);
            fail.Voluntary = true;
            return RunEvents.One(new RunEvent { type = "GAME_OVER", failure = fail });
        }

        private static List<RunEvent> WrapSpin(SpinStepResult r)
        {
            switch (r.kind)
            {
                case SpinStepKind.Rejected:
                    return RunEvents.One(new RunEvent { type = "REJECTED", reason = r.spin.rejectReason, spin = r.spin });
                case SpinStepKind.Cleared:
                    return RunEvents.One(new RunEvent { type = "STAGE_CLEARED", spin = r.spin, clear = r.clear });
                // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — A10 2페이즈 보스 재시작(진짜 클리어 아님).
                case SpinStepKind.BossPhase2:
                    return RunEvents.One(new RunEvent { type = "BOSS_PHASE2", spin = r.spin, clear = r.clear });
                case SpinStepKind.Revived:
                    return RunEvents.One(new RunEvent { type = "REVIVED", spin = r.spin, failure = r.failure });
                case SpinStepKind.PostSpin:
                    return RunEvents.One(new RunEvent { type = "POST_SPIN", spin = r.spin, failure = r.failure });
                case SpinStepKind.GameOver:
                    return RunEvents.One(new RunEvent { type = "GAME_OVER", spin = r.spin, failure = r.failure });
                default: // Continue
                    return RunEvents.One(new RunEvent { type = "SPIN_RESULT", spin = r.spin });
            }
        }
    }
}
