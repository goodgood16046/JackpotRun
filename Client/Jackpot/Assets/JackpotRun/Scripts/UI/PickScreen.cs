using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EvalResult = JackpotRun.Data.EvalResult;

namespace JackpotRun.UI
{
    /// <summary>
    /// jackpotpick 포팅 — 캐릭터/머신/장치 선택 화면.
    /// 정렬·필터·추천·자동 탭 진행·요약 갱신 로직은 원본 app.js를 그대로 따른다.
    /// </summary>
    public static class PickScreen
    {
        private static readonly Dictionary<string, int> GradeRank = new Dictionary<string, int>
        {
            { "S", 0 }, { "A", 1 }, { "B", 2 }, { "C", 3 },
        };

        private static readonly string[] SortKeys = { "reco", "diff", "ceiling", "recent" };

        public static void Build(RectTransform parent, JackpotRunApp app)
        {
            new Ctx(parent, app).Init();
        }

        private class Ctx
        {
            private readonly RectTransform _parent;
            private readonly JackpotRunApp _app;
            private readonly JackpotRun.Data.PlayerState _player;

            // 상태
            private string _tab = "char";
            private string _filter = "전체";
            private string _sort = "reco";
            private string _selChar;
            private string _selMac;
            private string _selDev = "";
            private bool _advancedChar;
            private bool _advancedMac;

            // 탭 UI
            private static readonly (string tab, string title)[] TabDefs =
            {
                ("char", "🎭 캐릭터"), ("mac", "🎰 슬롯머신"), ("dev", "🔧 장치"),
            };
            private readonly Image[] _tabButtonImages = new Image[3];
            private readonly Text[] _tabLabelTexts = new Text[3];

            // 필터/그리드 UI
            private RectTransform _chipsContent;
            private RectTransform _gridContent;
            private Dropdown _sortDropdown;

            // 요약 패널 UI
            private Text _sumComboText;
            private Text _sumGradeText;
            private Text _sumBlurbText;
            private Text _sumCeilingValue;
            private Text _sumStabilityValue;
            private Text _sumDifficultyValue;
            private Text _sumDifficultyLabel;
            private Text _sumProsText;
            private Text _sumConsText;
            private Text _sumBuildText;
            private Text _sumMsgText;
            private Button _startButton;

            public Ctx(RectTransform parent, JackpotRunApp app)
            {
                _parent = parent;
                _app = app;
                _player = app.Player;
            }

            public void Init()
            {
                var root = UiFactory.VGroup(_parent, 0, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.Fill(root);

                BuildHeader(root);
                BuildRecoRow(root);
                BuildTabsRow(root);
                BuildChipsRow(root);
                BuildSortRow(root);
                BuildGrid(root);
                BuildSummary(root);

                RefreshTabLabels();
                UpdateTabHighlight();
                RenderChips();
                RenderSection();
                UpdateSummary();
            }

            // ── 헤더 ──────────────────────────────────────────
            private void BuildHeader(RectTransform root)
            {
                var header = UiFactory.HGroup(root, 16, new RectOffset(24, 24, 16, 16), true, true);
                UiFactory.SizeHint(header, preferredHeight: 110);

                var who = UiFactory.Text(header, $"@{_player.nick} — 캐릭터+머신+장치를 골라 시작을 예약하세요",
                    26, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft);
                UiFactory.SizeHint(who, flexibleWidth: 1);

                var menuBtn = UiFactory.Button(header, "← 메뉴", new Vector2(160, 70),
                    UiFactory.Hex("#2A3048"), UiFactory.Hex("#E8EAF2"), () => _app.ShowMenu());
                UiFactory.SizeHint(menuBtn, preferredWidth: 160);
            }

            // ── 추천 버튼 ──────────────────────────────────────
            private void BuildRecoRow(RectTransform root)
            {
                var row = UiFactory.HGroup(root, 12, new RectOffset(24, 24, 8, 8), true, true);
                UiFactory.SizeHint(row, preferredHeight: 84);

                AddRecoButton(row, "입문", "beginner");
                AddRecoButton(row, "고점", "high");
                AddRecoButton(row, "도전", "challenge");
                AddRecoButton(row, "랜덤", "random");
            }

            private void AddRecoButton(RectTransform row, string label, string kind)
            {
                var btn = UiFactory.Button(row, label, new Vector2(0, 64),
                    UiFactory.Hex("#1B2138"), UiFactory.Hex("#FFD23F"), () => ApplyReco(kind));
                UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 64);
            }

