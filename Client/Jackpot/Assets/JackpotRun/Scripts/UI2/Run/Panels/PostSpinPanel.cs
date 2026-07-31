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
        private const float DimDuration = 0.3f; // 설계 명시(GameOverPanel과 공용 규칙)
        private const float ButtonStagger = 0.06f; // 설계 미명시 — 만회 버튼 등장 간격 기본값
        private const float ButtonPopDuration = 0.25f;

        [SerializeField] private CanvasGroup dimGroup;
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
                foreach (var b in buttons) b.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private IEnumerator EnterRoutine(List<RectTransform> buttons)
        {
            foreach (var b in buttons) if (b != null) b.localScale = Vector3.zero;
            yield return UiTween.FadeRoutine(dimGroup, 0f, 1f, DimDuration);
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
                    if (label != null) label.text = "🎲 도박꾼 무료 재굴림";
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
