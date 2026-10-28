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
    public enum BannerStyle { Nice, Big, Mega, Fever, Clutch, LastSpin }

    // 진동 패턴 헬퍼 — 웹 vibrate([a,b,c...])(진동/쉼 교대) 대응. SettingsSheet.SafeVibrate(ms)가 단발 진동이라 진동 구간마다
    // 한 번씩 부르고 쉼 구간은 실시간 대기로 잇는다. 진동 설정이 꺼져 있으면 즉시 종료한다.
    public static class FunHaptics
    {
        public static IEnumerator Routine(int[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || !SettingsSheet.VibeEnabled) yield break;
            for (int i = 0; i < pattern.Length; i++)
            {
                int ms = pattern[i];
                if (ms <= 0) continue;
                if (i % 2 == 0) SettingsSheet.SafeVibrate(ms);
                yield return new WaitForSecondsRealtime(ms / 1000f);
            }
        }
    }

    // 런 화면 연출 레이어 — FUN_OVERHAUL_DESIGN §2 F1(빅윈)·F2(보스 인트로)·F3(마지막 스핀)·F5(FEVER!) 의 "화면 전체" 요소.
    // 씬 리빌드 없이 ▶ Play 만으로 동작하도록 RunView.WireOnce 가 코드로 만들어 RunScreen 루트의 마지막 자식으로 얹는다
    // (UiSceneBuilder 무변경 — 전부 TMP 미사용 uGUI Text/Image, 둥근 도형은 ProcSprites). 이 객체 자체는 그래픽이 없어
    // 레이캐스트를 막지 않는다(보스 인트로 카드가 떠 있는 동안만 전면 Image 가 입력을 막는다).
    //
    //  · 중앙 대형 배너 큐: NICE!/BIG WIN!/MEGA WIN!/FEVER!/CLUTCH!/LAST SPIN! — 겹치지 않고 순서대로(웹 queueBanner/pumpBanner).
    //    새 스핀을 시작하면 대기 중인 배너는 버린다(ClearQueued), 화면 전환(클리어/종료)에는 즉시 정리한다(FlushAll).
    //  · 마지막 스핀 붉은 비네트 펄스(SetLastSpinVignette).
    //  · 보스 스테이지 시작 전면 카드 1.6초·탭 스킵(ShowBossIntro). 보스 그림은 JackpotCatalog.LoadBossSprite — 없으면 텍스트만.
    public sealed class RunFxLayer : MonoBehaviour
    {
        // ── 배너 타이밍(웹 fxbIn .34s / fxbOut .18s) ─────────────────────────────────────
        private const float BannerInDuration = 0.34f;
        private const float BannerOutDuration = 0.18f;

        // ── 보스 인트로(웹 showBossIntro 1600ms, biIn .25s / biOut .24s) ──────────────────────
        private const float BossIntroHold = 1.6f;
        private const float BossIntroFadeIn = 0.25f;
        private const float BossIntroFadeOut = 0.24f;

        // ── 마지막 스핀 비네트 ───────────────────────────────────────────────────────────
        private const float VignetteMaxAlpha = 0.42f;
        private const float VignetteMinAlpha = 0.22f;
        private const float VignetteFade = 0.4f;
        private const float VignetteHalfCycle = 0.65f;

        private struct BannerReq
        {
            public string text;
            public string sub;
            public BannerStyle style;
            public float duration;
        }

        private readonly Queue<BannerReq> _queue = new Queue<BannerReq>();
        private Coroutine _pump;
        private bool _showing;
        // 2차 검수 M1 — 코루틴 세대 토큰. 중첩(yield return IEnumerator) 코루틴은 부모 StopCoroutine 으로 멈추지 않을 수 있어, 끝난/교체된
        // 연출이 뒤늦게 값을 덮어쓰지 않도록 모든 루틴을 "단일 레벨 + 매 프레임 세대 확인"으로 짠다(불일치하면 즉시 종료). 끄기/정리
        // 경로는 최종값을 직접 써서 확정한다.
        private int _bannerGen;
        private int _vignetteGen;
        private int _bossGen;
        private float _bannerRemaining;   // 지금 떠 있는 배너의 남은 시간(PendingSeconds 계산용)

        private RectTransform _bannerRt;
        private CanvasGroup _bannerGroup;
        private Text _mainText;
        private Text _subText;
        private Outline _mainOutline;

        private Image _vignette;
        private Coroutine _vignetteRoutine;
        private bool _vignetteOn;

        private RectTransform _bossRoot;
        private CanvasGroup _bossGroup;
        private Image _bossVignette;
        private Coroutine _bossRoutine;
        private bool _bossClosing;

        public bool BannerBusy => _showing || _queue.Count > 0;
        public bool BossIntroActive => _bossRoot != null && _bossRoot.gameObject.activeSelf;

        /// <summary>runRoot(RunScreen 루트) 안에 전체화면 연출 레이어를 만들어 돌려준다.</summary>
        public static RunFxLayer Create(RectTransform runRoot)
        {
            if (runRoot == null) return null;
            var go = new GameObject("FunLayer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(runRoot, false);
            UiKit.Fill(rt);
            rt.SetAsLastSibling();
            var layer = go.AddComponent<RunFxLayer>();
            layer.Build();
            return layer;
        }

        private void Build()
        {
            BuildVignette();
            BuildBanner();
            BuildBossIntro();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 중앙 대형 배너 큐
        // ══════════════════════════════════════════════════════════════════════
        private void BuildBanner()
        {
            var go = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            _bannerRt = (RectTransform)go.transform;
            _bannerRt.SetParent(transform, false);
            _bannerRt.anchorMin = _bannerRt.anchorMax = new Vector2(0.5f, 0.62f);
            _bannerRt.pivot = new Vector2(0.5f, 0.5f);
            _bannerRt.sizeDelta = new Vector2(1040f, 300f);
            _bannerGroup = go.GetComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _mainText = UiKit.Text(_bannerRt, "", 100, Color.white, TextAnchor.MiddleCenter, true);
            _mainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _mainText.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.SetAnchors(_mainText.rectTransform, new Vector2(0f, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
            _mainOutline = _mainText.gameObject.AddComponent<Outline>();
            _mainOutline.effectDistance = new Vector2(4f, -4f);
            var shadow = _mainText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -8f);

            _subText = UiKit.Text(_bannerRt, "", 34, Color.white, TextAnchor.MiddleCenter, true);
            _subText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _subText.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.SetAnchors(_subText.rectTransform, Vector2.zero, new Vector2(1f, 0.3f), Vector2.zero, Vector2.zero);
            var subOutline = _subText.gameObject.AddComponent<Outline>();
            subOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subOutline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>배너를 큐에 넣는다. 재생 중이면 끝난 뒤 이어서 나온다(웹 queueBanner).</summary>
        public void QueueBanner(string text, BannerStyle style, float duration, string sub = "")
        {
            if (string.IsNullOrEmpty(text) || _bannerRt == null || !isActiveAndEnabled) return;   // 비활성(화면 이탈 중)이면 코루틴을 시작할 수 없다 — 버린다
            _queue.Enqueue(new BannerReq { text = text, sub = sub ?? "", style = style, duration = Mathf.Max(0.3f, duration) });
            if (_pump == null) _pump = StartCoroutine(PumpRoutine());
        }

        /// <summary>대기 중인(아직 뜨지 않은) 배너만 버린다 — 새 스핀을 시작할 때(웹 `bannerQ.length = 0`).</summary>
        public void ClearQueued() => _queue.Clear();

        /// <summary>재생 중인 배너까지 즉시 정리 — 화면 전환(클리어/종료)·런 재시작.</summary>
        public void FlushBanners()
        {
            _bannerGen++;   // 진행 중인 PumpRoutine 은 다음 프레임에 세대 불일치로 스스로 끝난다
            _queue.Clear();
            if (_pump != null) { StopCoroutine(_pump); _pump = null; }
            _showing = false;
            _bannerRemaining = 0f;
            if (_bannerGroup != null) _bannerGroup.alpha = 0f;
            if (_bannerRt != null) { _bannerRt.localScale = Vector3.one; _bannerRt.localRotation = Quaternion.identity; }
        }

        /// <summary>지금 떠 있는 배너 + 대기열 배너를 전부 보여 주는 데 필요한 남은 시간(초) — 클리어 시트를 열기 전 기다릴 길이.</summary>
        public float PendingSeconds
        {
            get
            {
                float t = _showing ? _bannerRemaining : 0f;
                foreach (var req in _queue) t += req.duration + BannerOutDuration;
                return t;
            }
        }

        /// <summary>배너가 모두 끝날 때까지(최대 maxSeconds) 기다린다 — 클리어 직전 NICE!/FEVER!/CLUTCH! 가 시트에 가려지지 않게.</summary>
        public IEnumerator WaitBanners(float maxSeconds)
        {
            float waited = 0f;
            while (BannerBusy && waited < maxSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // 배너 큐 펌프 — 한 배너의 등장/유지/퇴장을 이 코루틴 안에서 직접 돌린다(중첩 코루틴 없음). 세대가 바뀌면(FlushBanners) 즉시 끝낸다.
        private IEnumerator PumpRoutine()
        {
            int gen = _bannerGen;
            while (_queue.Count > 0)
            {
                var req = _queue.Dequeue();
                _showing = true;
                _bannerRemaining = req.duration + BannerOutDuration;
                ApplyStyle(req);

                _bannerGroup.alpha = 0f;
                _bannerRt.localScale = Vector3.one * 0.3f;
                _bannerRt.localRotation = Quaternion.Euler(0f, 0f, -4f);

                // 등장: scale .3→1 OutBack + 회전 -4°→0 + 페이드(웹 fxbIn).
                float t = 0f;
                while (t < BannerInDuration)
                {
                    if (gen != _bannerGen) yield break;
                    t += Time.deltaTime;
                    _bannerRemaining = Mathf.Max(0f, req.duration + BannerOutDuration - t);
                    float k = Mathf.Clamp01(t / BannerInDuration);
                    float e = UiTween.Apply(UiTween.Ease.OutBack, k);
                    _bannerRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, e);
                    _bannerRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-4f, 0f, k));
                    _bannerGroup.alpha = Mathf.Clamp01(k * 3f);
                    yield return null;
                }
                if (gen != _bannerGen) yield break;
                _bannerRt.localScale = Vector3.one;
                _bannerRt.localRotation = Quaternion.identity;
                _bannerGroup.alpha = 1f;

                // 유지 — 스타일별 장식(MEGA 무지개 색 순환 · FEVER/LAST SPIN 박동).
                float hold = Mathf.Max(0.05f, req.duration - BannerInDuration);
                float h = 0f;
                while (h < hold)
                {
                    if (gen != _bannerGen) yield break;
                    h += Time.deltaTime;
                    _bannerRemaining = Mathf.Max(0f, hold - h + BannerOutDuration);
                    if (req.style == BannerStyle.Mega) _mainText.color = Color.HSVToRGB(Mathf.Repeat(h * 0.9f, 1f), 0.55f, 1f);
                    float beat = 1f;
                    if (req.style == BannerStyle.Fever) beat = 1f + 0.08f * Mathf.Max(0f, Mathf.Sin(h * Mathf.PI * 2f / 0.3f));
                    else if (req.style == BannerStyle.LastSpin) beat = 1f + 0.10f * Mathf.Max(0f, Mathf.Sin(h * Mathf.PI * 2f / 0.45f));
                    _bannerRt.localScale = Vector3.one * beat;
                    yield return null;
                }

                // 퇴장: 커지며 사라짐(웹 fxbOut scale 1.12 + fade).
                t = 0f;
                while (t < BannerOutDuration)
                {
                    if (gen != _bannerGen) yield break;
                    t += Time.deltaTime;
                    _bannerRemaining = Mathf.Max(0f, BannerOutDuration - t);
                    float k = Mathf.Clamp01(t / BannerOutDuration);
                    _bannerRt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, k);
                    _bannerGroup.alpha = 1f - k;
                    yield return null;
                }
                if (gen != _bannerGen) yield break;
                _bannerGroup.alpha = 0f;
                _bannerRt.localScale = Vector3.one;
            }
            if (gen != _bannerGen) yield break;
            _showing = false;
            _bannerRemaining = 0f;
            _pump = null;
        }

        private void ApplyStyle(BannerReq req)
        {
            Color main, glow;
            int size, subSize = 34;
            switch (req.style)
            {
                case BannerStyle.Nice:
                    main = new Color(0.80f, 0.92f, 1f); glow = new Color(0.47f, 0.78f, 1f, 0.9f); size = 92; break;
                case BannerStyle.Big:
                    main = new Color(1f, 0.86f, 0.30f); glow = new Color(1f, 0.55f, 0.05f, 0.9f); size = 112; break;
                case BannerStyle.Mega:
                    main = Color.white; glow = new Color(1f, 0.43f, 0.78f, 0.95f); size = 132; break;
                case BannerStyle.Fever:
                    main = new Color(1f, 0.62f, 0.12f); glow = new Color(1f, 0.28f, 0.05f, 0.95f); size = 140; break;
                case BannerStyle.Clutch:
                    main = new Color(1f, 0.90f, 0.50f); glow = new Color(1f, 0.36f, 0.42f, 0.9f); size = 120; break;
                default: // LastSpin
                    main = new Color(1f, 0.49f, 0.54f); glow = new Color(1f, 0.23f, 0.31f, 0.95f); size = 96; subSize = 30; break;
            }
            _mainText.text = req.text;
            _mainText.fontSize = size;
            _mainText.color = main;
            _mainOutline.effectColor = glow;
            _subText.text = req.sub;
            _subText.fontSize = subSize;
        }

        // ══════════════════════════════════════════════════════════════════════
        // F3 마지막 스핀 붉은 비네트
        // ══════════════════════════════════════════════════════════════════════
        private void BuildVignette()
        {
            var go = new GameObject("LastSpinVignette", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            UiKit.Fill(rt);
            _vignette = go.GetComponent<Image>();
            _vignette.sprite = ProcSprites.Vignette();
            _vignette.type = Image.Type.Simple;
            _vignette.raycastTarget = false;
            _vignette.color = new Color(1f, 0.16f, 0.24f, 0f);
        }

        /// <summary>미클리어 마지막 스핀 대기 중이면 붉은 비네트를 은은히 펄스한다(웹 body.last-spin .lastspin-vig).</summary>
        public void SetLastSpinVignette(bool on)
        {
            if (_vignette == null || on == _vignetteOn) return;
            _vignetteOn = on;
            _vignetteGen++;
            if (_vignetteRoutine != null) { StopCoroutine(_vignetteRoutine); _vignetteRoutine = null; }
            if (!isActiveAndEnabled) { SetVignetteAlpha(on ? VignetteMaxAlpha : 0f); return; }   // 비활성이면 코루틴 없이 최종값만
            _vignetteRoutine = StartCoroutine(VignetteRoutine(on, _vignetteGen));
        }

        // 켜기: 현재 알파에서 최대치까지 페이드 인 후 최대↔최소 펄스를 반복 / 끄기: 0 까지 페이드 아웃 후 최종값 0 확정.
        // 단일 레벨 루프 + 세대 확인(중첩 코루틴 없음) — 켜고 끄기가 빠르게 번갈아도 늦은 쪽이 값을 덮어쓰지 않는다.
        private IEnumerator VignetteRoutine(bool on, int gen)
        {
            float from = _vignette != null ? _vignette.color.a : 0f;
            float to = on ? VignetteMaxAlpha : 0f;
            float t = 0f;
            while (t < VignetteFade)
            {
                if (gen != _vignetteGen || _vignette == null) yield break;
                t += Time.deltaTime;
                SetVignetteAlpha(Mathf.Lerp(from, to, UiTween.Apply(UiTween.Ease.OutQuad, t / VignetteFade)));
                yield return null;
            }
            if (gen != _vignetteGen) yield break;
            SetVignetteAlpha(to);
            if (!on) { _vignetteRoutine = null; yield break; }

            float phase = 0f;
            while (_vignette != null)
            {
                if (gen != _vignetteGen) yield break;
                phase += Time.deltaTime;
                float k = Mathf.PingPong(phase / VignetteHalfCycle, 1f);
                SetVignetteAlpha(Mathf.Lerp(VignetteMaxAlpha, VignetteMinAlpha, k));
                yield return null;
            }
        }

        private void SetVignetteAlpha(float a)
        {
            if (_vignette == null) return;
            var c = _vignette.color;
            c.a = a;
            _vignette.color = c;
        }

        // ══════════════════════════════════════════════════════════════════════
        // F2 보스 인트로 전면 카드
        // ══════════════════════════════════════════════════════════════════════
        private Text _bossTagText, _bossNameText, _bossDescText;
        private RectTransform _bossArtFrame;
        private Image _bossArtImage;
        private RectTransform _bossCard;

        private void BuildBossIntro()
        {
            var rootGo = new GameObject("BossIntro", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            _bossRoot = (RectTransform)rootGo.transform;
            _bossRoot.SetParent(transform, false);
            UiKit.Fill(_bossRoot);
            var dim = rootGo.GetComponent<Image>();
            dim.color = new Color(0.03f, 0.01f, 0.02f, 0.88f);   // 전면 딤 — raycastTarget 이 켜져 있어 카드가 뜬 동안 아래 입력을 막는다
            var btn = rootGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = dim;
            btn.onClick.AddListener(CloseBossIntro);
            _bossGroup = rootGo.GetComponent<CanvasGroup>();
            _bossGroup.alpha = 0f;

            var vigGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            var vrt = (RectTransform)vigGo.transform;
            vrt.SetParent(_bossRoot, false);
            UiKit.Fill(vrt);
            _bossVignette = vigGo.GetComponent<Image>();
            _bossVignette.sprite = ProcSprites.Vignette();
            _bossVignette.type = Image.Type.Simple;
            _bossVignette.raycastTarget = false;
            _bossVignette.color = new Color(1f, 0.1f, 0.15f, 0.5f);

            // 카드 — 세로 레이아웃(내용 높이에 맞춰 자란다).
            var card = UiKit.VGroup(_bossRoot, 12, new RectOffset(34, 34, 28, 26), true, true, autoSizeH: true);
            card.name = "Card";
            _bossCard = card;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(780f, 0f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = ProcSprites.RoundRect(30);
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.17f, 0.07f, 0.10f, 1f);
            cardImg.raycastTarget = false;
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(1f, 0.36f, 0.42f, 0.9f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            _bossTagText = UiKit.Text(card, "", 26, new Color(1f, 0.60f, 0.65f), TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(_bossTagText, preferredHeight: 40, flexibleHeight: 0);

            var frame = UiKit.Panel(card, "ArtFrame", new Color(0.05f, 0.03f, 0.06f, 1f), ProcSprites.RoundRect(20));
            frame.GetComponent<Image>().raycastTarget = false;
            UiKit.SizeHint(frame, preferredHeight: 440, flexibleHeight: 0);
            _bossArtFrame = frame;
            var frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(1f, 0.78f, 0.47f, 0.55f);
            frameOutline.effectDistance = new Vector2(2f, -2f);
            _bossArtImage = UiKit.Image(frame, null, Color.white);
            _bossArtImage.name = "Art";
            _bossArtImage.raycastTarget = false;
            UiKit.SetAnchors(_bossArtImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            _bossNameText = UiKit.Text(card, "", 58, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(_bossNameText, preferredHeight: 76, flexibleHeight: 0);
            var nameShadow = _bossNameText.gameObject.AddComponent<Outline>();
            nameShadow.effectColor = new Color(1f, 0.24f, 0.31f, 0.75f);
            nameShadow.effectDistance = new Vector2(2f, -2f);

            _bossDescText = UiKit.Text(card, "", 28, UiKit.Txt2, TextAnchor.UpperCenter);
            UiKit.SizeHint(_bossDescText, preferredHeight: 84, flexibleHeight: 0);

            var skip = UiKit.Text(card, "탭하여 건너뛰기", 22, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(skip, preferredHeight: 34, flexibleHeight: 0);

            _bossRoot.gameObject.SetActive(false);
        }

        /// <summary>보스 스테이지 시작 전면 카드를 띄운다(1.6초 후 자동 닫힘, 탭하면 즉시). 그림이 없으면 그림 칸 없이 텍스트만.</summary>
        public void ShowBossIntro(Boss boss, int stage)
        {
            if (boss == null || _bossRoot == null || !isActiveAndEnabled) return;
            _bossGen++;
            if (_bossRoutine != null) StopCoroutine(_bossRoutine);

            _bossTagText.text = $"BOSS STAGE {stage}";
            // 보스 이름은 astral 이모지를 빼고 한글만(TextSanitize 관례). desc 도 같은 규칙.
            _bossNameText.text = TextSanitize.StripAstral(boss.name);
            _bossDescText.text = TextSanitize.StripAstral(boss.desc ?? "");
            var sprite = JackpotCatalog.LoadBossSprite(boss.id);
            _bossArtImage.sprite = sprite;
            _bossArtImage.enabled = sprite != null;
            if (_bossArtFrame != null) _bossArtFrame.gameObject.SetActive(sprite != null);

            _bossClosing = false;
            _bossRoot.gameObject.SetActive(true);
            _bossRoot.SetAsLastSibling();
            _bossGroup.alpha = 0f;
            if (_bossCard != null) _bossCard.localScale = Vector3.one * 0.6f;
            StartCoroutine(FunHaptics.Routine(new[] { 30, 50, 90 }));
            _bossRoutine = StartCoroutine(BossIntroRoutine(_bossGen));
        }

        // 등장 페이드/카드 팝 → 유지(1.6s, 탭하면 즉시 종료) → 페이드 아웃을 한 코루틴 안에서 직접 돌린다(중첩 코루틴 없음, 세대 확인).
        private IEnumerator BossIntroRoutine(int gen)
        {
            float t = 0f;
            while (t < BossIntroFadeIn)
            {
                if (gen != _bossGen) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / BossIntroFadeIn);
                _bossGroup.alpha = k;
                if (_bossCard != null)
                    _bossCard.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UiTween.Apply(UiTween.Ease.OutBack, Mathf.Clamp01(t / 0.5f)));
                yield return null;
            }
            if (gen != _bossGen) yield break;
            _bossGroup.alpha = 1f;

            float held = 0f;
            float hold = Mathf.Max(0.1f, BossIntroHold - BossIntroFadeIn);
            while (held < hold && !_bossClosing)
            {
                if (gen != _bossGen) yield break;
                held += Time.deltaTime;
                t += Time.deltaTime;
                if (_bossCard != null && t < 0.5f)   // 팝 애니가 페이드 인보다 길 때 마저 재생
                    _bossCard.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UiTween.Apply(UiTween.Ease.OutBack, Mathf.Clamp01(t / 0.5f)));
                else if (_bossCard != null) _bossCard.localScale = Vector3.one;
                if (_bossVignette != null)
                {
                    var c = _bossVignette.color;
                    c.a = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(held * Mathf.PI * 2f / 0.8f));
                    _bossVignette.color = c;
                }
                yield return null;
            }
            if (_bossCard != null) _bossCard.localScale = Vector3.one;

            float startAlpha = _bossGroup.alpha;
            float f = 0f;
            while (f < BossIntroFadeOut)
            {
                if (gen != _bossGen) yield break;
                f += Time.deltaTime;
                _bossGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(f / BossIntroFadeOut));
                yield return null;
            }
            if (gen != _bossGen) yield break;
            _bossGroup.alpha = 0f;
            if (_bossRoot != null) _bossRoot.gameObject.SetActive(false);
            _bossRoutine = null;
        }

        private void CloseBossIntro()
        {
            // 탭 스킵 — 유지 루프만 끊고 페이드아웃은 BossIntroRoutine 이 이어서 처리한다(이미 닫는 중이면 무시).
            _bossClosing = true;
        }

        /// <summary>보스 인트로를 즉시 숨긴다(화면을 떠날 때·새 런).</summary>
        public void HideBossIntroImmediate()
        {
            _bossGen++;
            if (_bossRoutine != null) { StopCoroutine(_bossRoutine); _bossRoutine = null; }
            _bossClosing = false;
            if (_bossGroup != null) _bossGroup.alpha = 0f;
            if (_bossRoot != null) _bossRoot.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 공용
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>진동 패턴(진동/쉼 ms 교대)을 재생한다. 진동 설정을 존중한다.</summary>
        public void Haptic(params int[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || !isActiveAndEnabled) return;
            StartCoroutine(FunHaptics.Routine(pattern));
        }

        /// <summary>배너·비네트·보스 인트로를 전부 정리 — 새 런 시작·화면 이탈.</summary>
        public void FlushAll()
        {
            FlushBanners();
            _vignetteGen++;
            if (_vignetteRoutine != null) { StopCoroutine(_vignetteRoutine); _vignetteRoutine = null; }
            _vignetteOn = false;
            SetVignetteAlpha(0f);
            HideBossIntroImmediate();
        }

        private void OnDisable()
        {
            // 화면이 꺼지면 모든 코루틴이 멈춘다 — 다음에 켜질 때 반쯤 그려진 채 남지 않게 상태를 되돌린다.
            _bannerGen++;
            _vignetteGen++;
            _bossGen++;
            _queue.Clear();
            _pump = null;
            _showing = false;
            _bannerRemaining = 0f;
            _vignetteRoutine = null;
            _vignetteOn = false;
            _bossRoutine = null;
            if (_bannerGroup != null) _bannerGroup.alpha = 0f;
            SetVignetteAlpha(0f);
            if (_bossGroup != null) _bossGroup.alpha = 0f;
            if (_bossRoot != null) _bossRoot.gameObject.SetActive(false);
        }
    }
}
