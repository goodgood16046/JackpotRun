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
