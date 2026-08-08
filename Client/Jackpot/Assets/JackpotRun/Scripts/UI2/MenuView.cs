using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 메인 메뉴 화면 = 웹 단독판 renderHome 이식 — ENGINE_PORT_DESIGN.md S12 §4 + 웹 파리티 P4
    // (WEB_PARITY_DESIGN.md §1-A #15 A). scr-title(타이틀+부제) → **레벨 카드**(클릭형, 레벨 보상
    // 화면으로) → **게임 모드 선택기**(일반/심화, 심화는 P7 미구현이라 "준비 중" 잠금) → (승천 선택기는
    // P6 미구현이라 렌더 생략 — Editor/UiSceneBuilder.cs BuildMenuScreen 안의 "P4 A.3" 주석 자리 참조)
    // → hud 카드(칭호 +
    // 최고점수/최고스테이지/플레이 3칸) → "업적 n/34 · 장치 n/16 해금" 요약줄 → 게임 시작(골드) +
    // 랭킹/도감(고스트 2개) → 설명 2줄 → **데이터 초기화**(신규). 레이아웃은 UiSceneBuilder가 정적으로
    // 짓고 [SerializeField]로 이 컴포넌트에 와이어링한다 — 이 클래스는 "화면을 열 때마다 최신 프로필로
    // 갱신"만 담당한다(런타임 코드생성 없음).
    //
    // 이관 메모(S12a): 이전 슬라이스(S7~S10)의 캐러셀·"@닉네임/닉네임 변경" 행은 웹 renderHome에
    // 대응 요소가 없어 제거했다(설계 "임의 변경 금지" — 스펙에 없는 요소를 계속 얹는 것도 스펙 위반
    // 이라 판단). 그 결과 메뉴에서 LoginView로 돌아갈 경로가 사라졌다 — 웹도 별도 설정(⚙️) 진입점을
    // 쓰므로 원본에 맞는 동작이지만, Unity에는 그 설정 화면이 아직 없다(Fable 보고 대상 — 후속 슬라이스
    // 필요 여부 판단).
    //
    // 랭킹 버튼은 S15에서 실제 화면(RankView)으로 연결됐다(§4 그대로 버튼 유지, 토스트 안내는 제거).
    //
    // P4 A.2/A.3 범위 메모: 웹 소리 토글(`soundToggle`)은 P5(사운드) 예약 — 이번 슬라이스는 버튼 자체를
    // 짓지 않는다(빈 자리도 만들지 않음, 작업 지시 "주석으로 예약" 그대로). 승천 선택기(`ascSelector`)는
    // 웹도 `ascUnlocked()`(profile.ascMax>=0, 승천 1회 이상 졸업)가 false면 아예 렌더하지 않는데, 승천
    // 자체가 P6 미구현이라 Unity는 항상 이 조건이 거짓이다 — 그래서 지금은 렌더 자체를 생략한다(엔진에
    // ascMax 필드가 없어 조건 판정 코드조차 쓸 수 없음, §1-A #18 P6에서 이 자리를 다시 연다).
    public sealed class MenuView : MonoBehaviour
    {
        private AppRoot appRoot => AppRoot.Instance;

        [SerializeField] private Text hudTitleText;   // hud 칭호(titleOf(bestScore))
        [SerializeField] private Text statScoreValue;  // hud-stats "최고 점수"
        [SerializeField] private Text statStageValue;  // hud-stats "최고 스테이지"
        [SerializeField] private Text statPlaysValue;  // hud-stats "플레이"
        [SerializeField] private Text summaryText;      // "업적 n/34 · 장치 n/16 해금"
        [SerializeField] private Button rankButton;     // 랭킹(S15: RankView로 이동)
        [SerializeField] private RectTransform mainButtonRect; // S13 §E — fx_ui_aura 앵커("▶ 게임 시작" 버튼)

        // ── P4 A.1 — 레벨 카드(웹 lvlCard(lp,true), ui.js:592-602) ─────────────────────────────
        [SerializeField] private Button levelCardButton;
        [SerializeField] private Text levelBadgeText;   // "Lv.N"
        [SerializeField] private Text levelXpText;      // "n / req XP" 또는 "MAX"
        [SerializeField] private RectTransform levelBarFill; // anchorMax.x = ratio
        [SerializeField] private Image levelBarFillImage;

        // ── P4 A.2 — 게임 모드 선택기(웹 deepSelector(), ui.js:559-570) — 심화는 P7 미구현이라 잠금 ──
        [SerializeField] private Button modeDeepButton;

        // ── P4 A.5 — 데이터 초기화(웹 resetAsk/resetConfirm, ui.js:207-210) ────────────────────
        [SerializeField] private Button resetButton;
        [SerializeField] private ConfirmSheetPopup resetConfirmPopup;

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 gearbtn) — 설정 진입점(홈).
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsSheet settingsSheet;

        private ParticleSystem _mainButtonAura;

        private void Awake()
        {
            if (rankButton != null) rankButton.onClick.AddListener(OnRankClicked);
            if (levelCardButton != null) levelCardButton.onClick.AddListener(OnLevelCardClicked);
            if (modeDeepButton != null) modeDeepButton.onClick.AddListener(OnDeepModeClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        // 설정 시트의 "데이터 초기화"도 이 화면의 기존 확인 흐름(OnResetConfirmed)을 그대로 재사용한다
        // (작업 지시 "홈과 동일 확인 시트 재사용").
        private void OnSettingsClicked() => settingsSheet?.Show(OnResetConfirmed);

        private void OnEnable()
        {
            Refresh();
            _mainButtonAura = FxKit.I?.PlayLoop(FxId.UiAura, mainButtonRect);
        }

        private void OnDisable()
        {
            FxKit.I?.StopLoop(_mainButtonAura);
            _mainButtonAura = null;
        }

        private void OnRankClicked()
        {
            appRoot?.ShowRank();
        }

        private void OnLevelCardClicked()
        {
            appRoot?.ShowLevelRewards();
        }

        // 웹 deepSelector()의 두 번째 카드(심화·심볼 덱)는 P7(심화모드 전체, WEB_PARITY_DESIGN.md
        // §1-A #19) 없이는 실제로 전환할 상태가 없다 — 탭하면 "준비 중" 토스트만 안내한다(작업 지시
        // "탭 시 토스트" 그대로). 일반 카드는 항상 선택된 상태로만 표시되므로 별도 클릭 핸들러가 없다.
        private void OnDeepModeClicked()
        {
            appRoot?.Toast?.Show("심화 모드(심볼 덱)는 아직 준비 중이에요.");
        }

        private void OnResetClicked()
        {
            resetConfirmPopup?.Show(
                "데이터 초기화",
                "최고 점수 · 캐릭터/머신/장치 해금 · 도감 발견 기록이 모두 사라지고 처음(초보학생 + 기본)부터 시작합니다.\n랭킹에 등록한 기록은 그대로 유지돼요.",
                "초기화", OnResetConfirmed, "취소");
        }

        private void OnResetConfirmed()
        {
            appRoot?.ResetProfile();
        }

        /// <summary>AppRoot.ResetProfile()이 리셋 직후 호출한다(화면 전환 없이 카드만 새로고침) —
        /// OnEnable 갱신과 동일한 진입점을 공개로 승격했을 뿐 로직 변경은 없다.</summary>
        public void Refresh()
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return;

            if (hudTitleText != null) hudTitleText.text = Formulas.ScoreTitle(profile.BestScore).title;
            if (statScoreValue != null) statScoreValue.text = NumberFormat.Comma(profile.BestScore);
            if (statStageValue != null) statStageValue.text = NumberFormat.Comma(profile.BestStage);
            if (statPlaysValue != null) statPlaysValue.text = NumberFormat.Comma(profile.Runs);
            if (summaryText != null)
            {
                summaryText.text =
                    $"업적 {profile.AchievedIds.Count}/{Achievements.Count} · 장치 {profile.OwnedDevices.Count}/{Devices.Count} 해금";
            }

            RefreshLevelCard(profile);
        }

        private void RefreshLevelCard(PlayerProfile profile)
        {
            var lp = profile.LevelProgress();
            if (levelBadgeText != null) levelBadgeText.text = $"Lv.{lp.Level}";
            if (levelXpText != null)
                levelXpText.text = lp.Max ? "MAX" : $"{NumberFormat.Comma(lp.InLevel)} / {NumberFormat.Comma(lp.Need)} XP";
            if (levelBarFill != null)
            {
                float pct = lp.Max ? 1f : (float)lp.Ratio;
                levelBarFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            }
            // 웹은 MAX 레벨이라고 바 색을 바꾸지 않는다(무색 — 항상 Accent, Opus 2차검수 정리) — 그대로 맞춘다.
            if (levelBarFillImage != null) levelBarFillImage.color = UiKit.Accent;
        }
    }
}
