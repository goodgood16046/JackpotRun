using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // GameOver 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/GameOverPanel.cs. "GameOver: 딤 0.3s →
    // 패널 스케일인 → 신규 업적 스태거 리스트(0.05s 간격)"(설계 수치 그대로). 이관 원본:
    // Scripts/UI/RunPanels.cs의 BuildGameOver.
    public sealed class GameOverPanel : MonoBehaviour
    {
        private const float DimDuration = 0.3f; // 설계 명시
        private const float ScaleInDuration = 0.35f; // 설계 미명시 — 패널 스케일인 길이 기본값
        private const float AchStagger = 0.05f; // 설계 명시

        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Text titleScoreText;
        [SerializeField] private Text finalScoreText;
        [SerializeField] private Text stageReachedText;
        [SerializeField] private Text recordsText;
        [SerializeField] private RectTransform achHeaderRow;
        [SerializeField] private Text achHeaderText;
        [SerializeField] private RectTransform achContent;
        [SerializeField] private RectTransform achRowTemplate; // 자식 경로 계약: Label(Text)
        [SerializeField] private Text achTotalText;
        [SerializeField] private Button menuButton;

        private void Awake()
        {
            if (achRowTemplate != null) achRowTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(GameSession session, FailureOutcome failure, Action onMenu)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            var run = session.State;
            var profile = session.Profile;
            long finalScore = failure?.finalScore ?? 0;
            var (titleEmoji, titleLabel) = Formulas.ScoreTitle(finalScore);

            if (titleScoreText != null) titleScoreText.text = $"{titleEmoji} {titleLabel}";
            if (finalScoreText != null) finalScoreText.text = $"최종 점수 {NumberFormat.Comma(finalScore)}";
            if (stageReachedText != null) stageReachedText.text = $"도달 스테이지 {run.Stage}";
            if (recordsText != null)
                recordsText.text = $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 최고 스테이지 {profile.BestStage} · 통산 런 {profile.Runs}";

            var newAch = session.LastNewAchievements;
            bool hasNew = newAch != null && newAch.Count > 0;
            if (achHeaderRow != null) achHeaderRow.gameObject.SetActive(hasNew);
            if (achHeaderText != null) achHeaderText.text = hasNew ? $"🏅 신규 업적 {newAch.Count}개" : "";
            if (achTotalText != null) achTotalText.text = $"업적 {profile.AchievedIds.Count}/{Achievements.Count}";

            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(() => onMenu());
            }

            var rows = BuildAchRows(newAch);
            if (firstShow) StartCoroutine(EnterRoutine(rows));
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.localScale = Vector3.one;
                foreach (var r in rows) r.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private IEnumerator EnterRoutine(List<RectTransform> achRows)
        {
            if (cardRect != null) cardRect.localScale = Vector3.zero;
            foreach (var r in achRows) if (r != null) r.localScale = Vector3.zero;

            yield return UiTween.FadeRoutine(dimGroup, 0f, 1f, DimDuration);
            if (cardRect != null)
                yield return UiTween.ScaleRoutine(cardRect, Vector3.zero, Vector3.one, ScaleInDuration, UiTween.Ease.OutBack);

            for (int i = 0; i < achRows.Count; i++)
            {
                if (achRows[i] != null) StartCoroutine(UiTween.ScaleRoutine(achRows[i], Vector3.zero, Vector3.one, 0.2f, UiTween.Ease.OutBack));
                if (i < achRows.Count - 1) yield return new WaitForSeconds(AchStagger);
            }
        }

        private List<RectTransform> BuildAchRows(IReadOnlyList<AchDef> newAch)
        {
            var result = new List<RectTransform>();
            if (achContent == null || achRowTemplate == null) return result;

            for (int i = achContent.childCount - 1; i >= 0; i--)
            {
                var child = achContent.GetChild(i);
                if (child == achRowTemplate) continue;
                Destroy(child.gameObject);
            }

            if (newAch == null) return result;
            for (int i = 0; i < newAch.Count; i++)
            {
                var a = newAch[i];
                var row = Instantiate(achRowTemplate, achContent);
                row.gameObject.SetActive(true);
                row.name = "Ach_" + a.id;
                result.Add(row);
                var label = row.Find("Label")?.GetComponent<Text>();
                if (label != null) label.text = $"{a.emoji} {a.name} — {a.desc}";
            }
            return result;
        }
    }
}
