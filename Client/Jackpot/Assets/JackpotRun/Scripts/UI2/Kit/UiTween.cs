using System;
using System.Collections;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 코루틴 기반 UI 트윈 헬퍼 — ENGINE_PORT_DESIGN.md S7 "파일 구성" 표의 Kit/UiTween.cs.
    // MonoBehaviour 없이는 코루틴을 돌릴 수 없으므로(정적 실행 불가) 모든 헬퍼는 "러너"로 쓸
    // MonoBehaviour(host)를 인자로 받는다. 이 프로젝트에서는 ScreenRouter가 씬에 항상 상주하는
    // 표준 러너다(화면이 꺼져도 라우터 자체는 활성 상태 유지 — AppRoot.cs 참조). 뷰 자신이 활성
    // 상태에서 자기 자신을 재생하는 연출(카드 펄스 등)은 그 뷰를 host로 써도 된다 — 다만 host가
    // 비활성화/파괴되면 Unity 기본 동작대로 진행 중이던 코루틴도 함께 멎는다(연출 중간에 화면이
    // 꺼지는 경우를 대비해 각 루틴은 대상이 null이 되면 해당 프레임에서 조용히 종료한다).
    //
    // ── S7b 계약(그대로 사용) ─────────────────────────────────────────────────────────
    // 각 연산은 두 형태로 제공된다:
    //   1) `XxxRoutine(...)` — IEnumerator. 이미 코루틴 안에 있다면 `yield return UiTween.FadeRoutine(...)`
    //      처럼 직접 합성(compose)해서 순차 연출을 만들 때 쓴다(예: ScreenRouter의 화면 전환 시퀀스).
    //   2) `Xxx(host, ...)` — Coroutine. 코루틴 밖(버튼 onClick 등)에서 "발사 후 잊기"로 쓸 때
    //      `host.StartCoroutine(...)`을 대신 호출해 준다. 반환된 Coroutine은 host.StopCoroutine(...)로
    //      개별 중단 가능.
    // duration<=0은 즉시 목표값으로 스냅(1프레임도 기다리지 않음) — 파라미터화된 연출이 0으로 꺼졌을 때
    // 방어적으로 동작하게 하기 위함.
    public static class UiTween
    {
        public enum Ease
        {
            Linear,
            OutCubic,
            OutQuad,
            OutBack,
        }

        // easing 함수 — t는 0..1 입력(클램프됨), 반환값은 OutBack 특성상 1을 살짝 넘거나(오버슈트) 잠깐
        // 음수가 될 수 있다(정상 동작 — Lerp가 아니라 LerpUnclamped로 적용해야 바운스가 살아난다).
        public static float Apply(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.OutCubic:
                {
                    float f = t - 1f;
                    return f * f * f + 1f;
                }
                case Ease.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float f = t - 1f;
                    return 1f + c3 * f * f * f + c1 * f * f;
                }
                default:
                    return t;
            }
        }

        // ── Float ── 임의 스칼라 보간(다른 헬퍼들의 기반이기도 함).
        public static IEnumerator FloatRoutine(float from, float to, float duration, Action<float> onUpdate,
            Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (onUpdate == null) yield break;
            if (duration <= 0f)
            {
                onUpdate(to);
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                onUpdate(Mathf.LerpUnclamped(from, to, Apply(ease, t / duration)));
                yield return null;
            }
            onUpdate(to);
            onComplete?.Invoke();
        }

        public static Coroutine Float(MonoBehaviour host, float from, float to, float duration,
            Action<float> onUpdate, Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(FloatRoutine(from, to, duration, onUpdate, ease, onComplete));
        }

        // ── Move ── RectTransform.anchoredPosition 보간.
        public static IEnumerator MoveRoutine(RectTransform rt, Vector2 from, Vector2 to, float duration,
            Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            if (rt == null) yield break;
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (rt == null) yield break;
                t += Time.deltaTime;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Apply(ease, t / duration));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
            onComplete?.Invoke();
        }

        public static Coroutine Move(MonoBehaviour host, RectTransform rt, Vector2 from, Vector2 to, float duration,
            Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(MoveRoutine(rt, from, to, duration, ease, onComplete));
        }

        // ── Scale ── Transform.localScale 보간(버튼 프레스, 카드 팝인 등).
        public static IEnumerator ScaleRoutine(Transform t, Vector3 from, Vector3 to, float duration,
            Ease ease = Ease.OutBack, Action onComplete = null)
        {
            if (t == null) yield break;
            if (duration <= 0f)
            {
                t.localScale = to;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.localScale = Vector3.LerpUnclamped(from, to, Apply(ease, elapsed / duration));
                yield return null;
            }
            if (t != null) t.localScale = to;
            onComplete?.Invoke();
        }

        public static Coroutine Scale(MonoBehaviour host, Transform t, Vector3 from, Vector3 to, float duration,
            Ease ease = Ease.OutBack, Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(ScaleRoutine(t, from, to, duration, ease, onComplete));
        }

        // ── Fade ── CanvasGroup.alpha 보간(화면 전환 0.18s, 토스트, 딤 등).
        public static IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float duration,
            Action onComplete = null)
        {
            if (cg == null) yield break;
            cg.alpha = from;
            if (duration <= 0f)
            {
                cg.alpha = to;
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (cg != null) cg.alpha = to;
            onComplete?.Invoke();
        }

        public static Coroutine Fade(MonoBehaviour host, CanvasGroup cg, float from, float to, float duration,
            Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(FadeRoutine(cg, from, to, duration, onComplete));
        }

        // ── Shake ── RectTransform.anchoredPosition을 현재 위치 기준으로 감쇠 진동시킨다(피해 연출 등).
        // 시작 시점의 anchoredPosition을 기준점으로 캡처하고, 종료 시 정확히 그 값으로 복원한다.
        public static IEnumerator ShakeRoutine(RectTransform rt, float amplitude, float duration,
            Action onComplete = null)
        {
            if (rt == null) yield break;
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null) yield break;
                t += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(t / duration);
                float ox = UnityEngine.Random.Range(-1f, 1f) * amplitude * damper;
                float oy = UnityEngine.Random.Range(-1f, 1f) * amplitude * damper;
                rt.anchoredPosition = basePos + new Vector2(ox, oy);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = basePos;
            onComplete?.Invoke();
        }

        public static Coroutine Shake(MonoBehaviour host, RectTransform rt, float amplitude, float duration,
            Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(ShakeRoutine(rt, amplitude, duration, onComplete));
        }

        // ── CountUp ── 정수(long) 카운트업(EXP/점수/코인 델타 표시). onUpdate는 매 프레임 반올림된
        // 중간값을 받는다 — 마지막 호출은 항상 정확히 to.
        public static IEnumerator CountUpRoutine(long from, long to, float duration, Action<long> onUpdate,
            Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            if (onUpdate == null) yield break;
            if (duration <= 0f)
            {
                onUpdate(to);
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                double v = from + (to - from) * (double)Apply(ease, t / duration);
                onUpdate((long)Math.Round(v));
                yield return null;
            }
            onUpdate(to);
            onComplete?.Invoke();
        }

        public static Coroutine CountUp(MonoBehaviour host, long from, long to, float duration,
            Action<long> onUpdate, Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            if (host == null) return null;
            return host.StartCoroutine(CountUpRoutine(from, to, duration, onUpdate, ease, onComplete));
        }
    }
}