            // ── 탭 ────────────────────────────────────────────
            private void BuildTabsRow(RectTransform root)
            {
                var row = UiFactory.HGroup(root, 12, new RectOffset(24, 24, 8, 8), true, true);
                UiFactory.SizeHint(row, preferredHeight: 96);

                for (int i = 0; i < TabDefs.Length; i++)
                {
                    var def = TabDefs[i];
                    var btnGo = new GameObject("Tab_" + def.tab, typeof(RectTransform), typeof(Image), typeof(Button));
                    var rt = (RectTransform)btnGo.transform;
                    rt.SetParent(row, false);

                    var img = btnGo.GetComponent<Image>();
                    _tabButtonImages[i] = img;

                    var btn = btnGo.GetComponent<Button>();
                    btn.targetGraphic = img;
                    btn.onClick.AddListener(() => SetTab(def.tab));
                    UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 96);

                    var col = UiFactory.VGroup(rt, 2, new RectOffset(8, 8, 10, 10), true, true);
                    UiFactory.Fill(col);

                    var titleText = UiFactory.Text(col, def.title, 24, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);
                    UiFactory.SizeHint(titleText, preferredHeight: 40);

                    var labelText = UiFactory.Text(col, "선택 전", 20, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(labelText, preferredHeight: 34);
                    _tabLabelTexts[i] = labelText;
                }
            }

            private void UpdateTabHighlight()
            {
                for (int i = 0; i < TabDefs.Length; i++)
                {
                    bool active = TabDefs[i].tab == _tab;
                    _tabButtonImages[i].color = active ? UiFactory.Hex("#232A45") : UiFactory.Hex("#151A2E");
                }
            }

            private void SetTab(string tab)
            {
                _tab = tab;
                _filter = "전체";
                UpdateTabHighlight();
                RenderChips();
                RenderSection();
            }

            // ── 필터 칩 ────────────────────────────────────────
            private void BuildChipsRow(RectTransform root)
            {
                var scroll = UiFactory.Scroll(root, out var content, vertical: false);
                UiFactory.SizeHint(scroll, preferredHeight: 64);

                var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10;
                hlg.padding = new RectOffset(20, 20, 8, 8);
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childAlignment = TextAnchor.MiddleLeft;

                var csf = content.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                _chipsContent = content;
            }

            private void RenderChips()
            {
                for (int i = _chipsContent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_chipsContent.GetChild(i).gameObject);

                var present = new HashSet<string>();
                foreach (var id in TabIds(_tab))
                {
                    var m = MetaOf(_tab, id);
                    if (m?.tags != null) foreach (var t in m.tags) present.Add(t);
                }

                AddChip("전체");
                foreach (var t in JackpotRun.Data.PickMeta.ChipVocab)
                    if (present.Contains(t)) AddChip(t);
            }

            private void AddChip(string label)
            {
                bool on = _filter == label;
                var bg = on ? UiFactory.Hex("#FFD23F") : UiFactory.Hex("#1B2138");
                var fg = on ? UiFactory.Hex("#0B0E1A") : UiFactory.Hex("#E8EAF2");
                var btn = UiFactory.Button(_chipsContent, label, new Vector2(0, 48), bg, fg,
                    () => { _filter = label; RenderSection(); });
                float w = Mathf.Max(84, label.Length * 28 + 32);
                UiFactory.SizeHint(btn, preferredWidth: w, preferredHeight: 48);
            }

            // ── 정렬 ──────────────────────────────────────────
            private void BuildSortRow(RectTransform root)
            {
                var row = UiFactory.HGroup(root, 12, new RectOffset(24, 24, 8, 8), true, true);
                UiFactory.SizeHint(row, preferredHeight: 76);

                var label = UiFactory.Text(row, "정렬", 24, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleLeft);
                UiFactory.SizeHint(label, preferredWidth: 80);

                _sortDropdown = BuildSortDropdown(row);
                UiFactory.SizeHint(_sortDropdown, flexibleWidth: 1, preferredHeight: 60);
                _sortDropdown.onValueChanged.AddListener(idx =>
                {
                    _sort = SortKeys[Mathf.Clamp(idx, 0, SortKeys.Length - 1)];
                    RenderSection();
                });
            }

