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
    // S13 §D — 릴 스핀 연출 재설계(핵심). 과거(S7c)엔 셀이 제자리에서 0.05s 간격으로 무작위 심볼을
    // 교체하다 마지막에 결과로 툭 바뀌는 "부자연스러운" 연출이었다. 지금은 셀마다 RectMask2D + 세로
    // 5칸 Strip(위2/중앙/아래2)을 두고, 스핀 중엔 스트립이 실제로 아래로 흘러가며 한 칸(cellHeight)을
    // 지날 때마다 맨 위 슬롯을 재활용해 새 무작위 심볼을 채우는 "무한 스크롤 릴"이다(빌더 구조는
    // Editor/UiSceneBuilder.cs BuildReelCellTemplate 참조: Reel_i(RectMask2D+w_reel+테두리 2개
    // Outline) → Strip → Slot0..Slot4, 각 Icon+Tag).
    //
    // ── 노치(1칸 스크롤) 파이프라인 지연에 대한 구현 노트 ─────────────────────────────
    // Strip은 5칸(위2/중앙(index2)/아래2)이 고정 로컬 위치에 머물고, 한 노치마다 Strip 전체가
    // cellHeight만큼 아래로 이동한 뒤 "맨 위(index0)에서 밀려난 것을 맨 아래로 재활용"하는 대신,
    // 반대로 "맨 아래로 밀려난 슬롯의 내용을 새 심볼로 갈아 끼워 맨 위 자리로 재활용"한다(시각적으로
    // 동일 — 값 재사용 방향만 다름). 그 결과 한 노치가 끝나면 중앙(index2)엔 "2노치 전에 주입한 값"이
    // 나타난다(5칸 중 위 2칸을 거쳐야 중앙에 닿으므로). 그래서 최종 목표 심볼(Y)은 감속 3노치 중
    // *첫 번째* 노치에서 주입해야 *세 번째*(마지막) 노치가 끝날 때 중앙에 나타난다 — 실제 화면에는
    // 항상 "느려지는 마지막 노치에서 목표가 착지"로 보이므로 설계 문구("① 목표 심볼을 중앙 도착
    // 예정 슬롯에 심고 ② 3칸 감속 ③ 도착 시 오버슈트")와 정확히 일치한다. 니어미스 후보(X)는 그
    // 직전(감속 시작 바로 앞의 마지막 "유지" 노치)에 주입해 두 번째 감속 노치가 끝날 때 중앙을
    // 스쳐 지나가게 한다.
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
        // ── S13 §D 스핀 타이밍(설계 명시값 그대로, 미명시 항목은 주석에 "설계 미명시" 표기) ──────────
        private const float AccelDuration = 0.25f;    // 설계 명시: "0→최고속 0.25s 가속"
        private const float MaxSpeedNotch = 0.06f;    // 설계 명시: "최고속(0.06s/칸)"
        private const float AccelStartNotch = 0.16f;  // 설계 미명시 — 가속 시작 시점 노치 길이 기본값
        private const float BaseSpinHold = 0.35f;     // 설계 미명시 — 스태거 시작 전 최소 유지시간 기본값
        private const float StaggerDelay = 0.10f;     // 설계 명시: "왼쪽부터 0.10s 스태거"
        private const float DecelDur1 = 0.10f;        // 설계 명시: "0.10→0.16→0.24s"
        private const float DecelDur2 = 0.16f;
        private const float DecelDur3 = 0.24f;
        private const float OvershootDistance = 8f;   // 설계 명시: "Y +8px"
        private const float OvershootDuration = 0.16f; // 설계 미명시 — OutBack 복귀 길이 기본값

        // ── 니어미스(설계 명시값) ──────────────────────────────────────────────────────────
        private const float NearMissPauseDuration = 0.35f; // 설계 명시: "살짝 느려짐(0.35s)"
        private const float NearMissFlashDuration = 0.12f; // 설계 명시: "0.12s 골드 플래시"
        private const float NearMissGrayFadeDuration = 0.3f; // 설계 명시: "0.3s간 회색 톤으로 페이드"
        private static readonly Color NearMissGray = new Color(0.55f, 0.55f, 0.58f);

        // ── 평소(정지) 상태 이웃 슬롯 스타일(설계 명시값) ──────────────────────────────────
        private const float RestNeighborAlpha = 0.45f;
        private const float RestNeighborScale = 0.92f;

        // ── 매치/잭팟/해골 FX(S7c 이관, 그대로 유지 — 설계 D "매치 시 기존 연출 유지") ────────────
        private const float SetGlowPulseDuration = 0.35f;
        private const float Set4FlashAlpha = 0.06f;
        private const float Set4FlashDuration = 0.12f;
        private const float JackpotShakeAmplitude = 6f;
        private const float JackpotShakeDuration = 0.3f;
        private const float JackpotBannerHold = 1.2f;
        private const float JackpotBannerFade = 0.2f;
        private const float SkullShakeAmplitude = 4f;
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
        [SerializeField] private RectTransform cellTemplate; // 자식 경로 계약: "Strip"→"Slot0".."Slot4"(각 "Icon"/"Tag") + Outline×2
        [SerializeField] private SymbolSprite[] symbolSprites = Array.Empty<SymbolSprite>();
        [SerializeField] private CanvasGroup flashOverlay; // 화면 전체를 덮는 흰색 플래시(set4/잭팟 공용)
        [SerializeField] private CanvasGroup jackpotBannerGroup;
        [SerializeField] private RectTransform jackpotBannerRect;

        private Dictionary<string, Sprite> _spriteById;

        private sealed class SlotView
        {
            public RectTransform rt;
            public Image icon;
            public Text tag;
        }

        private sealed class CellView
        {
            public RectTransform rt;      // Reel_i(RectMask2D 뷰포트)
            public RectTransform strip;   // Strip(세로 5칸)
            public SlotView[] slots;      // 고정 5개, index0(맨위)..index4(맨아래), index2=중앙
            public Outline glow;          // 매치 글로우(테두리 Outline과 별개 — GetComponents<Outline>()[1])
            public string lastSymId;      // 마지막으로 중앙에 착지한 심볼 id — 세트 글로우 매칭용

            public Image CenterIcon => slots != null && slots.Length > 2 ? slots[2].icon : null;
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

        /// <summary>스핀 결과 연출 전체(가속→유지→왼쪽부터 스태거 감속 정지→세트/잭팟/해골 FX)를
        /// 재생한다. onCellsRevealed는 마지막 셀이 멈춘 직후(후처리 FX 시작 전) 호출된다 — RunView가
        /// 이 시점에 HUD EXP 카운트업/코인·점수 플로팅 텍스트를 동시에 시작한다(설계 "정지 후 획득
        /// 라인 표시").</summary>
        public IEnumerator PlaySpinRoutine(SpinResult result, Action onCellsRevealed)
        {
            if (result == null || result.cells == null)
            {
                onCellsRevealed?.Invoke();
                yield break;
            }

            EnsureCellCount(result.cells.Count);

            DetectNearMiss(result, out bool isNearMiss, out SymInfo nearMissSym);

            // ── 릴별 정지 타이밍: 왼쪽부터 0.10s 스태거(설계 명시), 가속(0.25s)+최소 유지(0.35s,
            // 설계 미명시 기본값) 이후 순서대로 멈춘다. 니어미스는 "마지막 릴"에만 적용(설계 그대로).
            int lastIdx = result.cells.Count - 1;
            var routines = new Coroutine[_cells.Count];
            for (int i = 0; i < _cells.Count && i < result.cells.Count; i++)
            {
                float stopDelay = AccelDuration + BaseSpinHold + i * StaggerDelay;
                bool nearMissHere = isNearMiss && i == lastIdx;
                routines[i] = StartCoroutine(SpinOneReel(_cells[i], result.cells[i], stopDelay, nearMissHere, nearMissSym));
            }
            for (int i = 0; i < routines.Length; i++)
                if (routines[i] != null) yield return routines[i];

            onCellsRevealed?.Invoke();

            // ── 세트/잭팟/해골 FX ───────────────────────────────────────────────────────
            yield return PostRevealFx(result);
        }

        // SpinResolver.cs의 ValueIds(=Symbols.ValueIds: cherry/star/book/gem/crown)만 "세트"에
        // 기여한다 — skull/bomb/coin/flame/magnet/dice/seed/wild/key는 몇 개가 모여도 세트가 되지
        // 않는다(엔진 규칙, SpinResolver.cs ValueIds 사용처 전수). 니어미스 후보도 같은 집합으로
        // 제한해야 "완성될 수 없는 조합"을 아깝다고 연출하는 오류를 피한다.
        private static readonly HashSet<string> NearMissCandidateIds = BuildNearMissCandidateIds();
        private static HashSet<string> BuildNearMissCandidateIds()
        {
            var set = new HashSet<string>();
            foreach (var sym in Symbols.ValueIds)
            {
                var info = Symbols.BySym(sym);
                if (info != null) set.Add(info.id);
            }
            return set;
        }

        // ── 니어미스 판정(설계 D "니어미스" — 뷰 계층에서만 수행, 엔진 결과 불변) ──────────────────
        // 결과가 세트를 완성하지 못했는데(bestSetCount<3) 마지막 릴을 제외한 칸들 중 어떤 "세트
        // 가능" 심볼이 정확히 2번 등장했다면(한 칸만 더 있었으면 세트 완성) 그 심볼을 "니어미스
        // 후보"로 삼는다. cells 배열에서 최다 심볼 개수와 마지막 셀 이웃을 비교하는 설계 문구 그대로.
        private static void DetectNearMiss(SpinResult result, out bool isNearMiss, out SymInfo nearMissSym)
        {
            isNearMiss = false;
            nearMissSym = null;

            int lastIdx = result.cells.Count - 1;
            if (lastIdx < 1 || result.bestSetCount >= 3) return;

            var counts = new Dictionary<string, int>();
            for (int i = 0; i < lastIdx; i++)
            {
                var sym = result.cells[i]?.sym;
                if (sym == null || !NearMissCandidateIds.Contains(sym.id)) continue;
                counts.TryGetValue(sym.id, out var c);
                counts[sym.id] = c + 1;
            }

            string bestId = null;
            int bestCount = 0;
            foreach (var kv in counts)
                if (kv.Value > bestCount) { bestCount = kv.Value; bestId = kv.Key; }

            if (bestCount < 2 || bestId == null) return;
            string lastSymId = result.cells[lastIdx]?.sym?.id;
            if (bestId == lastSymId) return; // 이미 세트가 완성됐을 상황(전제와 모순) — 방어적 가드

            var candidate = Symbols.ById(bestId);
            if (candidate == null) return;

            isNearMiss = true;
            nearMissSym = candidate;
        }

        // ── 릴 1개 스핀 상태 기계 ────────────────────────────────────────────────────────
        private IEnumerator SpinOneReel(CellView cv, Cell targetCell, float stopDelay, bool isNearMiss, SymInfo nearMissSym)
        {
            if (cv?.strip == null || targetCell?.sym == null) yield break;

            // ── 가속(0.25s)+유지 — stopDelay 직전 노치까지 무작위 심볼로 순환 ───────────────────
            float t = 0f;
            while (true)
            {
                float notchDur = NotchDuration(t);
                if (t + notchDur >= stopDelay) break;
                yield return AdvanceNotch(cv, notchDur, RandomSymbol(), "", UiTween.Ease.Linear);
                t += notchDur;
            }

            // ── 유지 구간의 마지막 한 노치 — 니어미스면 여기서 X를 주입한다(2노치 뒤 중앙 통과).
            {
                float notchDur = NotchDuration(t);
                SymInfo injected = isNearMiss ? nearMissSym : RandomSymbol();
                yield return AdvanceNotch(cv, notchDur, injected, "", UiTween.Ease.Linear);
            }

            // ── 감속 3노치(0.10→0.16/0.35→0.24, OutCubic) — 첫 노치에서 목표 심볼(Y)을 주입하면
            // 세 번째(마지막) 노치가 끝날 때 중앙에 도착한다(파일 헤더 "노치 파이프라인" 주석 참조).
            yield return AdvanceNotch(cv, DecelDur1, targetCell.sym, targetCell.tag, UiTween.Ease.OutCubic);

            float dur2 = isNearMiss ? NearMissPauseDuration : DecelDur2;
            yield return AdvanceNotch(cv, dur2, RandomSymbol(), "", UiTween.Ease.OutCubic);
            if (isNearMiss) StartCoroutine(NearMissFlashRoutine(cv)); // 니어미스 심볼이 지금 중앙을 스쳐 지나간다.

            yield return AdvanceNotch(cv, DecelDur3, RandomSymbol(), "", UiTween.Ease.OutCubic);

            // ── 정지: 오버슈트 + fx_spin_stop + 이웃 슬롯 디밍 ─────────────────────────────
            cv.lastSymId = targetCell.sym.id;
            yield return OvershootRoutine(cv);
            ApplyRestDimming(cv);
            Color tint = SymbolTintById.TryGetValue(cv.lastSymId ?? "", out var symTint) ? symTint : Color.white;
            FxKit.I?.Play(FxId.SpinStop, cv.rt, tint);

            if (isNearMiss) StartCoroutine(TintRoutine(cv.CenterIcon, Color.white, NearMissGray, NearMissGrayFadeDuration));
        }

        // 가속 구간 노치 길이 — 0→AccelDuration 동안 AccelStartNotch에서 MaxSpeedNotch로 줄어들다가
        // (OutQuad로 "빠르게 붙었다 최고속에 안착"하는 느낌) 그 이후는 MaxSpeedNotch로 고정(=유지).
        private static float NotchDuration(float elapsedSinceSpinStart)
        {
            float speedT = Mathf.Clamp01(elapsedSinceSpinStart / AccelDuration);
            return Mathf.Lerp(AccelStartNotch, MaxSpeedNotch, UiTween.Apply(UiTween.Ease.OutQuad, speedT));
        }

        // 스트립을 cellHeight만큼 아래로 흘려보낸 뒤(무한 스크롤 시각 효과), 맨 아래로 밀려난 슬롯을
        // 새 심볼로 갈아 끼워 맨 위 자리로 재활용하고 스트립 위치를 원점으로 되돌린다(이음매 없음).
        private IEnumerator AdvanceNotch(CellView cv, float duration, SymInfo newSym, string newTag, UiTween.Ease ease)
        {
            if (cv?.strip == null) yield break;
            yield return UiTween.MoveRoutine(cv.strip, Vector2.zero, new Vector2(0f, -UiKit.ReelCellSize), duration, ease);
            if (cv.strip == null) yield break;

            for (int k = cv.slots.Length - 1; k >= 1; k--)
                CopySlot(cv.slots[k], cv.slots[k - 1]);
            SetSlotSymbol(cv.slots[0], newSym, newTag);
            cv.strip.anchoredPosition = Vector2.zero;
        }

        private IEnumerator OvershootRoutine(CellView cv)
        {
            if (cv?.strip == null) yield break;
            cv.strip.anchoredPosition = new Vector2(0f, OvershootDistance);
            yield return UiTween.MoveRoutine(cv.strip, cv.strip.anchoredPosition, Vector2.zero, OvershootDuration, UiTween.Ease.OutBack);
        }

        // 평소(정지) 상태 — 중앙만 결과 심볼(알파1/스케일1), 나머지 4칸(위2/아래2)은 이웃 심볼
        // 취급(알파.45/스케일.92)으로 "릴의 위아래가 보이는" 느낌을 낸다(설계 D "평소" 문단 그대로).
        private static void ApplyRestDimming(CellView cv)
        {
            if (cv?.slots == null) return;
            for (int k = 0; k < cv.slots.Length; k++)
            {
                var slot = cv.slots[k];
                if (slot?.icon == null) continue;
                bool isCenter = k == 2;
                var c = slot.icon.color;
                c.a = isCenter ? 1f : RestNeighborAlpha;
                slot.icon.color = c;
                slot.rt.localScale = Vector3.one * (isCenter ? 1f : RestNeighborScale);
            }
        }

        private IEnumerator NearMissFlashRoutine(CellView cv)
        {
            if (cv?.glow == null) yield break;
            bool wasEnabled = cv.glow.enabled;
            Color prevColor = cv.glow.effectColor;
            cv.glow.enabled = true;
            cv.glow.effectColor = UiKit.Accent;
            yield return UiTween.FloatRoutine(0f, 1f, NearMissFlashDuration * 0.5f, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
            if (cv.glow == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, NearMissFlashDuration * 0.5f, a => SetOutlineAlpha(cv.glow, a), UiTween.Ease.OutQuad);
            if (cv.glow == null) yield break;
            // 니어미스는 실제 매치가 아니므로 글로우를 원상복구한다(대개 disabled) — 실제 매치 처리는
            // 이 루틴이 끝난 뒤 PostRevealFx/GlowMatchingCells가 별도로 담당(둘이 겹칠 일은 없다 —
            // 니어미스는 bestSetCount<3일 때만 발생하므로).
            cv.glow.effectColor = prevColor;
            cv.glow.enabled = wasEnabled;
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
            var icon = cv.CenterIcon;
            var baseColor = icon != null ? icon.color : Color.white;
            if (icon != null) StartCoroutine(TintRoutine(icon, baseColor, UiKit.Bad, SkullTintDuration));
            yield return UiTween.ShakeRoutine(cv.rt, SkullShakeAmplitude, SkullShakeDuration);
        }

        private IEnumerator TintRoutine(Image img, Color baseColor, Color tint, float duration)
        {
            if (img == null) yield break;
            yield return UiTween.FloatRoutine(0f, 1f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
            if (img == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, duration * 0.5f, t => { if (img != null) img.color = Color.Lerp(baseColor, tint, t); }, UiTween.Ease.OutQuad);
        }

        // ── 셀 구성 ──────────────────────────────────────────────────────────────────
        private void EnsureCellCount(int count)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
                if (_cells[i].rt != null) Destroy(_cells[i].rt.gameObject);
            _cells.Clear();

            if (reelRow == null || cellTemplate == null) return;
            for (int i = 0; i < count; i++)
            {
                var inst = Instantiate(cellTemplate, reelRow);
                inst.gameObject.SetActive(true);
                inst.name = "Cell_" + i;
                inst.localScale = Vector3.one;

                var stripRt = inst.Find("Strip") as RectTransform;
                var slots = new SlotView[5];
                for (int k = 0; k < 5; k++)
                {
                    var slotRt = stripRt != null ? stripRt.Find("Slot" + k) as RectTransform : null;
                    var slot = new SlotView
                    {
                        rt = slotRt,
                        icon = slotRt != null ? slotRt.Find("Icon")?.GetComponent<Image>() : null,
                        tag = slotRt != null ? slotRt.Find("Tag")?.GetComponent<Text>() : null,
                    };
                    SetSlotSymbol(slot, RandomSymbol(), ""); // 첫 프레임 임시 채움(즉시 스크롤이 덮어씀)
                    slots[k] = slot;
                }

                // BuildReelCellTemplate이 Outline을 2개 순서대로 붙인다(0=상시 테두리, 1=매치 글로우).
                var outlines = inst.GetComponents<Outline>();
                var cv = new CellView
                {
                    rt = inst,
                    strip = stripRt,
                    slots = slots,
                    glow = outlines.Length > 1 ? outlines[1] : (outlines.Length > 0 ? outlines[0] : null),
                };
                if (cv.glow != null) cv.glow.enabled = false;
                _cells.Add(cv);
            }
        }

        private void SetSlotSymbol(SlotView slot, SymInfo sym, string tag)
        {
            if (slot == null || sym == null) return;
            if (slot.icon != null)
            {
                slot.icon.sprite = _spriteById.TryGetValue(sym.id, out var sp) ? sp : null;
                slot.icon.color = Color.white;
                slot.icon.enabled = slot.icon.sprite != null;
            }
            if (slot.rt != null) slot.rt.localScale = Vector3.one;
            if (slot.tag != null) slot.tag.text = TranslateTag(tag);
        }

        private static void CopySlot(SlotView dest, SlotView src)
        {
            if (dest == null || src == null) return;
            if (dest.icon != null && src.icon != null)
            {
                dest.icon.sprite = src.icon.sprite;
                dest.icon.enabled = src.icon.enabled;
                dest.icon.color = Color.white; // 스크롤 중엔 항상 정상 밝기(디밍은 정지 후에만 적용)
            }
            if (dest.rt != null) dest.rt.localScale = Vector3.one;
            if (dest.tag != null && src.tag != null) dest.tag.text = src.tag.text;
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
