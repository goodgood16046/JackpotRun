using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>
    /// 게임 화면 — 상단 HUD(스테이지·EXP 진행바·남은 스핀·코인·점수·저주) + 릴 + 스핀 노트 피드 +
    /// 하단 조작부(스핀/특수모드/가방/장치) + 페이즈별 오버레이(RunPanels: NodeSelect/PerkOffer/Shop/
    /// PostSpin/GameOver). ENGINE_PORT_DESIGN.md S6. 리빌드는 전부 이벤트 시(Init/Send 이후)에만 —
    /// Update() 루프 없음.
    /// </summary>
    public static class RunScreen
    {
        public static void Build(RectTransform parent, JackpotRunApp app)
        {
            new Ctx(parent, app).Init();
        }

        private class Ctx
        {
            private const int NotesFeedCap = 6;

            private readonly RectTransform _root;
            private readonly JackpotRunApp _app;
            private readonly GameSession _session;

            private RectTransform _overlayLayer;
            private RectTransform _toastLayer;
            private GameObject _activeToastGo;

            // HUD
            private Text _stageText;
            private Text _cursesText;
            private RectTransform _expBarFill;
            private Text _expBarText;
            private Text _spinsText;
            private Text _coinsText;
            private Text _scoreText;

            // 릴 + 결과 라인
            private RectTransform _reelRow;
            private Text _resultLineText;

            // 스핀 노트 피드(최근 6줄)
            private Text _notesText;
            private readonly List<string> _notesFeed = new List<string>();

            // 하단 조작부
            private RectTransform _deviceRow;
            private Text _bagBtnText;

            // 이벤트로만 갱신되는 페이즈 패널용 캐시(직전 스핀/오퍼/실패 정보 — RunState엔 없는 1회성 필드).
            private SpinOutcome _lastSpin;
            private RunEvent _lastOfferEvent;
            private FailureOutcome _lastFailure;

            public Ctx(RectTransform parent, JackpotRunApp app)
            {
                _root = parent;
                _app = app;
                _session = app.Session;
            }

            public void Init()
            {
                var content = UiFactory.VGroup(_root, 0, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.Fill(content);

                BuildHud(content);
                BuildReel(content);
                BuildResultLine(content);
                BuildNotesFeed(content);
                BuildControls(content);

                // 페이즈 오버레이 — 빈 컨테이너(Image 없음, 라이캐스트 차단 안 함). 각 RunPanels.Build*가
                // 스크림(불투명 Image)을 직접 넣을 때만 하단 조작부 클릭을 막는다(자연스러운 모달 처리).
                var overlayGo = new GameObject("Overlay", typeof(RectTransform));
                _overlayLayer = (RectTransform)overlayGo.transform;
                _overlayLayer.SetParent(_root, false);
                UiFactory.Fill(_overlayLayer);

                var toastGo = new GameObject("ToastLayer", typeof(RectTransform));
                _toastLayer = (RectTransform)toastGo.transform;
                _toastLayer.SetParent(_root, false);
                UiFactory.Fill(_toastLayer);

                RefreshAll(_session.Controller.LaunchEvents);
            }

            // ── HUD ──────────────────────────────────────────────
            private void BuildHud(RectTransform col)
            {
                var hud = UiFactory.Panel(col, "Hud", UiFactory.Hex("#151A2E"));
                UiFactory.SizeHint(hud, preferredHeight: 200);
                var hudCol = UiFactory.VGroup(hud, 8, new RectOffset(24, 24, 16, 12), true, true);
                UiFactory.Fill(hudCol);

                var topRow = UiFactory.HGroup(hudCol, 12, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(topRow, preferredHeight: 46);
                _stageText = UiFactory.Text(topRow, "", 28, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(_stageText, flexibleWidth: 1);
                _cursesText = UiFactory.Text(topRow, "", 22, UiFactory.Hex("#FF6B6B"), TextAnchor.MiddleRight, true);
                UiFactory.SizeHint(_cursesText, preferredWidth: 200);

                var barBg = UiFactory.Panel(hudCol, "ExpBarBg", UiFactory.Hex("#2A3048"));
                UiFactory.SizeHint(barBg, preferredHeight: 36);
                _expBarFill = UiFactory.Panel(barBg, "ExpBarFill", UiFactory.Hex("#34D3C0"));
                UiFactory.SetAnchors(_expBarFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                _expBarText = UiFactory.Text(barBg, "", 20, UiFactory.Hex("#0B0E1A"), TextAnchor.MiddleCenter, true);
                UiFactory.Fill(_expBarText.rectTransform);

                var statsRow = UiFactory.HGroup(hudCol, 16, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(statsRow, preferredHeight: 42);
                _spinsText = UiFactory.Text(statsRow, "", 22, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleLeft);
                UiFactory.SizeHint(_spinsText, flexibleWidth: 1);
                _coinsText = UiFactory.Text(statsRow, "", 22, UiFactory.Hex("#FFD23F"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(_coinsText, flexibleWidth: 1);
                _scoreText = UiFactory.Text(statsRow, "", 22, UiFactory.Hex("#5B8CFF"), TextAnchor.MiddleRight, true);
                UiFactory.SizeHint(_scoreText, flexibleWidth: 1);
            }

            private void RefreshHud()
            {
                var run = _session.State;
                var boss = Bosses.For(run.Stage);
                _stageText.text = boss != null ? $"스테이지 {run.Stage} · {boss.emoji}{boss.name}" : $"스테이지 {run.Stage}";
                _cursesText.text = $"🌑저주 {run.Curses.Count}";

                var (quota, spins) = _session.PreviewQuotaSpins();
                float pct = quota > 0 ? Mathf.Clamp01((float)((double)run.StageExp / quota)) : 0f;
                _expBarFill.anchorMax = new Vector2(pct, 1f);
                _expBarText.text = $"{NumberFormat.Comma(run.StageExp)} / {NumberFormat.Comma(quota)}";

                int spinsLeft = Mathf.Max(spins - run.SpinIndex, 0);
                _spinsText.text = $"남은 스핀 {spinsLeft}";
                _coinsText.text = $"🪙 {NumberFormat.Comma(run.Coins)}";
                _scoreText.text = $"⭐ {NumberFormat.Comma(run.Score)}";
            }

            // ── 릴 ───────────────────────────────────────────────
            private void BuildReel(RectTransform col)
            {
                var section = UiFactory.Panel(col, "ReelSection", new Color(0, 0, 0, 0));
                UiFactory.SizeHint(section, preferredHeight: 180);
                _reelRow = UiFactory.HGroup(section, 12, new RectOffset(24, 24, 10, 10), true, true);
                UiFactory.Fill(_reelRow);
            }

            private void BuildResultLine(RectTransform col)
            {
                _resultLineText = UiFactory.Text(col, "", 22, UiFactory.Hex("#FFD23F"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(_resultLineText, preferredHeight: 40);
            }

            private void RefreshReel()
            {
                for (int i = _reelRow.childCount - 1; i >= 0; i--)
                    Object.Destroy(_reelRow.GetChild(i).gameObject);

                if (_lastSpin?.result == null)
                {
                    _resultLineText.text = "";
                    return;
                }

                var res = _lastSpin.result;
                for (int i = 0; i < res.cells.Count; i++) BuildReelCell(res.cells[i]);
                _resultLineText.text = $"+{NumberFormat.Comma(_lastSpin.gained)}EXP · +{NumberFormat.Comma(res.score)}점 · +{res.coins}🪙";
            }

            private void BuildReelCell(Cell cell)
            {
                var cellRt = UiFactory.Panel(_reelRow, "Cell", UiFactory.Hex("#1B2138"));
                UiFactory.SizeHint(cellRt, flexibleWidth: 1, preferredHeight: 150);
                var emojiText = UiFactory.Text(cellRt, cell.sym.emoji, 54, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Fill(emojiText.rectTransform);
                if (!string.IsNullOrEmpty(cell.tag))
                {
                    var tagText = UiFactory.Text(cellRt, cell.tag, 18, UiFactory.Hex("#FFD23F"), TextAnchor.UpperRight);
                    UiFactory.SetAnchors(tagText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-52, -30), new Vector2(-6, -4));
                }
            }

            // ── 스핀 노트 피드 ──────────────────────────────────────
            private void BuildNotesFeed(RectTransform col)
            {
                var panel = UiFactory.Panel(col, "NotesFeed", UiFactory.Hex("#11162A"));
                UiFactory.SizeHint(panel, flexibleHeight: 1, minHeight: 150);
                var inner = UiFactory.VGroup(panel, 4, new RectOffset(20, 20, 10, 10), true, true);
                UiFactory.Fill(inner);
                _notesText = UiFactory.Text(inner, "", 19, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(_notesText, flexibleHeight: 1);
            }

            private void AppendNote(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                _notesFeed.Add(text);
                while (_notesFeed.Count > NotesFeedCap) _notesFeed.RemoveAt(0);
                _notesText.text = string.Join("\n", _notesFeed);
            }

            private void AppendSpinNotes(SpinOutcome spin)
            {
                if (spin?.notes == null) return;
                for (int i = 0; i < spin.notes.Count; i++) AppendNote(spin.notes[i]);
            }

            // ── 하단 조작부 ─────────────────────────────────────────
            private void BuildControls(RectTransform col)
            {
                var controls = UiFactory.VGroup(col, 10, new RectOffset(20, 20, 10, 20), true, true);
                UiFactory.SizeHint(controls, preferredHeight: 320);

                // 특수모드(사용가능 조건은 엔진 거부에 맡기고 토스트로 안내 — 여기서 사전 비활성화하지 않음).
                var modeRow = UiFactory.HGroup(controls, 8, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(modeRow, preferredHeight: 70);
                AddModeButton(modeRow, $"🎯집중({Formulas.CMD_COST_FOCUS})", SpinMode.Focus);
                AddModeButton(modeRow, $"🎲올인({Formulas.CMD_COST_ALLIN})", SpinMode.Allin);
                AddModeButton(modeRow, $"🙏기도({Formulas.CMD_COST_PRAY})", SpinMode.Pray);
                AddModeButton(modeRow, $"⏰막판({Formulas.CMD_COST_LAST})", SpinMode.Last);

                var spinBtn = UiFactory.Button(controls, "🎰 스핀", new Vector2(0, 108),
                    UiFactory.Hex("#FFD23F"), UiFactory.Hex("#0B0E1A"), () => Send(new Spin(SpinMode.N)));
                UiFactory.SizeHint(spinBtn, preferredHeight: 108);

                var toolRow = UiFactory.HGroup(controls, 10, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(toolRow, preferredHeight: 80);

                var bagBtn = UiFactory.Button(toolRow, "🎒", new Vector2(0, 80),
                    UiFactory.Hex("#2A3048"), UiFactory.Hex("#E8EAF2"),
                    () => RunPanels.ShowBag(_overlayLayer, _session.State, itemId => Send(new UseItem(itemId))));
                UiFactory.SizeHint(bagBtn, preferredWidth: 170, preferredHeight: 80);
                _bagBtnText = bagBtn.GetComponentInChildren<Text>();

                _deviceRow = UiFactory.HGroup(toolRow, 10, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(_deviceRow, flexibleWidth: 1, preferredHeight: 80);
            }

            private void AddModeButton(RectTransform row, string label, SpinMode mode)
            {
                var btn = UiFactory.Button(row, label, new Vector2(0, 70), UiFactory.Hex("#1B2138"), UiFactory.Hex("#E8EAF2"),
                    () => Send(new Spin(mode)));
                UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 70);
            }

            private void RefreshBagLabel()
            {
                if (_bagBtnText != null) _bagBtnText.text = $"🎒{_session.State.Items.Count}/{ItemUse.ItemSlots}";
            }

            // 장착 장치는 dev_bell 발동 등으로 런 중 파괴될 수 있어(run.Device=="") 매 이벤트마다 리빌드.
            private void RefreshDeviceRow()
            {
                for (int i = _deviceRow.childCount - 1; i >= 0; i--)
                    Object.Destroy(_deviceRow.GetChild(i).gameObject);

                var run = _session.State;
                AddDeviceButtonIfAny(run.Device, isSecondary: false);
                AddDeviceButtonIfAny(run.Device2, isSecondary: true);

                if (run.CharId == "gambler")
                {
                    var btn = UiFactory.Button(_deviceRow, "🎲재굴림", new Vector2(0, 80),
                        UiFactory.Hex("#34D3C0"), UiFactory.Hex("#0B0E1A"), () => Send(new GamblerReroll()));
                    UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 80);
                }
            }

            private void AddDeviceButtonIfAny(string deviceId, bool isSecondary)
            {
                if (string.IsNullOrEmpty(deviceId)) return;
                var dev = Devices.ById(deviceId);
                if (dev == null) return;
                string suffix = isSecondary ? "(보조)" : "";

                if (dev.kind == "PASSIVE")
                {
                    // 정보 표시만 — 명령 없음(DeviceActions.Handle의 default 분기가 DEVICE_NOT_SUPPORTED로 거부).
                    var chip = UiFactory.Panel(_deviceRow, "PassiveChip", UiFactory.Hex("#2A3048"));
                    UiFactory.SizeHint(chip, flexibleWidth: 1, preferredHeight: 80);
                    var t = UiFactory.Text(chip, $"{dev.emoji}{dev.name}{suffix}", 17, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                    UiFactory.Fill(t.rectTransform);
                    return;
                }
                // dev_holdfile/dev_retake는 증강·유물 오퍼 패널 전용 명령(RunController.cs 계약) — 메인 줄 제외.
                if (dev.id == "dev_holdfile" || dev.id == "dev_retake") return;

                var btn = UiFactory.Button(_deviceRow, $"{dev.emoji}{dev.name}{suffix}", new Vector2(0, 80),
                    UiFactory.Hex("#5B8CFF"), UiFactory.Hex("#0B0E1A"), () => OnDeviceButtonClick(dev));
                UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 80);
            }

            private void OnDeviceButtonClick(DeviceDef dev)
            {
                if (dev.kind == "MANIP") OpenManipPicker(dev);
                else Send(new DeviceCmd(dev.id));
            }

            private void OpenManipPicker(DeviceDef dev)
            {
                RunPanels.ShowManipPicker(_overlayLayer, _session.State, dev,
                    (devId, arg) => Send(new DeviceCmd(devId, arg)));
            }

            // ── 액션 전송 + 전체 갱신 ─────────────────────────────────
            private void Send(RunAction action)
            {
                var events = _session.Do(action);
                RefreshAll(events);
            }

            private void RefreshAll(IReadOnlyList<RunEvent> events)
            {
                HandleEvents(events);
                RefreshHud();
                RefreshReel();
                RefreshBagLabel();
                RefreshDeviceRow();
                RefreshPhasePanel();
            }

            private void RefreshPhasePanel()
            {
                for (int i = _overlayLayer.childCount - 1; i >= 0; i--)
                    Object.Destroy(_overlayLayer.GetChild(i).gameObject);

                var run = _session.State;
                switch (run.Phase)
                {
                    case RunPhase.NodeSelect:
                        RunPanels.BuildNodeSelect(_overlayLayer, run, idx => Send(new ChooseNode(idx)));
                        break;
                    case RunPhase.EventAugment:
                    case RunPhase.EventRelic:
                        RunPanels.BuildPerkOffer(_overlayLayer, run, _lastOfferEvent,
                            idx => Send(new PickOffer(idx)),
                            idx => Send(new HoldAugment(idx)),
                            () => Send(new Retake()));
                        break;
                    case RunPhase.EventShop:
                        RunPanels.BuildShop(_overlayLayer, run,
                            idx => Send(new BuyOffer(idx)),
                            () => Send(new RerollShop()),
                            () => Send(new LeaveShop()));
                        break;
                    case RunPhase.PostSpin:
                        RunPanels.BuildPostSpin(_overlayLayer, run, _lastFailure,
                            dev => OpenManipPicker(dev),
                            () => Send(new GamblerReroll()),
                            () => Send(new Continue()));
                        break;
                    case RunPhase.GameOver:
                        RunPanels.BuildGameOver(_overlayLayer, _session, _lastFailure, () => _app.ShowMenu());
                        break;
                    default:
                        break; // Spin: 오버레이 없음(빈 컨테이너라 라이캐스트를 막지 않음) — 하단 조작부 그대로 노출.
                }
            }

            // ── RunEvent → 토스트/노트 피드 번역(카톡 텍스트 조립 금지 원칙 — UI가 구조화 이벤트를 연출) ──
            private void HandleEvents(IReadOnlyList<RunEvent> events)
            {
                for (int idx = 0; idx < events.Count; idx++)
                {
                    var e = events[idx];
                    switch (e.type)
                    {
                        case "REJECTED":
                            ShowToast(RejectReasonText(e.reason));
                            break;

                        case "SPIN_RESULT":
                            _lastSpin = e.spin;
                            AppendSpinNotes(e.spin);
                            break;

                        case "REVIVED":
                            _lastSpin = e.spin;
                            AppendSpinNotes(e.spin);
                            AppendNote(e.failure != null && e.failure.kind == "FATE_BELL_REVIVE"
                                ? "🔔 운명의종 발동! 스핀 +1" : "📜 보험증서 발동! 스핀 +2");
                            break;

                        case "POST_SPIN":
                            _lastSpin = e.spin;
                            AppendSpinNotes(e.spin);
                            _lastFailure = e.failure;
                            break;

                        case "STAGE_CLEARED":
                            // ⚠️ UI 계약: 즉시클리어 아이템(grad_ring/gold_grad_bell) 경로는 spin.result가
                            // null이다(RunController.cs 헤더 주의 1번) — 릴 갱신은 건너뛰고 요약만 표시.
                            if (e.spin != null && e.spin.result != null) { _lastSpin = e.spin; AppendSpinNotes(e.spin); }
                            if (e.clear != null) AppendNote(ClearSummaryText(e.clear));
                            break;

                        case "GAME_OVER":
                            if (e.spin != null && e.spin.result != null) { _lastSpin = e.spin; AppendSpinNotes(e.spin); }
                            _lastFailure = e.failure;
                            break;

                        case "DEVICE_MANIP_RESULT":
                            _lastSpin = e.spin;
                            AppendSpinNotes(e.spin);
                            break;

                        case "ITEM_USED":
                            AppendNote($"🎒 {RunPanels.ItemLabel(e.itemId)} 사용");
                            if (e.spin != null && e.spin.result != null) { _lastSpin = e.spin; AppendSpinNotes(e.spin); }
                            break;

                        case "DEVICE_ARMED":
                            AppendNote($"🔧 {RunPanels.DeviceLabel(e.deviceId)} 예약{(e.secondary ? "(보조)" : "")}");
                            break;

                        case "DEVICE_PEEK":
                            AppendNote($"🔮 다음 스핀 확정: {PeekCellsText(e.peekCells)}");
                            break;

                        case "PERK_GRANTED":
                            AppendNote($"✅ 획득: {RunPanels.PerkLabel(e.perkId)}");
                            break;

                        case "PERK_HELD":
                            AppendNote($"🗂️ 보류: {RunPanels.PerkLabel(e.perkId)}");
                            break;

                        case "PERK_OFFER":
                            _lastOfferEvent = e;
                            break;

                        case "RETAKE_EMPTY":
                            // _lastOfferEvent는 갱신하지 않는다 — 기존 오퍼(run.PerkOfferIds)가 그대로 유지되고
                            // 이 이벤트엔 배지 필드(offerTier 등)가 없어 덮어쓰면 정보가 유실된다.
                            AppendNote("🔁 재추첨 — 후보 없음(코인 환불)");
                            break;

                        case "NODE_RESOLVED":
                            AppendNote(NodeResolvedText(e));
                            break;

                        case "SHOP_PURCHASED":
                            AppendNote($"🛒 구매: {ShopEntryLabel(e.shopBought)}");
                            break;

                        case "SHOP_REROLLED":
                            AppendNote("🔁 상점 리롤");
                            break;

                        case "SHOP_LEFT":
                            AppendNote("🚪 상점을 나갑니다");
                            break;

                        case "RUN_STARTED":
                            AppendNote($"🎬 런 시작 · 🪙{NumberFormat.Comma(e.coinsDelta)}");
                            break;

                        default:
                            break;
                    }
                }
            }

            private static string ClearSummaryText(ClearOutcome c)
            {
                string debtNote = c.inDebt ? " (빚 상환 중·무보상)" : "";
                return $"🎉 스테이지 {c.clearedStage} 클리어 · {c.grade} · 점수+{NumberFormat.Comma(c.gainedScore)} · " +
                       $"코인+{NumberFormat.Comma(c.clearCoin)}{debtNote}{(c.nextNodeForcedPrism ? " · 🌈다음 프리즘 확정" : "")}";
            }

            private static string PeekCellsText(IReadOnlyList<string> ids)
            {
                if (ids == null || ids.Count == 0) return "";
                string s = "";
                for (int i = 0; i < ids.Count; i++)
                {
                    var info = Symbols.ById(ids[i]);
                    s += info != null ? info.emoji : "❔";
                }
                return s;
            }

            private static string NodeResolvedText(RunEvent e)
            {
                switch (e.node)
                {
                    case NodeKind.Rest:
                        return $"☕ 휴식 · 🪙+{NumberFormat.Comma(e.coinsDelta)}";
                    case NodeKind.Gamble:
                        return e.gambleWon
                            ? $"🎲 도박 성공! 🪙+{NumberFormat.Comma(e.coinsDelta)}"
                            : $"🎲 도박 실패… 🪙{NumberFormat.Comma(e.coinsDelta)}";
                    case NodeKind.Curse:
                        return $"🌑 저주 획득: {RunPanels.PerkLabel(e.curseGrantedId)} · 🪙+{NumberFormat.Comma(e.coinsDelta)}";
                    case NodeKind.Risk:
                        return $"⚠️ 위험! {RunPanels.PerkLabel(e.augmentGrantedId)} + 저주 {RunPanels.PerkLabel(e.curseGrantedId)}";
                    case NodeKind.Event:
                        return EventTableText(e);
                    default:
                        return $"노드 결과: {e.node}";
                }
            }

            private static string EventTableText(RunEvent e)
            {
                var parts = new List<string> { "❓ 이벤트" };
                if (e.coinsDelta != 0) parts.Add($"🪙{(e.coinsDelta > 0 ? "+" : "")}{NumberFormat.Comma(e.coinsDelta)}");
                if (e.scoreDelta != 0) parts.Add($"⭐{(e.scoreDelta > 0 ? "+" : "")}{NumberFormat.Comma(e.scoreDelta)}");
                if (e.bonusSpinsDelta != 0) parts.Add($"스핀+{e.bonusSpinsDelta}");
                if (!string.IsNullOrEmpty(e.itemGrantedId)) parts.Add($"🎁{RunPanels.ItemLabel(e.itemGrantedId)}");
                if (!string.IsNullOrEmpty(e.relicGrantedId)) parts.Add($"🛡️{RunPanels.PerkLabel(e.relicGrantedId)}");
                if (!string.IsNullOrEmpty(e.augmentGrantedId)) parts.Add($"✨{RunPanels.PerkLabel(e.augmentGrantedId)}");
                if (!string.IsNullOrEmpty(e.curseRemovedId)) parts.Add($"정화: {RunPanels.PerkLabel(e.curseRemovedId)} 제거");
                return string.Join(" · ", parts);
            }

            private static string ShopEntryLabel(ShopEntry entry)
            {
                if (entry == null) return "";
                return entry.kind == 'A' || entry.kind == 'R' ? RunPanels.PerkLabel(entry.id) : RunPanels.ItemLabel(entry.id);
            }

            // ── 토스트(REJECTED 전용, 2초 자동 소멸) ────────────────────
            private void ShowToast(string message)
            {
                if (_activeToastGo != null) Object.Destroy(_activeToastGo);

                var panel = UiFactory.Panel(_toastLayer, "Toast", UiFactory.Hex("#2A3048"));
                panel.GetComponent<Image>().raycastTarget = false;
                panel.anchorMin = new Vector2(0.5f, 1f);
                panel.anchorMax = new Vector2(0.5f, 1f);
                panel.pivot = new Vector2(0.5f, 1f);
                panel.sizeDelta = new Vector2(940f, 90f);
                panel.anchoredPosition = new Vector2(0f, -24f);

                var txt = UiFactory.Text(panel, message, 24, UiFactory.Hex("#FFD23F"), TextAnchor.MiddleCenter, true);
                UiFactory.Fill(txt.rectTransform);

                _activeToastGo = panel.gameObject;
                Object.Destroy(_activeToastGo, 2f);
            }

            private static readonly Dictionary<string, string> RejectReasons = new Dictionary<string, string>
            {
                { "PHASE_NOT_SPIN", "지금은 스핀할 수 없습니다" },
                { "LAST_NOT_FINAL_SPIN", "막판은 마지막 스핀에서만 가능합니다" },
                { "MODE_ALREADY_USED", "이번 스테이지에 이미 사용한 모드입니다" },
                { "INSUFFICIENT_COINS", "코인이 부족합니다" },
                { "PHASE_NOT_NODE_SELECT", "지금은 노드를 선택할 수 없습니다" },
                { "INVALID_INDEX", "잘못된 선택입니다" },
                { "PHASE_NOT_PERK_OFFER", "지금은 선택할 수 없습니다" },
                { "PHASE_NOT_EVENT_AUGMENT", "증강 선택 화면이 아닙니다" },
                { "DEVICE_NOT_EQUIPPED", "해당 장치가 장착되어 있지 않습니다" },
                { "ALREADY_HOLDING", "이미 보류 중인 증강이 있습니다" },
                { "ALREADY_USED", "이미 사용했습니다" },
                { "PHASE_NOT_SHOP", "지금은 상점을 이용할 수 없습니다" },
                { "BAG_FULL", "가방이 가득 찼습니다" },
                { "ITEM_NOT_IN_BAG", "가방에 없는 아이템입니다" },
                { "ITEM_UNKNOWN", "알 수 없는 아이템입니다" },
                { "ICLEAR_ALREADY_USED", "이번 스테이지에 이미 사용한 즉시클리어 아이템입니다" },
                { "NO_LAST_SPIN", "직전 스핀 결과가 없습니다" },
                { "DEVICE_UNKNOWN", "알 수 없는 장치입니다" },
                { "POST_SPIN_ONLY_MANIP_OR_GAMBLER", "지금은 만회 장치만 사용할 수 있습니다" },
                { "DEVICE_ALREADY_USED", "이번 스테이지에 이미 사용한 장치입니다" },
                { "USE_HOLD_AUGMENT_ACTION", "보류는 증강 선택 화면에서 하세요" },
                { "USE_RETAKE_ACTION", "재추첨은 증강·유물 선택 화면에서 하세요" },
                { "DEVICE_NOT_SUPPORTED", "이 장치는 직접 명령할 수 없습니다" },
                { "ARG_REQUIRED", "칸 번호를 선택해야 합니다" },
                { "LAST_CELLS_UNAVAILABLE", "직전 스핀 결과를 사용할 수 없습니다" },
                { "DEV_BELL_DEFICIT_TOO_HIGH", "부족 EXP가 너무 많아 발동할 수 없습니다" },
                { "PHASE_NOT_POST_SPIN", "지금은 포기할 수 없습니다" },
                { "PHASE_INVALID", "지금은 사용할 수 없습니다" },
                { "NOT_GAMBLER", "도박꾼 전용입니다" },
                { "UNKNOWN_ACTION", "처리할 수 없는 요청입니다" },
            };

            private static string RejectReasonText(string reason)
            {
                if (string.IsNullOrEmpty(reason)) return "⚠️ 처리할 수 없습니다";
                return RejectReasons.TryGetValue(reason, out var text) ? $"⚠️ {text}" : $"⚠️ {reason}";
            }
        }
    }
}
