using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 상단 HUD — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/HudView.cs: 스테이지·EXP 진행바·남은 스핀·
    // 코인·점수·저주·불운 게이지·보스 테두리+진입 배너. 이관 원본: Scripts/UI/RunScreen.cs의
    // BuildHud/RefreshHud(레이아웃은 버리고 로직만) + S7 "RunView 연출" 지시(EXP 바 트윈·골드 펄스·
    // 불운 게이지 펄스·보스 적색 틴트+배너)를 얹었다.
    //
    // 연출 수치 중 설계 문서에 명시되지 않은 것(펄스/배너 길이 등)은 이 파일 안에서 합리적 기본값을
    // 상수로 못박아 뒀다 — 명시된 값(EXP CountUp 0.3s)과 헷갈리지 않도록 주석으로 구분.
    public sealed class HudView : MonoBehaviour
    {
        private const float ExpGainDuration = 0.3f; // 설계 명시: "EXP CountUp(0.3s)"
        private const float QuotaPulseDuration = 0.35f; // 설계 미명시 — 골드 펄스 기본값
        private const float BossBannerHold = 1.4f; // 설계 미명시 — 보스 진입 배너 유지 시간
        private const float BossBannerFade = 0.25f;
        private const float GaugePulseDuration = 0.3f; // 설계 미명시 — 불운 게이지 만땅 펄스

        [SerializeField] private Text stageText;
        [SerializeField] private Text cursesText;
        [SerializeField] private RectTransform expBarFill; // anchorMax.x를 0..1로 움직인다(RunScreen.cs와 동일 방식)
        [SerializeField] private Image expBarFillImage; // 골드 펄스용 색 플래시 대상
        [SerializeField] private Text expBarText;
        [SerializeField] private Text spinsText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Outline hudOutline; // 보스 스테이지 적색 틴트(UiKit.AddGlowOutline로 생성)
        [SerializeField] private Image[] unluckyPips = System.Array.Empty<Image>(); // 5칸 고정
        [SerializeField] private CanvasGroup bossBannerGroup;
        [SerializeField] private RectTransform bossBannerRect;
        [SerializeField] private Text bossBannerText;

        private string _shownBossId; // 마지막으로 배너를 띄운 보스 id(null=아직 없음/보스 아님) — 재진입 배너 중복 방지
        private Coroutine _expRoutine;
        private Coroutine _bossBannerRoutine;
        private Coroutine _gaugePulseRoutine;
        private ParticleSystem _bossFx; // S7c 연출 훅: 보스 스테이지 동안 유지되는 fx_boss 루프 핸들

        private void Awake()
        {
            if (bossBannerGroup != null) bossBannerGroup.alpha = 0f;
        }

        /// <summary>애니메이션 없이 전체를 즉시 갱신 — 화면 진입 첫 표시(RUN_STARTED 등)에 사용.</summary>
        public void RefreshInstant(RunState run, (long quota, int spins) preview)
        {
            RefreshStageCurses(run);
            RefreshSpinsCoinsScore(run, preview);
            SetExpBarImmediate(run.StageExp, preview.quota);
            RefreshUnluckyGauge(run.UnluckyGauge, false);
            RefreshBossState(run);
        }

        /// <summary>스핀 등으로 EXP/코인/점수/저주/스핀수가 바뀌었을 때 — EXP 바만 0.3s 트윈, 나머지는 즉시.</summary>
        public void RefreshAfterSpin(RunState run, (long quota, int spins) preview, long expBefore)
        {
            RefreshStageCurses(run);
            RefreshSpinsCoinsScore(run, preview);
            RefreshUnluckyGauge(run.UnluckyGauge, true);
            RefreshBossState(run);

            if (_expRoutine != null) StopCoroutine(_expRoutine);
            _expRoutine = StartCoroutine(AnimateExpRoutine(expBefore, run.StageExp, preview.quota));
        }

        private void RefreshStageCurses(RunState run)
        {
            if (stageText != null) stageText.text = $"스테이지 {run.Stage}";
            // S8 항목⑤: 🌑(astral)는 레거시 Text에서 렌더링되지 않는다 — 한글 라벨만 사용.
            if (cursesText != null) cursesText.text = $"저주 {run.Curses.Count}";
        }

        private void RefreshSpinsCoinsScore(RunState run, (long quota, int spins) preview)
        {
            if (spinsText != null)
            {
                int spinsLeft = Mathf.Max(preview.spins - run.SpinIndex, 0);
                spinsText.text = $"남은 스핀 {spinsLeft}";
            }
            // Fable 육안 검수 지시(2026-07-31): 코인 라벨이 뭘 뜻하는지 불명확 — 원인은 🪙(U+1FA99) 같은
            // 색상 이모지(astral-plane, 서로게이트 쌍)가 레거시 uGUI Text에서 렌더링되지 않는 것으로
            // 추정된다(⭐☠처럼 BMP 범위 기존 기호 문자는 렌더링됨 — RunView 필드 참고). 이모지에만
            // 의존하지 않고 한글 라벨을 항상 붙인다.
            if (coinsText != null) coinsText.text = $"코인 {NumberFormat.Comma(run.Coins)}";
            if (scoreText != null) scoreText.text = $"점수 {NumberFormat.Comma(run.Score)}";
        }

        private void SetExpBarImmediate(long exp, long quota)
        {
            float pct = quota > 0 ? Mathf.Clamp01((float)((double)exp / quota)) : 0f;
            if (expBarFill != null) expBarFill.anchorMax = new Vector2(pct, 1f);
            if (expBarText != null) expBarText.text = $"{NumberFormat.Comma(exp)} / {NumberFormat.Comma(quota)}";
        }

        private System.Collections.IEnumerator AnimateExpRoutine(long fromExp, long toExp, long quota)
        {
            bool crossedQuota = quota > 0 && fromExp < quota && toExp >= quota;
            // S7c 연출 훅: "EXP 채움 중 ExpGain" — 바 끝점(expBarFill)에서 1회 재생.
            if (toExp > fromExp) FxKit.I?.Play(FxId.ExpGain, expBarFill);
            yield return UiTween.CountUpRoutine(fromExp, toExp, ExpGainDuration, v => SetExpBarImmediate(v, quota),
                UiTween.Ease.OutCubic);
            if (crossedQuota) PlayQuotaPulse();
            _expRoutine = null;
        }

        /// <summary>S7c 연출 훅: "코인 증가 시 Coin(릴→코인 라벨 flyTo)" — RunView가 스핀 결과의
        /// coinsGained를 이미 알고 있어 여기로 직접 넘긴다(로직 변경 없음, 호출 추가).</summary>
        public void PlayCoinFx(RectTransform from, int coinsGained)
        {
            if (coinsGained <= 0 || coinsText == null) return;
            FxKit.I?.PlayFlyTo(FxId.Coin, from, coinsText.rectTransform, Mathf.Clamp(coinsGained, 1, 8));
        }

        private void PlayQuotaPulse()
        {
            if (expBarFillImage == null) return;
            StartCoroutine(QuotaPulseRoutine());
        }

        private System.Collections.IEnumerator QuotaPulseRoutine()
        {
            var baseColor = expBarFillImage.color;
            var gold = UiKit.Accent;
            yield return UiTween.FloatRoutine(0f, 1f, QuotaPulseDuration * 0.5f,
                t => expBarFillImage.color = Color.Lerp(baseColor, gold, t), UiTween.Ease.OutQuad);
            if (expBarFillImage == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, QuotaPulseDuration * 0.5f,
                t => expBarFillImage.color = Color.Lerp(baseColor, gold, t), UiTween.Ease.OutQuad);
        }

        // ── 불운 게이지 🍀 5칸 ────────────────────────────────────────────────────────
        private void RefreshUnluckyGauge(int gauge, bool allowPulse)
        {
            for (int i = 0; i < unluckyPips.Length; i++)
            {
                if (unluckyPips[i] == null) continue;
                unluckyPips[i].color = i < gauge ? UiKit.Accent : UiKit.Card;
            }
            if (allowPulse && gauge >= Formulas.UNLUCKY_MAX && _gaugePulseRoutine == null)
                _gaugePulseRoutine = StartCoroutine(GaugePulseRoutine());
        }

        private System.Collections.IEnumerator GaugePulseRoutine()
        {
            for (int i = 0; i < unluckyPips.Length; i++)
            {
                if (unluckyPips[i] == null) continue;
                yield return UiTween.ScaleRoutine(unluckyPips[i].transform, Vector3.one, Vector3.one * 1.3f,
                    GaugePulseDuration * 0.5f, UiTween.Ease.OutBack);
                yield return UiTween.ScaleRoutine(unluckyPips[i].transform, Vector3.one * 1.3f, Vector3.one,
                    GaugePulseDuration * 0.5f, UiTween.Ease.OutQuad);
            }
            _gaugePulseRoutine = null;
        }

        // ── 보스 스테이지: 테두리 적색 틴트 + 진입 배너 ─────────────────────────────────
        private void RefreshBossState(RunState run)
        {
            var boss = Bosses.For(run.Stage);
            if (stageText != null)
                stageText.text = boss != null ? $"스테이지 {run.Stage} · {boss.emoji}{boss.name}" : $"스테이지 {run.Stage}";

            if (hudOutline != null) hudOutline.enabled = boss != null;

            string bossId = boss?.id;
            if (bossId != _shownBossId)
            {
                _shownBossId = bossId;
                if (boss != null) PlayBossBanner(boss);

                // S7c 연출 훅: "보스 스테이지 Boss 루프(스테이지 종료 시 Stop)" — HUD 영역(자기 자신의
                // RectTransform, hudRoot에 붙어 있다) 기준으로 재생.
                if (boss != null && _bossFx == null) _bossFx = FxKit.I?.PlayLoop(FxId.Boss, (RectTransform)transform);
                else if (boss == null && _bossFx != null)
                {
                    FxKit.I?.StopLoop(_bossFx);
                    _bossFx = null;
                }
            }
        }

        private void PlayBossBanner(Boss boss)
        {
            if (bossBannerGroup == null || bossBannerText == null) return;
            if (_bossBannerRoutine != null) StopCoroutine(_bossBannerRoutine);
            bossBannerText.text = $"⚠ 보스: {boss.emoji}{boss.name}";
            _bossBannerRoutine = StartCoroutine(BossBannerRoutine());
        }

        private System.Collections.IEnumerator BossBannerRoutine()
        {
            if (bossBannerRect != null) bossBannerRect.anchoredPosition = new Vector2(bossBannerRect.anchoredPosition.x, 40f);
            yield return UiTween.FadeRoutine(bossBannerGroup, 0f, 1f, BossBannerFade);
            if (bossBannerRect != null)
                yield return UiTween.MoveRoutine(bossBannerRect, bossBannerRect.anchoredPosition,
                    new Vector2(bossBannerRect.anchoredPosition.x, 0f), 0.2f, UiTween.Ease.OutBack);
            yield return new WaitForSeconds(BossBannerHold);
            yield return UiTween.FadeRoutine(bossBannerGroup, 1f, 0f, BossBannerFade);
            _bossBannerRoutine = null;
        }

        /// <summary>런 재시작(화면 재진입) 시 내부 캐시 초기화 — 새 런의 첫 보스 진입 배너가 다시 뜨도록.</summary>
        public void ResetForNewRun()
        {
            _shownBossId = null;
            if (bossBannerGroup != null) bossBannerGroup.alpha = 0f;
            if (hudOutline != null) hudOutline.enabled = false;
            if (_bossFx != null)
            {
                FxKit.I?.StopLoop(_bossFx);
                _bossFx = null;
            }
        }
    }
}
