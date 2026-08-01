using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 인트로 타이틀 화면 = 웹 단독판 renderIntro 이식 — ENGINE_PORT_DESIGN.md S12 §3. Intro 씬의
    // 항상-첫 화면(IntroSceneRoot.ShowInitialScreen)이다 — 닉네임 유무 판정은 이전 슬라이스(S8/S11)
    // 처럼 씬 진입 시점이 아니라, 여기 시작 버튼을 누른 "이후"로 미뤘다(웹 exitIntro와 동일 순서).
    //
    // 릴 타일 3개는 110ms마다 무작위 심볼로 바뀌고(웹 introTimer setInterval 그대로), 동시에 각자
    // 1.6s 주기로 둥실+글로우 펄스를 돈다(delay 0/.2/.4s로 위상이 어긋난다 — 웹 `introReel` 키프레임).
    // uGUI엔 실제 blur box-shadow가 없어 "2px 골드 테두리 + 24px 글로우"는 Outline 하나(테두리+반투명
    // 확산 근사)로 재해석했다(설계 §7 "box-shadow 외부 글로우 → Outline").
    public sealed class TitleView : MonoBehaviour
    {
        private const float SymbolCycleInterval = 0.11f; // 웹 introTimer 110ms
        private const float BobPeriod = 1.6f;             // 웹 introReel 1.6s
        private const float BobAmplitude = 13f;            // CSS -7px × 1.9(스케일 규칙)
        private static readonly float[] BobDelay = { 0f, 0.2f, 0.4f };
        private const float GlowAlphaLo = 0.35f, GlowAlphaHi = 0.85f; // 웹 introReel box-shadow 알파 펄스
        private const float GlowDistLo = 3f, GlowDistHi = 6f;         // 글로우 확산 근사(설계 미명시 — Outline 거리)
        private const float EntryRiseDuration = 0.28f; // 웹 introFade 0.28s
        private const float EntryRiseOffset = 11f;      // CSS 6px × 1.9
        private const float StartBounceDuration = 0.5f; // 웹 gpop .5s(overshoot는 UiTween.Ease.OutBack로 근사)

        private AppRoot appRoot => AppRoot.Instance;

        [SerializeField] private RectTransform contentRoot; // 진입 페이드+라이즈 대상(§3 "진입 페이드+6px 상승")
        [SerializeField] private RectTransform[] reelTiles;
        [SerializeField] private Image[] reelIcons;
        [SerializeField] private Outline[] reelGlows;
        [SerializeField] private Sprite[] symbolSprites; // 14종(빌더가 UiSpriteGen.Load("sym_"+id)로 구움)
        [SerializeField] private Text bestText;
        [SerializeField] private Button startButton;

        private Coroutine _symbolLoop;
        private Coroutine[] _bobLoops;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnEnable()
        {
            RefreshBest();
            PlayEntry();

            if (symbolSprites != null && symbolSprites.Length > 0 && reelIcons != null && reelIcons.Length > 0)
                _symbolLoop = StartCoroutine(SymbolCycleLoop());

            if (reelTiles != null && reelTiles.Length > 0)
            {
                _bobLoops = new Coroutine[reelTiles.Length];
                for (int i = 0; i < reelTiles.Length; i++)
                    _bobLoops[i] = StartCoroutine(BobLoop(i));
            }
        }

        private void OnDisable()
        {
            if (_symbolLoop != null)
            {
                StopCoroutine(_symbolLoop);
                _symbolLoop = null;
            }
            if (_bobLoops != null)
            {
                foreach (var c in _bobLoops)
                    if (c != null) StopCoroutine(c);
                _bobLoops = null;
            }
        }

        // 웹 exitIntro(): "닉네임 없으면 Login, 있으면 Menu" — 이 판정을 씬 진입 시점(IntroSceneRoot)
        // 대신 여기(시작 버튼을 누른 시점)에서 한다(설계 S12 지시).
        private void OnStartClicked()
        {
            bool hasNick = !string.IsNullOrEmpty(LoginView.SavedNick());
            if (hasNick) appRoot?.ShowMenu();
            else appRoot?.ShowLogin();
        }

        private void RefreshBest()
        {
            if (bestText == null) return;
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile != null && profile.Runs > 0)
            {
                string title = Formulas.ScoreTitle(profile.BestScore).title;
                bestText.text = $"{title} · 최고 {NumberFormat.Comma(profile.BestScore)}점 · {NumberFormat.Comma(profile.Runs)}런";
            }
            else
            {
                bestText.text = "버튼·애니로 즐기는 슬롯 로그라이크!";
            }
        }

        // §3 "진입 시 페이드+6px 상승 0.28s" — ScreenRouter가 화면 전체를 이미 0.18s로 페이드하므로
        // (공용 전환 인프라, 건드리지 않는다) 여기서는 contentRoot 자신의 위치+알파를 한 번 더
        // 겹쳐 살짝 더 느린 "올라오는" 느낌을 낸다(두 알파가 곱해질 뿐이라 충돌하지 않는다).
        // 시작 버튼의 1회 gpop 바운스(scale .4→1.18→1)도 여기서 함께 재생한다.
        private void PlayEntry()
        {
            if (contentRoot != null)
            {
                var cg = contentRoot.GetComponent<CanvasGroup>();
                if (cg == null) cg = contentRoot.gameObject.AddComponent<CanvasGroup>();
                Vector2 basePos = contentRoot.anchoredPosition;
                contentRoot.anchoredPosition = basePos - new Vector2(0f, EntryRiseOffset);
                cg.alpha = 0f;
                UiTween.Move(this, contentRoot, contentRoot.anchoredPosition, basePos, EntryRiseDuration, UiTween.Ease.OutCubic);
                UiTween.Fade(this, cg, 0f, 1f, EntryRiseDuration);
            }

            if (startButton != null)
            {
                var rt = startButton.transform;
                rt.localScale = Vector3.one * 0.4f;
                UiTween.Scale(this, rt, rt.localScale, Vector3.one, StartBounceDuration, UiTween.Ease.OutBack);
            }
        }

        private IEnumerator SymbolCycleLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(SymbolCycleInterval);
                for (int i = 0; i < reelIcons.Length; i++)
                {
                    if (reelIcons[i] == null) continue;
                    reelIcons[i].sprite = symbolSprites[Random.Range(0, symbolSprites.Length)];
                }
            }
        }

        // 1.6s 주기 코사인 파형(0→진폭→0)으로 CSS ease-in-out 0%/50%/100% 키프레임을 근사한다 — Y
        // 위치와 글로우 강도(Outline alpha/거리)가 같은 위상으로 함께 펄스한다.
        private IEnumerator BobLoop(int index)
        {
            var tile = reelTiles[index];
            if (tile == null) yield break;
            var glow = reelGlows != null && index < reelGlows.Length ? reelGlows[index] : null;

            // 한 프레임 기다렸다가 기준 위치를 잡는다 — Awake 시점엔 HorizontalLayoutGroup이 아직
            // 배치하지 않아 전부 (0,0)이고, 그 값을 기준으로 삼으면 타일 3개가 같은 자리에 겹친다.
            yield return null;
            if (tile == null) yield break;
            float baseY = tile.anchoredPosition.y;
            float delay = index < BobDelay.Length ? BobDelay[index] : 0f;
            float startTime = Time.time + delay;

            while (true)
            {
                if (tile == null) yield break;
                float elapsed = Time.time - startTime;
                float wave;
                if (elapsed < 0f)
                {
                    wave = 0f;
                }
                else
                {
                    float phase = (elapsed % BobPeriod) / BobPeriod;
                    wave = (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * 0.5f; // 0→1→0
                }

                // X는 레이아웃 그룹이 정한 값을 그대로 두고 Y만 흔든다(겹침 방지).
                tile.anchoredPosition = new Vector2(tile.anchoredPosition.x, baseY - BobAmplitude * wave);
                if (glow != null)
                {
                    var c = glow.effectColor;
                    c.a = Mathf.Lerp(GlowAlphaLo, GlowAlphaHi, wave);
                    glow.effectColor = c;
                    float d = Mathf.Lerp(GlowDistLo, GlowDistHi, wave);
                    glow.effectDistance = new Vector2(d, -d);
                }
                yield return null;
            }
        }
    }
}
