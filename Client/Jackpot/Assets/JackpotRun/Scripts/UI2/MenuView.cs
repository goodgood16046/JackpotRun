using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 메인 메뉴 화면 — ENGINE_PORT_DESIGN.md S7 "화면 사양" MenuView: 타이틀·대표 캐릭터 아트 3장
    // 캐러셀(좌우 슬라이드 4s 루프)·시작/도감 버튼·프로필 요약 카드. 레이아웃(타이틀 텍스트, 버튼
    // 2개)은 UiSceneBuilder가 정적으로 만들고 [SerializeField]로 이 컴포넌트에 와이어링한다 — 버튼
    // onClick도 빌더가 직접 AppRoot.ShowPick/ShowDex에 퍼시스턴트 리스너로 연결하므로 여기서는
    // "화면이 보이는 동안 재생되는 연출"(캐러셀)과 "화면을 열 때마다 최신값으로 갱신해야 하는 표시"
    // (프로필 요약)만 담당한다.
    //
    // 이관 원본: Scripts/UI/MainMenuScreen.cs(프로필 요약 문구 그대로) — 레이아웃 코드만 버렸다.
    public sealed class MenuView : MonoBehaviour
    {
        // carouselTrack의 자식은 정확히 4개(고유 아트 3장 + 첫 장의 복제본 1장)이고, 각 자식은
        // SlotWidth 간격으로 나란히 배치되어 있다(UiSceneBuilder가 배치) — 마지막 슬롯(복제본)에
        // 도달하면 순간적으로 0번 위치로 되돌려 무한 루프처럼 보이게 한다.
        private const float SlotWidth = 560f;
        private const int UniqueSlideCount = 3;
        private const float HoldDuration = 4f;
        private const float SlideDuration = 0.6f;

        [SerializeField] private AppRoot appRoot;
        [SerializeField] private Text profileSummaryText;
        [SerializeField] private RectTransform carouselTrack;

        private Coroutine _carouselRoutine;

        private void OnEnable()
        {
            RefreshSummary();
            if (carouselTrack != null)
            {
                carouselTrack.anchoredPosition = Vector2.zero;
                _carouselRoutine = StartCoroutine(CarouselLoop());
            }
        }

        private void OnDisable()
        {
            if (_carouselRoutine != null)
            {
                StopCoroutine(_carouselRoutine);
                _carouselRoutine = null;
            }
        }

        private void RefreshSummary()
        {
            if (profileSummaryText == null) return;
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null)
            {
                profileSummaryText.text = string.Empty;
                return;
            }
            profileSummaryText.text =
                $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 런 {NumberFormat.Comma(profile.Runs)}회 · " +
                $"업적 {profile.AchievedIds.Count}/{Achievements.Count}";
        }

        private IEnumerator CarouselLoop()
        {
            int index = 0;
            while (true)
            {
                yield return new WaitForSeconds(HoldDuration);
                if (carouselTrack == null) yield break;

                index++;
                Vector2 from = carouselTrack.anchoredPosition;
                Vector2 to = new Vector2(-SlotWidth * index, 0f);
                yield return UiTween.MoveRoutine(carouselTrack, from, to, SlideDuration, UiTween.Ease.OutCubic);
                if (carouselTrack == null) yield break;

                if (index >= UniqueSlideCount)
                {
                    // 마지막 슬롯은 0번 슬라이드의 복제본이라 순간이동해도 시각적으로 이어진다.
                    carouselTrack.anchoredPosition = Vector2.zero;
                    index = 0;
                }
            }
        }
    }
}
