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
    // EventShop 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/ShopPanel.cs. 오퍼 행 스태거 팝인
    // (0.08s, OutBack), 가격 pill, 코인 부족 시 구매 버튼 흔들림 피드백, 리롤/나가기("RunView 연출"·
    // "PerkOfferPanel/ShopPanel" 지시). 이관 원본: Scripts/UI/RunPanels.cs의 BuildShop/BuildShopRow.
    //
    // 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 renderShop ui.js:1421-1482) — 심화 런
    // 전용 "상점|심볼 정비" 탭 추가. 일반 런은 탭 자체가 안 보이고(웹 `if (!deep && shopTab!=="buy")
    // shopTab="buy"`) 항상 buy 탭 하나만 동작 — 기존 구매 흐름은 완전히 무변경. 정비 서비스 11종은
    // 기존 rowTemplate(Icon/Name/Desc/PriceButton)을 그대로 재사용해 신규 위젯을 최소화했다. 대상
    // 선택이 필요한 서비스(addBasic/addHigh/addRare/remove/upgrade/swap/tagbuff)는 `repairTargetPanel`
    // (ListPickerPanel 공용 인스턴스, 상점 위에 얹히는 서브시트)로 후보를 보여준다 — 웹의 "대상선택 →
    // 확인" 2단계 중 확인(미리보기) 단계는 이번 슬라이스에서 생략(RepairShop.Execute가 원자적으로
    // 검증·거부하므로 안전 — 단, 사용자가 미리보기 없이 곧장 커밋한다는 점만 웹과 다르다, 이탈 사항).
    public sealed class ShopPanel : MonoBehaviour
    {
        private const float RowStagger = 0.08f;
        private const float RowPopDuration = 0.28f;
        private const float ShakeAmplitude = 8f; // 설계 미명시 — 코인 부족 흔들림 기본값
        private const float ShakeDuration = 0.25f;
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시).
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate; // 자식 경로 계약: Icon/Name/Desc/PriceButton/PriceLabel
        [SerializeField] private Text emptyText;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Text rerollButtonLabel;
        [SerializeField] private Button leaveButton;
        [SerializeField] private RectTransform cardRect; // S12c §6 — 시트(Card) 슬라이드업 대상
        [SerializeField] private CanvasGroup dimGroup; // S12c §6 — 배경 딤 페이드(BuildSheetChrome.dimGroup)

        // ── 웹 파리티 P7-4b — 심화 전용 "심볼 정비" 탭 ──────────────────────────────────────
        [SerializeField] private RectTransform tabRow;
        [SerializeField] private Image buyTabImage;
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Image repairTabImage;
        [SerializeField] private Button repairTabButton;
        [SerializeField] private Text repairSummaryText;
        [SerializeField] private ListPickerPanel repairTargetPanel;

        private string _tab = "buy"; // "buy" | "repair" — 웹 shopTab 미러(모듈 변수 → 인스턴스 필드).
        private RunState _run;
        private Action<int> _onBuy;
        private Action _onReroll;
        private Action _onLeave;
        private Action<string, RepairArgs> _onRepairBuy;

        // swap(A→B) 2단계 선택 중간 상태 — 웹 repairSel.sel.from과 동일 관례.
        private string _swapPendingServiceId;
        private string _swapPendingFrom;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(RunState run, Action<int> onBuy, Action onReroll, Action onLeave, Action<string, RepairArgs> onRepairBuy)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            _run = run;
            _onBuy = onBuy;
            _onReroll = onReroll;
            _onLeave = onLeave;
            _onRepairBuy = onRepairBuy;
            // 웹 ui.js:1423 `if (!deep && shopTab !== "buy") shopTab = "buy";` — 일반 런은 정비 탭 자체가
            // 없으므로 심화가 아니면 buy로 강제 복귀(다른 심화 런에서 탭을 바꿔 두고 나갔다가 일반 런으로
            // 다시 상점에 들어오는 경우까지 방어).
            if (!run.DeepMode) _tab = "buy";

            // S8 항목⑤: astral 이모지(🛒🔁🪙 등)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (titleText != null) titleText.text = $"상점 · 코인 {NumberFormat.Comma(run.Coins)}";

            if (tabRow != null) tabRow.gameObject.SetActive(run.DeepMode);
            if (buyTabButton != null) { buyTabButton.onClick.RemoveAllListeners(); buyTabButton.onClick.AddListener(() => SwitchTab("buy")); }
            if (repairTabButton != null) { repairTabButton.onClick.RemoveAllListeners(); repairTabButton.onClick.AddListener(() => SwitchTab("repair")); }
            RefreshTabHighlight();

            if (rerollButtonLabel != null) rerollButtonLabel.text = $"리롤 ({Shop.RerollCostFor(run)}코인)";
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(() => onReroll());
            }
            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => onLeave());
            }

            var rows = _tab == "repair" ? BuildRepairRows(run) : BuildRows(run, onBuy);
            if (_tab != "repair" && emptyText != null) emptyText.gameObject.SetActive(run.ShopOffer.Count == 0);
            else if (emptyText != null) emptyText.gameObject.SetActive(false);

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
            repairTargetPanel?.Hide();
            _swapPendingServiceId = null;
            _swapPendingFrom = null;
        }

        private void SwitchTab(string tab)
        {
            if (_run == null || _tab == tab) return;
            _tab = tab;
            RefreshTabHighlight();
            var rows = _tab == "repair" ? BuildRepairRows(_run) : BuildRows(_run, _onBuy);
            if (_tab != "repair" && emptyText != null) emptyText.gameObject.SetActive(_run.ShopOffer.Count == 0);
            else if (emptyText != null) emptyText.gameObject.SetActive(false);
            foreach (var r in rows) if (r != null) r.localScale = Vector3.one; // 탭 전환은 즉시 표시(재진입 팝인 생략).
        }

        private void RefreshTabHighlight()
        {
            if (buyTabImage != null) buyTabImage.color = _tab == "buy" ? UiKit.Panel3 : UiKit.PanelBg;
            if (repairTabImage != null) repairTabImage.color = _tab == "repair" ? UiKit.Panel3 : UiKit.PanelBg;
        }

        // S12c §6 — 시트 슬라이드업(0.24s OutCubic) + 배경 딤 페이드 동시 재생, 완료 후 행 스태거 팝인.
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

        private List<RectTransform> BuildRows(RunState run, Action<int> onBuy)
        {
            var result = new List<RectTransform>();
            if (rowsContent == null || rowTemplate == null) return result;

            ClearRows();

            for (int i = 0; i < run.ShopOffer.Count; i++)
            {
                int idx = i;
                var entry = run.ShopOffer[i];

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "ShopEntry_" + entry.id;
                result.Add(row);

                PCat cat = entry.kind == 'A' ? PCat.AUGMENT : entry.kind == 'R' ? PCat.RELIC : PCat.ITEM;
                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(cat, entry.id)));

                string emoji, name, desc;
                if (entry.kind == 'A' || entry.kind == 'R')
                {
                    var perk = Perks.ById(entry.id);
                    emoji = perk?.emoji ?? "❔"; name = perk?.name ?? entry.id; desc = perk?.desc ?? "";
                }
                else
                {
                    var item = Items.ById(entry.id);
                    emoji = item?.emoji ?? "❔"; name = item?.name ?? entry.id; desc = item?.desc ?? "";
                }

                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = $"{KindLabel(entry.kind)} {name}";
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = desc;

                bool affordable = run.Coins >= entry.price;
                var priceButton = row.Find("Content/PriceButton")?.GetComponent<Button>();
                var priceImage = priceButton != null ? priceButton.GetComponent<Image>() : null;
                var priceLabel = row.Find("Content/PriceButton/PriceLabel")?.GetComponent<Text>();
                if (priceLabel != null) priceLabel.text = NumberFormat.Comma(entry.price);
                if (priceImage != null) priceImage.color = affordable ? UiKit.Accent : UiKit.Card;
                if (priceLabel != null) priceLabel.color = affordable ? UiKit.Bg : UiKit.TextSecondary;
                if (priceButton != null)
                {
                    priceButton.onClick.RemoveAllListeners();
                    var buttonRt = priceButton.GetComponent<RectTransform>();
                    priceButton.onClick.AddListener(() =>
                    {
                        if (affordable) onBuy(idx);
                        else if (buttonRt != null) StartCoroutine(UiTween.ShakeRoutine(buttonRt, ShakeAmplitude, ShakeDuration));
                    });
                }
            }
            return result;
        }

        // ── 웹 파리티 P7-4b: 심볼 정비 탭 ────────────────────────────────────────────────
        private void ClearRows()
        {
            if (rowsContent == null) return;
            for (int i = rowsContent.childCount - 1; i >= 0; i--)
            {
                var child = rowsContent.GetChild(i);
                if (child == rowTemplate) continue;
                Destroy(child.gameObject);
            }
        }

        private List<RectTransform> BuildRepairRows(RunState run)
        {
            var result = new List<RectTransform>();
            if (rowsContent == null || rowTemplate == null) return result;
            ClearRows();
            RefreshRepairSummary(run);

            var services = RepairServices.All;
            for (int i = 0; i < services.Length; i++)
            {
                var sv = services[i];
                if (!RepairShop.IsAvailable(run, sv)) continue; // 저주 정화는 저주 보유 시만 노출(웹 repairServices() 필터).

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "RepairEntry_" + sv.id;
                result.Add(row);

                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = null; icon.enabled = false; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                // Opus 2차검수(P7-4b) [중대③] — RepairServiceDef.emoji(웹 원문)는 astral일 수 있다
                // (🗑️📦🗜️🏷️🕊️ 등). 이 아이콘 슬롯은 스프라이트가 없어(정비 서비스는 art 자체가
                // 없음) emoji가 유일한 시각 요소지만, 이름(Name 텍스트)이 바로 옆에 항상 표시되므로
                // StripAstral로 지워도(빈 아이콘) 정보 손실은 없다 — PerkCard 등 기존 아이콘 폴백과
                // 동일한 완화(도감 그리드처럼 "유일한 식별 수단"은 아님).
                if (iconEmoji != null) { iconEmoji.text = TextSanitize.StripAstral(sv.emoji); iconEmoji.gameObject.SetActive(true); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = sv.name;
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = sv.desc;

                int price = RepairShop.Price(run, sv);
                bool affordable = run.Coins >= price;
                var priceButton = row.Find("Content/PriceButton")?.GetComponent<Button>();
                var priceImage = priceButton != null ? priceButton.GetComponent<Image>() : null;
                var priceLabel = row.Find("Content/PriceButton/PriceLabel")?.GetComponent<Text>();
                if (priceLabel != null) priceLabel.text = NumberFormat.Comma(price);
                if (priceImage != null) priceImage.color = affordable ? UiKit.Accent : UiKit.Card;
                if (priceLabel != null) priceLabel.color = affordable ? UiKit.Bg : UiKit.TextSecondary;
                if (priceButton != null)
                {
                    priceButton.onClick.RemoveAllListeners();
                    var buttonRt = priceButton.GetComponent<RectTransform>();
                    var svCapture = sv;
                    priceButton.onClick.AddListener(() =>
                    {
                        if (affordable) OnRepairServiceClicked(svCapture);
                        else if (buttonRt != null) StartCoroutine(UiTween.ShakeRoutine(buttonRt, ShakeAmplitude, ShakeDuration));
                    });
                }
            }
            return result;
        }

        // 웹 ui.js:1462-1468 repair-summary — 총량/상한/압축패널티/태그강화 요약줄(정보 완전성 우선,
        // 카드 그리드 대신 한 줄 텍스트로 축약).
        private void RefreshRepairSummary(RunState run)
        {
            if (repairSummaryText == null) return;
            repairSummaryText.gameObject.SetActive(true);
            int total = Pouch.Total(run.Pouch);
            var bounds = RepairShop.Bounds(run);
            double penalty = DeepRunHooks.DeepPenalty(run);
            var parts = new List<string> { $"총량 {total} ({bounds.totalMin}~{bounds.totalMax})" };
            if (penalty > 1.0 + 1e-9) parts.Add($"요구 EXP +{Mathf.RoundToInt((float)((penalty - 1.0) * 100.0))}%");
            else parts.Add("압축 패널티 없음");
            if (run.DeepTagBuff.Count > 0)
            {
                var tagParts = new List<string>();
                foreach (var kv in run.DeepTagBuff)
                    if (kv.Value > 0) tagParts.Add($"#{kv.Key}+{Mathf.RoundToInt((float)(kv.Value * 100.0))}%");
                if (tagParts.Count > 0) parts.Add("태그강화 " + string.Join(" ", tagParts));
            }
            repairSummaryText.text = string.Join(" · ", parts);
        }

        // 서비스 클릭 — 대상선택 불필요면 즉시 실행, 필요하면 repairTargetPanel로 후보를 보여준다.
        private void OnRepairServiceClicked(RepairServiceDef sv)
        {
            if (!sv.targetPick) { _onRepairBuy(sv.id, null); return; }
            if (sv.kind == "tagbuff") { ShowTagTargets(sv); return; }
            if (sv.kind == "swap") { ShowSwapFromTargets(sv); return; }
            ShowSymTargets(sv);
        }

        private void ShowSymTargets(RepairServiceDef sv)
        {
            var targets = RepairShop.TargetsSym(_run, sv, "id");
            var items = new List<ListPickerPanel.Item>();
            foreach (var t in targets)
            {
                var tCap = t;
                items.Add(new ListPickerPanel.Item
                {
                    Head = SymLabel(tCap.Id),
                    Body = $"보유 ×{tCap.N} · {tCap.Rarity}",
                    OnPick = () => { repairTargetPanel?.Hide(); _onRepairBuy(sv.id, new RepairArgs { Id = tCap.Id }); },
                });
            }
            string subtitle = targets.Count == 0
                ? (sv.kind == "remove" ? "제거할 심볼이 없어요." : sv.kind == "upgrade" ? "업그레이드할 기본 심볼이 없어요." : "대상 심볼이 없어요.")
                : $"{sv.desc} — 대상 심볼을 고르세요.";
            repairTargetPanel?.Show(RepairSvTitle(sv), subtitle, items, onClose: () => { });
        }

        private void ShowSwapFromTargets(RepairServiceDef sv)
        {
            var targets = RepairShop.TargetsSym(_run, sv, "from");
            var items = new List<ListPickerPanel.Item>();
            foreach (var t in targets)
            {
                var tCap = t;
                items.Add(new ListPickerPanel.Item
                {
                    Head = SymLabel(tCap.Id),
                    Body = $"보유 ×{tCap.N} · {tCap.Rarity}",
                    OnPick = () => { _swapPendingServiceId = sv.id; _swapPendingFrom = tCap.Id; ShowSwapToTargets(sv, tCap.Id); },
                });
            }
            string subtitle = targets.Count == 0 ? "교체할 심볼이 없어요." : $"① 바꿀 심볼(A)을 고르세요 — {sv.desc}";
            repairTargetPanel?.Show(RepairSvTitle(sv), subtitle, items, onClose: () => { });
        }

        private void ShowSwapToTargets(RepairServiceDef sv, string fromId)
        {
            var targets = RepairShop.TargetsSym(_run, sv, "to");
            var items = new List<ListPickerPanel.Item>();
            foreach (var t in targets)
            {
                if (t.Id == fromId) continue; // 웹 repairPickSwapTo() — A 자신은 B 후보에서 제외.
                var tCap = t;
                items.Add(new ListPickerPanel.Item
                {
                    Head = SymLabel(tCap.Id),
                    Body = $"보유 ×{tCap.N} · {tCap.Rarity}",
                    OnPick = () =>
                    {
                        repairTargetPanel?.Hide();
                        _onRepairBuy(sv.id, new RepairArgs { From = fromId, To = tCap.Id });
                        _swapPendingServiceId = null; _swapPendingFrom = null;
                    },
                });
            }
            repairTargetPanel?.Show(RepairSvTitle(sv), $"② {SymLabel(fromId)} → 어떤 심볼로 바꿀까요?", items, onClose: () => { });
        }

        private void ShowTagTargets(RepairServiceDef sv)
        {
            var tags = RepairShop.TargetsTag(_run);
            var items = new List<ListPickerPanel.Item>();
            foreach (var t in tags)
            {
                var tCap = t;
                string buffTxt = tCap.Buff > 0 ? $" · 현재 +{Mathf.RoundToInt((float)(tCap.Buff * 100.0))}%" : "";
                items.Add(new ListPickerPanel.Item
                {
                    Head = "#" + tCap.Tag,
                    Body = $"주머니 {tCap.Cnt}개{buffTxt}",
                    OnPick = () => { repairTargetPanel?.Hide(); _onRepairBuy(sv.id, new RepairArgs { Tag = tCap.Tag }); },
                });
            }
            string subtitle = tags.Count == 0 ? "강화할 태그가 없어요." : $"강화할 태그를 고르세요 — 해당 태그 심볼 EXP +{Mathf.RoundToInt((float)(sv.pct * 100.0))}%.";
            repairTargetPanel?.Show(RepairSvTitle(sv), subtitle, items, onClose: () => { });
        }

        // Opus 2차검수(P7-4b) [중대③] — repairTargetPanel 타이틀 4곳(ShowSymTargets/SwapFrom/SwapTo/
        // TagTargets)이 전부 "{sv.emoji} {sv.name}" 형태라 공용 헬퍼로 묶는다(RunView.RepairServiceLabel
        // 과 동일 StripAstral 근거 — 공백 구분자만 다름, ShopPanel은 시트 타이틀이라 공백 유지).
        private static string RepairSvTitle(RepairServiceDef sv) => TextSanitize.StripAstral($"{sv.emoji} {sv.name}");

        // 심볼 id → "{emoji} {이름}" — empty/random은 카탈로그(Symbols.All)에 없는 센티널이라 웹
        // symInfo()와 동일한 고정 라벨로 대체(astral 🎲 대신 BMP "◎").
        // Opus 2차검수(P7-4b) [중대③] — RunView.SymLabel과 동일 근거로 StripAstral 적용.
        private static string SymLabel(string id)
        {
            if (id == "empty") return "▫ 빈칸";
            if (id == "random") return "◎ 랜덤칸";
            var s = Symbols.ById(id);
            return s != null ? TextSanitize.StripAstral($"{s.emoji} {s.name}") : id;
        }

        private static string CatalogIdOf(PCat cat, string id)
        {
            switch (cat)
            {
                case PCat.AUGMENT: return "aug_" + id;
                case PCat.RELIC: return "rel_" + id;
                case PCat.CURSE: return "cur_" + id;
                case PCat.ITEM: return "item_" + id;
                default: return id;
            }
        }

        private static string KindLabel(char kind) => kind == 'A' ? "[증강]" : kind == 'R' ? "[유물]" : "[아이템]";
    }
}