            private static Dropdown BuildSortDropdown(RectTransform parent)
            {
                var ddGo = new GameObject("SortDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
                var rt = (RectTransform)ddGo.transform;
                rt.SetParent(parent, false);
                ddGo.GetComponent<Image>().color = UiFactory.Hex("#1B2138");
                var dd = ddGo.GetComponent<Dropdown>();

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(rt, false);
                UiFactory.SetAnchors(labelRt, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-30, -6));
                var labelText = labelGo.GetComponent<Text>();
                labelText.font = UiFactory.Kor();
                labelText.fontSize = 26;
                labelText.color = UiFactory.Hex("#E8EAF2");
                labelText.alignment = TextAnchor.MiddleLeft;

                var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                var templateRt = (RectTransform)templateGo.transform;
                templateRt.SetParent(rt, false);
                templateRt.pivot = new Vector2(0.5f, 1f);
                templateRt.anchorMin = new Vector2(0f, 0f);
                templateRt.anchorMax = new Vector2(1f, 0f);
                templateRt.anchoredPosition = new Vector2(0f, 2f);
                templateRt.sizeDelta = new Vector2(0f, 220f);
                templateGo.GetComponent<Image>().color = UiFactory.Hex("#1B2138");
                var templateScroll = templateGo.GetComponent<ScrollRect>();
                templateScroll.horizontal = false;

                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                var viewportRt = (RectTransform)viewportGo.transform;
                viewportRt.SetParent(templateRt, false);
                UiFactory.Fill(viewportRt);
                viewportGo.GetComponent<Image>().color = Color.white;
                viewportGo.GetComponent<Mask>().showMaskGraphic = false;

                var contentGo = new GameObject("Content", typeof(RectTransform));
                var contentRt = (RectTransform)contentGo.transform;
                contentRt.SetParent(viewportRt, false);
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(0f, 0f);

                var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
                var itemRt = (RectTransform)itemGo.transform;
                itemRt.SetParent(contentRt, false);
                itemRt.anchorMin = new Vector2(0f, 0.5f);
                itemRt.anchorMax = new Vector2(1f, 0.5f);
                itemRt.sizeDelta = new Vector2(0f, 56f);

                var itemBgGo = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
                var itemBgRt = (RectTransform)itemBgGo.transform;
                itemBgRt.SetParent(itemRt, false);
                UiFactory.Fill(itemBgRt);
                itemBgGo.GetComponent<Image>().color = UiFactory.Hex("#232A45");

                var itemCheckGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
                var itemCheckRt = (RectTransform)itemCheckGo.transform;
                itemCheckRt.SetParent(itemRt, false);
                itemCheckRt.anchorMin = new Vector2(0f, 0.5f);
                itemCheckRt.anchorMax = new Vector2(0f, 0.5f);
                itemCheckRt.sizeDelta = new Vector2(20f, 20f);
                itemCheckRt.anchoredPosition = new Vector2(14f, 0f);
                itemCheckGo.GetComponent<Image>().color = UiFactory.Hex("#FFD23F");

                var itemLabelGo = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
                var itemLabelRt = (RectTransform)itemLabelGo.transform;
                itemLabelRt.SetParent(itemRt, false);
                UiFactory.SetAnchors(itemLabelRt, Vector2.zero, Vector2.one, new Vector2(30, 2), new Vector2(-10, -2));
                var itemLabelText = itemLabelGo.GetComponent<Text>();
                itemLabelText.font = UiFactory.Kor();
                itemLabelText.fontSize = 26;
                itemLabelText.color = UiFactory.Hex("#E8EAF2");
                itemLabelText.alignment = TextAnchor.MiddleLeft;

                var toggle = itemGo.GetComponent<Toggle>();
                toggle.targetGraphic = itemBgGo.GetComponent<Image>();
                toggle.graphic = itemCheckGo.GetComponent<Image>();
                toggle.isOn = true;

                templateScroll.content = contentRt;
                templateScroll.viewport = viewportRt;

                dd.template = templateRt;
                dd.captionText = labelText;
                dd.itemText = itemLabelText;

                templateGo.SetActive(false);

                dd.options.Clear();
                dd.options.Add(new Dropdown.OptionData("추천순"));
                dd.options.Add(new Dropdown.OptionData("난이도순"));
                dd.options.Add(new Dropdown.OptionData("고점순"));
                dd.options.Add(new Dropdown.OptionData("최근해금순"));
                dd.value = 0;
                dd.RefreshShownValue();

                return dd;
            }

            // ── 카드 그리드 ────────────────────────────────────
            private void BuildGrid(RectTransform root)
            {
                var scroll = UiFactory.Scroll(root, out var content, vertical: true);
                UiFactory.SizeHint(scroll, flexibleHeight: 1);
                UiFactory.Grid(content, new Vector2(500, 360), new Vector2(20, 20), 2);
                content.gameObject.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 20, 20, 20);
                _gridContent = content;
            }

