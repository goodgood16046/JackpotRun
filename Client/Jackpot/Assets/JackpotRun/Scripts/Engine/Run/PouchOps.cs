using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19) — 심화모드 주머니 → 릴 칸 추출. 웹 engine.js
    // `pouchDrawOne`/`pouchDraw`/`rollFromPouch` 그대로 전사. 반환형이 SpinResolver.RollRaw와 동일한
    // List<Cell>이라 SpinResolver.Evaluate가 100% 그대로 재사용된다(웹 "reel → evaluate 100% 재사용"과
    // 동일한 설계) — 이 파일은 Cell을 만들 뿐 평가 로직을 전혀 갖지 않는다.
    //
    // bias(선택) — 웹 배치 B(심화 perk 가중치 편향, P7-2/3 범위)의 구조를 미리 받아두는 매개변수다.
    // 이번 슬라이스는 호출부(SpinResolver.ResolveSpin)가 항상 null을 넘겨(심화 퍽 자체가 아직 없음)
    // 실질적으로 기존 순수 count 비중과 100% 동일하게 동작한다 — PouchBias 타입과 분기 골격만 웹과
    // 동일하게 맞춰 둬서 P7-2/3가 이 함수 내부를 건드리지 않고 bias만 채워 넣을 수 있게 한다.
    public sealed class PouchBias
    {
        // symId → 가중치 배수(기본 1.0). count>0인 심볼에만 적용(count==0 심볼은 절대 주입 불가).
        public readonly Dictionary<string, double> Mul = new Dictionary<string, double>();
        // symId → 가중치 가산(기본 0). count>0인 심볼에만 적용.
        public readonly Dictionary<string, double> Add = new Dictionary<string, double>();
        // 희귀/전설(POUCH_RARITY "희귀"|"전설") 심볼에 추가로 곱하는 배수. null=미적용.
        public double? RareMul;

        public bool IsNoop => Mul.Count == 0 && Add.Count == 0 && RareMul == null;
    }

    public static class PouchOps
    {
        // id → 셀. "empty"=빈칸, "random"=랜덤칸(빈칸/랜덤칸 제외 실심볼 재추첨), 나머지=Symbols.ById —
        // 웹 `pouchDrawOne`. 웹 파리티 P7-2 blocker(§0, WEB_PARITY_DESIGN.md §2-(AA)) — internal로
        // 승격해 SpinResolver.CellsFromIds가 LockedNext의 "random" 센티널을 동일 로직으로 왕복 복원할
        // 수 있게 한다(중복 정의 방지).
        internal static Cell DrawOne(string id, Rng rng, IReadOnlyDictionary<string, int> pouch)
        {
            if (id == "empty") return new Cell(SpinResolver.EmptySym);
            if (id == "random")
            {
                var pool = new List<(string id, int n)>();
                int total = 0;
                // 웹 파리티 — Object.entries 삽입순 대신 Pouch.Symbols71 고정순으로 스캔(Content/Pouch.cs
                // Symbols71 각주와 동일한 결정론 근거).
                for (int i = 0; i < Pouch.Symbols71.Length; i++)
                {
                    var k = Pouch.Symbols71[i];
                    if (k == "random" || k == "empty") continue;
                    if (pouch.TryGetValue(k, out var n) && n > 0) { pool.Add((k, n)); total += n; }
                }
                if (pool.Count == 0) return new Cell(SpinResolver.EmptySym);
                double r = rng.NextDouble() * total;
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= pool[i].n;
                    if (r <= 0)
                    {
                        var info = Symbols.ById(pool[i].id);
                        return new Cell(info ?? SpinResolver.EmptySym, "🎲칸");
                    }
                }
                var fallbackInfo = Symbols.ById(pool[0].id);
                return new Cell(fallbackInfo ?? SpinResolver.EmptySym, "🎲칸");
            }
            var s = Symbols.ById(id);
            return new Cell(s ?? SpinResolver.EmptySym);
        }

        // 주머니 → reel 칸 추출(비중 가중랜덤). mods 미적용(순수 주머니 확률) — 총량 0/빈 주머니는
        // 전부 빈칸(방어). 웹 `pouchDraw`.
        //
        // 스캔 순서 — Pouch.Symbols71 고정순(Content/Pouch.cs 각주 참조, Object.entries 삽입순 대체).
        //
        // Opus 2차검수(P7-1, 2026-08-09) [LOW]⑤ — **미지 id 방어**: 이 스캔은 `pouch`(딕셔너리)의
        // 키를 직접 순회하지 않고 항상 `Pouch.Symbols71`(71개 고정 후보 목록)을 기준으로 순회한다 —
        // 즉 `pouch`에 `Pouch.Symbols71`에 없는 id가 어떤 경로로든 섞여 들어가도(예: 버그로 잘못된
        // id가 주입됨) 이 함수는 그 항목을 완전히 무시한다(가중치 스캔에 아예 후보로 오르지 않음 —
        // 아래 for문이 `Pouch.Symbols71`만 순회하므로 구조적으로 자명). `Pouch.Total(pouch)`/
        // `Pouch.Validate(pouch)`는 딕셔너리 값을 그대로 합산하므로, 미지 id가 섞이면 "덱 총량"과
        // "실제로 뽑힐 수 있는 총량"이 갈라질 수 있다는 뜻이다(그 미지 id 몫만큼 카운트는 잡히지만
        // 절대 안 뽑힘) — 이 슬라이스는 그런 id가 애초에 주머니에 들어갈 경로가 없어(RunController가
        // `Pouch.NewStartPouch()`로만 채움, P7-2/3 보상/정비소도 전부 `Pouch.Symbols71` 기반 id만
        // 다룰 예정) 실질 위험은 없지만, 향후 어떤 경로로든 미지 id가 섞여도 draw 확률에 조용히
        // 무영향(=안전한 무시)임을 여기 명시해 둔다.
        public static List<Cell> PouchDraw(Rng rng, IReadOnlyDictionary<string, int> pouch, int reel, PouchBias bias = null)
        {
            var pouchSafe = pouch ?? new Dictionary<string, int>();
            var baseEntries = new List<(string id, double w)>();
            double baseTotal = 0;
            for (int i = 0; i < Pouch.Symbols71.Length; i++)
            {
                var id = Pouch.Symbols71[i];
                if (pouchSafe.TryGetValue(id, out var n) && n > 0) { baseEntries.Add((id, n)); baseTotal += n; }
            }

            var entries = baseEntries;
            double total = baseTotal;

            if (bias != null && !bias.IsNoop)
            {
                var weighted = new List<(string id, double w)>(baseEntries.Count);
                double wTotal = 0;
                for (int i = 0; i < baseEntries.Count; i++)
                {
                    var (id, n) = baseEntries[i];
                    double mul = bias.Mul.TryGetValue(id, out var mv) ? mv : 1.0;
                    double add = bias.Add.TryGetValue(id, out var av) ? av : 0.0;
                    double w = n * mul + add;
                    var rar = Pouch.RarityOf(id);
                    if ((rar == "희귀" || rar == "전설") && bias.RareMul.HasValue) w *= bias.RareMul.Value;
                    w = System.Math.Max(0.0, w);
                    weighted.Add((id, w));
                    wTotal += w;
                }
                if (wTotal > 0) { entries = weighted; total = wTotal; }
                // wTotal==0 → base 폴백(드로우 불능 방지, entries/total은 이미 baseEntries/baseTotal).
            }

            var cells = new List<Cell>(reel);
            for (int c = 0; c < reel; c++)
            {
                if (total <= 0) { cells.Add(new Cell(SpinResolver.EmptySym)); continue; }
                double r = rng.NextDouble() * total;
                Cell picked = null;
                for (int i = 0; i < entries.Count; i++)
                {
                    r -= entries[i].w;
                    if (r <= 0) { picked = DrawOne(entries[i].id, rng, pouchSafe); break; }
                }
                cells.Add(picked ?? DrawOne(entries[0].id, rng, pouchSafe));
            }
            return cells;
        }
    }
}
