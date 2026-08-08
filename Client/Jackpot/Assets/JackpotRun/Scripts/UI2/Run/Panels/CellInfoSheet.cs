using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 셀 정보 탭 바텀시트 — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16, 웹 openCellSheet, ui.js:959-1010).
    // ReelView 셀 탭 → CellInfoView.Build(run, idx)로 산출한 표시 전용 분해값을 그린다. RunPhase와 무관하게
    // 언제든 뜰 수 있는 독립 팝업(BagPopup과 동일 취지 — 스크림 클릭으로 닫힘).
    public sealed class CellInfoSheet : MonoBehaviour
    {
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text tagsText;
        [SerializeField] private Text specialsText;

        [Header("EXP/점수 분해 — 자식 경로: Inner/Label·Value")]
        [SerializeField] private RectTransform calcRowsContent;
        [SerializeField] private RectTransform calcRowTemplate;

        [SerializeField] private Text muNoteText;   // 전체 배수 안내
        [SerializeField] private Text setNoteText;  // 세트 보너스 고정 안내(웹 cb-note 그대로)

        [Header("이 칸에 영향 주는 항목 — 자식 경로: Content/IconSlot/Icon·IconEmoji, Content/InfoCol/Name·Kind·Desc")]
        [SerializeField] private RectTransform affectingRowsContent;
        [SerializeField] private RectTransform affectingRowTemplate;
        [SerializeField] private Text affectingEmptyText;

        [SerializeField] private Text setsText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (calcRowTemplate != null) calcRowTemplate.gameObject.SetActive(false);
            if (affectingRowTemplate != null) affectingRowTemplate.gameObject.SetActive(false);
            // Opus 2차검수 필수⑤(2026-08-09) — 여기서 gameObject.SetActive(false)를 부르면 안 된다.
            // 빌더(UiSceneBuilder.BuildCellInfoSheet→BuildSheetChrome)가 이 오브젝트를 이미 비활성으로
            // 구워 둔다 — 씬 로드 시점엔 비활성이라 Awake 자체가 지연되고, Show()가 처음 호출돼
            // SetActive(true)로 활성화되는 바로 그 프레임에 Awake가 동기 실행된다. 그 안에서 다시
            // SetActive(false)를 부르면 Show()가 막 켠 활성 상태를 스스로 되돌려 다음 줄의
            // StartCoroutine(EnterRoutine())이 "비활성 오브젝트에서 코루틴 시작 불가" 오류로 조용히
            // 실패한다(최초 오픈 1회만 재현 — 이후엔 Awake가 다시 안 불려 정상 동작해 보임). 빌더가
            // 굽는 초기 비활성 상태에 그대로 의존한다(RewardDonePanel/PostSpinPanel과 동일 관례).
            if (dimGroup != null) dimGroup.alpha = 0f;
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        /// <summary>run/idx로 CellInfoView.Build를 호출해 채운다. 산출값이 null(직전 스핀 정보 없음)이면
        /// 아무것도 하지 않는다(웹 openCellSheet의 `if (!info) return;` 그대로 — 시트 자체를 열지 않음).</summary>
        public void Show(RunState run, int idx)
        {
            var info = CellInfoView.Build(run, idx);
            if (info == null) return;

            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            string pos = info.isCenter ? " · 가운데 칸" : (info.idx == 0 ? " · 첫 칸" : "");
            if (titleText != null)
                titleText.text = TextSanitize.StripAstral($"{info.sym.emoji} {info.sym.name} · {info.idx + 1}번 칸{pos}");

            if (tagsText != null)
            {
                var tags = info.sym.tags;
                string joined = (tags != null && tags.Length > 0) ? string.Join("  ", TagChips(tags)) : "";
                tagsText.text = joined;
                tagsText.gameObject.SetActive(!string.IsNullOrEmpty(joined));
            }

            if (specialsText != null)
            {
                // Opus 2차검수 필수③ — CellInfoView 산출 문자열은 지금은 astral-free지만, 콘텐츠(퍽/
                // 장치 desc 등)가 특수효과 문구에 섞여 들어갈 여지가 있어 다른 필드(title/name)와
                // 동일하게 표시 직전 방어적으로 새니타이즈한다(TextSanitize 관례).
                string joined = info.specials.Count > 0 ? "▸ " + string.Join("\n▸ ", info.specials) : "";
                specialsText.text = TextSanitize.StripAstral(joined);
                specialsText.gameObject.SetActive(info.specials.Count > 0);
            }

            BuildCalcRows(info);

            if (muNoteText != null)
            {
                string flat = info.flatExp != 0 ? $" · 스핀마다 +{info.flatExp}" : "";
                muNoteText.text = $"전체 배수: 모든 칸 합계에 EXP ×{FmtMul(info.expMul)}{flat} 가 곱·가산되어 최종 획득 EXP가 됩니다.";
            }
            if (setNoteText != null)
                setNoteText.text = "세트 보너스: 같은 그림을 2개 이상 모으면 추가 EXP·점수! 2개 +8 · 3개 +18 · 4개 +42 · 5개 +100 EXP.";

            BuildAffectingRows(info);

            if (setsText != null)
            {
                if (info.sets.Count > 0)
                {
                    var lines = new List<string>();
                    for (int i = 0; i < info.sets.Count; i++) lines.Add($"{info.sets[i].name} — {info.sets[i].desc}");
                    // Opus 2차검수 필수③ — SetEffect.name/desc(Sets.cs 콘텐츠)는 astral 이모지를 담을 수
                    // 있어 표시 직전 새니타이즈.
                    setsText.text = TextSanitize.StripAstral("활성 세트 효과\n" + string.Join("\n", lines));
                    setsText.gameObject.SetActive(true);
                }
                else setsText.gameObject.SetActive(false);
            }

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

        private IEnumerator EnterRoutine()
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
        }

        private static IEnumerable<string> TagChips(IReadOnlyList<string> tags)
        {
            for (int i = 0; i < tags.Count; i++) yield return "#" + tags[i];
        }

        // ── EXP/점수 분해 (웹 openCellSheet calc/score 블록, ui.js:963-979) ──────────────────
        private void BuildCalcRows(CellInfoResult info)
        {
            if (calcRowsContent == null || calcRowTemplate == null) return;
            for (int i = calcRowsContent.childCount - 1; i >= 0; i--)
            {
                var child = calcRowsContent.GetChild(i);
                if (child == calcRowTemplate) continue;
                Destroy(child.gameObject);
            }

            if (info.empty)
            {
                AddCalcRow("이 칸", "EXP 0 (제거됨)", true);
                return;
            }

            AddCalcRow("기본 EXP", info.baseExp.ToString(), false);
            if (info.perSymExp != 0) AddCalcRow($"심볼 보너스 {info.sym.name}", Signed(info.perSymExp), false);
            for (int i = 0; i < info.tagBonuses.Count; i++)
                AddCalcRow($"태그 보너스 #{info.tagBonuses[i].tag}", "+" + info.tagBonuses[i].value, false);
            if (info.skullExp != 0) AddCalcRow("해골 EXP", Signed(info.skullExp), false);
            if (info.centerMul != 1.0) AddCalcRow("가운데 칸 배수", "×" + FmtMul(info.centerMul), false);
            AddCalcRow("= 이 칸 EXP", info.cellExp.ToString(), true);

            if (info.cellScore != 0)
            {
                AddCalcRow("기본 점수", info.baseScore.ToString(), false);
                if (info.perSymScore != 0) AddCalcRow("심볼 점수 보너스", Signed(info.perSymScore), false);
                if (info.skullScore != 0) AddCalcRow("해골 점수", Signed(info.skullScore), false);
                AddCalcRow("= 이 칸 점수", info.cellScore.ToString(), true);
            }
        }

        private void AddCalcRow(string label, string value, bool total)
        {
            var row = Instantiate(calcRowTemplate, calcRowsContent);
            row.gameObject.SetActive(true);
            var l = row.Find("Inner/Label")?.GetComponent<Text>();
            var v = row.Find("Inner/Value")?.GetComponent<Text>();
            if (l != null) { l.text = label; l.color = total ? UiKit.TextPrimary : UiKit.TextSecondary; }
            if (v != null) { v.text = value; v.color = total ? UiKit.Accent : UiKit.TextPrimary; }
        }

        // ── 이 칸에 영향 주는 증강·유물·캐릭터·저주 (웹 ui.js:982-984) ───────────────────────
        private void BuildAffectingRows(CellInfoResult info)
        {
            if (affectingRowsContent == null || affectingRowTemplate == null) return;
            for (int i = affectingRowsContent.childCount - 1; i >= 0; i--)
            {
                var child = affectingRowsContent.GetChild(i);
                if (child == affectingRowTemplate) continue;
                Destroy(child.gameObject);
            }

            if (affectingEmptyText != null) affectingEmptyText.gameObject.SetActive(info.affecting.Count == 0);

            for (int i = 0; i < info.affecting.Count; i++)
            {
                var a = info.affecting[i];
                var row = Instantiate(affectingRowTemplate, affectingRowsContent);
                row.gameObject.SetActive(true);
                row.name = "Affect_" + i;

                string catalogId = CatalogIdFor(a);
                var sprite = string.IsNullOrEmpty(catalogId) ? null : JackpotCatalog.LoadSprite(JackpotCatalog.Get(catalogId));
                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = TextSanitize.StripAstral(a.emoji); iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = TextSanitize.StripAstral(a.name);
                var kindText = row.Find("Content/InfoCol/Kind")?.GetComponent<Text>();
                if (kindText != null) kindText.text = string.IsNullOrEmpty(a.tier) ? a.kind : $"{a.kind} · {TierLabel(a.tier)}";
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = a.effect;
            }
        }

        // BagPopup/PerkOfferPanel/PickMeta.cs와 동일한 카탈로그 id 접두 관례.
        private static string CatalogIdFor(AffectingEntry a)
        {
            switch (a.kind)
            {
                case "증강": return "aug_" + a.id;
                case "유물": return "rel_" + a.id;
                case "저주": return "cur_" + a.id;
                case "캐릭터": return "char_" + a.id;
                default: return null;
            }
        }

        private static string TierLabel(string tier)
        {
            switch (tier)
            {
                case "SILVER": return "실버";
                case "GOLD": return "골드";
                case "PRISM": return "프리즘";
                default: return tier;
            }
        }

        private static string Signed(long v) => (v > 0 ? "+" : "") + v;

        private static string FmtMul(double v)
        {
            string s = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
    }
}
