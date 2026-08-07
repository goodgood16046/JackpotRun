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
    //
    // S14 §E — "화면 채도 0.5로 0.4s 페이드"(게임오버 진입). uGUI에는 saturate() 필터가 없어 S12 §7
    // 재해석 규칙("filter: saturate/brightness → CanvasGroup 알파 또는 색 곱") 그대로, 기존 검정
    // 스크림(dimGroup)의 페이드를 "채도 저하"의 근사로 재사용하고 지속시간만 0.3s→0.4s로 맞췄다
    // (완전한 채도 필터를 새로 만들지 않음 — 재해석 보고 대상).
    public sealed class GameOverPanel : MonoBehaviour
    {
        private const float DimDuration = 0.4f; // S14 §E 명시: "0.4s 페이드"(원래 0.3s에서 갱신)
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

        private ParticleSystem _gameOverFx; // S7c 연출 훅: 표시 중 fx_gameover 루프

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

            // WEB_PARITY P1 ⑤: 자발적 포기(voluntary)면 점수티어 플레이버 타이틀 대신 "포기 — 즉시 결산"
            // 계열 문구로 실패 프레이밍을 생략한다(웹 game.js:2547 "🏁 스테이지 N에서 런을 종료했어요 —
            // 지금까지 점수로 결산!" 대응. astral 이모지 금지·한글만).
            bool voluntary = failure != null && failure.Voluntary;
            var (titleEmoji, titleLabel) = Formulas.ScoreTitle(finalScore);
            if (titleScoreText != null) titleScoreText.text = voluntary ? "포기 — 즉시 결산" : $"{titleEmoji} {titleLabel}";
            if (finalScoreText != null) finalScoreText.text = $"최종 점수 {NumberFormat.Comma(finalScore)}";
            if (stageReachedText != null) stageReachedText.text = $"도달 스테이지 {run.Stage}";
            if (recordsText != null)
                recordsText.text = $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 최고 스테이지 {profile.BestStage} · 통산 런 {profile.Runs}";

            var newAch = session.LastNewAchievements;
            bool hasNew = newAch != null && newAch.Count > 0;
            if (achHeaderRow != null) achHeaderRow.gameObject.SetActive(hasNew);
            // S8 항목⑤: 🏅(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (achHeaderText != null) achHeaderText.text = hasNew ? $"신규 업적 {newAch.Count}개" : "";
            if (achTotalText != null) achTotalText.text = $"업적 {profile.AchievedIds.Count}/{Achievements.Count}";

            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(() => onMenu());
            }

            // S7c 연출 훅: "GameOverPanel: 표시 중 GameOver 루프." — 이미 재생 중이면 다시 시작하지 않는다.
            if (_gameOverFx == null && cardRect != null) _gameOverFx = FxKit.I?.PlayLoop(FxId.GameOver, cardRect);

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
            if (_gameOverFx != null)
            {
                FxKit.I?.StopLoop(_gameOverFx);
                _gameOverFx = null;
            }
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
