using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 릴 주변 상태 표시줄 — FUN_OVERHAUL_DESIGN F3(LAST SPIN 태그)·F4(홀드 칩/안내)·F5(피버 게이지 바).
    // ReelSection(ReelView 가 붙은 RectTransform) 안에 코드로 얹는다(씬 리빌드 불필요 — UiSceneBuilder 무변경). ReelSection 은
    // 레이아웃 그룹이 아니라 자식 좌표를 마음대로 쓸 수 있고, 셀(높이 UiKit.ReelCellSize)이 섹션 세로 중앙에 놓이므로
    // 위치를 "섹션 중앙 ± 셀 반높이"로 잡아 섹션이 자라도 셀 바로 위/아래에 붙어 있게 한다.
    //   · 위 줄: 홀드 칩(왼쪽) · LAST SPIN! 태그(가운데) · 홀드 안내(오른쪽)
    //   · 아래 줄: 피버 게이지 바(일반 모드 전용 — 심화 HUD 의 FeverBarBg/feverText 와는 별개 요소라 섞이지 않는다)
    // 심화 런(RunController.Hold/Fever == null)에서는 칩/안내/바를 숨긴다.
    public sealed class ReelStatusHud : MonoBehaviour
    {
        private const float CellHalf = UiKit.ReelCellSize * 0.5f;   // 98
        private const float SideMargin = 28f;
        private const float TopRowY = CellHalf + 3f;                // 칩/태그 바닥이 셀 윗면 3px 위
        private const float BarGap = 6f;
        private const float BarHeight = 26f;
        private const float FillAnimDuration = 0.6f;                // 웹 .fv-fill transition .8s 근사

        private static readonly Color ChipText = new Color(0.71f, 1f, 0.95f);

        private RectTransform _chipRt;
        private CanvasGroup _chipGroup;
        private Text _chipText;
        private Text _hintText;

        private RectTransform _tagRt;
        private CanvasGroup _tagGroup;
        private Coroutine _tagPulse;
        private bool _tagOn;

        private RectTransform _feverRt;
        private RectTransform _fillRt;
        private Image _fillImg;
        private Outline _trackOutline;
        private Text _feverText;
        private Coroutine _fillRoutine;
        private Coroutine _glowRoutine;
        private float _fillPct;
        private int _glowMode;   // 0 평상 · 1 곧 폭발(hot) · 2 피버 중(active)

        /// <summary>reelSection(ReelView 의 RectTransform) 안에 상태 표시줄을 만든다.</summary>
        public static ReelStatusHud Create(RectTransform reelSection)
        {
            if (reelSection == null) return null;
            var go = new GameObject("ReelStatus", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(reelSection, false);
            UiKit.Fill(rt);
            var hud = go.AddComponent<ReelStatusHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            BuildChip();
            BuildLastTag();
            BuildFeverBar();
            SetHold(null, false, null);
            SetFever(null, false);
            SetLastSpin(false);
        }

        // ── 홀드 칩 + 안내 ─────────────────────────────────────────────────────────────
        private void BuildChip()
        {
            var chip = UiKit.HGroup(transform, 0, new RectOffset(16, 16, 2, 2), true, true, autoSizeW: true);
            chip.name = "HoldChip";
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
            chip.pivot = new Vector2(0f, 0f);
            chip.sizeDelta = new Vector2(0f, 30f);
            chip.anchoredPosition = new Vector2(SideMargin, TopRowY);
            var bg = chip.gameObject.AddComponent<Image>();
            bg.sprite = ProcSprites.RoundRect(15);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(UiKit.Good.r, UiKit.Good.g, UiKit.Good.b, 0.16f);
            bg.raycastTarget = false;
            var outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(UiKit.Good.r, UiKit.Good.g, UiKit.Good.b, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            _chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
            _chipGroup.blocksRaycasts = false;
            _chipGroup.interactable = false;
            _chipRt = chip;

            _chipText = UiKit.Text(chip, "홀드 0", 20, ChipText, TextAnchor.MiddleCenter, true);
            _chipText.horizontalOverflow = HorizontalWrapMode.Overflow;

            _hintText = UiKit.Text(transform, "", 20, UiKit.TextSecondary, TextAnchor.MiddleRight);
            _hintText.name = "HoldHint";
            _hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hrt = _hintText.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0.5f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.sizeDelta = new Vector2(470f, 30f);
            hrt.anchoredPosition = new Vector2(-SideMargin, TopRowY);
        }

        // ── LAST SPIN! 태그 ────────────────────────────────────────────────────────────
        private void BuildLastTag()
        {
            var go = new GameObject("LastTag", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _tagRt = (RectTransform)go.transform;
            _tagRt.SetParent(transform, false);
            _tagRt.anchorMin = _tagRt.anchorMax = new Vector2(0.5f, 0.5f);
            _tagRt.pivot = new Vector2(0.5f, 0f);
            _tagRt.sizeDelta = new Vector2(236f, 32f);
            _tagRt.anchoredPosition = new Vector2(0f, TopRowY);
            var bg = go.GetComponent<Image>();
            bg.sprite = ProcSprites.RoundRect(16);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.86f, 0.16f, 0.24f, 1f);
            bg.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.70f, 0.73f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            _tagGroup = go.GetComponent<CanvasGroup>();
            _tagGroup.alpha = 0f;
            _tagGroup.blocksRaycasts = false;
            _tagGroup.interactable = false;

            var label = UiKit.Text(_tagRt, "LAST SPIN!", 22, Color.white, TextAnchor.MiddleCenter, true);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Fill(label.rectTransform);
            _tagRt.gameObject.SetActive(false);
        }

        // ── 피버 게이지 바 ─────────────────────────────────────────────────────────────
        private void BuildFeverBar()
        {
            var go = new GameObject("FeverBar", typeof(RectTransform), typeof(Image));
            _feverRt = (RectTransform)go.transform;
            _feverRt.SetParent(transform, false);
            _feverRt.anchorMin = new Vector2(0f, 0.5f);
            _feverRt.anchorMax = new Vector2(1f, 0.5f);
            _feverRt.pivot = new Vector2(0.5f, 1f);
            _feverRt.sizeDelta = new Vector2(-SideMargin * 2f, BarHeight);
            _feverRt.anchoredPosition = new Vector2(0f, -(CellHalf + BarGap));
            var track = go.GetComponent<Image>();
            track.sprite = ProcSprites.RoundRect(13);
            track.type = Image.Type.Sliced;
            track.color = new Color(0.04f, 0.05f, 0.09f, 1f);
            track.raycastTarget = false;
            _trackOutline = go.AddComponent<Outline>();
            _trackOutline.effectColor = new Color(1f, 0.55f, 0.16f, 0.4f);
            _trackOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _fillRt = (RectTransform)fill.transform;
            _fillRt.SetParent(_feverRt, false);
            _fillRt.anchorMin = Vector2.zero;
            _fillRt.anchorMax = new Vector2(0f, 1f);
            _fillRt.offsetMin = new Vector2(2f, 2f);
            _fillRt.offsetMax = new Vector2(-2f, -2f);
            _fillImg = fill.GetComponent<Image>();
            _fillImg.sprite = ProcSprites.RoundRect(11);
            _fillImg.type = Image.Type.Sliced;
            _fillImg.color = new Color(1f, 0.55f, 0.05f, 1f);
            _fillImg.raycastTarget = false;

            _feverText = UiKit.Text(_feverRt, "", 18, Color.white, TextAnchor.MiddleCenter, true);
            _feverText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Fill(_feverText.rectTransform);
            var textOutline = _feverText.gameObject.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 갱신 API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>홀드 칩/안내 갱신. hold==null(심화)이면 숨긴다. capable=지금 칸을 탭해 홀드할 수 있는 상태.</summary>
        public void SetHold(HoldView hold, bool capable, RunState run)
        {
            if (_chipRt == null) return;
            if (hold == null)
            {
                _chipRt.gameObject.SetActive(false);
                if (_hintText != null) _hintText.text = "";
                return;
            }
            _chipRt.gameObject.SetActive(true);
            int held = hold.held != null ? hold.held.Count : 0;
            _chipText.text = held > 0 ? $"홀드 {hold.charges}  ({held}/{hold.max})" : $"홀드 {hold.charges}";
            _chipGroup.alpha = (hold.charges < 1 && held == 0) ? 0.45f : 1f;   // 0 이면 흐리게(웹 .hold-chip.off)

            string hint = "";
            if (capable) hint = held > 0 ? $"{held}칸 고정 · 다음 스핀에 유지" : "탭: 홀드 · 길게: 정보";
            else if (run != null && run.Phase == RunPhase.Spin && run.SpinIndex > 0 && hold.charges < 1) hint = "홀드 충전 없음 (집중 +1)";
            _hintText.text = hint;
            _hintText.color = (capable && held > 0) ? ChipText : UiKit.TextSecondary;
        }

        /// <summary>미클리어 마지막 스핀 대기 중 LAST SPIN! 태그 펄스(웹 .last-tag.on).</summary>
        public void SetLastSpin(bool on)
        {
            if (_tagRt == null || on == _tagOn) return;
            _tagOn = on;
            if (_tagPulse != null) { StopCoroutine(_tagPulse); _tagPulse = null; }
            if (on)
            {
                _tagRt.gameObject.SetActive(true);
                _tagPulse = StartCoroutine(TagPulseRoutine());
            }
            else
            {
                _tagGroup.alpha = 0f;
                _tagRt.gameObject.SetActive(false);
            }
        }

        private IEnumerator TagPulseRoutine()
        {
            _tagGroup.alpha = 0f;
            _tagRt.localScale = Vector3.one * 0.7f;
            yield return UiTween.FloatRoutine(0f, 1f, 0.25f, k =>
            {
                if (_tagGroup != null) _tagGroup.alpha = k;
                if (_tagRt != null) _tagRt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, k);
            }, UiTween.Ease.OutBack);
            while (_tagRt != null)
            {
                yield return UiTween.ScaleRoutine(_tagRt, Vector3.one, Vector3.one * 1.07f, 0.35f, UiTween.Ease.OutQuad);
                if (_tagRt == null) yield break;
                yield return UiTween.ScaleRoutine(_tagRt, Vector3.one * 1.07f, Vector3.one, 0.35f, UiTween.Ease.OutQuad);
            }
        }

        /// <summary>피버 게이지 바 갱신(릴 아래). fever==null(심화)이면 바를 숨긴다. animate 면 채움을 부드럽게 늘린다.</summary>
        public void SetFever(FeverView fever, bool animate)
        {
            if (_feverRt == null) return;
            if (fever == null)
            {
                _feverRt.gameObject.SetActive(false);
                StopGlow();
                return;
            }
            _feverRt.gameObject.SetActive(true);

            bool active = fever.spins > 0;
            float pct = active
                ? Mathf.Clamp01(fever.spins / (float)Formulas.FEVER_SPINS)
                : Mathf.Clamp01(fever.gauge / (float)Formulas.FEVER_MAX);
            int pctInt = Mathf.RoundToInt(pct * 100f);
            bool hot = !active && pctInt >= 90;

            _feverText.text = active
                ? $"FEVER ×{NumberFormat.Fmt(Formulas.FEVER_EXP_MUL)} · 남은 {fever.spins}스핀"
                : $"피버 게이지 {pctInt}%{(hot ? " · 곧 폭발!" : "")}";

            SetGlowMode(active ? 2 : (hot ? 1 : 0));
            AnimateFill(pct, animate);
        }

        private void AnimateFill(float target, bool animate)
        {
            if (_fillRoutine != null) { StopCoroutine(_fillRoutine); _fillRoutine = null; }
            if (!animate || !isActiveAndEnabled || Mathf.Approximately(_fillPct, target))
            {
                SetFill(target);
                return;
            }
            _fillRoutine = StartCoroutine(FillRoutine(_fillPct, target));
        }

        private IEnumerator FillRoutine(float from, float to)
        {
            yield return UiTween.FloatRoutine(from, to, FillAnimDuration, SetFill, UiTween.Ease.OutCubic);
            _fillRoutine = null;
        }

        private void SetFill(float pct)
        {
            _fillPct = pct;
            if (_fillRt != null) _fillRt.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
        }

        // 평상=주황 / 곧 폭발=골드 펄스+테두리 번쩍(웹 .fever-wrap.hot) / 피버 중=붉은-금 흐름(웹 .active).
        private void SetGlowMode(int mode)
        {
            if (mode == _glowMode && (mode == 0 || _glowRoutine != null)) return;
            _glowMode = mode;
            StopGlow();
            if (mode == 0)
            {
                if (_fillImg != null) _fillImg.color = new Color(1f, 0.55f, 0.05f, 1f);
                if (_trackOutline != null) _trackOutline.effectColor = new Color(1f, 0.55f, 0.16f, 0.4f);
                return;
            }
            if (isActiveAndEnabled) _glowRoutine = StartCoroutine(GlowRoutine(mode));
        }

        private void StopGlow()
        {
            if (_glowRoutine != null) { StopCoroutine(_glowRoutine); _glowRoutine = null; }
        }

        private IEnumerator GlowRoutine(int mode)
        {
            Color a = mode == 2 ? new Color(1f, 0.22f, 0.02f, 1f) : new Color(1f, 0.55f, 0.05f, 1f);
            Color b = mode == 2 ? new Color(1f, 0.88f, 0.40f, 1f) : new Color(1f, 0.82f, 0.25f, 1f);
            float half = mode == 2 ? 0.55f : 0.25f;
            while (_fillImg != null)
            {
                yield return UiTween.FloatRoutine(0f, 1f, half, k => ApplyGlow(a, b, k, mode), UiTween.Ease.OutQuad);
                if (_fillImg == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0f, half, k => ApplyGlow(a, b, k, mode), UiTween.Ease.OutQuad);
            }
        }

        private void ApplyGlow(Color a, Color b, float k, int mode)
        {
            if (_fillImg != null) _fillImg.color = Color.Lerp(a, b, k);
            if (_trackOutline != null)
                _trackOutline.effectColor = new Color(1f, mode == 2 ? 0.70f : 0.82f, 0.25f, Mathf.Lerp(0.55f, 1f, k));
        }

        /// <summary>새 런 시작/화면 이탈 — 전부 초기 상태로.</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            _fillRoutine = null;
            _glowRoutine = null;
            _tagPulse = null;
            _tagOn = false;
            _glowMode = 0;
            if (_tagRt != null) _tagRt.gameObject.SetActive(false);
            SetFill(0f);
            SetHold(null, false, null);
            SetFever(null, false);
        }

        private void OnDisable()
        {
            // 코루틴이 멈추므로 진행 중이던 상태 플래그를 되돌린다(다시 켜질 때 SetXxx 가 재시작한다).
            _fillRoutine = null;
            _glowRoutine = null;
            _tagPulse = null;
            _glowMode = 0;
            _tagOn = false;
            if (_tagRt != null) _tagRt.gameObject.SetActive(false);
        }
    }
}
