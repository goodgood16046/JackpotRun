using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 런 상태머신 — 02_service.md §1-A(state 전이) 실제 코드 분기(L184-200) 기준.
    // KDoc 주석(SlotV2Entities.kt:19)의 EVENT_ITEMSHOP/EVENT_GAMBLE/EVENT_REST/EVENT_CURSE 4개는
    // 실제 코드에 존재하지 않는 오기재 state다(02_service.md §1-A 각주) — 포함하지 않는다.
    // GameOver는 Kotlin 원본엔 없다(게임오버 시 SlotV2RunRow 자체를 DB에서 삭제, L2051) — 인메모리 엔진은
    // "삭제" 대신 종료 상태로 표현해야 RunController(S4)가 결과를 읽고 처리할 수 있으므로 추가했다.
    public enum RunPhase
    {
        CharSelect,
        MachineSelect,
        DeviceSelect,
        DeviceSelect2,
        Spin,
        PostSpin,
        NodeSelect,
        EventAugment,
        EventRelic,
        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — AUGLEVEL 노드에서 레벨업 후보(보유 증강 중
        // AugLevels.IsLevelable && Lv<3)를 오퍼하는 상태. 웹 PHASE.PERK_PICK + _pickKind==="LVL"에
        // 대응(game.js:1622,2142-2145). NodeEvents.PickOffer가 이 phase일 때 perks에 새로 add하는 대신
        // RunState.PerkLevels[id]를 +1한다.
        EventAugLevel,
        EventShop,
        // WEB_PARITY P1 ④: DEVICE 노드 선택 후 [장착하기]/[코인+15] 결정을 기다리는 상태(웹 PHASE.DEVICE_NODE,
        // game.js:1696 `case "DEVICE": r.phase = PHASE.DEVICE_NODE;`) — NodeEvents.TakeDevice가 해소한다.
        DeviceNode,
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 노드/상점 처리가 끝난 뒤 곧장 다음 스테이지
        // SPIN으로 넘어가지 않고 "보상 획득 → 다음 스테이지 인트로" 화면에서 대기한다(웹 PHASE.
        // REWARD_DONE, game.js:1573-1585 `_enterRewardDone`). RunController.ProceedToStage("스테이지 N
        // 시작" 탭)가 해소해 Spin으로 넘어간다. 예외: DEVICE 노드 확정(TakeDevice)은 웹 deviceNodeTake
        // (game.js:2523-2529)처럼 이 화면을 건너뛰고 곧장 Spin으로 간다(NodeEvents.TakeDevice 주석 참조).
        RewardDone,
        GameOver,

        // ── 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — POUCH 오퍼 v3 2-step 커밋 +
        // REST/GAMBLE 심화 2택 + 3스테이지 연계 보너스) — P1 DeviceNode 선례(오퍼 확정을 기다리는
        // 전용 phase) 그대로 6종 신설. 웹은 이 전부를 `r._pickKind` 문자열 하나로 구분하지만(PERK_PICK
        // phase 공유), Unity는 기존 EventAugment/EventRelic/EventAugLevel/DeviceNode 관례(퍽 종류별
        // 전용 RunPhase)를 그대로 따른다 — 작업 지시 "RunPhase 3종 신설"은 POUCH 2-step(EventPouch/
        // EventPouchCost/EventPouchRemove)만 명시했지만, REST_DEEP/GAMBLE_DEEP/SYNAUG_BONUS도 동일하게
        // "사용자 응답을 기다리는 오퍼 상태"라 같은 패턴으로 3종을 추가했다(이탈 사항, 최종 보고에 명시).
        //
        // EventPouch — POUCH 노드(및 JACKPOT 노드, 웹이 같은 `_pickKind="POUCH"`로 라우팅하는 것과
        // 동일하게 재사용) 오퍼 카드(RunState.PouchOptions) 선택 대기. skip/저주(무료)/기본없음(무료
        // 추가)는 여기서 곧장 커밋되고, 실버/골드/프리즘은 EventPouchCost·EventPouchRemove로 이어진다.
        EventPouch,
        // EventPouchCost — 프리즘 특수 카드 전용, 교체 비용 방식 선택(기본 심볼 2개 제거 vs 저주+1).
        EventPouchCost,
        // EventPouchRemove — 제거할 기본 심볼 선택(RunState.RemoveCandidateIds) + 원자적 커밋(검증 실패 시 롤백).
        EventPouchRemove,
        // EventRestDeep — 심화 REST 노드 2택(코인+12 vs 해골 정화, 해골 보유 시만 등장 — 웹 game.js:1624-1634).
        EventRestDeep,
        // EventGambleDeep — 심화 GAMBLE 노드 2택(코인 도박 vs 심볼 도박 — 웹 game.js:1636-1663).
        EventGambleDeep,
        // EventSynAugBonus — 3스테이지 증강 연계 보너스(웹 game.js:2152-2184) — AUGMENT 픽 직후
        // (stage-1)%3==0 조건 충족 시 태그 일치 특수 심볼 무료 오퍼(RunState.PouchOptions 재사용, 2장).
        EventSynAugBonus,
    }

    // 스테이지 클리어 후 제시되는 노드 종류 — 02_service.md §3-E/§5. Kotlin SlotV2Engine.Node enum(ELITE 포함,
    // L2398)은 실제로 clearStage()의 노드풀 생성(L868-871)에 쓰이지 않는 사문화된 enum이라 옮기지 않았다.
    // 대신 clearStage L868-871에서 실제 사용되는 8종("RELIC","SHOP","REST","GAMBLE","EVENT" 상시 +
    // "CURSE","RISK" nextStage>=6)을 그대로 옮긴다. AUGMENT는 매 클리어 필수 포함(항상 3개 중 1개).
    public enum NodeKind
    {
        Augment,
        Relic,
        Shop,
        Rest,
        Gamble,
        Event,
        Curse,
        Risk,
        // ── 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 심화 노드 풀, 웹 game.js:
        // 1439-1494) — 심화 런(DeepMode) 전용 4종. StageFlow.RollDeepNodes만 생성한다(일반 런의
        // RollNextNodes는 절대 이 값들을 넣지 않음).
        // Pouch — 심화 노드풀 첫 슬롯 고정(웹 `nodes=["POUCH", second]`). NodeEvents.ChooseNode가
        // Run/PouchOffer.cs의 오퍼 생성으로 라우팅한다.
        Pouch,
        // Jackpot — 웹 dpool에 stage>=3부터 섞이는 낮은 가중 노드(§9.2 J3). 현재 덱 최다 잭팟태그
        // 기반 특수심볼 3택+스킵 — POUCH와 동일한 EventPouch phase/카드 계약을 재사용한다.
        Jackpot,
        // SymAug/SymRel — 심볼증강/심볼유물 + 관련 일반 증강·유물(deepCompatPool) 혼합 오퍼. 웹은
        // 둘 다 일반 AUGMENT/RELIC과 같은 `_pickKind`(="AUG"/"REL")로 라우팅하지만, Unity는 풀 구성이
        // 달라 노드 선택 시점에 구분이 필요해 별도 NodeKind로 유지한다(오퍼 phase 자체는 EventAugment/
        // EventRelic을 그대로 공유 — Run/PouchOffer.cs 참조).
        SymAug,
        SymRel,
        // WEB_PARITY P1 ④: 보스 클리어 직후에만 등장(웹 game.js:1438,1493 — drops.length일 때만 노드에
        // 추가되는 4번째 옵션). 선택 시 RunState.PendingDeviceDrop을 오퍼로 보여주고 장착/코인 중 택1.
        Device,
        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — StageFlow.ClearStage가 AUGMENT 노드를 확률
        // (기본10%+pity, 상한20%)로 이 노드로 교체한다(웹 game.js:1501-1507). 3택 규칙 중 "AUGMENT 필수
        // 1개" 자리를 대체할 뿐 옵션 개수는 그대로 3개 — Device처럼 "추가" 옵션이 아니다.
        AugLevel,
    }

    // 표시 모드 — SlotV2RunRow.displayMode. UI 연출 선택일 뿐 수치 로직에 영향 없음(카톡 전용 요소 아님 —
    // Unity RunScreen(S6)도 간단/상세/계산 모드 개념을 재사용할 수 있어 그대로 유지).
    public enum DisplayMode
    {
        Simple,
        Normal,
        Calc,
    }

    // 런 세션 상태 — 02_service.md §1-C(SlotV2RunRow, data/SlotV2Entities.kt L14-79) 전 필드 대응.
    // CSV(콤마 직렬화) 필드는 전부 타입 있는 컬렉션으로 재설계했다(설계 원칙 1 / 02_service.md §10-1).
    //
    // [제외 목록 — 카톡 전용 필드, 02_service.md §1-C·§12 근거로 이식하지 않음]
    //   - linkId, ownerKey, ownerNick, ownerUserId : 카카오톡 채팅방/유저 식별자(§12-6 resolveUid 등,
    //     Unity는 별도 플레이어/세이브슬롯 식별자를 쓴다).
    //   - startedAt, lastActionAt : RUN_TTL_MS(10분) 자동 만료·purge는 다수 사용자가 공유하는 챗봇 서버
    //     DB 특유 로직이다(§10-7). 싱글플레이 로컬 세이브의 일시정지/재개는 S5(Unity 저장 어댑터)가
    //     별도로 설계한다.
    //   - pendingOptions(원본 CSV, char/machine/device 선택 후보·상점 오퍼·perk pick 후보 등 다목적 겸용) :
    //     "타입 있는 컬렉션으로 재설계" 원칙에 따라 상태별로 쪼개야 한다. S3(StageFlow)가 다루는 것은
    //     NODE_SELECT의 3택뿐이라 NodeOptions로 좁혀 옮겼다. CHAR_SELECT/MACHINE_SELECT/DEVICE_SELECT(2)/
    //     EVENT_SHOP/EVENT_AUGMENT/EVENT_RELIC의 후보 목록은 그 상태를 실제로 구현하는 S4가 각자 목적에
    //     맞는 전용 타입 필드를 추가해야 한다(하나의 범용 문자열 리스트를 재사용하지 말 것).
    //
    // [보류 — 값은 유지하되 실질 로직 없음]
    //   - devCooldown : 02_service.md §9-A — clearStage에서 -1(하한0)만 되고, 이 값을 set/check해서 장치
    //     사용을 막는 코드가 SlotV2Service.kt/SlotV2Engine.kt 어디에도 없다(원본 자체가 미완성/미사용).
    //     필드는 보존하고 StageFlow가 원본과 동일하게 감소만 시킨다.
    public sealed class RunState
    {
        // ── 이 런의 결정론적 RNG(§S3 결정) ─────────────────────────────────
        // Kotlin은 rng() = Random(System.nanoTime())으로 호출마다 새로 만들어 재현성이 아예 없다
        // (01_engine.md §11-8/ENGINE_PORT_DESIGN.md 원칙 2 "RNG 소비 순서 규칙 유지, 비트스트림 일치는
        // 불필요"). 이 엔진은 런 시작 시 seed 1개로 Rng를 1번 생성해 런 내내 재사용한다 — 같은 seed로
        // 시작한 런은 이 C# 엔진 안에서 항상 같은 결과열을 낸다(자체 재현성 보장).
        public readonly Rng Rng;

        public RunPhase Phase = RunPhase.CharSelect;

        public string CharId = "";
        public string MachineId = "";

        public int Stage = 1;
        public int SpinIndex = 0;
        public long StageExp = 0;
        public long Score = 0;
        public long Coins = 0;

        // 영구(런 내내 유지) — 증강/유물(AUGMENT+RELIC 공용 id 공간) / 저주.
        public readonly List<string> Perks = new List<string>();
        public readonly List<string> Curses = new List<string>();

        // 🎒가방 — 최대 ITEM_SLOTS(=3, StageFlow/ItemUse 쪽 상수, 02_service.md §1-C).
        public readonly List<string> Items = new List<string>();
        // NEXTSPIN 아이템 — 다음 스핀 1회 적용 후 자동 소거.
        public readonly List<string> ArmItems = new List<string>();
        // PHASE 아이템 — 이번 스테이지 내내 적용, 클리어 시 소거.
        public readonly List<string> PhaseItems = new List<string>();

        public int StageBonusSpins = 0;

        // 이번 스테이지에 쓴 특수스핀명령/장치cmd 마커. "RUNSHOP"/"RUNORACLE"만 런 끝까지 보존(클리어 시
        // StageFlow가 그 둘만 남기고 리셋).
        public readonly HashSet<string> UsedCmds = new HashSet<string>();

        public string Device = "";   // 메인 장치(모든 종류)
        public string Device2 = "";  // 보조 장치(ARMED/PEEK만, 후반 해금)

        // NODE_SELECT에서 제시된 3택(02_service.md §3-E). 다른 상태의 선택지는 위 "제외 목록" 주석 참조.
        public readonly List<NodeKind> NodeOptions = new List<NodeKind>();

        // ── S4 전용 상태 필드 (위 "제외 목록" 주석이 예고한 대로 S4가 추가) ──────────────────
        // EVENT_AUGMENT/EVENT_RELIC에서 제시된 후보 퍽 id 목록(NodeEvents.cs). 항상 0~3개.
        public readonly List<string> PerkOfferIds = new List<string>();
        // EVENT_SHOP에서 제시된 상점 6칸(Shop.cs). ShopEntry는 Shop.cs 정의(같은 어셈블리라 순환참조 없음).
        public readonly List<ShopEntry> ShopOffer = new List<ShopEntry>();

        public bool FlameNext = false; // 다음 스핀 EXP -50%
        public bool SeedNext = false;  // 다음 스핀 🌱 성장 예약

        // 직전 스핀 원시 심볼 id — 재굴림/고정/복사/교체/재시험(S4 MANIP 훅)의 원본 입력. 폭탄 제거·
        // 자석 복사 등 Evaluate 내부 변형 "이전" 스냅샷이라 릴에 실제로 보이는 결과와 다를 수 있다 —
        // 표시용으로는 아래 LastCellsFinal을 쓸 것(재굴림 입력 용도로는 계속 이 필드가 정답, 원본
        // Kotlin 계약 그대로 유지).
        public readonly List<string> LastCells = new List<string>();

        // 웹 파리티 P4 Opus 2차검수 필수①(2026-08-09, WEB_PARITY_DESIGN.md §1-A #16) — 직전 스핀의
        // "최종" 칸(웹 `r.lastCells = res.cells`, Evaluate 이후 — 폭탄 제거/자석 복사/성장/와일드 주입
        // 전부 반영됨). CellInfoView가 이 필드를 읽어 릴 표시와 정확히 일치하는 셀 정보를 보여준다.
        // SpinResolver.ResolveSpin(주 경로)·DeviceActions.cs의 MANIP 재계산·도박꾼 무료재굴림·
        // ItemUse.UseRetakeForm(재시험) 총 4곳에서 Evaluate 직후 갱신한다.
        public readonly List<Cell> LastCellsFinal = new List<Cell>();

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 `r.lastResult.notes` — 스핀 3경로가 모두
        // `r.lastResult = res`로 채우는 SpinResult.notes) — STAGE_CLEAR 보드의 "이 스핀에서 얻은 방법"
        // 안내(§2-(W) renderStageClear cs.lastNotes)에 필요해 LastCellsFinal과 동일한 4곳(SpinResolver.
        // ResolveSpin·DeviceActions의 MANIP 재계산·도박꾼 무료재굴림·ItemUse.UseRetakeForm)에서 함께
        // 갱신한다 — LastMods와 달리 UseRetakeForm도 갱신한다(웹 `_freeReroll()`도 `r.lastResult = res`는
        // 하되 `r.lastMods`만 건드리지 않는다, game.js:1222 참조).
        // Opus 2차검수 LOW③(2026-08-09) — LastCellsFinal과 동일하게 readonly 리스트를 Clear+AddRange로
        // 갱신한다(직접 재대입은 SpinResult.notes 내부 리스트 참조를 그대로 공유해 버려, 그 SpinResult가
        // 나중에 재사용/변형되면 캐시가 몰래 바뀔 위험이 있었다).
        public readonly List<string> LastNotes = new List<string>();

        public long LastGain = 0;
        public long LastScoreGain = 0;
        public int LastCoinGain = 0;
        public int LastSet4 = 0;      // 직전 스핀이 runSet4에 더한 기여(0/1) — net-adjust용
        public int LastAdjPairs = 0;  // 직전 스핀이 runAdjPairs에 더한 기여(0/1) — net-adjust용
        public int LastSpinNo = -1;   // 직전 스핀의 SpinIndex(0-base), -1=없음

        public double PendingNextExpMul = 1.0; // 다음 스핀 EXP 배수 예약(보조 코인투입 등). 적용 후 1.0 리셋.

        // 예언(PEEK)/timeline_ticket으로 확정된 다음 스핀 원시 심볼 id. 비어있지 않으면 RNG 없이 그대로 사용.
        public readonly List<string> LockedNext = new List<string>();

        public int DevCooldown = 0; // 위 "보류" 주석 참조 — set/check 로직 없음, 감소만.

        public int RunJackpots = 0;
        public long RunBestSpin = 0;

        public DisplayMode DisplayMode = DisplayMode.Normal;

        // 이번 런 심볼 등장수 누적(symId -> count).
        public readonly Dictionary<string, int> RunSymCounts = new Dictionary<string, int>();

        public int UnluckyGauge = 0;
        public int ClosestClear = -1; // 이번 런 가장 아슬아슬한 클리어 마진(초과 EXP 최솟값), -1=아직 없음

        public bool Survive = false;     // 보험증서 — 이번 스테이지 실패 1회 생존권
        public int DebtStages = 0;       // 빚문서 — 남은 무보상 스테이지 수
        public readonly List<string> PhasePerks = new List<string>(); // 깨진프리즘 임시 perk(이번 스테이지 한정)

        public string HeldAug = ""; // 보류파일(dev_holdfile) 보관 중인 증강 후보 1개, ""=없음

        public bool UsedItemThisRun = false;

        public int RunAdjPairs = 0;
        public int RunPrayWins = 0;
        public int RunLastSpinClears = 0;
        public int RunCloseClears = 0;
        public int RunFastClears = 0;
        public int RunSet4 = 0;

        // 웹 파리티 P3(#9) — 이번 런에서 클리어한 보스 수(통산 누적 아님, 웹 game.js:350 r.stats.bossClears
        // / game.js:1421 `if (boss) r.stats.bossClears += 1`). PlayerLevelTracker.ApplyRunEnd의 runXp
        // 공식(bossClearsThisRun*20)에만 쓰인다 — PlayerProfile.Stats["bossClears"](통산 누적, StatTracker
        // 담당)와는 다른 값이라 혼동하지 말 것.
        public int RunBossClears = 0;

        public int GrowthStack = 0; // 0~5
        public int SnowStack = 0;   // 0~4
        public bool FateBellUsed = false; // Kotlin Int(0/1, 런 1회) → bool로 정리(§CSV 재설계 원칙과 동일 취지)

        public bool RunUsedCmd = false;  // 이번 런 특수 스핀명령(집중/올인/기도/최후) 사용 여부
        public bool RunRerolled = false; // 이번 런 재굴림/조작 장치 사용 여부

        // ── WEB_PARITY P1 ①: 특수스핀 첫 사용 무료 (웹 game.js:347 cmdFreeUsed) ──────────────────
        // 런 단위(종류별 "FOCUS"/"ALLIN"/"PRAY"/"LAST" 첫 1회) — StageFlow.ClearStage의 스테이지 스코프
        // 리셋(UsedCmds.RemoveWhere 등)에서 절대 건드리지 않는다. 발동이 실제로 성공했을 때만
        // SpinResolver.ResolveSpin이 추가한다(코인부족/타이밍 등으로 거부되면 미소진 — 웹과 동일, §2-E).
        public readonly HashSet<string> CmdFreeUsed = new HashSet<string>();

        // ── WEB_PARITY P1 ④: DEVICE 노드 / EVENT 장치획득 — 런 스코프 "영구보유 장치" 캐시 ──────────
        // PlayerProfile.OwnedDevices(Engine/Profile, 이 어셈블리 밖 상위 계층)의 미러. RunController
        // 생성 시 호출측(GameSession)이 프로필의 보유 목록으로 채워 넣고, 런 중 EVENT 10분기표 6번/
        // DEVICE 노드로 새로 얻은 장치도 즉시 여기 추가한다(같은 런 안에서 중복 지급 방지). 실제
        // 프로필 영속화는 이 필드가 아니라 RunEvent.deviceGrantedId를 관찰하는 StatTracker가 담당
        // (엔진은 Engine/Profile을 참조하지 않는다 — 설계 원칙 6).
        public readonly HashSet<string> OwnedDeviceIds = new HashSet<string>();

        // 보스 클리어 직후 DEVICE 노드에 오퍼할 장치 id("" = 이번 클리어에 드랍 없음/이미 소비됨) —
        // 웹 r._drop(game.js:1438,1493-1494) 대응. NodeKind.Device 선택 시(NodeEvents.TakeDevice)
        // 이 값을 소비하고 다시 ""로 리셋한다.
        public string PendingDeviceDrop = "";

        // ── 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12, 웹 game.js:319 perkLevels/_augLevelChance/
        // _augLevelBoost) — 증강 레벨업(Lv1~3). id -> 현재 레벨(딕셔너리에 없으면 Lv1, 웹
        // `r.perkLevels[id] || 1`과 동일 관례 — Perks/RISK/EVENT로 새 증강을 얻어도 별도 초기화 불필요).
        public readonly Dictionary<string, int> PerkLevels = new Dictionary<string, int>();

        // AUGLEVEL 노드 등장 확률(pity) — 기본 10%, 미발동 시 +2%p 누적(상한 20%), 발동 시 10%로 리셋.
        public double AugLevelChance = 0.10;
        // 🖍형광펜(AUGCHANCE) — 웹 game.js:791 `_augLevelBoost += 0.15`. 웹 파리티 P7-3b(WEB_PARITY_
        // DESIGN.md §1-A #19 "Sp 신규 51종")부터 DeepRunHooks.ProcessDeepSpinFollowups가 res.
        // augChanceNext 신호로 이 필드에 실제로 가산한다(이전엔 대응 심볼 효과가 없어 항상 0인 후크뿐
        // 이었다) — StageFlow.ClearStage/RollDeepNodes의 pity 계산이 자동으로 반영.
        public double AugLevelBoost = 0.0;

        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, 웹 game.js:320 `_prismInk`) — 💧프리즘잉크
        // 아이템 사용 시 true. 다음 AUGMENT 노드 오퍼를 강제로 PRISM 티어로 뽑게 하고 소비 시 리셋
        // (NodeEvents.OfferPerks 참조).
        public bool PrismInkActive = false;

        // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑤(WEB_PARITY_DESIGN.md §2, 웹 game.js:320
        // `_prismInkBought`) — 상점에서 "prism_ink" 상품을 이미 구매했으면 런 끝까지 재구매를 막는다
        // (game.js:2350/2356 shopBuy 가드). 아이템 자체를 "사용"하는 것(ItemUse.PrismInkActive와 무관한
        // 별개 플래그)과는 무관 — 순수히 "상점 상품칸에서 다시 살 수 있는가"만 제한한다.
        public bool PrismInkBought = false;

        // ── 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15/#16) ────────────────────────────────
        // 직전 완료 스핀(SpinResolver.ResolveSpin/DeviceActions MANIP 재계산)에 실제로 쓰인 Mods 스냅샷.
        // 웹 r.lastMods(game.js:355,941,1286) 대응 — 셀 정보 탭(cellInfo)이 "지금 이 순간의 mods"가
        // 아니라 "그 칸이 실제로 나온 스핀의 mods"로 분해해야 정확하므로 재계산이 아니라 캐시가 필요하다.
        // 아직 스핀이 없었으면(런 시작 직후) null — CellInfoView가 이 경우 방어적으로 null을 반환한다.
        public Mods LastMods = null;

        // RewardDone 화면에 뜨는 보상 메시지 — 웹 r.rewardMsg(game.js:1574 `_enterRewardDone(msg)`).
        // RewardFlow.Enter가 노드/상점 처리 완료 시점에 채운다(§NodeEvents.cs/Shop.cs 각 분기 참조).
        public string RewardMessage = "";

        // 이번 상점 방문(EVENT_SHOP)에서 구매한 항목의 "emoji+name" 라벨 목록 — 웹 r.shopBought
        // (game.js:355 초기화, 2305 상점 진입 시 리셋, 2358/2492 구매 시 push, 2515-2518 shopExit 소비)
        // 그대로. NodeEvents.ChooseNode의 Shop 분기가 상점 진입 시 비우고, Shop.Buy가 구매마다 추가하며,
        // Shop.Leave가 REWARD_DONE 화면의 "🛒 상점에서 구매: ..." 메시지 조립에 소비한다(소비 후에도
        // 클리어하지 않음 — 다음 상점 진입 시 ChooseNode가 다시 비우므로 굳이 여기서 지울 필요 없음,
        // 웹도 shopExit에서 `this.run.shopBought = [];`로 리셋하지만 이미 소비된 뒤라 결과는 동일).
        public readonly List<string> ShopBoughtLabels = new List<string>();

        // ── 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:121-141 ascMods/game.js:285-291
        // startRun) — 승천(심화 학기) A1~A10. 0=일반. 런 시작 후 절대 바뀌지 않는다(웹 `r.asc = useAsc`,
        // 재대입 없음) — 클램프·해금 상한 판정은 RunController 생성 이전(호출측)에서 끝난다.
        public int Asc = 0;

        // 웹 game.js:291 `graduatedThisRun: false` — 이번 런에서 스테이지15 클리어(2페이즈 보스는 2페이즈
        // 완료 시점)에 도달했는지. StatTracker.ApplyGameOverTracking이 이 플래그로 PlayerProfile.AscMax/
        // BestAscScore/BestAscLevel/Mastery.AscMax 갱신 여부를 결정한다(웹 game.js:2562 그대로).
        public bool GraduatedThisRun = false;

        // 웹 game.js:321 `_bossPhase2: false` — A10 최종보스(스테이지15) 2페이즈 진행 중 표시. 1페이즈
        // 클리어 시 true로 세워지고 요구치가 ×1.3 추가된 채 같은 스테이지를 재시작한다(StageFlow.
        // ClearStage 참조) — 2페이즈까지 클리어해야 진짜 졸업(GraduatedThisRun=true)이 확정된다.
        public bool BossPhase2 = false;

        // 웹 game.js:321 `_devCdUntil: 0` — A9+ 능동장치(코인투입 dev_coin·예언 dev_oracle) 쿨다운.
        // 사용 시 `run.Stage + 2`로 세팅되고, `run.Stage < DevCdUntil`인 동안 해당 장치 사용이 거부된다
        // (DeviceActions.HandleDevCoin/HandlePeek 참조, 웹 game.js:1306,1315).
        public int DevCdUntil = 0;

        // 웹 game.js:425 `r._bannedSym` — A8+ 스테이지 진입마다 무작위 재추첨되는 금지 심볼 id(cherry/
        // book/star/gem 중 1개, ""=없음/asc<8). AscRunHooks.RollBannedSym이 "스테이지 시작" 지점 3곳
        // (런 시작·다음 스테이지 진입·A10 2페이즈 재시작)에서 갱신하고, ApplyRunAscMods가 실제 롤
        // mods에 symbolWeightMul=0으로 반영한다.
        public string BannedSym = "";

        // ── 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 game.js:285-337 startRun 심화 필드군) ──
        // 심화모드(심볼 덱/주머니) — 사실상 제2의 게임. 이 슬라이스(P7-1)는 코어(주머니 추출/덱검증/
        // 압축패널티/deepPity/instant소모/점수격리)만 배선한다 — 심볼퍽/정비소/전공/잭팟태그/피버/
        // 오퍼는 P7-2/3, UI 보드는 P7-4.
        //
        // 웹 game.js:292 `deepMode: wantDeep` — 이 런이 심화모드인지. RunController 생성자가 세팅한
        // 뒤 런 내내 바뀌지 않는다(Asc와 동일 취급). 심화 런은 asc가 항상 0으로 강제된다(웹 game.js:
        // 285-289 "wantDeep이면 asc 강제 0" — RunController 생성자 참조).
        public bool DeepMode = false;

        // 웹 game.js:293 `pouch: wantDeep ? E.startPouch() : null` — 심볼 주머니 { [symId]: count }.
        // 일반 런은 항상 빈 상태(DeepMode=false면 아무도 채우지 않음) — RunController가 DeepMode=true일
        // 때만 Content.Pouch.NewStartPouch()로 채운다. PouchOps.PouchDraw(추출)·Pouch.Validate(검증)가
        // 이 필드를 읽는다.
        public readonly Dictionary<string, int> Pouch = new Dictionary<string, int>();

        // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 `_symUnlockedSet()` — DEFAULT_UNLOCKED_
        // SYMS(58) ∪ profile.symUnlocked) — 이번 런에서 유효한 "심볼 해금" 집합의 미러. RunState.
        // OwnedDeviceIds(§P1 ④ 각주와 동일 관례 — 상위 계층 PlayerProfile은 여기서 직접 참조할 수 없어
        // (설계 원칙 6), 호출측(GameSession→RunController 생성자)이 profile.EffectiveSymUnlocked()로
        // 채워 넣는다. 심화 런이 아니면(DeepMode=false) 아무도 읽지 않지만 항상 채워 둔다(null 가드
        // 불필요). PouchOffer.EnterPouchOffer(POUCH 오퍼)·NodeEvents.PickOffer(3스테이지 연계 보너스
        // 후보 필터)가 이 필드를 읽는다 — P7-1/2/3이 Pouch.DefaultUnlocked로 근사해 두었던 자리를
        // 실해금 값으로 교체.
        public readonly HashSet<string> SymUnlocked = new HashSet<string>();

        // 배치F P2(웹 game.js:337 `deepPity: null`) — 획득 심볼 2스핀 보장 상태. add/upgrade(P7-2/3
        // 보상 지급) 직후 설정되고 DeepRunHooks.ApplyDeepPity가 첫 fresh 굴림에서 소진한다. 이 슬라이스는
        // 지급 진입점(오퍼)이 없어 상태·치환 로직만 준비 — 테스트는 이 필드를 직접 세팅해 검증한다.
        public DeepPityState DeepPity = null;

        // 웹 game.js:308 `deepCompressExtra: 0` — 상점 '덱 압축' 서비스 누적 요구율(+5%씩,
        // DeepRunHooks.DeepPenalty 곱셈 인자). 정비소 자체는 P7-2/3라 이 슬라이스에서는 항상 0으로
        // 고정된다(필드만 미리 준비 — 정비소 슬라이스가 이 값을 갱신하기만 하면 곧장 반영됨).
        public double DeepCompressExtra = 0.0;

        // 웹 game.js:295-304 `deepStats` — 심화 전용 추적(랭킹 오염 방지·요약 + P7-4 심화 업적 카운터
        // 소스). 이번 슬라이스는 상태 골격만 준비한다(작업 지시 8번) — StatTracker 카운터 반영(P7-4)은
        // 아직 이 필드를 읽지 않는다. DeepMode=false면 항상 null.
        public DeepStats DeepStats = null;

        // ── 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 — 심볼퍽·전공·정비소) ──────
        // 웹 game.js:308-310 `deepTotalMaxDelta: 0`/`deepTotalMinDelta: 0`/`deepTagBuff: {}` — 정비소
        // '덱 확장'/'덱 압축'/'태그 강화' 서비스가 누적하는 상태(Run/RepairShop.cs Execute가 갱신).
        // DeepRunHooks.DeepPenalty·RepairShop.Bounds가 소비한다. 일반 런은 항상 0/0/빈 dict(무영향).
        public int DeepTotalMaxDelta = 0;
        public int DeepTotalMinDelta = 0;
        public readonly Dictionary<string, double> DeepTagBuff = new Dictionary<string, double>();

        // 웹 game.js:317-318 `deepArchFamily`/`deepArchTier`(undefined 초기값 → "없음"과 동치) — 계열
        // 아키타입(전공) 발동/승급 감지용 직전 상태 스냅샷. DeepRunHooks.CheckArchetypeChange가 매 정비
        // 구매 후 갱신하고, 변화가 있으면 RunEvent("ARCHETYPE_CHANGED")를 반환한다(UI 토스트는 P7-4).
        public string DeepArchFamily = null;
        public int DeepArchTier = 0;

        // 웹 game.js `symPerkMods`가 읽는 "보유 심볼퍽" 저장소는 별도 필드가 아니라 **위 Perks/PerkLevels
        // 그대로다**(웹 `E.symPerkMods(r.perks, r.pouch, r.perkLevels)` — sa_/sp_/sr_ 접두 id가 일반
        // 증강·유물·저주 id와 전역 겹치지 않아 안전하게 같은 배열/딕셔너리를 공유한다). Content/SymPerks.cs
        // 헤더 주석에 근거 상세.

        // ══════════════════════════════════════════════════════════════════════
        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 잭팟태그/피버/자동소멸/POUCH
        // 오퍼 v3/심화 노드 풀) — 웹 game.js §9.0~§9.2/§3 V3P3/§2 V3P2 상태 필드군.
        // ══════════════════════════════════════════════════════════════════════

        // ── §9.1 J2 피버 게이지 — 웹 game.js `feverGauge`/`feverSpins` ──
        public double FeverGauge = 0.0;
        public int FeverSpins = 0;

        // ── §9.0 J1 잭팟 태그 단회성 신호 — 웹 game.js `_reachBias`/`_jackpotPrismPending`/
        // `_feverJackpotPrism`/`_jackpotCrownPending` + §9.2 J3 `_retryReelPending`/`_retryReelUsed`/
        // `_reachMarkUsed`/`_jackpotCrownUsed`/`_bellTicketUses`/`_jpTicketUses` ──
        public string ReachBiasTag = null;     // 리치 달성 다음 스핀 해당 태그 bias ×1.5 대상(null=없음)
        public int ReachBiasSpinsLeft = 0;
        public bool JackpotPrismPending = false;  // 태그잭팟 → 다음 POUCH 오퍼 프리즘 후보 1장 보장
        public bool FeverJackpotPrism = false;    // 피버잭팟 → 프리즘 보장 추가(1회)
        public bool JackpotCrownPending = false;  // 잭팟왕관 → 프리즘 보장 추가(1회)
        public bool RetryReelPending = false;     // 재도전릴 — 다음 스핀 1칸 재굴림 예약
        public bool RetryReelUsed = false;        // 스테이지 1회 제한(클리어 시 리셋)
        public bool ReachMarkUsed = false;        // 스테이지 1회 제한(클리어 시 리셋)
        public bool JackpotCrownUsed = false;     // 스테이지 1회 제한(클리어 시 리셋)
        public int BellTicketUses = 0;            // 런 2회 제한
        public int JpTicketUses = 0;              // 런 2회 제한(공유 카운터)

        // ── §3 V3P3 자동 소멸 — 웹 game.js `_decayForewarned` ──
        public bool DecayForewarned = false;

        // ── 배치F P6 퍼펙트 드로우 — 웹 game.js `perfectDrawStage`(undefined 초기값). -1=아직 없음
        // (stage는 1부터 시작해 항상 -1과 다르므로 웹의 "undefined !== stage" 판정과 동치). ──
        public int PerfectDrawStage = -1;

        // ── §2 V3P2 POUCH 오퍼 2-step 커밋 — 웹 game.js `r.options`(PERK_PICK 공용) 중 이 슬라이스가
        // 다루는 카드 계약(special/skip) 전용. NodeEvents.ChooseNode(Pouch/Jackpot 노드)·
        // Run/PouchOffer.cs(3스테이지 연계 보너스)가 채운다. ──
        public readonly List<PouchOfferCard> PouchOptions = new List<PouchOfferCard>();
        // 웹 `r._pendingSpecial` — 실버/골드/프리즘 특수 카드 픽 확정 전 임시 보관(교체 대상/비용 결정 중).
        public PouchPendingSpecial PendingSpecial = null;
        // EventPouchRemove 오퍼 — 웹 `baseSymbols`(그 자리에서 즉석 계산돼 옵션 목록으로 쓰였다가 버려짐).
        // Unity는 RunPhase가 별도 상태라 재계산 대신 생성 시점에 스냅샷해 둔다(웹과 동일 값 — 둘 다
        // POUCH_COST/POUCH_REMOVE 진입 시점의 run.Pouch 스냅샷).
        public readonly List<string> RemoveCandidateIds = new List<string>();

        // ── §3 Step 2/3 REST/GAMBLE 심화 2택 — 웹 `r.options`(id만 필요, PERK_PICK 공용) ──
        public readonly List<string> DeepChoiceIds = new List<string>();

        // ══════════════════════════════════════════════════════════════════════
        // 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종 전면 이식") — 웹 game.js
        // §Phase4 `_applyDeepSpinMeta`(768-862)·`_openShop`/`_freshShop`(2304-2347)·`_beginStage`
        // (407-422)가 참조하는 다음스핀/상점/보스/fuse 상태 필드군.
        // ══════════════════════════════════════════════════════════════════════

        // 🌱씨앗/🌿새싹 — 다음 스핀 성장 예약("ANY"|"HIGH"|null). SpinResolver.RollCells가 소비 예정
        // (웹 `_growNextRoll`) — 이번 슬라이스는 신호 저장까지만, 실제 성장 치환은 후속 슬라이스.
        public string GrowNext = null;
        // ⏳모래시계 — 이번 스핀 EXP 30% 다음 스핀 이월. SpinResolver.ResolveSpin이 소진.
        public long CarryOverExp = 0;
        // 🧾영수증/🎟쿠폰/🛒장바구니 — 다음 상점 1회 적용 플래그(Shop.FreshOffer가 소진).
        public bool DeepShopDiscount = false;
        public bool DeepShopCoupon = false;
        public int DeepShopSlotBonus = 0;
        // 🛡방패/📋시험지 — 다음 보스 스핀 1회용(SpinResolver.ResolveSpin이 소진).
        public bool BossShield = false;
        public bool BossExempt = false;
        // 🧿저주눈(즉시)/🔮수정구(상점 진입 시 이관) — 다음 주머니(POUCH) 오퍼 후보 +N(상한 2).
        public int DeepRewardBonus = 0;
        public int DeepCrystalPending = 0;
        // 💳검은카드 — 다음 상점 1개 무료(상점 진입 시 세팅, Shop.Buy가 소진).
        public bool BlackCardShopFree = false;
        // 🧷안전핀노트 — 이번 스테이지 등장 마킹(StageFlow.RollDeepNodes의 AUGLEVEL pity 실패 분기가 소비).
        public bool SafePinActive = false;
        // 🌀운명의소용돌이 — 2번째 굴림 비교(SpinResolver.ResolveSpin)·소비(DeepRunHooks.
        // ProcessDeepSpinFollowups). Opus 2차검수(P7-3b) [LOW 일괄] — 웹 실사용 quirk 재확인 결과
        // 스테이지 스코프가 아니라 런 스코프(런 전체 1회)로 통일 — 전설 등급 희소성과 맞물려 실질
        // "런 1회"로만 관측되는 웹 동작을 그대로 재현(기존 스테이지번호 트래킹 방식에서 단순 bool로 전환).
        public bool FateVortexUsed = false;
        public bool FateVortexConsumed = false;

        public RunState(long seed)
        {
            Rng = new Rng(seed);
        }
    }
}
