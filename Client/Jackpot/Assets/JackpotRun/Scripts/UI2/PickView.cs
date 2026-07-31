using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 시작 조합 선택 화면 — ENGINE_PORT_DESIGN.md S7 "화면 사양" PickView. **실 프로필 해금 연동**
    // (DemoData는 참조하지 않는다 — profile.IsCharUnlocked/IsMachineUnlocked/IsDeviceUnlocked, 잠금
    // 힌트는 pick.unlock 그대로 표시). 정렬/필터/추천/자동 탭 진행/Evaluate 연동 로직은
    // Scripts/UI/PickScreen.cs를 그대로 이관했다 — 레이아웃만 새로 짰다(카드/칩은 UiSceneBuilder가
    // 만든 템플릿을 Instantiate로 복제해 값만 채우는 방식, "런타임 코드생성 금지" 원칙).
    public sealed class PickView : MonoBehaviour
    {
        private static readonly Dictionary<string, int> GradeRank = new Dictionary<string, int>
        {
            { "S", 0 }, { "A", 1 }, { "B", 2 }, { "C", 3 },
        };

        private static readonly string[] TabOrder = { "char", "mac", "dev" };
        private static readonly string[] SortKeys = { "reco", "diff", "ceiling", "recent" };
        private static readonly string[] RecoKinds = { "beginner", "high", "challenge", "random" };
        private const float TabFadeDuration = 0.15f;

        [SerializeField] private AppRoot appRoot;

        [Header("추천 4버튼 — 순서 고정: 입문/고점/도전/랜덤 (RecoKinds)")]
        [SerializeField] private Button[] recoButtons = Array.Empty<Button>();

        [Header("탭 3버튼 — 순서 고정: 캐릭터/머신/장치 (TabOrder)")]
        [SerializeField] private Button[] tabButtons = Array.Empty<Button>();
        [SerializeField] private Image[] tabButtonImages = Array.Empty<Image>();
        [SerializeField] private Text[] tabLabelTexts = Array.Empty<Text>();

        [Header("필터 칩")]
        [SerializeField] private RectTransform chipsContent;
        [SerializeField] private RectTransform chipTemplate;

        [Header("정렬 4버튼 — 순서 고정: 추천순/난이도순/고점순/최근해금순 (SortKeys)")]
        [SerializeField] private Button[] sortButtons = Array.Empty<Button>();
        [SerializeField] private Image[] sortButtonImages = Array.Empty<Image>();

        [Header("카드 그리드")]
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private CanvasGroup gridCanvasGroup;
        [SerializeField] private RectTransform cardTemplate;

        [Header("요약 패널")]
        [SerializeField] private Text comboText;
        [SerializeField] private Text gradeText;
        [SerializeField] private Text ceilingValueText;
        [SerializeField] private Text stabilityValueText;
        [SerializeField] private Text difficultyValueText;
        [SerializeField] private Text difficultyLabelText;
        [SerializeField] private Text blurbText;
        [SerializeField] private Text prosText;
        [SerializeField] private Text consText;
        [SerializeField] private Text buildText;
        [SerializeField] private Button startButton;

        // ── 상태 (매번 화면에 들어올 때 OnEnable에서 초기화 — 기존 PickScreen.Init과 동일 규칙) ──
        private string _tab = "char";
        private string _filter = "전체";
        private string _sort = "reco";
        private string _selChar;
        private string _selMac;
        private string _selDev = "";
        private bool _advancedChar;
        private bool _advancedMac;

        // RenderSection이 새로 그린 카드 중 "방금 선택된" 카드에만 글로우 펄스를 태우기 위한 1회성 표식.
        private string _justPickedTab;
        private string _justPickedId;

        private Coroutine _tabTransition;
        private bool _wired;

        private void Awake()
        {
            WireOnce();
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < recoButtons.Length && i < RecoKinds.Length; i++)
            {
                string kind = RecoKinds[i];
                recoButtons[i].onClick.AddListener(() => ApplyReco(kind));
            }

            for (int i = 0; i < tabButtons.Length && i < TabOrder.Length; i++)
            {
                string tab = TabOrder[i];
                tabButtons[i].onClick.AddListener(() => SetTab(tab));
            }

            for (int i = 0; i < sortButtons.Length && i < SortKeys.Length; i++)
            {
                string key = SortKeys[i];
                sortButtons[i].onClick.AddListener(() =>
                {
                    _sort = key;
                    UpdateSortHighlight();
                    RenderSection();
                });
            }

            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnEnable()
        {
            _tab = "char";
            _filter = "전체";
            _sort = "reco";
            _selChar = null;
            _selMac = null;
            _selDev = "";
            _advancedChar = false;
            _advancedMac = false;
            _justPickedTab = null;
            _justPickedId = null;

            if (gridCanvasGroup != null) gridCanvasGroup.alpha = 1f;

            RefreshTabLabels();
            UpdateTabHighlight();
            UpdateSortHighlight();
            RenderChips();
            RenderSection();
            UpdateSummary();
        }

        private void OnDisable()
        {
            if (_tabTransition != null)
            {
                StopCoroutine(_tabTransition);
                _tabTransition = null;
            }
        }

        // ── 탭 ────────────────────────────────────────────────────────────────────────
        private void SetTab(string tab)
        {
            if (tab == _tab) return;
            _tab = tab;
            _filter = "전체";
            UpdateTabHighlight();
            if (_tabTransition != null) StopCoroutine(_tabTransition);
            _tabTransition = StartCoroutine(TabTransition());
        }

        // 탭 전환 슬라이드(0.15s) — ENGINE_PORT_DESIGN.md S7 "화면 사양" PickView 지시. 카드 그리드를
        // 페이드아웃 → 재구성 → 페이드인 한다(gridCanvasGroup이 없으면 페이드 없이 즉시 재구성).
        private IEnumerator TabTransition()
        {
            if (gridCanvasGroup != null)
                yield return UiTween.FadeRoutine(gridCanvasGroup, 1f, 0f, TabFadeDuration);

            RenderChips();
            RenderSection();

            if (gridCanvasGroup != null)
                yield return UiTween.FadeRoutine(gridCanvasGroup, 0f, 1f, TabFadeDuration);

            _tabTransition = null;
        }

        private void UpdateTabHighlight()
        {
            for (int i = 0; i < tabButtonImages.Length && i < TabOrder.Length; i++)
                tabButtonImages[i].color = TabOrder[i] == _tab ? UiKit.CardTop : UiKit.PanelBg;
        }

        private void UpdateSortHighlight()
        {
            for (int i = 0; i < sortButtonImages.Length && i < SortKeys.Length; i++)
                sortButtonImages[i].color = SortKeys[i] == _sort ? UiKit.Accent : UiKit.Card;
        }

        private void RefreshTabLabels()
        {
            var c = !string.IsNullOrEmpty(_selChar) ? MetaOf("char", _selChar) : null;
            var m = !string.IsNullOrEmpty(_selMac) ? MetaOf("mac", _selMac) : null;
            var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;
            if (tabLabelTexts.Length > 0) tabLabelTexts[0].text = c != null ? $"{c.emoji} {c.name}" : "선택 전";
            if (tabLabelTexts.Length > 1) tabLabelTexts[1].text = m != null ? $"{m.emoji} {m.name}" : "선택 전";
            if (tabLabelTexts.Length > 2) tabLabelTexts[2].text = d != null ? $"{d.emoji} {d.name}" : "장치 없이";
        }

        // ── 필터 칩 ───────────────────────────────────────────────────────────────────
        private void RenderChips()
        {
            if (chipsContent == null || chipTemplate == null) return;
            ClearChildren(chipsContent, chipTemplate);

            var present = new HashSet<string>();
            foreach (var id in TabIds(_tab))
            {
                var m = MetaOf(_tab, id);
                if (m?.tags != null) foreach (var t in m.tags) present.Add(t);
            }

            AddChip("전체");
            foreach (var t in PickMeta.ChipVocab)
                if (present.Contains(t)) AddChip(t);
        }

        private void AddChip(string label)
        {
            bool on = _filter == label;
            var chip = Instantiate(chipTemplate, chipsContent);
            chip.gameObject.SetActive(true);
            chip.name = "Chip_" + label;

            var bg = chip.GetComponent<Image>();
            if (bg != null) bg.color = on ? UiKit.Accent : UiKit.Card;

            var labelText = chip.Find("Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.color = on ? UiKit.Bg : UiKit.TextPrimary;
            }

            var button = chip.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    _filter = label;
                    RenderChips();
                    RenderSection();
                });
            }
        }

        // ── 카드 그리드 ───────────────────────────────────────────────────────────────
        private void RenderSection()
        {
            if (gridContent == null || cardTemplate == null) return;

            var ids = new List<string>();
            foreach (var id in TabIds(_tab))
            {
                if (_filter == "전체") { ids.Add(id); continue; }
                var m = MetaOf(_tab, id);
                if (m?.tags != null && Array.IndexOf(m.tags, _filter) >= 0) ids.Add(id);
            }
            ids = SortIds(ids);

            ClearChildren(gridContent, cardTemplate);

            if (_tab == "dev") BuildCard("dev", "", isNoneDevice: true);
            foreach (var id in ids) BuildCard(_tab, id, isNoneDevice: false);

            _justPickedTab = null;
            _justPickedId = null;
        }

        private void BuildCard(string tab, string id, bool isNoneDevice)
        {
            string name, effText, badgeLabel, unlockHint;
            Color badgeColor;
            Sprite icon = null;
            bool unlocked;
            bool selected;

            if (isNoneDevice)
            {
                name = "장치 없이";
                effText = "이번 런은 장치를 장착하지 않습니다. 순수 캐릭터·머신 조합.";
                badgeLabel = "기본";
                badgeColor = UiKit.Blue;
                unlocked = true;
                selected = _selDev == "";
                unlockHint = null;
            }
            else
            {
                var entry = EntryOf(tab, id);
                var m = (entry != null && entry.hasPick) ? entry.pick : null;
                if (m == null) return;

                name = m.name;
                effText = m.eff;
                icon = JackpotCatalog.LoadSprite(entry);
                unlocked = IsUnlocked(tab, id);
                selected = (tab == "char" && _selChar == id) || (tab == "mac" && _selMac == id) || (tab == "dev" && _selDev == id);

                if (tab == "dev")
                {
                    badgeLabel = string.IsNullOrEmpty(m.kind) ? "능동" : m.kind;
                    badgeColor = m.kind == "패시브" ? UiKit.Good : UiKit.Blue;
                }
                else
                {
                    badgeLabel = PickMeta.DiffLabel(m.diff);
                    badgeColor = PickMeta.DiffColor(m.diff);
                }
                unlockHint = !string.IsNullOrEmpty(m.unlock) ? m.unlock : "조건 미정";
            }

            var card = Instantiate(cardTemplate, gridContent);
            card.gameObject.SetActive(true);
            card.name = isNoneDevice ? "Card_none" : "Card_" + id;

            // "Body"는 아이콘/이름/배지/효과 텍스트를 세로로 쌓는 내부 VerticalLayoutGroup 컨테이너
            // (UiSceneBuilder.BuildCardTemplate) — Lock/Selected/IconEmoji는 오버레이라 카드 루트의
            // 직계 자식으로 따로 둔다(레이아웃 그룹 스태킹에서 제외하기 위함).
            var iconImage = card.Find("Body/Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
            var iconEmoji = card.Find("IconEmoji")?.gameObject;
            if (iconEmoji != null) iconEmoji.SetActive(icon == null);

            var nameText = card.Find("Body/NameRow/Name")?.GetComponent<Text>();
            if (nameText != null) nameText.text = name;

            var badgeBg = card.Find("Body/NameRow/Badge")?.GetComponent<Image>();
            if (badgeBg != null) badgeBg.color = badgeColor;
            var badgeLabelText = card.Find("Body/NameRow/Badge/Label")?.GetComponent<Text>();
            if (badgeLabelText != null) badgeLabelText.text = badgeLabel;

            var effTextComp = card.Find("Body/Eff")?.GetComponent<Text>();
            if (effTextComp != null) effTextComp.text = effText;

            var lockRoot = card.Find("Lock");
            if (lockRoot != null)
            {
                lockRoot.gameObject.SetActive(!unlocked);
                var hintText = lockRoot.Find("LockCol/Hint")?.GetComponent<Text>();
                if (hintText != null) hintText.text = "해금: " + (unlockHint ?? "조건 미정");
            }

            var selectedMark = card.Find("Selected");
            if (selectedMark != null) selectedMark.gameObject.SetActive(selected);

            var outline = card.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = selected;
                if (selected) SetOutlineAlpha(outline, 0.55f);
            }

            var button = card.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = unlocked;
                if (unlocked)
                {
                    string capturedTab = tab;
                    string capturedId = isNoneDevice ? "" : id;
                    button.onClick.AddListener(() => Pick(capturedTab, capturedId));
                }
            }

            string pickedId = isNoneDevice ? "" : id;
            if (selected && outline != null && tab == _justPickedTab && pickedId == _justPickedId)
                StartCoroutine(PulseOutline(outline));
        }

        // 선택 시 outline 글로우 펄스 — ENGINE_PORT_DESIGN.md S7 "화면 사양" PickView 지시.
        private IEnumerator PulseOutline(Outline outline)
        {
            if (outline == null) yield break;
            yield return UiTween.FloatRoutine(0.25f, 1f, 0.18f, a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
            if (outline == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0.55f, 0.22f, a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
        }

        private static void SetOutlineAlpha(Outline outline, float a)
        {
            if (outline == null) return;
            var c = outline.effectColor;
            c.a = a;
            outline.effectColor = c;
        }

        private static void ClearChildren(RectTransform parent, RectTransform skip)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (skip != null && child == skip) continue;
                Destroy(child.gameObject);
            }
        }

        // ── 선택 ──────────────────────────────────────────────────────────────────────
        private void Pick(string tab, string id)
        {
            if (tab == "char") _selChar = id;
            else if (tab == "mac") _selMac = id;
            else _selDev = id;

            _justPickedTab = tab;
            _justPickedId = id;

            RefreshTabLabels();
            RenderSection();
            UpdateSummary();

            if (tab == "char" && !_advancedChar) { _advancedChar = true; SetTab("mac"); }
            else if (tab == "mac" && !_advancedMac) { _advancedMac = true; SetTab("dev"); }
        }

        // ── 추천 조합(PickScreen.ApplyReco 이관) ─────────────────────────────────────
        private void ApplyReco(string kind)
        {
            var uc = UnlockedIds("char");
            var um = UnlockedIds("mac");
            var ud = UnlockedIds("dev");

            string c, m, d;
            if (kind == "random")
            {
                c = uc.Count > 0 ? uc[UnityEngine.Random.Range(0, uc.Count)] : "novice";
                m = um.Count > 0 ? um[UnityEngine.Random.Range(0, um.Count)] : "basic";
                d = (ud.Count > 0 && UnityEngine.Random.value < 0.6f) ? ud[UnityEngine.Random.Range(0, ud.Count)] : "";
            }
            else if (!PickMeta.TryRecommend(kind, uc, um, ud, out c, out m, out d))
            {
                return;
            }

            _selChar = c;
            _selMac = m;
            _selDev = d ?? "";
            _advancedChar = true;
            _advancedMac = true;

            RefreshTabLabels();
            RenderSection();
            UpdateSummary();

            var toast = appRoot != null && appRoot.Router != null ? appRoot.Router.Toast : null;
            toast?.Show(RecoLabel(kind) + " 조합을 적용했습니다.");
        }

        private static string RecoLabel(string kind)
        {
            switch (kind)
            {
                case "beginner": return "입문";
                case "high": return "고점";
                case "challenge": return "도전";
                default: return "랜덤";
            }
        }

        // ── 요약 패널(PickScreen.UpdateSummary 이관) ─────────────────────────────────
        private void UpdateSummary()
        {
            var c = !string.IsNullOrEmpty(_selChar) ? MetaOf("char", _selChar) : null;
            var m = !string.IsNullOrEmpty(_selMac) ? MetaOf("mac", _selMac) : null;
            var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;

            string charPart = c != null ? $"{c.emoji}{c.name}" : "캐릭터?";
            string macPart = m != null ? $"{m.emoji}{m.name}" : "머신?";
            string devPart = d != null ? $"{d.emoji}{d.name}" : "🚫장치없이";
            if (comboText != null) comboText.text = $"{charPart} + {macPart} + {devPart}";

            bool ready = c != null && m != null;
            if (startButton != null) startButton.interactable = ready;

            if (!ready)
            {
                if (gradeText != null) gradeText.text = "";
                if (blurbText != null) blurbText.text = "캐릭터와 슬롯머신을 고르면 조합 시너지·점수 고점·안정성·난이도를 분석해 드려요.";
                if (ceilingValueText != null) ceilingValueText.text = "";
                if (stabilityValueText != null) stabilityValueText.text = "";
                if (difficultyValueText != null) difficultyValueText.text = "";
                if (difficultyLabelText != null) difficultyLabelText.text = "난이도";
                if (prosText != null) prosText.text = "";
                if (consText != null) consText.text = "";
                if (buildText != null) buildText.text = "";
                return;
            }

            var ev = PickMeta.Evaluate(_selChar, _selMac, _selDev);
            if (ev == null) return;

            if (gradeText != null)
            {
                gradeText.text = $"시너지 {ev.grade}";
                gradeText.color = ev.gradeColor;
            }
            if (ceilingValueText != null) ceilingValueText.text = ev.ceilingStars;
            if (stabilityValueText != null) stabilityValueText.text = ev.stabilityStars;
            if (difficultyValueText != null) difficultyValueText.text = ev.difficultyStars;
            if (difficultyLabelText != null) difficultyLabelText.text = $"난이도 · {ev.diffLabel}";
            if (blurbText != null) blurbText.text = $"시너지 {ev.grade}. {ev.blurb}";
            if (prosText != null) prosText.text = (ev.pros != null && ev.pros.Count > 0) ? string.Join("\n", ev.pros) : "";
            if (consText != null) consText.text = (ev.cons != null && ev.cons.Count > 0) ? string.Join("\n", ev.cons) : "특별한 약점 없음";
            if (buildText != null) buildText.text = ev.buildTokens != null ? string.Join(" / ", ev.buildTokens) : "";
        }

        private void OnStartClicked()
        {
            if (string.IsNullOrEmpty(_selChar) || string.IsNullOrEmpty(_selMac)) return;
            appRoot?.StartRun(_selChar, _selMac, _selDev ?? "");
        }

        // ── 정렬(PickScreen.SortIds/Metric 이관) ─────────────────────────────────────
        private List<string> SortIds(List<string> ids)
        {
            var full = new List<string>(TabIds(_tab));
            var result = new List<string>(ids);
            result.Sort((a, b) =>
            {
                int ua = IsUnlocked(_tab, a) ? 0 : 1;
                int ub = IsUnlocked(_tab, b) ? 0 : 1;
                if (ua != ub) return ua - ub;
                double ma = Metric(a);
                double mb = Metric(b);
                int cmp = ma.CompareTo(mb);
                if (cmp != 0) return cmp;
                return full.IndexOf(a) - full.IndexOf(b);
            });
            return result;
        }

        private double Metric(string id)
        {
            var m = MetaOf(_tab, id);
            if (m == null) return 0;
            if (_sort == "diff") return m.diff != 0 ? m.diff : 2;
            if (_sort == "ceiling") return -m.ceiling;
            if (_sort == "recent") return -PickMeta.UnlockOrder(m);

            // reco
            if (_tab == "char")
            {
                bool beginner = m.tags != null && Array.IndexOf(m.tags, "초보추천") >= 0;
                return (beginner ? -3 : 0) + (m.diff != 0 ? m.diff : 2);
            }
            if (!string.IsNullOrEmpty(_selChar))
            {
                EvalResult ev = _tab == "mac"
                    ? PickMeta.Evaluate(_selChar, id, _selDev)
                    : PickMeta.Evaluate(_selChar, string.IsNullOrEmpty(_selMac) ? FirstUnlockedMac() : _selMac, id);
                if (ev != null) return GradeRankOf(ev.grade) * 4 - m.ceiling * 0.3;
            }
            return m.diff != 0 ? m.diff : 2;
        }

        private static int GradeRankOf(string grade) => GradeRank.TryGetValue(grade, out var r) ? r : 3;

        private string FirstUnlockedMac()
        {
            foreach (var id in PickMeta.MacOrder)
                if (IsUnlocked("mac", id)) return id;
            return "basic";
        }

        // ── 데이터 접근 ───────────────────────────────────────────────────────────────
        private static string[] TabIds(string tab)
        {
            if (tab == "char") return PickMeta.CharOrder;
            if (tab == "mac") return PickMeta.MacOrder;
            return PickMeta.DevOrder;
        }

        private static CatalogEntry EntryOf(string tab, string id)
        {
            if (string.IsNullOrEmpty(id) && tab != "dev") return null;
            var pickId = JackpotCatalog.PickIdOf(tab, id ?? string.Empty);
            return JackpotCatalog.Get(pickId);
        }

        private static PickInfo MetaOf(string tab, string id)
        {
            var e = EntryOf(tab, id);
            return (e != null && e.hasPick) ? e.pick : null;
        }

        // 실 프로필 해금 연동 — PlayerProfile.IsCharUnlocked/IsMachineUnlocked/IsDeviceUnlocked
        // (Engine/Profile/PlayerProfile.cs)를 그대로 쓴다. DemoData는 더 이상 참조하지 않는다.
        private bool IsUnlocked(string tab, string id)
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return false;
            if (tab == "char") return profile.IsCharUnlocked(Characters.ById(id));
            if (tab == "mac") return profile.IsMachineUnlocked(Machines.ById(id));
            return profile.IsDeviceUnlocked(Devices.ById(id));
        }

        private List<string> UnlockedIds(string tab)
        {
            var result = new List<string>();
            foreach (var id in TabIds(tab))
                if (IsUnlocked(tab, id)) result.Add(id);
            return result;
        }
    }
}
