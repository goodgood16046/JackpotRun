using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Data;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // EventAugment/EventRelic 페이즈 패널 — ENGINE_PORT_DESIGN.md S15 §C "보상 선택 모달"(전면 재설계).
    // 이전(S7~S12c) 형태는 하단 시트+세로 목록이었다 — 사용자 피드백("보상선택 UI 너무 구려") 반영,
    // 로그라이크 표준(중앙 3장 가로 카드) 모달로 교체. 유물 노드(EventRelic)도 헤더 문구만 바꿔 재사용
    // (RunView.cs가 두 페이즈 모두 이 컴포넌트의 Show()를 그대로 호출 — 변경 없음).
    public sealed class PerkOfferPanel : MonoBehaviour
    {
        // ── S15 §C 애니메이션 타이밍(설계 명시값은 주석에 원문 인용, 미명시는 "기본값"이라 표기) ──
        private const float DimFadeDuration = 0.15f; // 설계 명시: "딤 페이드 0.15s"
        private const float CardStagger = 0.08f; // 설계 명시: "0.08s 스태거 팝인"
        private const float CardPopDuration = 0.28f; // 설계 미명시 — OutBack 팝인 길이 기본값(S12c 관례 유지)
        private const float PopInStartScale = 0.82f; // 설계 명시: "scale .82→1.06→1"(오버슈트 정점은 OutBack 곡선 자체가 결정 — 정확히 1.06을 맞추도록 곡선을 새로 짜지 않음, 기존 UiTween.Ease.OutBack 재사용)
        private const float OtherFadeDuration = 0.15f; // 설계 명시: "나머지 2장 0.15s 페이드아웃·축소"
        private const float OtherShrinkScale = 0.85f; // 설계가 "축소"라고만 함 — 배율 기본값
        private const float SelectedScale = 1.12f; // 설계 명시
        private const float SelectScaleDuration = 0.15f; // 설계 미명시 — OtherFadeDuration과 동일 보조 길이
        private const float CloseDelay = 0.35f; // 설계 명시: "0.35s 후 닫힘"(선택 시점부터 총 지연)

        [SerializeField] private Text titleText; // 헤더 "증강 선택"/"유물 선택" 46pt
        [SerializeField] private Text subtitleText; // 헤더 부제 24pt(스테이지 + 배너 안내)
        [SerializeField] private Image tierBadgeImage; // 헤더 티어 배지 배경(제안 중 최고 티어색 저알파 틴트)
        [SerializeField] private Text tierBadgeText; // 헤더 티어 배지 라벨
        [SerializeField] private RectTransform cardsContent; // 카드 3장 가로열(HGroup, cardTemplate 복제 대상)
        [SerializeField] private RectTransform cardTemplate; // 자식 경로 계약: Content/Art/Icon·IconEmoji, Content/Name,
        // Content/TierRow/TierRibbon(+/Label), Content/Badges, Content/Desc, Content/PickButton, HoldCorner(카드 루트 직계)
        [SerializeField] private Button retakeButton; // 모달 하단 보조 행 — 전체 재추첨(단일, dev_retake 보유 시)
        [SerializeField] private Text retakeButtonLabel;
        [SerializeField] private CanvasGroup dimGroup; // 전체 화면 딤(0.72, UiSceneBuilder가 baked alpha로 굽는다)

        // 선택 연출 진행 중 중복 클릭 방지(0.35s 닫힘 지연 동안 카드/재추첨 버튼 재입력 차단).
        private bool _closing;
        // S15 §C "각 카드 뒤 티어색 오라 파티클" — FxKit.PlayLoop 핸들. 카드가 파괴/재생성될 때마다
        // 반드시 멈춰야 한다(파티클은 FxLayer 소속이라 카드 GameObject 파괴와 함께 사라지지 않는다).
        private readonly List<ParticleSystem> _auraHandles = new List<ParticleSystem>();

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(RunState run, RunEvent offerEvent, Action<int> onPick, Action<int> onHold, Action onRetake)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            _closing = false;

            bool isAugment = run.Phase == RunPhase.EventAugment;
            // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — AUGLEVEL 노드도 이 패널을 재사용한다
            // (웹 ui.js:1023 AUGLEVEL 헤더 "⬆️ 증강 강화"). PerkOfferIds에는 "새로 얻을 후보"가 아니라
            // "레벨업할 보유 증강"이 들어있다 — BuildCards가 카드에 Lv.N→Lv.N+1 배지로 구분해 보여준다.
            bool isAugLevel = run.Phase == RunPhase.EventAugLevel;
            if (titleText != null) titleText.text = isAugLevel ? "증강 강화" : (isAugment ? "증강 선택" : "유물 선택");

            // Opus 2차검수(P7-4b) [치명] — SYMAUG/SYMREL 노드(웹 game.js:1764-1791, Run/PouchOffer.cs
            // EnterSymAugOrRel)는 EventAugment/EventRelic phase를 공유하지만 PerkOfferIds에 sa_/sp_/
            // sr_ 접두 심볼퍽 id가 섞여 들어올 수 있다 — Perks.ById가 이를 모르므로(카탈로그가 다름)
            // 이전엔 이 루프가 그 카드들을 통째로 건너뛰어 오퍼가 빈 채로 뜨는 소프트락이었다(3장 다
            // 심볼퍽이면 카드 0장). NodeEvents.PickOffer의 그랜트 경로(§PickOffer:511-516 grantedSymPerk)와
            // 동일한 "Perks.ById 우선, 없으면 SymPerks.Get" 원칙을 표시 경로에도 적용한다.
            var ids = run.PerkOfferIds;
            Tier maxTier = Tier.SILVER;
            PCat fallbackCat = (isAugment || isAugLevel) ? PCat.AUGMENT : PCat.RELIC;
            for (int i = 0; i < ids.Count; i++)
            {
                var p = ResolvePerk(ids[i], fallbackCat);
                if (p != null && p.tier > maxTier) maxTier = p.tier;
            }
            var maxTierColor = UiKit.TierColor(maxTier.ToString());
            if (tierBadgeText != null) { tierBadgeText.text = TierLabel(maxTier); tierBadgeText.color = maxTierColor; }
            if (tierBadgeImage != null)
            {
                var bg = maxTierColor;
                bg.a = 0.16f;
                tierBadgeImage.color = bg;
            }

            var subtitleParts = new List<string> { $"스테이지 {run.Stage}" };
            if (offerEvent != null)
            {
                if (offerEvent.offerBossPrism) subtitleParts.Add("보스 클리어! 프리즘 확정");
                if (offerEvent.offerTierBumped) subtitleParts.Add("행운! 등급업");
                if (offerEvent.offerHeldIncluded) subtitleParts.Add("보류 후보 포함");
                if (offerEvent.offerRetake) subtitleParts.Add("재추첨 결과");
            }
            if (subtitleText != null) subtitleText.text = string.Join(" · ", subtitleParts);

            bool canHold = isAugment && Shop.HasDevice(run, "dev_holdfile") && string.IsNullOrEmpty(run.HeldAug);
            // AUGLEVEL은 보류/재추첨 대상이 아니다(웹에 대응 UI 없음 — dev_holdfile/dev_retake는 새 퍽
            // 후보 오퍼 전용, "보유 증강 레벨업" 오퍼에는 개념이 성립하지 않는다).
            bool canRetake = !isAugLevel && Shop.HasDevice(run, "dev_retake") && !run.UsedCmds.Contains("RETAKE");
            if (retakeButton != null)
            {
                // retakeButton은 "하단 보조 행"의 유일한 자식 — 부모(BottomRow) 자체를 껐다 켜서
                // ContentSizeFitter(모달 자동 높이)가 빈 자리를 남기지 않고 다시 접히게 한다.
                var bottomRow = retakeButton.transform.parent;
                if (bottomRow != null) bottomRow.gameObject.SetActive(canRetake);
                retakeButton.interactable = true;
                retakeButton.onClick.RemoveAllListeners();
                if (canRetake) retakeButton.onClick.AddListener(() => onRetake());
                if (retakeButtonLabel != null) retakeButtonLabel.text = $"재추첨 ({Formulas.RETAKE_COIN_COST}코인)";
            }

            var cards = BuildCards(run, offerEvent, isAugLevel, onPick, canHold ? onHold : (Action<int>)null);

            if (firstShow) StartCoroutine(EnterRoutine(cards));
            else ShowCardsInstant(cards);
        }

        public void Hide()
        {
            StopAllCoroutines();
            StopCardAuras();
            gameObject.SetActive(false);
        }

        // ── 등장(S15 §C: "딤 페이드 0.15s → 카드 3장 0.08s 스태거 팝인") ────────────────────
        private IEnumerator EnterRoutine(List<(RectTransform rect, Color tier)> cards)
        {
            if (dimGroup != null) yield return UiTween.FadeRoutine(dimGroup, 0f, 1f, DimFadeDuration);
            yield return PopInRoutine(cards);
        }

        private IEnumerator PopInRoutine(List<(RectTransform rect, Color tier)> cards)
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].rect != null) cards[i].rect.localScale = Vector3.one * PopInStartScale;

            for (int i = 0; i < cards.Count; i++)
            {
                var (rect, tier) = cards[i];
                if (rect != null)
                {
                    StartCoroutine(UiTween.ScaleRoutine(rect, Vector3.one * PopInStartScale, Vector3.one, CardPopDuration, UiTween.Ease.OutBack));
                    StartCardAura(rect, tier);
                }
                if (i < cards.Count - 1) yield return new WaitForSeconds(CardStagger);
            }
        }

        // 재추첨/재진입(패널이 이미 열려 있던 상태) — 팝인을 다시 재생하지 않고 즉시 표시한다
        // (기존 S12c 동작 그대로 유지, S15 §C는 재추첨 자체의 연출을 별도로 명시하지 않음).
        private void ShowCardsInstant(List<(RectTransform rect, Color tier)> cards)
        {
            if (dimGroup != null) dimGroup.alpha = 1f;
            foreach (var (rect, tier) in cards)
            {
                if (rect == null) continue;
                rect.localScale = Vector3.one;
                var cg = rect.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                StartCardAura(rect, tier);
            }
        }

        private void StartCardAura(RectTransform card, Color tierColor)
        {
            var handle = FxKit.I?.PlayLoop(FxId.UiAura, card, tierColor);
            if (handle != null) _auraHandles.Add(handle);
        }

        private void StopCardAuras()
        {
            for (int i = 0; i < _auraHandles.Count; i++) FxKit.I?.StopLoop(_auraHandles[i]);
            _auraHandles.Clear();
        }

        // ── 선택(S15 §C: "고른 카드 1.12 확대 + 티어색 파티클, 나머지 2장 0.15s 페이드아웃·축소,
        // 0.35s 후 닫힘") ────────────────────────────────────────────────────────────────
        private void OnCardPicked(int idx, RectTransform selectedRect, Color tierColor,
            List<(RectTransform rect, Color tier)> allCards, Action<int> onPick)
        {
            if (_closing) return; // 닫힘 연출 중 중복 클릭 방지
            _closing = true;
            // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 celebratePerk 트리거 ui.js:167
            // `st = g.pickPerk(...); ...; celebratePerk(cp);`) — 카드를 실제로 "고른" 시점. 보류(onHold)는
            // 이 메서드를 타지 않으므로(홀드 버튼은 onHold(idx)를 바로 부른다) 웹처럼 사운드가 나지 않는다.
            SoundKit.Sfx("perk");
            StopCardAuras();

            foreach (var (rect, _) in allCards)
            {
                if (rect == null) continue;
                var hover = rect.GetComponent<PerkCardHover>();
                if (hover != null) hover.enabled = false;
                var pickBtn = rect.Find("Content/PickButton")?.GetComponent<Button>();
                if (pickBtn != null) pickBtn.interactable = false;
                var holdBtn = rect.Find("HoldCorner")?.GetComponent<Button>();
                if (holdBtn != null) holdBtn.interactable = false;
            }
            if (retakeButton != null) retakeButton.interactable = false;

            StartCoroutine(SelectRoutine(idx, selectedRect, tierColor, allCards, onPick));
        }

        private IEnumerator SelectRoutine(int idx, RectTransform selected, Color tierColor,
            List<(RectTransform rect, Color tier)> all, Action<int> onPick)
        {
            // S15c 지시("PerkOfferPanel: 카드 선택 시 티어색 파티치")를 CardPick으로 재생한다 —
            // "없는 FxId를 새로 만들지 말고 기존 CardPick/UiAura 사용"(작업 지시) 그대로.
            FxKit.I?.Play(FxId.CardPick, selected, tierColor);
            if (selected != null)
                StartCoroutine(UiTween.ScaleRoutine(selected, selected.localScale, Vector3.one * SelectedScale, SelectScaleDuration, UiTween.Ease.OutBack));

            foreach (var (rect, _) in all)
            {
                if (rect == null || rect == selected) continue;
                var cg = rect.GetComponent<CanvasGroup>();
                if (cg != null) StartCoroutine(UiTween.FadeRoutine(cg, cg.alpha, 0f, OtherFadeDuration));
                StartCoroutine(UiTween.ScaleRoutine(rect, rect.localScale, Vector3.one * OtherShrinkScale, OtherFadeDuration));
            }

            yield return new WaitForSeconds(CloseDelay);
            onPick(idx);
        }

        // ── 카드 채우기 ──────────────────────────────────────────────────────────────────
        private List<(RectTransform rect, Color tier)> BuildCards(RunState run, RunEvent offerEvent, bool isAugLevel, Action<int> onPick, Action<int> onHold)
        {
            var cardList = new List<(RectTransform rect, Color tier)>();
            StopCardAuras();
            if (cardsContent == null || cardTemplate == null) return cardList;

            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            // Opus 2차검수(P7-4b) [치명] — 위 Show()의 maxTier 루프와 동일한 심볼퍽 폴백(fallbackCat은
            // run.Phase로 다시 계산 — BuildCards가 isAugment를 직접 받지 않아 여기서 한 번 더 구한다).
            PCat fallbackCat = (run.Phase == RunPhase.EventAugment || isAugLevel) ? PCat.AUGMENT : PCat.RELIC;
            var ids = run.PerkOfferIds;
            for (int i = 0; i < ids.Count; i++)
            {
                int idx = i;
                var perk = ResolvePerk(ids[i], fallbackCat);
                if (perk == null) continue;
                bool heldBadge = offerEvent != null && offerEvent.offerHeldIncluded && idx == 0;
                bool synergyBadge = offerEvent != null && offerEvent.offerSynergyPerkId == perk.id;
                var tierColor = UiKit.TierColor(perk.tier.ToString());

                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true);
                card.name = "PerkCard_" + perk.id;

                var icon = card.Find("Content/Art/Icon")?.GetComponent<Image>();
                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(perk.cat, perk.id)));
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = card.Find("Content/Art/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = perk.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                // S15 §C "아트 260 정사각(티어색 3px 테두리+글로우)" — 호버 시 강화되는 기준값(0.8
                // 알파)으로 굽고, PerkCardHover가 hover 진입 시 1.0/거리 확대로 되돌린다.
                var artOutline = card.Find("Content/Art")?.GetComponent<Outline>();
                if (artOutline != null)
                {
                    var baseColor = tierColor;
                    baseColor.a = 0.8f;
                    artOutline.effectColor = baseColor;
                }

                var nameText = card.Find("Content/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = perk.name;

                var tierLabelText = card.Find("Content/TierRow/TierRibbon/Label")?.GetComponent<Text>();
                if (tierLabelText != null) { tierLabelText.text = TierLabel(perk.tier); tierLabelText.color = tierColor; }
                var tierRibbonBg = card.Find("Content/TierRow/TierRibbon")?.GetComponent<Image>();
                if (tierRibbonBg != null)
                {
                    var bg = tierColor;
                    bg.a = 0.18f;
                    tierRibbonBg.color = bg;
                }

                var badgesText = card.Find("Content/Badges")?.GetComponent<Text>();
                if (badgesText != null)
                {
                    // 웹 파리티 P3-3 — AUGLEVEL 카드는 "Lv.N → Lv.N+1" 배지로 레벨업임을 표시한다
                    // (웹 ui.js:1050 `Lv.${p.curLevel} → Lv.${p.nextLevel}`, Perks.cs 데이터는 그대로
                    // 재사용 — 별도 레벨별 설명 텍스트가 없어 Desc는 기존 perk.desc를 그대로 둔다).
                    string levelBadge = "";
                    if (isAugLevel)
                    {
                        int curLv = run.PerkLevels.TryGetValue(perk.id, out var lvNow) ? lvNow : 1;
                        levelBadge = $"Lv.{curLv} → Lv.{Math.Min(3, curLv + 1)} ";
                    }
                    string badges = levelBadge + (heldBadge ? "[보류] " : "") + (synergyBadge ? "[시너지]" : "");
                    badgesText.text = badges;
                    badgesText.gameObject.SetActive(!string.IsNullOrEmpty(badges));
                }

                var synergyOutline = card.GetComponent<Outline>();
                if (synergyOutline != null) synergyOutline.enabled = synergyBadge; // 보라 테두리(시너지 주입 카드)

                var descText = card.Find("Content/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = perk.desc;

                var pickBtn = card.Find("Content/PickButton")?.GetComponent<Button>();
                if (pickBtn != null)
                {
                    pickBtn.onClick.RemoveAllListeners();
                    pickBtn.interactable = true;
                    var cardRt = card;
                    // cardList는 이 루프가 전부 끝난 뒤(패널이 실제로 눌리는 시점)에나 읽히므로, 클로저가
                    // 캡처하는 시점엔 비어 있어도 상관없다(참조 타입 — 클릭 시점엔 이미 3장 다 채워짐).
                    pickBtn.onClick.AddListener(() => OnCardPicked(idx, cardRt, tierColor, cardList, onPick));
                }

                var holdBtn = card.Find("HoldCorner")?.GetComponent<Button>();
                if (holdBtn != null)
                {
                    bool show = onHold != null;
                    holdBtn.gameObject.SetActive(show);
                    holdBtn.interactable = true;
                    holdBtn.onClick.RemoveAllListeners();
                    if (show) holdBtn.onClick.AddListener(() => onHold(idx));
                }

                var hover = card.gameObject.AddComponent<PerkCardHover>();
                hover.Init(card, artOutline, tierColor);

                cardList.Add((card, tierColor));
            }
            return cardList;
        }

        // S15 §C "호버/누름: 카드 -6px 부양 + 글로우 강화, 누름 시 scale .96." — 누름 스케일(.96)은
        // [선택] 버튼 자신의 PressFx(PressedScale=0.96, 기존 상수와 정확히 일치)가 이미 담당하므로
        // 여기서는 중복 구현하지 않는다(카드 루트 스케일은 이 패널의 팝인/선택 코루틴이 전담 —
        // PressFx를 카드 루트에도 붙이면 같은 localScale을 두 주체가 동시에 건드려 충돌한다).
        // 호버(포인터 진입/이탈)만 이 컴포넌트가 anchoredPosition(부양)과 Art 테두리 알파/두께(글로우)로 담당.
        private sealed class PerkCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private const float LiftY = 6f; // 설계 명시: "-6px 부양"
            private const float LiftDuration = 0.12f; // 설계 미명시 기본값

            private RectTransform _target;
            private Outline _artGlow;
            private Color _tierColor;
            private float _baseY;
            private Coroutine _routine;

            public void Init(RectTransform target, Outline artGlow, Color tierColor)
            {
                _target = target;
                _artGlow = artGlow;
                _tierColor = tierColor;
                _baseY = target != null ? target.anchoredPosition.y : 0f;
            }

            public void OnPointerEnter(PointerEventData eventData) => Animate(true);
            public void OnPointerExit(PointerEventData eventData) => Animate(false);

            private void OnDisable() => Animate(false);

            private void Animate(bool hover)
            {
                if (_target == null) return;
                if (_routine != null) StopCoroutine(_routine);
                float from = _target.anchoredPosition.y;
                float to = _baseY + (hover ? LiftY : 0f);
                _routine = StartCoroutine(UiTween.FloatRoutine(from, to, LiftDuration,
                    y =>
                    {
                        if (_target != null) _target.anchoredPosition = new Vector2(_target.anchoredPosition.x, y);
                    },
                    UiTween.Ease.OutQuad));

                if (_artGlow != null)
                {
                    var c = _tierColor;
                    c.a = hover ? 1f : 0.8f;
                    _artGlow.effectColor = c;
                    _artGlow.effectDistance = hover ? new Vector2(5f, -5f) : new Vector2(3f, -3f);
                }
            }
        }

        // Opus 2차검수(P7-4b) [치명] — Perks.ById(순정 증강/유물)가 모르는 심볼퍽(sa_/sp_/sr_ 접두)을
        // PouchOffer.WrapSymPerk(internal로 승격)로 카드 표시 가능한 Perk 형태로 합성한다. 스프라이트는
        // 없어도 안전(JackpotCatalog.CatalogIdOf가 "aug_sa_..."/"rel_sr_..."로 조회하지만 실제 저장
        // 키는 "symaug_"/"symrel_"라 미스 → sprite=null → emoji 폴백으로 자연 강등, 크래시 없음).
        private static Perk ResolvePerk(string id, PCat fallbackCat)
        {
            var p = Perks.ById(id);
            if (p != null) return p;
            var sp = SymPerks.Get(id);
            return sp != null ? PouchOffer.WrapSymPerk(sp, fallbackCat) : null;
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

        private static string TierLabel(Tier t)
        {
            switch (t)
            {
                case Tier.SILVER: return "실버";
                case Tier.GOLD: return "골드";
                default: return "프리즘";
            }
        }
    }
}
