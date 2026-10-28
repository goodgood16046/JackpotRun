using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Data;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 릴 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/ReelView.cs: "SymbolCell 5~6개 — 스핀 연출 담당".
    // 셀 개수는 장치(➕보조릴 등)에 따라 스핀마다 바뀔 수 있어(SpinResult.cells.Count) 매 스핀 셀을
    // 다시 만든다(이관 원본 Scripts/UI/RunScreen.cs의 RefreshReel/BuildReelCell과 동일한 "이벤트 시에만
    // 리빌드" 원칙). 심볼 스프라이트(sym_<id>)는 런타임 코드에서 접근 불가능한 Editor 전용
    // UiSpriteGen을 참조할 수 없으므로, 빌드 시점에 UiSceneBuilder가 14종 전부를 symbolSprites 배열로
    // 구워 넣는다("빌더가 와이어링" 원칙, MenuView 캐러셀의 AssetDatabase 베이크와 동일 패턴).
    //
    // S13 §D — 릴 스핀 연출 재설계(핵심). 과거(S7c)엔 셀이 제자리에서 0.05s 간격으로 무작위 심볼을
    // 교체하다 마지막에 결과로 툭 바뀌는 "부자연스러운" 연출이었다. 지금은 셀마다 RectMask2D + 세로
    // 5칸 Strip(위2/중앙/아래2)을 두고, 스핀 중엔 스트립이 실제로 아래로 흘러가며 한 칸(cellHeight)을
    // 지날 때마다 맨 위 슬롯을 재활용해 새 무작위 심볼을 채우는 "무한 스크롤 릴"이다(빌더 구조는
    // Editor/UiSceneBuilder.cs BuildReelCellTemplate 참조: Reel_i(RectMask2D+w_reel+테두리 2개
    // Outline) → Strip → Slot0..Slot4, 각 Icon+Tag).
    //
    // S14 §A — 릴 이웃 칸 상시 노출. 셀 뷰포트(Reel_i, RectMask2D)는 196(UiKit.ReelCellSize)을
    // 그대로 두고, Strip 내부 개별 슬롯 높이만 130(UiKit.ReelSlotSize)으로 줄여 위/아래 이웃이
    // 뷰포트 안으로 33px씩 비쳐 보이게 한다(빌더가 슬롯 크기를 짓고, 이 파일은 노치 스크롤 거리로
    // UiKit.ReelSlotSize를 그대로 참조한다 — 두 곳이 어긋나면 이음매가 깨진다).
    //
    // S14 §B/§C — 스핀 체감(차징/최고속 스트릭/기대감/착지 임팩트)과 결과 연출 단계별(2~5매치+잭팟
    // 슬로모)을 얹는다. 게임 로직·확률은 불변 — 전부 뷰 계층 연출이다.
    //
    // ── 노치(1칸 스크롤) 파이프라인 지연에 대한 구현 노트 ─────────────────────────────
    // Strip은 5칸(위2/중앙(index2)/아래2)이 고정 로컬 위치에 머물고, 한 노치마다 Strip 전체가
    // 슬롯 높이만큼 아래로 이동한 뒤 "맨 위(index0)에서 밀려난 것을 맨 아래로 재활용"하는 대신,
    // 반대로 "맨 아래로 밀려난 슬롯의 내용을 새 심볼로 갈아 끼워 맨 위 자리로 재활용"한다(시각적으로
    // 동일 — 값 재사용 방향만 다름). 그 결과 어떤 슬롯에 심볼을 주입하면 그 값은 "주입 후 2노치가
    // 끝날 때" 중앙(index2)에 도착한다(5칸 중 위 2칸을 거쳐야 하므로). 감속 노치 수를 D라 하면,
    // 최종 목표 심볼(Y)은 (D-2)번째(0-based로는 D-3) 노치에서 주입해야 마지막 노치가 끝날 때
    // 중앙에 나타난다 — DecelSequence(...)가 D=3(기본)/D=5(기대감) 양쪽에 이 공식을 그대로 적용한다.
    // 니어미스 후보(X)는 감속 시작 직전(마지막 유지 노치)에 주입해 늘 "감속 2번째 노치가 끝날 때"
    // 중앙을 스쳐 지나간다(D와 무관 — 자기 주입 시점 기준 2노치 뒤이므로).
    //
    // S8 항목⑤(심볼 표시 근본 수정): 레거시 uGUI Text는 astral(서로게이트 페어) 이모지를 렌더링하지
    // 못한다(🍒📘💎🪙👑🔥🧲💣🎲🌱🌀🗝 등 전부 미표시) — 심볼은 UiSpriteGen이 굽는 도형 스프라이트
    // (sym_<id>.png)만으로 표현하고, 이 파일의 "Emoji" 텍스트 오버레이는 완전히 제거했다(셀 자식
    // 경로 계약도 "Icon"(Image)/"Tag"(Text) + Outline으로 축소). Cell.tag(SpinResolver.cs, 엔진 산출
    // 문자열)도 astral 이모지라 TranslateTag로 BMP 안전 기호로 치환해 표시한다(엔진 파일은 수정하지
    // 않는다 — UI 레이어 표시 치환만).
    //
    // S7c/S13/S14 연출 훅: 셀 정지마다 FxId.SpinStop(심볼색)+FxId.ReelLand(착지 먼지), set3/4 성립 시
    // FxId.SetHit, 4매치 FxId.Converge(수렴), 잭팟 시 FxId.Jackpot+FxId.JackpotRays, 해골 칸 FxId.Skull.
    // FxKit.I가 null이면(프리팹 미로드 등) 전부 조용히 무시한다.
    //
    // 재미 개편(FUN_OVERHAUL_DESIGN F1·F4·F5·F7) — ① 홀드: 칸마다 코드 생성 오버레이(청록 틴트+프레임 발광+자물쇠 배지)를 얹고
    // (SetHeld), 스핀 때 홀드된 칸(SpinOutcome.holdUsed)은 스트립을 돌리지 않고 제자리에서 직전 심볼로 고정한다. 칸 탭/롱프레스는
    // CellPressHandler 가 구분한다(탭=RunView 가 홀드 토글 또는 셀 정보, 롱프레스=셀 정보). ② 빅윈: PostRevealFx 끝에 티어별
    // 셰이크·플래시·FxKit 파티클·사운드·진동을 얹는다(배너는 RunView 의 RunFxLayer). ③ 피버: 피버 중 릴 프레임 금빛 발광+불꽃 루프
    // (SetFeverFrame). ④ 기대감 기준 같은 값심볼 2→3개(웹 2026-10-28 동일 변경 — 2개는 스핀 절반에서 떠 긴장감이 흐려졌다).
    //
    // S14 §G — Time.timeScale 조작은 JackpotSlowMoRoutine 1곳뿐(try/finally 복구). 모든 화면 셰이크는
    // reelRow/셀 개별이 아니라 runScreenRoot(RunScreen 루트, RunView가 자기 자신의 RectTransform을
    // 넘긴다)에만 적용한다 — PlayScreenShake가 이전 셰이크를 취소하고 원점(0,0)으로 스냅한 뒤 새로
    // 시작해 여러 셰이크가 겹쳐도 위치가 누적 표류하지 않는다.
    public sealed class ReelView : MonoBehaviour
    {
        // ── S13 §D 스핀 타이밍(설계 명시값 그대로, 미명시 항목은 주석에 "설계 미명시" 표기) ──────────
        private const float AccelDuration = 0.25f;    // 설계 명시: "0→최고속 0.25s 가속"
        private const float MaxSpeedNotch = 0.06f;    // 설계 명시: "최고속(0.06s/칸)"
        private const float AccelStartNotch = 0.16f;  // 설계 미명시 — 가속 시작 시점 노치 길이 기본값
        private const float BaseSpinHold = 0.35f;     // 설계 미명시 — 스태거 시작 전 최소 유지시간 기본값
        private const float StaggerDelay = 0.10f;     // 설계 명시: "왼쪽부터 0.10s 스태거"
        private const float DecelDur1 = 0.10f;        // 설계 명시: "0.10→0.16→0.24s"
        private const float DecelDur2 = 0.16f;
        private const float DecelDur3 = 0.24f;
        private const float OvershootDistance = 8f;   // 설계 명시: "Y +8px"
        private const float OvershootDuration = 0.16f; // 설계 미명시 — OutBack 복귀 길이 기본값

        private static readonly float[] DecelDursNormal = { DecelDur1, DecelDur2, DecelDur3 };

        // ── S14 §B 기대감(anticipation) — "감속 3→5노치" 확장분(설계 미명시 세부값, 뒤로 갈수록
        // 느려지는 형태만 설계 명시 톤을 따랐다). DecelSequence(...)가 D=3/D=5 양쪽에 동일 공식 적용.
        private const float AntDecel1 = 0.10f;
        private const float AntDecel2 = 0.14f;
        private const float AntDecel3 = 0.18f;
        private const float AntDecel4 = 0.22f;
        private const float AntDecel5 = 0.32f;
        private static readonly float[] DecelDursAnticipation = { AntDecel1, AntDecel2, AntDecel3, AntDecel4, AntDecel5 };
        private const float AnticipationHoldExtension = 0.6f; // 설계 명시: "유지 구간을 +0.6s 연장"
        private const float AnticipationZoomScale = 1.02f;    // 설계 명시: "화면 살짝 줌인(1.0→1.02)"
        private const float AnticipationZoomInDuration = 0.2f; // 설계 미명시
        private const float AnticipationZoomOutDuration = 0.25f; // 설계 미명시
        private const float AnticipationGlowHalfCycle = 0.25f; // 설계 미명시 — 골드 펄스 주기 절반

        // ── 니어미스(설계 명시값) ──────────────────────────────────────────────────────────
        private const float NearMissPauseDuration = 0.35f; // 설계 명시: "살짝 느려짐(0.35s)"
        private const float NearMissFlashDuration = 0.12f; // 설계 명시: "0.12s 골드 플래시"
        private const float NearMissGrayFadeDuration = 0.3f; // 설계 명시: "0.3s간 회색 톤으로 페이드"
        private static readonly Color NearMissGray = new Color(0.55f, 0.55f, 0.58f);

        // ── S14 §A 평소(정지) 상태 이웃 슬롯 스타일(설계 명시값 — S13 초안 0.45/0.92에서 갱신) ──────
        private const float RestNeighborAlpha = 0.42f;
        private const float RestNeighborScale = 0.88f;

        // ── S14 §B 차징(스핀 시작 반동) ──────────────────────────────────────────────────
        private const float ChargeUpDistance = 12f;   // 설계 명시: "위로 12px 반동"
        private const float ChargeUpDuration = 0.12f; // 설계 명시: "0.12s, OutBack"
        private const float ChargeFallDuration = 0.10f; // 설계 미명시 — 낙하 복귀 길이 기본값

        // ── S14 §B 최고속 모션 스트릭 ────────────────────────────────────────────────────
        private const float MaxSpeedSymbolAlpha = 0.75f; // 설계 명시
        private const float MaxSpeedSymbolScale = 1.04f; // 설계 명시
        private const float MaxSpeedStreakAlpha = 0.35f; // 설계 명시

        // ── S14 §B 착지 임팩트 ───────────────────────────────────────────────────────────
        private const float LandSquashDuration = 0.06f; // 설계 명시: "0.06s 스쿼시"
        private const float LandSquashScaleY = 0.92f;   // 설계 명시: "scaleY 0.92→1"
        private const float LandShakeAmplitude = 2f;    // 설계 명시: "미세 셰이크(2px)"
        private const float LandShakeDuration = 0.12f;  // 설계 미명시

        // ── S14 §C 매치 단계별 화면 플래시/셰이크(설계 표 그대로, 지속시간은 미명시 기본값) ─────────
        private const float Set3FlashAlpha = 0.04f;   // 설계 명시: "3매치: 플래시 4%"
        private const float Set3ShakeAmplitude = 3f;  // 설계 명시: "셰이크 3px"
        private const float Set3ShakeDuration = 0.2f; // 설계 미명시
        private const float Set4FlashAlpha = 0.08f;   // 설계 명시: "4매치: 플래시 8%"
        private const float Set4ShakeAmplitude = 6f;  // 설계 명시: "셰이크 6px"
        private const float Set4ShakeDuration = 0.25f; // 설계 미명시

        // ── S14 §C 잭팟 슬로모(§G "1곳만 + try/finally") ────────────────────────────────
        private const float JackpotSlowMoScale = 0.35f;    // 설계 명시: "Time.timeScale 0.35→1.0"
        private const float JackpotSlowMoDuration = 0.25f; // 설계 명시: "0.25s" — 실시간(WaitForSecondsRealtime) 기준

        // ── S16 §B 폭탄 폭발 연출(변형 공개 패스) — 설계 명시값 그대로, 미명시 항목은 기본값 ─────────
        private const float TransformHoldDuration = 0.25f;    // 설계 명시: "폭발 전 잠깐(0.25s) 원본 심볼을 보여주는 대기"
        private const float TransformStaggerDelay = 0.05f;    // 설계 명시: "칸별 스태거 0.05s"
        private const float BombPunchPeakScale = 1.18f;       // 설계 명시: "1→1.18→0"
        private const float BombPunchGrowDuration = 0.08f;    // 설계 미명시 — 총 0.22s 중 성장 구간 분배 기본값
        private const float BombPunchShrinkDuration = 0.14f;  // 설계 미명시 — 나머지(0.22s-0.08s), InBack 수축
        private const float BombSourcePunchScale = 0.94f;     // 설계 명시: "폭탄 칸 자신도 0.94→1 살짝 펀치"
        private const float BombSourcePunchDuration = 0.12f;  // 설계 미명시
        private const float TransformPopHalfDuration = 0.06f; // 설계 명시: "0.12s 스케일 다운→업"의 절반
        private const float BombShakeAmplitude = 6f;          // 설계 명시: "없으면 ±6px"(S14 PlayScreenShake 재사용)
        private const float BombShakeDuration = 0.15f;        // 설계 명시: "0.15s"

        // ── 매치/잭팟/해골 FX(S7c 이관, 그대로 유지 — 설계 D "매치 시 기존 연출 유지") ────────────
        private const float SetGlowPulseDuration = 0.35f;
        private const float Set4FlashDuration = 0.12f;
        private const float JackpotShakeAmplitude = 6f;
        private const float JackpotShakeDuration = 0.3f;
        private const float JackpotBannerHold = 1.2f;
        private const float JackpotBannerFade = 0.2f;
        private const float SkullShakeAmplitude = 4f;
        private const float SkullShakeDuration = 0.25f;
        private const float SkullTintDuration = 0.3f;

        // UiSpriteGen.SymbolColors(Editor 전용)와 동일한 값을 런타임에서 쓰기 위한 복제 — 소스가
        // 다르면(에디터/런타임) 갈라지므로 심볼 색을 바꿀 때 두 표를 함께 갱신할 것(UiSpriteGen.cs 헤더 참조).
        private static readonly Dictionary<string, Color> SymbolTintById = new Dictionary<string, Color>
        {
            { "cherry", HexColor("#E5484D") }, { "book", HexColor("#4C8DFF") }, { "star", HexColor("#F5C518") },
            { "gem", HexColor("#7C5CFF") }, { "crown", HexColor("#FFB300") }, { "skull", HexColor("#9BA3B4") },
            { "coin", HexColor("#E8B93C") }, { "flame", HexColor("#FF6B35") }, { "magnet", HexColor("#5B8CFF") },
            { "bomb", HexColor("#3A4051") }, { "dice", HexColor("#E8EAF2") }, { "seed", HexColor("#4CAF50") },
            { "wild", HexColor("#00C2A8") }, { "key", HexColor("#C9A227") },
        };

        // S16 §B — 폭탄 폭발 버스트(FxId.Skull) 틴트. 설계 명시값 그대로(UiKit.Hex와 동일 구현의
        // HexColor를 그대로 재사용).
        private static readonly Color BombBurstTint = HexColor("#FF7A3D");

        // Cell.tag(SpinResolver.cs)가 내보내는 astral 이모지를 BMP 안전 기호로 치환(엔진 파일은 미수정
        // — 표시 레이어에서만 변환). 목록: 성장 "🌱→"/와일드주입 "🌀"/제거 "🧽"/왕관강제 "👑"/폭탄폭발
        // "💥"/자석흡착 "🧲"(SpinResolver.cs 그렙 결과 전수).
        private static readonly Dictionary<string, string> TagTranslate = new Dictionary<string, string>
        {
            { "🌱→", "↑" }, { "🌀", "W" }, { "🧽", "X" }, { "👑", "♛" }, { "💥", "*" }, { "🧲", "M" },
        };

        [Serializable]
        private struct SymbolSprite
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private RectTransform reelRow;
        [SerializeField] private RectTransform cellTemplate; // 자식 경로 계약: "Strip"→"Slot0".."Slot4"(각 "Icon"/"Tag") + "Streak"(Image) + Outline×2
        [SerializeField] private SymbolSprite[] symbolSprites = Array.Empty<SymbolSprite>();
        [SerializeField] private CanvasGroup flashOverlay; // 화면 전체를 덮는 흰색 플래시(set4/잭팟 공용)
        [SerializeField] private CanvasGroup jackpotBannerGroup;
        [SerializeField] private RectTransform jackpotBannerRect;
        // S14 §G — 모든 화면 셰이크/기대감 줌의 대상. RunView가 자기 자신의 RectTransform("RunScreen"
        // 루트)을 넘긴다(HudView도 동일 참조를 별도로 보유 — 보스 진입 셰이크용).
        [SerializeField] private RectTransform runScreenRoot;

        private Dictionary<string, Sprite> _spriteById;
        // FUN_OVERHAUL_DESIGN §3 — 런타임 해석 캐시(null 도 캐시해 Resources.Load 반복 호출을 막는다).
        // 인스턴스 필드라 화면(씬)을 다시 열면 비워진다 → 나중에 PNG 가 추가돼도 stale null 이 남지 않는다.
        private Dictionary<string, Sprite> _resolvedById;
        private Coroutine _screenShakeRoutine;

        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 탭(openCellSheet 대응). RunView가 1회만
        // 등록한다(WireOnce 관례) — EnsureCellCount가 매 스핀 셀을 새로 만들 때마다 이 핸들러를
        // 인덱스별로 다시 건다.
        private Action<int> _cellTapHandler;

        public void SetCellTapHandler(Action<int> handler) => _cellTapHandler = handler;

        // 재미 개편 F4 — 롱프레스(0.45s) = 셀 정보(홀드 가능 상태에서 탭은 홀드 토글이라 정보 진입이 롱프레스로 옮겨 간다).
        private Action<int> _cellLongPressHandler;

        public void SetCellLongPressHandler(Action<int> handler) => _cellLongPressHandler = handler;

        /// <summary>지금 셀이 만들어져 있는가(홀드 선택에는 직전 스핀 결과가 화면에 남아 있어야 한다 — RunView 가 불필요한 ShowIdle 을 건너뛰는 근거).</summary>
        public bool HasCells => _cells.Count > 0;

        /// <summary>릴과 같은 순서로 심볼 스프라이트를 해석한다(신규 그림 → 씬에 구운 도형 타일 → null). 앵콜 박스 타일이 같은 그림을 쓰게 한다.</summary>
        public Sprite ResolveSymbolSprite(string symId) => SymbolSpriteOf(symId);
        public int CellCount => _cells.Count;

        private sealed class SlotView
        {
            public RectTransform rt;
            public Image icon;
            public Text tag;
        }

        private sealed class CellView
        {
            public RectTransform rt;      // Reel_i(RectMask2D 뷰포트)
            public RectTransform strip;   // Strip(세로 5칸)
            public SlotView[] slots;      // 고정 5개, index0(맨위)..index4(맨아래), index2=중앙
            public Image streak;          // S14 §B — 최고속 모션 스트릭 오버레이(w_streak, 평소 알파 0)
            public Outline glow;          // 매치 글로우(테두리 Outline과 별개 — GetComponents<Outline>()[1])
            public string lastSymId;      // 마지막으로 중앙에 착지한 심볼 id — 세트 글로우 매칭용
            public CanvasGroup holdGroup; // F4 홀드 오버레이(틴트+프레임 발광+자물쇠 배지) — 평소 알파 0
            public RectTransform holdBadge; // 자물쇠 배지(켜질 때 팝)
            public Coroutine holdFade;    // 홀드 자물쇠 팝/스핀 후 소등 페이드(한 번에 하나)
            public int holdGen;           // 2차 검수 M1 — 코루틴 세대 토큰(교체된 연출이 값을 덮어쓰지 않게)
            public bool holdOn;           // 오버레이가 켜져 있는가

            public Image CenterIcon => slots != null && slots.Length > 2 ? slots[2].icon : null;
        }

        private readonly List<CellView> _cells = new List<CellView>();

        private void Awake()
        {
            _spriteById = new Dictionary<string, Sprite>(symbolSprites.Length);
            foreach (var s in symbolSprites)
                if (!string.IsNullOrEmpty(s.id) && s.sprite != null) _spriteById[s.id] = s.sprite;
            _resolvedById = new Dictionary<string, Sprite>();

            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            if (flashOverlay != null) flashOverlay.alpha = 0f;
            if (jackpotBannerGroup != null) jackpotBannerGroup.alpha = 0f;
        }

        /// <summary>빈 릴(런 시작 직후 등 아직 스핀 결과가 없을 때) — 이전 셀을 모두 지운다.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            _screenShakeRoutine = null;
            ResetFeverFrame();
            for (int i = _cells.Count - 1; i >= 0; i--)
                if (_cells[i].rt != null) Destroy(_cells[i].rt.gameObject);
            _cells.Clear();
        }

        /// <summary>스핀 전 대기 상태 릴을 보여준다 — 셀은 PlaySpinRoutine 안에서만 만들어지므로,
        /// 이걸 호출하지 않으면 첫 스핀 전까지 릴 영역이 통째로 비어 보인다(런 시작 직후 화면).
        /// 중앙/이웃 슬롯에 무작위 심볼을 채워 "멈춰 있는 슬롯머신" 상태로 세운다.</summary>
        public void ShowIdle(int reelCount)
        {
            if (reelCount <= 0) return;
            EnsureCellCount(reelCount);
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (cell.slots == null) continue;
                for (int k = 0; k < cell.slots.Length; k++)
                {
                    var slot = cell.slots[k];
                    if (slot?.icon == null) continue;
                    slot.icon.sprite = SymbolSpriteOf(RandomSymbol().id);
                    slot.icon.enabled = slot.icon.sprite != null;
                    if (slot.tag != null) slot.tag.text = "";
                }
            }
        }

        /// <summary>스핀 결과 연출 전체(차징→가속→유지→왼쪽부터 스태거 감속 정지→세트/잭팟/해골 FX)를
        /// 재생한다. onCellsRevealed는 마지막 셀이 멈춘 직후(후처리 FX 시작 전) 호출된다 — RunView가
        /// 이 시점에 HUD EXP 카운트업/코인·점수 플로팅 텍스트를 동시에 시작한다(설계 "정지 후 획득
        /// 라인 표시").</summary>
        public IEnumerator PlaySpinRoutine(SpinResult result, Action onCellsRevealed, IReadOnlyList<int> heldCells = null, int bigWinTier = 0)
        {
            if (result == null || result.cells == null)
            {
                onCellsRevealed?.Invoke();
                yield break;
            }

            EnsureCellCount(result.cells.Count);

            // S16 §B — 릴은 최종 결과(cells)가 아니라 원시 착지 심볼(rawCells)로 멈춘다(폭탄💥/자석🧲
            // 등 Evaluate 내부 변형 이전 상태 — null이면 기존대로 cells, 방어).
            IReadOnlyList<Cell> landingCells = result.rawCells ?? result.cells;

            // F4 홀드 — 직전 스핀의 심볼을 유지하는 칸은 돌리지 않는다: 새로 만든 칸에 지금 착지 심볼을 미리 세워 두고
            // 오버레이(자물쇠)를 켠 채로 두었다가, 다른 칸이 다 멈춘 뒤 한 번 번쩍이며 해제한다(웹 holdKeepReel).
            HashSet<int> held = null;
            if (heldCells != null && heldCells.Count > 0)
            {
                held = new HashSet<int>();
                for (int k = 0; k < heldCells.Count; k++)
                {
                    int hi = heldCells[k];
                    if (hi >= 0 && hi < _cells.Count && hi < landingCells.Count) held.Add(hi);
                }
                if (held.Count == 0) held = null;
                else foreach (int hi in held) PrepareHeldCell(_cells[hi], landingCells[hi]);
            }

            // S14 §B — 차징: 릴 전체가 위로 반동했다 낙하하며 스핀이 시작된다(스핀 버튼 스쿼시는
            // RunView가 버튼 자신에 대해 동시에 재생한다). 홀드 칸은 제자리.
            yield return ChargeRoutine(held);

            DetectNearMiss(result, out bool isNearMiss, out SymInfo nearMissSym);
            bool isAnticipation = DetectAnticipation(result);

            // ── 릴별 정지 타이밍: 왼쪽부터 0.10s 스태거(설계 명시), 가속(0.25s)+최소 유지(0.35s,
            // 설계 미명시 기본값) 이후 순서대로 멈춘다. 니어미스/기대감은 "마지막 릴"에만 적용(설계 그대로).
            // 마지막 릴이 홀드면 돌지 않으므로 연출 없음(웹 planReelFx).
            int lastIdx = result.cells.Count - 1;
            if (held != null && held.Contains(lastIdx)) { isNearMiss = false; isAnticipation = false; }
            var routines = new Coroutine[_cells.Count];
            for (int i = 0; i < _cells.Count && i < landingCells.Count; i++)
            {
                if (held != null && held.Contains(i))
                {
                    routines[i] = StartCoroutine(HoldKeepCell(_cells[i], landingCells[i]));
                    continue;
                }
                float stopDelay = AccelDuration + BaseSpinHold + i * StaggerDelay;
                bool nearMissHere = isNearMiss && i == lastIdx;
                bool anticipationHere = isAnticipation && i == lastIdx;
                routines[i] = StartCoroutine(SpinOneReel(_cells[i], landingCells[i], stopDelay, nearMissHere, anticipationHere, nearMissSym));
            }
            for (int i = 0; i < routines.Length; i++)
                if (routines[i] != null) yield return routines[i];

            // S16 §B — 전 릴 정지 후, 원시 착지(raw)와 최종 결과(cells)가 다른 칸만 최종 값으로 갈아
            // 끼우는 변형 공개 패스. 변형이 없으면(대부분의 스핀) 즉시 반환 — 추가 지연 0.
            yield return TransformRevealRoutine(result);

            // F4: 잠금 표시 해제(번쩍이며 사라짐) — 결과 공개 직전.
            if (held != null) foreach (int hi in held) ReleaseHeldCell(_cells[hi]);

            onCellsRevealed?.Invoke();

            // ── 세트/잭팟/해골/빅윈 FX ──────────────────────────────────────────────────
            yield return PostRevealFx(result, bigWinTier);
        }

        // F4 — 홀드 칸 사전 배치: 중앙 슬롯에 이번 착지 심볼(=직전 raw)을 세우고 이웃은 임의 심볼로 채운 뒤 오버레이를 켠다.
        private void PrepareHeldCell(CellView cv, Cell landing)
        {
            if (cv?.slots == null || landing?.sym == null) return;
            for (int k = 0; k < cv.slots.Length; k++)
                SetSlotSymbol(cv.slots[k], k == 2 ? landing.sym : RandomSymbol(), k == 2 ? landing.tag : "");
            ApplyRestDimming(cv);
            cv.lastSymId = landing.sym.id;
            SetHoldVisual(cv, true, pop: false);
        }

        // F4 — 홀드 칸은 회전 없이 즉시 끝난다(착지음/먼지 FX 도 없다 — 돌지 않았으므로).
        private IEnumerator HoldKeepCell(CellView cv, Cell landing)
        {
            yield break;
        }

        // S14 §B — 차징: 모든 셀의 Strip을 동시에 위로 12px 반동시켰다 낙하 복귀시킨다(스크롤 노치와
        // 동일한 Strip.anchoredPosition을 잠깐 빌려 쓰고, 끝나면 정확히 0으로 되돌리므로 이어지는
        // AdvanceNotch 시퀀스와 충돌하지 않는다). reelRow는 HorizontalLayoutGroup 자식이라
        // anchoredPosition을 레이아웃이 되돌릴 수 있어 셀(cv.rt)이 아니라 Strip에 적용한다.
        private IEnumerator ChargeRoutine(HashSet<int> skip = null)
        {
            if (_cells.Count == 0) yield break;
            yield return AnimateAllStripsY(0f, ChargeUpDistance, ChargeUpDuration, UiTween.Ease.OutBack, skip);
            yield return AnimateAllStripsY(ChargeUpDistance, 0f, ChargeFallDuration, UiTween.Ease.OutQuad, skip);
        }

        // skip: 홀드 칸 인덱스 — 반동 없이 제자리를 지킨다.
        private IEnumerator AnimateAllStripsY(float from, float to, float duration, UiTween.Ease ease, HashSet<int> skip = null)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float y = Mathf.LerpUnclamped(from, to, UiTween.Apply(ease, t / duration));
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (skip != null && skip.Contains(i)) continue;
                    var strip = _cells[i]?.strip;
                    if (strip != null) strip.anchoredPosition = new Vector2(0f, y);
                }
                yield return null;
            }
            for (int i = 0; i < _cells.Count; i++)
            {
                if (skip != null && skip.Contains(i)) continue;
                var strip = _cells[i]?.strip;
                if (strip != null) strip.anchoredPosition = new Vector2(0f, to);
            }
        }

        // SpinResolver.cs의 ValueIds(=Symbols.ValueIds: cherry/star/book/gem/crown)만 "세트"에
        // 기여한다 — skull/bomb/coin/flame/magnet/dice/seed/wild/key는 몇 개가 모여도 세트가 되지
        // 않는다(엔진 규칙, SpinResolver.cs ValueIds 사용처 전수). 니어미스/기대감 후보도 같은
        // 집합으로 제한해야 "완성될 수 없는 조합"을 아깝다고/기대된다고 연출하는 오류를 피한다.
        private static readonly HashSet<string> NearMissCandidateIds = BuildNearMissCandidateIds();
        private static HashSet<string> BuildNearMissCandidateIds()
        {
            var set = new HashSet<string>();
            foreach (var sym in Symbols.ValueIds)
            {
                var info = Symbols.BySym(sym);
                if (info != null) set.Add(info.id);
            }
            return set;
        }

        // cells[0..uptoExclusiveIdx) 구간에서 "세트 가능" 심볼 중 가장 많이 등장한 것을 찾는다 —
        // 니어미스/기대감/2매치 감지가 전부 공유하는 카운팅 로직(설계 D "cells 배열에서 최다 심볼
        // 개수와 마지막 셀 이웃 비교" 문구를 일반화).
        private static void FindDominantValueSymbol(SpinResult result, int uptoExclusiveIdx, out string bestId, out int bestCount)
        {
            bestId = null;
            bestCount = 0;
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < uptoExclusiveIdx && i < result.cells.Count; i++)
            {
                var sym = result.cells[i]?.sym;
                if (sym == null || !NearMissCandidateIds.Contains(sym.id)) continue;
                counts.TryGetValue(sym.id, out var c);
                counts[sym.id] = c + 1;
            }
            foreach (var kv in counts)
                if (kv.Value > bestCount) { bestCount = kv.Value; bestId = kv.Key; }
        }

        // ── S14 §B 기대감(anticipation) 판정 — "정지 순서상 남은 릴이 1개이고 이미 같은 심볼 3개 이상"
        // (마지막 릴을 뺀 앞 칸 기준)이면 결과가 실제로 세트를 완성하는지와 무관하게 트리거된다(니어미스와
        // 달리 "완성되는 경우"도 포함 — 서스펜스는 결과와 무관하게 쌓인다).
        // 재미 개편 F7(웹 ui.js ANTIC_MIN, 2026-10-28 리드 결정): 기준을 2→3개로 올렸다 — 2개 기준은 스핀의 절반에서 떠
        // 긴장감이 흐려졌다. 니어미스(DetectNearMiss)도 같은 3개 기준 위에서 "4매치 실패"로 맞춰 다시 기대감의 부분집합이 된다.
        private const int AnticipationMinCount = 3;

        private static bool DetectAnticipation(SpinResult result)
        {
            int lastIdx = result.cells.Count - 1;
            if (lastIdx < 1) return false;
            FindDominantValueSymbol(result, lastIdx, out _, out int bestCount);
            return bestCount >= AnticipationMinCount;
        }

        // ── 니어미스 판정(설계 D "니어미스" — 뷰 계층에서만 수행, 엔진 결과 불변) ──────────────────
        // 재미 개편 F7(웹 ui.js planReelFx, 2026-10-28 리드 결정): 기대감 조건(마지막 릴을 뺀 앞 칸에 "세트 가능" 같은 값심볼 3개↑)
        // + 최종 결과가 4매치 미만(bestSetCount < AnticipationMinCount+1 — 한 칸만 더 맞았으면 4매치였다) + 마지막 릴 결과가 그
        // 심볼이 아니면 그 심볼을 "니어미스 후보"로 삼는다. (기존 "앞 2개 + 결과 3매치 미만"은 기대감 기준을 3개로 올리면서
        // 기대감과 동시에 성립할 수 없게 돼 니어미스가 영영 안 뜨므로 4매치 실패 기준으로 옮겼다.)
        private static void DetectNearMiss(SpinResult result, out bool isNearMiss, out SymInfo nearMissSym)
        {
            isNearMiss = false;
            nearMissSym = null;

            int lastIdx = result.cells.Count - 1;
            if (lastIdx < 1 || result.bestSetCount >= AnticipationMinCount + 1) return;

            FindDominantValueSymbol(result, lastIdx, out string bestId, out int bestCount);
            if (bestCount < AnticipationMinCount || bestId == null) return;
            string lastSymId = result.cells[lastIdx]?.sym?.id;
            if (bestId == lastSymId) return; // 마지막 릴이 같은 심볼이면 니어미스가 아니다(4매치가 이뤄진 경우 등) — 방어적 가드

            var candidate = Symbols.ById(bestId);
            if (candidate == null) return;

            isNearMiss = true;
            nearMissSym = candidate;
        }

        // ── 릴 1개 스핀 상태 기계 ────────────────────────────────────────────────────────
        private IEnumerator SpinOneReel(CellView cv, Cell targetCell, float stopDelay, bool isNearMiss, bool isAnticipation, SymInfo nearMissSym)
        {
            if (cv?.strip == null || targetCell?.sym == null) yield break;

            // ── 가속(0.25s)+유지 — stopDelay 직전 노치까지 무작위 심볼로 순환. 최고속 구간(노치 길이가
            // MaxSpeedNotch에 도달)에서는 §B 모션 스트릭(스트릭 오버레이+심볼 알파/스케일)을 켠다.
            float t = 0f;
            while (true)
            {
                float notchDur = NotchDuration(t);
                if (t + notchDur >= stopDelay) break;
                bool maxSpeed = notchDur <= MaxSpeedNotch + 0.001f;
                ApplyMaxSpeedStyle(cv, maxSpeed);
                yield return AdvanceNotch(cv, notchDur, RandomSymbol(), "", UiTween.Ease.Linear);
                t += notchDur;
            }

            // ── S14 §B 기대감 — 남은 릴 1개(=이 릴)이고 이미 2개 이상 같은 심볼이면 유지 구간을
            // +0.6s 연장하고, 셀 테두리 골드 펄스 + 화면 줌인(1.0→1.02)을 재생한다.
            Coroutine anticipationGlow = null;
            if (isAnticipation)
            {
                ApplyMaxSpeedStyle(cv, true);
                anticipationGlow = StartCoroutine(AnticipationGlowLoop(cv));
                if (runScreenRoot != null)
                    StartCoroutine(UiTween.ScaleRoutine(runScreenRoot, runScreenRoot.localScale,
                        Vector3.one * AnticipationZoomScale, AnticipationZoomInDuration, UiTween.Ease.OutQuad));

                float extra = 0f;
                while (extra < AnticipationHoldExtension)
                {
                    yield return AdvanceNotch(cv, MaxSpeedNotch, RandomSymbol(), "", UiTween.Ease.Linear);
                    extra += MaxSpeedNotch;
                }
            }

            // ── 유지 종료 — 블러/스트릭 해제 후 마지막 한 노치(니어미스면 여기서 X를 주입, 2노치 뒤 중앙 통과).
            ApplyMaxSpeedStyle(cv, false);
            {
                SymInfo injected = isNearMiss ? nearMissSym : RandomSymbol();
                yield return AdvanceNotch(cv, MaxSpeedNotch, injected, "", UiTween.Ease.Linear);
            }

            if (anticipationGlow != null)
            {
                StopCoroutine(anticipationGlow);
                if (cv.glow != null) cv.glow.enabled = false;
            }

            // ── 감속(파일 헤더 "노치 파이프라인" 공식) — 기대감이면 3→5노치로 늘어난다(설계 §B).
            float[] decelDurs = isAnticipation ? DecelDursAnticipation : DecelDursNormal;
            yield return DecelSequence(cv, targetCell, decelDurs, isNearMiss);

            if (isAnticipation && runScreenRoot != null)
                StartCoroutine(UiTween.ScaleRoutine(runScreenRoot, runScreenRoot.localScale, Vector3.one,
                    AnticipationZoomOutDuration, UiTween.Ease.OutQuad));

            // ── 정지: 오버슈트 + 착지 임팩트(§B) + fx_spin_stop + 이웃 슬롯 디밍 ─────────────────
            cv.lastSymId = targetCell.sym.id;
            yield return OvershootRoutine(cv);
            ApplyRestDimming(cv);
            Color tint = SymbolTintById.TryGetValue(cv.lastSymId ?? "", out var symTint) ? symTint : Color.white;
            FxKit.I?.Play(FxId.SpinStop, cv.rt, tint);
            PlayLandingImpact(cv);
            // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 ui.js:334 landReel 꼬리) — 릴 1개가
            // 멈출 때마다(전체 스핀 1회당 릴 개수만큼 반복) "reel" 틱음.
            SoundKit.Sfx("reel");

            if (isNearMiss) StartCoroutine(TintRoutine(cv.CenterIcon, Color.white, NearMissGray, NearMissGrayFadeDuration));
        }

        // 가속 구간 노치 길이 — 0→AccelDuration 동안 AccelStartNotch에서 MaxSpeedNotch로 줄어들다가
        // (OutQuad로 "빠르게 붙었다 최고속에 안착"하는 느낌) 그 이후는 MaxSpeedNotch로 고정(=유지).
        private static float NotchDuration(float elapsedSinceSpinStart)
        {
            float speedT = Mathf.Clamp01(elapsedSinceSpinStart / AccelDuration);
            return Mathf.Lerp(AccelStartNotch, MaxSpeedNotch, UiTween.Apply(UiTween.Ease.OutQuad, speedT));
        }

        // ── S14 §B 감속 시퀀스 — 노치 수 D(3=기본/5=기대감) 공용. 목표 심볼(Y)은 (D-2)번째 노치
        // (0-based D-3)에서 주입해야 마지막 노치가 끝날 때 중앙에 도착한다. 니어미스 X는 감속이
        // 몇 노치든 항상 "2번째 노치(0-based idx1)"가 끝날 때 중앙을 스친다(자기 주입 시점 기준
        // 2노치 뒤이므로 D와 무관 — 파일 헤더 주석 참조).
        private IEnumerator DecelSequence(CellView cv, Cell targetCell, float[] durs, bool isNearMiss)
        {
            int d = durs.Length;
            int injectAt = d - 3;
            for (int idx = 0; idx < d; idx++)
            {
                bool isTargetNotch = idx == injectAt;
                SymInfo sym = isTargetNotch ? targetCell.sym : RandomSymbol();
                string tag = isTargetNotch ? targetCell.tag : "";
                float dur = durs[idx];
                bool isPassNotch = idx == 1;
                if (isPassNotch && isNearMiss) dur = NearMissPauseDuration;
                yield return AdvanceNotch(cv, dur, sym, tag, UiTween.Ease.OutCubic);
                if (isPassNotch && isNearMiss) StartCoroutine(NearMissFlashRoutine(cv));
            }
        }

        // 스트립을 cellHeight만큼 아래로 흘려보낸 뒤(무한 스크롤 시각 효과), 맨 아래로 밀려난 슬롯을
        // 새 심볼로 갈아 끼워 맨 위 자리로 재활용하고 스트립 위치를 원점으로 되돌린다(이음매 없음).
        private IEnumerator AdvanceNotch(CellView cv, float duration, SymInfo newSym, string newTag, UiTween.Ease ease)
        {
            if (cv?.strip == null) yield break;
            yield return UiTween.MoveRoutine(cv.strip, Vector2.zero, new Vector2(0f, -UiKit.ReelSlotSize), duration, ease);
            if (cv.strip == null) yield break;

            for (int k = cv.slots.Length - 1; k >= 1; k--)
                CopySlot(cv.slots[k], cv.slots[k - 1]);
            SetSlotSymbol(cv.slots[0], newSym, newTag);
            cv.strip.anchoredPosition = Vector2.zero;
        }

        private IEnumerator OvershootRoutine(CellView cv)
        {
            if (cv?.strip == null) yield break;
            cv.strip.anchoredPosition = new Vector2(0f, OvershootDistance);
            yield return UiTween.MoveRoutine(cv.strip, cv.strip.anchoredPosition, Vector2.zero, OvershootDuration, UiTween.Ease.OutBack);
        }

        // 평소(정지) 상태 — 중앙만 결과 심볼(알파1/스케일1), 나머지 4칸(위2/아래2)은 이웃 심볼
        // 취급(알파.42/스케일.88, S14 §A)으로 "릴의 위아래가 상시 보이는" 느낌을 낸다.
        private static void ApplyRestDimming(CellView cv)
        {
            if (cv?.slots == null) return;
            for (int k = 0; k < cv.slots.Length; k++)
            {
                var slot = cv.slots[k];
                if (slot?.icon == null) continue;
                bool isCenter = k == 2;
                var c = slot.icon.color;
                c.a = isCenter ? 1f : RestNeighborAlpha;
                slot.icon.color = c;
                slot.rt.localScale = Vector3.one * (isCenter ? 1f : RestNeighborScale);
            }
        }

        // S14 §B — 최고속 구간 스타일(활성 시 심볼 alpha.75/scale1.04 + 스트릭 오버레이 alpha.35,
        // 비활성 시 원상복구). 감속 시작 전 반드시 false로 되돌려야 정지 연출이 흐릿하지 않다.
        private static void ApplyMaxSpeedStyle(CellView cv, bool active)
        {
            if (cv?.slots == null) return;
            float a = active ? MaxSpeedSymbolAlpha : 1f;
            float s = active ? MaxSpeedSymbolScale : 1f;
            for (int k = 0; k < cv.slots.Length; k++)
            {
                var slot = cv.slots[k];
                if (slot?.icon == null) continue;
                var c = slot.icon.color;
                c.a = a;
                slot.icon.color = c;
                if (slot.rt != null) slot.rt.localScale = Vector3.one * s;
            }
            if (cv.streak != null)
            {
                var c = cv.streak.color;
                c.a = active ? MaxSpeedStreakAlpha : 0f;
                cv.streak.color = c;
            }
        }

        // S14 §B — 기대감 동안 셀 테두리(글로우 아웃라인)를 반복 펄스(0.5s 주기)한다. 실제 매치가
        // 나중에 이 셀에서 성립하면 PostRevealFx의 GlowMatchingCells가 별도로 다시 켠다.
        private IEnumerator AnticipationGlowLoop(CellView cv)
        {
            if (cv?.glow == null) yield break;
            cv.glow.enabled = true;
            cv.glow.effectColor = UiKit.Accent;
            while (true)
            {
                yield return UiTween.FloatRoutine(0.25f, 1f, AnticipationGlowHalfCycle, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
                if (cv.glow == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0.25f, AnticipationGlowHalfCycle, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
            }
        }

        // S14 §B — 착지 임팩트: 셀 스쿼시(scaleY 0.92→1) + fx_reel_land(바닥 먼지) + 화면 미세 셰이크(2px).
        private void PlayLandingImpact(CellView cv)
        {
            if (cv?.rt == null) return;
            StartCoroutine(LandSquashRoutine(cv.rt));
            FxKit.I?.Play(FxId.ReelLand, cv.rt);
            PlayScreenShake(LandShakeAmplitude, LandShakeDuration);
        }

        private IEnumerator LandSquashRoutine(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector3 from = new Vector3(1f, LandSquashScaleY, 1f);
            rt.localScale = from;
            yield return UiTween.ScaleRoutine(rt, from, Vector3.one, LandSquashDuration, UiTween.Ease.OutQuad);
        }

        // S14 §G — RunScreen 루트 셰이크 공용 헬퍼. 진행 중인 셰이크가 있으면 취소하고 원점(0,0)으로
        // 스냅한 뒤 새로 시작한다 — 여러 셰이크(연속 릴 착지 등)가 겹쳐도 anchoredPosition이 누적
        // 표류하지 않는다(각 UiTween.ShakeRoutine은 시작 시점 위치를 기준점으로 캡처하므로, 취소 없이
        // 겹치면 서로 다른 기준점으로 복원해 표류가 생긴다).
        private void PlayScreenShake(float amplitude, float duration)
        {
            if (runScreenRoot == null) return;
            if (_screenShakeRoutine != null)
            {
                StopCoroutine(_screenShakeRoutine);
                runScreenRoot.anchoredPosition = Vector2.zero;
            }
            _screenShakeRoutine = StartCoroutine(ScreenShakeRoutine(amplitude, duration));
        }

        // ── S16 §C 클리어 등급 연출(웹 파리티 P4, WEB_PARITY_DESIGN.md §1-A #16) ────────────────
        // 웹 stageClearFx(ui.js:1700-1709) shake(tier>=5?"xl":tier>=3?"bg":"sm") + "tier>=4면 230ms 후
        // 2차 흔들림" 그대로. gradeTier는 1~5, PERFECT=6(웹과 동일 — StageFlow.ClearStage 주석 참조)이라
        // tier>=5가 6(perfect)도 자연히 xl로 포함한다. amplitude/duration은 이 파일 기존 셰이크 상수
        // (Set3/Set4/Jackpot 3px~6px 대역)와 같은 눈금으로 맞췄다 — 웹 CSS 진폭 자체는 이식 대상이 아님.
        private const float ClearShakeAmpSm = 3f;
        private const float ClearShakeAmpBg = 6f;
        private const float ClearShakeAmpXl = 9f;
        private const float ClearShakeDurSm = 0.2f;
        private const float ClearShakeDurBg = 0.3f;
        private const float ClearShakeDurXl = 0.4f;
        private const float ClearShakeSecondDelay = 0.23f; // 웹 "230ms 후" 그대로

        public void PlayClearShake(int gradeTier)
        {
            float amp = gradeTier >= 5 ? ClearShakeAmpXl : gradeTier >= 3 ? ClearShakeAmpBg : ClearShakeAmpSm;
            float dur = gradeTier >= 5 ? ClearShakeDurXl : gradeTier >= 3 ? ClearShakeDurBg : ClearShakeDurSm;
            PlayScreenShake(amp, dur);
            if (gradeTier >= 4) StartCoroutine(DelayedSecondShake());
        }

        private IEnumerator DelayedSecondShake()
        {
            yield return new WaitForSeconds(ClearShakeSecondDelay);
            PlayScreenShake(ClearShakeAmpBg, ClearShakeDurBg);
        }

        private IEnumerator ScreenShakeRoutine(float amplitude, float duration)
        {
            yield return UiTween.ShakeRoutine(runScreenRoot, amplitude, duration);
            _screenShakeRoutine = null;
        }

        private IEnumerator NearMissFlashRoutine(CellView cv)
        {
            if (cv?.glow == null) yield break;
            bool wasEnabled = cv.glow.enabled;
            Color prevColor = cv.glow.effectColor;
            cv.glow.enabled = true;
            cv.glow.effectColor = UiKit.Accent;
            yield return UiTween.FloatRoutine(0f, 1f, NearMissFlashDuration * 0.5f, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
            if (cv.glow == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, NearMissFlashDuration * 0.5f, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
            if (cv.glow == null) yield break;
            // 니어미스는 실제 매치가 아니므로 글로우를 원상복구한다(대개 disabled) — 실제 매치 처리는
            // 이 루틴이 끝난 뒤 PostRevealFx/GlowMatchingCells가 별도로 담당(둘이 겹칠 일은 없다 —
            // 이 플래시는 마지막 릴이 멈추기 전(감속 2번째 노치)에 끝나고, 셋 글로우는 전 릴 정지 후이며 마지막 칸은 그 심볼이 아니다.
            // F7 이후 니어미스는 3매치 결과와 함께 날 수 있다 — 3개는 글로우, 빗나간 마지막 칸만 회색으로 가라앉는다).
            cv.glow.effectColor = prevColor;
            cv.glow.enabled = wasEnabled;
        }

        // ══════════════════════════════════════════════════════════════════════
        // S16 §B — 변형 공개 패스. SpinResolver.Evaluate가 폭탄 인접 칸을 Cell(EmptySym,"💥")로
        // 덮어써 원본 심볼 정보가 소실되므로(엔진 로직 불변), 릴은 원시 착지(rawCells)로 먼저 멈추고
        // 전 릴 정지 후 이 패스가 raw≠final인 칸만 최종 값으로 갈아 끼운다. 대부분의 스핀은 변형이
        // 없어 즉시 반환(추가 지연 0).
        // ══════════════════════════════════════════════════════════════════════
        private IEnumerator TransformRevealRoutine(SpinResult result)
        {
            var raw = result.rawCells;
            var final = result.cells;
            if (raw == null) yield break;

            var diffIdxs = new List<int>();
            for (int i = 0; i < _cells.Count && i < raw.Count && i < final.Count; i++)
                if (!ReferenceEquals(raw[i], final[i])) diffIdxs.Add(i);
            if (diffIdxs.Count == 0) yield break;

            // 폭발 전 잠깐(0.25s) 원본 심볼을 그대로 보여주는 대기(설계 명시).
            yield return new WaitForSeconds(TransformHoldDuration);

            bool anyBombBurst = false;
            var bombSourcePunched = new HashSet<int>();
            var reveals = new List<Coroutine>();
            for (int k = 0; k < diffIdxs.Count; k++)
            {
                int i = diffIdxs[k];
                float delay = k * TransformStaggerDelay; // 칸별 스태거 0.05s(설계 명시)
                var finalCell = final[i];
                bool isBombBurst = finalCell?.tag == "💥";
                reveals.Add(StartCoroutine(RevealTransformedCellRoutine(_cells[i], finalCell, isBombBurst, delay)));

                if (isBombBurst)
                {
                    anyBombBurst = true;
                    // 터뜨린 주체(인접 폭탄 칸) 강조 — 같은 스태거 시점에 0.94→1 살짝 펀치(설계 명시).
                    foreach (int adj in new[] { i - 1, i + 1 })
                    {
                        if (adj < 0 || adj >= raw.Count || adj >= _cells.Count) continue;
                        if (raw[adj]?.sym == null || raw[adj].sym.special != Sp.BOMB) continue;
                        if (!bombSourcePunched.Add(adj)) continue;
                        reveals.Add(StartCoroutine(BombSourcePunchRoutine(_cells[adj], delay)));
                    }
                }
            }

            // 폭발이 1개 이상이면 릴 전체에 S14 셰이크 함수(PlayScreenShake) 재사용(설계 명시 ±6px/0.15s).
            // 웹 파리티 P5(웹 ui.js:445 blastBomb 꼬리 `shake("sm"); snd.sfx("bomb");`) — 같은 타이밍에 폭발음.
            if (anyBombBurst) { PlayScreenShake(BombShakeAmplitude, BombShakeDuration); SoundKit.Sfx("bomb"); }

            for (int r = 0; r < reveals.Count; r++)
                if (reveals[r] != null) yield return reveals[r];
        }

        // 최종 셀 태그가 "💥"(폭탄 폭발)이면 FxId.Skull 주황 틴트 버스트 + 칸 스케일 펀치(1→1.18→0,
        // InBack 수축) 후 최종(빈칸) 표시. 그 외 변형은 개별 FX 없이 0.12s 스케일 다운→업 팝 스왑으로만
        // 최종 셀을 보여준다(설계 — 이번 스코프는 폭탄만 강조). Evaluate 내부 변형은 💥·🧲 2종뿐이라
        // 이 분기는 실질적으로 자석 복사(🧲) 전용이다 — 👑/🧽/🌀/🌱→는 Evaluate 이전에 raw에 반영돼
        // raw≠final 차집합에 안 잡힌다(Opus S16 검수 중요-1).
        private IEnumerator RevealTransformedCellRoutine(CellView cv, Cell finalCell, bool isBombBurst, float startDelay)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            var slot = cv?.slots != null && cv.slots.Length > 2 ? cv.slots[2] : null;
            if (slot?.rt == null || finalCell?.sym == null) yield break;

            if (isBombBurst)
            {
                FxKit.I?.Play(FxId.Skull, cv.rt, BombBurstTint);
                yield return UiTween.ScaleRoutine(slot.rt, Vector3.one, Vector3.one * BombPunchPeakScale, BombPunchGrowDuration, UiTween.Ease.OutQuad);
                if (slot.rt == null) yield break;
                yield return UiTween.ScaleRoutine(slot.rt, slot.rt.localScale, Vector3.zero, BombPunchShrinkDuration, UiTween.Ease.InBack);
                if (slot.rt == null) yield break;
                SetSlotSymbol(slot, finalCell.sym, finalCell.tag); // 내부에서 스케일을 1로 복원(빈칸이라 시각적으로 무관)
                cv.lastSymId = finalCell.sym.id;
            }
            else
            {
                yield return UiTween.ScaleRoutine(slot.rt, Vector3.one, Vector3.zero, TransformPopHalfDuration, UiTween.Ease.OutQuad);
                if (slot.rt == null) yield break;
                SetSlotSymbol(slot, finalCell.sym, finalCell.tag); // 스케일0(비가시) 상태에서 최종 심볼로 교체
                slot.rt.localScale = Vector3.zero; // SetSlotSymbol이 내부적으로 1로 되돌리므로 팝업 전 다시 0으로
                cv.lastSymId = finalCell.sym.id;
                yield return UiTween.ScaleRoutine(slot.rt, Vector3.zero, Vector3.one, TransformPopHalfDuration, UiTween.Ease.OutBack);
            }
        }

        // 폭탄 칸 자신도 0.94→1 살짝 펀치(터뜨린 주체 강조, 설계 명시).
        private IEnumerator BombSourcePunchRoutine(CellView cv, float startDelay)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            var slot = cv?.slots != null && cv.slots.Length > 2 ? cv.slots[2] : null;
            if (slot?.rt == null) yield break;
            // ScaleRoutine은 from을 즉시 대입하지 않고 첫 프레임부터 보간한다 — 시작 스케일을 명시
            // 대입해야 0.94 수축이 실제로 보인다(LandSquashRoutine과 동일 조치, Opus S16 검수 경미-1).
            slot.rt.localScale = Vector3.one * BombSourcePunchScale;
            yield return UiTween.ScaleRoutine(slot.rt, Vector3.one * BombSourcePunchScale, Vector3.one, BombSourcePunchDuration, UiTween.Ease.OutBack);
        }

        // S14 §C — 결과 연출 단계별(2/3/4/5매치+잭팟). 잭팟(전칸)이 최우선이고, 그 다음 4/3매치
        // 순서로 배타적으로 처리한다(2매치는 3+가 아닐 때만 — TryPairAccent가 자체 가드).
        private IEnumerator PostRevealFx(SpinResult result, int bigWinTier = 0)
        {
            bool jackpot = !string.IsNullOrEmpty(result.jackpotSym);
            int matchCount = result.bestSetCount;
            bool hasSet = !string.IsNullOrEmpty(result.bestSetId) && matchCount >= 3;

            // set3(및 그 상위인 set4/잭팟) 전부 해당 셀 글로우가 깔린다 — 잭팟은 사실상 전칸이라
            // bestSetId==jackpotSym(전 칸 동일 심볼)이므로 GlowMatchingCells 한 번으로 전칸이 켜진다.
            if (hasSet)
            {
                // 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목①, Opus 2차검수 권장④) —
                // count>=2인 모든 값심볼 그룹이 각각 세트 보너스를 받으므로(SpinResult.setIds), 대표
                // 그룹(bestSetId) 하나만 켜던 걸 setIds 전체로 확장한다(각 그룹은 서로 다른 심볼 id라
                // GlowMatchingCells를 여러 번 불러도 칸이 겹치지 않는다 — 세트4/잭팟 연출(아래
                // shake/컨버지)은 여전히 대표 그룹(bestSetId/matchCount) 기준 그대로). 그룹별 글로우
                // 세기는 웹 ui.js:397 `lvl(n) = max(2, min(5, n))` 그대로 GroupGlowLevel로 클램프해
                // 넘긴다(2매치=1회 발광 그대로, 3~5매치는 반복재생으로 강도 escalation).
                if (result.setIds != null && result.setIds.Count > 0)
                {
                    for (int gi = 0; gi < result.setIds.Count; gi++)
                    {
                        var gid = result.setIds[gi];
                        GlowMatchingCells(gid, GroupGlowLevel(result, gid));
                    }
                }
                else
                {
                    GlowMatchingCells(result.bestSetId, GroupGlowLevel(result, result.bestSetId));
                }
                // S15 §B 표 "세트 3매치 ⑤"(4매치·잭팟도 "위 +" 누적) — 화면 하단(잭팟의 "가장자리"는
                // 겸용, FxPrefabGen.Build_RisingLight 주석 참조)에서 상승 광입자.
                FxKit.I?.Play(FxId.RisingLight, reelRow);
            }
            else TryPairAccent(result);

            // 웹 파리티 P5(웹 ui.js:368-369 그대로) — 잭팟이면 잭팟음만, 아니면 bestCount>=3(세트
            // 성립, matchCount 3/4 둘 다 hasSet==true라 한 번에 커버)일 때만 win. 2매치 이하는 무음
            // (TryPairAccent는 시각 연출만 — 웹도 2매치엔 sfx가 없다).
            // 재미 개편 F1: 빅윈 티어가 있으면 티어 사운드가 일반 승리음을 대체한다(웹 doSpin 분기 — 잭팟이 최우선이라 중복 금지).
            if (jackpot) SoundKit.Sfx("jackpot");
            else if (bigWinTier > 0) SoundKit.Sfx("bigwin" + Mathf.Clamp(bigWinTier, 1, 3));
            else if (hasSet) SoundKit.Sfx("win");

            if (jackpot)
            {
                FxKit.I?.Play(FxId.Jackpot, reelRow);
                FxKit.I?.Play(FxId.JackpotRays, reelRow);
                yield return JackpotSlowMoRoutine();
                yield return FlashAndShake(JackpotShakeAmplitude, JackpotShakeDuration, alpha: 0.18f);
                yield return JackpotBanner();
            }
            else if (matchCount == 4)
            {
                PlayConvergeFx(result.bestSetId);
                yield return FlashAndShake(Set4ShakeAmplitude, Set4ShakeDuration, alpha: Set4FlashAlpha);
            }
            else if (matchCount == 3)
            {
                yield return FlashAndShake(Set3ShakeAmplitude, Set3ShakeDuration, alpha: Set3FlashAlpha);
            }

            if (result.skulls > 0) PlaySkullFx(result.cells);

            // 재미 개편 F1: 빅윈 티어별 셰이크·플래시·파티클·진동 — 세트 연출 위에 얹되 입력을 더 막지 않도록 비동기로 발사한다.
            // 잭팟은 위에서 이미 최상위 연출을 했으므로 건너뛴다(중복 금지). 마지막에 호출해 해골 셰이크보다 우선한다.
            if (!jackpot && bigWinTier > 0) PlayBigWinImpact(bigWinTier);
        }

        // ── F1 빅윈 임팩트(티어 1=NICE 2=BIG 3=MEGA) — 웹 BIGWIN 표(shake sm/bg/xl, 파티클 10/24/48, vib 18/[30,40,50]/[40,40,60,40,140]) ──
        private static readonly int[] BigWinVibe1 = { 18 };
        private static readonly int[] BigWinVibe2 = { 30, 40, 50 };
        private static readonly int[] BigWinVibe3 = { 40, 40, 60, 40, 140 };
        private static readonly int[] ConfettiByBigWinTier = { 0, 2, 4 };   // index = tier-1 (NICE 는 CoinSpark 만)
        private const float BigWinFlashAlphaBig = 0.08f;
        private const float BigWinFlashAlphaMega = 0.18f;

        private void PlayBigWinImpact(int tier)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            PlayClearShake(tier == 3 ? 5 : tier == 2 ? 3 : 1);   // 클리어 등급 셰이크 눈금 재사용: sm / bg / xl(+2차)
            if (flashOverlay != null && tier >= 2) StartCoroutine(FlashRoutine(tier == 3 ? BigWinFlashAlphaMega : BigWinFlashAlphaBig));
            FxKit.I?.Play(FxId.RisingLight, reelRow);
            if (tier == 1) FxKit.I?.Play(FxId.CoinSpark, reelRow);
            int bursts = ConfettiByBigWinTier[tier - 1];
            if (bursts > 0) StartCoroutine(BigWinConfettiRoutine(bursts));
            StartCoroutine(FunHaptics.Routine(tier == 3 ? BigWinVibe3 : tier == 2 ? BigWinVibe2 : BigWinVibe1));
        }

        private IEnumerator BigWinConfettiRoutine(int bursts)
        {
            for (int i = 0; i < bursts; i++)
            {
                FxKit.I?.Play(FxId.Clear, reelRow);
                if (i < bursts - 1) yield return new WaitForSeconds(0.08f);
            }
        }

        // S14 §C — 잭팟 슬로모(§G "1곳만 + try/finally 복구"). 지속시간(0.25s)은 실시간
        // (WaitForSecondsRealtime) 기준 — Time.timeScale이 걸린 채로 스케일된 대기를 쓰면 실제
        // 경과시간이 0.25/0.35≈0.71s로 늘어나 §G "입력 차단 최대 0.6s" 규칙을 넘어선다.
        private IEnumerator JackpotSlowMoRoutine()
        {
            float prevScale = Time.timeScale;
            try
            {
                Time.timeScale = JackpotSlowMoScale;
                yield return new WaitForSecondsRealtime(JackpotSlowMoDuration);
            }
            finally
            {
                Time.timeScale = prevScale;
            }
        }

        // S14 §C / S15 §B 표 "세트 2매치" — 정식 세트(3+)엔 못 미치지만 같은 심볼이 정확히 2번 나온
        // 칸에 골드 테두리 펄스 + 레이어드 파티클(FxId.Match2: 심볼색 파편12+링1+별4)을 함께 낸다.
        // 이전엔 Outline 펄스만 있어 "Outline 단독 사용" 위반이었다 — 이번 슬라이스에서 파티클을 항상
        // 함께 재생하도록 고쳤다(설계 "Outline/플래시 단독 사용 금지").
        // 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목①, Opus 2차검수 권장④) — 이 메서드는
        // PostRevealFx의 "hasSet==false"(bestCount<3) 분기에서만 불린다. 그 조건에서는 setIds에 담긴
        // 모든 그룹이 반드시 count==2다(bestCount가 전체 최댓값인데 <3 이므로 다른 그룹도 전부 <=2,
        // setIds는 count>=2만 담으므로 정확히 2로 확정) — 구현을 `FindDominantValueSymbol`로 대표
        // 그룹 1개만 뽑던 것에서 setIds 전체 순회로 바꿔, 두 값심볼 그룹이 동시에 2세트를 이룬
        // 케이스에서 양쪽 다 연출되게 한다(세트 보너스 지급도 P8부터 양쪽 다 받으므로 시각 연출도
        // 동일 원칙 — 지급과 연출의 불일치를 없앤다).
        private void TryPairAccent(SpinResult result)
        {
            if (result.setIds != null && result.setIds.Count > 0)
            {
                for (int gi = 0; gi < result.setIds.Count; gi++)
                {
                    var gid = result.setIds[gi];
                    int cnt = (result.counts != null && result.counts.TryGetValue(gid, out var c)) ? c : 0;
                    if (cnt != 2) continue; // 방어적 재확인(이 분기에서는 항상 참이어야 함)
                    PairAccentCells(gid);
                }
                return;
            }
            // 방어적 폴백(setIds 미제공 — 정상 경로에선 도달 불가, 구 대표-그룹 로직 유지).
            FindDominantValueSymbol(result, result.cells.Count, out string bestId, out int bestCount);
            if (bestCount != 2 || bestId == null) return;
            PairAccentCells(bestId);
        }

        private void PairAccentCells(string symId)
        {
            Color tint = SymbolTintById.TryGetValue(symId, out var symTint) ? symTint : Color.white;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cv = _cells[i];
                if (cv.glow == null || cv.lastSymId != symId) continue;
                cv.glow.enabled = true;
                cv.glow.effectColor = UiKit.Accent;
                StartCoroutine(PulseOutline(cv.glow));
                FxKit.I?.Play(FxId.Match2, cv.rt, tint);
            }
        }

        // S14 §C — 4매치: 매치된 각 셀에서 중앙 셀로 에너지가 수렴하는 느낌(fx_converge, PlayFlyTo).
        // S15 §B 표 "4매치 ⑥⑦" — 셀당 8개(매치 셀 수에 따라 합산 약 24~32개, 설계 "30개" 근사) +
        // 중앙 도착 시 폭발 링 2연발(arrivalBurst=ConvergeBurst가 FlyToRoutine 안에서 정확한 도착
        // 좌표에 자동 재생 — FxKit.cs 주석 참조).
        private void PlayConvergeFx(string symId)
        {
            if (string.IsNullOrEmpty(symId) || _cells.Count == 0) return;
            var target = _cells[_cells.Count / 2].rt;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cv = _cells[i];
                if (cv.lastSymId != symId || cv.rt == target) continue;
                FxKit.I?.PlayFlyTo(FxId.Converge, cv.rt, target, 8, arrivalBurst: FxId.ConvergeBurst);
            }
        }

        // 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목①, Opus 2차검수 권장④, 웹 ui.js:
        // 397-411 glowMatches의 `lvl(n) = max(2, min(5, n))`) — 그룹 크기(2~5)에 비례해 FxId.SetHit를
        // 반복재생한다(NodePanel.PlayConfettiEscalation과 동일 "고티어일수록 화려하다" 근사 원칙 —
        // 웹처럼 CSS 클래스/파티클 크기를 직접 바꾸는 정밀 1:1 이식이 아니라 반복재생으로 체감만
        // 재현). level==2(최솟값, 기존 호출부 전부의 기본값)는 1회만 재생해 무회귀.
        private static readonly int[] GlowBurstsByLevel = { 1, 2, 3, 4 }; // index=level-2(레벨 2~5)
        private const float GlowBurstStagger = 0.06f;

        private void GlowMatchingCells(string symId, int level = 2)
        {
            if (string.IsNullOrEmpty(symId)) return;
            // S15 §B — fx_set_hit의 파편 레이어는 심볼색 틴트 전제(SpinStop과 동일 팔레트 규약).
            Color tint = SymbolTintById.TryGetValue(symId, out var symTint) ? symTint : Color.white;
            int bursts = GlowBurstsByLevel[Mathf.Clamp(level, 2, 5) - 2];
            for (int i = 0; i < _cells.Count; i++)
            {
                var cv = _cells[i];
                if (cv.glow == null || cv.lastSymId != symId) continue;
                cv.glow.enabled = true;
                cv.glow.effectColor = UiKit.Accent;
                StartCoroutine(PulseOutline(cv.glow));
                StartCoroutine(GlowBurstEscalationRoutine(cv.rt, tint, bursts));
            }
        }

        private IEnumerator GlowBurstEscalationRoutine(RectTransform target, Color tint, int bursts)
        {
            for (int i = 0; i < bursts; i++)
            {
                FxKit.I?.Play(FxId.SetHit, target, tint);
                if (i < bursts - 1) yield return new WaitForSeconds(GlowBurstStagger);
            }
        }

        // 그룹 표시 크기(웹 ui.js:398 `grpSize`) → 2~5 레벨 클램프. Unity `result.counts[id]`는
        // `SpinResolver.Evaluate`가 이미 와일드를 bestSetId 그룹에 병합해 둔 최종값이라(counts[bestId]
        // += totalWilds) 이 함수에서 별도로 와일드를 다시 더할 필요가 없다(웹은 raw 셀 스캔에서
        // 와일드를 제외하고 집계해 UI 레이어가 bestSetId일 때만 +wilds 하지만, Unity는 엔진 산출
        // counts를 그대로 읽으면 이미 동일한 값이 된다).
        private static int GroupGlowLevel(SpinResult result, string id)
        {
            if (string.IsNullOrEmpty(id) || result?.counts == null) return 2;
            int n = result.counts.TryGetValue(id, out var c) ? c : 2;
            return Mathf.Clamp(n, 2, 5);
        }

        private IEnumerator PulseOutline(Outline outline)
        {
            if (outline == null) yield break;
            var c = outline.effectColor;
            c.a = 0f;
            outline.effectColor = c;
            yield return UiTween.FloatRoutine(0f, 1f, SetGlowPulseDuration * 0.5f,
                a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
            if (outline == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0.7f, SetGlowPulseDuration * 0.5f,
                a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
        }

        private static void SetOutlineAlpha(Outline outline, float a)
        {
            if (outline == null) return;
            var c = outline.effectColor;
            c.a = a;
            outline.effectColor = c;
        }

        // S14 §G — shakeAmplitude>0이면 runScreenRoot를 흔든다(reelRow/셀 개별 셰이크 금지).
        private IEnumerator FlashAndShake(float shakeAmplitude, float shakeDuration, float alpha)
        {
            if (flashOverlay != null) StartCoroutine(FlashRoutine(alpha));
            if (shakeAmplitude > 0f && runScreenRoot != null)
            {
                PlayScreenShake(shakeAmplitude, shakeDuration);
                yield return new WaitForSeconds(shakeDuration);
            }
            else
            {
                yield return new WaitForSeconds(Set4FlashDuration);
            }
        }

        private IEnumerator FlashRoutine(float alpha)
        {
            yield return UiTween.FadeRoutine(flashOverlay, 0f, alpha, Set4FlashDuration * 0.5f);
            if (flashOverlay == null) yield break;
            yield return UiTween.FadeRoutine(flashOverlay, alpha, 0f, Set4FlashDuration * 0.5f);
        }

        private IEnumerator JackpotBanner()
        {
            if (jackpotBannerGroup == null) yield break;
            if (jackpotBannerRect != null) jackpotBannerRect.anchoredPosition = new Vector2(jackpotBannerRect.anchoredPosition.x, 60f);
            yield return UiTween.FadeRoutine(jackpotBannerGroup, 0f, 1f, JackpotBannerFade);
            if (jackpotBannerRect != null)
                yield return UiTween.MoveRoutine(jackpotBannerRect, jackpotBannerRect.anchoredPosition,
                    new Vector2(jackpotBannerRect.anchoredPosition.x, 0f), 0.2f, UiTween.Ease.OutBack);
            yield return new WaitForSeconds(JackpotBannerHold);
            yield return UiTween.FadeRoutine(jackpotBannerGroup, 1f, 0f, JackpotBannerFade);
        }

        private void PlaySkullFx(List<Cell> cells)
        {
            for (int i = 0; i < _cells.Count && i < cells.Count; i++)
            {
                if (cells[i]?.sym == null || cells[i].sym.id != "skull") continue;
                StartCoroutine(SkullCellFx(_cells[i]));
                FxKit.I?.Play(FxId.Skull, _cells[i].rt);
            }
        }

        // S14 §G — 셀 개별이 아니라 runScreenRoot를 흔든다(이전엔 cv.rt 개별 셰이크였다 — 규칙 준수를
        // 위한 재해석/행동 변경, 보고 대상).
        private IEnumerator SkullCellFx(CellView cv)
        {
            if (cv?.rt == null) yield break;
            var icon = cv.CenterIcon;
            var baseColor = icon != null ? icon.color : Color.white;
            if (icon != null) StartCoroutine(TintRoutine(icon, baseColor, UiKit.Bad, SkullTintDuration));
            PlayScreenShake(SkullShakeAmplitude, SkullShakeDuration);
            yield return new WaitForSeconds(SkullShakeDuration);
        }

        private IEnumerator TintRoutine(Image img, Color baseColor, Color tint, float duration)
        {
            if (img == null) yield break;
            yield return UiTween.FloatRoutine(0f, 1f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
            if (img == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
        }

        // ══════════════════════════════════════════════════════════════════════
        // F4 홀드 표시 — 칸 위 오버레이(청록 틴트 + 프레임 발광 + 자물쇠 배지). 칸(RectMask2D) 안쪽에 얹는 자식이라 마스크 안에서만
        // 그려진다. 전부 코드 생성 도형(ProcSprites)이라 씬 리빌드가 필요 없고 astral 이모지(🔒)도 쓰지 않는다.
        // ══════════════════════════════════════════════════════════════════════
        private const float HoldBadgeSize = 44f;
        private const float HoldLockIconSize = 30f;
        private const float HoldKeepFadeDuration = 0.7f;   // 웹 holdKeep 700ms
        private static readonly Color HoldLockColor = new Color(0.72f, 1f, 0.95f);

        // 칸이 처음 홀드될 때 한 번만 만든다(이미 있으면 그대로).
        private static void EnsureHoldOverlay(CellView cv)
        {
            if (cv?.rt == null || cv.holdGroup != null) return;
            var go = new GameObject("HoldOverlay", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cv.rt, false);
            UiKit.Fill(rt);
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var teal = UiKit.Good;
            var tint = UiKit.Panel(rt, "Tint", new Color(teal.r, teal.g, teal.b, 0.17f));
            UiKit.Fill(tint);
            tint.GetComponent<Image>().raycastTarget = false;

            var frame = UiKit.Panel(rt, "Frame", teal, ProcSprites.FrameGlow());
            UiKit.Fill(frame);
            frame.GetComponent<Image>().raycastTarget = false;

            var badge = UiKit.Panel(rt, "Badge", new Color(0.02f, 0.14f, 0.16f, 0.92f), ProcSprites.Disc());
            var badgeImg = badge.GetComponent<Image>();
            badgeImg.type = Image.Type.Simple;
            badgeImg.raycastTarget = false;
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 1f);
            badge.pivot = new Vector2(0.5f, 1f);
            badge.sizeDelta = new Vector2(HoldBadgeSize, HoldBadgeSize);
            badge.anchoredPosition = new Vector2(0f, -6f);
            var badgeRing = badge.gameObject.AddComponent<Outline>();
            badgeRing.effectColor = new Color(teal.r, teal.g, teal.b, 0.9f);
            badgeRing.effectDistance = new Vector2(1.5f, -1.5f);

            var lockImg = UiKit.Image(badge, ProcSprites.Lock(), HoldLockColor);
            lockImg.name = "Lock";
            lockImg.raycastTarget = false;
            var lrt = lockImg.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(HoldLockIconSize, HoldLockIconSize);
            lrt.anchoredPosition = Vector2.zero;

            cv.holdGroup = cg;
            cv.holdBadge = badge;
        }

        /// <summary>현재 홀드된 칸 인덱스로 오버레이를 맞춘다(null/빈 목록이면 전부 끈다). 새로 켜지는 칸은 자물쇠가 팝한다.</summary>
        public void SetHeld(IReadOnlyList<int> held)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                bool on = false;
                if (held != null)
                    for (int k = 0; k < held.Count; k++)
                        if (held[k] == i) { on = true; break; }
                SetHoldVisual(_cells[i], on, pop: true);
            }
        }

        private void SetHoldVisual(CellView cv, bool on, bool pop)
        {
            if (cv == null || cv.holdOn == on) return;
            if (on) EnsureHoldOverlay(cv);
            cv.holdOn = on;
            cv.holdGen++;   // 진행 중인 팝/페이드는 다음 프레임에 세대 불일치로 스스로 끝난다
            if (cv.holdFade != null) { StopCoroutine(cv.holdFade); cv.holdFade = null; }
            if (cv.holdGroup == null) return;   // 한 번도 홀드된 적 없는 칸 — 끄기는 할 일이 없다
            cv.holdGroup.alpha = on ? 1f : 0f;
            if (cv.holdBadge != null) cv.holdBadge.localScale = Vector3.one;
            if (on && pop && cv.holdBadge != null && isActiveAndEnabled)
                cv.holdFade = StartCoroutine(HoldPopRoutine(cv, cv.holdGen));
        }

        // 자물쇠 배지 팝(0.2→1 OutBack 0.3s) — 단일 루프 + 세대 확인, 끝에서 최종 스케일 1 확정.
        private IEnumerator HoldPopRoutine(CellView cv, int gen)
        {
            const float dur = 0.3f;
            float t = 0f;
            while (t < dur)
            {
                if (gen != cv.holdGen || cv.holdBadge == null) yield break;
                t += Time.deltaTime;
                cv.holdBadge.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, UiTween.Apply(UiTween.Ease.OutBack, t / dur));
                yield return null;
            }
            if (gen != cv.holdGen) yield break;
            if (cv.holdBadge != null) cv.holdBadge.localScale = Vector3.one;
            cv.holdFade = null;
        }

        // 스핀 후 홀드 표시 해제 — 번쩍이며(0.7s) 사라진다(웹 .reel.hold-keep). holdOn 은 즉시 꺼진 것으로 취급해
        // 곧이어 오는 SetHeld(빈 목록)가 페이드를 끊지 않게 한다.
        private void ReleaseHeldCell(CellView cv)
        {
            if (cv?.holdGroup == null || !cv.holdOn) return;
            cv.holdOn = false;
            cv.holdGen++;
            if (cv.holdFade != null) StopCoroutine(cv.holdFade);
            cv.holdGroup.alpha = 1f;
            if (cv.holdBadge != null) cv.holdBadge.localScale = Vector3.one;
            cv.holdFade = StartCoroutine(HoldFadeRoutine(cv, cv.holdGen));
        }

        private IEnumerator HoldFadeRoutine(CellView cv, int gen)
        {
            float t = 0f;
            while (t < HoldKeepFadeDuration)
            {
                if (gen != cv.holdGen || cv.holdGroup == null) yield break;
                t += Time.deltaTime;
                cv.holdGroup.alpha = 1f - UiTween.Apply(UiTween.Ease.OutQuad, t / HoldKeepFadeDuration);
                yield return null;
            }
            if (gen != cv.holdGen) yield break;
            if (cv.holdGroup != null) cv.holdGroup.alpha = 0f;   // 끄기 경로 최종값 확정
            cv.holdFade = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // F5 피버 중 릴 프레임 — 금빛 발광 프레임(ProcSprites.FrameGlow, 섹션 자식 · 셀 뒤)이 맥동하고 FxKit 불꽃 루프(fx_boss 를
        // 금빛으로 틴트)가 릴 가장자리에서 오른다. 피버 상태는 RunView 가 RunController.Fever 로 판단해 넘긴다.
        // ══════════════════════════════════════════════════════════════════════
        private static readonly Color FeverFrameGold = new Color(1f, 0.72f, 0.16f, 1f);
        private static readonly Color FeverFrameOrange = new Color(1f, 0.45f, 0.05f, 1f);
        private static readonly Color FeverFxTint = new Color(1f, 0.72f, 0.18f, 0.7f);
        private const float FeverFramePad = 12f;

        private RectTransform _feverFrame;
        private Image _feverFrameImg;
        private Coroutine _feverPulse;
        private int _feverGen;   // 2차 검수 M1 — 피버 프레임 펄스 세대 토큰
        private ParticleSystem _feverFx;
        private bool _feverOn;

        public void SetFeverFrame(bool on)
        {
            if (on == _feverOn) return;
            _feverOn = on;
            if (on)
            {
                EnsureFeverFrame();
                if (_feverFrame != null)
                {
                    _feverFrame.gameObject.SetActive(true);
                    _feverGen++;
                    if (_feverPulse != null) StopCoroutine(_feverPulse);
                    if (isActiveAndEnabled) _feverPulse = StartCoroutine(FeverFramePulse(_feverGen));
                }
                if (_feverFx == null) _feverFx = FxKit.I?.PlayLoop(FxId.Boss, reelRow, FeverFxTint);
            }
            else ResetFeverFrame();
        }

        /// <summary>피버 발동 순간(FEVER! 배너와 함께) — 셰이크 + 금빛 상승 광입자/색종이. 사운드·배너·진동은 RunView 몫이다.</summary>
        public void PlayFeverTriggerFx()
        {
            PlayClearShake(3);   // bg 눈금(웹 shake("bg"))
            FxKit.I?.Play(FxId.RisingLight, reelRow, FeverFxTint);
            StartCoroutine(FeverBurstRoutine());
        }

        private IEnumerator FeverBurstRoutine()
        {
            yield return new WaitForSeconds(0.12f);
            FxKit.I?.Play(FxId.Clear, reelRow, FeverFxTint);
        }

        private void ResetFeverFrame()
        {
            _feverOn = false;
            _feverGen++;
            if (_feverPulse != null) { StopCoroutine(_feverPulse); _feverPulse = null; }
            if (_feverFrame != null) _feverFrame.gameObject.SetActive(false);
            if (_feverFx != null) { FxKit.I?.StopLoop(_feverFx); _feverFx = null; }
        }

        private void OnDisable()
        {
            // 화면을 떠날 때 루프 파티클을 남기지 않는다(코루틴은 비활성화와 함께 멈춘다).
            ResetFeverFrame();
        }

        private void EnsureFeverFrame()
        {
            if (_feverFrame != null) return;
            var go = new GameObject("FeverFrame", typeof(RectTransform), typeof(Image));
            _feverFrame = (RectTransform)go.transform;
            _feverFrame.SetParent(transform, false);
            _feverFrame.SetAsFirstSibling();   // 셀(reelRow) 뒤 — 셀 사이/바깥으로 번져 보이는 발광
            // 가로로 늘어나는(stretch-x) 앵커 + 세로 고정 높이: sizeDelta.x 는 "부모 폭 - 값"이라 음수로 좌우 여백을 준다.
            _feverFrame.anchorMin = new Vector2(0f, 0.5f);
            _feverFrame.anchorMax = new Vector2(1f, 0.5f);
            _feverFrame.pivot = new Vector2(0.5f, 0.5f);
            _feverFrame.sizeDelta = new Vector2(-FeverFramePad * 2f, UiKit.ReelCellSize + FeverFramePad * 2f);
            _feverFrame.anchoredPosition = Vector2.zero;
            _feverFrameImg = go.GetComponent<Image>();
            _feverFrameImg.sprite = ProcSprites.FrameGlow();
            _feverFrameImg.type = Image.Type.Sliced;
            _feverFrameImg.color = FeverFrameGold;
            _feverFrameImg.raycastTarget = false;
        }

        // 금빛↔주황 맥동(0.5s 반주기)을 단일 루프로 돌린다 — 세대가 바뀌면(꺼짐/재켜짐) 값을 건드리지 않고 끝난다.
        private IEnumerator FeverFramePulse(int gen)
        {
            float phase = 0f;
            while (_feverFrameImg != null)
            {
                if (gen != _feverGen) yield break;
                phase += Time.deltaTime;
                ApplyFeverFrame(Mathf.PingPong(phase / 0.5f, 1f));
                yield return null;
            }
        }

        private void ApplyFeverFrame(float k)
        {
            if (_feverFrameImg == null) return;
            var c = Color.Lerp(FeverFrameOrange, FeverFrameGold, k);
            c.a = Mathf.Lerp(0.6f, 1f, k);
            _feverFrameImg.color = c;
        }

        // ── 셀 구성 ──────────────────────────────────────────────────────────────────
        private void EnsureCellCount(int count)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
                if (_cells[i].rt != null) Destroy(_cells[i].rt.gameObject);
            _cells.Clear();

            if (reelRow == null || cellTemplate == null) return;
            for (int i = 0; i < count; i++)
            {
                var inst = Instantiate(cellTemplate, reelRow);
                inst.gameObject.SetActive(true);
                inst.name = "Cell_" + i;
                inst.localScale = Vector3.one;

                var stripRt = inst.Find("Strip") as RectTransform;
                var streakImg = inst.Find("Streak")?.GetComponent<Image>();
                var slots = new SlotView[5];
                for (int k = 0; k < 5; k++)
                {
                    var slotRt = stripRt != null ? stripRt.Find("Slot" + k) as RectTransform : null;
                    var slot = new SlotView
                    {
                        rt = slotRt,
                        icon = slotRt != null ? slotRt.Find("Icon")?.GetComponent<Image>() : null,
                        tag = slotRt != null ? slotRt.Find("Tag")?.GetComponent<Text>() : null,
                    };
                    SetSlotSymbol(slot, RandomSymbol(), ""); // 첫 프레임 임시 채움(즉시 스크롤이 덮어씀)
                    slots[k] = slot;
                }

                // BuildReelCellTemplate이 Outline을 2개 순서대로 붙인다(0=상시 테두리, 1=매치 글로우).
                var outlines = inst.GetComponents<Outline>();
                var cv = new CellView
                {
                    rt = inst,
                    strip = stripRt,
                    slots = slots,
                    streak = streakImg,
                    glow = outlines.Length > 1 ? outlines[1] : (outlines.Length > 0 ? outlines[0] : null),
                };
                if (cv.glow != null) cv.glow.enabled = false;
                if (cv.streak != null)
                {
                    var c = cv.streak.color;
                    c.a = 0f;
                    cv.streak.color = c;
                }
                // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 탭. 인덱스는 클로저로 고정(i는 루프
                // 변수라 캡처 전 로컬 복사 필요). 결과가 없는 칸(대기 상태/직전 스핀 정보 없음)은
                // CellInfoView.Build가 null을 반환하므로 핸들러 쪽에서 조용히 무시된다.
                int cellIdx = i;
                // 재미 개편 F4 — 탭/롱프레스 구분(CellPressHandler). 기존 Button.onClick 탭 리스너는 중복 호출을 막기 위해 비운다
                // (Button 자체는 그대로 둔다 — 포인터 이벤트를 받는 Selectable 로서 해가 없다).
                var button = inst.GetComponent<Button>();
                if (button != null) button.onClick.RemoveAllListeners();
                var press = inst.gameObject.GetComponent<CellPressHandler>();
                if (press == null) press = inst.gameObject.AddComponent<CellPressHandler>();
                press.Tap = () => _cellTapHandler?.Invoke(cellIdx);
                press.LongPress = () => _cellLongPressHandler?.Invoke(cellIdx);
                // 홀드 오버레이는 칸이 처음 홀드될 때 지연 생성한다(EnsureHoldOverlay) — 스핀마다 칸을 새로 만드는 구조라 매번 미리 만들면 낭비.
                _cells.Add(cv);
            }
        }

        private void SetSlotSymbol(SlotView slot, SymInfo sym, string tag)
        {
            if (slot == null || sym == null) return;
            if (slot.icon != null)
            {
                slot.icon.sprite = SymbolSpriteOf(sym.id);
                slot.icon.color = Color.white;
                slot.icon.enabled = slot.icon.sprite != null;
            }
            if (slot.rt != null) slot.rt.localScale = Vector3.one;
            if (slot.tag != null) slot.tag.text = TranslateTag(tag);
        }

        // FUN_OVERHAUL_DESIGN §3 — 심볼 스프라이트 해석 순서:
        //   ① Resources/JackpotRun/Sprites/Symbols/sym_<id>.png(신규 광택 아이콘, 투명 배경) — 씬을 다시
        //      굽지 않아도(▶ Play 만으로) 새 그림이 보이도록 런타임에 지연 로드한다.
        //   ② 빌더가 씬에 구워 둔 도형 타일(_spriteById, 구 Art/UI/sym_*.png) — 신규 그림이 없을 때의 폴백.
        //   ③ 둘 다 없으면 null(호출부가 icon.enabled=false 로 빈칸 처리).
        // 신규 그림이 구운 타일보다 우선하는 이유: 구워진 14종은 옛 도형 타일이라, 리빌드 전에도 새 아트가
        // 이겨야 한다(리빌드 후엔 빌더가 같은 순서로 굽는다 — UiSceneBuilder.LoadSymbolArt).
        private Sprite SymbolSpriteOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_resolvedById == null) _resolvedById = new Dictionary<string, Sprite>();
            if (_resolvedById.TryGetValue(id, out var cached)) return cached;

            var sp = JackpotCatalog.LoadSymbolSprite(id);
            if (sp == null && _spriteById != null) _spriteById.TryGetValue(id, out sp);
            _resolvedById[id] = sp;
            return sp;
        }

        private static void CopySlot(SlotView dest, SlotView src)
        {
            if (dest == null || src == null) return;
            if (dest.icon != null && src.icon != null)
            {
                dest.icon.sprite = src.icon.sprite;
                dest.icon.enabled = src.icon.enabled;
                dest.icon.color = Color.white; // 스크롤 중엔 항상 정상 밝기(디밍은 정지 후에만 적용)
            }
            if (dest.rt != null) dest.rt.localScale = Vector3.one;
            if (dest.tag != null && src.tag != null) dest.tag.text = src.tag.text;
        }

        private static string TranslateTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "";
            return TagTranslate.TryGetValue(tag, out var safe) ? safe : tag;
        }

        // Opus 2차검수(P7-1, 2026-08-09) [HIGH]① — 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19)로
        // `Symbols.All`이 14→72종으로 늘면서, 신규 58종(심화모드 주머니 전용, 전부 weight=0)은
        // `unity-assets/manifest.json`에 스프라이트 자산 자체가 없다(P7-2/3/4 이후 아트 파이프라인
        // 별도 요청 대상). 이 필러는 일반 릴의 "스핀 중 지나가는 임시 심볼"·니어미스·첫 프레임
        // 채움용이라 신규 58종이 섞이면 ~80%(58/72) 확률로 스프라이트 미스 → 빈 사각형이 보이는
        // 회귀가 생긴다. 릴에 실제로 등장 가능한 심볼(weight>0인 원본 10종 + 머신/퍽이 주입하는
        // 휴면 4종)은 전부 `Symbols.LegacyCount`(14) 안에 있으므로 그 범위로만 필러를 고른다.
        private static SymInfo RandomSymbol()
        {
            var syms = Symbols.All;
            return syms[UnityEngine.Random.Range(0, Symbols.LegacyCount)];
        }

        private static Color HexColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
