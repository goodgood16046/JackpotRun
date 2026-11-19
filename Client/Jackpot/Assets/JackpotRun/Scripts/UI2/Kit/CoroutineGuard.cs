using System;
using System.Collections;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 코루틴 예외 방어 — 재미 개편 2차 검수 M2. Unity 는 중첩 코루틴(yield return IEnumerator)이 던진 예외를 바깥 이터레이터로 되돌려 주지
    // 않는다(바깥의 try/finally 가 실행되지 않고 코루틴이 그냥 죽는다) — 그러면 PlayRoutine 의 `_busy`/입력 잠금이 영원히 안 풀리는
    // 소프트락이 된다. Run(inner) 는 inner(와 그 안에서 yield 되는 중첩 IEnumerator 전부)를 한 프레임씩 직접 구동하면서 예외를
    // Debug.LogException 으로 기록하고 조용히 끝낸다 — 호출부는 `yield return CoroutineGuard.Run(...)` 뒤에서 항상 이어서 실행된다.
    // 중첩이 아닌 yield 값(WaitForSeconds/Coroutine/null 등)은 그대로 Unity 에 넘긴다.
    public static class CoroutineGuard
    {
        public static IEnumerator Run(IEnumerator inner)
        {
            if (inner == null) yield break;
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }
                if (current is IEnumerator nested) yield return Run(nested);   // 중첩도 같은 방어를 씌워 따라 내려간다
                else yield return current;
            }
        }

        /// <summary>즉시 실행되는 연출/갱신 호출 하나를 예외로부터 격리한다(예외는 로그만 남기고 삼킨다).</summary>
        public static void Safe(Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