            private void RenderSection()
            {
                var ids = new List<string>();
                foreach (var id in TabIds(_tab))
                {
                    if (_filter == "전체") { ids.Add(id); continue; }
                    var m = MetaOf(_tab, id);
                    if (m?.tags != null && Array.IndexOf(m.tags, _filter) >= 0) ids.Add(id);
                }
                ids = SortIds(ids);

                for (int i = _gridContent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_gridContent.GetChild(i).gameObject);

                if (_tab == "dev") BuildNoneDevCard(_gridContent);
                foreach (var id in ids) BuildCard(_gridContent, _tab, id);

                RenderChips();
            }

            // ── 정렬 로직(app.js sortIds 포팅) ───────────────────
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
                if (_sort == "recent") return -JackpotRun.Data.PickMeta.UnlockOrder(m);

                // reco
                if (_tab == "char")
                {
                    bool beginner = m.tags != null && Array.IndexOf(m.tags, "초보추천") >= 0;
                    return (beginner ? -3 : 0) + (m.diff != 0 ? m.diff : 2);
                }
                if (!string.IsNullOrEmpty(_selChar))
                {
                    EvalResult ev = _tab == "mac"
                        ? JackpotRun.Data.PickMeta.Evaluate(_selChar, id, _selDev)
                        : JackpotRun.Data.PickMeta.Evaluate(_selChar, string.IsNullOrEmpty(_selMac) ? FirstUnlockedMac() : _selMac, id);
                    if (ev != null) return GradeRankOf(ev.grade) * 4 - m.ceiling * 0.3;
                }
                return m.diff != 0 ? m.diff : 2;
            }

            private static int GradeRankOf(string grade) => GradeRank.TryGetValue(grade, out var r) ? r : 3;

            private string FirstUnlockedMac()
            {
                foreach (var id in JackpotRun.Data.PickMeta.MacOrder)
                    if (_player.machines.Contains(id)) return id;
                return "basic";
            }

            // ── 데이터 접근 ────────────────────────────────────
            private string[] TabIds(string tab)
            {
                if (tab == "char") return JackpotRun.Data.PickMeta.CharOrder;
                if (tab == "mac") return JackpotRun.Data.PickMeta.MacOrder;
                return JackpotRun.Data.PickMeta.DevOrder;
            }

            private JackpotRun.Data.CatalogEntry EntryOf(string tab, string id)
            {
                if (string.IsNullOrEmpty(id) && tab != "dev") return null;
                var pickId = JackpotRun.Data.JackpotCatalog.PickIdOf(tab, id ?? string.Empty);
                return JackpotRun.Data.JackpotCatalog.Get(pickId);
            }

            private JackpotRun.Data.PickInfo MetaOf(string tab, string id)
            {
                var e = EntryOf(tab, id);
                return (e != null && e.hasPick) ? e.pick : null;
            }

            private bool IsUnlocked(string tab, string id)
            {
                if (tab == "char") return _player.chars.Contains(id);
                if (tab == "mac") return _player.machines.Contains(id);
                return _player.ownedDevices.Contains(id);
            }

            // ── 선택 ──────────────────────────────────────────
            private void Pick(string tab, string id)
            {
                if (tab == "char") _selChar = id;
                else if (tab == "mac") _selMac = id;
                else _selDev = id;

                RefreshTabLabels();
                RenderSection();
                UpdateSummary();

                if (tab == "char" && !_advancedChar) { _advancedChar = true; SetTab("mac"); }
                else if (tab == "mac" && !_advancedMac) { _advancedMac = true; SetTab("dev"); }
            }

            private void RefreshTabLabels()
            {
                var c = !string.IsNullOrEmpty(_selChar) ? MetaOf("char", _selChar) : null;
                var m = !string.IsNullOrEmpty(_selMac) ? MetaOf("mac", _selMac) : null;
                var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;
                _tabLabelTexts[0].text = c != null ? $"{c.emoji} {c.name}" : "선택 전";
                _tabLabelTexts[1].text = m != null ? $"{m.emoji} {m.name}" : "선택 전";
                _tabLabelTexts[2].text = d != null ? $"{d.emoji} {d.name}" : "장치 없이";
            }

