using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 순수 C# 난수 래퍼 — 01_engine.md §10/§11 규칙을 지키며 System.Random 위에 얇게 얹는다.
    //
    // [충실도 목표] ENGINE_PORT_DESIGN.md 원칙 2: "RNG 비트스트림까지 Kotlin과 동일할 필요는 없다
    // (자체 시드 재현성만 보장)". kotlin.random.Random과 System.Random은 내부 알고리즘 자체가 다르므로
    // (01_engine.md §11-1) 같은 시드에서 같은 결과열이 나오도록 만들 수 없다 — 이 클래스는 "같은 seed를
    // 넣으면 이 C# 엔진 안에서는 항상 같은 결과가 나온다"만 보장한다.
    //
    // 반드시 유지해야 하는 것은 RNG *소비 순서 규칙*:
    //   - PickOrDefault: 빈 컬렉션이면 RNG를 전혀 소비하지 않고 default(T) 반환
    //     (Kotlin Iterable<T>.randomOrNull(rng) 규칙, §10.5 핵심 규칙 문단).
    //   - Pick: 빈 컬렉션이면 예외(Kotlin random(rng)와 동일 — 빈 컬렉션에서 randomOrNull 아닌 random 호출은 예외).
    //   - Shuffle: Kotlin MutableList<T>.shuffle(random) 알고리즘(Fisher-Yates, 뒤에서부터 스왑,
    //     lastIndex downTo 1, j = random.nextInt(i+1))을 그대로 재현 — §11-2.
    public sealed class Rng
    {
        private readonly Random _r;

        public Rng(long seed)
        {
            // 64비트 seed를 32비트 System.Random 시드로 폴딩(상위/하위 32비트를 모두 반영해 엔트로피 보존).
            int folded = unchecked((int)(seed ^ (seed >> 32)));
            _r = new Random(folded);
        }

        // Kotlin Random.nextInt(bound): [0, bound) 상한 배타적 — System.Random.Next(maxValue)와 동일 계약(§11-4).
        public int Next(int boundExclusive) => _r.Next(boundExclusive);

        // Kotlin Random.nextDouble(): [0,1) — System.Random.NextDouble()과 동일 계약(§11-4).
        public double NextDouble() => _r.NextDouble();

        // Kotlin Iterable<T>.random(rng): 빈 컬렉션이면 예외.
        public T Pick<T>(IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException("Rng.Pick: list is empty (Kotlin random(rng)는 빈 컬렉션에서 예외).");
            return list[_r.Next(list.Count)];
        }

        // Kotlin Iterable<T>.randomOrNull(rng): 빈 컬렉션이면 RNG 미소비 + default(T) 반환(§10.5 핵심 규칙).
        public T PickOrDefault<T>(IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[_r.Next(list.Count)];
        }

        // Kotlin MutableList<T>.shuffle(random) — Fisher-Yates, 뒤(lastIndex)에서 1까지 내려오며
        // j = random.nextInt(i+1) 위치와 교환. §11-2에 따라 스왑 방향·인덱스 범위를 원본과 동일하게 유지.
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _r.Next(i + 1);
                if (j == i) continue;
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // 가중치 기반 다중 추첨 — ENGINE_PORT_DESIGN.md 공유 Rng 계약에 명시된 메서드지만
        // SlotV2Engine.kt에는 이 정확한 시그니처(범용 T, count개 추첨)의 함수가 존재하지 않는다.
        // 가장 근접한 원본은 §10.1 weighted()(심볼 1개를 누적합 스캔으로 뽑는 알고리즘)이며,
        // rollRaw()가 이를 reel번 "복원추출"로 반복 호출한다(§10.2) — 같은 심볼이 여러 칸에 중복 등장 가능.
        // 이 메서드는 그 패턴을 일반화한 것: 매 회(count번) 독립적으로 누적합 스캔 1회(=NextDouble() 1회 소비)로
        // 뽑고, pool에서 제거하지 않는다(복원추출). total<=0 또는 스캔이 부동소수 오차로 끝까지 가면
        // weighted()의 SYMS[0] 폴백과 동일하게 pool[0]을 반환한다.
        // [설계 결손 — 보고 대상] ENGINE_PORT_DESIGN.md에 WeightedPick의 정확한 의미(복원/비복원,
        // RNG 소비량)가 명시되어 있지 않아 §10.1 weighted()의 단일 추첨 알고리즘을 그대로 일반화하는
        // 쪽으로 판단했다. S2 이후 실제 사용처가 생기면 Fable 확인 후 필요 시 재조정 요망.
        public IReadOnlyList<T> WeightedPick<T>(IReadOnlyList<(T item, double w)> pool, int count)
        {
            var result = new List<T>();
            if (pool == null || pool.Count == 0 || count <= 0) return result;

            for (int n = 0; n < count; n++)
            {
                double total = 0.0;
                for (int i = 0; i < pool.Count; i++) total += pool[i].w;

                double r = _r.NextDouble() * total; // RNG 호출 1회 (weighted()와 동일)
                T chosen = pool[0].item; // 폴백 (weighted()의 SYMS[0] 폴백과 동일한 구조적 필연)
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= pool[i].w;
                    if (r <= 0)
                    {
                        chosen = pool[i].item;
                        break;
                    }
                }
                result.Add(chosen);
            }
            return result;
        }
    }
}
