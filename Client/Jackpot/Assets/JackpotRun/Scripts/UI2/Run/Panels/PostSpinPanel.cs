using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // PostSpin 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/PostSpinPanel.cs. "PostSpin: 어둡게+
    // 만회 버튼 등장"(딤 0.3s, GameOverPanel과 동일 규칙 재사용) 지시. 만회 수단(도박꾼 재굴림/장치)은
    // FailureOutcome.manipHints를 그대로 버튼화한다. 이관 원본: Scripts/UI/RunPanels.cs의 BuildPostSpin.
    public sealed class PostSpinPanel : MonoBehaviour
    {
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시).
        // 이전엔 "GameOverPanel과 공용 규칙"으로 0.3s 딤-only 페이드였으나, 이 패널은 이제 바텀시트로
        // 전환되어 딤 페이드 + 카드 슬라이드가 함께 재생된다(GameOverPanel은 중앙 팝업 유지라 그대로 0.3s).
        private const float SheetSlideDuration = 0.24f;
        private const float ButtonStagger = 0.06f; // 설계 미명시 — 만회 버튼 등장 간격 기본값
        private const float ButtonPopDuration = 0.25f;

        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private RectTransform cardRect; // S12c §6 — 시트(Card) 슬라이드업 대상
        [SerializeField] private Text subText;
        [SerializeField] private RectTransform manipButtonsContent;
        [SerializeField] private RectTransform manipButtonTemplate; // 자식 경로 계약: Label(Text)
        [SerializeField] private Button giveUpButton;

        private void Awake()
        {
            if (manipButtonTemplate != null) manipButtonTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(RunState run, FailureOutcome failure, Action<DeviceDef> onManip, Action onGambler, Action onGiveUp)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            long deficit = failure?.deficitAtFailure ?? 0;
            if (subText != null)
                subText.text = $"부족 EXP {NumberFormat.Comma(deficit)} — 만회 수단을 사용하거나 포기하세요.";

            if (giveUpButton != null)
            {
                giveUpButton.onClick.RemoveAllListeners();
                giveUpButton.onClick.AddListener(() => onGiveUp());
            }

            var buttons = BuildManipButtons(failure, onManip, onGambler);
            if (firstShow) StartCoroutine(EnterRoutine(buttons));
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
                foreach (var b in buttons) b.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // S12c §6 — 시트 슬라이드업(0.24s OutCubic) + 배경 딤 페이드 동시 재생, 완료 후 버튼 스태거 팝인.
        private IEnumerator EnterRoutine(List<RectTransform> buttons)
        {
            foreach (var b in buttons) if (b != null) b.localScale = Vector3.zero;
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null) StartCoroutine(UiTween.ScaleRoutine(buttons[i], Vector3.zero, Vector3.one, ButtonPopDuration, UiTween.Ease.OutBack));
                if (i < buttons.Count - 1) yield return new WaitForSeconds(ButtonStagger);
            }
        }

        private List<RectTransform> BuildManipButtons(FailureOutcome failure, Action<DeviceDef> onManip, Action onGambler)
        {
            var result = new List<RectTransform>();
            if (manipButtonsContent == null || manipButtonTemplate == null) return result;

            for (int i = manipButtonsContent.childCount - 1; i >= 0; i--)
            {
                var child = manipButtonsContent.GetChild(i);
                if (child == manipButtonTemplate) continue;
                Destroy(child.gameObject);
            }

            var hints = failure != null ? failure.manipHints : null;
            foreach (var hint in hints ?? (IReadOnlyList<string>)Array.Empty<string>())
            {
                if (hint == "GAMBLER_REROLL")
                {
                    var btn = Instantiate(manipButtonTemplate, manipButtonsContent);
                    btn.gameObject.SetActive(true);
                    btn.name = "Manip_Gambler";
                    result.Add(btn);
                    var label = btn.Find("Label")?.GetComponent<Text>();
                    if (label != null) label.text = "도박꾼 무료 재굴림";
                    var button = btn.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => onGambler());
                    }
                }
                else if (hint.StartsWith("DEVICE:"))
                {
                    string devId = hint.Substring("DEVICE:".Length);
                    var dev = Devices.ById(devId);
                    if (dev == null) continue;

                    var btn = Instantiate(manipButtonTemplate, manipButtonsContent);
                    btn.gameObject.SetActive(true);
                    btn.name = "Manip_" + devId;
                    result.Add(btn);
                    var label = btn.Find("Label")?.GetComponent<Text>();
                    if (label != null) label.text = $"{dev.emoji} {dev.name}";
                    var button = btn.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => onManip(dev));
                    }
                }
            }
            return result;
        }
    }
}
