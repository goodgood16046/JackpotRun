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
