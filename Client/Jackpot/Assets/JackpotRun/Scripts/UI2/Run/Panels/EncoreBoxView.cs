using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 앵콜 박스 — FUN_OVERHAUL_DESIGN F6 / §1 R5(클리어 점수 더블업). NodePanel(클리어 보드 + 노드 선택, NodeSelect 페이즈 한 화면)의
    // 카드 스크롤 영역 맨 위에 코드로 얹는다(씬 리빌드 불필요 — UiSceneBuilder 무변경, 전부 TMP 미사용 uGUI).
    //
    // 흐름(엔진 계약은 FunViews.cs / EncoreFlow.cs):
    //   · Encore.available → [앵콜 ×{nextMul} · 판돈 {stake}점] 버튼. 누르면 GameSession.DoEncoreSpin() — 결과(ENCORE_SPIN.spin.encoreResult)는
    //     이미 엔진 상태에 확정돼 있고, 이 뷰는 그 결과에 착지하는 연출만 한다(결정론): 5칸 굴림 → 왼쪽부터 착지 → 성공이면 팟 카운트업+
    //     ×배율 팝+상승 파티클, BUST 면 팟이 반으로 쪼개져 떨어지며 조각이 튀고 박스가 흔들리고 진동.
    //   · Encore.active(진행 중) → 팟/배율 표시 + [정산 +{pot}점] 버튼(DoEncoreCashOut). 노드 카드를 고르면 진행 중인 앵콜은 엔진이 먼저
    //     자동 정산하므로(ENCORE_SETTLED) 정산 버튼은 선택 사항이다.
    //   · BUST/정산 완료 후에는 결과 메시지만 남기고(이 클리어 보드가 떠 있는 동안) 버튼은 숨긴다. 심화(Encore==null)면 박스 없음.
    // 연출 중(Busy)에는 버튼과 NodePanel 의 노드 카드 입력을 잠근다(onBusy 콜백).
    public sealed class EncoreBoxView : MonoBehaviour
    {
        private enum Mode { Hidden, Idle, Active, Bust, Settled }

        private const float TileSize = 112f;
        private const float FullHeight = 560f;      // 버튼 포함
        private const float NoButtonHeight = 456f;  // 버튼 행 제외(560 - 96 - 간격 8)
        private const float RollFirstWait = 0.48f;  // 웹 encoreRoll: 첫 칸 480ms, 이후 190ms 간격
        private const float RollStepWait = 0.19f;
        private const float RollTickInterval = 0.065f;
        private const float PotCountUpDuration = 0.75f;
        private const int PotFontSize = 64;

        private static readonly Color BoxBg = new Color(UiKit.Purple.r, UiKit.Purple.g, UiKit.Purple.b, 0.15f);
        private static readonly Color BoxLine = new Color(UiKit.Purple.r, UiKit.Purple.g, UiKit.Purple.b, 0.65f);
        private static readonly Color BustBg = new Color(UiKit.Bad.r, UiKit.Bad.g, UiKit.Bad.b, 0.20f);
        private static readonly Color TileBg = new Color(0.12f, 0.15f, 0.25f, 1f);
        private static readonly Color GoBtnColor = new Color(0.55f, 0.36f, 0.96f, 1f);
        private static readonly HashSet<string> ValueSymIds = BuildValueSymIds();

        private sealed class Tile
        {
            public RectTransform rt;
            public Image icon;
            public Text label;
            public Outline border;
            public bool rolling;
        }

        private GameSession _session;
        private Action _onChanged;       // 팟/점수가 바뀌어 NodePanel 의 누적 총점 표시를 갱신해야 할 때
        private Action<bool> _onBusy;    // 연출 시작/종료 — NodePanel 이 노드 카드 입력을 잠근다

        // ── UI 참조 ──
        private LayoutElement _layout;
        private Image _bg;
        private Outline _bgOutline;
        private RectTransform _body;
        private Text _subText, _msgText, _potLabel, _potText, _mulText;
        private RectTransform _potWrap, _tilesRow, _btnRow;
        private Button _spinBtn, _cashBtn;
        private Text _spinBtnLabel, _cashBtnLabel;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ── 상태 ──
        private bool _busy;
        private Mode _result = Mode.Hidden;   // 이 클리어 보드에서 끝난 앵콜의 결과(Bust/Settled) — 보드가 떠 있는 동안 유지
        private long _settledPot;
        private long _lostPot;
        private string _lastMsg = "";
        private bool _lastMsgBad;
        private IReadOnlyList<Cell> _lastCells;

        public bool Busy => _busy;

        /// <summary>cardsContent(노드 카드 스택) 안에 앵콜 박스를 만든다. 반환된 뷰는 처음엔 숨겨져 있다.</summary>
        public static EncoreBoxView Create(RectTransform parent)
        {
            if (parent == null) return null;
            var go = new GameObject("EncoreBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var view = go.AddComponent<EncoreBoxView>();
            view.Build();
            go.SetActive(false);
            return view;
        }

        /// <summary>세션/콜백 연결. NodePanel.Show 마다 호출해도 된다(멱등).</summary>
        public void Bind(GameSession session, Action onChanged, Action<bool> onBusy)
        {
            _session = session;
            _onChanged = onChanged;
            _onBusy = onBusy;
        }

        private static HashSet<string> BuildValueSymIds()
        {
            var set = new HashSet<string>();
            foreach (var sym in Symbols.ValueIds)
            {
                var info = Symbols.BySym(sym);
                if (info != null) set.Add(info.id);
            }
            return set;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 구성
        // ══════════════════════════════════════════════════════════════════════
        private void Build()
        {
            _layout = GetComponent<LayoutElement>();
            _layout.preferredHeight = FullHeight;
            _layout.flexibleHeight = 0f;
            _bg = GetComponent<Image>();
            _bg.sprite = ProcSprites.RoundRect(24);
            _bg.type = Image.Type.Sliced;
            _bg.color = BoxBg;
            _bg.raycastTarget = false;
            _bgOutline = gameObject.AddComponent<Outline>();
            _bgOutline.effectColor = BoxLine;
            _bgOutline.effectDistance = new Vector2(2f, -2f);

            // Body — 흔들림(BUST) 대상. 루트는 상위 VerticalLayoutGroup 이 위치를 정하므로 직접 흔들면 레이아웃 재계산에 덮인다.
            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _body = (RectTransform)bodyGo.transform;
            _body.SetParent(transform, false);
            UiKit.Fill(_body);
            var vlg = bodyGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var title = UiKit.Text(_body, "앵콜 - 클리어 점수 더블업", 30, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(title, preferredHeight: 42, flexibleHeight: 0);

            _subText = UiKit.Text(_body, "", 19, UiKit.Txt2, TextAnchor.MiddleCenter);
            UiKit.SizeHint(_subText, preferredHeight: 56, flexibleHeight: 0);

            _tilesRow = MakeRow("Tiles", 10f, 130f);

            var potRow = MakeRow("PotRow", 8f, 100f);
            _potLabel = UiKit.Text(potRow, "판돈", 22, UiKit.TextSecondary, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(_potLabel, preferredWidth: 96, preferredHeight: 84, flexibleWidth: 0, flexibleHeight: 0);
            var wrapGo = new GameObject("PotWrap", typeof(RectTransform), typeof(LayoutElement));
            _potWrap = (RectTransform)wrapGo.transform;
            _potWrap.SetParent(potRow, false);
            var wrapLe = wrapGo.GetComponent<LayoutElement>();
            wrapLe.preferredWidth = 340f; wrapLe.preferredHeight = 84f; wrapLe.flexibleWidth = 0f; wrapLe.flexibleHeight = 0f;
            _potText = UiKit.Text(_potWrap, "0", PotFontSize, UiKit.Accent, TextAnchor.MiddleCenter, true);
            _potText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Fill(_potText.rectTransform);
            var unit = UiKit.Text(potRow, "점", 26, UiKit.Accent, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(unit, preferredWidth: 44, preferredHeight: 84, flexibleWidth: 0, flexibleHeight: 0);
            _mulText = UiKit.Text(potRow, "", 40, UiKit.Pink, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(_mulText, preferredWidth: 150, preferredHeight: 84, flexibleWidth: 0, flexibleHeight: 0);

            _msgText = UiKit.Text(_body, "", 21, UiKit.Txt2, TextAnchor.MiddleCenter);
            UiKit.SizeHint(_msgText, preferredHeight: 64, flexibleHeight: 0);

            _btnRow = MakeRow("Buttons", 12f, 96f);
            _cashBtn = UiKit.Button(_btnRow, "정산", new Vector2(0f, 96f), UiKit.Panel2, UiKit.Accent, OnCashOutClicked, ProcSprites.RoundRect(24));
            UiKit.SizeHint(_cashBtn, flexibleWidth: 1, preferredHeight: 96, flexibleHeight: 0);
            _cashBtnLabel = _cashBtn.GetComponentInChildren<Text>();
            _spinBtn = UiKit.Button(_btnRow, "앵콜", new Vector2(0f, 96f), GoBtnColor, Color.white, OnSpinClicked, ProcSprites.RoundRect(24));
            UiKit.SizeHint(_spinBtn, flexibleWidth: 1, preferredHeight: 96, flexibleHeight: 0);
            _spinBtnLabel = _spinBtn.GetComponentInChildren<Text>();
            if (_cashBtnLabel != null) _cashBtnLabel.fontSize = 30;
            if (_spinBtnLabel != null) _spinBtnLabel.fontSize = 30;
        }

        private RectTransform MakeRow(string name, float spacing, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_body, false);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
            return rt;
        }

        // ── 칸(타일) ─────────────────────────────────────────────────────────────────
        private void RebuildTiles(int count)
        {
            if (_tiles.Count == count) return;
            ClearTiles();
            for (int i = 0; i < count; i++) _tiles.Add(MakeTile(i));
        }

        private Tile MakeTile(int index)
        {
            var go = new GameObject("Tile_" + index, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_tilesRow, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = TileSize; le.preferredHeight = TileSize; le.flexibleWidth = 0f; le.flexibleHeight = 0f;
            var bg = go.GetComponent<Image>();
            bg.sprite = ProcSprites.RoundRect(16);
            bg.type = Image.Type.Sliced;
            bg.color = TileBg;
            bg.raycastTarget = false;
            var border = go.AddComponent<Outline>();
            border.effectColor = UiKit.Bd2;
            border.effectDistance = new Vector2(2f, -2f);

            var icon = UiKit.Image(rt, null, Color.white);
            icon.name = "Icon";
            icon.raycastTarget = false;
            UiKit.SetAnchors(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            icon.enabled = false;

            var label = UiKit.Text(rt, "?", 34, UiKit.Dim2, TextAnchor.MiddleCenter, true);
            label.name = "Label";
            UiKit.Fill(label.rectTransform);

            return new Tile { rt = rt, icon = icon, label = label, border = border };
        }

        private Sprite SpriteOf(string symId)
        {
            if (string.IsNullOrEmpty(symId)) return null;
            if (_spriteCache.TryGetValue(symId, out var sp)) return sp;
            sp = JackpotCatalog.LoadSymbolSprite(symId);
            _spriteCache[symId] = sp;
            return sp;
        }

        private void SetTileSymbol(Tile t, SymInfo sym)
        {
            var sp = sym != null ? SpriteOf(sym.id) : null;
            t.icon.sprite = sp;
            t.icon.enabled = sp != null;
            t.label.text = sp != null ? "" : (sym != null ? TextSanitize.StripAstral(sym.name) : "?");
            t.label.fontSize = sp != null ? 34 : 22;
        }

        private void ResetTilesToPlaceholder()
        {
            int count = _tiles.Count > 0 ? _tiles.Count : TileCountForRun();
            RebuildTiles(count);
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                t.rolling = false;
                t.icon.enabled = false;
                t.label.text = "?";
                t.label.fontSize = 34;
                t.border.effectColor = UiKit.Bd2;
                t.rt.localScale = Vector3.one;
                t.rt.localRotation = Quaternion.identity;
            }
        }

        private int TileCountForRun()
        {
            var st = _session?.State;
            bool subreel = st != null && (st.Device == "dev_subreel" || st.Device2 == "dev_subreel");
            return subreel ? Formulas.REEL + 1 : Formulas.REEL;
        }

        private void ShowCells(IReadOnlyList<Cell> cells)
        {
            RebuildTiles(cells.Count);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < cells.Count; i++)
            {
                var sym = cells[i]?.sym;
                if (sym != null && ValueSymIds.Contains(sym.id)) counts[sym.id] = (counts.TryGetValue(sym.id, out var c) ? c : 0) + 1;
            }
            for (int i = 0; i < _tiles.Count && i < cells.Count; i++)
            {
                var t = _tiles[i];
                var sym = cells[i]?.sym;
                t.rolling = false;
                SetTileSymbol(t, sym);
                t.rt.localScale = Vector3.one;
                t.rt.localRotation = Quaternion.identity;
                bool skull = sym != null && sym.id == "skull";
                bool pair = sym != null && counts.TryGetValue(sym.id, out var n) && n >= Formulas.ENCORE_MIN_SET;
                t.border.effectColor = skull ? UiKit.Bad : (pair ? UiKit.Accent : UiKit.Bd2);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 상태 반영
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>새 클리어 보드(NodePanel 첫 표시) — 지난 결과/연출 상태를 지운다.</summary>
        public void ResetForNewClear()
        {
            StopAllCoroutines();
            _busy = false;
            _result = Mode.Hidden;
            _settledPot = 0;
            _lostPot = 0;
            _lastMsg = "";
            _lastMsgBad = false;
            _lastCells = null;
            ClearHalvesAndShards();
            if (_potText != null) _potText.enabled = true;
            if (_body != null) _body.anchoredPosition = Vector2.zero;
            ClearTiles();   // 타일은 다음 Refresh/굴림에서 필요한 개수로 다시 만든다
        }

        private void ClearTiles()
        {
            if (_tilesRow == null) return;
            for (int i = _tilesRow.childCount - 1; i >= 0; i--)
            {
                var child = _tilesRow.GetChild(i);
                child.SetParent(null, false);   // 레이아웃에서 즉시 빠지게(Destroy 는 프레임 끝에 실행)
                Destroy(child.gameObject);
            }
            _tiles.Clear();
        }

        /// <summary>엔진 상태(RunController.Encore)에 맞춰 박스를 다시 그린다. 연출 중이면 끝난 뒤 알아서 다시 부른다.</summary>
        public void Refresh()
        {
            if (_busy || _session == null) return;
            var view = _session.Controller.Encore;
            Mode m;
            if (view == null) m = Mode.Hidden;
            else if (view.active) m = Mode.Active;
            else if (_result == Mode.Bust) m = Mode.Bust;
            else if (_result == Mode.Settled) m = Mode.Settled;
            else if (view.available) m = Mode.Idle;
            else m = Mode.Hidden;

            if (m == Mode.Hidden) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            ApplyMode(m, view);
        }

        private void ApplyMode(Mode m, EncoreView view)
        {
            var run = _session.State;
            int left = Mathf.Max(0, run.EncoreSpinsTotal - run.EncoreSpinsUsed);
            _bg.color = m == Mode.Bust ? BustBg : BoxBg;
            _bgOutline.effectColor = m == Mode.Bust ? UiKit.Bad : BoxLine;
            if (_potText != null) _potText.enabled = true;

            string defaultMsg = $"성공할 때마다 팟 ×{NumberFormat.Fmt(1.0 + Formulas.ENCORE_STEP)} → ×{NumberFormat.Fmt(1.0 + Formulas.ENCORE_STEP * 2)} → ...   " +
                                $"페어가 없거나 해골 {Formulas.ENCORE_BUST_SKULLS}개면 BUST(전부 소멸)";
            bool showSpin = false, showCash = false;
            switch (m)
            {
                case Mode.Idle:
                    _subText.text = $"남은 스핀 {left}회 중 1회를 쓰고 판돈 {NumberFormat.Comma(view.stake)}점을 걸어요";
                    _potLabel.text = "판돈";
                    _potText.text = NumberFormat.Comma(view.stake);
                    _potText.color = UiKit.Accent;
                    _mulText.text = "";
                    SetMessage(_lastMsg.Length > 0 ? _lastMsg : defaultMsg, _lastMsgBad);
                    if (_lastCells == null) ResetTilesToPlaceholder(); else ShowCells(_lastCells);
                    showSpin = view.available;
                    break;
                case Mode.Active:
                    _subText.text = view.nextMul.HasValue
                        ? $"남은 스핀 {left}회 · 다음 성공 시 ×{NumberFormat.Fmt(view.nextMul.Value)}"
                        : $"남은 스핀 {left}회";
                    _potLabel.text = "팟";
                    _potText.text = NumberFormat.Comma(view.pot);
                    _potText.color = UiKit.Accent;
                    _mulText.text = view.k > 0 ? "×" + NumberFormat.Fmt(1.0 + Formulas.ENCORE_STEP * view.k) : "";
                    SetMessage(_lastMsg.Length > 0 ? _lastMsg : defaultMsg, _lastMsgBad);
                    if (_lastCells == null) ResetTilesToPlaceholder(); else ShowCells(_lastCells);
                    showSpin = view.available;
                    showCash = true;
                    break;
                case Mode.Bust:
                    _subText.text = "앵콜 종료";
                    _potLabel.text = "팟";
                    _potText.text = "0";
                    _potText.color = UiKit.TextSecondary;
                    _mulText.text = "";
                    SetMessage(_lastMsg, true);
                    if (_lastCells != null) ShowCells(_lastCells);
                    break;
                case Mode.Settled:
                    _subText.text = "정산 완료";
                    _potLabel.text = "정산";
                    _potText.text = NumberFormat.Comma(_settledPot);
                    _potText.color = UiKit.Accent;
                    _mulText.text = "";
                    SetMessage(_lastMsg, false);
                    if (_lastCells != null) ShowCells(_lastCells);
                    break;
            }

            if (_spinBtn != null)
            {
                _spinBtn.gameObject.SetActive(showSpin);
                _spinBtn.interactable = !_busy;
                if (_spinBtnLabel != null && view != null && view.nextMul.HasValue)
                    _spinBtnLabel.text = m == Mode.Idle
                        ? $"앵콜 ×{NumberFormat.Fmt(view.nextMul.Value)}\n판돈 {NumberFormat.Comma(view.stake)}점"
                        : $"앵콜 ×{NumberFormat.Fmt(view.nextMul.Value)}";
            }
            if (_cashBtn != null)
            {
                _cashBtn.gameObject.SetActive(showCash);
                _cashBtn.interactable = !_busy;
                if (_cashBtnLabel != null && view != null) _cashBtnLabel.text = $"정산 +{NumberFormat.Comma(view.pot)}점";
            }
            bool anyButton = showSpin || showCash;
            if (_btnRow != null) _btnRow.gameObject.SetActive(anyButton);
            if (_layout != null) _layout.preferredHeight = anyButton ? FullHeight : NoButtonHeight;
        }

        private void SetMessage(string text, bool bad)
        {
            if (_msgText == null) return;
            _msgText.text = text ?? "";
            _msgText.color = bad ? UiKit.ConWarn : UiKit.Txt2;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_spinBtn != null) _spinBtn.interactable = !busy;
            if (_cashBtn != null) _cashBtn.interactable = !busy;
            _onBusy?.Invoke(busy);
        }

        private static void Toast(string message)
        {
            var app = AppRoot.Instance;
            if (app != null && app.Toast != null) app.Toast.Show(message);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 버튼
        // ══════════════════════════════════════════════════════════════════════
        private void OnSpinClicked()
        {
            if (_busy || _session == null) return;
            var before = _session.Controller.Encore;
            if (before == null || !before.available) { Toast("앵콜을 사용할 수 없어요"); Refresh(); return; }
            long potBefore = before.active ? before.pot : before.stake;   // 이번 스핀 전 팟(첫 스핀이면 판돈)

            var events = _session.DoEncoreSpin();
            SpinOutcome spin = null;
            string reject = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].type == "ENCORE_SPIN") spin = events[i].spin;
                else if (events[i].type == "REJECTED") reject = events[i].reason;
            }
            if (spin == null || spin.encoreResult == null || spin.result == null)
            {
                Toast(RunView.RejectReasonText(reject));
                Refresh();
                return;
            }
            StartCoroutine(EncoreRoutine(spin, potBefore));
        }

        private void OnCashOutClicked()
        {
            if (_busy || _session == null) return;
            var before = _session.Controller.Encore;
            if (before == null || !before.active) { Toast("진행 중인 앵콜이 없어요"); Refresh(); return; }
            long pot = before.pot;

            var events = _session.DoEncoreCashOut();
            string reject = null;
            bool settled = false;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].type == "ENCORE_SETTLED") { settled = true; pot = events[i].scoreDelta; }
                else if (events[i].type == "REJECTED") reject = events[i].reason;
            }
            if (!settled)
            {
                Toast(RunView.RejectReasonText(reject));
                Refresh();
                return;
            }
            _result = Mode.Settled;
            _settledPot = pot;
            _lastMsg = $"정산 완료! 팟 {NumberFormat.Comma(pot)}점이 점수에 더해졌어요";
            _lastMsgBad = false;
            SoundKit.Sfx("coin");
            SettingsSheet.SafeVibrate();
            FxKit.I?.Play(FxId.CoinSpark, _potWrap);
            Refresh();
            _onChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 앵콜 스핀 연출
        // ══════════════════════════════════════════════════════════════════════
        private IEnumerator EncoreRoutine(SpinOutcome spin, long potBefore)
        {
            SetBusy(true);
            var er = spin.encoreResult;
            var cells = spin.result.cells;
            try
            {
                SoundKit.Sfx("spin");
                _lastMsg = "";
                _lastMsgBad = false;
                SetMessage("", false);
                _bg.color = BoxBg;
                _bgOutline.effectColor = BoxLine;
                yield return RollTiles(cells);
                _lastCells = cells;
                if (er.bust) yield return BustFx(er, spin.result);
                else yield return UpFx(er, potBefore);
            }
            finally
            {
                // 연출 도중 예외/비활성화가 나도 게임 상태는 이미 반영돼 있다 — 입력 잠금만은 반드시 푼다.
                SetBusy(false);
            }
            Refresh();
            _onChanged?.Invoke();
        }

        // 타일이 심볼을 빠르게 바꾸다 왼쪽부터 차례로 착지(웹 encoreRoll).
        private IEnumerator RollTiles(IReadOnlyList<Cell> cells)
        {
            RebuildTiles(cells.Count);
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                t.rolling = true;
                t.border.effectColor = UiKit.Purple;
                t.rt.localScale = Vector3.one;
                t.rt.localRotation = Quaternion.identity;
            }
            RollTick();
            float tickT = 0f;
            for (int i = 0; i < _tiles.Count && i < cells.Count; i++)
            {
                float wait = i == 0 ? RollFirstWait : RollStepWait;
                float waited = 0f;
                while (waited < wait)
                {
                    waited += Time.deltaTime;
                    tickT += Time.deltaTime;
                    if (tickT >= RollTickInterval) { tickT = 0f; RollTick(); }
                    yield return null;
                }
                LandTile(_tiles[i], cells[i]);
            }
            // 같은 값심볼 2개↑ 칸 강조(해골 칸은 LandTile 에서 이미 붉게)
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < cells.Count; i++)
            {
                var sym = cells[i]?.sym;
                if (sym != null && ValueSymIds.Contains(sym.id)) counts[sym.id] = (counts.TryGetValue(sym.id, out var c) ? c : 0) + 1;
            }
            for (int i = 0; i < _tiles.Count && i < cells.Count; i++)
            {
                var sym = cells[i]?.sym;
                if (sym != null && counts.TryGetValue(sym.id, out var n) && n >= Formulas.ENCORE_MIN_SET)
                {
                    _tiles[i].border.effectColor = UiKit.Accent;
                    StartCoroutine(UiTween.ScaleRoutine(_tiles[i].rt, Vector3.one * 1.1f, Vector3.one, 0.25f, UiTween.Ease.OutBack));
                }
            }
            yield return new WaitForSeconds(0.22f);
        }

        private void RollTick()
        {
            var all = Symbols.All;
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (!_tiles[i].rolling) continue;
                SetTileSymbol(_tiles[i], all[UnityEngine.Random.Range(0, Symbols.LegacyCount)]);
            }
        }

        private void LandTile(Tile t, Cell cell)
        {
            t.rolling = false;
            var sym = cell?.sym;
            SetTileSymbol(t, sym);
            bool skull = sym != null && sym.id == "skull";
            t.border.effectColor = skull ? UiKit.Bad : UiKit.Bd2;
            StartCoroutine(UiTween.ScaleRoutine(t.rt, Vector3.one * 0.7f, Vector3.one, 0.26f, UiTween.Ease.OutBack));
            if (skull) StartCoroutine(WobbleRoutine(t.rt));
            SoundKit.Sfx("reel");
        }

        private IEnumerator WobbleRoutine(RectTransform rt)
        {
            float t = 0f;
            const float dur = 0.5f;
            while (t < dur && rt != null)
            {
                t += Time.deltaTime;
                float damp = 1f - Mathf.Clamp01(t / dur);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 7f * damp);
                yield return null;
            }
            if (rt != null) rt.localRotation = Quaternion.identity;
        }

        // 성공: 팟 카운트업 + ×배율 팝 + 상승 연출.
        private IEnumerator UpFx(EncoreResult er, long potBefore)
        {
            long potAfter = potBefore + er.gained;
            _potLabel.text = "팟";
            _potText.color = UiKit.Accent;
            StartCoroutine(UiTween.CountUpRoutine(potBefore, potAfter, PotCountUpDuration,
                v => { if (_potText != null) _potText.text = NumberFormat.Comma(v); }, UiTween.Ease.OutCubic));
            Pop(_potText.rectTransform);
            _mulText.text = "×" + NumberFormat.Fmt(er.mulApplied);
            Pop(_mulText.rectTransform);

            int kk = Mathf.Max(1, Mathf.RoundToInt((float)((er.mulApplied - 1.0) / Formulas.ENCORE_STEP)));
            SoundKit.Sfx("encoreup" + Mathf.Clamp(kk, 1, 6));
            SettingsSheet.SafeVibrate(22);
            FxKit.I?.Play(FxId.CoinSpark, _potWrap);
            FxKit.I?.Play(FxId.RisingLight, _potWrap);
            StartCoroutine(OutlineFlash(UiKit.Accent));

            string msg = $"성공! 팟 ×{NumberFormat.Fmt(er.mulApplied)} · +{NumberFormat.Comma(er.gained)}점";
            if (er.settled)
            {
                msg += $"\n남은 스핀이 없어 자동 정산 +{NumberFormat.Comma(er.settledPot)}점";
                _result = Mode.Settled;
                _settledPot = er.settledPot;
                SoundKit.Sfx("coin");
            }
            _lastMsg = msg;
            _lastMsgBad = false;
            SetMessage(msg, false);
            yield return new WaitForSeconds(0.8f);
        }

        // 실패(BUST): 팟이 반으로 깨져 떨어지고 조각이 튀며 박스가 흔들리고 진동.
        private IEnumerator BustFx(EncoreResult er, SpinResult res)
        {
            int skulls = res != null ? res.skulls : 0;
            string why = skulls >= Formulas.ENCORE_BUST_SKULLS ? $"해골 {skulls}개" : "페어가 하나도 없어요";
            _result = Mode.Bust;
            _lostPot = er.lostPot;
            string msg = $"BUST! {why} — 팟 {NumberFormat.Comma(er.lostPot)}점이 소멸했어요";
            _lastMsg = msg;
            _lastMsgBad = true;

            SoundKit.Sfx("bust");
            StartCoroutine(FunHaptics.Routine(new[] { 60, 40, 140, 40, 60 }));
            _bg.color = BustBg;
            _bgOutline.effectColor = UiKit.Bad;
            SetMessage(msg, true);
            StartCoroutine(UiTween.ShakeRoutine(_body, 12f, 0.6f));
            yield return SplitPotRoutine(er.lostPot);
            yield return new WaitForSeconds(0.25f);
        }

        // ── BUST 조각 연출 ──────────────────────────────────────────────────────────
        private readonly List<GameObject> _fxObjects = new List<GameObject>();

        private RectTransform MakeHalf(string name, string text, bool leftHalf)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_potWrap, false);
            rt.anchorMin = new Vector2(leftHalf ? 0f : 0.5f, 0f);
            rt.anchorMax = new Vector2(leftHalf ? 0.5f : 1f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            // 글자는 전체 폭으로 깔고(부모 폭의 2배 앵커) 부모 마스크(RectMask2D)가 왼쪽/오른쪽 절반만 보여 준다.
            var label = UiKit.Text(rt, text, PotFontSize, UiKit.Accent, TextAnchor.MiddleCenter, true);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(leftHalf ? 0f : -1f, 0f);
            lrt.anchorMax = new Vector2(leftHalf ? 2f : 1f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            _fxObjects.Add(go);
            return rt;
        }

        private IEnumerator SplitPotRoutine(long lost)
        {
            if (_potWrap == null || _potText == null) yield break;
            string txt = NumberFormat.Comma(lost);
            _potText.text = txt;
            _potText.enabled = false;
            var left = MakeHalf("HalfL", txt, true);
            var right = MakeHalf("HalfR", txt, false);

            // 조각 12개 — 금색/붉은색 작은 사각이 사방으로 튀며 중력으로 떨어진다.
            const int shardCount = 12;
            var shards = new RectTransform[shardCount];
            var shardImgs = new Image[shardCount];
            var vel = new Vector2[shardCount];
            var spin = new float[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                var go = new GameObject("Shard_" + i, typeof(RectTransform), typeof(Image));
                var srt = (RectTransform)go.transform;
                srt.SetParent(_potWrap, false);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = new Vector2(10f, 14f);
                srt.anchoredPosition = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = (i % 2 == 0) ? UiKit.Accent : UiKit.Bad;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float spd = UnityEngine.Random.Range(140f, 380f);
                vel[i] = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd + 60f);
                spin[i] = UnityEngine.Random.Range(-540f, 540f);
                shards[i] = srt;
                shardImgs[i] = img;
                _fxObjects.Add(go);
            }

            float t = 0f;
            const float dur = 0.9f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float fall = k * k;   // 가속 낙하
                if (left != null)
                {
                    left.anchoredPosition = new Vector2(-34f * k, -78f * fall);
                    left.localRotation = Quaternion.Euler(0f, 0f, 14f * k);
                }
                if (right != null)
                {
                    right.anchoredPosition = new Vector2(34f * k, -86f * fall);
                    right.localRotation = Quaternion.Euler(0f, 0f, -16f * k);
                }
                float fade = 1f - Mathf.Clamp01((k - 0.45f) / 0.55f);
                SetHalfAlpha(left, fade);
                SetHalfAlpha(right, fade);
                for (int i = 0; i < shardCount; i++)
                {
                    if (shards[i] == null) continue;
                    vel[i].y -= 900f * Time.deltaTime;
                    shards[i].anchoredPosition += vel[i] * Time.deltaTime;
                    shards[i].localRotation = Quaternion.Euler(0f, 0f, spin[i] * t);
                    var c = shardImgs[i].color;
                    c.a = 1f - k;
                    shardImgs[i].color = c;
                }
                yield return null;
            }
            ClearHalvesAndShards();
            if (_potText != null)
            {
                _potText.text = "0";
                _potText.color = UiKit.TextSecondary;
                _potText.enabled = true;
            }
        }

        private static void SetHalfAlpha(RectTransform half, float a)
        {
            if (half == null) return;
            var label = half.GetComponentInChildren<Text>();
            if (label == null) return;
            var c = label.color;
            c.a = a;
            label.color = c;
        }

        private void ClearHalvesAndShards()
        {
            for (int i = 0; i < _fxObjects.Count; i++)
                if (_fxObjects[i] != null) Destroy(_fxObjects[i]);
            _fxObjects.Clear();
        }

        // ── 공용 연출 ───────────────────────────────────────────────────────────────
        private void Pop(RectTransform rt)
        {
            if (rt == null) return;
            StartCoroutine(UiTween.ScaleRoutine(rt, Vector3.one * 1.25f, Vector3.one, 0.45f, UiTween.Ease.OutBack));
        }

        private IEnumerator OutlineFlash(Color flash)
        {
            if (_bgOutline == null) yield break;
            Color baseColor = BoxLine;
            yield return UiTween.FloatRoutine(0f, 1f, 0.2f, k => { if (_bgOutline != null) _bgOutline.effectColor = Color.Lerp(baseColor, flash, k); }, UiTween.Ease.OutQuad);
            yield return UiTween.FloatRoutine(1f, 0f, 0.5f, k => { if (_bgOutline != null) _bgOutline.effectColor = Color.Lerp(baseColor, flash, k); }, UiTween.Ease.OutQuad);
        }

        private void OnDisable()
        {
            // 보드가 닫히면(노드 선택·화면 이탈) 코루틴이 멈춘다 — 잠금이 남지 않게 풀고 조각 오브젝트를 정리한다.
            if (_busy)
            {
                _busy = false;
                _onBusy?.Invoke(false);
            }
            ClearHalvesAndShards();
            if (_potText != null) _potText.enabled = true;
        }
    }
}
