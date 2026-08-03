# 잭팟런 엔진 C# 이식 설계서 (2026-07-30, Fable)

Kotlin 원본(`kotlin-reference/`, 커밋 c73452c)을 Unity용 C# 엔진으로 이식한다.
**정밀 사양은 `Docs/EngineSpec/01_engine.md`·`02_service.md`·`03_meta.md`가 계약서다** —
구현자는 반드시 스펙 문서와 Kotlin 원본을 직접 대조하며 작성한다. 이 문서는 아키텍처·타입 계약·슬라이스 계획만 정의한다.

## 원칙

1. **순수 C# 엔진**: `Client/Jackpot/Assets/JackpotRun/Scripts/Engine/**`는 `System.*`만 사용,
   `UnityEngine` 참조 금지. → `Client/Jackpot/Tools/EngineTests/`(dotnet net8.0 콘솔)로 에디터 없이
   골든 테스트 실행. csproj에 `<LangVersion>9</LangVersion>` 고정(Unity 2022.3 호환 검증).
2. **충실도 목표 = 공식·확률 분포·수치의 일치**. RNG 비트스트림까지 Kotlin과 동일할 필요는 없다
   (자체 시드 재현성만 보장). 단 **RNG 소비 순서 규칙**(빈 컬렉션은 미소비 등, 01_engine §10)은 유지.
3. **콘텐츠 테이블은 C# 코드로 전사**(Kotlin과 동일 방식). id는 catalog.json과 1:1 —
   테스트에서 교차 검증. 효과는 `Dictionary<string,double>` 효과키 방식 유지(해석은 Mods/Resolver).
4. 원본 버그로 판정된 것(02_service 발견 2·5번, 01_engine 부록 A)은 **원본 동작 그대로 이식**하되
   코드에 `// [원본 버그 유지]` 주석 + 설계 부록에 목록화. 수정은 밸런스 패치 단계에서 별도 결정.
5. 카톡 텍스트 명령은 **typed action API**로 대체(§RunController). 문자열 파싱 이식 금지.
6. 저장은 엔진 밖: 엔진은 인메모리 상태만. Unity 어댑터가 JsonUtility DTO로 영속화(별도 슬라이스).
7. 숫자 표기는 기존 `NumberFormat` 규칙 재사용(소수 2자리·끝 0 제거).

## 디렉터리 / 공유 타입 계약

```
Assets/JackpotRun/Scripts/Engine/
├─ Core/    Rng.cs, Formulas.cs, StatReq.cs, Tier.cs
├─ Content/ Symbols.cs, Machines.cs, Characters.cs, Bosses.cs,
│           Perks.cs(증강·유물·저주), Items.cs, Devices.cs, Sets.cs,
│           Schools.cs(연구+PERK_GATE_OVERRIDES 45), Achievements.cs(482, S5에서)
├─ Run/     RunState.cs, Mods.cs, SpinResolver.cs, StageFlow.cs,
│           Shop.cs, ItemUse.cs, DeviceActions.cs, RunController.cs, RunEvent.cs
└─ Profile/ PlayerProfile.cs, AchievementEngine.cs (S5)
```

네임스페이스 `JackpotRun.Engine` (하위 구분 없음 — 단일 어셈블리 내 폴더 구분만).

### 공유 타입 (S1이 확정, S2+는 이 시그니처를 그대로 사용)

```csharp
public enum Tier { SILVER, GOLD, PRISM }
public enum PCat { AUGMENT, RELIC, CURSE, ITEM }
public enum Sym { /* 01_engine §2의 14종, 선언 순서 = Kotlin 순서 */ }

public sealed class StatReq { public string key; public long value; }
public static class Unlocks { public static bool Meets(IReadOnlyList<StatReq> req, IReadOnlyDictionary<string,long> stat); }

public sealed class Rng {
    public Rng(long seed);
    public int Next(int boundExclusive);
    public double NextDouble();
    public T Pick<T>(IReadOnlyList<T> list);            // 빈 리스트 = 예외
    public T PickOrDefault<T>(IReadOnlyList<T> list);   // 빈 리스트 = default, RNG 미소비 (Kotlin randomOrNull)
    public void Shuffle<T>(IList<T> list);              // Fisher-Yates
    public IReadOnlyList<T> WeightedPick<T>(IReadOnlyList<(T item, double w)> pool, int count); // 스펙 §10 방식
}

public sealed class SymInfo { public Sym sym; public string emoji; public long exp; public long score; public bool dormant; }
public sealed class Boss { public string id, name; public int bonusSpins; public double quotaMul; public string[] counterTags; }
public sealed class Machine { public string id, name, emoji; public double scoreMod;
    public Dictionary<Sym,double> weightMul; public Dictionary<Sym,double> weightAdd; public List<StatReq> unlockReq; }
public sealed class Character { public string id, name, emoji; public List<StatReq> unlockReq;
    /* 효과는 Mods.BuildMods의 id별 case로 구현 (Kotlin buildMods와 동일 구조) */ }
public sealed class Perk { public string id, name, emoji, desc; public PCat cat; public Tier tier;
    public int price; public Dictionary<string,double> fx; public string school; }
public sealed class ItemDef { public string id, name, emoji, desc; public string kind; /* NEXTSPIN|PHASE|INSTANT */
    public int coinCost; public Dictionary<string,double> fx; }
public sealed class DeviceDef { public string id, name, emoji, desc; public string kind; /* 01_engine §8 DevKind */
    public bool rare; public string unlockAch; public Dictionary<string,double> fx; }
public sealed class SetEffect { public string id, name, desc; public string[] requires;
    public string reqChar, reqMachine, reqDevice;   // Kotlin 원본 게이트 — 33종 중 14종 사용 (buildMods L1958-1960)
    public Dictionary<string,double> fx; }
```

콘텐츠 정적 클래스 규격: `public static class Machines { public static readonly Machine[] All; public static Machine ById(string id); }`
— Characters/Perks/Items/Devices/Sets/Bosses/Schools 모두 동일 패턴. **개수 상수**(`public const int Count`)를 두고
테스트가 80/61/16/73/16/16/33/4를 검증한다.

- **Perk·ItemDef·DeviceDef·SetEffect 클래스 정의 파일**: `Engine/Content/ContentTypes.cs` (Fable 작성, 2026-07-30 확정).
- **fx 효과키 명명 규칙** (S2b 제안 승인): Kotlin `Mods` 실제 필드명을 그대로 키로 쓰고, 심볼/태그별
  오버레이는 점 표기 — 예: `"symbolWeightMul.CHERRY"`. S3 Mods.cs는 이 규칙으로 해석한다.

### RunController (S3~S4 — UI 계약)

```csharp
public sealed class RunController {
    public RunController(PlayerProfileView profile, string charId, string machineId, string deviceId, long seed);
    public RunState State { get; }                      // 읽기 전용 뷰로 노출
    public IReadOnlyList<RunEvent> Do(RunAction a);     // 모든 상호작용 단일 진입점
}
public abstract class RunAction { /* Spin, ChooseNode(int), BuyOffer(int), RerollShop, LeaveShop,
    UseItem(string id), DeviceCmd(kind, arg), PickAugment(int), Continue ... — 02_service §7 명령 전수 매핑 */ }
public sealed class RunEvent { public string type; public string text; /* + 타입별 페이로드 필드 */ }
```

RunEvent는 UI가 연출로 번역할 **구조화 이벤트**(스핀 결과 릴, EXP 증감 내역, 세트 발동, 상점 오퍼 목록,
스테이지 클리어 요약 등). 카톡 출력 문자열을 그대로 만들지 말 것 — 데이터를 담아라.

## 검증 (Tools/EngineTests)

`EngineTests.csproj`: net8.0, LangVersion 9, `<Compile Include="..\..\Assets\JackpotRun\Scripts\Engine\**\*.cs" />`.
`Program.cs` — 미니 어서션 러너(실패 시 exit 1, 실패 목록 출력). 테스트 카테고리:

