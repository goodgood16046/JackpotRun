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
        // UiSceneBuilder.cs가 굽는 정적 라벨("집중({CMD_COST_FOCUS})" 등)과 순서가 같아야 한다.
        private static readonly string[] ModeBaseLabels = { "집중", "올인", "기도", "막판" };

        // ── S14 §B 차징(스핀 버튼 스쿼시) ────────────────────────────────────────────────
        private const float ChargeSquashDuration = 0.08f; // 설계 명시: "버튼 0.08s 스쿼시"
        private const float ChargeSquashScale = 0.94f;    // 설계 명시: "scale 0.94"
        private const float ChargeReleaseDuration = 0.10f; // 설계 미명시

        // AppRoot는 DontDestroyOnLoad 싱글턴(S8)이라 씬에 없다 — SceneBuilder가 와이어링할 수 없으므로
        // 정적 인스턴스를 계산 프로퍼티로 읽는다(호출부는 그대로 "appRoot.XXX").
        private AppRoot appRoot => AppRoot.Instance;
        [SerializeField] private HudView hudView;
        [SerializeField] private ReelView reelView;
        // S16 — 구 resultLineText(한 줄 요약)+NotesFeed 로그 나열을 "결과 패널"로 교체(GainPanel.cs).
        [SerializeField] private GainPanel gainPanel;
        [SerializeField] private NotesFeed notesFeed;
        [SerializeField] private CanvasGroup controlsGroup; // 연출 중 조작부 잠금(스팸 클릭 방지)

        [Header("모드 4버튼 — 순서 고정: 집중/올인/기도/막판 (ModeOrder)")]
        [SerializeField] private Button[] modeButtons = Array.Empty<Button>();
        [SerializeField] private Button spinButton;
        [SerializeField] private Button bagButton;
        [SerializeField] private Text bagButtonLabel;
        [SerializeField] private RectTransform deviceRow;
        // WEB_PARITY P1 ⑤: "게임 포기 (즉시 결산)" 진입점 — 웹 액션바 giveUpBtn() 대응(ui.js:849-871).
        [SerializeField] private Button giveUpButton;
        [SerializeField] private RectTransform deviceButtonTemplate; // 자식 경로 계약: Label(Text)

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — HUD "?"(튜토리얼 재시작)/"⚙"(설정) 버튼.
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TutorialOverlay tutorialOverlay;
        [SerializeField] private SettingsSheet settingsSheet;

        [Header("페이즈 패널 / 팝업")]
        [SerializeField] private NodePanel nodePanel;
        [SerializeField] private PerkOfferPanel perkOfferPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private PostSpinPanel postSpinPanel;
        [SerializeField] private GameOverPanel gameOverPanel;
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — RewardDone(보상 획득 → 다음 스테이지 인트로).
        [SerializeField] private RewardDonePanel rewardDonePanel;
        [SerializeField] private BagPopup bagPopup;
        [SerializeField] private ManipPickPopup manipPickPopup;
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 정보 탭(openCellSheet 대응).
        [SerializeField] private CellInfoSheet cellInfoSheet;
        // WEB_PARITY P1 ⑤/④: 범용 확인 시트(ConfirmSheetPopup) 인스턴스 2개 — 포기 확인 / DEVICE 노드
        // 오퍼(장착·코인) 선택. 동시에 뜰 일이 없는 별개 상황이라 각자 전용 인스턴스를 쓴다.
        [SerializeField] private ConfirmSheetPopup giveUpConfirmPopup;
        [SerializeField] private ConfirmSheetPopup deviceOfferPopup;

        private GameSession _session;
        private bool _busy;
        private bool _wired;

        // 이벤트로만 갱신되는 페이즈 패널용 캐시(직전 클리어/오퍼/실패 정보 — RunState엔 없는 1회성 필드,
        // 이관 원본 RunScreen.Ctx의 _lastSpin/_lastOfferEvent/_lastFailure와 동일 역할).
        private ClearOutcome _lastClear;
        private RunEvent _lastOfferEvent;
        private FailureOutcome _lastFailure;

        // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 ui.js:695 `if (st.stage !== curStage) {
        // curStage = st.stage; if (st.boss) snd.sfx("boss"); }` — renderPlay(SPIN/POST_SPIN)에서만
        // 평가된다) — PlayRoutine 꼬리에서 phase==Spin/PostSpin일 때만 재평가한다(NodeSelect/Shop 등
        // 오버레이 화면에서는 웹도 이 체크를 건너뛴다).
        private int _lastBossCheckStage;

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
                var btnRect = modeButtons[i].GetComponent<RectTransform>();
                // S16 규칙: "스핀 시작 시... gainPanel?.Clear()" — 직전 스핀의 결과 패널이 다음 스핀
                // 시작과 동시에 사라지도록 액션 전송 직전에 비운다.
                // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 doSpin() 첫 줄 `snd.sfx("spin")`) —
                // 4개 특수스핀 버튼도 전부 스핀을 발동하므로 동일하게 재생(웹 data-act="spin" 공용).
                modeButtons[i].onClick.AddListener(() => { gainPanel?.Clear(); SoundKit.Sfx("spin"); PlaySpinChargeSquash(btnRect); Send(new Spin(mode)); });
                // 웹은 이 5개 버튼(메인+특수스핀4)을 "tap" 사운드에서 제외한다(위 spin 사운드와 겹치지
                // 않도록) — PressFx.cs 헤더 주석 참조.
                modeButtons[i].GetComponent<PressFx>()?.SuppressTapSfx();
            }
            if (spinButton != null)
            {
                var spinRect = spinButton.GetComponent<RectTransform>();
                spinButton.onClick.AddListener(() => { gainPanel?.Clear(); SoundKit.Sfx("spin"); PlaySpinChargeSquash(spinRect); Send(new Spin(SpinMode.N)); });
                spinButton.GetComponent<PressFx>()?.SuppressTapSfx();
            }
            if (bagButton != null)
                bagButton.onClick.AddListener(() => bagPopup?.Show(_session.State, itemId => Send(new UseItem(itemId))));
            if (giveUpButton != null)
                giveUpButton.onClick.AddListener(OnGiveUpClicked);
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 탭 → CellInfoSheet.
            reelView?.SetCellTapHandler(OnCellTapped);

            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — HUD "?"/"⚙".
            if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialButtonClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonClicked);

            // 웹 TOUR 6스텝 대상(#hud/#reels/#spinbtn/#ab-extra/#abicons) — RunView가 이미 들고 있는
            // 필드에서 직접 뽑는다(별도 빌더 배선 불필요): 특수스핀 4버튼 행(#ab-extra)은
            // modeButtons[0]의 부모(HGroup), 아이템/장치 열(#abicons)은 bagButton의 부모(HGroup)로
            // 근사한다(UiSceneBuilder.BuildRunControls의 실제 행 구조와 일치 — modeRow/toolRow). toolRow는
            // 아이템/장치칸뿐 아니라 giveUpButton("포기")까지 한 행에 담고 있어 이 근사는 웹 #abicons
            // (아이템/장치/상태 확인 전용)보다 살짝 넓다 — "포기" 버튼도 하이라이트 범위에 함께 들어온다
            // (Opus 2차검수 LOW⑥, 사소한 범위 확장이라 손대지 않음, 5단계 문구 "아이템 사용 · 장치 발동 ·
            // 내 빌드 확인"과 완전히 무관한 요소는 아님 — 그대로 유지).
            RectTransform extraRow = modeButtons.Length > 0 && modeButtons[0] != null
                ? modeButtons[0].transform.parent as RectTransform : null;
            RectTransform iconsRow = bagButton != null ? bagButton.transform.parent as RectTransform : null;
            tutorialOverlay?.SetTargets(
                hudView != null ? (RectTransform)hudView.transform : null,
                reelView != null ? (RectTransform)reelView.transform : null,
                spinButton != null ? spinButton.GetComponent<RectTransform>() : null,
                extraRow, iconsRow);
        }

        // 웹 tut(ui.js:201 "tut": startTutorial()) — 언제든 눌러서 처음부터 다시 볼 수 있다(웹은
        // st.phase가 SPIN/POST_SPIN일 때만 재생 — startTutorial 가드 그대로).
        private void OnTutorialButtonClicked()
        {
            if (_session == null) return;
            var phase = _session.State.Phase;
            if (phase != RunPhase.Spin && phase != RunPhase.PostSpin) return;
            tutorialOverlay?.StartTour();
        }

        // 웹 gearbtn — Opus 2차검수 필수⑥(2026-08-09): 웹 설정 시트(ui.js:881-908 openSettings)엔
        // 데이터 초기화 행 자체가 없다(그건 홈 화면 전용 `.reset-link`, ui.js:630 — 별개 UI). 런 화면의
        // settingsSheet는 애초에 reset 관련 필드를 짓지 않으므로(UiSceneBuilder.BuildSettingsSheet
        // includeReset:false) 여기서 콜백을 넘길 필요가 없다.
        // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 setSound() ui.js:875 `if (st && st.phase)
        // snd.bgmStart();`) — 런 화면 설정 시트에서 소리를 켜면 즉시 BGM을 재개한다(홈 인스턴스는
        // 이 콜백을 넘기지 않는다 — MenuView.OnSettingsClicked 참조, 웹도 st==null인 홈에선 재개하지 않음).
        private void OnSettingsButtonClicked() => settingsSheet?.Show(onSoundOn: SoundKit.BgmStart);

        // Opus 2차검수 LOW⑤(2026-08-09) — _session은 Bind() 이전(씬 로드 직후 등)엔 null일 수 있고,
        // _busy(스핀/연출 처리 중)일 때 탭하면 애니메이션 도중 상태를 읽어 화면과 안 맞는 셀 정보가 뜰
        // 수 있다 — 다른 액션 핸들러(OnGiveUpClicked 등)와 동일한 가드를 그대로 적용.
        private void OnCellTapped(int idx)
        {
            // Opus 2차검수 항목5(2026-08-09) — 릴 셀은 PressFx 없는 raw Button이라(ReelView 전용
            // 탭 감지, BuildReelCellTemplate 참조) 전역 tap 훅을 못 탄다. 웹은 셀도 `[data-act]`
            // 전역 클릭 위임을 그대로 타 무조건 tap이 나므로(가드/처리 성공 여부와 무관), 여기서도
            // 아래 가드보다 먼저 재생한다.
            SoundKit.Sfx("tap");
            if (_busy || _session == null) return;
            cellInfoSheet?.Show(_session.State, idx);
        }

        // WEB_PARITY P1 ⑤: "게임 포기" 클릭 → 확인 시트(웹 ui.js:863-871 giveUpAsk) → 확정 시 즉시 결산.
        // 오작동 방지를 위한 1회 확인만 하고, 확정 후에는 SendGiveUp()이 그대로 Send(RunAction)와 같은
        // PlayRoutine(연출·HUD 갱신·GameOverPanel 표시)을 탄다.
        private void OnGiveUpClicked()
        {
            if (_busy || _session == null) return;
            var run = _session.State;
            if (run.Phase != RunPhase.Spin && run.Phase != RunPhase.PostSpin) return;
            long score = run.Score;
            giveUpConfirmPopup?.Show(
                "게임 포기",
                $"지금까지 모은 점수 {NumberFormat.Comma(score)}점 · 스테이지 {run.Stage} 로 런을 즉시 종료·결산할까요?",
                "결산하기", SendGiveUp,
                "계속 플레이", null);
        }

        private void SendGiveUp()
        {
            if (_busy || _session == null) return;
            var events = _session.DoGiveUp();
            StartCoroutine(PlayRoutine(events));
        }

        /// <summary>S8 "전환 흐름": PlaySceneRoot.Awake → AppRoot.RegisterPlay → GameSession 생성 →
        /// RunView.Bind(session). Awake 단계에서 호출되므로 뒤이어 실행되는 OnEnable이 이 값을 그대로
        /// 이어받는다(Unity 씬 로드 순서: 모든 Awake 완료 → 모든 OnEnable).</summary>
        public void Bind(GameSession session)
        {
            _session = session;
            // OnEnable이 세션보다 먼저 도는 경로(PlaySceneRoot.Start에서 바인딩)에서는 초기화가
            // 통째로 건너뛰어져 릴이 비어 보였다 — 이미 활성 상태면 여기서 초기화를 이어서 한다.
            if (isActiveAndEnabled && !_initialized) InitForRun();
        }

        private void OnEnable()
        {
            // Bind가 먼저 호출되지 않았다면(Play 씬 단독 실행 등 예외 경로) AppRoot.Session을 폴백으로 읽는다.
            if (_session == null) _session = appRoot != null ? appRoot.Session : null;
            if (_session == null) return; // 초기 씬 로드 중 화면이 잠깐 활성화되는 경우 등 방어
            InitForRun();
        }

        private bool _initialized;

        private void InitForRun()
        {
            if (_session == null) return;
            _initialized = true;
            _busy = false;
            _lastClear = null;
            _lastOfferEvent = null;
            _lastFailure = null;
            _lastBossCheckStage = 0; // 웹 curStage 초기값 0 그대로 — 첫 renderPlay에서도 보스 체크가 돈다.

            notesFeed?.Clear();
            reelView?.Clear();
            // 첫 스핀 전까지 릴이 비어 보이지 않도록 대기 상태 셀을 세워 둔다(셀 생성은 원래
            // PlaySpinRoutine 안에서만 일어난다). 릴 수는 엔진 기본값 + 보조릴 장치 보정.
            reelView?.ShowIdle(IdleReelCount());
            hudView?.ResetForNewRun();
            gainPanel?.Clear(); // S16 규칙: 새 런 시작 시 이전 값 잔상 제거
            HidePanels();
            SetControlsInteractable(true);

            // WEB_PARITY P1 ②: 첫 판 즉시 시작 안내 토스트(웹 game.js:361 "🎮 첫 판은 바로 시작!...") —
            // astral 이모지 금지(uGUI Text 렌더 제약), 한글만. NavButton이 StartRun(firstRunToast:true)로
            // 세워 둔 플래그를 여기서 1회만 소비한다.
            if (appRoot != null && appRoot.ConsumeFirstRunToast())
                appRoot.Toast?.Show("첫 판은 바로 시작! (초보학생 + 기본 슬롯) — 다음 판부터 직접 선택해요");

            StartCoroutine(PlayRoutine(_session.Controller.LaunchEvents));
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 render() ui.js:736-737 — 자동 시작 재검사는
            // 이제 PlayRoutine 꼬리의 tutorialOverlay.MaybeAutoStart 호출이 담당한다, Opus 2차검수 LOW①.
            // 위 PlayRoutine이 LaunchEvents를 처리하면서 그 꼬리에서 1차 평가가 자연히 일어난다).
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _busy = false;
            _initialized = false;
            // 페이즈 패널/팝업은 전역 OverlayLayer 산하라(RunScreen의 자식이 아님) SetActive(false)가
            // 저절로 전파되지 않는다 — 화면을 떠날 때(EndRun→ShowMenu 등) 명시적으로 닫아야 다음 화면
            // 위에 유령처럼 남아있는 것을 막는다.
            HidePanels();
        }

        // 대기 상태 릴 개수 — SpinResolver와 같은 규칙(보조릴 장치면 +1). 엔진 상태를 읽기만 한다.
        private int IdleReelCount()
        {
            var st = _session?.Controller?.State;
            bool subreel = st != null && (st.Device == "dev_subreel" || st.Device2 == "dev_subreel");
            return subreel ? Formulas.REEL + 1 : Formulas.REEL;
        }

        private void HidePanels()
        {
            nodePanel?.Hide();
            perkOfferPanel?.Hide();
            shopPanel?.Hide();
            postSpinPanel?.Hide();
            gameOverPanel?.Hide();
            rewardDonePanel?.Hide();
            bagPopup?.Hide();
            manipPickPopup?.Hide();
            cellInfoSheet?.Hide();
            giveUpConfirmPopup?.Hide();
            deviceOfferPopup?.Hide();
            settingsSheet?.Hide();
            tutorialOverlay?.HideImmediate();
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
                var quotaSpins = _session.PreviewQuotaSpins();
                yield return reelView.PlaySpinRoutine(spinToAnimate.result, () =>
                {
                    hudView.RefreshAfterSpin(_session.State, quotaSpins, expBefore);
                    // S16 — "정지 후 획득 라인 표시": 결과 패널(GainPanel)이 대문짝 카운트업 + 기여
                    // 내역 스태거를 재생한다(구 ScorePopupRoutine 대체).
                    gainPanel?.Show(spinToAnimate);
                    // S7c 연출 훅: "코인 증가 시 Coin(릴→코인 라벨 flyTo)".
                    hudView.PlayCoinFx((RectTransform)reelView.transform, res.coins);
                    // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 doSpin 꼬리 ui.js:379-382) —
                    // 튜토리얼 1단 action 스텝 중 실제 스핀이었다면 결과 해설(2단)로 이어간다.
                    tutorialOverlay?.NotifySpinResult(_session.State, quotaSpins.quota, quotaSpins.spins);
                });
            }
            else
            {
                // 스핀 결과가 없는 이벤트 배치(런 시작·노드 선택 등)에서는 릴을 비우는 대신 대기
                // 상태로 세워 둔다 — 그냥 Clear하면 첫 스핀 전까지 릴 영역이 통째로 비어 보인다.
                reelView.ShowIdle(IdleReelCount());
                hudView.RefreshInstant(_session.State, _session.PreviewQuotaSpins());
            }

            RefreshBagLabel();
            RefreshDeviceRow();
            RefreshModeButtons();
            RefreshPhasePanel();
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 tutLive 호출 지점 — render() 매번) — 3단
            // 라이브 안내는 모든 액션 배치 처리 후 현재 phase/stage를 본다(스핀뿐 아니라 노드선택·상점
            // 등에서도 트리거되어야 하므로 RefreshPhasePanel 다음, PlayRoutine 공통 꼬리에 둔다).
            // Opus 2차검수 LOW①(2026-08-09) — 자동 시작(MaybeAutoStart)도 여기서 매번 재평가한다(웹
            // render()가 매번 조건을 다시 보는 것과 동일 취지, 1회성 코루틴이었던 이전 버전을 대체).
            if (_session != null)
            {
                tutorialOverlay?.NotifyPhase(_session.State.Phase, _session.State.Stage);
                tutorialOverlay?.MaybeAutoStart(_session.State, appRoot == null || appRoot.Profile == null || appRoot.Profile.TutDone);

                // 웹 파리티 P5 — bgmStart(웹 ui.js:696 `if (soundOn) snd.bgmStart();`, renderPlay 매번
                // 호출 — SoundKit.BgmStart는 이미 켜져 있으면 조용히 무시하므로 매번 불러도 안전)
                // + 보스 스테이지 최초 진입 사운드(위 _lastBossCheckStage 필드 주석 참조).
                var run = _session.State;
                if (run.Phase == RunPhase.Spin || run.Phase == RunPhase.PostSpin)
                {
                    SoundKit.BgmStart();
                    if (run.Stage != _lastBossCheckStage)
                    {
                        _lastBossCheckStage = run.Stage;
                        if (Bosses.For(run.Stage) != null) SoundKit.Sfx("boss");
                    }
                }
            }

            SetControlsInteractable(true);
            _busy = false;
        }

        // ── WEB_PARITY P1 ①: 모드 4버튼 비용 라벨 — 웹 ui.js:809-819 cmdBtn 대응 ────────────────────
        // 종류별 런 첫 사용이면 비용 대신 "무료" 표기 + 코인 0이어도 버튼 활성. 소진했으면 정가 표기
        // (보스 스테이지 +1 서차지 반영). 기존 라벨 구성 방식(UiSceneBuilder "집중(1)" 류)을 그대로
        // 확장해 런타임에 갱신한다.
        private void RefreshModeButtons()
        {
            var run = _session?.State;
            if (run == null || modeButtons == null) return;
            bool boss = Bosses.For(run.Stage) != null;

            for (int i = 0; i < modeButtons.Length && i < ModeOrder.Length; i++)
            {
                var mode = ModeOrder[i];
                // SpinMode.Focus→"FOCUS" 등 — RunState.UsedCmds/CmdFreeUsed가 쓰는 마커와 동일 규약
                // (SpinResolver.ResolveSpin의 CmdMarker와 일치, enum 이름 대문자화로 충분).
                string marker = mode.ToString().ToUpperInvariant();
                bool usedThisStage = run.UsedCmds.Contains(marker);
                bool free = !run.CmdFreeUsed.Contains(marker);
                int cost = SpinResolver.CmdCoinCost(mode, boss);

                var btn = modeButtons[i];
                if (btn == null) continue;
                var label = btn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    string baseLabel = i < ModeBaseLabels.Length ? ModeBaseLabels[i] : mode.ToString();
                    label.text = free ? $"{baseLabel}(무료)" : $"{baseLabel}({cost})";
                }
                bool affordable = free || run.Coins >= cost; // 무료면 코인 0이어도 활성
                btn.interactable = !usedThisStage && affordable;
            }

            // Opus 1차검수 수정⑥②(2026-08-07): 포기는 SPIN/POST_SPIN 밖에서 눌러도 OnGiveUpClicked가
            // 조용히 무시하기만 했다(클릭했는데 아무 반응 없음 — 나쁜 UX). Phase 기준으로
            // interactable을 직접 반영해 "지금은 누를 수 없다"는 사실이 버튼 자체에서 보이게 한다.
            if (giveUpButton != null)
                giveUpButton.interactable = run.Phase == RunPhase.Spin || run.Phase == RunPhase.PostSpin;
        }

        // S14 §B — 스핀을 발동한 버튼 자체를 0.94로 스쿼시했다 되돌린다(릴 반동은 ReelView가 동시에
        // 재생 — PlaySpinRoutine 시작 부분의 ChargeRoutine). PressFx가 이미 포인터 다운/업에 0.96
        // 스쿼시를 재생하지만, 그건 터치 자체의 즉각 피드백이고 이건 "스핀이 실제로 시작되는 순간"에
        // 맞춘 별도의 체감 펀치라 함께 재생해도 부자연스럽지 않다(설계 "스핀 버튼 누름 → 버튼 0.08s
        // 스쿼시" — 4버튼 모드도 전부 스핀을 발동하므로 동일하게 적용한다).
        private void PlaySpinChargeSquash(RectTransform rt)
        {
            if (rt == null) return;
            StartCoroutine(SpinChargeSquashRoutine(rt));
        }

        private IEnumerator SpinChargeSquashRoutine(RectTransform rt)
        {
            yield return UiTween.ScaleRoutine(rt, rt.localScale, Vector3.one * ChargeSquashScale, ChargeSquashDuration, UiTween.Ease.OutQuad);
            if (rt == null) yield break;
            yield return UiTween.ScaleRoutine(rt, rt.localScale, Vector3.one, ChargeReleaseDuration, UiTween.Ease.OutBack);
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
            if (phase != RunPhase.EventAugment && phase != RunPhase.EventRelic && phase != RunPhase.EventAugLevel) perkOfferPanel?.Hide();
            if (phase != RunPhase.EventShop) shopPanel?.Hide();
            if (phase != RunPhase.PostSpin) postSpinPanel?.Hide();
            if (phase != RunPhase.GameOver) gameOverPanel?.Hide();
            if (phase != RunPhase.DeviceNode) deviceOfferPopup?.Hide();
            if (phase != RunPhase.RewardDone) rewardDonePanel?.Hide();

            switch (phase)
            {
                case RunPhase.NodeSelect:
                    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 클리어 등급 화면 흔들림 escalation.
                    // NodePanel은 ReelView를 모르므로 RunView가 콜백으로 연결한다(배너 등장과 같은 타이밍).
                    nodePanel?.Show(_lastClear, run, idx => Send(new ChooseNode(idx)), tier => reelView?.PlayClearShake(tier));
                    break;
                case RunPhase.EventAugment:
                case RunPhase.EventRelic:
                case RunPhase.EventAugLevel:
                    perkOfferPanel?.Show(run, _lastOfferEvent,
                        idx => Send(new PickOffer(idx)), idx => Send(new HoldAugment(idx)), () => Send(new Retake()));
                    break;
                case RunPhase.EventShop:
                    // 웹 파리티 P5(웹 ui.js:170 `st = g.shopBuy(...); snd.sfx("coin");`) — 구매 확정 시점.
                    shopPanel?.Show(run, idx => { SoundKit.Sfx("coin"); Send(new BuyOffer(idx)); }, () => Send(new RerollShop()), () => Send(new LeaveShop()));
                    break;
                case RunPhase.PostSpin:
                    postSpinPanel?.Show(run, _lastFailure, OpenManipPicker, () => Send(new GamblerReroll()), () => Send(new Continue()));
                    break;
                case RunPhase.GameOver:
                    gameOverPanel?.Show(_session, _lastFailure, () => appRoot.EndRun());
                    break;
                // WEB_PARITY P1 ④: DEVICE 노드 오퍼(웹 game.js:2523-2529 deviceNodeTake) — [장착하기]/
                // [코인+15] 중 택1, 어느 쪽이든 장치는 영구 보유로 지급된다(TakeDevice).
                case RunPhase.DeviceNode:
                    ShowDeviceOffer(run);
                    break;
                // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 노드/상점 처리 완료 → RewardDone 화면.
                case RunPhase.RewardDone:
                    rewardDonePanel?.Show(run, () => Send(new ProceedToStage()));
                    break;
                default:
                    break; // Spin: 오버레이 없음 — 하단 조작부 그대로 노출.
            }
        }

        private void ShowDeviceOffer(RunState run)
        {
            var dev = Devices.ById(run.PendingDeviceDrop);
            if (dev == null) { deviceOfferPopup?.Hide(); return; }
            deviceOfferPopup?.Show(
                $"{dev.emoji} {dev.name} 획득",
                dev.desc,
                "장착하기", () => Send(new TakeDevice(true)),
                "코인 +15", () => Send(new TakeDevice(false)));
        }

        private void OpenManipPicker(DeviceDef dev)
        {
            manipPickPopup?.Show(_session.State, dev, (devId, arg) => Send(new DeviceCmd(devId, arg)));
        }

        // ── 가방 라벨 / 장치열 ───────────────────────────────────────────────────────
        private void RefreshBagLabel()
        {
            // S8 항목⑤: 🎒(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (bagButtonLabel != null) bagButtonLabel.text = $"가방 {_session.State.Items.Count}/{ItemUse.EffectiveSlots(_session.State)}";
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
                SetDeviceButtonVisual(btn, "재굴림", UiKit.Good, UiKit.Bg, true, () => Send(new GamblerReroll()));
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
                        appRoot?.Toast?.Show(RejectReasonText(e.reason));
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
                        notesFeed?.Append($"가방: {ItemLabel(e.itemId)} 사용");
                        if (e.spin != null && e.spin.result != null) { spinToAnimate = e.spin; AppendSpinNotes(e.spin); }
                        break;

                    case "DEVICE_ARMED":
                        notesFeed?.Append($"장치: {DeviceLabel(e.deviceId)} 예약{(e.secondary ? "(보조)" : "")}");
                        break;

                    case "DEVICE_PEEK":
                        notesFeed?.Append($"다음 스핀 확정: {PeekCellsText(e.peekCells)}");
                        break;

                    // WEB_PARITY P1 ④: DEVICE 노드 오퍼 — 실제 장착/코인 결정은 NodePanel 팝업(TakeDevice)이
                    // 처리하고, 여기서는 로그 한 줄만 남긴다.
                    case "DEVICE_OFFER":
                        notesFeed?.Append($"장치 오퍼: {DeviceLabel(e.deviceId)}");
                        break;

                    case "PERK_GRANTED":
                        notesFeed?.Append($"✅ 획득: {PerkLabel(e.perkId)}");
                        break;

                    case "PERK_HELD":
                        notesFeed?.Append($"보류: {PerkLabel(e.perkId)}");
                        break;

                    // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — AUGLEVEL 노드 선택 결과.
                    case "PERK_LEVELED":
                        notesFeed?.Append($"⬆ 강화: {PerkLabel(e.perkId)} Lv.{e.perkLevelBefore}→Lv.{e.perkLevelAfter}");
                        break;

                    case "PERK_OFFER":
                        _lastOfferEvent = e;
                        break;

                    case "RETAKE_EMPTY":
                        // _lastOfferEvent는 갱신하지 않는다 — 기존 오퍼(run.PerkOfferIds)가 그대로 유지되고
                        // 이 이벤트엔 배지 필드(offerTier 등)가 없어 덮어쓰면 정보가 유실된다.
                        notesFeed?.Append("재추첨 — 후보 없음(코인 환불)");
                        break;

                    case "NODE_RESOLVED":
                        notesFeed?.Append(NodeResolvedText(e));
                        break;

                    case "SHOP_PURCHASED":
                        notesFeed?.Append($"구매: {ShopEntryLabel(e.shopBought)}");
                        break;

                    case "SHOP_REROLLED":
                        notesFeed?.Append("상점 리롤");
                        break;

                    case "SHOP_LEFT":
                        notesFeed?.Append("상점을 나갑니다");
                        break;

                    case "RUN_STARTED":
                        notesFeed?.Append($"런 시작 · 코인 {NumberFormat.Comma(e.coinsDelta)}");
                        break;

                    // 웹 파리티 P4 — RewardDone → Spin(ProceedToStage) 완료. 별도 로그 불필요(다음
                    // 스핀 UI가 곧바로 새 스테이지 정보를 보여준다).
                    case "STAGE_STARTED":
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
            return $"스테이지 {c.clearedStage} 클리어 · {c.grade} · 점수+{NumberFormat.Comma(c.gainedScore)} · " +
                   $"코인+{NumberFormat.Comma(c.clearCoin)}{debtNote}{(c.nextNodeForcedPrism ? " · 다음 프리즘 확정" : "")}";
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
                    return $"☕ 휴식 · 코인+{NumberFormat.Comma(e.coinsDelta)}";
                case NodeKind.Gamble:
                    return e.gambleWon
                        ? $"도박 성공! 코인+{NumberFormat.Comma(e.coinsDelta)}"
                        : $"도박 실패… 코인{NumberFormat.Comma(e.coinsDelta)}";
                case NodeKind.Curse:
                    return $"저주 획득: {PerkLabel(e.curseGrantedId)} · 코인+{NumberFormat.Comma(e.coinsDelta)}";
                case NodeKind.Risk:
                    return $"⚠ 위험! {PerkLabel(e.augmentGrantedId)} + 저주 {PerkLabel(e.curseGrantedId)}";
                case NodeKind.Event:
                    return EventTableText(e);
                // WEB_PARITY P1 ④: DEVICE 노드 확정(TakeDevice) — coinsDelta>0이면 미장착(코인만),
                // 0이면 장착(웹 game.js:2523-2529 deviceNodeTake).
                case NodeKind.Device:
                    return e.coinsDelta > 0
                        ? $"장치 획득(미장착): {DeviceLabel(e.deviceGrantedId)} · 코인+{NumberFormat.Comma(e.coinsDelta)}"
                        : $"장치 장착: {DeviceLabel(e.deviceGrantedId)}";
                // 웹 파리티 P3-3 — NodeEvents.ChooseNode의 방어적 "레벨업 후보 없음" 폴백(이론상 도달 불가).
                case NodeKind.AugLevel:
                    return "⬆ 강화할 증강이 없어요";
                default:
                    return $"노드 결과: {e.node}";
            }
        }

        private static string EventTableText(RunEvent e)
        {
            var parts = new List<string> { "❓ 이벤트" };
            if (e.coinsDelta != 0) parts.Add($"코인{(e.coinsDelta > 0 ? "+" : "")}{NumberFormat.Comma(e.coinsDelta)}");
            if (e.scoreDelta != 0) parts.Add($"⭐{(e.scoreDelta > 0 ? "+" : "")}{NumberFormat.Comma(e.scoreDelta)}");
            if (e.bonusSpinsDelta != 0) parts.Add($"스핀+{e.bonusSpinsDelta}");
            if (!string.IsNullOrEmpty(e.itemGrantedId)) parts.Add(ItemLabel(e.itemGrantedId));
            if (!string.IsNullOrEmpty(e.relicGrantedId)) parts.Add(PerkLabel(e.relicGrantedId));
            if (!string.IsNullOrEmpty(e.augmentGrantedId)) parts.Add(PerkLabel(e.augmentGrantedId));
            if (!string.IsNullOrEmpty(e.curseRemovedId)) parts.Add($"정화: {PerkLabel(e.curseRemovedId)} 제거");
            // WEB_PARITY P1 ④: EVENT 6번 분기(장치 획득) — 웹 game.js:2292.
            if (!string.IsNullOrEmpty(e.deviceGrantedId)) parts.Add($"장치 {DeviceLabel(e.deviceGrantedId)}");
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
            { "PHASE_NOT_SPIN_OR_POST_SPIN", "지금은 포기할 수 없습니다" },
            { "PHASE_NOT_DEVICE_NODE", "지금은 장치를 선택할 수 없습니다" },
            { "NO_PENDING_DEVICE_DROP", "받을 장치가 없습니다" },
            { "PHASE_NOT_REWARD_DONE", "지금은 다음 스테이지를 시작할 수 없습니다" },
        };

        private static string RejectReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "⚠ 처리할 수 없습니다";
            return RejectReasons.TryGetValue(reason, out var text) ? $"⚠ {text}" : $"⚠ {reason}";
        }
    }
}
