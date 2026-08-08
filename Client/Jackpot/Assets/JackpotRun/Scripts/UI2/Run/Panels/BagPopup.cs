using System;
using System.Collections;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 가방 팝업 — ENGINE_PORT_DESIGN.md S7 Run/Panels/BagPopup.cs. 🎒 버튼으로 아무 때나 열 수 있는
    // 모달(스크림 클릭으로 닫힘) — 페이즈 패널과 달리 RunPhase와 무관하게 독립적으로 뜬다.
    // 이관 원본: Scripts/UI/RunPanels.cs의 ShowBag.
    public sealed class BagPopup : MonoBehaviour
    {
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시,
        // 이전엔 중앙 팝업 scale-in 0.25s OutBack이었다).
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Button scrimButton; // 배경 클릭 시 닫힘
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private CanvasGroup dimGroup; // S12c §6 — 배경 딤 페이드(BuildSheetChrome.dimGroup)
        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate; // 자식 경로 계약: Icon/IconEmoji/Name/Desc/UseButton
        [SerializeField] private Text emptyText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            // Opus 2차검수 필수⑤(2026-08-09, P4 REWARD_DONE/셀 정보 탭 슬라이스 발견 — 기존 결함,
            // CellInfoSheet.cs Awake 주석에 전체 메커니즘 설명) — 여기서 gameObject.SetActive(false)를
            // 부르면 안 된다. 빌더(BuildBagPopup→BuildSheetChrome)가 이미 비활성으로 굽는다 — Show()가
            // 처음 SetActive(true)를 부르는 그 프레임에 Awake가 지연 실행되며, 이 안의 SetActive(false)가
            // 그 활성화를 스스로 되돌려 최초 오픈 1회만 EnterRoutine 코루틴이 조용히 실패했다.
            if (dimGroup != null) dimGroup.alpha = 0f;
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        public void Show(RunState run, Action<string> onUse)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            // S8 항목⑤: 🎒(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (titleText != null) titleText.text = $"가방 ({run.Items.Count}/{ItemUse.EffectiveSlots(run)})";
            if (emptyText != null) emptyText.gameObject.SetActive(run.Items.Count == 0);

            BuildRows(run, onUse);

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

        // S12c §6 — 시트 슬라이드업(0.24s OutCubic) + 배경 딤 페이드 동시 재생.
        private IEnumerator EnterRoutine()
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
        }

        private void BuildRows(RunState run, Action<string> onUse)
        {
            if (rowsContent == null || rowTemplate == null) return;
            for (int i = rowsContent.childCount - 1; i >= 0; i--)
            {
                var child = rowsContent.GetChild(i);
                if (child == rowTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < run.Items.Count; i++)
            {
                var item = Items.ById(run.Items[i]);
                if (item == null) continue;

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "ItemRow_" + i;

                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get("item_" + item.id));
                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = item.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = item.name;
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = item.desc;

                string itemId = run.Items[i];
                var useBtn = row.Find("Content/UseButton")?.GetComponent<Button>();
                if (useBtn != null)
                {
                    useBtn.onClick.RemoveAllListeners();
                    useBtn.onClick.AddListener(() =>
                    {
                        onUse(itemId);
                        Hide();
                    });
                }
            }

            BuildPerkLevelRows(run);
        }

        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12, 작업 지시 B.4) — 레벨업 가능한 보유 증강에
        // Lv 뱃지 표시. run.Perks를 그리는 화면이 UI2에 이 팝업 말고 없어(grep 결과 전무 — 작업 지시의
        // "BagPopup?"이 유일한 후보였다) 최소 침습으로 기존 아이템 행 템플릿을 그대로 재사용한다(새
        // 자식 노드 없음, Icon/Name/Desc만 채우고 UseButton은 숨김 — 증강은 소모형 아이템과 달리
        // "사용" 동작이 없는 상시 효과). AUG_LEVELS 등록 증강(AugLevels.IsLevelable)만 대상 — 레벨
        // 개념이 없는 나머지 증강/유물/저주는 여기 표시 대상이 아니다(뱃지 자체가 무의미).
        private void BuildPerkLevelRows(RunState run)
        {
            if (rowsContent == null || rowTemplate == null) return;
            for (int i = 0; i < run.Perks.Count; i++)
            {
                var perkId = run.Perks[i];
                if (!AugLevels.IsLevelable(perkId)) continue;
                var perk = Perks.ById(perkId);
                if (perk == null) continue;
                int lv = run.PerkLevels.TryGetValue(perkId, out var lvv) ? lvv : 1;

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "PerkLevelRow_" + perkId;

                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get("aug_" + perk.id));
                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = perk.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = $"{perk.name} Lv.{lv}";
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = perk.desc;

                var useBtn = row.Find("Content/UseButton")?.GetComponent<Button>();
                if (useBtn != null) useBtn.gameObject.SetActive(false); // 소모형 아님 — 사용 버튼 숨김
            }
        }
    }
}
