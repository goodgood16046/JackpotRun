using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20) — 범용 N-카드 선택 시트. 심화 런의 6개 오퍼
    // phase(EventPouch/EventPouchCost/EventPouchRemove/EventRestDeep/EventGambleDeep/EventSynAugBonus,
    // RunView.ShowDeepOffer가 호출)와 심화 상점 '심볼 정비' 탭의 대상 선택(ShopPanel의 repair target/
    // swap/tagbuff 선택, RepairShop.TargetsSym/TargetsTag가 후보를 공급)이 데이터 모양만 다를 뿐
    // "제목+부제 위에 N개 카드, 하나 고르면 콜백"이라는 동일 패턴을 공유한다 — PerkOfferPanel(Perk
    // 전용 복잡한 호버/보류 애니메이션)을 그대로 재사용할 수 없고(PouchOfferCard/RepairTargetSym은
    // Perk가 아니다), NodePanel의 단순 카드 리스트(Head/Body Text + Button)를 모델로 삼되 카드 개수가
    // 1~70+개까지 가변이라(addBasic 서비스는 심볼 10종 전체가 후보) 별도 컴포넌트로 신설했다 — 신규
    // 위젯 최소화 원칙에 따라 "여러 RunPhase + 상점 대상선택" 전부가 이 하나를 공유한다(호출측이 매번
    // Item 리스트만 새로 구성).
    public sealed class ListPickerPanel : MonoBehaviour
    {
        private const float RowStagger = 0.03f; // 카드 개수가 많을 수 있어(addBasic 10종+) NodePanel의
        // 0.08s보다 짧게(총 대기시간 상한 관리) — 설계 미명시, 기존 스태거 관례 축소 적용.
        private const float RowPopDuration = 0.22f;
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(다른 바텀시트 패널과 동일).
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private RectTransform cardsContent;
        // 자식 경로 계약: "Content/Head"(Text)·"Content/Body"(Text)·"Content/Badge"(Text, 티어/비용
        // 요약 — 없으면 빈 문자열로 숨김). 카드 루트 자체가 Button(NodePanel과 동일 관례).
        [SerializeField] private RectTransform cardTemplate;
        [SerializeField] private RectTransform cardRect; // BuildSheetChrome.card — 슬라이드 대상
        [SerializeField] private CanvasGroup dimGroup;
        // 취소 가능한 컨텍스트(상점 대상선택)에서만 쓴다 — RunPhase 오퍼 6종은 "반드시 하나를 골라야
        // 진행되는" 상태라 취소 버튼을 아예 숨긴다(Show가 onClose=null이면 SetActive(false), 소프트락
        // 방지 원칙 — 취소해도 갈 곳이 없는 화면에 취소 버튼을 두지 않는다).
        [SerializeField] private Button closeButton;

        public sealed class Item
        {
            public string Head;
            public string Body;
            public string Badge;
            public Color TierColor = Color.clear; // clear=기본 테두리색(UiKit.Bd) 유지
            public Action OnPick;
        }

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
            // Opus 2차검수(P7-4b) [경미③] — 여기서 걸었던 `closeButton.onClick.AddListener(Hide)`는
            // 사문(死文)이었다: Show()가 매번 `closeButton.onClick.RemoveAllListeners()`로 리스너를
            // 통째로 비운 뒤 onClose 유무에 따라 새로 붙이므로(아래 Show 참조) Awake가 붙인 리스너는
            // 첫 Show() 호출 즉시 제거돼 한 번도 실행되지 못한다 — 제거.
        }

        public void Show(string title, string subtitle, IReadOnlyList<Item> items, Action onClose = null)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            if (titleText != null) titleText.text = title;
            if (subtitleText != null)
            {
                subtitleText.text = subtitle ?? "";
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }
            if (closeButton != null)
            {
                bool canClose = onClose != null;
                closeButton.gameObject.SetActive(canClose);
                closeButton.onClick.RemoveAllListeners();
                if (canClose) closeButton.onClick.AddListener(() => { Hide(); onClose(); });
            }

            var rows = BuildCards(items);
            StopAllCoroutines();
            if (firstShow) StartCoroutine(EnterRoutine(rows));
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
                foreach (var r in rows) if (r != null) r.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private IEnumerator EnterRoutine(List<RectTransform> rows)
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
            yield return PopInRoutine(rows);
        }

        private IEnumerator PopInRoutine(List<RectTransform> rows)
        {
            foreach (var r in rows) if (r != null) r.localScale = Vector3.zero;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) StartCoroutine(UiTween.ScaleRoutine(rows[i], Vector3.zero, Vector3.one, RowPopDuration, UiTween.Ease.OutBack));
                if (i < rows.Count - 1) yield return new WaitForSeconds(RowStagger);
            }
        }

        private List<RectTransform> BuildCards(IReadOnlyList<Item> items)
        {
            var result = new List<RectTransform>();
            if (cardsContent == null || cardTemplate == null || items == null) return result;

            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true);
                card.name = "ListPickerCard_" + i;
                result.Add(card);

                var headText = card.Find("Content/Head")?.GetComponent<Text>();
                if (headText != null) headText.text = item.Head ?? "";
                var bodyText = card.Find("Content/Body")?.GetComponent<Text>();
                if (bodyText != null)
                {
                    bodyText.text = item.Body ?? "";
                    bodyText.gameObject.SetActive(!string.IsNullOrEmpty(item.Body));
                }
                var badgeText = card.Find("Content/Badge")?.GetComponent<Text>();
                if (badgeText != null)
                {
                    badgeText.text = item.Badge ?? "";
                    badgeText.gameObject.SetActive(!string.IsNullOrEmpty(item.Badge));
                }

                var outline = card.GetComponent<Outline>();
                if (outline != null) outline.effectColor = item.TierColor == Color.clear ? UiKit.Bd : item.TierColor;

                var btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var action = item.OnPick;
                    if (action != null) btn.onClick.AddListener(() => action());
                }
            }
            return result;
        }
    }
}
