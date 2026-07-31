using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 릴 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/ReelView.cs: "SymbolCell 5~6개 — 스핀 연출 담당".
    // 셀 개수는 장치(➕보조릴 등)에 따라 스핀마다 바뀔 수 있어(SpinResult.cells.Count) 매 스핀 셀을
    // 다시 만든다(이관 원본 Scripts/UI/RunScreen.cs의 RefreshReel/BuildReelCell과 동일한 "이벤트 시에만
    // 리빌드" 원칙). 심볼 스프라이트(sym_<id>)는 런타임 코드에서 접근 불가능한 Editor 전용
    // UiSpriteGen을 참조할 수 없으므로, 빌드 시점에 UiSceneBuilder가 14종 전부를 symbolSprites 배열로
    // 구워 넣는다("빌더가 와이어링" 원칙, MenuView 캐러셀의 AssetDatabase 베이크와 동일 패턴).
    //
    // 연출 사양(S7 "RunView 연출", 수치 그대로):
    //   스핀 0.45s 순환(0.05s 간격 랜덤 교체) → 왼쪽부터 0.08s 스태거 정지(OutBack 스케일 바운스).
    //   set3=해당 셀 글로우, set4=화면 플래시(흰 6% 0.12s), 잭팟(전칸)=플래시+셰이크(6px 0.3s)+배너.
    //   해골 페널티: 셀 흔들림+적색 틴트(설계 문구상 EXP 바 항목과 나란히 적혀 있지만 "셀"이라 릴 소관).
    // 바운스 지속시간·해골 흔들림 진폭 등 설계에 수치가 없는 값은 이 파일 안에서 상수로 못박고 주석 표기.
    //
    // S8 항목⑤(심볼 표시 근본 수정): 레거시 uGUI Text는 astral(서로게이트 페어) 이모지를 렌더링하지
    // 못한다(🍒📘💎🪙👑🔥🧲💣🎲🌱🌀🗝 등 전부 미표시) — 심볼은 UiSpriteGen이 굽는 도형 스프라이트
    // (sym_<id>.png)만으로 표현하고, 이 파일의 "Emoji" 텍스트 오버레이는 완전히 제거했다(셀 자식
    // 경로 계약도 "Icon"(Image)/"Tag"(Text) + Outline으로 축소). Cell.tag(SpinResolver.cs, 엔진 산출
    // 문자열)도 astral 이모지라 TranslateTag로 BMP 안전 기호로 치환해 표시한다(엔진 파일은 수정하지
    // 않는다 — UI 레이어 표시 치환만).
    //
    // S7c 연출 훅: 셀 정지마다 FxId.SpinStop(심볼색), set3/4 성립 시 FxId.SetHit, 잭팟 시 FxId.Jackpot,
    // 해골 칸 FxId.Skull. FxKit.I가 null이면(프리팹 미로드 등) 전부 조용히 무시한다.
    public sealed class ReelView : MonoBehaviour
    {
        private const float SpinDuration = 0.45f;   // 설계 명시
        private const float CycleInterval = 0.05f;  // 설계 명시
        private const float StaggerDelay = 0.08f;   // 설계 명시
        private const float RevealBounceDuration = 0.22f; // 설계 미명시 — OutBack 바운스 길이 기본값
        private const float SetGlowPulseDuration = 0.35f; // 설계 미명시
        private const float Set4FlashAlpha = 0.06f; // 설계 명시: "흰 6% 알파"
        private const float Set4FlashDuration = 0.12f; // 설계 명시
        private const float JackpotShakeAmplitude = 6f; // 설계 명시
        private const float JackpotShakeDuration = 0.3f; // 설계 명시
        private const float JackpotBannerHold = 1.2f; // 설계 미명시
        private const float JackpotBannerFade = 0.2f;
        private const float SkullShakeAmplitude = 4f; // 설계 미명시 — 해골 페널티 흔들림 기본값
        private const float SkullShakeDuration = 0.25f;
        private const float SkullTintDuration = 0.3f;

        // UiSpriteGen.SymbolColors(Editor 전용)와 동일한 값을 런타임에서 쓰기 위한 복제 — 소스가
        // 다르면(에디터/런타임) 갈라지므로 심볼 색을 바꿀 때 두 표를 함께 갱신할 것(UiSpriteGen.cs 헤더 참조).
        private static readonly Dictionary<string, Color> SymbolTintById = new Dictionary<string, Color>
        {
            { "cherry", HexColor("#E5484D") }, { "book", HexColor("#4C8DFF") }, { "star", HexColor("#F5C518") },
            { "gem", HexColor("#7C5CFF") }, { "crown", HexColor("#FFB300") }, { "skull", HexColor("#9BA3B4") },
            { "coin", HexColor("#E8B93C") }, { "flame", HexColor("#FF6B35") }, { "magnet", HexColor("#5B8CFF") },
            { "bomb", HexColor("#3A4051") }, { "dice", HexColor("#E8EAF2") }, { "seed", HexColor("#4CAF50") },
            { "wild", HexColor("#00C2A8") }, { "key", HexColor("#C9A227") },
        };

        // Cell.tag(SpinResolver.cs)가 내보내는 astral 이모지를 BMP 안전 기호로 치환(엔진 파일은 미수정
        // — 표시 레이어에서만 변환). 목록: 성장 "🌱→"/와일드주입 "🌀"/제거 "🧽"/왕관강제 "👑"/폭탄폭발
        // "💥"/자석흡착 "🧲"(SpinResolver.cs 그렙 결과 전수).
        private static readonly Dictionary<string, string> TagTranslate = new Dictionary<string, string>
        {
            { "🌱→", "↑" }, { "🌀", "W" }, { "🧽", "X" }, { "👑", "♛" }, { "💥", "*" }, { "🧲", "M" },
        };

        [Serializable]
        private struct SymbolSprite
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private RectTransform reelRow;
        [SerializeField] private RectTransform cellTemplate; // 자식 경로 계약: "Icon"(Image)/"Tag"(Text), Outline
        [SerializeField] private SymbolSprite[] symbolSprites = Array.Empty<SymbolSprite>();
        [SerializeField] private CanvasGroup flashOverlay; // 화면 전체를 덮는 흰색 플래시(set4/잭팟 공용)
        [SerializeField] private CanvasGroup jackpotBannerGroup;
        [SerializeField] private RectTransform jackpotBannerRect;

        private Dictionary<string, Sprite> _spriteById;

        private sealed class CellView
        {
            public RectTransform rt;
            public Image icon;
            public Text tag;
            public Outline glow;
            public string lastSymId; // 마지막으로 SetCellSymbol에 전달된 심볼 id — 세트 글로우 매칭용
        }

        private readonly List<CellView> _cells = new List<CellView>();

        private void Awake()
        {
            _spriteById = new Dictionary<string, Sprite>(symbolSprites.Length);
            foreach (var s in symbolSprites)
                if (!string.IsNullOrEmpty(s.id) && s.sprite != null) _spriteById[s.id] = s.sprite;

            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            if (flashOverlay != null) flashOverlay.alpha = 0f;
            if (jackpotBannerGroup != null) jackpotBannerGroup.alpha = 0f;
        }

        /// <summary>빈 릴(런 시작 직후 등 아직 스핀 결과가 없을 때) — 이전 셀을 모두 지운다.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            for (int i = _cells.Count - 1; i >= 0; i--)
                if (_cells[i].rt != null) Destroy(_cells[i].rt.gameObject);
            _cells.Clear();
        }

        /// <summary>스핀 결과 연출 전체(순환→스태거 정지→세트/잭팟/해골 FX)를 재생한다.
        /// onCellsRevealed는 마지막 셀이 멈춘 직후(후처리 FX 시작 전) 호출된다 — RunView가 이 시점에
        /// HUD EXP 카운트업/코인·점수 플로팅 텍스트를 동시에 시작한다(설계 "정지 후 획득 라인 표시").</summary>
        public IEnumerator PlaySpinRoutine(SpinResult result, Action onCellsRevealed)
        {
            if (result == null || result.cells == null)
            {
                onCellsRevealed?.Invoke();
                yield break;
            }

            EnsureCellCount(result.cells.Count);

            // ── 순환(0.45s, 0.05s 간격 랜덤 교체) ──────────────────────────────────────
            float t = 0f;
            float sinceTick = 0f;
            while (t < SpinDuration)
            {
                float dt = Time.deltaTime;
                t += dt;
                sinceTick += dt;
                if (sinceTick >= CycleInterval)
                {
                    sinceTick = 0f;
                    for (int i = 0; i < _cells.Count; i++) SetCellSymbol(_cells[i], RandomSymbol(), "");
                }
                yield return null;
            }

            // ── 왼쪽부터 0.08s 스태거 정지(OutBack 스케일 바운스 + FxId.SpinStop) ───────────
            for (int i = 0; i < _cells.Count && i < result.cells.Count; i++)
            {
                var cell = result.cells[i];
                SetCellSymbol(_cells[i], cell.sym, cell.tag);
                StartCoroutine(RevealBounce(_cells[i]));
                if (i < _cells.Count - 1) yield return new WaitForSeconds(StaggerDelay);
            }
            yield return new WaitForSeconds(RevealBounceDuration);

            onCellsRevealed?.Invoke();

            // ── 세트/잭팟/해골 FX ───────────────────────────────────────────────────────
            yield return PostRevealFx(result);
        }

        private IEnumerator RevealBounce(CellView cv)
        {
            if (cv?.rt == null) yield break;
            cv.rt.localScale = Vector3.one * 0.82f;
            yield return UiTween.ScaleRoutine(cv.rt, cv.rt.localScale, Vector3.one, RevealBounceDuration, UiTween.Ease.OutBack);
            if (cv.rt == null) yield break;
            Color tint = SymbolTintById.TryGetValue(cv.lastSymId ?? "", out var c) ? c : Color.white;
            FxKit.I?.Play(FxId.SpinStop, cv.rt, tint);
        }

        private IEnumerator PostRevealFx(SpinResult result)
        {
            bool jackpot = !string.IsNullOrEmpty(result.jackpotSym);
            bool hasSet = !string.IsNullOrEmpty(result.bestSetId) && result.bestSetCount >= 3;

            // set3(및 그 상위인 set4/잭팟) 전부 해당 셀 글로우가 깔린다 — 잭팟은 사실상 전칸이라
            // bestSetId==jackpotSym(전 칸 동일 심볼)이므로 GlowMatchingCells 한 번으로 전칸이 켜진다.
            if (hasSet) GlowMatchingCells(result.bestSetId);

            if (jackpot)
            {
                FxKit.I?.Play(FxId.Jackpot, reelRow);
                yield return FlashAndShake(JackpotShakeAmplitude, JackpotShakeDuration, alpha: 0.18f);
                yield return JackpotBanner();
            }
            else if (result.bestSetCount >= 4)
            {
                yield return FlashAndShake(0f, 0f, alpha: Set4FlashAlpha);
            }

            if (result.skulls > 0) PlaySkullFx(result.cells);
        }

        private void GlowMatchingCells(string symId)
        {
            if (string.IsNullOrEmpty(symId)) return;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cv = _cells[i];
                if (cv.glow == null || cv.lastSymId != symId) continue;
                cv.glow.enabled = true;
                StartCoroutine(PulseOutline(cv.glow));
                FxKit.I?.Play(FxId.SetHit, cv.rt);
            }
        }

        private IEnumerator PulseOutline(Outline outline)
        {
            if (outline == null) yield break;
            var c = outline.effectColor;
            c.a = 0f;
            outline.effectColor = c;
            yield return UiTween.FloatRoutine(0f, 1f, SetGlowPulseDuration * 0.5f,
                a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
            if (outline == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0.7f, SetGlowPulseDuration * 0.5f,
                a => SetOutlineAlpha(outline, a), UiTween.Ease.OutQuad);
        }

        private static void SetOutlineAlpha(Outline outline, float a)
        {
            if (outline == null) return;
            var c = outline.effectColor;
            c.a = a;
            outline.effectColor = c;
        }

        private IEnumerator FlashAndShake(float shakeAmplitude, float shakeDuration, float alpha)
        {
            if (flashOverlay != null) StartCoroutine(FlashRoutine(alpha));
            if (shakeAmplitude > 0f && reelRow != null)
                yield return UiTween.ShakeRoutine(reelRow, shakeAmplitude, shakeDuration);
            else
                yield return new WaitForSeconds(Set4FlashDuration);
        }

        private IEnumerator FlashRoutine(float alpha)
        {
            yield return UiTween.FadeRoutine(flashOverlay, 0f, alpha, Set4FlashDuration * 0.5f);
            if (flashOverlay == null) yield break;
            yield return UiTween.FadeRoutine(flashOverlay, alpha, 0f, Set4FlashDuration * 0.5f);
        }

        private IEnumerator JackpotBanner()
        {
            if (jackpotBannerGroup == null) yield break;
            if (jackpotBannerRect != null) jackpotBannerRect.anchoredPosition = new Vector2(jackpotBannerRect.anchoredPosition.x, 60f);
            yield return UiTween.FadeRoutine(jackpotBannerGroup, 0f, 1f, JackpotBannerFade);
            if (jackpotBannerRect != null)
                yield return UiTween.MoveRoutine(jackpotBannerRect, jackpotBannerRect.anchoredPosition,
                    new Vector2(jackpotBannerRect.anchoredPosition.x, 0f), 0.2f, UiTween.Ease.OutBack);
            yield return new WaitForSeconds(JackpotBannerHold);
            yield return UiTween.FadeRoutine(jackpotBannerGroup, 1f, 0f, JackpotBannerFade);
        }

        private void PlaySkullFx(List<Cell> cells)
        {
            for (int i = 0; i < _cells.Count && i < cells.Count; i++)
            {
                if (cells[i]?.sym == null || cells[i].sym.id != "skull") continue;
                StartCoroutine(SkullCellFx(_cells[i]));
                FxKit.I?.Play(FxId.Skull, _cells[i].rt);
            }
        }

        private IEnumerator SkullCellFx(CellView cv)
        {
            if (cv?.rt == null) yield break;
            var baseColor = cv.icon != null ? cv.icon.color : Color.white;
            if (cv.icon != null) StartCoroutine(TintRoutine(cv.icon, baseColor, UiKit.Bad, SkullTintDuration));
            yield return UiTween.ShakeRoutine(cv.rt, SkullShakeAmplitude, SkullShakeDuration);
        }

        private IEnumerator TintRoutine(Image img, Color baseColor, Color tint, float duration)
        {
            yield return UiTween.FloatRoutine(0f, 1f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
            if (img == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
        }

        // ── 셀 구성 ──────────────────────────────────────────────────────────────────
        private void EnsureCellCount(int count)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                if (_cells[i].rt != null) Destroy(_cells[i].rt.gameObject);
            }
            _cells.Clear();

            if (reelRow == null || cellTemplate == null) return;
            for (int i = 0; i < count; i++)
            {
                var inst = Instantiate(cellTemplate, reelRow);
                inst.gameObject.SetActive(true);
                inst.name = "Cell_" + i;
                inst.localScale = Vector3.one;

                var cv = new CellView
                {
                    rt = inst,
                    icon = inst.Find("Icon")?.GetComponent<Image>(),
                    tag = inst.Find("Tag")?.GetComponent<Text>(),
                    glow = inst.GetComponent<Outline>(),
                };
                if (cv.glow != null) cv.glow.enabled = false;
                _cells.Add(cv);
            }
        }

        private void SetCellSymbol(CellView cv, SymInfo sym, string tag)
        {
            if (cv == null || sym == null) return;
            cv.lastSymId = sym.id;
            if (cv.icon != null)
            {
                cv.icon.sprite = _spriteById.TryGetValue(sym.id, out var sp) ? sp : null;
                cv.icon.color = Color.white;
                cv.icon.enabled = cv.icon.sprite != null;
            }
            if (cv.tag != null) cv.tag.text = TranslateTag(tag);
        }

        private static string TranslateTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "";
            return TagTranslate.TryGetValue(tag, out var safe) ? safe : tag;
        }

        private static SymInfo RandomSymbol()
        {
            var syms = Symbols.All;
            return syms[UnityEngine.Random.Range(0, syms.Length)];
        }

        private static Color HexColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
