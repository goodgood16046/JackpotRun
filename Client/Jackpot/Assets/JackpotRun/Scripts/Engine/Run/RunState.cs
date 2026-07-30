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
        EventShop,
        GameOver,
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

        public bool FlameNext = false; // 다음 스핀 EXP -50%
        public bool SeedNext = false;  // 다음 스핀 🌱 성장 예약

        // 직전 스핀 원시 심볼 id — 재굴림/고정/복사/교체/재시험(S4 MANIP 훅)의 원본.
        public readonly List<string> LastCells = new List<string>();
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

        public int GrowthStack = 0; // 0~5
        public int SnowStack = 0;   // 0~4
        public bool FateBellUsed = false; // Kotlin Int(0/1, 런 1회) → bool로 정리(§CSV 재설계 원칙과 동일 취지)

        public bool RunUsedCmd = false;  // 이번 런 특수 스핀명령(집중/올인/기도/최후) 사용 여부
        public bool RunRerolled = false; // 이번 런 재굴림/조작 장치 사용 여부

        public RunState(long seed)
        {
            Rng = new Rng(seed);
        }
    }
}
