using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 설정 시트 — 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 openSettings ui.js:881-908). 웹은
    // 소리/볼륨/진동 토글 + 계정 행을 담는 전역 시트(gearbtn, 인트로/플레이 공용 1개 인스턴스)지만,
    // Unity는 씬이 둘(Intro/Play)로 갈려 각 화면이 자기 인스턴스를 소유한다(MenuView.settingsSheet /
    // RunView.settingsSheet — ConfirmSheetPopup 등 기존 패턴과 동일, BuildSettingsSheet가 양쪽에 각각
    // 굽는다). 소리/볼륨은 P5(사운드, WEB_PARITY_DESIGN.md 로드맵) 예약이라 이번 슬라이스는 비활성 행 +
    // "준비 중"만 둔다(작업 지시 그대로). 진동은 PlayerPrefs로 즉시 동작하고, 데이터 초기화는 홈
    // (MenuView)의 확인 시트와 동일한 문구/흐름을 자기 자신의 ConfirmSheetPopup으로 재사용한다.
    public sealed class SettingsSheet : MonoBehaviour
    {
        private const float SheetSlideDuration = 0.24f; // 다른 Run 패널들과 동일 관례(S12c §6).

        // 웹 slotweb_vibe(localStorage) 대응 — PlayerPrefs 키. 기본값 켜짐(1), 웹과 동일(game.js
        // `localStorage.getItem("slotweb_vibe") !== "0"`).
        public const string VibePrefKey = "jackpotrun_vibe";
        public static bool VibeEnabled => PlayerPrefs.GetInt(VibePrefKey, 1) != 0;

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private Button vibeToggleButton;
        [SerializeField] private Text vibeToggleLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private ConfirmSheetPopup resetConfirmPopup;
        [SerializeField] private Button closeButton;

        private System.Action _onResetConfirmed;

        private void Awake()
        {
            // Opus 2차검수 필수⑤ 계열 결함(2026-08-09, CellInfoSheet/BagPopup/ManipPickPopup/
            // ConfirmSheetPopup Awake 주석과 동일 원인) — 여기서 SetActive(false)를 부르면 안 된다.
            // 빌더(BuildSettingsSheet→BuildSheetChrome)가 이미 비활성으로 굽는다.
            if (dimGroup != null) dimGroup.alpha = 0f;
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (vibeToggleButton != null) vibeToggleButton.onClick.AddListener(OnVibeToggle);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            RefreshVibeLabel();
        }

        /// <summary>onResetConfirmed — 데이터 초기화 확인 시 실제로 지울 방법(AppRoot.ResetProfile 등)은
        /// 호출측(MenuView/RunView)이 넘긴다 — 이 컴포넌트는 UI만 담당하고 프로필/세션에 직접 접근하지
        /// 않는다(다른 Run 패널들의 콜백 위임 관례와 동일).</summary>
        public void Show(System.Action onResetConfirmed = null)
        {
            _onResetConfirmed = onResetConfirmed;
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            RefreshVibeLabel();

            StopAllCoroutines();
            if (firstShow) StartCoroutine(EnterRoutine());
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator EnterRoutine()
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
        }

        // 웹 game.js:216-218 setVibe — 토글 + (켤 때만) 짧은 확인 진동.
        private void OnVibeToggle()
        {
            bool next = !VibeEnabled;
            PlayerPrefs.SetInt(VibePrefKey, next ? 1 : 0);
            PlayerPrefs.Save();
            if (next) SafeVibrate();
            RefreshVibeLabel();
        }

        private void RefreshVibeLabel()
        {
            bool on = VibeEnabled;
            if (vibeToggleLabel != null) { vibeToggleLabel.text = on ? "켜짐" : "꺼짐"; vibeToggleLabel.color = on ? UiKit.Bg : UiKit.TextSecondary; }
            if (vibeToggleButton != null)
            {
                var img = vibeToggleButton.GetComponent<Image>();
                if (img != null) img.color = on ? UiKit.Accent : UiKit.Panel2;
            }
        }

        private void OnResetClicked()
        {
            // 웹 파리티 P4-3(작업 지시 B "데이터 초기화 — 홈과 동일 확인 시트 재사용") — MenuView.
            // OnResetClicked와 동일한 문구(웹 ui.js:207-210 resetAsk 시트 텍스트) — 인스턴스는 화면별로
            // 별도지만(giveUpConfirmPopup/deviceOfferPopup과 동일 관례) 내용/흐름은 그대로 재사용한다.
            resetConfirmPopup?.Show(
                "데이터 초기화",
                "최고 점수 · 캐릭터/머신/장치 해금 · 도감 발견 기록이 모두 사라지고 처음(초보학생 + 기본)부터 시작합니다.\n랭킹에 등록한 기록은 그대로 유지돼요.",
                "초기화", () => { Hide(); _onResetConfirmed?.Invoke(); }, "취소");
        }

        // PressFx(모든 버튼 공용 프레스 피드백)가 탭 진동 훅으로 호출한다 — 설정 OFF면 무시.
        // Opus 2차검수 필수④(2026-08-09) — `Handheld.Vibrate()`는 안드로이드에서 기기별 기본 패턴
        // (대략 ~500ms 롱버즈)을 그대로 재생해 웹의 짧은 확인 진동(vibrate(18) 등, 7~18ms대)과 체감이
        // 전혀 다르다("탭마다 길게 부르르 떤다"는 결함) — `VibrationEffect.createOneShot(15ms, DEFAULT_
        // AMPLITUDE)`를 리플렉션(AndroidJavaObject)으로 직접 호출해 웹 근사 길이로 짧게 낸다(API 26+).
        // 에디터/안드로이드가 아닌 플랫폼/구버전 API/예외 상황은 전부 조용히 무시(웹 vibrate()의
        // try/catch 관례와 동일).
        private const long VibrateShortMs = 15L;

        public static void SafeVibrate()
        {
            if (!VibeEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;

                    using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        int sdkInt = versionClass.GetStatic<int>("SDK_INT");
                        if (sdkInt >= 26) // VibrationEffect는 API 26(Android 8.0)부터.
                        {
                            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                            {
                                const int defaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE
                                var effect = effectClass.CallStatic<AndroidJavaObject>(
                                    "createOneShot", VibrateShortMs, defaultAmplitude);
                                vibrator.Call("vibrate", effect);
                            }
                        }
                        else
                        {
                            vibrator.Call("vibrate", VibrateShortMs); // 구버전 API — 밀리초 오버로드.
                        }
                    }
                }
            }
            catch { /* 미지원 기기/권한 없음 등 — 조용히 무시(웹 vibrate()의 try/catch 관례와 동일) */ }
#endif
        }
    }
}
