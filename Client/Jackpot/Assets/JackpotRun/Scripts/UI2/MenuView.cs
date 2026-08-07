using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 메인 메뉴 화면 = 웹 단독판 renderHome 이식 — ENGINE_PORT_DESIGN.md S12 §4. scr-title(타이틀+
    // 부제) → hud 카드(칭호 + 최고점수/최고스테이지/플레이 3칸) → "업적 n/34 · 장치 n/16 해금" 요약줄
    // → 게임 시작(골드) + 랭킹/도감(고스트 2개) → 설명 2줄. 레이아웃은 UiSceneBuilder가 정적으로 짓고
    // [SerializeField]로 이 컴포넌트에 와이어링한다 — 이 클래스는 "화면을 열 때마다 최신 프로필로
    // 갱신"만 담당한다(런타임 코드생성 없음).
    //
    // 이관 메모(S12a): 이전 슬라이스(S7~S10)의 캐러셀·"@닉네임/닉네임 변경" 행은 웹 renderHome에
    // 대응 요소가 없어 제거했다(설계 "임의 변경 금지" — 스펙에 없는 요소를 계속 얹는 것도 스펙 위반
    // 이라 판단). 그 결과 메뉴에서 LoginView로 돌아갈 경로가 사라졌다 — 웹도 별도 설정(⚙️) 진입점을
    // 쓰므로 원본에 맞는 동작이지만, Unity에는 그 설정 화면이 아직 없다(Fable 보고 대상 — 후속 슬라이스
    // 필요 여부 판단).
    //
    // 랭킹 버튼은 S15에서 실제 화면(RankView)으로 연결됐다(§4 그대로 버튼 유지, 토스트 안내는 제거).
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

        private ParticleSystem _mainButtonAura;

        private void Awake()
        {
            if (rankButton != null) rankButton.onClick.AddListener(OnRankClicked);
        }

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

        private void Refresh()
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
        }
    }
}
