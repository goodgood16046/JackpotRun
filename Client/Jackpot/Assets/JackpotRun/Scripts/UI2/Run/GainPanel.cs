using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 스핀 결과 패널(Gain Panel) — ENGINE_PORT_DESIGN.md S16 "스핀 결과 패널: '얼마나·왜 얻었는지'".
    // 릴 아래 자리(구 StageInfo)를 대체한다 — 사용자 피드백 "현재 스핀에서 점수를 얼마나 얻었고 뭐
    // 때문에 얻었는지가 있어야 하는데 없다"에 대한 응답. 엔진 값을 표시 전용으로만 읽는다(재계산 금지,
    // 최종 EXP/점수는 항상 outcome.gained/result.exp/result.score/result.coins 원본 그대로).
    // HudView.cs/NotesFeed.cs와 동일 스타일(SerializeField로 빌더가 자식을 꽂아 준다).
    //
    // 자식 경로 계약(Editor/UiSceneBuilder.cs BuildRunGainPanel이 이 형태로 짓는다):
    //   bigNumberText            — "+{N} EXP" 대문짝(Text)
    //   scoreChipRoot/Bg/Label   — 점수 칩(BuildAutoPill 산출물)
    //   coinChipRoot/Bg/Label    — 코인 칩(BuildAutoPill 산출물)
    //   rowsContent/rowTemplate  — 기여 내역 행 스택. rowTemplate 자식 경로: "Inner/Label"(Text)·
    //                              "Inner/Value"(Text), 루트에 CanvasGroup(스태거 페이드용).
    //                              "Inner"는 rowsContent의 VerticalLayoutGroup이 직접 건드리지 않는
    //                              손자뻘 RectTransform이라, 행이 추가/제거되며 레이아웃이 재계산돼도
    //                              진행 중인 위치 트윈(8px 상승)이 덮어써지지 않는다.
    //   setExplainRoot/Text      — 세트 성립 시에만 노출하는 보라 테두리 설명 박스.
    public sealed class GainPanel : MonoBehaviour
    {
        // ── 대문짝 카운트업 + 팝인 ──────────────────────────────────────────────────
        private const float BigCountUpDuration = 0.35f; // 설계 명시: "0.35s 카운트업(OutCubic)"
        private const float BigPopDuration = 0.3f;       // 설계 미명시 — 팝인 자체 길이 기본값
        private const float BigPopScaleFrom = 0.8f;      // 설계 명시: "scale 0.8→1"

        // ── 기여 내역 줄 스태거 등장 ────────────────────────────────────────────────
        private const float RowStagger = 0.05f;        // 설계 명시: "0.05s 스태거"
        private const float RowRiseDistance = 8f;      // 설계 명시: "8px 상승"
        private const float RowRevealDuration = 0.22f; // 설계 미명시 — 개별 행 페이드+상승 길이 기본값

        [SerializeField] private Text bigNumberText;

        [SerializeField] private RectTransform scoreChipRoot;
        [SerializeField] private Image scoreChipBg;
        [SerializeField] private Text scoreChipLabel;
        [SerializeField] private RectTransform coinChipRoot;
        [SerializeField] private Image coinChipBg;
        [SerializeField] private Text coinChipLabel;

        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate;

        [SerializeField] private RectTransform setExplainRoot;
        [SerializeField] private Text setExplainText;

        private readonly List<RectTransform> _rows = new List<RectTransform>();

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            // HudView.Awake의 배너 alpha=0 관례와 동일 — RunView.InitForRun의 Clear() 호출에만 기대지
            // 않고, 씬 로드 직후(첫 스핀 전) 빈 칩/설명 박스가 한 프레임이라도 보이지 않게 방어한다.
            HideAll();
        }

        /// <summary>다음 스핀 시작·런 시작 시 잔상 제거 — 전부 숨긴다(S16 규칙: "다음 스핀 시작 시
        /// 초기화, 이전 값 잔상 금지").</summary>
        public void Clear()
        {
            StopAllCoroutines();
            ClearRows();
            HideAll();
        }

        private void HideAll()
        {
            if (bigNumberText != null) bigNumberText.text = "";
            if (scoreChipRoot != null) scoreChipRoot.gameObject.SetActive(false);
            if (coinChipRoot != null) coinChipRoot.gameObject.SetActive(false);
            if (setExplainRoot != null) setExplainRoot.gameObject.SetActive(false);
        }

        /// <summary>이번 스핀 결과를 표시하고 연출을 재생한다. outcome.result가 없으면(방어적으로만
        /// 발생 가능 — 호출측 RunView는 이미 result!=null을 확인하고 부른다) 그냥 비운다.</summary>
        public void Show(SpinOutcome outcome)
        {
            var result = outcome?.result;
            if (result == null) { Clear(); return; }

            StopAllCoroutines();
            ClearRows();
            StartCoroutine(ShowRoutine(outcome, result));
        }

        private IEnumerator ShowRoutine(SpinOutcome outcome, SpinResult result)
        {
            // 칩 2개 — 값이 0이면 숨긴다(설계 명시). 카운트업 대상으로 명시되지 않아 즉시 표시.
            SetChip(scoreChipRoot, scoreChipBg, scoreChipLabel, "점수", result.score, UiKit.Blue);
            SetChip(coinChipRoot, coinChipBg, coinChipLabel, "코인", result.coins, UiKit.Accent);

            // 세트 설명 박스 — 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목①): count>=2인
            // 모든 값심볼 그룹이 각각 보너스를 받으므로(setIds), 트리거도 bestSetCount 단일 전제 대신
            // setIds 소스로 통일(값은 사실상 동치 — 어떤 그룹이든 count>=2가 있으면 bestCount도 >=2).
            bool showSetExplain = result.setIds != null && result.setIds.Count > 0;
            if (setExplainRoot != null) setExplainRoot.gameObject.SetActive(showSetExplain);
            if (showSetExplain && setExplainText != null)
            {
                setExplainText.text = result.setIds.Count > 1
                    ? $"같은 심볼 세트가 동시에 {result.setIds.Count}개 형성됐어요 — 개수가 많을수록 보너스가 크게 늘어납니다"
                    : $"같은 심볼 {result.bestSetCount}개 — 개수가 많을수록 보너스가 크게 늘어납니다";
            }

            // 대문짝 — N은 outcome.gained(모드·보스·안전장치까지 반영된 실제 획득 EXP).
            if (bigNumberText != null)
            {
                bigNumberText.text = "+0 EXP";
                bigNumberText.rectTransform.localScale = Vector3.one * BigPopScaleFrom;
                StartCoroutine(UiTween.ScaleRoutine(bigNumberText.rectTransform,
                    Vector3.one * BigPopScaleFrom, Vector3.one, BigPopDuration, UiTween.Ease.OutBack));
                yield return UiTween.CountUpRoutine(0, outcome.gained, BigCountUpDuration,
                    v => bigNumberText.text = $"+{NumberFormat.Comma(v)} EXP", UiTween.Ease.OutCubic);
            }

            var lines = ComputeLines(result, outcome);
            yield return BuildRowsRoutine(lines);
        }

        private static void SetChip(RectTransform root, Image bg, Text label, string title, long amount, Color bgColor)
        {
            if (root == null) return;
            // "값이 0이면 그 칩은 숨긴다"(설계 명시) — score/coins는 엔진이 Math.Max(...,0)으로 클램프해
            // 음수가 나오지 않는다(SpinResolver.Evaluate 반환부).
            bool show = amount != 0;
            root.gameObject.SetActive(show);
            if (!show) return;
            if (bg != null) bg.color = bgColor;
            if (label != null)
            {
                label.text = $"{title} +{NumberFormat.Comma(amount)}";
                label.color = UiKit.Bg; // 밝은 배경 위 대비(ShopPanel PriceButton의 Accent배경+Bg글자 관례와 동일).
            }
        }

        // ── 기여 내역 행 스폰 + 스태거 등장 ──────────────────────────────────────────
        private IEnumerator BuildRowsRoutine(List<GainLine> lines)
        {
            if (rowsContent == null || rowTemplate == null) yield break;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "GainRow_" + i;
                _rows.Add(row);

                var inner = row.Find("Inner") as RectTransform;
                var label = row.Find("Inner/Label")?.GetComponent<Text>();
                var value = row.Find("Inner/Value")?.GetComponent<Text>();
                if (label != null) { label.text = line.label; label.color = line.color; }
                if (value != null) { value.text = line.value; value.color = line.color; }

                var cg = row.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                if (inner != null) inner.anchoredPosition = new Vector2(0f, -RowRiseDistance);

                StartCoroutine(RowRevealRoutine(inner, cg));
                if (i < lines.Count - 1) yield return new WaitForSeconds(RowStagger);
            }
        }

        private IEnumerator RowRevealRoutine(RectTransform inner, CanvasGroup cg)
        {
            if (inner == null) yield break;
            Vector2 from = inner.anchoredPosition;
            float t = 0f;
            while (t < RowRevealDuration)
            {
                if (inner == null) yield break;
                t += Time.deltaTime;
                float e = UiTween.Apply(UiTween.Ease.OutCubic, t / RowRevealDuration);
                inner.anchoredPosition = Vector2.LerpUnclamped(from, Vector2.zero, e);
                if (cg != null) cg.alpha = e;
                yield return null;
            }
            if (inner != null) inner.anchoredPosition = Vector2.zero;
            if (cg != null) cg.alpha = 1f;
        }

        private void ClearRows()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();
        }

        // ── 기여 내역 계산 (표시 전용 분해) ──────────────────────────────────────────
        private readonly struct GainLine
        {
            public readonly string label;
            public readonly string value;  // 화면 표시용 완성 문자열("+12"·"×1.45" 등)
            public readonly long amount;   // 축약(D단계) 비교/합산용 — 배율 줄은 합산 불가라 0 고정
            public readonly Color color;

            public GainLine(string label, string value, long amount, Color color)
            {
                this.label = label;
                this.value = value;
                this.amount = amount;
                this.color = color;
            }
        }

        // SpinResult가 이미 갖고 있는 값만으로 "왜 얻었는지" 분해선을 만든다(엔진 재계산 금지).
        // preMul을 목표합으로 놓고 심볼기본→세트→해골 순으로 채운 뒤 남는 오차(폭탄/자석/열쇠/코인/
        // 주사위/태그가산/인접/양끝/불꽃/첫막스핀·희귀버스트 등 신규16종 배수 — 여기서 항목화하지
        // 않는 전부)를 "기타"로 흡수한다(지시 원문: "오차를 숨기지 마라"). 잭팟 고정가산·총배율 캡은
        // preMul 계산 시점(전역배수 적용 전) 이후에 붙는 값이라 이 분해 대상 밖이다 — 잭팟 발생 시
        // NotesFeed(최근 3줄)에 별도로 뜬다(재해석 보고 대상, 작업 지시가 A/B/C 항목만 규정).
        private static List<GainLine> ComputeLines(SpinResult r, SpinOutcome outcome)
        {
            // ── A. 배수 적용 전 줄들(목표 합계 = r.preMul) ──────────────────────────
            var a = new List<GainLine>();
            long sumA = 0;

            // 심볼 기본 — Symbols.ValueIds 선언 순서(엔진 tie-break와 동일: 체리·별·책·보석·왕관).
            for (int i = 0; i < Symbols.ValueIds.Length; i++)
            {
                var info = Symbols.BySym(Symbols.ValueIds[i]);
                if (r.counts != null && r.counts.TryGetValue(info.id, out var cnt) && cnt > 0)
                {
                    long v = (long)cnt * info.exp;
                    a.Add(new GainLine($"{info.name} ×{cnt}", SignedAmount(v), v, UiKit.TextSecondary));
                    sumA += v;
                }
            }

            // 세트 보너스 — 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목①): count>=2인
            // 모든 값심볼 그룹이 각각 지급되므로(r.setIds, 구 bestSetCount 단일 전제 결함 수정) 그룹별로
            // 한 줄씩 항목화한다(2개 이상 동시 형성은 희소하지만 발생 시 둘 다 정확히 보여야 "기타" 잔차로
            // 조용히 흡수되지 않는다 — 지시 원문 "오차를 숨기지 마라"). Opus 2차검수 권장⑤ — 이 줄의
            // 값은 `mods.setExpMul`/`twoSetBonusMul`(짝맞춤 +20%)이 곱해진 "실제 지급액"이어야 한다
            // (raw `Symbols.SetExp[n]`만 쓰면 배수 반영분이 조용히 "기타" 잔차로 흡수돼 그만큼 오차를
            // 숨기는 셈). `ComputeLines`는 설계 원칙상 Mods를 다시 계산하지 않으므로(엔진 재계산 금지),
            // 엔진이 SET 블록에서 이미 그 최종값을 적어 둔 `r.notes`의 "{emoji}×{cnt} 세트 +{add}"
            // (SpinResolver.cs, add=SetExp[n]*setExpMul*twoMul 이미 반영됨)를 파싱해 그대로 쓴다 — 못
            // 찾으면(정상 경로에선 도달 불가) raw SetExp[n]으로 안전 폴백.
            if (r.setIds != null)
            {
                for (int si = 0; si < r.setIds.Count; si++)
                {
                    var sid = r.setIds[si];
                    int cnt = (r.counts != null && r.counts.TryGetValue(sid, out var c)) ? c : 0;
                    if (cnt < 2) continue;
                    var sinfo = Symbols.ById(sid);
                    int n = Mathf.Min(cnt, Symbols.SetExp.Length - 1);
                    long v = ParseSetBonusNote(r.notes, sinfo?.emoji, cnt) ?? Symbols.SetExp[n];
                    string label = sinfo != null ? $"{sinfo.name} 세트 {cnt}연속" : $"세트 {cnt}연속";
                    a.Add(new GainLine(label, SignedAmount(v), v, UiKit.Accent));
                    sumA += v;
                }
            }

            // 해골 페널티 — "해골빌드"(보너스로 전환된 경우) 노트가 있으면 이 줄은 생략하고 잔차로
            // 흡수한다(설계 지시 명시). Formulas.SKULL_PENALTY는 mods.skullPenaltyMul을 반영하지 않는
            // 근사치라 실제 페널티와 어긋날 수 있는데, 그 오차 역시 바로 아래 "기타" 잔차가 흡수한다.
            bool skullIsBuild = r.notes != null && r.notes.Exists(n => n.Contains("해골빌드"));
            if (r.skulls > 0 && !skullIsBuild)
            {
                long v = -((long)r.skulls * Formulas.SKULL_PENALTY);
                a.Add(new GainLine($"해골 ×{r.skulls}", SignedAmount(v), v, UiKit.Bad));
                sumA += v;
            }

            // 잔차 — preMul과의 오차를 "기타"로 그대로 노출한다(오차 은폐 금지, 설계 명시).
            long residual = r.preMul - sumA;
            if (residual != 0) a.Add(new GainLine("기타", SignedAmount(residual), residual, UiKit.TextSecondary));

            // ── B. 배수/가산 줄 ──────────────────────────────────────────────────
            GainLine? mulLine = null;
            if (Math.Abs(r.mul - 1.0) > 0.0001)
                mulLine = new GainLine("배율", $"×{NumberFormat.Fmt(r.mul)}", 0, UiKit.Purple);
            GainLine? flatLine = null;
            if (r.flat != 0)
                flatLine = new GainLine("가산 효과", SignedAmount(r.flat), r.flat, UiKit.Green);

            // ── C. 모드 보정 — 스핀모드/보스/안전장치 등으로 실제 획득이 result.exp와 달라진 차액.
            // 숨기면 대문짝 숫자와 내역 합계가 안 맞는다(설계 명시) — 항상 표시. 색은 설계 미명시라
            // 부호에 맞춰 good/bad로 직관 표시.
            GainLine? modeLine = null;
            long modeDiff = outcome.gained - r.exp;
            if (modeDiff != 0)
                modeLine = new GainLine("모드 보정", SignedAmount(modeDiff), modeDiff, modeDiff > 0 ? UiKit.Green : UiKit.Bad);

            // ── D. 6줄 캡 — 초과분은 A그룹(심볼·세트·해골·잔차) 안에서만 절댓값 작은 것부터 합쳐
            // 축약한다. 배율 줄은 배수 표기라 합산이 불가능하고, 가산/모드 보정은 최대 1줄씩이라 항상
            // 보존한다(재해석 — 지시문은 "줄이 6개를 넘으면" 전체를 대상으로 말하지만, 실제로 줄 수가
            // 늘어나는 건 심볼 줄 수뿐이라 A그룹 축약만으로 총합이 항상 6줄 이하로 보장된다).
            int bcCount = (mulLine.HasValue ? 1 : 0) + (flatLine.HasValue ? 1 : 0) + (modeLine.HasValue ? 1 : 0);
            int budgetForA = Mathf.Max(6 - bcCount, 1);
            if (a.Count > budgetForA)
            {
                int etcIdx = a.FindIndex(l => l.label == "기타");
                long etcSum = 0;
                if (etcIdx >= 0) { etcSum = a[etcIdx].amount; a.RemoveAt(etcIdx); }
                int keep = Mathf.Max(budgetForA - 1, 0);
                while (a.Count > keep)
                {
                    int minIdx = 0; long minAbs = long.MaxValue;
                    for (int i = 0; i < a.Count; i++)
                    {
                        long abs = Math.Abs(a[i].amount);
                        if (abs < minAbs) { minAbs = abs; minIdx = i; }
                    }
                    etcSum += a[minIdx].amount;
                    a.RemoveAt(minIdx);
                }
                a.Add(new GainLine("기타", SignedAmount(etcSum), etcSum, UiKit.TextSecondary));
            }

            var lines = new List<GainLine>(a);
            if (mulLine.HasValue) lines.Add(mulLine.Value);
            if (flatLine.HasValue) lines.Add(flatLine.Value);
            if (modeLine.HasValue) lines.Add(modeLine.Value);
            return lines;
        }

        private static string SignedAmount(long v) => v >= 0 ? $"+{NumberFormat.Comma(v)}" : $"-{NumberFormat.Comma(-v)}";

        // 웹 파리티 P8(Opus 2차검수 권장⑤) — SpinResolver.cs SET 블록의 `notes.Add($"{emoji}×{cnt}
        // 세트 +{add}")`(add = SetExp[n]*mods.setExpMul*twoMul, 이미 배수 반영된 최종값)를 역파싱해
        // Mods를 다시 계산하지 않고도 정확한 지급액을 얻는다. 못 찾으면 null(호출측이 raw SetExp[n]로
        // 안전 폴백).
        private static long? ParseSetBonusNote(List<string> notes, string emoji, int cnt)
        {
            if (notes == null || string.IsNullOrEmpty(emoji)) return null;
            string prefix = $"{emoji}×{cnt} 세트 +";
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                if (note == null || !note.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (long.TryParse(note.Substring(prefix.Length), out var v)) return v;
            }
            return null;
        }
    }
}