            // ── 추천 조합(app.js applyReco 포팅) ─────────────────
            private void ApplyReco(string kind)
            {
                string c, m, d;
                var uc = new List<string>(_player.chars);
                var um = new List<string>(_player.machines);
                var ud = new List<string>(_player.ownedDevices);

                if (kind == "random")
                {
                    c = uc.Count > 0 ? uc[UnityEngine.Random.Range(0, uc.Count)] : "novice";
                    m = um.Count > 0 ? um[UnityEngine.Random.Range(0, um.Count)] : "basic";
                    d = (ud.Count > 0 && UnityEngine.Random.value < 0.6f) ? ud[UnityEngine.Random.Range(0, ud.Count)] : "";
                }
                else
                {
                    if (!JackpotRun.Data.PickMeta.TryRecommend(kind, uc, um, ud, out c, out m, out d)) return;
                }

                _selChar = c;
                _selMac = m;
                _selDev = d ?? "";
                _advancedChar = true;
                _advancedMac = true;

                RefreshTabLabels();
                RenderSection();
                UpdateSummary();
            }

            // ── 요약 패널 ──────────────────────────────────────
            private void BuildSummary(RectTransform root)
            {
                var panel = UiFactory.Panel(root, "Summary", UiFactory.Hex("#151A2E"));
                UiFactory.SizeHint(panel, preferredHeight: 560);

                var col = UiFactory.VGroup(panel, 8, new RectOffset(24, 24, 16, 12), true, true);
                UiFactory.Fill(col);

                _sumComboText = UiFactory.Text(col, "", 30, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(_sumComboText, preferredHeight: 44);

                _sumGradeText = UiFactory.Text(col, "", 30, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(_sumGradeText, preferredHeight: 40);

                var meterRow = UiFactory.HGroup(col, 20, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(meterRow, preferredHeight: 60);
                var ceil = BuildMeterCell(meterRow, "점수 고점");
                var stab = BuildMeterCell(meterRow, "안정성");
                var diff = BuildMeterCell(meterRow, "난이도");
                _sumCeilingValue = ceil.value;
                _sumStabilityValue = stab.value;
                _sumDifficultyValue = diff.value;
                _sumDifficultyLabel = diff.label;

                _sumBlurbText = UiFactory.Text(col, "", 22, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(_sumBlurbText, preferredHeight: 56);

                var colsRow = UiFactory.HGroup(col, 20, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(colsRow, minHeight: 120, flexibleHeight: 1);
                _sumProsText = BuildListCell(colsRow, "장점", UiFactory.Hex("#34D3C0"));
                _sumConsText = BuildListCell(colsRow, "주의", UiFactory.Hex("#FF6B6B"));

                _sumBuildText = UiFactory.Text(col, "", 20, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(_sumBuildText, preferredHeight: 34);

                _startButton = UiFactory.Button(col, "시작 예약", new Vector2(0, 80),
                    UiFactory.Hex("#FFD23F"), UiFactory.Hex("#0B0E1A"), OnStartClicked);
                UiFactory.SizeHint(_startButton, preferredHeight: 80);

                _sumMsgText = UiFactory.Text(col, "", 20, UiFactory.Hex("#34D3C0"), TextAnchor.MiddleCenter);
                UiFactory.SizeHint(_sumMsgText, preferredHeight: 32);
            }

            private (Text label, Text value) BuildMeterCell(RectTransform row, string label)
            {
                var cell = UiFactory.VGroup(row, 2, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(cell, flexibleWidth: 1);
                var l = UiFactory.Text(cell, label, 19, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                UiFactory.SizeHint(l, preferredHeight: 26);
                var v = UiFactory.Text(cell, "", 26, UiFactory.Hex("#FFD23F"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(v, preferredHeight: 32);
                return (l, v);
            }

            private Text BuildListCell(RectTransform row, string title, Color color)
            {
                var cell = UiFactory.VGroup(row, 4, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(cell, flexibleWidth: 1);
                var t = UiFactory.Text(cell, title, 21, color, TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(t, preferredHeight: 28);
                var body = UiFactory.Text(cell, "", 19, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(body, flexibleHeight: 1);
                return body;
            }

            private void UpdateSummary()
            {
                var c = !string.IsNullOrEmpty(_selChar) ? MetaOf("char", _selChar) : null;
                var m = !string.IsNullOrEmpty(_selMac) ? MetaOf("mac", _selMac) : null;
                var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;

                string charPart = c != null ? $"{c.emoji}{c.name}" : "캐릭터?";
                string macPart = m != null ? $"{m.emoji}{m.name}" : "머신?";
                string devPart = d != null ? $"{d.emoji}{d.name}" : "🚫장치없이";
                _sumComboText.text = $"{charPart} + {macPart} + {devPart}";

                bool ready = c != null && m != null;
                _startButton.interactable = ready;
                _sumMsgText.text = "";

                if (!ready)
                {
                    _sumGradeText.text = "";
                    _sumBlurbText.text = "캐릭터와 슬롯머신을 고르면 조합 시너지·점수 고점·안정성·난이도를 분석해 드려요.";
                    _sumCeilingValue.text = "";
                    _sumStabilityValue.text = "";
                    _sumDifficultyValue.text = "";
                    _sumDifficultyLabel.text = "난이도";
                    _sumProsText.text = "";
                    _sumConsText.text = "";
                    _sumBuildText.text = "";
                    return;
                }

                var ev = JackpotRun.Data.PickMeta.Evaluate(_selChar, _selMac, _selDev);
                _sumGradeText.text = $"시너지 {ev.grade}";
                _sumGradeText.color = ev.gradeColor;
                _sumCeilingValue.text = ev.ceilingStars;
                _sumStabilityValue.text = ev.stabilityStars;
                _sumDifficultyValue.text = ev.difficultyStars;
                _sumDifficultyLabel.text = $"난이도 · {ev.diffLabel}";
                _sumBlurbText.text = $"시너지 {ev.grade}. {ev.blurb}";
                _sumProsText.text = (ev.pros != null && ev.pros.Count > 0) ? string.Join("\n", ev.pros) : "";
                _sumConsText.text = (ev.cons != null && ev.cons.Count > 0) ? string.Join("\n", ev.cons) : "특별한 약점 없음";
                _sumBuildText.text = ev.buildTokens != null ? string.Join(" / ", ev.buildTokens) : "";
            }

            private void OnStartClicked()
            {
                if (string.IsNullOrEmpty(_selChar) || string.IsNullOrEmpty(_selMac)) return;
                var c = MetaOf("char", _selChar);
                var m = MetaOf("mac", _selMac);
                var d = !string.IsNullOrEmpty(_selDev) ? MetaOf("dev", _selDev) : null;
                string devTxt = d != null ? $" + {d.emoji}{d.name}" : " + 🚫장치없이";
                string combo = $"{c.emoji}{c.name} + {m.emoji}{m.name}{devTxt}";
                _sumMsgText.text = $"✅ {combo} — 로컬 데모: 예약은 백엔드 연동 후 지원";
            }

            // ── 카드 ──────────────────────────────────────────
            private void BuildCard(RectTransform parent, string tab, string id)
            {
                var entry = EntryOf(tab, id);
                var m = (entry != null && entry.hasPick) ? entry.pick : null;
                if (m == null) return;

                bool unlocked = IsUnlocked(tab, id);
                bool selected = (tab == "char" && _selChar == id) || (tab == "mac" && _selMac == id) || (tab == "dev" && _selDev == id);

                var cardGo = new GameObject("Card_" + id, typeof(RectTransform), typeof(Image));
                var cardRt = (RectTransform)cardGo.transform;
                cardRt.SetParent(parent, false);
                var cardImg = cardGo.GetComponent<Image>();
                cardImg.color = UiFactory.Hex("#1B2138");

                if (selected)
                {
                    var outline = cardGo.AddComponent<Outline>();
                    outline.effectColor = UiFactory.Hex("#FFD23F");
                    outline.effectDistance = new Vector2(3, -3);
                }

                if (unlocked)
                {
                    var cardBtn = cardGo.AddComponent<Button>();
                    cardBtn.targetGraphic = cardImg;
                    cardBtn.onClick.AddListener(() => Pick(tab, id));
                }

                var col = UiFactory.VGroup(cardRt, 6, new RectOffset(16, 16, 14, 14), true, true);
                UiFactory.Fill(col);

                var topRow = UiFactory.HGroup(col, 12, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(topRow, preferredHeight: 96);

                var sprite = JackpotRun.Data.JackpotCatalog.LoadSprite(entry);
                if (sprite != null)
                {
                    var img = UiFactory.Image(topRow, sprite, Color.white);
                    UiFactory.SizeHint(img, preferredWidth: 88, preferredHeight: 88);
                }
                else
                {
                    var emojiText = UiFactory.Text(topRow, m.emoji, 64, Color.white, TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(emojiText, preferredWidth: 88, preferredHeight: 88);
                }

                var nameCol = UiFactory.VGroup(topRow, 2, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(nameCol, flexibleWidth: 1);

                var nameRow = UiFactory.HGroup(nameCol, 8, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(nameRow, preferredHeight: 36);
                var nameText = UiFactory.Text(nameRow, m.name, 26, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(nameText, flexibleWidth: 1);

                string badgeLabel;
                Color badgeColor;
                if (tab == "dev")
                {
                    badgeLabel = string.IsNullOrEmpty(m.kind) ? "능동" : m.kind;
                    badgeColor = m.kind == "패시브" ? UiFactory.Hex("#34d3c0") : UiFactory.Hex("#5b8cff");
                }
                else
                {
                    badgeLabel = JackpotRun.Data.PickMeta.DiffLabel(m.diff);
                    badgeColor = JackpotRun.Data.PickMeta.DiffColor(m.diff);
                }
                var badge = UiFactory.Panel(nameRow, "Badge", badgeColor);
                UiFactory.SizeHint(badge, preferredWidth: Mathf.Max(70, badgeLabel.Length * 22 + 20), preferredHeight: 32);
                var badgeText = UiFactory.Text(badge, badgeLabel, 17, UiFactory.Hex("#0B0E1A"), TextAnchor.MiddleCenter, true);
                UiFactory.Fill(badgeText.rectTransform);

                var roleText = UiFactory.Text(nameCol, m.role, 19, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleLeft);
                UiFactory.SizeHint(roleText, preferredHeight: 26);

                var effText = UiFactory.Text(col, m.eff, 20, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(effText, preferredHeight: 52);

                if (m.tags != null && m.tags.Length > 0)
                {
                    var tagRow = UiFactory.HGroup(col, 6, new RectOffset(0, 0, 0, 0), true, true);
                    UiFactory.SizeHint(tagRow, preferredHeight: 32);
                    foreach (var tag in m.tags)
                    {
                        var chip = UiFactory.Panel(tagRow, "Tag", TagColor(tag));
                        UiFactory.SizeHint(chip, preferredWidth: Mathf.Max(50, tag.Length * 20 + 16), preferredHeight: 30);
                        var chipText = UiFactory.Text(chip, tag, 15, TagTextColor(tag), TextAnchor.MiddleCenter, true);
                        UiFactory.Fill(chipText.rectTransform);
                    }
                }

                if (unlocked)
                {
                    if (tab == "dev")
                    {
                        string cmdLine = !string.IsNullOrEmpty(m.cmd) ? $"명령 .{m.cmd}" : "유형 패시브(상시 자동)";
                        var cmdText = UiFactory.Text(col, cmdLine, 18, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(cmdText, preferredHeight: 26);

                        var coolText = UiFactory.Text(col, $"쿨다운 {m.cool}", 18, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(coolText, preferredHeight: 26);

                        var whenText = UiFactory.Text(col, $"추천: {m.when}", 18, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(whenText, flexibleHeight: 1);
                    }
                    else
                    {
                        var prosLines = new List<string>();
                        if (m.pros != null) for (int i = 0; i < m.pros.Length && i < 2; i++) prosLines.Add("＋ " + m.pros[i]);
                        var prosText = UiFactory.Text(col, string.Join("\n", prosLines), 17, UiFactory.Hex("#34D3C0"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(prosText, preferredHeight: 48);

                        string consLine = (m.cons != null && m.cons.Length > 0) ? "－ " + m.cons[0] : "";
                        var consText = UiFactory.Text(col, consLine, 17, UiFactory.Hex("#FF6B6B"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(consText, preferredHeight: 24);

                        var buildText = UiFactory.Text(col, $"추천 빌드: {m.build}", 17, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                        UiFactory.SizeHint(buildText, flexibleHeight: 1);
                    }
                }
                else
                {
                    BuildLockOverlay(cardRt, tab, id, m);
                }

                if (selected)
                {
                    var checkText = UiFactory.Text(cardRt, "선택됨 ✓", 17, UiFactory.Hex("#FFD23F"), TextAnchor.UpperRight, true);
                    UiFactory.SetAnchors(checkText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -34), new Vector2(-12, -6));
                }
            }

            private void BuildNoneDevCard(RectTransform parent)
            {
                bool selected = _selDev == "";

                var cardGo = new GameObject("Card_none", typeof(RectTransform), typeof(Image), typeof(Button));
                var cardRt = (RectTransform)cardGo.transform;
                cardRt.SetParent(parent, false);
                var cardImg = cardGo.GetComponent<Image>();
                cardImg.color = UiFactory.Hex("#1B2138");

                if (selected)
                {
                    var outline = cardGo.AddComponent<Outline>();
                    outline.effectColor = UiFactory.Hex("#FFD23F");
                    outline.effectDistance = new Vector2(3, -3);
                }

                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = cardImg;
                btn.onClick.AddListener(() => Pick("dev", ""));

                var col = UiFactory.VGroup(cardRt, 8, new RectOffset(16, 16, 14, 14), true, true);
                UiFactory.Fill(col);

                var topRow = UiFactory.HGroup(col, 12, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(topRow, preferredHeight: 96);
                var emojiText = UiFactory.Text(topRow, "🚫", 64, Color.white, TextAnchor.MiddleCenter);
                UiFactory.SizeHint(emojiText, preferredWidth: 88, preferredHeight: 88);

                var nameCol = UiFactory.VGroup(topRow, 2, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(nameCol, flexibleWidth: 1);
                var nameText = UiFactory.Text(nameCol, "장치 없이", 26, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(nameText, preferredHeight: 36);
                var roleText = UiFactory.Text(nameCol, "기본 · 장치 미장착", 19, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleLeft);
                UiFactory.SizeHint(roleText, preferredHeight: 26);

                var effText = UiFactory.Text(col, "이번 런은 장치를 장착하지 않습니다. 순수 캐릭터·머신 조합.",
                    20, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(effText, flexibleHeight: 1);

                if (selected)
                {
                    var checkText = UiFactory.Text(cardRt, "선택됨 ✓", 17, UiFactory.Hex("#FFD23F"), TextAnchor.UpperRight, true);
                    UiFactory.SetAnchors(checkText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -34), new Vector2(-12, -6));
                }
            }

            private void BuildLockOverlay(RectTransform cardRt, string tab, string id, JackpotRun.Data.PickInfo m)
            {
                var overlay = UiFactory.Panel(cardRt, "LockOverlay", new Color(0.043f, 0.055f, 0.102f, 0.45f));
                UiFactory.Fill(overlay);

                var col = UiFactory.VGroup(overlay, 8, new RectOffset(16, 16, 16, 16), true, true);
                UiFactory.Fill(col);

                var lockText = UiFactory.Text(col, "🔒 잠김", 26, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(lockText, preferredHeight: 40);

                var hint = ResolveUnlockHint(tab, id, m);
                var hintText = UiFactory.Text(col, $"해금: {hint.text}", 17, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(hintText, flexibleHeight: 1);

                if (hint.pct.HasValue)
                {
                    var barBg = UiFactory.Panel(col, "BarBg", UiFactory.Hex("#2A3048"));
                    UiFactory.SizeHint(barBg, preferredHeight: 14);
                    var fillRt = UiFactory.Panel(barBg, "BarFill", UiFactory.Hex("#34D3C0"));
                    float pct01 = Mathf.Clamp01(hint.pct.Value / 100f);
                    UiFactory.SetAnchors(fillRt, Vector2.zero, new Vector2(pct01, 1f), Vector2.zero, Vector2.zero);

                    var pctText = UiFactory.Text(col, $"{hint.pct.Value}%", 15, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(pctText, preferredHeight: 22);
                }
            }

            private (string text, int? pct) ResolveUnlockHint(string tab, string id, JackpotRun.Data.PickInfo m)
            {
                Dictionary<string, JackpotRun.Data.UnlockHint> bucket =
                    tab == "char" ? _player.charHints : tab == "mac" ? _player.machineHints : _player.deviceHints;

                if (bucket != null && bucket.TryGetValue(id, out var h) && !string.IsNullOrEmpty(h.text))
                {
                    int pct = Mathf.Clamp(h.pct, 0, 100);
                    return (h.text, pct);
                }
                if (m != null && !string.IsNullOrEmpty(m.unlock)) return (m.unlock, null);
                return ("조건 미정", null);
            }

            private static Color TagColor(string tag)
            {
                string cls = (JackpotRun.Data.PickMeta.TagClass != null &&
                              JackpotRun.Data.PickMeta.TagClass.TryGetValue(tag, out var c)) ? c : "";
                switch (cls)
                {
                    case "hot": return UiFactory.Hex("#FF6B6B");
                    case "good": return UiFactory.Hex("#34D3C0");
                    case "high": return UiFactory.Hex("#FFD23F");
                    default: return UiFactory.Hex("#2A3048");
                }
            }

            // 클래스 없는 태그는 어두운 칩 배경(#2A3048) — 글자는 밝게, 컬러 칩만 어둡게.
            private static Color TagTextColor(string tag)
            {
                bool bright = JackpotRun.Data.PickMeta.TagClass != null &&
                              JackpotRun.Data.PickMeta.TagClass.ContainsKey(tag);
                return bright ? UiFactory.Hex("#0B0E1A") : UiFactory.Hex("#E8EAF2");
            }
        }
    }
}
