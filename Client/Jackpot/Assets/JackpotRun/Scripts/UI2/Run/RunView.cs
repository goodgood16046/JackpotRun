using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 런 화면 오케스트레이터 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/RunView.cs: "HUD·릴·노트·
    // 버튼열 오케스트레이션(RunEvent 스트림 소비)". GameSession.Do(...)가 돌려주는 RunEvent 배치를
    // (1) 노트/토스트/캐시로 북마킹 → (2) 스핀이 있었다면 ReelView 연출을 재생 → (3) HUD/가방/장치열/
    // 페이즈 패널을 갱신하는 순서로 처리한다("정지 후 획득 라인 표시" 등 연출 순서 지시를 만족).
    // 이관 원본: Scripts/UI/RunScreen.cs(레이아웃은 버리고 HandleEvents/RefreshAll 로직만 그대로 이식).
    public sealed class RunView : MonoBehaviour
    {
        private static readonly SpinMode[] ModeOrder = { SpinMode.Focus, SpinMode.Allin, SpinMode.Pray, SpinMode.Last };

        [SerializeField] private AppRoot appRoot;
        [SerializeField] private HudView hudView;
        [SerializeField] private ReelView reelView;
        [SerializeField] private Text resultLineText; // 릴-노트 사이 "스테이지 정보 영역" 획득 요약(Fable 지시)
        [SerializeField] private NotesFeed notesFeed;
        [SerializeField] private CanvasGroup controlsGroup; // 연출 중 조작부 잠금(스팸 클릭 방지)

        [Header("모드 4버튼 — 순서 고정: 집중/올인/기도/막판 (ModeOrder)")]
        [SerializeField] private Button[] modeButtons = Array.Empty<Button>();
        [SerializeField] private Button spinButton;
        [SerializeField] private Button bagButton;
        [SerializeField] private Text bagButtonLabel;
        [SerializeField] private RectTransform deviceRow;
        [SerializeField] private RectTransform deviceButtonTemplate; // 자식 경로 계약: Label(Text)

        [Header("페이즈 패널 / 팝업")]
        [SerializeField] private NodePanel nodePanel;
        [SerializeField] private PerkOfferPanel perkOfferPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private PostSpinPanel postSpinPanel;
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private BagPopup bagPopup;
        [SerializeField] private ManipPickPopup manipPickPopup;

        private GameSession _session;
        private bool _busy;
        private bool _wired;

        // 이벤트로만 갱신되는 페이즈 패널용 캐시(직전 클리어/오퍼/실패 정보 — RunState엔 없는 1회성 필드,
        // 이관 원본 RunScreen.Ctx의 _lastSpin/_lastOfferEvent/_lastFailure와 동일 역할).
        private ClearOutcome _lastClear;
        private RunEvent _lastOfferEvent;
        private FailureOutcome _lastFailure;

        private void Awake()
        {
            if (deviceButtonTemplate != null) deviceButtonTemplate.gameObject.SetActive(false);
            WireOnce();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < modeButtons.Length && i < ModeOrder.Length; i++)
            {
                var mode = ModeOrder[i];
                modeButtons[i].onClick.AddListener(() => Send(new Spin(mode)));
            }
            if (spinButton != null) spinButton.onClick.AddListener(() => Send(new Spin(SpinMode.N)));
            if (bagButton != null)
                bagButton.onClick.AddListener(() => bagPopup?.Show(_session.State, itemId => Send(new UseItem(itemId))));
        }

        private void OnEnable()
        {
            _session = appRoot != null ? appRoot.Session : null;
            if (_session == null) return; // 초기 씬 로드 중 화면이 잠깐 활성화되는 경우 등 방어

            _busy = false;
            _lastClear = null;
            _lastOfferEvent = null;
            _lastFailure = null;

            notesFeed?.Clear();
            reelView?.Clear();
            hudView?.ResetForNewRun();
            if (resultLineText != null) resultLineText.text = "";
            HidePanels();
            SetControlsInteractable(true);

            StartCoroutine(PlayRoutine(_session.Controller.LaunchEvents));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _busy = false;
            // 페이즈 패널/팝업은 전역 OverlayLayer 산하라(RunScreen의 자식이 아님) SetActive(false)가
            // 저절로 전파되지 않는다 — 화면을 떠날 때(EndRun→ShowMenu 등) 명시적으로 닫아야 다음 화면
            // 위에 유령처럼 남아있는 것을 막는다.
            HidePanels();
        }

        private void HidePanels()
        {
            nodePanel?.Hide();
            perkOfferPanel?.Hide();
            shopPanel?.Hide();
            postSpinPanel?.Hide();
            gameOverPanel?.Hide();
            bagPopup?.Hide();
            manipPickPopup?.Hide();
        }

        // ── 액션 전송 + 전체 갱신 ────────────────────────────────────────────────────────
        private void Send(RunAction action)
        {
            if (_busy || _session == null) return;
            var events = _session.Do(action);
            StartCoroutine(PlayRoutine(events));
        }

        private IEnumerator PlayRoutine(IReadOnlyList<RunEvent> events)
        {
            _busy = true;
            SetControlsInteractable(false);

            SpinOutcome spinToAnimate = HandleEvents(events);

            if (spinToAnimate != null && spinToAnimate.result != null)
            {
                long expBefore = spinToAnimate.newExp - spinToAnimate.gained;
                var res = spinToAnimate.result;
                long gained = spinToAnimate.gained;
                yield return reelView.PlaySpinRoutine(spinToAnimate.result, () =>
                {
                    hudView.RefreshAfterSpin(_session.State, _session.PreviewQuotaSpins(), expBefore);
                    SetResultLine(gained, res.score, res.coins);
                });
            }
            else
            {
                reelView.Clear();
                hudView.RefreshInstant(_session.State, _session.PreviewQuotaSpins());
            }

            RefreshBagLabel();
            RefreshDeviceRow();
            RefreshPhasePanel();

            SetControlsInteractable(true);
            _busy = false;
        }

        // "스테이지 정보 영역"(릴-노트 사이 flex 영역) 획득 요약 — Fable 지시로 이모지 없이 한글 라벨만
        // 사용한다(astral 이모지가 레거시 Text에서 렌더링되지 않는 문제 회피, HudView 코인/점수와 동일 이유).
        private void SetResultLine(long expGained, long scoreGained, int coinsGained)
        {
            if (resultLineText == null) return;
            resultLineText.text =
                $"획득 EXP +{NumberFormat.Comma(expGained)} · 점수 +{NumberFormat.Comma(scoreGained)} · 코인 +{coinsGained}";
        }

        private void SetControlsInteractable(bool interactable)
        {
            if (controlsGroup != null)
            {
                controlsGroup.interactable = interactable;
                controlsGroup.blocksRaycasts = interactable;
            }
        }

        // ── 페이즈 패널 ──────────────────────────────────────────────────────────────
        private void RefreshPhasePanel()
        {
            var run = _session.State;
            var phase = run.Phase;

            if (phase != RunPhase.NodeSelect) nodePanel?.Hide();
            if (phase != RunPhase.EventAugment && phase != RunPhase.EventRelic) perkOfferPanel?.Hide();
            if (phase != RunPhase.EventShop) shopPanel?.Hide();
            if (phase != RunPhase.PostSpin) postSpinPanel?.Hide();
            if (phase != RunPhase.GameOver) gameOverPanel?.Hide();

            switch (phase)
            {
                case RunPhase.NodeSelect:
                    nodePanel?.Show(_lastClear, run, idx => Send(new ChooseNode(idx)));
                    break;
                case RunPhase.EventAugment:
                case RunPhase.EventRelic:
                    perkOfferPanel?.Show(run, _lastOfferEvent,
                        idx => Send(new PickOffer(idx)), idx => Send(new HoldAugment(idx)), () => Send(new Retake()));
                    break;
                case RunPhase.EventShop:
                    shopPanel?.Show(run, idx => Send(new BuyOffer(idx)), () => Send(new RerollShop()), () => Send(new LeaveShop()));
                    break;
                case RunPhase.PostSpin:
                    postSpinPanel?.Show(run, _lastFailure, OpenManipPicker, () => Send(new GamblerReroll()), () => Send(new Continue()));
                    break;
                case RunPhase.GameOver:
                    gameOverPanel?.Show(_session, _lastFailure, () => appRoot.EndRun());
                    break;
                default:
                    break; // Spin: 오버레이 없음 — 하단 조작부 그대로 노출.
            }
        }

        private void OpenManipPicker(DeviceDef dev)
        {
            manipPickPopup?.Show(_session.State, dev, (devId, arg) => Send(new DeviceCmd(devId, arg)));
        }

        // ── 가방 라벨 / 장치열 ───────────────────────────────────────────────────────
        private void RefreshBagLabel()
        {
            if (bagButtonLabel != null) bagButtonLabel.text = $"🎒{_session.State.Items.Count}/{ItemUse.ItemSlots}";
        }

        private void RefreshDeviceRow()
        {
            if (deviceRow == null || deviceButtonTemplate == null) return;
            for (int i = deviceRow.childCount - 1; i >= 0; i--)
            {
                var child = deviceRow.GetChild(i);
                if (child == deviceButtonTemplate) continue;
                Destroy(child.gameObject);
            }

            var run = _session.State;
            AddDeviceButtonIfAny(run.Device, isSecondary: false);
            AddDeviceButtonIfAny(run.Device2, isSecondary: true);

            if (run.CharId == "gambler")
            {
                var btn = Instantiate(deviceButtonTemplate, deviceRow);
                btn.gameObject.SetActive(true);
                btn.name = "Device_GamblerReroll";
                SetDeviceButtonVisual(btn, "🎲재굴림", UiKit.Good, UiKit.Bg, true, () => Send(new GamblerReroll()));
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
                var chip = Instantiate(deviceButtonTemplate, deviceRow);
                chip.gameObject.SetActive(true);
                chip.name = "Device_" + dev.id;
                SetDeviceButtonVisual(chip, $"{dev.emoji}{dev.name}{suffix}", UiKit.Card, UiKit.TextSecondary, false, null);
                return;
            }
            // dev_holdfile/dev_retake는 증강·유물 오퍼 패널 전용 명령(RunController.cs 계약) — 메인 줄 제외.
            if (dev.id == "dev_holdfile" || dev.id == "dev_retake") return;

            var btn = Instantiate(deviceButtonTemplate, deviceRow);
            btn.gameObject.SetActive(true);
            btn.name = "Device_" + dev.id;
            SetDeviceButtonVisual(btn, $"{dev.emoji}{dev.name}{suffix}", UiKit.Blue, UiKit.Bg, true, () => OnDeviceButtonClick(dev));
        }

        private static void SetDeviceButtonVisual(RectTransform btnRt, string label, Color bg, Color fg, bool interactable, Action onClick)
        {
            var image = btnRt.GetComponent<Image>();
            if (image != null) image.color = bg;
            var text = btnRt.Find("Label")?.GetComponent<Text>();
            if (text != null) { text.text = label; text.color = fg; }
            var button = btnRt.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = interactable;
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick());
            }
        }

        private void OnDeviceButtonClick(DeviceDef dev)
        {
            if (dev.kind == "MANIP") OpenManipPicker(dev);
            else Send(new DeviceCmd(dev.id));
        }

        // ── RunEvent → 토스트/노트 피드 번역(이관 원본 RunScreen.HandleEvents 그대로) ──────────────
        // 반환값: 이번 배치에서 마지막으로 스핀 결과를 들고 있던 SpinOutcome(없으면 null) — ReelView
        // 연출 대상 결정에 쓰인다(원본의 _lastSpin 대입 순서와 동일하게 "마지막 것이 이긴다").
        private SpinOutcome HandleEvents(IReadOnlyList<RunEvent> events)
        {
            SpinOutcome spinToAnimate = null;

            for (int idx = 0; idx < events.Count; idx++)
            {
                var e = events[idx];
                switch (e.type)
                {
                    case "REJECTED":
                        appRoot?.Router?.Toast?.Show(RejectReasonText(e.reason));
                        break;

                    case "SPIN_RESULT":
                        spinToAnimate = e.spin;
                        AppendSpinNotes(e.spin);
                        break;

                    case "REVIVED":
                        spinToAnimate = e.spin;
                        AppendSpinNotes(e.spin);
                        notesFeed?.Append(e.failure != null && e.failure.kind == "FATE_BELL_REVIVE"
                            ? "🔔 운명의종 발동! 스핀 +1" : "📜 보험증서 발동! 스핀 +2");
                        break;

                    case "POST_SPIN":
                        spinToAnimate = e.spin;
                        AppendSpinNotes(e.spin);
                        _lastFailure = e.failure;
                        break;

                    case "STAGE_CLEARED":
                        // ⚠️ UI 계약: 즉시클리어 아이템 경로는 spin.result가 null이다(RunController.cs 헤더
                        // 주의 1번) — 릴 갱신은 건너뛰고 요약만 노트에 남긴다.
                        if (e.spin != null && e.spin.result != null) { spinToAnimate = e.spin; AppendSpinNotes(e.spin); }
                        if (e.clear != null) { _lastClear = e.clear; notesFeed?.Append(ClearSummaryText(e.clear)); }
                        break;

                    case "GAME_OVER":
                        if (e.spin != null && e.spin.result != null) { spinToAnimate = e.spin; AppendSpinNotes(e.spin); }
                        _lastFailure = e.failure;
                        break;

                    case "DEVICE_MANIP_RESULT":
                        spinToAnimate = e.spin;
                        AppendSpinNotes(e.spin);
                        break;

                    case "ITEM_USED":
                        notesFeed?.Append($"🎒 {ItemLabel(e.itemId)} 사용");
                        if (e.spin != null && e.spin.result != null) { spinToAnimate = e.spin; AppendSpinNotes(e.spin); }
                        break;

                    case "DEVICE_ARMED":
                        notesFeed?.Append($"🔧 {DeviceLabel(e.deviceId)} 예약{(e.secondary ? "(보조)" : "")}");
                        break;

                    case "DEVICE_PEEK":
                        notesFeed?.Append($"🔮 다음 스핀 확정: {PeekCellsText(e.peekCells)}");
                        break;

                    case "PERK_GRANTED":
                        notesFeed?.Append($"✅ 획득: {PerkLabel(e.perkId)}");
                        break;

                    case "PERK_HELD":
                        notesFeed?.Append($"🗂️ 보류: {PerkLabel(e.perkId)}");
                        break;

                    case "PERK_OFFER":
                        _lastOfferEvent = e;
                        break;

                    case "RETAKE_EMPTY":
                        // _lastOfferEvent는 갱신하지 않는다 — 기존 오퍼(run.PerkOfferIds)가 그대로 유지되고
                        // 이 이벤트엔 배지 필드(offerTier 등)가 없어 덮어쓰면 정보가 유실된다.
                        notesFeed?.Append("🔁 재추첨 — 후보 없음(코인 환불)");
                        break;

                    case "NODE_RESOLVED":
                        notesFeed?.Append(NodeResolvedText(e));
                        break;

                    case "SHOP_PURCHASED":
                        notesFeed?.Append($"🛒 구매: {ShopEntryLabel(e.shopBought)}");
                        break;

                    case "SHOP_REROLLED":
                        notesFeed?.Append("🔁 상점 리롤");
                        break;

                    case "SHOP_LEFT":
                        notesFeed?.Append("🚪 상점을 나갑니다");
                        break;

                    case "RUN_STARTED":
                        notesFeed?.Append($"🎬 런 시작 · 🪙{NumberFormat.Comma(e.coinsDelta)}");
                        break;

                    default:
                        break;
                }
            }

            return spinToAnimate;
        }

        private void AppendSpinNotes(SpinOutcome spin)
        {
            if (spin?.notes == null || notesFeed == null) return;
            for (int i = 0; i < spin.notes.Count; i++) notesFeed.Append(spin.notes[i]);
        }

        // ── 라벨/문구 헬퍼(이관 원본 RunPanels.cs/RunScreen.cs 그대로) ─────────────────────
        private static string PerkLabel(string perkId)
        {
            var p = Perks.ById(perkId);
            return p != null ? $"{p.emoji}{p.name}" : (perkId ?? "");
        }

        private static string ItemLabel(string itemId)
        {
            var i = Items.ById(itemId);
            return i != null ? $"{i.emoji}{i.name}" : (itemId ?? "");
        }

        private static string DeviceLabel(string deviceId)
        {
            var d = Devices.ById(deviceId);
            return d != null ? $"{d.emoji}{d.name}" : (deviceId ?? "");
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
                    return $"🌑 저주 획득: {PerkLabel(e.curseGrantedId)} · 🪙+{NumberFormat.Comma(e.coinsDelta)}";
                case NodeKind.Risk:
                    return $"⚠️ 위험! {PerkLabel(e.augmentGrantedId)} + 저주 {PerkLabel(e.curseGrantedId)}";
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
            if (!string.IsNullOrEmpty(e.itemGrantedId)) parts.Add($"🎁{ItemLabel(e.itemGrantedId)}");
            if (!string.IsNullOrEmpty(e.relicGrantedId)) parts.Add($"🛡️{PerkLabel(e.relicGrantedId)}");
            if (!string.IsNullOrEmpty(e.augmentGrantedId)) parts.Add($"✨{PerkLabel(e.augmentGrantedId)}");
            if (!string.IsNullOrEmpty(e.curseRemovedId)) parts.Add($"정화: {PerkLabel(e.curseRemovedId)} 제거");
            return string.Join(" · ", parts);
        }

        private static string ShopEntryLabel(ShopEntry entry)
        {
            if (entry == null) return "";
            return entry.kind == 'A' || entry.kind == 'R' ? PerkLabel(entry.id) : ItemLabel(entry.id);
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
