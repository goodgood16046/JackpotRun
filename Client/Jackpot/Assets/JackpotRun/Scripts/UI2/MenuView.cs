using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 메인 메뉴 화면 = 웹 단독판 renderHome 이식 — ENGINE_PORT_DESIGN.md S12 §4 + 웹 파리티 P4/P7-4
    // (WEB_PARITY_DESIGN.md §1-A #15 A). scr-title(타이틀+부제) → **레벨 카드**(클릭형, 레벨 보상
    // 화면으로) → **게임 모드 선택기**(일반/심화 실토글, 웹 파리티 P7-4부터 해금 없이 항상 노출 —
    // 심화 선택 시 아래 승천 선택기는 숨김, 상호배제) → **승천(심화
    // 학기) 선택기**(웹 파리티 P6, WEB_PARITY_DESIGN.md §1-A #18 — profile.AscUnlocked()==false(한 번도
    // 졸업 못함)면 섹션 비활성화) → hud 카드(칭호 +
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
    // P4 A.2/A.3 범위 메모: 웹 소리 토글(`soundToggle`)은 P5(사운드, WEB_PARITY_DESIGN.md §1-A #17)에서
    // 완성했다 — 웹 renderHome(ui.js:630) `<button class="reset-link sndtog withlabel"
    // data-act="soundToggle">${sndIcon()} 소리</button>`를 "데이터 초기화" 링크 버튼과 같은 행에
    // 나란히 짓는다(soundToggleButton/soundToggleLabel 신규 필드, UiSceneBuilder.BuildMenuScreen).
    // 승천 선택기(`ascSelector`)는 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18)에서 구현했다 — 웹처럼
    // `ascUnlocked()`(profile.AscMax>=0, 승천 1회 이상 졸업)가 false면 SetActive(false)로 숨긴다.
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

        // ── P7-4 — 게임 모드 선택기(웹 deepSelector(), ui.js:559-570) — 실토글(해금 없음, 항상 노출) ──
        [SerializeField] private Button modeNormalButton;
        [SerializeField] private Outline modeNormalOutline;
        [SerializeField] private Text modeNormalNameText;
        [SerializeField] private Button modeDeepButton;
        [SerializeField] private Outline modeDeepOutline;
        [SerializeField] private Text modeDeepNameText;
        [SerializeField] private Text deepHintText;

        // ── P6 — 승천(심화 학기) 선택기(웹 ascSelector(), ui.js:572-590) ───────────────────────
        // profile.AscUnlocked()==false(한 번도 졸업 못함)면 전체 섹션을 비활성화한다(웹은 아예 렌더
        // 자체를 생략 — Unity는 씬 구조를 유지한 채 SetActive(false)로 동등하게 구현). 웹 파리티 P7-4부터
        // 심화모드(deep) 선택 중에도 이 섹션을 숨긴다(상호배제, RefreshAscSelector 참조).
        [SerializeField] private RectTransform ascSectionRoot;
        [SerializeField] private Text ascBadgeText;  // "일반" / "심화 N"
        [SerializeField] private Text ascLevelText;  // "일반 난이도" / "점수 보정 ×N"
        [SerializeField] private Text ascRuleText;   // "표준 규칙..." / "이번 단계: ..." / "누적 난이도 상승"
        [SerializeField] private Text ascHintText;   // "다음 단계는 심화 N 졸업 후 열려요" / "승천 점수는 별도 집계..."
        [SerializeField] private Button ascPrevButton;
        [SerializeField] private Button ascNextButton;

        // ── P4 A.5 — 데이터 초기화(웹 resetAsk/resetConfirm, ui.js:207-210) ────────────────────
        [SerializeField] private Button resetButton;
        [SerializeField] private ConfirmSheetPopup resetConfirmPopup;

        // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17) — 홈 소리 토글(웹 renderHome sndtog, ui.js:630).
        [SerializeField] private Button soundToggleButton;
        [SerializeField] private Text soundToggleLabel;

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 gearbtn) — 설정 진입점(홈).
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsSheet settingsSheet;

        private ParticleSystem _mainButtonAura;

        private void Awake()
        {
            if (rankButton != null) rankButton.onClick.AddListener(OnRankClicked);
            if (levelCardButton != null) levelCardButton.onClick.AddListener(OnLevelCardClicked);
            if (modeNormalButton != null) modeNormalButton.onClick.AddListener(OnNormalModeClicked);
            if (modeDeepButton != null) modeDeepButton.onClick.AddListener(OnDeepModeClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (soundToggleButton != null) soundToggleButton.onClick.AddListener(OnSoundToggleClicked);
            if (ascPrevButton != null) ascPrevButton.onClick.AddListener(OnAscPrevClicked);
            if (ascNextButton != null) ascNextButton.onClick.AddListener(OnAscNextClicked);
        }

        // 설정 시트의 "데이터 초기화"도 이 화면의 기존 확인 흐름(OnResetConfirmed)을 그대로 재사용한다
        // (작업 지시 "홈과 동일 확인 시트 재사용").
        // Opus 2차검수 항목4(2026-08-09) — 시트를 닫을 때 홈 소리 토글 라벨을 재동기화(웹 syncSndIcons
        // 대응). 시트 안에서 소리를 껐다 켜도 홈의 별도 링크 버튼 라벨은 이 콜백이 없으면 갱신되지 않는다.
        private void OnSettingsClicked() => settingsSheet?.Show(OnResetConfirmed, onHide: RefreshSoundToggle);

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

        // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 deepToggle — ui.js "case deepToggle:
        // selDeep = !selDeep; if (selDeep) selAsc = 0;") — 심화모드 선택은 해금이 없다(항상 노출·항상
        // 선택 가능). 심화를 켜면 승천 선택값을 0으로 되돌려 상호배제(§0 결정 원칙 — 두 난이도 축은
        // 동시에 켤 수 없다, RunController 생성자가 deep이면 asc를 어차피 0으로 강제하지만 홈 화면
        // 표시값도 미리 맞춰 둔다).
        private void OnDeepModeClicked()
        {
            if (appRoot == null) return;
            appRoot.SelectedDeep = true;
            appRoot.SelectedAsc = 0;
            Refresh();
        }

        private void OnNormalModeClicked()
        {
            if (appRoot == null) return;
            appRoot.SelectedDeep = false;
            Refresh();
        }

        // 웹 ui.js:560-568 deepSelector() 두 카드의 선택 상태 배색(.deep-mode.sel = Accent 테두리·이름,
        // 그 외 = 기본 테두리·TextPrimary) + 심화 선택 시에만 노출되는 힌트 2줄.
        private void RefreshGameModeSelector()
        {
            bool deep = appRoot != null && appRoot.SelectedDeep;
            if (modeNormalOutline != null) modeNormalOutline.effectColor = deep ? UiKit.Bd : UiKit.Accent;
            if (modeNormalNameText != null) modeNormalNameText.color = deep ? UiKit.TextPrimary : UiKit.Accent;
            if (modeDeepOutline != null) modeDeepOutline.effectColor = deep ? UiKit.Accent : UiKit.Bd;
            if (modeDeepNameText != null) modeDeepNameText.color = deep ? UiKit.Accent : UiKit.TextPrimary;
            if (deepHintText != null) deepHintText.gameObject.SetActive(deep);
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

        // 웹 파리티 P5(웹 setSound(!soundOn), ui.js:211 `case "soundToggle": setSound(!soundOn); break;`)
        // — 홈에서는 런이 없으므로(st==null) bgmStart를 시도하지 않는다(웹도 `if (st && st.phase)`
        // 가드로 동일하게 건너뛴다). 끌 때는 방어적으로 BgmStop까지 호출(멱등 — 애초에 홈에서 BGM이
        // 재생 중일 수 없지만, SettingsSheet.OnSoundToggle과 동일 모양을 맞춰 둔다).
        private void OnSoundToggleClicked()
        {
            bool next = !SoundKit.Enabled;
            SoundKit.SetEnabled(next);
            if (next) SoundKit.Sfx("coin");
            else SoundKit.BgmStop();
            RefreshSoundToggle();
        }

        private void RefreshSoundToggle()
        {
            bool on = SoundKit.Enabled;
            if (soundToggleLabel != null)
            {
                soundToggleLabel.text = on ? "소리 켜짐" : "소리 꺼짐";
                soundToggleLabel.color = on ? UiKit.TextPrimary : UiKit.TextSecondary;
            }
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
            RefreshGameModeSelector();
            RefreshAscSelector(profile);
            RefreshSoundToggle();
        }

        // ── P6 — 승천(심화 학기) 선택기(웹 ascSelector(), ui.js:572-590) ───────────────────────
        // 웹 ui.js:573 `if (selDeep) return "";` — 심화모드(심볼 덱) 선택 중이면 승천 선택기 자체를
        // 숨긴다(요구치 이중 가중 방지, §0 결정 원칙 "두 난이도 축 상호배제" — P7-1부터 엔진은 이미
        // deep이면 asc를 0으로 강제하지만, 홈 화면 표시도 함께 숨겨야 사용자가 승천을 고르고 있다고
        // 착각하지 않는다).
        private void RefreshAscSelector(PlayerProfile profile)
        {
            bool unlocked = profile.AscUnlocked() && !(appRoot != null && appRoot.SelectedDeep);
            if (ascSectionRoot != null) ascSectionRoot.gameObject.SetActive(unlocked);
            if (!unlocked) return;

            int maxA = profile.MaxPlayableAsc();
            // 웹 game.js:576 `selAsc = Math.max(0, Math.min(maxA, selAsc));` — 매 렌더마다 재클램프.
            int sel = Mathf.Clamp(appRoot.SelectedAsc, 0, maxA);
            appRoot.SelectedAsc = sel;
            var info = profile.GetAscInfo(sel);

            if (ascBadgeText != null) ascBadgeText.text = sel == 0 ? "일반" : $"심화 {sel}";
            if (ascLevelText != null)
                ascLevelText.text = sel == 0 ? "일반 난이도" : $"점수 보정 ×{NumberFormat.Fmt(info.ScoreMul)}";
            if (ascRuleText != null)
                ascRuleText.text = sel == 0
                    ? "표준 규칙 · 점수 랭킹 반영"
                    : (!string.IsNullOrEmpty(info.Rule) ? "이번 단계: " + info.Rule : "누적 난이도 상승");
            if (ascHintText != null)
                ascHintText.text = (sel >= maxA && sel < info.Max)
                    ? $"다음 단계는 심화 {sel} 졸업(스테이지 15 클리어) 후 열려요"
                    : "승천 점수는 별도 집계 — 일반 랭킹은 안전";
            if (ascPrevButton != null) ascPrevButton.interactable = sel > 0;
            if (ascNextButton != null) ascNextButton.interactable = sel < maxA;
        }

        private void OnAscPrevClicked()
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return;
            appRoot.SelectedAsc = Mathf.Max(0, appRoot.SelectedAsc - 1);
            RefreshAscSelector(profile);
        }

        private void OnAscNextClicked()
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return;
            int maxA = profile.MaxPlayableAsc();
            appRoot.SelectedAsc = Mathf.Min(maxA, appRoot.SelectedAsc + 1);
            RefreshAscSelector(profile);
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
