using System;
using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // NodeSelect 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/NodePanel.cs. 스테이지 클리어 등급
    // 배너(드롭+점수 CountUp) 후 1.0s 뒤 노드 3택 카드 패널이 슬라이드업 한다("RunView 연출" 지시 그대로).
    // 이관 원본: Scripts/UI/RunPanels.cs의 BuildNodeSelect(레이아웃은 버리고 NodeKindInfo/카드 구성 로직만).
    public sealed class NodePanel : MonoBehaviour
    {
        private const float BannerFadeDuration = 0.15f;
        private const float BannerDropDuration = 0.4f; // 설계 미명시 — OutBack 드롭 길이 기본값
        private const float ScoreCountUpDuration = 0.5f; // 설계 미명시
        private const float PostBannerDelay = 1.0f; // 설계 명시: "1.0s 후 노드 패널 슬라이드업"
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시).
        private const float CardSlideDuration = 0.24f;
        private const float BannerRestY = -160f;
        private const float BannerStartY = 240f;

        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private RectTransform bannerRect;
        [SerializeField] private Text bannerGradeText;
        [SerializeField] private Text bannerScoreText;
        [SerializeField] private Text bannerSubText;
        [SerializeField] private RectTransform cardRect; // 슬라이드업 대상(카드 패널 전체)
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private RectTransform cardTemplate;
        [SerializeField] private CanvasGroup dimGroup; // S14 §E — 배경 딤(scrim 자체의 CanvasGroup) 페이드

        private Coroutine _routine;

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(ClearOutcome clear, RunState run, Action<int> onChoose)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            BuildCards(run, onChoose);

            if (_routine != null) StopCoroutine(_routine);
            if (firstShow) _routine = StartCoroutine(EnterRoutine(clear));
            else SnapToRest();
        }

        public void Hide()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            gameObject.SetActive(false);
        }

        private void SnapToRest()
        {
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
            if (dimGroup != null) dimGroup.alpha = 1f;
        }

        private IEnumerator EnterRoutine(ClearOutcome clear)
        {
            // S12c §6 — 하단 고정 앵커(BuildSheetChrome) 시트라 오프스크린 시작 위치는 카드 자신의
            // 높이(rect.height)만큼 아래(translateY(100%) 재해석) — 고정 매직넘버 대신 실측값 사용.
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (bannerGroup != null) bannerGroup.alpha = 0f;

            if (clear != null)
            {
                PopulateBanner(clear);
                if (bannerRect != null) bannerRect.anchoredPosition = new Vector2(0f, BannerStartY);
                // S7c 연출 훅: "NodePanel(클리어 배너): Clear."
                FxKit.I?.Play(FxId.Clear, bannerRect);
                yield return UiTween.FadeRoutine(bannerGroup, 0f, 1f, BannerFadeDuration);
                if (bannerRect != null)
                    yield return UiTween.MoveRoutine(bannerRect, bannerRect.anchoredPosition,
                        new Vector2(0f, BannerRestY), BannerDropDuration, UiTween.Ease.OutBack);
                if (bannerScoreText != null)
                    yield return UiTween.CountUpRoutine(0, clear.gainedScore, ScoreCountUpDuration,
                        v => bannerScoreText.text = $"+{NumberFormat.Comma(v)}점", UiTween.Ease.OutCubic);
                yield return new WaitForSeconds(PostBannerDelay);
            }

            // S14 §E / S12c §6 — "1.0s 후 노드 패널 슬라이드업 + 배경 딤 페이드"(동시 재생, 0.24s OutCubic).
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, CardSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero,
                    CardSlideDuration, UiTween.Ease.OutCubic);
            _routine = null;
        }

        private void PopulateBanner(ClearOutcome clear)
        {
            if (bannerGradeText != null) bannerGradeText.text = clear.grade;
            if (bannerScoreText != null) bannerScoreText.text = "+0점";
            if (bannerSubText != null)
            {
                // S8 항목⑤: 🎉/🌈(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
                // Opus 검수 반영(2026-08-07): 빚문서는 점수(gainedScore)만 0이고 코인(clearCoin)은
                // 정상 지급된다(웹 game.js:1416-1420) — "무보상"이라고 하면 코인도 안 나오는 것처럼
                // 오해되므로 "점수 보상 0"으로 문구를 좁혔다.
                string debt = clear.inDebt ? " (빚 상환 중·점수 보상 0)" : "";
                string prism = clear.nextNodeForcedPrism ? " · 다음 프리즘 확정" : "";
                bannerSubText.text = $"스테이지 {clear.clearedStage} 클리어 · 코인+{NumberFormat.Comma(clear.clearCoin)}{debt}{prism}";
            }
        }

        private void BuildCards(RunState run, Action<int> onChoose)
        {
            if (cardsContent == null || cardTemplate == null) return;
            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < run.NodeOptions.Count; i++)
            {
                int idx = i;
                var (emoji, title, desc) = NodeKindInfo(run.NodeOptions[i]);

                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true);
                card.name = "NodeCard_" + idx;

                var headText = card.Find("Content/Head")?.GetComponent<Text>();
                if (headText != null) headText.text = $"{emoji} {title}";
                var bodyText = card.Find("Content/Body")?.GetComponent<Text>();
                if (bodyText != null) bodyText.text = desc;

                var btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => onChoose(idx));
                }
            }
        }

        // S8 항목⑤: astral 이모지(✨🛡️🛒🎲🌑 등)는 레거시 Text에서 렌더링되지 않는다 — BMP 기호로 대체.
        private static (string emoji, string title, string desc) NodeKindInfo(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Augment: return ("★", "증강", "증강 3종 중 1개를 선택합니다.");
                case NodeKind.Relic: return ("◆", "유물", "유물 3종 중 1개를 선택합니다.");
                case NodeKind.Shop: return ("▲", "상점", "코인으로 증강·유물·아이템을 구매합니다.");
                // WEB_PARITY P1 ④: 코인 8 → 12(웹 game.js:1633).
                case NodeKind.Rest: return ("☕", "휴식", "코인 +12를 즉시 받습니다.");
                case NodeKind.Gamble: return ("♠", "도박", "보유 코인 전부를 걸고 50% 확률로 2배 또는 전부를 잃습니다.");
                case NodeKind.Event: return ("❓", "이벤트", "무작위 보상 이벤트가 발생합니다.");
                // WEB_PARITY P1 ④: 코인 15 → 30(웹 game.js:1673).
                case NodeKind.Curse: return ("●", "저주", "저주 1개를 받는 대신 코인 +30을 받습니다.");
                case NodeKind.Risk: return ("⚠", "위험", "프리즘/골드 증강과 저주를 동시에 받습니다.");
                // WEB_PARITY P1 ④: 보스 클리어 후에만 등장(RollNextNodes 신규 4번째 옵션).
                case NodeKind.Device: return ("■", "장치", "무작위 미보유 장치 1개를 오퍼합니다 — 장착 또는 코인 +15.");
                default: return ("❔", k.ToString(), "");
            }
        }
    }
}
