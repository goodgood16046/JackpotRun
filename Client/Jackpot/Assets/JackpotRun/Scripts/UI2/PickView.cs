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

        // AppRoot는 DontDestroyOnLoad 싱글턴(S8)이라 씬에 없다 — SceneBuilder가 와이어링할 수 없으므로
        // 정적 인스턴스를 계산 프로퍼티로 읽는다(호출부는 그대로 "appRoot.XXX").
        private AppRoot appRoot => AppRoot.Instance;

        [Header("head — S10 index.html #who")]
        [SerializeField] private Text headWhoText;

        [Header("추천 4버튼 — 순서 고정: 입문/고점/도전/랜덤 (RecoKinds)")]
        [SerializeField] private Button[] recoButtons = Array.Empty<Button>();

        [Header("탭 3버튼 — 순서 고정: 캐릭터/머신/장치 (TabOrder)")]
        [SerializeField] private Button[] tabButtons = Array.Empty<Button>();
        [SerializeField] private Image[] tabButtonImages = Array.Empty<Image>();
        [SerializeField] private Text[] tabNumTexts = Array.Empty<Text>(); // S10 .tnum(①/②/③ + 완료 시 " ✓")
        [SerializeField] private Text[] tabLabelTexts = Array.Empty<Text>();

        [Header("필터 칩")]
        [SerializeField] private RectTransform chipsContent;
        [SerializeField] private RectTransform chipTemplate;

        [Header("정렬 4버튼 — 순서 고정: 추천순/난이도순/고점순/최근해금순 (SortKeys)")]
        [SerializeField] private Button[] sortButtons = Array.Empty<Button>();
        [SerializeField] private Image[] sortButtonImages = Array.Empty<Image>();

        [Header("sechead — S10 index.html .sechead")]
        [SerializeField] private Text sectionTitleText;
        [SerializeField] private Text sectionCountText;

        [Header("카드 그리드")]
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private CanvasGroup gridCanvasGroup;
        [SerializeField] private RectTransform cardTemplate;

        [Header("요약 패널")]
        [SerializeField] private Text comboText;
        [SerializeField] private Text comboBuildText; // S10 .sum-combo .bl
        [SerializeField] private Text gradeText;
        [SerializeField] private Image gradeBadgeImage; // S10 .sum-grade 배경(등급색 저알파 틴트)
        [SerializeField] private Text ceilingValueText;
        [SerializeField] private Text stabilityValueText;
        [SerializeField] private Text difficultyValueText;
        [SerializeField] private Text difficultyLabelText;
        [SerializeField] private Text blurbText;
        [SerializeField] private Text prosText;
        [SerializeField] private Text consText;
        [SerializeField] private RectTransform buildChipsContent; // S10 .sd-builds
        [SerializeField] private RectTransform buildChipTemplate;
        [SerializeField] private Button startButton;

        // pick.css .tab .tnum 텍스트(①/②/③) — 완료 시 PickMeta 없이 그대로 " ✓"(green) 접미한다.
        private static readonly string[] TabNums = { "①", "②", "③" };

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

            if (headWhoText != null)
            {
                string nick = LoginView.SavedNick();
                headWhoText.text = (string.IsNullOrEmpty(nick) ? "" : "@" + nick + " — ") +
                    "캐릭터 + 머신 + 장치를 골라 시작을 예약하세요";
            }

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

            // pick.css .tab.done .tnum::after{content:" ✓";color:green} — 캐릭터/머신은 선택해야 완료,
            // 장치는 "장치 없이"도 완료로 간주(app.js refreshTabLabels()의 dev 탭 .add("done") 그대로).
            bool[] done = { c != null, m != null, true };
            for (int i = 0; i < tabNumTexts.Length && i < TabNums.Length; i++)
                tabNumTexts[i].text = TabNums[i] + (done[i] ? " <color=#4ADE80>✓</color>" : "");
        }

        private static string TabTitleOf(string tab)
        {
            if (tab == "char") return "캐릭터";
            if (tab == "mac") return "슬롯머신";
            return "장치";
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

            // S10 .sechead: 제목 + "해금 n/m"(+필터 적용 중이면 필터명도).
            if (sectionTitleText != null) sectionTitleText.text = TabTitleOf(_tab);
            if (sectionCountText != null)
            {
                var full = TabIds(_tab);
                int total = full.Length;
                int unlocked = 0;
                foreach (var id in full) if (IsUnlocked(_tab, id)) unlocked++;
                sectionCountText.text = _filter != "전체"
                    ? $"해금 {unlocked}/{total} · 필터 “{_filter}”"
                    : $"해금 {unlocked}/{total}";
            }

            _justPickedTab = null;
            _justPickedId = null;
        }

        // S10 — pick.css .jcard 구조 그대로 채운다(UiSceneBuilder.BuildCardTemplate의 Find 경로 계약과
        // 정확히 일치해야 한다). 로직(정렬/필터/추천/Evaluate/해금판정)은 그대로, 채워 넣는 필드만 확장.
        private void BuildCard(string tab, string id, bool isNoneDevice)
        {
            string name, role, effText, badgeLabel, unlockHint, prosConsRich, footRich;
            Color badgeColor;
            string[] tags = null;
            Sprite icon = null;
            bool unlocked;
            bool selected;
            bool hasBadge;
            bool hasBody; // ProsCons/Foot(unlocked) 또는 UnlockBox(locked) — none 카드는 둘 다 숨김

            if (isNoneDevice)
            {
                name = "장치 없이";
                role = "기본 · 장치 미장착";
                effText = "이번 런은 장치를 장착하지 않습니다. 순수 캐릭터·머신 조합.";
                badgeLabel = "";
                badgeColor = UiKit.Dim2;
                unlocked = true;
                selected = _selDev == "";
                unlockHint = null;
                prosConsRich = "";
                footRich = "";
                hasBadge = false;
                hasBody = false;
            }
            else
            {
                var entry = EntryOf(tab, id);
                var m = (entry != null && entry.hasPick) ? entry.pick : null;
                if (m == null) return;

                name = m.name;
                role = m.role;
                effText = m.eff;
                tags = m.tags;
                icon = JackpotCatalog.LoadSprite(entry);
                unlocked = IsUnlocked(tab, id);
                selected = (tab == "char" && _selChar == id) || (tab == "mac" && _selMac == id) || (tab == "dev" && _selDev == id);
                hasBadge = true;
                hasBody = true;

                if (tab == "dev")
                {
                    badgeLabel = string.IsNullOrEmpty(m.kind) ? "능동" : m.kind;
                    badgeColor = m.kind == "패시브" ? UiKit.Teal : UiKit.Blue;
                    string line1 = !string.IsNullOrEmpty(m.cmd) ? $"<b>명령</b> .{m.cmd}" : "<b>유형</b> 패시브(상시 자동)";
                    prosConsRich = line1 + "\n" + $"<b>쿨다운</b> {m.cool}";
                    footRich = $"추천: <color=#FFD23F><b>{m.when}</b></color>";
                }
                else
                {
                    badgeLabel = PickMeta.DiffLabel(m.diff);
                    badgeColor = PickMeta.DiffColor(m.diff);
                    var lines = new List<string>();
                    if (m.pros != null)
                        for (int i = 0; i < m.pros.Length && lines.Count < 2; i++)
                            lines.Add($"<color=#4ADE80><b>＋</b></color> {m.pros[i]}");
                    if (m.cons != null && m.cons.Length > 0)
                        lines.Add($"<color=#FF9B9B><b>－</b></color> {m.cons[0]}");
                    prosConsRich = string.Join("\n", lines);
                    footRich = $"추천 빌드: <color=#FFD23F><b>{m.build}</b></color>";
                }
                unlockHint = !string.IsNullOrEmpty(m.unlock) ? m.unlock : "조건 미정";
            }

            var card = Instantiate(cardTemplate, gridContent);
            card.gameObject.SetActive(true);
            card.name = isNoneDevice ? "Card_none" : "Card_" + id;

            var stripeImg = card.Find("Stripe")?.GetComponent<Image>();
            if (stripeImg != null) stripeImg.color = badgeColor;

            var iconImage = card.Find("Body/Top/IconSlot/Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
            var iconEmojiText = card.Find("Body/Top/IconSlot/IconEmoji")?.GetComponent<Text>();
            if (iconEmojiText != null)
            {
                // S8 항목⑤ 관례 그대로: astral 이모지가 렌더링되지 않아 BMP 대체 기호를 쓴다.
                iconEmojiText.text = "⊘";
                iconEmojiText.gameObject.SetActive(icon == null);
            }

            var nameText = card.Find("Body/Top/Info/NameRow/Name")?.GetComponent<Text>();
            if (nameText != null) nameText.text = name;

            var badgeRoot = card.Find("Body/Top/Info/NameRow/Badge");
            if (badgeRoot != null)
            {
                badgeRoot.gameObject.SetActive(hasBadge);
                if (hasBadge)
                {
                    var badgeBg = badgeRoot.GetComponent<Image>();
                    if (badgeBg != null) badgeBg.color = badgeColor;
                    var badgeLabelText = badgeRoot.Find("Label")?.GetComponent<Text>();
                    if (badgeLabelText != null) badgeLabelText.text = badgeLabel;
                }
            }

            var roleText = card.Find("Body/Top/Info/Role")?.GetComponent<Text>();
            if (roleText != null) roleText.text = role;

            var effTextComp = card.Find("Body/Eff/Text")?.GetComponent<Text>();
            if (effTextComp != null) effTextComp.text = effText;

            // .jc-tags — 고정 4슬롯(Tag0..Tag3), Unity에 flex-wrap이 없어 비줄바꿈 한 줄로 재해석.
            var tagsRoot = card.Find("Body/Tags");
            for (int i = 0; i < 4; i++)
            {
                var slot = tagsRoot?.Find("Tag" + i);
                if (slot == null) continue;
                bool has = tags != null && i < tags.Length;
                slot.gameObject.SetActive(has);
                if (!has) continue;
                string tagLabel = tags[i];
                string cls = PickMeta.TagClass.TryGetValue(tagLabel, out var c) ? c : "";
                var slotBg = slot.GetComponent<Image>();
                if (slotBg != null) slotBg.color = UiKit.TagBg(cls);
                var slotLabel = slot.Find("Label")?.GetComponent<Text>();
                if (slotLabel != null)
                {
                    slotLabel.text = tagLabel;
                    slotLabel.color = UiKit.TagFg(cls);
                }
            }

            // 잠금 시 opacity .62(css .jcard.locked) — 채도 감소(filter:saturate(.5))는 Unity UI로
            // 재현할 수 없어 생략(S10 재해석 항목, 보고 대상).
            var canvasGroup = card.GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = unlocked ? 1f : 0.62f;

            var prosConsGo = card.Find("Body/ProsCons")?.gameObject;
            var footGo = card.Find("Body/Foot")?.gameObject;
            var unlockBoxGo = card.Find("Body/UnlockBox")?.gameObject;
            bool showBody = hasBody && unlocked;
            bool showUnlockBox = hasBody && !unlocked;
            prosConsGo?.SetActive(showBody);
            footGo?.SetActive(showBody);
            unlockBoxGo?.SetActive(showUnlockBox);
            if (showBody)
            {
                var prosConsText = prosConsGo?.GetComponent<Text>();
                if (prosConsText != null) prosConsText.text = prosConsRich;
                var footText = footGo?.GetComponent<Text>();
                if (footText != null) footText.text = footRich;
            }
            else if (showUnlockBox)
            {
                var unlockText = card.Find("Body/UnlockBox/Text")?.GetComponent<Text>();
                if (unlockText != null) unlockText.text = $"<color=#8B93A7>해금:</color> {unlockHint}";
            }

            // .jc-lock(우상단 🔒 잠김) / .jc-check(선택됨 ✓) — app.js corner 로직대로 상호배타라
            // 노드 하나(Corner)를 상태별로 다시 칠해 재사용한다.
            var corner = card.Find("Corner");
            if (corner != null)
            {
                var cornerImg = corner.GetComponent<Image>();
                var cornerLabel = corner.Find("Label")?.GetComponent<Text>();
                if (!unlocked)
                {
                    corner.gameObject.SetActive(true);
                    if (cornerImg != null) cornerImg.color = new Color(14f / 255f, 16f / 255f, 25f / 255f, 0.85f);
                    if (cornerLabel != null) { cornerLabel.text = "잠김"; cornerLabel.color = UiKit.TextSecondary; }
                }
                else if (selected)
                {
                    corner.gameObject.SetActive(true);
                    if (cornerImg != null) cornerImg.color = UiKit.Gold;
                    if (cornerLabel != null) { cornerLabel.text = "선택됨 ✓"; cornerLabel.color = UiKit.Bg; }
                }
                else
                {
                    corner.gameObject.SetActive(false);
                }
            }

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

            var toast = appRoot != null ? appRoot.Toast : null;
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

        // ── 요약 패널(PickScreen.UpdateSummary 이관, S10: pick.css .sum-*/.sd-* 룩) ───────────
        private void UpdateSummary()
        {
            var c = !string.IsNullOrEmpty(_selChar) ? MetaOf("char", _selChar) : null;
            var m = !string.IsNullOrEmpty(_selMac) ? MetaOf("mac", _selMac) : null;
            var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;

            string charPart = c != null ? $"{c.emoji}{c.name}" : "캐릭터?";
            string macPart = m != null ? $"{m.emoji}{m.name}" : "머신?";
            string devPart = d != null ? $"{d.emoji}{d.name}" : "⊘장치없이";
            if (comboText != null) comboText.text = $"{charPart} + {macPart} + {devPart}";

            bool ready = c != null && m != null;
            if (startButton != null) startButton.interactable = ready;

            if (!ready)
            {
                if (comboBuildText != null) comboBuildText.text = "";
                if (gradeText != null) gradeText.text = "";
                if (gradeBadgeImage != null) gradeBadgeImage.color = new Color(0f, 0f, 0f, 0f);
                if (blurbText != null) blurbText.text = "캐릭터와 슬롯머신을 고르면 조합 시너지·점수 고점·안정성·난이도를 분석해 드려요.";
                if (ceilingValueText != null) ceilingValueText.text = "";
                if (stabilityValueText != null) stabilityValueText.text = "";
                if (difficultyValueText != null) difficultyValueText.text = "";
                if (difficultyLabelText != null) difficultyLabelText.text = "난이도";
                if (prosText != null) prosText.text = "";
                if (consText != null) consText.text = "";
                ClearBuildChips();
                return;
            }

            var ev = PickMeta.Evaluate(_selChar, _selMac, _selDev);
            if (ev == null) return;

            if (comboBuildText != null)
                comboBuildText.text = ev.buildTokens != null ? string.Join(" / ", ev.buildTokens) : "";

            if (gradeText != null)
            {
                gradeText.text = $"시너지 {ev.grade}";
                gradeText.color = ev.gradeColor;
            }
            if (gradeBadgeImage != null)
            {
                var badgeTint = ev.gradeColor;
                badgeTint.a = 0.14f; // pick.css .sum-grade{border:1px solid currentColor} 재해석 — 저알파 배경 틴트
                gradeBadgeImage.color = badgeTint;
            }
            if (ceilingValueText != null) ceilingValueText.text = ev.ceilingStars;
            if (stabilityValueText != null) stabilityValueText.text = ev.stabilityStars;
            if (difficultyValueText != null) difficultyValueText.text = ev.difficultyStars;
            if (difficultyLabelText != null) difficultyLabelText.text = $"난이도 · {ev.diffLabel}";
            if (blurbText != null) blurbText.text = $"<color=#FFD23F><b>시너지 {ev.grade}.</b></color> {ev.blurb}";

            // pick.css .sd-box.pro li::before(▲ green) / .con li::before(▼ #ff9b9b) 프리픽스.
            if (prosText != null)
                prosText.text = (ev.pros != null && ev.pros.Count > 0)
                    ? string.Join("\n", ev.pros.ConvertAll(p => "<color=#4ADE80>▲</color> " + p))
                    : "";
            if (consText != null)
                consText.text = (ev.cons != null && ev.cons.Count > 0)
                    ? string.Join("\n", ev.cons.ConvertAll(p => "<color=#FF9B9B>▼</color> " + p))
                    : "<color=#FF9B9B>▼</color> 특별한 약점 없음";

            RenderBuildChips(ev.buildTokens);
        }

        // .sd-builds — 빌드 토큰을 골드 필 칩으로(필터 칩과 동일한 템플릿 인스턴스화 기법).
        private void RenderBuildChips(List<string> tokens)
        {
            ClearBuildChips();
            if (buildChipsContent == null || buildChipTemplate == null || tokens == null) return;
            foreach (var token in tokens)
            {
                var chip = Instantiate(buildChipTemplate, buildChipsContent);
                chip.gameObject.SetActive(true);
                chip.name = "BuildChip_" + token;
                var bg = chip.GetComponent<Image>();
                if (bg != null) bg.color = new Color(1f, 0.824f, 0.247f, 0.06f);
                var label = chip.Find("Label")?.GetComponent<Text>();
                if (label != null) { label.text = token; label.color = UiKit.Accent; }
            }
        }

        private void ClearBuildChips()
        {
            if (buildChipsContent == null) return;
            for (int i = buildChipsContent.childCount - 1; i >= 0; i--)
            {
                var child = buildChipsContent.GetChild(i);
                if (buildChipTemplate != null && child == buildChipTemplate) continue;
                Destroy(child.gameObject);
            }
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
