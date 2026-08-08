using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // RewardDone 페이즈 패널 — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15). 노드/상점 처리가 끝난 뒤
    // 곧장 다음 스테이지로 가지 않고 "보상 획득 → 다음 스테이지 인트로" 화면에서 대기한다(웹
    // renderRewardDone, ui.js:1897-1936). 구성(웹 순서 그대로): 보상 메시지 → 보유 효과(증강/유물/
    // 저주/장치) 목록 → 현재 능력치 → 다음 스테이지 프리뷰(요구 EXP·스핀 수·보스) → [스테이지 N 시작].
    //
    // 웹은 "보유 효과" 칩을 눌러야 상세(이름·등급·효과)가 펼쳐지는 2단 인터랙션이지만(showBuildInfo,
    // ui.js:1938-1951), 이 패널은 기존 BagPopup 행 템플릿 관례(Icon/Name/Desc 상시 노출)를 따라 처음부터
    // 전부 펼쳐서 보여준다 — 정보량은 동일하고 탭 자체가 줄어드는 단순화(Fable 최종검수 대상, 이탈 아님).
    public sealed class RewardDonePanel : MonoBehaviour
    {
        private const float SheetSlideDuration = 0.24f; // S12c §6 시트 슬라이드업 관례와 동일.

        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Text messageText;

        [Header("보유 효과 목록 — 자식 경로: Content/IconSlot/Icon·IconEmoji, Content/InfoCol/Name·Desc·Kind")]
        [SerializeField] private RectTransform buildRowsContent;
        [SerializeField] private RectTransform buildRowTemplate;
        [SerializeField] private Text buildEmptyText;

        [Header("현재 능력치 — 자식 경로: Inner/Label·Value")]
        [SerializeField] private RectTransform statRowsContent;
        [SerializeField] private RectTransform statRowTemplate;
        [SerializeField] private Text statEmptyText;
        [SerializeField] private Text symLineText; // 심볼 EXP/점수/태그 보너스 요약 한 줄(있을 때만)

        [Header("다음 스테이지 프리뷰")]
        [SerializeField] private Text nextTitleText;
        [SerializeField] private Text nextSubText;
        [SerializeField] private Text nextBossDescText;

        [SerializeField] private Button startButton;
        [SerializeField] private Text startButtonLabel;

        private void Awake()
        {
            if (buildRowTemplate != null) buildRowTemplate.gameObject.SetActive(false);
            if (statRowTemplate != null) statRowTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(RunState run, Action onProceed)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            if (messageText != null)
                messageText.text = TextSanitize.StripAstral(string.IsNullOrEmpty(run.RewardMessage) ? "보상 획득!" : run.RewardMessage);

            BuildBuildRows(run);
            BuildStatRows(run);
            BuildNextPreview(run);

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(() => onProceed());
            }

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

        // ── 보유 효과(증강/유물 → 저주 → 장치, 웹 buildIds 순서 그대로) ──────────────────────────
        private void BuildBuildRows(RunState run)
        {
            if (buildRowsContent == null || buildRowTemplate == null) return;
            for (int i = buildRowsContent.childCount - 1; i >= 0; i--)
            {
                var child = buildRowsContent.GetChild(i);
                if (child == buildRowTemplate) continue;
                Destroy(child.gameObject);
            }

            int count = 0;
            for (int i = 0; i < run.Perks.Count; i++)
            {
                var perk = Perks.ById(run.Perks[i]);
                if (perk == null) continue;
                int lv = run.PerkLevels.TryGetValue(perk.id, out var lvv) ? lvv : 1;
                string kind = perk.cat == PCat.AUGMENT ? "증강" : "유물";
                string lvSuffix = lv > 1 ? $" Lv.{lv}" : "";
                string catalogId = (perk.cat == PCat.AUGMENT ? "aug_" : "rel_") + perk.id;
                AddBuildRow(perk.id, catalogId, perk.emoji, perk.name + lvSuffix, perk.desc, $"{kind} · {TierLabel(perk.tier)}");
                count++;
            }
            for (int i = 0; i < run.Curses.Count; i++)
            {
                var curse = Perks.ById(run.Curses[i]);
                if (curse == null) continue;
                AddBuildRow(curse.id, "cur_" + curse.id, curse.emoji, curse.name, curse.desc, "저주");
                count++;
            }
            if (!string.IsNullOrEmpty(run.Device))
            {
                var dev = Devices.ById(run.Device);
                if (dev != null)
                {
                    // 장치 카탈로그 id는 접두 없이 원본 id 그대로(PickMeta.cs devKey 관례와 동일).
                    AddBuildRow(dev.id, dev.id, dev.emoji, dev.name, dev.desc, dev.kind == "PASSIVE" ? "장치 · 패시브" : "장치");
                    count++;
                }
            }
            if (buildEmptyText != null) buildEmptyText.gameObject.SetActive(count == 0);
        }

        private void AddBuildRow(string id, string catalogId, string emoji, string name, string desc, string kind)
        {
            var row = Instantiate(buildRowTemplate, buildRowsContent);
            row.gameObject.SetActive(true);
            row.name = "Build_" + id;

            var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(catalogId));
            var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
            if (iconEmoji != null) { iconEmoji.text = TextSanitize.StripAstral(emoji); iconEmoji.gameObject.SetActive(sprite == null); }

            var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
            if (nameText != null) nameText.text = TextSanitize.StripAstral(name);
            var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
            if (descText != null) descText.text = TextSanitize.StripAstral(desc);
            var kindText = row.Find("Content/InfoCol/Kind")?.GetComponent<Text>();
            if (kindText != null) kindText.text = kind;
        }

        private static string TierLabel(Tier t)
        {
            switch (t)
            {
                case Tier.SILVER: return "실버";
                case Tier.GOLD: return "골드";
                default: return "프리즘";
            }
        }

        // ── 현재 능력치(RewardDoneView.CurrentStats) ─────────────────────────────────────────
        private void BuildStatRows(RunState run)
        {
            var stats = RewardDoneView.CurrentStats(run);
            if (statRowsContent != null && statRowTemplate != null)
            {
                for (int i = statRowsContent.childCount - 1; i >= 0; i--)
                {
                    var child = statRowsContent.GetChild(i);
                    if (child == statRowTemplate) continue;
                    Destroy(child.gameObject);
                }
                for (int i = 0; i < stats.rows.Count; i++)
                {
                    var r = stats.rows[i];
                    var row = Instantiate(statRowTemplate, statRowsContent);
                    row.gameObject.SetActive(true);
                    row.name = "Stat_" + i;
                    var label = row.Find("Inner/Label")?.GetComponent<Text>();
                    var value = row.Find("Inner/Value")?.GetComponent<Text>();
                    if (label != null) label.text = r.label;
                    if (value != null) { value.text = r.value; value.color = r.up ? UiKit.Good : UiKit.Bad; }
                }
            }
            if (statEmptyText != null) statEmptyText.gameObject.SetActive(stats.rows.Count == 0);

            if (symLineText != null)
            {
                var parts = new List<string>();
                if (stats.symExp.Count > 0) parts.Add("심볼 EXP " + string.Join("  ", stats.symExp));
                if (stats.symScore.Count > 0) parts.Add("심볼 점수 " + string.Join("  ", stats.symScore));
                if (stats.tags.Count > 0) parts.Add("태그 " + string.Join("  ", stats.tags));
                symLineText.text = string.Join("\n", parts);
                symLineText.gameObject.SetActive(parts.Count > 0);
            }
        }

        // ── 다음 스테이지 프리뷰(RewardDoneView.NextPreview) ─────────────────────────────────
        private void BuildNextPreview(RunState run)
        {
            var np = RewardDoneView.NextPreview(run);
            var boss = string.IsNullOrEmpty(np.bossId) ? null : Bosses.ById(np.bossId);

            if (nextTitleText != null)
                nextTitleText.text = boss != null ? $"{TextSanitize.StripAstral(boss.emoji)} 보스 — {boss.name}" : $"다음 STAGE {np.stage}";
            if (nextBossDescText != null)
            {
                nextBossDescText.gameObject.SetActive(boss != null);
                if (boss != null) nextBossDescText.text = TextSanitize.StripAstral(boss.desc);
            }
            if (nextSubText != null)
                nextSubText.text = $"요구 EXP {NumberFormat.Comma(np.quota)} · 스핀 {np.spins}";

            if (startButtonLabel != null) startButtonLabel.text = $"스테이지 {np.stage} 시작";
        }
    }
}