1. **골든 공식**: quota(1..40)·stageClearScore 대표 케이스·accountExp/expForLevel 곡선 —
   기대값은 Kotlin 식을 **손으로 계산해 주석에 산출 과정 명기**(C# 결과 복붙 금지 — 순환 검증 방지).
2. **콘텐츠 무결성**: 개수(80/61/16/73/16/16/33/4/482), id 중복 0, catalog.json과 id 교차 대조
   (엔진에만 있는 id / catalog에만 있는 id 리포트 — dev 4종 무아트는 허용 목록).
3. **확률표**: 머신별 최종 가중치(기본 가중치 × weightMul + weightAdd)가 스펙 표와 일치.
4. **시뮬레이션 스모크**(S3 이후): 고정 시드 1,000런 자동 플레이 — 예외 0, 클리어율·평균점수 출력(밸런스 육안 확인용).

## 슬라이스 계획 (각 슬라이스 = Sonnet 구현 → Opus 검수 → Fable 최종)

| 슬라이스 | 내용 | 의존 |
|---|---|---|
| **S1** | Core(Rng·Formulas·StatReq·Tier·Unlocks) + Content 기본(Symbols·Machines·Characters·Bosses) + EngineTests 하네스 + 골든 테스트 1·2(부분)·3 | — |
| **S2a** | Perks.cs — 증강 80·유물 61·저주 16 전사 (id·name·emoji·desc·tier·price·school·fx) | 타입은 본 문서 |
| **S2b** | Items 73 + Devices 16 + Sets 33 + Schools/GateOverrides 45 전사 | 타입은 본 문서 |
| **S3** | RunState·Mods(buildMods)·SpinResolver(훅 순서!)·StageFlow(노드/보스/클리어/폭망) | S1·S2 |
| **S4** | Shop·ItemUse·DeviceActions·RunController 완성 + 시뮬레이션 스모크 | S3 |
| **S5** | PlayerProfile·AchievementEngine(482)·해금 통합 + Unity 저장 어댑터(JsonUtility) | S3 |
| **S6** | RunScreen UI(uGUI, 기존 UiFactory 활용) + PickScreen "시작 예약"→실제 런 시작 연결 | S4·S5 |

## S6 — 게임 화면 UI (2026-07-31 확정)

기존 UI 규칙(코드 생성 uGUI·UiFactory·레거시 Text·1080×1920 다크 테마) 그대로. 파일:

- `Scripts/Game/GameSession.cs` (namespace JackpotRun.Game) — 수명주기 접착: ProfileStore.Load →
  `RunController(char, mac, dev, seed=현재틱, profile.Stats참조, dev2)` 생성 → 매 `Do(action)` 반환
  이벤트를 StatTracker에 공급 → GAME_OVER 시 기록 갱신(bestScore/bestStage/runs/totalScore)·
  AchievementEngine.Evaluate·ProfileStore.Save. **seed는 이 파일에서만 시각 기반 생성**(엔진은 순수 유지).
- `Scripts/UI/RunScreen.cs` + `Scripts/UI/RunPanels.cs` — 게임 화면. 구성:
  상단 HUD(스테이지·요구 EXP 진행바·남은 스핀·코인·점수·저주 수), 릴 5~6칸(심볼 이모지 타일 +
  획득 EXP/점수 라인), 스핀 노트 피드(outcomeNotes 최근 6줄), 하단 버튼열(스핀 + 특수모드
  올인/집중/기도/막판 — 사용가능 조건은 엔진 거부에 맡기고 거부 사유 토스트), 가방(아이템 사용),
  장치 명령 버튼(장착 장치의 kind별 — MANIP은 칸 선택 팝업).
  Phase 패널: NodeSelect(3택 카드) · PerkOffer(3카드 — 스프라이트+이름+desc, offerHeldIncluded/
  offerSynergyPerkId/offerTierBumped 배지) · Shop(오퍼 목록+가격+리롤6+나가기) · PostSpin(만회 버튼:
  GREROL/장치 or 포기) · GameOver(최종 점수·등급·기록 갱신 표시·새 업적 목록·[메뉴로]).
- 수정: `JackpotRunApp.cs`(ShowRun(char,mac,dev) 추가·GameSession 보유), `PickScreen.cs`("시작 예약"
  버튼 → `app.ShowRun(선택 조합)` — 데모 메시지 대체), `MainMenuScreen.cs`(프로필 요약 줄: 최고점수·
  런 수·업적 n/482 — ProfileStore 로드).
- **catalog 스프라이트 키 매핑**: 엔진 퍼크 id는 무접두("study"), catalog id는 접두("aug_study").
  헬퍼 `CatalogIdOf(PCat, id)` — AUGMENT→"aug_"·RELIC→"rel_"·CURSE→"cur_"·ITEM→"item_"·장치는 dev_ 그대로.
  `JackpotCatalog.Get(catalogId)` → `LoadSprite`. 스프라이트 없으면 이모지 폴백(기존 규약).
- RunEvent 계약 주의(RunController.cs 헤더): STAGE_CLEARED의 spin.result null 가드, REJECTED 토스트.
- 검증: csc 스모크 컴파일 + 에디터 리프레시 + 플레이모드 로그 무예외(기존 방식). dotnet 테스트 대상 아님.

## S7 — UI 전면 개편: 씬 기반 uGUI + 아트 활용 + 연출 (2026-07-31 확정)

기존 코드생성 UI(S6)는 검증용 골격이었다 — 사용자 판정: 이미지 활용 부족·연출 부재·룩 미달.
**씬 기반으로 전면 재구축한다.** 엔진·GameSession·RunEvent 계약은 불변(뷰 계층만 교체).

### 원칙
- **씬이 진실**: `Assets/JackpotRun/Scenes/JackpotRun.unity` — Canvas·화면·팝업이 실제 씬 오브젝트.
  에디터에서 수정 가능해야 한다. 단, 씬은 손으로 만들지 않고 **SceneBuilder 에디터 스크립트**가
  결정론적으로 생성한다(메뉴 JackpotRun/Build UI Scene — 재실행 = 재생성, 파괴적이므로 확인 다이얼로그).
- 뷰 컨트롤러는 MonoBehaviour + [SerializeField] 참조(빌더가 와이어링). 런타임 코드생성 금지.
- 기존 `JackpotRunApp` 부트스트랩은 씬에 `AppRoot`가 있으면 아무것도 하지 않는다(폴백 유지).
- 외부 패키지 금지 — 트윈은 자체 코루틴 헬퍼(`UiTween`), 스프라이트는 절차 생성 PNG 에셋.
- SampleScene은 빌드 목록에서 제외, JackpotRun.unity가 유일한 빌드 씬.

### 파일 구성
```
Scripts/UI2/                     # 새 뷰 계층 (기존 Scripts/UI는 S7 완료 후 제거 예정)
├─ Kit/UiTween.cs               # Float/Move/Scale/Fade/Shake/CountUp + ease(OutBack/OutCubic/OutQuad)
├─ Kit/UiKit.cs                 # 팔레트·텍스트 스타일·티어색·공통 생성 헬퍼(뷰가 참조)
├─ Kit/PressFx.cs               # 버튼 프레스 스케일(0.96)·비활성 알파
├─ AppRoot.cs                   # 엔트리: 프로필 로드, ScreenRouter 보유, GameSession 수명주기(기존 로직 이관)
├─ ScreenRouter.cs              # 화면 전환 — CanvasGroup 페이드(0.18s) + 활성화 토글, Overlay/Toast 관리
├─ MenuView.cs / PickView.cs / DexView.cs
├─ Run/RunView.cs               # HUD·릴·노트·버튼열 오케스트레이션 (RunEvent 스트림 소비)
├─ Run/ReelView.cs              # SymbolCell 5~6개 — 스핀 연출 담당
├─ Run/HudView.cs, NotesFeed.cs
├─ Run/Panels/NodePanel.cs, PerkOfferPanel.cs, ShopPanel.cs, PostSpinPanel.cs, GameOverPanel.cs, BagPopup.cs, ManipPickPopup.cs
└─ ToastManager.cs
Editor/UiSceneBuilder.cs         # 씬+프리팹 생성기 (아래 사양)
Editor/UiSpriteGen.cs            # 절차 스프라이트 PNG 생성 → Assets/JackpotRun/Art/UI/
```

### 절차 생성 아트 (UiSpriteGen — 빌더가 1회 실행, 결과는 커밋되는 에셋)
- 9-slice 라운드 사각형: `panel_r24`, `card_r16`, `chip_r999`(pill), `outline_r16`(테두리만, 선택 글로우용)
- 수직 그라데이션 카드 배경 `card_grad`(상단 밝게 +8%), 릴 셀 홈 `cell_inset`(내부 그림자 느낌 2px 어둡게)
- EXP 바: `bar_bg_r12` / `bar_fill_r12`
- 심볼 타일 14종: `sym_<id>.png` — 심볼 고유색 라운드 타일(색상표: cherry #E5484D · book #4C8DFF · star #F5C518 · gem #7C5CFF · crown #FFB300 · skull #9BA3B4(암배경 #2A0F14) · coin #E8B93C · flame #FF6B35 · magnet #5B8CFF · bomb #3A4051 · dice #E8EAF2 · seed #4CAF50 · wild #00C2A8 · key #C9A227) + 중앙 이모지는 뷰에서 Text 오버레이. 생성 크기 256, Sprite/FullRect, 9-slice border 32(타일류만).

### 화면 사양 (1080×1920 세로 고정)
- **공통**: 배경 #0B0E1A, 패널 #151A2E, 카드 #1B2138+grad, 강조 #FFD23F, 폰트 Pretendard(기존 로더).
  화면 전환 페이드, 모든 버튼 PressFx, 스크롤은 Elastic.
- **MenuView**: 타이틀 로고 텍스트(72pt, 골드 그라데이션 대신 골드+그림자), 대표 캐릭터 아트 3장 캐러셀(좌우 슬라이드 4s 루프, char 스프라이트 512 표시), 버튼 [게임 시작]·[도감], 하단 프로필 요약 카드(최고점수·런·업적 n/482 — ProfileStore).
- **PickView**: 실프로필 해금 연동(**데모 데이터 제거** — chars/machines: profile.IsCharUnlocked/IsMachineUnlocked, 장치: profile.OwnedDevices; 힌트는 pick.unlock). 카드 = 아트 대형(상단 정사각 ~300px, 잠금 시 그레이스케일 대신 어둡게+자물쇠 오버레이), 이름+난이도 배지, eff 1줄, 탭 전환 슬라이드(0.15s), 선택 시 outline 글로우 펄스 + 요약 패널 갱신은 기존 Evaluate 로직 이관. 시작 버튼 → AppRoot.StartRun.
- **RunView 연출 (핵심)**:
  - 스핀: 버튼 → 릴 셀들이 0.45s 동안 심볼 순환(0.05s 간격 랜덤 교체) 후 **왼쪽부터 0.08s 스태거로 정지**(OutBack 스케일 바운스). 정지 후 획득 라인 표시: EXP CountUp(0.3s), 코인/점수 델타 플로팅 텍스트(+N 떠오르며 페이드).
  - 세트/잭팟: set3 = 해당 셀 글로우, set4 = 화면 플래시(흰 6% 알파 0.12s), 잭팟(전칸) = 플래시+셰이크(6px 0.3s)+"JACKPOT" 배너.
  - EXP 바: 트윈 채움, 요구치 도달 순간 골드 펄스. 해골 페널티: 셀 흔들림+적색 틴트.
  - 불운 게이지 🍀 5칸 표시(HUD), 가득 시 펄스.
  - 스테이지 클리어: 상단에서 등급 배너 드롭(OutBack)+점수 CountUp → 1.0s 후 노드 패널 슬라이드업.
  - 보스 스테이지: HUD 테두리 적색 틴트 + 진입 배너("보스: <이름>").
  - 특수모드 버튼: 사용 불가 시 흐림, 사용 시 아이콘 강조. 장치 버튼은 catalog 스프라이트 아이콘 사용(64px).
  - PostSpin: 어둡게+만회 버튼 등장, GameOver: 딤 0.3s → 패널 스케일인 → 신규 업적 스태거 리스트(0.05s 간격).
- **PerkOfferPanel/ShopPanel**: 카드 3장 스태거 팝인(0.08s, OutBack), 아트 대형(200px), 티어 리본(실버/골드/프리즘 색), 시너지 주입 카드는 🧩 배지 + 보라 테두리, 보류 카드는 🗂️ 배지. 상점은 가격 pill + 코인 부족 시 흔들림 피드백.
- **DexView**: 카테고리 탭(가로 스크롤 pill), 3열 그리드(아트 300px), 잠금 어둡게+자물쇠, 상세 팝업(아트 512 + 스탯) — 기존 로직 이관하되 실프로필 진행도(업적 달성 체크) 표시.

### SceneBuilder 사양
- 메뉴 `JackpotRun/Build UI Scene`: ① UiSpriteGen 실행(없는 것만) ② 씬 생성/덮어쓰기(확인 후)
  ③ Canvas(1080×1920, match 0.5, sortingOrder 100)+EventSystem+AppRoot+ScreenRouter+화면 4종+Overlay 구성,
  모든 [SerializeField] 와이어링 ④ Build Settings 씬 목록을 JackpotRun.unity 단독으로 설정 ⑤ 저장.
- 반복 실행 안전(기존 씬 삭제 후 재생성). 생성물은 전부 커밋 대상.

### 이관 규칙
- 기존 Scripts/UI의 **로직**(정렬/필터/Evaluate 연동/RunEvent 분기/카탈로그 매핑)은 재사용·이관하되
  레이아웃 코드는 버린다. Scripts/UI는 S7 검수 통과 후 일괄 삭제(별도 커밋).
- RunEvent 계약(RunController.cs 헤더) 준수 — STAGE_CLEARED result null 가드 포함.
- 검증: MCP로 씬 빌드 실행 → 플레이 → 각 화면 스크린샷 → Fable 육안 검수 루프.

## S7c — 카메라 + 파티클 이펙트 (2026-07-31 확정)

현 씬은 카메라 0개 + ScreenSpaceOverlay다. Overlay 캔버스 아래의 ParticleSystem은 **렌더되지 않는다**.
파티클을 쓰려면 카메라가 필수다.

### 렌더링 전환 (SceneBuilder)
- `UICamera` 생성: Orthographic, size 5, depth 0, clearFlags SolidColor(#0B0E1A), tag "MainCamera",
  position (0,0,-100), cullingMask Everything, allowHDR/MSAA 기본.
- `JackpotRunCanvas`: renderMode = **ScreenSpaceCamera**, worldCamera = UICamera, planeDistance = 100.
  sortingOrder 100 유지. → 캔버스 로컬 1unit = 1px(레퍼런스 1080×1920 기준)이라 파티클 크기도 px 단위로 잡는다.
- 파티클은 캔버스 하위(연출 지점)에 배치하고 `ParticleSystemRenderer.sortingOrder`로 UI 위/아래를 정한다:
  배경 앰비언트 = canvas.sortingOrder-1(=99), 일반 연출 = 150, 화면 전체 연출(잭팟/클리어) = 250.
  `sortingLayerName`은 기본("Default") 유지, `renderMode = Billboard`, `alignment = View`.

### 파티클 에셋 생성 (`Editor/FxPrefabGen.cs`) — 절차 생성, 외부 패키지 금지
- 텍스처: 소프트 원형 도트(`Art/FX/dot_soft.png`, 64×64, 방사 알파 그라데이션), 별(`star_soft.png`, 4각 스파클), 사각 조각(`confetti.png`, 8×12 단색).
- 머티리얼: `Shader.Find("Particles/Standard Unlit")` → 없으면 `"Legacy Shaders/Particles/Additive"` 폴백.
  **생성한 셰이더를 GraphicsSettings의 Always Included Shaders에 추가**(빌드에서 스트립 방지).
  가산합성(Additive) 머티리얼 `fx_add.mat`, 알파블렌드 `fx_alpha.mat`.
- 프리팹 11종 → `Resources/JackpotRun/FX/<id>.prefab` (런타임 Resources.Load):

| id | 트리거 | 사양 |
|---|---|---|
| `fx_spin_stop` | 릴 셀 정지마다 | 스파크 8개, 0.25s, 셀 크기 방사, 심볼색(런타임 startColor 주입), size 10~18 |
| `fx_set_hit` | 세트 3/4 성립 | 링 확산(shape Circle radius 60, burst 14) + 반짝, 0.4s, 골드 |
| `fx_jackpot` | 전칸 일치 | 컨페티 80개(중력 400, 회전, confetti 텍스처, 1.4s) + 중앙 방사 버스트 30개(가산, 골드/화이트) |
| `fx_exp_gain` | EXP 바 채움 | 바 끝점에서 흐르는 트레일 12개/초, 0.4s, 시안(#34D3C0) |
| `fx_coin` | 코인 획득 | 코인색 도트 3~8개가 HUD 코인 라벨로 날아감(런타임 목표점 지정, 0.5s) |
| `fx_clear` | 스테이지 클리어 | 별 낙하 40개(상단 라인 emitter, 1.2s) + 배너 뒤 광채 펄스 |
| `fx_boss` | 보스 스테이지 진입 | 붉은 잔불 상승 루프(HUD 테두리, 20개/초, 0.6 알파) — 스테이지 동안 유지 |
| `fx_skull` | 해골 페널티 | 검은 연기 퍼프 6개, 0.5s, 알파블렌드, 상승 |
| `fx_perk_pick` | 퍼크 선택 | 카드 중심에서 티어색 스파클 24개 폭발, 0.6s |
| `fx_gameover` | 게임오버 패널 | 재 낙하 30개/초 루프, 어두운 회색, 알파 0.4 |
| `fx_menu_ambient` | 메뉴 화면 상시 | 골드 먼지 상승 6개/초 루프, 알파 0.25, size 6~12 |

- 프리팹 루트에 `ParticleSystem` + `ParticleSystemRenderer`(머티리얼·sortingOrder 지정) + RectTransform 불필요(Transform).
  `playOnAwake=false`, `stopAction=None`(풀 재사용), 루프형만 `loop=true`.

### 런타임 API (`Scripts/UI2/Fx/FxKit.cs`)
```csharp
public enum FxId { SpinStop, SetHit, Jackpot, ExpGain, Coin, Clear, Boss, Skull, PerkPick, GameOver, MenuAmbient }
public sealed class FxKit : MonoBehaviour {           // AppRoot가 보유, 캔버스 하위 "FxLayer"에 스폰
    public static FxKit I { get; }
    public ParticleSystem Play(FxId id, RectTransform anchor, Color? tint = null);   // anchor 중심에 1회 재생
    public ParticleSystem PlayAt(FxId id, Vector2 canvasLocalPos, Color? tint = null);
    public ParticleSystem PlayFlyTo(FxId id, RectTransform from, RectTransform to, int count); // 코인 등
    public ParticleSystem PlayLoop(FxId id, RectTransform anchor, Color? tint = null); // 핸들 반환 — 호출측이 Stop
    public void StopLoop(ParticleSystem handle);
}
```
- 인스턴스 풀(프리팹별 최대 8), `Resources.Load<GameObject>("JackpotRun/FX/" + id)` 지연 로드, 미존재 시 무시(null 반환·예외 금지).
- 좌표 변환: `RectTransformUtility.CalculateRelativeRectTransformBounds` 대신 `anchor.TransformPoint(anchor.rect.center)` → FxLayer 로컬로 `InverseTransformPoint`.

### 연출 훅 (기존 뷰에 호출 추가 — 로직 변경 금지)
- `ReelView`: 셀 정지마다 `SpinStop`(심볼색), set3/4 시 `SetHit`, 잭팟 시 `Jackpot`, 해골 칸 `Skull`.
- `HudView`: EXP 채움 중 `ExpGain`, 코인 증가 시 `Coin`(릴→코인 라벨 flyTo), 보스 스테이지 `Boss` 루프(스테이지 종료 시 Stop).
- `NodePanel`(클리어 배너): `Clear`. `PerkOfferPanel`: 카드 선택 시 `PerkPick`(티어색).
- `GameOverPanel`: 표시 중 `GameOver` 루프. `MenuView`: 활성 동안 `MenuAmbient` 루프.

### 주의
- 파티클 시간은 `Time.unscaledDeltaTime` 불필요(타임스케일 조작 없음) — 기본 사용.
- 모바일 성능: 동시 파티클 총량 상한 ~300, 프리팹 maxParticles 개별 지정.
- 파티클이 UI 클릭을 막지 않도록 FxLayer에 `CanvasGroup{blocksRaycasts=false, interactable=false}`.

## S8 — 씬 분리: Intro / Play (2026-07-31 확정)

단일 씬(JackpotRun.unity)을 **Intro(로그인·메뉴·조합선택·도감) + Play(런 플레이)** 로 나눈다.
런 씬은 파티클·연출이 무거워지므로 분리하고, 인트로는 가볍게 유지한다.

### 씬 구성
| 씬 | 빌드 인덱스 | 내용 |
|---|---|---|
| `Assets/JackpotRun/Scenes/Intro.unity` | 0 | UICamera · IntroCanvas · **LoginView** · MenuView · PickView · DexView · FxLayer · Toast |
| `Assets/JackpotRun/Scenes/Play.unity` | 1 | UICamera · PlayCanvas · RunView(HUD/Reel/Notes/Controls) · OverlayLayer(패널 7종) · FxLayer · Toast |
- 기존 `JackpotRun.unity`와 `Scenes/SampleScene.unity`는 삭제(빌드 목록도 두 씬만).

### 영속 계층 (씬을 넘나드는 것)
- **`AppRoot`가 DontDestroyOnLoad 싱글턴이 된다.** 씬 뷰 참조를 갖지 않는다(직렬화 참조 금지 — 씬 전환 시 dangling).
  보유: `Profile`(PlayerProfile) · `ProfileStore` 저장 · `Session`(GameSession) · `PendingLaunch{charKey,macKey,devKey}` ·
  씬 전환 API · **전환 페이드용 캔버스**(sortingOrder 500, DontDestroyOnLoad, 0.2s 암전→복귀).
- 생성: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 없으면 만든다(어느 씬에서 Play를 눌러도 동작).
- 각 씬은 `IntroSceneRoot` / `PlaySceneRoot` MonoBehaviour가 자기 씬의 뷰를 [SerializeField]로 들고,
  `Awake`에서 `AppRoot.Instance`에 자기를 등록한다(역방향 참조만 — AppRoot는 인터페이스로만 안다).

### 전환 흐름
```
Intro(Login?) → Menu → Pick → [시작] → AppRoot.StartRun(c,m,d)
   = PendingLaunch 저장 → 페이드아웃 → LoadScene("Play")
Play: PlaySceneRoot.Awake → AppRoot.ConsumePendingLaunch() → GameSession 생성 → RunView.Bind(session) → 페이드인
런 종료(GAME_OVER→[메뉴로]) → AppRoot.EndRun() = 프로필 저장 → 페이드아웃 → LoadScene("Intro") → Menu 표시
```
- Play 씬을 직접 열고 Play를 눌렀을 때(에디터 개발 편의): PendingLaunch가 없으면 기본 조합(novice/basic/"")으로 시작하고 경고 로그.

### LoginView (신규, 인증 백엔드는 후속)
- 닉네임 입력(legacy `InputField`, 2~12자) + [시작하기] + [게스트로 시작].
- 저장은 `PlayerPrefs("jackpotrun_nick")` — **엔진(PlayerProfile) 건드리지 않는다.**
- 이미 닉네임이 있으면 Intro 진입 시 Login을 건너뛰고 Menu로. Menu에 "@닉네임" 표기 + [닉네임 변경] 소형 버튼.
- Firebase 연동 시 이 화면이 실제 로그인으로 교체된다(주석으로 명시).

### SceneBuilder 개편
- `UiSceneBuilder`: `BuildAll()`(메뉴 `JackpotRun/Build UI Scenes`) → `BuildIntroScene()` + `BuildPlayScene()`,
  각각 개별 메뉴 항목도 제공. 비대화형 `BuildAllUnattended()` 유지(MCP용).
- 공통 골격(카메라·캔버스·FxLayer·Toast) 생성은 헬퍼로 공유. 빌드 세팅은 Intro(0)·Play(1) 두 개로 설정.
- S7c의 카메라/ScreenSpaceCamera 전환 규칙은 두 씬 모두에 적용.

## S10 — Intro 화면을 웹 페이지와 동일한 룩으로 (2026-07-31 확정)

기준 원본: `public/jackpotpick/pick.css`(220줄) + `public/jackpotpick/index.html` + `app.js` 렌더 구조.
**웹 화면을 그대로 옮긴다** — 색·간격·폰트 크기·구성요소 배치를 CSS 값 그대로 매핑.

### 팔레트 (pick.css `:root` 그대로 — UiKit 상수 교체)
`bg0 #0b0d15` · `bg1 #11131f` · `panel #171a27` · `panel2 #1c2030` · `bd #2a3048` · `bd2 #394365`
`txt #e9ebf5` · `dim #8b93a7` · `dim2 #69718a` · `gold #ffd23f` · `amber #f59e0b` · `pink #ff7adb`
`teal #34d3c0` · `blue #5b8cff` · `purple #a974ff` · `red #ff6b6b` · `green #4ade80`

### 카드 구조 — **현재 Unity(대형 아트 상단)를 웹 구조로 교체**
웹 `.jcard`: 좌측 4px 난이도색 스트라이프 · 배경 세로 그라데이션(panel2→panel) · 테두리 2px `bd` · 라운드 15
- `.jc-top`(가로): **아이콘 52×52 라운드 11**(좌) + 이름 15.5px 800 / 역할 11.5px dim(우)
- `.badge b-diff`: 이름 옆 인라인, 난이도색 배경 + 어두운 글자, 10.5px 800, 라운드 7
- `.jc-eff`: 효과 박스 — 배경 rgba(255,255,255,.035), 테두리 bd, 라운드 9, 패딩 7/9, 12.5px `#cdd3e6`
- `.jc-tags`: 태그 칩 10.5px 700 라운드 6 — hot(빨강)/good(민트)/high(핑크)/기본(회색) 4종 배색 CSS 그대로
- `.jc-pc`: 장점 `＋`(green) 2줄 · 주의 `－`(#ff9b9b) 1줄, 11.7px
- `.jc-foot`: "추천 빌드: **X**"(gold), 11px
- 선택: 테두리 gold + 배경 밝게 + `.jc-check` 우상단 골드 pill "선택됨 ✓"
- 잠금: 불투명도 .62 + 채도 감소 + `.jc-lock` 우상단 "🔒 잠김"(BMP 대체: "잠김") + `.jc-unlock` 점선 골드 박스
- 그리드: 최소 228px 자동 채움 → 1080 세로 기준 **2열**, 간격 10

### 상단 구성 (index.html 순서 그대로)
1. `.head`: 타이틀(굵게, `.sub`만 골드) + `.lead` 설명 13.5px dim + `.who` "@닉 — …" 골드 14px
2. `.tabs`: 3탭 — 각 탭에 `.tnum`(11px dim2, 완료 시 "✓" green) + 제목 13.5px 800 + `.tpick`(11px gold, 선택값). active = 보라 그라데이션 배경 + amber 테두리
3. `.recos`: pill 버튼 4개(라운드 999, 12.5px 800) — beginner=teal 테두리, high=pink, challenge=red, random=기본
4. `.toolbar`: `.chips`(pill 12px, on=골드 배경/어두운 글자) + `.sortrow`(라벨 + 드롭다운 느낌 버튼 4개)
5. `.sechead`: "🎭 캐릭터" 대신 텍스트 16px + `.cnt` "해금 n/m" 12px dim
6. 카드 그리드
7. `.summary` 하단 고정 시트: 상단 구분선 bd2, 배경 그라데이션 + 그림자
   - `.sum-combo`: 조합 13.5px 800 + `.bl` 빌드토큰 11.5px gold
   - `.sum-grade`: 등급 배지(현재색 테두리 + 같은 색 글자) 12px 900
   - `.sd-meters`: 3칸 그리드 — 각 칸 panel 배경/bd 테두리/라운드 11, `.mk` 11px dim + `.mv` 별 14px(고점·안정 gold, 난이도 red)
   - `.sd-blurb`: 골드 톤 박스(배경 rgba(255,210,63,.06), 테두리 #f59e0b33, 라운드 11) 13px
   - `.sd-cols`: 장점/주의 2열, 각 항목 앞에 ▲(green)/▼(#ff9b9b)
   - `.sd-builds`: 빌드 토큰 칩(골드 글자 + 골드 6% 배경)
   - `.go`: 전체 폭 골드 그라데이션 버튼 14.5px 900, 비활성 시 불투명도 .42

### MenuView / DexView
- Menu도 같은 팔레트·타이포로 통일(현재 큰 캐러셀은 유지하되 카드/버튼 스타일을 웹 톤으로).
- Dex는 `public/jackpotdex/style.css` 기준: `.card` 가로 배치(아이콘 좌 + 이름/설명 우), 그리드 3열, `.mini` 진행바, 잠금 카드 `❓ ???` 마스킹.

### 구현 규칙
- 새 스프라이트가 필요하면 `UiSpriteGen`에 추가(라운드 반경별 9-slice: r7/r9/r11/r13/r15/r999). **재생성 시 overwrite:true 주의**(S9 교훈).
- 아이콘 52px는 catalog 스프라이트 그대로(현재 대형 아트를 축소해 사용).
- 폰트 크기는 CSS px를 1080 기준 그대로 쓰되, 가독성 위해 **×1.6 스케일**(웹은 360~420px 폭 기준, Unity 캔버스는 1080) — 예: 15.5px → 25pt, 12.5px → 20pt, 11px → 18pt.
- 클릭 차단 재발 방지: 투명 컨테이너 패널은 반드시 `Image.raycastTarget=false`.

## S15 — 글로벌 랭킹 (Firebase RTDB, 앱+웹) (2026-08-03, Fable 설계)

**배경**: Firebase 콘솔의 프로젝트 **표시명이 "JackpotRun"으로 변경**됐다(사용자 통보). 표시명
변경은 프로젝트 ID·URL·키에 영향이 없으므로 **ID는 `jackpotrun-web` 그대로**다(2026-08-03 실측:
`jackpotrun-web-default-rtdb...` RTDB·호스팅 정상 응답, `jackpotrun` ID의 RTDB/호스팅은 전 리전
404 — 별도 신규 프로젝트는 존재하지 않음). 이 슬라이스는 그 프로젝트의 RTDB에 **앱·웹 공용
글로벌 랭킹**을 신설한다. 카톡 봇의 `jackpotdex/<token>.rank`(봇 자체 리더보드)와는 별개 보드다.

### DB 계약 — `jackpotrank/$pid`

- `$pid`: 기기(설치)별 GUID 32자(`Guid.NewGuid().ToString("N")`) — PlayerPrefs `"jackpotrun_pid"`.
- 값: `{ "nick": string(1~12자), "score": long, "stage": long, "ts": long(unix ms) }`
  - `score` = 프로필 `bestScore`, `stage` = 프로필 `bestStage` — **서로 다른 런의 개인 최고치일 수
    있다**(이 보드는 "개인 최고 기록" 게시판, 단일 런 스냅샷이 아님). `ts` = 마지막 갱신 시각.
- `database.rules.json`에 노드 추가: `.read: true` · `.indexOn: ["score"]` · `$pid`에 `.write: true`
  \+ `.validate`($pid 길이 8~40, 필수 자식 nick/score/stage/ts, nick 문자열 1~12자, score/stage/ts
  숫자, `$other: .validate false`). 공개 쓰기는 기존 노드들과 같은 수준 — CLAUDE.md의 미해결 보안
  이슈 범위에 포함되며 이 슬라이스에서 조이지 않는다.
- **규칙이 배포되기 전에는 read/write 모두 거부**(RTDB 기본 거부) — 앱 랭킹 화면이 오류 상태를
  보여주는 것이 정상. 배포는 사용자: `firebase deploy --only hosting,database --project jackpotrun-web`
  (이 PC엔 firebase CLI/node 없음).

### Unity — 파일별 작업

1. **`Scripts/Game/MiniJson.cs` (신규)** — 순수 C#(UnityEngine 비의존) 재귀하강 JSON **파서**(쓰기
   없음). `public static object Parse(string)` — object→`Dictionary<string, object>`, array→
   `List<object>`, string(이스케이프 `\" \\ \/ \b \f \n \r \t \uXXXX`), number→`double`,
   `true/false/null`. 형식 오류 시 예외 대신 **null 반환**. RankingService 전용 최소 구현.
2. **`Scripts/Game/RankingService.cs` (신규)** — `static class`, `UnityEngine.Networking` 사용.
   - `const string DbUrl = "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app"`
     (주석: 콘솔 표시명 "JackpotRun" = 프로젝트 ID `jackpotrun-web`. **프로젝트를 옮기면 이 상수
     하나만 교체**) · `const string Node = "jackpotrank"`.
   - `public static string PlayerId()` — PlayerPrefs `"jackpotrun_pid"`, 없으면 GUID "N" 생성·저장.
   - `[Serializable]` DTO(nick/score/stage/ts) → `JsonUtility.ToJson`으로 PUT 바디.
   - `public sealed class Entry { string pid; string nick; long score; long stage; long ts; }`
   - `public static void TrySubmitBest(MonoBehaviour host)` — nick=`LoginView.SavedNick()`,
     profile=`AppRoot.Instance?.Profile`. **스킵**: host/profile null · nick 빈 문자열 ·
     `BestScore <= 0`. PlayerPrefs `"jackpotrun_rank_sent_score"`(string으로 저장한 long)·
     `"jackpotrun_rank_sent_nick"`과 비교해 **BestScore가 더 크거나 nick이 달라졌을 때만**
     `UnityWebRequest.Put($"{DbUrl}/{Node}/{pid}.json", json)` 코루틴(host에서 Start, timeout 10s,
     Content-Type application/json). 성공(result Success) 시 prefs 갱신, 실패 시 `Debug.Log`만
     (prefs를 안 건드리므로 다음 트리거에서 자동 재시도).
   - `public static void Fetch(MonoBehaviour host, Action<List<Entry>> onOk, Action<string> onError)`
     — `GET {DbUrl}/{Node}.json`(timeout 10s). 본문 `"null"` → 빈 리스트로 onOk. MiniJson 파싱 —
     루트가 Dictionary가 아니면 onError, 항목별로 형이 안 맞으면 **그 항목만 건너뜀**. 정렬:
     score 내림차순, 동점 ts 오름차순(먼저 세운 쪽 위). HTTP/네트워크 오류 → onError(사유 문자열).
3. **`ScreenRouter.cs`** — `ScreenId`에 `Rank` 추가(맨 끝).
4. **`AppRoot.cs`** — `public void ShowRank()`(기존 ShowX와 동일 패턴). `RegisterIntro` 끝에
   `RankingService.TrySubmitBest(this)` 호출 — host가 DontDestroyOnLoad인 AppRoot라 씬 전환에
   코루틴이 안 끊긴다. EndRun → Intro 복귀 시 RegisterIntro가 다시 불리므로 **게임오버 직후의
   신기록도 이 훅 하나로 업로드**되고, 앱 재시작 시(오프라인이었던 기록) 재시도도 겸한다.
5. **`IntroSceneRoot.cs`** — `[SerializeField] RankView rankView` + `public RankView Rank` 프로퍼티
   (기존 뷰 5종과 동일 형식).
6. **`MenuView.cs`** — `OnRankClicked()`를 `appRoot?.ShowRank()`로 교체(토스트 제거), 헤더 주석의
   "랭킹 화면 없음" 문구 갱신.
7. **`Scripts/UI2/RankView.cs` (신규)** — 필드: `Text statusText` · `RectTransform listContent` ·
   `RectTransform rowTemplate`(자식 경로 계약: `"Content/RankNo"`·`"Content/Nick"`·`"Content/Score"`
   각 Text, 루트에 행 배경 Image — UiKit.HGroup이 만드는 중간 GameObject를 "Content"로 개명해 찾는다.
   Transform.Find는 직계 자식만 찾으므로, BuildDexCardTemplate "Content" 계약과 동일. **Opus 1차
   검수 치명-1 반영**: 초안의 "직계 자식" 표기는 8번의 HGroup 구조와 모순이었다). `Awake`:
   rowTemplate 비활성. `OnEnable`: 기존 행 제거(rowTemplate 제외 — DexView.RenderGrid 패턴) →
   statusText "랭킹 불러오는 중..." → `RankingService.Fetch(this, ...)`.
   onOk 0건 → "아직 등록된 기록이 없어요\n첫 기록의 주인공이 되어보세요!"; 있으면 statusText 숨기고
   **상위 100행** 생성 — RankNo: **항상 숫자**, 1~3위는 금/은/동 색 강조(`UiKit.Gold` /
   `#C7CFDE` / `#D08A4E`. 초안의 메달 `🥇🥈🥉`는 astral 이모지라 레거시 Text가 렌더링하지 못해
   — S8 항목⑤ 실측 — 색상 숫자로 대체, **Opus 검수 중요-3 반영**) / Nick: nick / Score:
   `NumberFormat.Comma(score) + " · S" + stage`. **내 행**(pid==PlayerId()): 배경 `UiKit.CardTop` +
   Nick 색 `UiKit.Gold`. onError → statusText
   "랭킹을 불러오지 못했어요\n네트워크 확인 후 다시 열어주세요". 화면이 꺼지면 host(this) 코루틴이
   함께 멎으므로 콜백 누수 없음 — 콜백 첫 줄에서 파괴/비활성 가드.
   추가(Fable 최종 검수): `AppRoot.CompleteLogin`에도 `TrySubmitBest` 훅 — 닉네임 변경 직후 보드
   반영(내부 "닉이 달라졌나" 판정이 실제 PUT 여부 결정).
8. **`Editor/UiSceneBuilder.cs`** — `BuildRankScreen(canvasRoot)`: **BuildDexScreen 컨벤션 그대로**
   (UiKit.Panel/Fill/VGroup/HGroup/SizeHint/Scroll, `panel_r24`). 헤더 90("잭팟런 랭킹" H1 —
   초안의 "🏆"는 astral 미렌더로 제거(중요-3) —
   "← 메뉴" 백버튼 160×70 `#2A3048` — `AddNavButton(backButton, NavButton.Target.Menu)`) →
   statusText(TextSecondary, MiddleCenter, preferredHeight 64, flexibleHeight 0) → 세로
   Scroll(flexibleHeight 1): content에 VerticalLayoutGroup(spacing 10, padding 20/20/12/20,
   childControlWidth/Height true, childForceExpandWidth true·Height false) + ContentSizeFitter
   (vertical Preferred) + rowTemplate. **rowTemplate**: UiKit.Panel(`UiKit.PanelBg`, `rrect_r11`)
   preferredHeight 84, 내부 HGroup(spacing 12, padding 18/18/12/12): RankNo(28pt, 폭 88,
   MiddleCenter) · Nick(24pt bold, flexibleWidth 1, MiddleLeft) · Score(22pt, 폭 320, MiddleRight,
   TextSecondary). RankView 와이어링은 기존 화면들과 같은 SerializedObject·FindProperty 방식.
   `BuildIntroScene`에 화면 등록: screens 배열 `(ScreenId.Rank, root, group)` + IntroSceneRoot
   `rankView` 와이어링. **구현 후 씬 리빌드 필수**(메뉴 JackpotRun/Build Intro Scene).

### 웹 — 파일별 작업

1. **`public/ranking/index.html` (신규)** — jackpotdex/index.html의 뼈대·메타(viewport, lang=ko,
   다크 배경) 축소판. 타이틀 "잭팟런 랭킹". 헤더(🏆 잭팟런 랭킹 + 부제 "글로벌 최고 기록") +
   `#list` 컨테이너 + 상태 문구 영역.
2. **`public/ranking/app.js` (신규)** — firebase-app/database **10.12.2 모듈**(jackpotdex와 동일),
   **동일 firebaseConfig**(주석: 표시명 "JackpotRun" = ID `jackpotrun-web`). `get(ref(db,
   "jackpotrank"))` 1회 → 값 배열화 → score 내림차순·ts 오름차순 → **상위 100** 렌더. 행: 순위
   (1~3위 🥇🥈🥉) · 닉네임 · `점수 comma` · `S스테이지` · 날짜(`YYYY.MM.DD`, ts 기준). 0건 →
   "아직 등록된 기록이 없어요", 오류 → "랭킹을 불러오지 못했어요". **닉네임은 반드시 이스케이프**
   (jackpotdex `_hesc` 패턴) — XSS 방지.
3. **`public/ranking/style.css` (신규)** — jackpotdex/style.css의 팔레트·`.rankrow` 계열에서 필요한
   최소만 복제(전체 복사 금지).
4. **`firebase.json`** — 기존 두 블록과 동일한 `/ranking/**` no-cache 헤더 블록 추가.
5. **`database.rules.json`** — 위 DB 계약의 `jackpotrank` 규칙 추가.

### 검증·주의

- 엔진(`Engine/`) 무변경 — EngineTests 영향 없음. 새 코드는 전부 Unity 어댑터/에디터/웹.
- csc 스모크(오프라인 컴파일) + 에디터 리프레시 후 Editor.log CS 오류 grep + Intro 씬 리빌드 +
  플레이 스모크(메뉴 → 랭킹 → 백버튼). 규칙 미배포 상태에서는 오류 상태 문구가 뜨는 게 정상.
- 표기 규칙 준수: 점수는 정수 comma(`NumberFormat.Comma` / 웹 `toLocaleString`).
- 웹 스타일·구조는 기존 파일에서 복제하되 **jackpotpick/jackpotdex는 수정하지 않는다**.

## S14 — 연출 강화 (2026-08-01, Fable 설계)

S13 구조(스트립 릴 + FxKit) 위에 **체감 연출**을 얹는다. 게임 로직·확률은 불변(뷰 계층 전용).

### A. 릴 — 이웃 칸 상시 노출 (S13 보고 1번 결정)
- **셀 뷰포트 196 유지, 슬롯 높이 130으로 축소** → 중앙 1칸 + 위/아래 각 33px씩 보인다.
- 중앙 슬롯: alpha 1.0 · scale 1.0 · 심볼 95px. 이웃: **alpha 0.42 · scale 0.88** + 살짝 어두운 오버레이.
- 셀 상·하단에 **페이드 마스크**(위/아래 28px, 배경색→투명 그라데이션 스프라이트 `w_reel_fade`)를 덮어
  잘린 이웃이 자연스럽게 사라지게 한다.

### B. 스핀 체감
- **차징**: 스핀 버튼 누름 → 버튼 0.08s 스쿼시(scale 0.94) + 릴 3~5칸이 **위로 12px 반동**(0.12s, OutBack) 후 낙하 시작.
- **모션 스트릭**: 최고속 구간에서 각 셀에 세로 블러 스트릭 오버레이(`w_streak`, alpha 0.35, 아래로 흐름)와
  심볼 alpha 0.75·scale 1.04.
- **기대감(anticipation)**: 정지 순서상 **남은 릴이 1개이고 이미 2개 이상 같은 심볼**이면 그 릴만
  유지 구간을 +0.6s 연장하고 감속을 3→5노치로 늘린다 + 셀 테두리 골드 펄스 + 화면 살짝 줌인(1.0→1.02).
- **정지 임팩트**: 각 릴 착지 시 셀 0.06s 스쿼시(scaleY 0.92→1) + 바닥 먼지 파티클(`fx_reel_land`, 6개) + 미세 셰이크(2px).

### C. 결과 연출 (매치 단계별 차등)
| 단계 | 연출 |
|---|---|
| 2매치 | 해당 셀 골드 테두리 + 광선 스윕 1회 + 스파클 4 |
| 3매치 | + 화면 플래시 4% · 셰이크 3px · `fx_set_hit` 링 |
| 4매치 | + 플래시 8% · 셰이크 6px · 셀에서 중앙으로 에너지 수렴 파티클 |
| 5매치(잭팟) | **슬로모 0.25s**(Time.timeScale 0.35 → 1.0 복귀) + 전체 컨페티 + "JACKPOT!" 배너 드롭+스케일 펄스 + 골드 방사 광선 8줄 회전 |
- **점수 팝업**: 획득 EXP/점수를 릴 위에서 **카운트업**(0.35s, OutCubic) + 위로 60px 떠오르며 페이드.
- **코인 획득**: 코인 파티클이 HUD 코인 라벨로 날아가 도착 시 라벨 0.15s 펄스(scale 1.15).

### D. HUD 피드백
- EXP 바: 채움 트윈(0.5s OutCubic) + **선두에 흐르는 광점**, 목표 근접(≥80%) 시 골드 펄스, 초과 달성 시 초록 전환.
- 남은 스핀 1회 이하 → 스핀 카운터 적색 점멸(0.8s 주기).
- 저주 증가 시 HUD 테두리 보라 플래시, 보스 진입 시 적색 비네트 펄스 + 배너 드롭 + 0.4s 셰이크.

### E. 화면 전환 · 패널
- 스테이지 클리어: 상단에서 등급 배너 낙하(OutBack) → 점수 카운트업 → 별 낙하 파티클 → 1.0s 후 노드 패널
  **아래에서 슬라이드업 + 배경 딤 페이드**.
- 퍽/상점 카드: 0.08s 스태거 팝인 + 카드 hover 시 미세 부양(-4px)과 그림자 강화.
- 게임오버: 화면 채도 0.5로 0.4s 페이드 + 재 낙하 파티클 + 패널 스케일인 + 신규 업적 0.05s 스태거.

### F. 신규 에셋
스프라이트: `w_reel_fade`(상하 페이드 마스크) · `w_streak`(세로 모션 스트릭) · `w_ray`(방사 광선 1줄, 잭팟용).
파티클: `fx_reel_land`(착지 먼지) · `fx_converge`(4매치 수렴) · `fx_jackpot_rays`(회전 광선).

### G. 규칙
- **Time.timeScale 조작은 잭팟 연출 1곳만**, 반드시 `try/finally`로 1.0 복구(코루틴 중단 대비).
- 모든 셰이크는 캔버스 루트가 아니라 **RunScreen 루트 RectTransform**에만 적용(HUD/버튼 입력 방해 금지).
- 파티클 총량 상한 유지, FxLayer `blocksRaycasts=false` 유지.
- 연출로 인해 **입력이 막히는 구간은 최대 0.6s**를 넘지 않는다(잭팟 슬로모 포함).

## S13 — 릴 스트립 스핀 · UI 발광 파티클 · 늘어남/겹침 정리 (2026-08-01, Fable 설계)

사용자 지적 4건을 한 슬라이스로 처리한다.

### A. 9-slice 늘어남 정리 (실측 7건)
증상: `chip_r999`(border 128)를 작은 요소에 쓰면 경계가 대상보다 커서 **동그라미가 늘어난 타원**이 된다.
실측 위반: `Pip_0~4`(24×28) · `Toast`(800×84) · `PriceButton`(100×100).
- **규칙**: 9-slice 스프라이트의 `border 합 ≤ 대상 변 길이`. 위반 시 더 작은 반경 스프라이트를 쓴다.
- `UiKit.PillSprite(float targetHeight)` 헬퍼 신설 — 높이에 따라 `w_r9/w_r12/w_r16/w_r18/w_r22/w_pill_btn` 중
  `border*2 ≤ height`인 가장 큰 반경을 반환. **pill이 필요한 모든 곳은 이 헬퍼를 거친다.**
- 기존 `chip_r999` 사용처 전수 교체(칩·배지·토스트·가격 버튼·불운 pip·빌드칩 등).
- 검증: 플레이 중 스크립트로 `border 합 > rect` 위반 0건 확인.

### B. 이미지 비율 늘어남
- 카탈로그 아트(정사각 256)는 **`Image.preserveAspect = true`** 고정. 슬롯이 비정사각이면 여백을 남긴다.
- 아이콘 슬롯은 정사각으로 만들고(`AspectRatioFitter` 또는 고정 sizeDelta), 부모 레이아웃이
  `childForceExpandHeight`로 늘리지 않게 한다(S10에서 아이콘이 83→203으로 늘어난 사례 재발 방지).

### C. 겹침
- 레이아웃 그룹 기준 겹침은 현재 0건. 남은 실사례는 **Pick 카드의 `Role` 텍스트가 `Eff` 박스와 겹치는 것**
  (Info 세로 그룹 높이 83에 NameRow 34+Role 24가 중앙 정렬되며 아래로 밀림) — Info 높이를 실제 내용
  합(NameRow+spacing+Role)으로 맞추고 Top 행 높이를 그에 맞춰 재계산.
- 회귀 방지: 빌더 마지막에 `#if UNITY_EDITOR` 자가 점검(레이아웃 그룹 자식 rect 겹침 발견 시 경고 로그).

### D. 릴 스핀 연출 재설계 (핵심)
현재: 심볼이 제자리에서 무작위 교체되다 마지막에 툭 바뀜 → **부자연스럽고 결과가 갑자기 튄다**.
목표: **세로 스트립이 돌아가며 위/아래 칸이 보이고, 한 칸씩 넘어가다 감속·정지. 아깝게 빗나간 느낌 연출.**

구조(빌더):
```
Reel_i (셀, 정사각, RectMask2D + 배경 w_reel + 테두리)
└─ Strip (세로, 자식 5칸: 위2 / 중앙 / 아래2, 각 칸 높이 = 셀 높이)
     └─ Slot_k: 심볼 Image(preserveAspect) + 태그 텍스트
```
- **평소**: 중앙 슬롯만 결과 심볼, 위/아래 슬롯은 이웃 심볼(살짝 어둡게 alpha 0.45, 스케일 0.92)로
  "릴의 위아래가 보이는" 느낌.
- **스핀 시작**: 스트립이 아래로 흐르고, 한 칸(cellH)을 지날 때마다 **맨 위 슬롯을 재활용해 새 무작위 심볼**을
  채운다(무한 스크롤). 속도: 0→최고속(0.06s/칸) 0.25s 가속 → 유지.
- **정지**: 릴별로 왼쪽부터 0.10s 스태거. 정지 시퀀스 = ① 목표 심볼을 중앙 도착 예정 슬롯에 심고
  ② 남은 3칸을 **한 칸씩 감속**(0.10 → 0.16 → 0.24s, OutCubic) ③ 마지막 칸 도착 시 살짝 오버슈트
  (Y +8px 후 OutBack 복귀) + `fx_spin_stop` 스파크.
- **니어미스(아깝게 빗나감)**: 마지막 릴 정지 직전, 결과가 세트를 완성하지 못하는데 **직전/직후 이웃 심볼이
  세트를 완성했을 경우** → 그 "완성 심볼"이 중앙을 **통과했다가** 한 칸 더 밀려 실제 결과에 멈춘다.
  통과 순간 0.12s 골드 플래시 + 살짝 느려짐(0.35s), 정지 후 셀이 0.3s간 회색 톤으로 페이드(아쉬움).
  판정은 **뷰 계층에서만** 수행(엔진 결과 불변) — `cells` 배열에서 최다 심볼 개수와 마지막 셀 이웃 비교.
- 매치 시 기존 연출(골드 테두리·글로우·광선 스윕) 유지.

### E. UI 발광 파티클 (FxKit 확장)
신규 프리팹 4종(`FxPrefabGen`, 새 파일명):
| id | 용도 | 사양 |
|---|---|---|
| `fx_ui_aura` | 버튼/카드 뒤 은은한 발광 | 루프, 6개/초, 크기 40~90, 알파 .12, 매우 느린 상승, 가산합성 |
| `fx_title_spark` | 타이틀 릴 주변 반짝임 | 루프, 4개/초, 별 텍스처, 크기 8~18, 알파 .5, 위로 천천히 |
| `fx_btn_press` | 버튼 누를 때 | 버스트 10, 0.35s, 방사, 골드 |
| `fx_card_pick` | 카드 선택 시 | 버스트 18, 0.5s, 티어색, 링 확산 |
훅: TitleView(릴 주변 `fx_title_spark` 루프 + 시작 버튼 뒤 `fx_ui_aura`), MenuView(주 버튼 뒤 aura),
PickView(카드 선택 시 `fx_card_pick`), 공통 `PressFx`에 `fx_btn_press`(골드 버튼만).
**성능**: 동시 파티클 상한 유지, FxLayer는 `blocksRaycasts=false`.

## S12 — 웹 단독판 UI 전면 이식 (2026-08-01, Fable 설계)

**시각 기준 원본 = `Docs/WebRef/slot/`** (style.css 1310줄 · ui.js 2102줄). S11(인트로만)을 흡수·확장한다.
목표: 웹 단독판을 화면 단위로 최대한 동일하게 재현. 기존 S7~S10의 UI2 구조(씬 빌더 + 뷰 컴포넌트)는 유지하고
**토큰·스프라이트·레이아웃 수치·애니메이션을 원본 CSS 값으로 교체**한다.

### 0. 공통 토큰 (style.css :root — 전 화면 공통, UiKit에 상수화)
| 토큰 | 값 | 토큰 | 값 |
|---|---|---|---|
| bg0 | #07080f | txt | #eef1fb |
| bg1 | #0e1020 | txt2 | #c3cae3 |
| bg2 | #141833 | dim | #8b93b5 |
| panel | #161a2c | dim2 | #6a7299 |
| panel2 | #1c2238 | gold | #ffd23f |
| panel3 | #252c46 | gold2 | #ffb300 |
| bd | #2c3454 | amber | #f59e0b |
| bd2 | #3f4a76 | ink | #15131f |
| pink #ff6ec7 · teal #2ee6c8 · blue #5b9bff · purple #b07bff · red #ff5d6c · green #4ade80 · silver #cdd6ea |

라운드: `r-sm 9 · r-md 12 · r-lg 16 · r-xl 18 · r-2xl 22 · r-pill 999`
**스케일: CSS px × 1.9** (웹 `#app` 최대폭 560 ↔ 캔버스 1080). 이후 모든 신규/개편 화면에 적용.
`--gloss` = 상단 하이라이트 `linear-gradient(180deg, rgba(255,255,255,.14), transparent)` — 카드/칩/릴 상단 42~50%에 오버레이.

### 1. 절차 생성 스프라이트 (UiSpriteGen — **전부 새 파일명**, overwrite:false 함정 주의)
| 파일 | 용도 |
|---|---|
| `w_r9/r12/r16/r18/r22.png` | 9-slice 라운드 사각(각 반경, border = 반경) |
| `w_pill.png` | pill(반경 999 → 128 border) |
| `w_gloss.png` | 상단 흰색 14%→투명 세로 그라데이션(오버레이 전용) |
| `w_reel.png` | 릴 셀 배경 165° 그라데이션 #2a3354→#1a2038(48%)→#10162a |
| `w_gold_btn.png` | 골드 버튼 배경 세로 그라데 #ffe680→#f59e0b |
| `w_ghost_btn.png` | 보조 버튼 세로 그라데 panel3→panel2 |
| `w_panel_grad.png` | 패널 세로 그라데 panel2→panel |
| `w_aurora.png` (1080×1920) | 배경 오로라: 방사 4겹(보라 22%/8% .28 · 핑크 82%/4% .22 · 민트 50%/102% .16 · 파랑 50%/40% .10) + 세로 bg1→bg0(60%) |
| `w_vignette.png` | 비네트 `radial(120% 80% at 50% 0%, transparent 55%, rgba(0,0,0,.55))` |
| `w_expfill.png` | EXP 바 채움 가로 그라데 #f59e0b→#ffd23f(70%)→#fff6c0 |

### 2. 화면 매핑 (웹 ui.js ↔ Unity)
| 웹 | Unity | 비고 |
|---|---|---|
| `renderIntro` | **TitleView**(신규) | Intro 씬 첫 화면 |
| `renderLoginGate` | LoginView | 게이트 버튼 스타일 적용 |
| `renderHome` | MenuView | 타이틀+통계 HUD+큰 버튼 3개 |
| `renderSelect` | PickView | 이미 구현(S10) — 토큰만 교체 |
| `renderPlay` | RunView | HUD·릴·gain·logbox·actionbar 재구성 |
| `renderNode/Perk/Shop/StageClear/End` | 기존 패널들 | 시트 스타일 적용 |
| `renderDex` | DexView | 토큰 교체 |

### 3. TitleView (`.intro`, style.css 795-815 / ui.js 436-448)
- 전체 화면 세로 중앙, gap 13(→25), padding 24(→46), 진입 페이드+6px 상승 0.28s
- **릴 타일 3개**: 62×80(→118×152), gap 10(→19), 라운드 14(→27), `w_reel` 배경 + **2px gold 테두리** +
  상단 42% gloss + 글로우(0 0 24px rgba(255,210,63,.4))
  - **110ms마다 심볼 무작위 교체**(ui.js:447) — `sym_*.png` 14종 사용
  - **1.6s 둥실**: Y 0→-7(→-13)→0, 글로우 .35↔.85, delay 0 / 0.2s / 0.4s
- 타이틀 48(→91) w900 letterSpacing -1, 골드 그라데 텍스트 → **#ffdd5c 단색 + 골드 글로우 그림자**로 근사
- sub 13.5(→26) txt2 · best 13(→25) gold w700 · **start 버튼**: pill, padding 15/36(→29/68), 17(→32) w900,
  ink 글자, `w_gold_btn` 배경, 그림자, 진입 1회 `gpop` 바운스(0.5s, scale .4→1.18→1)
- hint 11(→21) dim
- 배경: `w_aurora` + `w_vignette`, 오로라는 16s 주기 scale 1.02↔1.08 + 미세 이동

### 4. MenuView = `renderHome`
- `.scr-title`: h1 27(→51) w900 — "잭팟런" 골드, sub 13(→25) txt2
- `.hud` 카드: `w_panel_grad` + bd 테두리 + r-xl, 안에 칭호(15→29 gold w800) +
  **`.hud-stats` 3칸**(최고 점수 / 최고 스테이지 / 플레이): 각 칸 `rgba(0,0,0,.25)` + bd + r-md + 상단 gloss,
  k 10(→19) dim, v 15(→29) w800 흰색
- 하단 요약 줄: "업적 n/482 · 장치 n/16 해금" 11.5(→22) dim
- **버튼**: `.bigbtn` 전체폭(padding 16→30, 16.5→31 w900, ink, `w_gold_btn`, r-lg) "▶ 게임 시작" +
  `.bigbtn.ghost` 2개(랭킹/도감 — `w_ghost_btn` + bd2 테두리 + txt 글자) 가로 1:1
- 설명 문구 11(→21) dim 2줄

### 5. RunView = `renderPlay`
- **HUD 카드**(`.hud`): `w_panel_grad` + bd + r-xl, padding 12/13(→23/25)
  - `.hud-top`: `STAGE N`(16→30 w900 흰색) + 보스 배지 + 우측 상태 칩(`.hud-build`)
  - **`.expbar-wrap`**: 높이 24(→46), 배경 #0a0c18 + bd + pill, 채움 `w_expfill` + 골드 글로우,
    중앙에 흰 글자(그림자 3겹) "현재/요구" — 채움은 0.5s ease 트윈
  - **`.hud-stats` 3칸**: 스핀 / 점수 / 코인(코인 칸은 gold 테두리 + gold 글자 + 글로우)
  - 불운 게이지 줄
- **릴**(`.reels`): 가로 중앙, gap 8(→15), 셀은 **정사각**(max 94→179), r-xl, `w_reel` 배경 +
  2px bd2 테두리 + 안쪽 그림자 + 상단 42% gloss. 심볼 스프라이트 중앙(50→95).
  - 스핀 중: 심볼 0.14s 주기로 Y -6↔+6 + 알파 .5↔1 + 셀 밝기 1↔1.25
  - 정지: `settle` 0.34s (Y -14 scale .7 → +2 scale 1.06 → 0/1) — **왼쪽부터 스태거**
  - 매치: 골드 테두리 + 안팎 글로우 + 1.6s 1회 펄스(scale 1.02) + **광선 스윕**(2.2s 1회) +
    반짝 입자 몇 개. 매치 수(2~5)에 따라 강도·속도 상향(m-2~m-5 표 그대로)
- **`.gain`**: 최소 높이 128(→243), 중앙 정렬 — 큰 획득 텍스트 24(→46) w900 gold + `gpop` 등장,
  `.notes` 칩들(11→21, rgba(255,255,255,.06) + bd + r8)
- **`.logbox`**: 최근 8줄, 11.5(→22) txt2, 배경 rgba(0,0,0,.22) + bd + r12, 최대높이 86(→163) 스크롤
- **`.actionbar`** 하단 고정: 배경 그라데 + 상단 bd2 선 + 그림자(blur는 생략),
  `.ab-extra`(특수 명령 버튼들, `.abtn` r11) + `.ab-main`(**`.spinbtn`** 전체폭 padding 17(→32),
  17.5(→33) w900 ink, `w_gold_btn`, **3s 주기 광선 스윕**, 눌림 시 Y+3 scale .99 / +
  `.iconbtn` 62(→118) 폭 세로형(아이콘+라벨 10→19), 아이템 개수 뱃지(red pill))

### 6. 시트/패널 (`.sheet`)
하단에서 슬라이드업 0.24s, `w_panel_grad` + bd2 + 상단만 r-2xl, 배경 딤 rgba(0,0,0,.62),
최대 높이 82% 스크롤. Node/Perk/Shop/StageClear/End 패널을 이 스타일로 통일.

### 7. uGUI 재해석 규칙 (원본 CSS 기능 → Unity)
| CSS | Unity |
|---|---|
| `linear-gradient` 배경 | 절차 생성 스프라이트(위 §1) |
| 그라데이션 **텍스트** | 단색 근사 + Shadow/Outline (uGUI 불가) |
| `box-shadow` 외부 글로우 | 글로우 스프라이트(반투명 확산) 뒤에 깔기 or Outline |
| `inset` 그림자 | `w_reel`/`w_*` 텍스처에 직접 구움 |
| `backdrop-filter: blur` | 생략(불투명 패널) |
| `filter: saturate/brightness` | CanvasGroup 알파 또는 색 곱 |
| `aspect-ratio: 1` | AspectRatioFitter |
| `animation` | UiTween 코루틴(이징 대응: cubic-bezier → OutBack/OutCubic 근사) |

### 8. 슬라이스
- **S12a**: 토큰 교체 + 스프라이트 11종 + **TitleView** + **MenuView(=Home)** + 오로라 배경
- **S12b**: **RunView 전면**(HUD/expbar/릴 연출/gain/logbox/actionbar)
- **S12c**: 시트 스타일 통일(Node/Perk/Shop/Clear/End) + PickView·DexView 토큰 정리 + 구 `Scripts/UI` 삭제

각 슬라이스: Sonnet 구현 → Fable MCP 스크린샷 검수 → **즉시 커밋**.

## S11 — 인트로 타이틀 화면 (웹 단독판 그대로) (2026-08-01 확정, S12에 흡수)

⚠️ **시각 기준 원본이 바뀌었다.** 지금까지 참고한 `public/jackpotpick/`은 조합 선택 페이지였고,
사용자가 말한 "인트로 화면"은 **웹 단독판 타이틀 화면**이다 — 스냅샷: `Docs/WebRef/slot/`
(`ui.js` 436-448 마크업 + `style.css` 795-815 `.intro*`). 앞으로 UI 기준은 이 스냅샷이다.

### 팔레트 교체 (`Docs/WebRef/slot/style.css` :root)
`bg0 #07080f` · `bg1 #0e1020` · `bg2 #141833` · `panel #161a2c` · `panel2 #1c2238` · `panel3 #252c46`
`bd #2c3454` · `bd2 #3f4a76` · `txt #eef1fb` · `txt2 #c3cae3` · `dim #8b93b5` · `dim2 #6a7299`
`gold #ffd23f` · `gold2 #ffb300` · `amber #f59e0b` · `pink #ff6ec7` · `teal #2ee6c8` · `blue #5b9bff`
`purple #b07bff` · `red #ff5d6c` · `green #4ade80` · `silver #cdd6ea` · `ink #15131f`

### 스케일 규칙
웹 `#app` 최대폭 560px ↔ Unity 캔버스 1080 → **CSS px × 1.9**. (기존 Pick 화면의 ×1.6은 그대로 두고,
S11 이후 신규/개편 화면은 ×1.9로 통일한다.)

### IntroTitleView (신규 — Intro 씬의 첫 화면, Login보다 앞)
`.intro` 전체 화면 세로 중앙 정렬, gap 13(→25), padding 24(→46), 진입 시 페이드+6px 상승 0.28s.
1. **`.intro-reels`** — 타일 3개, 각 62×80(→**118×152**), gap 10(→19), 라운드 14(→27)
   - 배경 세로 그라데이션 `#2a3354 → #10162a`, **테두리 2px `gold`**, 상단 42% 광택 오버레이
   - 그림자: 안쪽 `0 3px 10px #0008` + 바깥 골드 글로우 `0 0 24px rgba(255,210,63,.4)`
   - **심볼이 110ms마다 무작위 교체**(`ui.js:447` setInterval) — 심볼은 우리 `sym_*.png` 타일 14종 사용
   - **둥실 애니메이션** `introReel 1.6s ease-in-out infinite`: Y 0 → -7(→-13) → 0, 글로우 0.35→0.85 펄스,
     2번째 타일 delay 0.2s, 3번째 0.4s
2. **`.intro-title`** — 48px(→**91**) weight 900, letter-spacing -1, 골드 그라데이션(#ffe87a→#ffb300).
   uGUI는 그라데이션 텍스트 불가 → **`#ffdd5c` 단색 + 골드 글로우 그림자**(0 3px 28px rgba(255,210,63,.45))로 근사.
   좌측에 슬롯머신 아이콘(이모지 렌더 불가 → `UiSpriteGen`에 슬롯머신 도형 스프라이트 추가, 64px)
3. **`.intro-sub`** 13.5px(→26) `txt2`
4. **`.intro-best`** 13px(→25) `gold` 700 — 런 기록이 있으면 "칭호 · 최고 N점 · N런", 없으면 고정 문구
5. **`.intro-start`** — pill 버튼, padding 15/36(→29/68), 17px(→32) weight 900, 글자색 `ink`,
   배경 그라데이션 `#ffe680→amber`, 골드 그림자. 진입 시 1회 bounce(`gpop` 0.5s, overshoot).
   누르면 → 닉네임 없으면 Login, 있으면 Menu.
6. **`.intro-hint`** 11px(→21) `dim`

### 배경 (모든 Intro 화면 공통)
`body::before` 오로라 — 4개 방사 그라데이션(보라 22%/8%, 핑크 82%/4%, 민트 50%/102%, 파랑 50%/40%) +
세로 그라데이션. uGUI로는 **오로라 스프라이트 1장을 절차 생성**(1080×1920, 위 4색 방사 블렌드)해서
화면 뒤에 깔고, 16s 주기로 아주 느리게 스케일 1.02↔1.08 + 미세 이동(`aurora` 애니메이션 근사).
비네트(`body::after`)는 별도 스프라이트 또는 같은 텍스처에 합성.

### 흐름 변경
`Intro 씬: IntroTitle → (닉네임 없으면) Login → Menu → Pick → Play 씬`
ScreenRouter `ScreenId`에 `Title` 추가, 진입 화면은 항상 Title.

## 구현 공통 규칙

- 스펙 문서와 Kotlin이 다르면 **Kotlin이 정답** — 발견 시 보고(스펙 문서 정정은 Fable 몫).
- C# 9 문법까지만. `decimal` 금지(원본은 Double/Long — 동일 타입 사용). 정수 나눗셈·Long 연산은
  01_engine "C# 이식 시 주의" 절 준수.
- 파일 인코딩 UTF-8(BOM 없음). 한글 이름·설명 문자열은 Kotlin 원문 그대로.
- 설계에 없는 파일·구조 추가 금지. 충돌 시 우회하지 말고 보고.

## 계약 확정 이력 (Fable 승인, 2026-07-30)

- `SetEffect` += desc·reqChar·reqMachine·reqDevice (S2b 보고 반영, 클래스 정의는 `Content/ContentTypes.cs`)
- `SymInfo`는 Kotlin `Sym` data class 전체 필드로 확장(가중치·coin·special·rare·tags 등), `Boss` += emoji·desc,
  `Character` += scoreMod·startCoins (S1 보고 반영)
- `PCat`는 설계대로 4종 유지 — `ITEM`은 현재 미사용(Kotlin은 3종)
- `Rng.WeightedPick` = 누적합 스캔·복원추출·매 회 RNG 1소비 (Kotlin §10.1 weighted 일반화)
- fx 효과키: Kotlin Mods 필드명 + 점 표기 오버레이(`perSymbolExp.<sym>` 등), 조건부 신규 증강 16종은
  `cond.<param>` 접두(Perks.cs 헤더 주석이 사전) — S3 Mods/Resolver는 이 사전대로 해석
- `Perks.All`은 `IReadOnlyDictionary<string,Perk>` (콘텐츠 정적 규격의 배열 패턴 예외)
- `TestCtx`에 `Check(bool,string)` 별칭 허용

## S4 백로그 (Opus S1·S2 검수 이관분)

- `INSTANT_CLEAR_ITEMS` 6종 집합 + `isInstantClearItem()` 이식 (SlotV2Engine.kt:1009-1012, 스테이지당 1회 캡)
- `Device.needsArg` 메타 복원 (dev_pin/copy/swap/holdfile — typed action 인자 요구 여부)
- `Formulas.MeetsReq` 死코드 정리 (Unlocks.Meets로 단일화)
- 아이템 73종 선언 순서가 Kotlin과 다름(kind별 재그룹) — 동작 무영향 확인됨, 순서 의존 로직 추가 금지

## 부록: 원본 유지 버그 목록 (이식 시 [원본 버그 유지] 주석 대상)

- 비상졸업벨 강제클리어 시 주 슬롯만 파괴(보조 슬롯 미처리) — 02_service 발견 2
- `hasPrism`이 임시 phasePerks 미반영 — 02_service 발견 4
- `set_align`/`set_perfect_calc` 요구조건 동일·상호배제 없음(이중 가산) — 01_engine 부록 A
- 보스 3종(finals/strict/luck) desc "요구↑"와 달리 quotaMul 1.0 · 보스 개별 규칙 실행 코드 없음 — 01_engine 부록 A
- Mods의 죽은 필드 4종(skullPenaltyMul 등)은 선언만 이식(값 변경 콘텐츠 없음 유지)
