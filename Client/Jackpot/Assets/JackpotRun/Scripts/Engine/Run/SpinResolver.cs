using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 스핀 모드 — SlotV2Service.SPIN_CMDS 값("N"/"FOCUS"/"ALLIN"/"PRAY"/"LAST", 02_service.md §2 step1/§7-B).
    // typed action API로 대체(설계 원칙 5 — 카톡 텍스트 명령 파싱 이식 금지).
    public enum SpinMode
    {
        N,
        Focus,
        Allin,
        Pray,
        Last,
    }

    // 릴 1칸 — Kotlin data class Cell(sym, tag)(SlotV2Engine.kt L2056). tag는 표시 보조(와일드치환/복사/
    // 성장/제거 등) 정보일 뿐 수치 로직에는 영향 없다.
    public sealed class Cell
    {
        public readonly SymInfo sym;
        public readonly string tag;

        public Cell(SymInfo sym, string tag = "")
        {
            this.sym = sym;
            this.tag = tag;
        }
    }

    // 스핀 1회 평가 결과 — Kotlin data class SpinResult(SlotV2Engine.kt L2057-2074) 전사.
    // counts/bestSetId/jackpotSym은 Kotlin과 동일하게 심볼id(string) 기반으로 유지한다(Sym enum으로
    // 바꾸면 "empty" 센티널 셀이 자리표시용 Sym 값과 우연히 충돌할 위험이 있어 — 아래 Evaluate 주석 참조).
    public sealed class SpinResult
    {
        public List<Cell> cells;
        public long exp;
        public long score;
        public int coins;
        public Dictionary<string, int> counts;
        public Dictionary<string, int> tagCounts;
        public string bestSetId;   // null = 세트 없음
        public int bestSetCount;
        public int skulls;
        public bool flameNext;
        public bool seedNext;
        public string jackpotSym;  // null = 잭팟 없음
        public List<string> notes;
        public long preMul;  // 전역배수 적용 전 EXP(계산모드 표시용)
        public double mul;   // 적용된 전역 expMul
        public int flat;     // 가산 flatExp
    }

    // ResolveSpin() 한 번 호출(스핀 1회 전체 파이프라인) 결과. StageFlow가 이 값을 보고 클리어/실패를
    // 판정한다(02_service.md §2 step 26의 분기는 SpinResolver 책임 밖 — StageFlow.cs가 담당).
    public sealed class SpinOutcome
    {
        public bool rejected;      // true면 run 상태 변경/코인차감 전혀 없음(§2 step15 "거부" 규칙)
        public string rejectReason;

        public SpinMode mode;
        public SpinResult result;
        public long gained;        // 최종 이번 스핀 EXP(모드/보스/안전벨트/비상벨 전부 반영 후)
        public long newExp;        // run.stageExp + gained (클리어 판정 기준)
        public long newScore;
        public long newCoins;
        public int newSpinIndex;
        public long quota;
        public int spins;
        public bool destroyDevice;
        public bool badSpin;
        public bool prayMiracle;
        public int cmdCost;
        public readonly List<string> notes = new List<string>(); // res.notes + 모드/보스/안전장치 부가설명
    }

    // 스핀 1회의 정확한 순서 이식 — 02_service.md §2(핵심)·§10.1-10.4, Kotlin SlotV2Engine의
    // weighted/rollRaw/cellsFromIds/rollOne/applyCellOps/evaluate/spinsPerStage/cmdCoinCost와
    // SlotV2Service.handleSpin(L511-715, step 1-25까지 — step26의 클리어/실패 분기는 StageFlow.cs)를 전사.
    public static class SpinResolver
    {
        // ── 10.1 weighted() (Kotlin L2079-2091) ────────────────────────────────
        private static SymInfo Weighted(Rng rng, Mods mods)
        {
            var syms = Symbols.All;
            var w = new double[syms.Length];
            double total = 0.0;
            for (int i = 0; i < syms.Length; i++)
            {
                double x = syms[i].weight;
                if (syms[i].rare) x *= mods.rareWeightMul; // 희귀 = crown·wild만
                x *= mods.symbolWeightMul.TryGetValue(syms[i].sym, out var wm) ? wm : 1.0;
                x += mods.weightAdd.TryGetValue(syms[i].sym, out var wa) ? wa : 0.0;
                w[i] = x;
                total += x;
            }
            double r = rng.NextDouble() * total; // RNG 호출 1회
            for (int i = 0; i < syms.Length; i++)
            {
                r -= w[i];
                if (r <= 0) return syms[i];
            }
            return syms[0]; // 부동소수 오차 폴백
        }

        private static readonly string[] SeedGrowPool = { "book", "star", "crown" };

        // ── 10.2 rollRaw() (Kotlin L2095-2102) ─────────────────────────────────
        // 호출 순서 고정: [reel칸 weighted() 순차 호출] → (seedActive면) [성장심볼 선택 1회] → [칸 인덱스 1회].
        public static List<Cell> RollRaw(Rng rng, Mods mods, int reel, bool seedActive)
        {
            var cells = new List<Cell>(reel);
            for (int i = 0; i < reel; i++) cells.Add(new Cell(Weighted(rng, mods)));
            if (seedActive)
            {
                string grow = rng.Pick(SeedGrowPool);
                int idx = rng.Next(reel);
                cells[idx] = new Cell(Symbols.ById(grow), "🌱→");
            }
            return cells;
        }

        public static List<Cell> CellsFromIds(IReadOnlyList<string> ids)
        {
            var list = new List<Cell>();
            if (ids == null) return list;
            for (int i = 0; i < ids.Count; i++)
            {
                var info = Symbols.ById(ids[i]);
                if (info != null) list.Add(new Cell(info));
            }
            return list;
        }

        public static Cell RollOne(Rng rng, Mods mods) => new Cell(Weighted(rng, mods));

        // ── 셀 조작 (Kotlin applyCellOps L2114-2125) — NEXTSPIN 아이템의 "셀 치환" 레버, 평가 직전 적용.
        // eraser_old/eraser_fine은 동일 case(최저가치 1칸 제거), eraser_god은 2회 반복.
        // seal_tape/skull_sticker는 여기 관여 없음(ApplyItemMods 소관 — Kotlin 주석과 동일).
        public static void ApplyCellOps(List<Cell> cells, IReadOnlyList<string> armIds, Rng rng)
        {
            if (armIds == null) return;
            for (int i = 0; i < armIds.Count; i++)
            {
                switch (armIds[i])
                {
                    case "eraser_old":
                    case "eraser_fine":
                        RemoveLowestValue(cells);
                        break;
                    case "eraser_god":
                        RemoveLowestValue(cells);
                        RemoveLowestValue(cells);
                        break;
                    case "wild_temp":
                        cells[rng.Next(cells.Count)] = new Cell(Symbols.ById("wild"), "🌀");
                        break;
                    case "fake_crown":
                        ReplaceHighestWithCrown(cells);
                        break;
                }
            }
        }

        // Kotlin cellValue(c) = c.sym.exp + c.sym.score (L2112) — "가장 낮은/높은 칸" 판정 기준.
        private static long CellValue(Cell c) => c.sym.exp + c.sym.score;

        private static void RemoveLowestValue(List<Cell> cells)
        {
            int bestIdx = -1;
            long bestVal = long.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].sym.id == "empty") continue;
                long v = CellValue(cells[i]);
                if (v < bestVal) { bestVal = v; bestIdx = i; } // minByOrNull: 첫 최솟값 유지(엄격 <)
            }
            if (bestIdx >= 0) cells[bestIdx] = new Cell(EmptySym, "🧽");
        }

        private static void ReplaceHighestWithCrown(List<Cell> cells)
        {
            int bestIdx = -1;
            long bestVal = long.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].sym.id == "empty") continue;
                long v = CellValue(cells[i]);
                if (v > bestVal) { bestVal = v; bestIdx = i; } // maxByOrNull: 첫 최댓값 유지(엄격 >)
            }
            if (bestIdx >= 0) cells[bestIdx] = new Cell(Symbols.ById("crown"), "👑");
        }

        // Kotlin `private val EMPTY = Sym("empty", "▫", "빈칸")`(SlotV2Engine.kt L108) — Symbols.cs의
        // 14종 목록(All)엔 포함되지 않는 원본과 동일한 별도 센티널이라 여기서 직접 구성한다(Symbols.cs
        // 수정 금지). sym(Sym enum) 필드값은 empty에 대해 의미가 없다(아래 Evaluate 주석 참조) — 임의로
        // Sym.Cherry를 채우되, 모든 실사용 코드는 .id/.special/.tags만 보고 .sym은 참조하지 않는다.
        private static readonly SymInfo EmptySym = new SymInfo
        {
            sym = Sym.Cherry, id = "empty", emoji = "▫", name = "빈칸",
            exp = 0, score = 0, coin = 0, weight = 0, special = Sp.NONE, rare = false, dormant = true,
            tags = Array.Empty<string>(),
        };

        // VALUE_IDS(Kotlin L131) — Symbols.ValueIds(Sym[])에서 파생한 문자열 집합. Symbols.cs를 단일
        // 소스로 유지하되, evaluate() 로직 전체는 Kotlin과 동일하게 문자열 id 비교로 판정한다(Sym enum
        // 비교로 바꾸면 EmptySym의 임의 sym 필드값이 진짜 심볼과 우연히 일치해 오판정할 위험이 있다).
        private static readonly HashSet<string> ValueIds = BuildValueIds();
        private static HashSet<string> BuildValueIds()
        {
            var set = new HashSet<string>();
            var ids = Symbols.ValueIds;
            for (int i = 0; i < ids.Length; i++) set.Add(Symbols.BySym(ids[i]).id);
            return set;
        }
        // 동점(tie) 결정론 처리 — 01_engine.md §11-7: Kotlin `counts.maxByOrNull{}`은 HashMap 버킷 순서에
        // 의존해 재현 불가능하다. 스펙 문서가 권장하는 대로 "심볼 선언 순서를 우선순위로 쓰는 명시적
        // tie-break"로 대체한다(Symbols.ValueIds 선언 순서: cherry,star,book,gem,crown — Kotlin L131
        // setOf 리터럴 순서와 동일). [스펙-Kotlin 불일치 — 의도된 결정론화, 보고 대상]
        private static readonly string[] ValueIdsPriorityOrder = Symbols.ValueIds.Select(s => Symbols.BySym(s).id).ToArray();

        private static int PerSymExpBonus(Mods mods, string id)
        {
            var info = Symbols.ById(id);
            if (info == null) return 0; // "empty" 등 실제 콘텐츠에 없는 id → 항상 0(Kotlin map miss와 동치)
            return mods.perSymbolExp.TryGetValue(info.sym, out var v) ? v : 0;
        }

        private static int PerSymScoreBonus(Mods mods, string id)
        {
            var info = Symbols.ById(id);
            if (info == null) return 0;
            return mods.perSymbolScore.TryGetValue(info.sym, out var v) ? v : 0;
        }

        // ── evaluate() (Kotlin L2131-2356) — 원시 셀 → 폭탄/자석/세트/잭팟/위치/해골/신규16종/캡/전역배수.
        public static SpinResult Evaluate(
            Rng rng, IReadOnlyList<Cell> raw, Mods mods, int spinIndex, int spinsPerStage,
            bool flamePenalty, double capMul)
        {
            var notes = new List<string>();
            var cells = new List<Cell>(raw);
            int reel = Math.Max(cells.Count, 1);

            // 🌱 씨앗 성장 표기(첫 매치만)
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].tag == "🌱→") { notes.Add($"🌱 씨앗→{cells[i].sym.emoji}"); break; }
            }

            // 💣 폭탄 — 등장한 폭탄 개수만큼 각각 양옆 제거 → EXP 환산. 폭탄끼리는 안 지움, 중복 제거 방지.
            int bombExp = 0;
            var bombIdxs = new List<int>();
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.BOMB) bombIdxs.Add(i);
            if (bombIdxs.Count > 0)
            {
                var removedSet = new HashSet<int>();
                for (int bi = 0; bi < bombIdxs.Count; bi++)
                {
                    int b = bombIdxs[bi];
                    foreach (int j in new[] { b - 1, b + 1 })
                    {
                        if (j >= 0 && j < reel && cells[j].sym.special != Sp.BOMB && cells[j].sym.id != "empty" && !removedSet.Contains(j))
                        {
                            removedSet.Add(j);
                            cells[j] = new Cell(EmptySym, "💥");
                        }
                    }
                }
                int removed = removedSet.Count;
                bombExp = removed * Formulas.BOMB_EXP_PER;
                if (removed > 0) notes.Add($"💣{(bombIdxs.Count > 1 ? $"×{bombIdxs.Count} " : " ")}{removed}칸 제거 +{bombExp}");
            }

            // 🧲 자석 — 등장한 자석 개수만큼 각각 옆칸(왼쪽 우선→오른쪽) 실심볼 복사. 소스는 폭탄처리 후
            // 스냅샷(magSrc) 기준 — 자석↔자석 연쇄복사만 차단.
            var magIdxs = new List<int>();
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.MAGNET) magIdxs.Add(i);
            if (magIdxs.Count > 0)
            {
                var magSrc = new List<Cell>(cells);
                for (int mi2 = 0; mi2 < magIdxs.Count; mi2++)
                {
                    int mi = magIdxs[mi2];
                    Cell src = null;
                    foreach (int cand in new[] { mi - 1, mi + 1 })
                    {
                        if (cand < 0 || cand >= reel) continue;
                        var c = magSrc[cand];
                        if (c.sym.special == Sp.NONE && c.sym.id != "empty") { src = c; break; }
                    }
                    if (src != null)
                    {
                        cells[mi] = new Cell(src.sym, "🧲");
                        notes.Add($"🧲 {src.sym.emoji} 복사");
                    }
                }
            }

            // value 심볼 집계 (🌀 와일드는 최다 그룹에 합류)
            var counts = new Dictionary<string, int>();
            int wilds = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                var s = cells[i].sym;
                if (s.special == Sp.WILD) wilds++;
                else if (s.id != "empty" && ValueIds.Contains(s.id)) counts[s.id] = counts.TryGetValue(s.id, out var c) ? c + 1 : 1;
            }
            string bestId = null;
            int bestTmp = 0;
            for (int i = 0; i < ValueIdsPriorityOrder.Length; i++)
            {
                var sid = ValueIdsPriorityOrder[i];
                if (counts.TryGetValue(sid, out var cnt) && cnt > bestTmp) { bestTmp = cnt; bestId = sid; }
            }
            if (bestId != null && wilds > 0) counts[bestId] = counts[bestId] + wilds;
            else if (bestId == null && wilds > 0) { bestId = "cherry"; counts["cherry"] = wilds; }
            int bestCount = bestId != null && counts.TryGetValue(bestId, out var bc) ? bc : 0;

            // 기본 EXP/점수/코인 + 즉발 심볼효과 + 태그 집계
            double exp = 0.0, score = 0.0;
            int coins = 0;
            double expNoCenter = 0.0;
            double jackpotFixed = 0.0;
            int symCoinGain = 0;
            int keyCount = 0;
            var tagCounts = new Dictionary<string, int>();
            int skulls = 0;
            for (int idx = 0; idx < cells.Count; idx++)
            {
                var s = cells[idx].sym;
                double cellExp = s.exp + PerSymExpBonus(mods, s.id);
                if (s.tags != null)
                {
                    for (int ti = 0; ti < s.tags.Length; ti++)
                    {
                        var tag = s.tags[ti];
                        tagCounts[tag] = tagCounts.TryGetValue(tag, out var tc) ? tc + 1 : 1;
                        cellExp += mods.tagExpBonus.TryGetValue(tag, out var teb) ? teb : 0;
                    }
                }
                expNoCenter += cellExp;
                if (idx == reel / 2) cellExp *= mods.centerExpMul; // 가운데 칸 강화
                exp += cellExp;
                score += s.score + PerSymScoreBonus(mods, s.id);
                coins += (int)s.coin;
                switch (s.special)
                {
                    case Sp.DICE:
                        int d = 1 + rng.Next(12); // Kotlin nextInt(1,13) == [1,12]
                        exp += d; expNoCenter += d; notes.Add($"🎲 +{d}");
                        break;
                    case Sp.SKULL:
                        skulls++;
                        double se = mods.skullExp + mods.perSkullExp;
                        exp += se; expNoCenter += se; score += mods.skullScoreBonus;
                        break;
                    case Sp.COIN:
                        symCoinGain += (int)s.coin;
                        break;
                    case Sp.KEY:
                        keyCount++;
                        break;
                }
            }
            exp += bombExp; expNoCenter += bombExp;

            if (keyCount > 0)
            {
                int keyCoins = keyCount * Formulas.KEY_COIN_PER;
                coins += keyCoins;
                notes.Add($"🗝 보물 +{keyCoins}🪙");
            }
            if (symCoinGain > 0) notes.Add($"🪙 +{symCoinGain}🪙");
            bool anySeed = false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.SEED) { anySeed = true; break; }
            if (anySeed) notes.Add("🌱 다음 성장↑");

            // 세트 보너스
            if (bestId != null && bestCount >= 2)
            {
                int n = Math.Min(bestCount, Symbols.SetExp.Length - 1);
                double twoMul = bestCount == 2 ? mods.twoSetBonusMul : 1.0;
                double add = Symbols.SetExp[n] * mods.setExpMul * twoMul;
                exp += add; expNoCenter += add; score += Symbols.SetScore[n];
                notes.Add($"{Symbols.ById(bestId).emoji}×{bestCount} 세트 +{(int)add}");
                if (twoMul != 1.0) notes.Add($"👯짝맞춤 +{(int)((twoMul - 1.0) * 100)}%");
            }

            // 🎰 잭팟 — 전 칸 동일(와일드 포함) 심볼
            string jackpotSym = null;
            if (bestId != null && bestCount >= reel && reel >= 5)
            {
                jackpotSym = bestId;
                int jb = bestId switch
                {
                    "cherry" => 120, "book" => 320, "star" => 360, "gem" => 160, "crown" => 520, _ => 200,
                };
                jackpotFixed += jb; score += jb * 5;
                notes.Add($"🎰{Symbols.ById(bestId).emoji}×{bestCount} 잭팟! +{jb}EXP·+{jb * 5}점");
            }

            // 인접 판정
            if (mods.adjacentSameExp != 0)
            {
                int pairs = 0;
                for (int i = 0; i < reel - 1; i++)
                {
                    var a = cells[i].sym; var b = cells[i + 1].sym;
                    if (a.id == b.id && ValueIds.Contains(a.id)) pairs++;
                }
                if (pairs > 0)
                {
                    exp += pairs * mods.adjacentSameExp; expNoCenter += pairs * mods.adjacentSameExp;
                    notes.Add($"🔗 인접 {pairs}쌍 +{pairs * mods.adjacentSameExp}");
                }
            }

            // 위치 판정 — 양끝 동일
            if (mods.endsMatchExpMul != 1.0 && reel >= 2)
            {
                var a = cells[0].sym; var b = cells[reel - 1].sym;
                if (a.id == b.id && ValueIds.Contains(a.id))
                {
                    exp *= mods.endsMatchExpMul;
                    notes.Add($"↔ 양끝 {a.emoji} EXP ×{mods.endsMatchExpMul}");
                }
            }

            // ☠ 해골 페널티/보너스
            if (skulls > 0)
            {
                double skullBonusPer = mods.skullExp + mods.perSkullExp;
                if (skullBonusPer > 0)
                {
                    notes.Add($"☠ {skulls}개 +{(int)(skullBonusPer * skulls)} (해골빌드)");
                }
                else
                {
                    double pen = skulls * Formulas.SKULL_PENALTY * mods.skullPenaltyMul;
                    exp -= pen; expNoCenter -= pen;
                    if (pen > 0) notes.Add($"☠ {skulls}개 -{(int)pen}");
                }
            }

            // (C2) capBase — 위치/불꽃/전역배수/center 적용 전 가산 baseline. 총배율 캡 비교 기준.
            double capBase = Math.Max(expNoCenter, 0.0);

            // 🔥 불꽃
            bool anyFlame = false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.FLAME) { anyFlame = true; break; }
            if (anyFlame) { exp *= 1.5; notes.Add("🔥 EXP +50%"); }
            if (flamePenalty) { exp *= 0.5; notes.Add("🔥 여파 EXP -50%"); }

            // 첫/막 스핀 배수
            if (spinIndex == 0) exp *= mods.firstSpinExpMul;
            if (spinIndex == spinsPerStage - 1) exp *= mods.lastSpinExpMul;

            // 신규 16종 per-spin 조건부 배수 (capBase 이후 · 전역배수 이전 → 총배율 캡 대상)
            int rareN = 0;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.rare) rareN++;
            if (rareN >= 2 && mods.rareBurstExpMul != 1.0)
            {
                exp *= mods.rareBurstExpMul;
                notes.Add($"💫운명폭발 EXP ×{FmtMul(mods.rareBurstExpMul)}");
            }
            if (bestCount >= 3 && mods.set3ExpMul != 1.0)
            {
                exp *= mods.set3ExpMul;
                notes.Add($"🧩퍼즐 세트{bestCount} EXP ×{FmtMul(mods.set3ExpMul)}");
            }
            if (mods.perfectShapeExpMul != 1.0 && reel >= 3)
            {
                var a = cells[0].sym; var b = cells[reel - 1].sym; var c = cells[reel / 2].sym;
                bool endsWild = a.special == Sp.WILD || b.special == Sp.WILD;
                bool endsSame = (a.id == b.id && ValueIds.Contains(a.id)) ||
                                (endsWild && (ValueIds.Contains(a.id) || ValueIds.Contains(b.id)));
                string endId = ValueIds.Contains(a.id) ? a.id : (ValueIds.Contains(b.id) ? b.id : null);
                bool centerOk = endId != null && (c.id == endId || c.special == Sp.WILD);
                if (endsSame && centerOk)
                {
                    bool withWild = endsWild || c.special == Sp.WILD;
                    double pm = withWild ? 1.7 : mods.perfectShapeExpMul;
                    exp *= pm;
                    notes.Add($"💠완벽한모양 EXP ×{FmtMul(pm)}");
                }
            }

            // 전역 배수 + 고정(잭팟은 아직 미포함)
            long preMulExp = Math.Max((long)exp, 0);
            exp = exp * mods.expMul + mods.flatExp;

            // (C2) 총배율 캡 — center/ends/flame/first·last/rareBurst/set3/perfectShape/global 곱을 합친
            // 최종배율(=exp-flatExp)을 capBase 대비 capMul로 클램프. 잭팟 고정가산은 캡 예외(곱 밖).
            if (capMul > 0.0 && capBase > 0.0)
            {
                double variable = exp - mods.flatExp;
                double ceiling = capBase * capMul;
                if (variable > ceiling)
                {
                    exp = ceiling + mods.flatExp;
                    notes.Add($"🧯총배율 캡 ×{FmtMul1(capMul)}");
                }
            }
            exp += jackpotFixed;

            // 신규 16종 per-spin 점수 배수
            if (rareN >= 2 && mods.rareBurstScoreMul != 1.0) score *= mods.rareBurstScoreMul;
            if (bestCount >= 4 && mods.set4ScoreMul != 1.0) score *= mods.set4ScoreMul;
            if (skulls >= 3 && mods.skull3ScoreMul != 1.0)
            {
                score *= mods.skull3ScoreMul;
                notes.Add($"👁️해골관찰 ☠{skulls} 점수 ×{FmtMul(mods.skull3ScoreMul)}");
            }
            score = score * mods.scoreMul + mods.flatScore;
            coins = (int)(coins * mods.coinMul) + Formulas.COIN_BASE;

            return new SpinResult
            {
                cells = cells,
                exp = Math.Max((long)exp, 0),
                score = Math.Max((long)score, 0),
                coins = coins,
                counts = counts,
                tagCounts = tagCounts,
                bestSetId = bestId,
                bestSetCount = bestCount,
                skulls = skulls,
                flameNext = false,
                seedNext = anySeed,
                jackpotSym = jackpotSym,
                notes = notes,
                preMul = preMulExp,
                mul = mods.expMul,
                flat = mods.flatExp,
            };
        }

        // 배율 표기 — 소수 2자리·끝0제거(Kotlin fmtMul, L2077).
        private static string FmtMul(double v)
        {
            string s = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
        // capMul 표기는 Kotlin이 "%.1f"로 소수 1자리 포맷 후 끝0/점 제거(L2326).
        private static string FmtMul1(double v)
        {
            string s = v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }

        // ── spinsPerStage / effSpins / qOf / cmdCoinCost ────────────────────────
        public static int SpinsPerStage(Mods mods) => ModsBuilder.SpinsPerStage(mods);

        public static int EffSpins(RunState run, Mods mods) =>
            Math.Max(SpinsPerStage(mods) + run.StageBonusSpins + Bosses.Spins(run.Stage), Formulas.MIN_SPINS);

        public static long QuotaOf(int stage, Mods mods)
        {
            int baseSpins = SpinsPerStage(mods);
            int bsp = Bosses.Spins(stage);
            double prop = (bsp > 0 && baseSpins > 0) ? (double)(baseSpins + bsp) / baseSpins : 1.0;
            return (long)(Formulas.Quota(stage) * mods.quotaMul * Bosses.QuotaMulFor(stage) * prop);
        }

        public static int CmdCoinCost(SpinMode mode, bool boss) => ModsBuilder.CmdCoinCost(mode, boss);

        // ── applyBoss (Kotlin L92-106) — 정수 나눗셈(내림)을 그대로 유지 ──
        public static (long gained, string note) ApplyBoss(
            Boss boss, long gained, SpinResult res, int spinIndex, int spins,
            double expectedPerSpin, int augCount)
        {
            switch (boss.id)
            {
                case "finals":
                    if (spinIndex == spins - 1) return (gained * 2, " · 📝기말 막스핀×2");
                    if (spinIndex == 0) return (gained * 9 / 10, " · 📝기말 첫스핀-10%");
                    return (gained, "");
                case "strict":
                    return res.bestSetCount < 3 ? (gained / 2, " · 👨‍🏫콤보없음 ×0.5") : (gained, "");
                case "luck":
                {
                    bool hasRare = false;
                    for (int i = 0; i < res.cells.Count; i++)
                    {
                        var id = res.cells[i].sym.id;
                        if (id == "star" || id == "crown" || id == "wild") { hasRare = true; break; }
                    }
                    return hasRare ? (gained * 18 / 10, " · 🎲희귀 ×1.8") : (gained * 8 / 10, " · 🎲노희귀 ×0.8");
                }
                case "grad":
                {
                    double pace = expectedPerSpin * 0.7;
                    if (expectedPerSpin > 0.0 && gained < pace)
                    {
                        return augCount < 3
                            ? (gained * 75 / 100, " · 🎓빈약빌드 ×0.75")
                            : (gained * 85 / 100, " · 🎓꾸준함부족 ×0.85");
                    }
                    return (gained, "");
                }
                default:
                    return (gained, "");
            }
        }

        // ── RunCtx 구성 헬퍼 (Kotlin runCtxOf, L71-78) ──
        private static RunCtx RunCtxOf(RunState run, int spinIndex, int spinsPerStage, long quota) => new RunCtx
        {
            stage = run.Stage, spinIndex = spinIndex, spinsPerStage = spinsPerStage,
            stageExp = run.StageExp, quota = quota,
            growthStack = run.GrowthStack, snowStack = run.SnowStack,
            curseCount = run.Curses.Count, unluckyGauge = run.UnluckyGauge,
            boss = Bosses.For(run.Stage) != null,
        };

        private static string CmdMarker(SpinMode mode) => mode switch
        {
            SpinMode.Focus => "FOCUS", SpinMode.Allin => "ALLIN", SpinMode.Pray => "PRAY", SpinMode.Last => "LAST",
            _ => "N",
        };

        // ══════════════════════════════════════════════════════════════════════
        // ResolveSpin — 02_service.md §2 step 1~25 전사(step26 클리어/실패 분기는 StageFlow가 담당).
        // run을 직접 변형한다(Kotlin의 run.copy() 불변 갱신을 인메모리 가변 상태로 재설계 — 설계 원칙과
        // 일치, RunState는 엔진 내부 가변 상태다). 거부(rejected) 시에는 아무 것도 변형하지 않는다.
        // ══════════════════════════════════════════════════════════════════════
        public static SpinOutcome ResolveSpin(RunState run, SpinMode mode)
        {
            var arm = run.ArmItems;
            var phase = run.PhaseItems;
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var curses = run.Curses;

            // step 3-8: 3단계 mods 재계산(ctx조건부 증강 평가용) — 02_service.md §10-2 주의사항 그대로.
            var preMods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device);
            var preCtx = RunCtxOf(run, run.SpinIndex, SpinsPerStage(preMods0), QuotaOf(run.Stage, preMods0));
            var preMods = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device, preCtx);
            int preEffSpins = EffSpins(run, preMods);
            var runCtx = RunCtxOf(run, run.SpinIndex, preEffSpins, QuotaOf(run.Stage, preMods));
            var baseMods = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device, runCtx);
            if (mode == SpinMode.Focus) baseMods.rareWeightMul *= 0.5; // 안정화: 고점 억제

            var mods = ModsBuilder.ApplyItemMods(baseMods, Concat(arm, phase));
            var devEq = Devices.ById(run.Device);
            if (devEq != null && devEq.kind == "PASSIVE") mods = ModsBuilder.ApplyPassiveDevice(mods, devEq.id);

            // (C2) 배율 상한 — hasPrism은 영구 perks만(phasePerks 미반영). [원본 버그 유지] 02_service.md §2-12.
            bool hasPrism = false;
            for (int i = 0; i < run.Perks.Count; i++)
            {
                var p = Perks.ById(run.Perks[i]);
                if (p != null && p.tier == Tier.PRISM) { hasPrism = true; break; }
            }
            double capMul = Formulas.CapMulFor(run.Stage, hasPrism);
            if (mods.expMul > capMul) mods.expMul = capMul;
            if (mods.lastSpinExpMul > 5.0) mods.lastSpinExpMul = 5.0;

            int spins = EffSpins(run, mods);
            long quota = QuotaOf(run.Stage, mods);

            bool bossStage = Bosses.For(run.Stage) != null;
            int cmdCost = CmdCoinCost(mode, bossStage);
            if (mode != SpinMode.N)
            {
                if (mode == SpinMode.Last && run.SpinIndex != spins - 1)
                    return Rejected(mode, "LAST_NOT_FINAL_SPIN");
                if (run.UsedCmds.Contains(CmdMarker(mode)))
                    return Rejected(mode, "MODE_ALREADY_USED");
                if (run.Coins < cmdCost)
                    return Rejected(mode, "INSUFFICIENT_COINS");
            }

            int reel = (devEq != null && devEq.id == "dev_subreel") ? Formulas.REEL + 1 : Formulas.REEL;
            List<Cell> raw = run.LockedNext.Count > 0
                ? CellsFromIds(run.LockedNext)
                : RollRaw(run.Rng, mods, reel, run.SeedNext);
            ApplyCellOps(raw, arm, run.Rng);
            var rawIds = raw.Select(c => c.sym.id).ToList();

            var res = Evaluate(run.Rng, raw, mods, run.SpinIndex, spins, run.FlameNext, capMul);
            long gained = res.exp;
            var outcomeNotes = new List<string>(res.notes);
            bool prayMiracle = false;

            switch (mode)
            {
                case SpinMode.Focus:
                {
                    long floor = (long)(quota / (double)spins * 0.6);
                    if (gained < floor) { gained = floor; outcomeNotes.Add("🎯집중 — 최소 보장"); }
                    else outcomeNotes.Add("🎯집중(안정)");
                    break;
                }
                case SpinMode.Allin:
                    if (res.skulls >= 2) { gained = 0; outcomeNotes.Add("🎲올인 실패! EXP 0"); }
                    else { gained *= 2; outcomeNotes.Add("🎲올인 성공! EXP ×2"); }
                    break;
                case SpinMode.Pray:
                {
                    long low = (long)(quota / (double)spins * 0.5);
                    if (run.Rng.Next(100) < 8) { gained *= 3; outcomeNotes.Add("🙏✨ 기적! EXP ×3"); prayMiracle = true; }
                    else if (gained < low) { gained += 25; outcomeNotes.Add("🙏기도 — 불운보정 +25"); }
                    else outcomeNotes.Add("🙏기도");
                    break;
                }
                case SpinMode.Last:
                    gained = (long)(gained * 1.75);
                    outcomeNotes.Add("⏰최후 EXP ×1.75");
                    break;
            }

            if (run.PendingNextExpMul != 1.0)
            {
                gained = (long)(gained * run.PendingNextExpMul);
                outcomeNotes.Add($"🪙다음스핀 ×{FmtMul(run.PendingNextExpMul)}");
            }

            var boss = Bosses.For(run.Stage);
            if (boss != null)
            {
                double expPerSpin = spins > 0 ? quota / (double)spins : 0.0;
                var (g2, bn) = ApplyBoss(boss, gained, res, run.SpinIndex, spins, expPerSpin, run.Perks.Count);
                gained = g2;
                if (!string.IsNullOrEmpty(bn)) outcomeNotes.Add(bn.TrimStart(' ', '·'));
            }

            if (devEq != null && devEq.id == "dev_safe")
            {
                long fl = (long)(quota / (double)spins * 0.35);
                if (gained < fl) { gained = fl; outcomeNotes.Add("🦺안전벨트 최소 보장"); }
            }

            bool destroyDevice = false;
            if (arm.Contains("dev_bell"))
            {
                gained = Math.Max(gained, Math.Max(quota - run.StageExp, 0) + 1);
                destroyDevice = true;
                outcomeNotes.Add("🔔비상졸업벨 발동! 즉시 클리어");
            }

            double expected = spins > 0 ? quota / (double)spins : 0.0;
            bool badSpin = !destroyDevice && (gained <= expected * 0.4 || res.skulls >= 3);
            int newGauge = badSpin ? Math.Min(run.UnluckyGauge + 1, Formulas.UNLUCKY_MAX) : run.UnluckyGauge;
            if (badSpin && newGauge > run.UnluckyGauge)
                outcomeNotes.Add(newGauge >= Formulas.UNLUCKY_MAX
                    ? "🍀불운 가득! 다음 보상 희귀↑ 보장"
                    : $"🍀불운 {newGauge}/{Formulas.UNLUCKY_MAX}");   // Kotlin L598-599

            int adjPairsFired = (mods.adjacentSameExp != 0 && AdjPairCount(res.cells) > 0) ? 1 : 0;
            int set4Fired = res.bestSetCount >= 4 ? 1 : 0;
            int prevSpinIndex = run.SpinIndex;
            // 🔥 한 방 신기록 — 이번 런 최고 EXP 스핀 갱신 시 연출 (Kotlin L605-607)
            if (gained > run.RunBestSpin && gained >= quota / 2 && run.RunBestSpin > 0)
                outcomeNotes.Add("🔥 이번 런 최고의 한 방!");
            long newExp = run.StageExp + gained;
            long newScore = run.Score + res.score;
            long newCoins = run.Coins + res.coins - cmdCost;
            int newIdx = run.SpinIndex + 1;

            // ── run 상태 반영 (Kotlin run.copy(...), L627-643) ──
            run.StageExp = newExp;
            run.Score = newScore;
            run.Coins = newCoins;
            run.SpinIndex = newIdx;
            run.FlameNext = res.flameNext;
            run.SeedNext = res.seedNext;
            run.ArmItems.Clear();
            if (mode != SpinMode.N) run.UsedCmds.Add(CmdMarker(mode));
            if (destroyDevice) run.Device = "";
            run.LastCells.Clear(); run.LastCells.AddRange(rawIds);
            run.LastGain = gained;
            run.LastScoreGain = res.score;
            run.LastCoinGain = res.coins;
            run.LastSet4 = set4Fired;
            run.LastAdjPairs = adjPairsFired;
            run.LastSpinNo = prevSpinIndex;
            run.PendingNextExpMul = 1.0;
            run.LockedNext.Clear();
            run.RunJackpots += res.jackpotSym != null ? 1 : 0;
            run.RunBestSpin = Math.Max(run.RunBestSpin, gained);
            BumpSymCounts(run.RunSymCounts, res.cells);
            run.UnluckyGauge = newGauge;
            run.RunAdjPairs += adjPairsFired;
            run.RunSet4 += set4Fired;
            run.RunPrayWins += (mode == SpinMode.Pray && (prayMiracle || newExp >= quota)) ? 1 : 0;
            run.RunUsedCmd = run.RunUsedCmd || mode != SpinMode.N;

            return new SpinOutcome
            {
                rejected = false,
                mode = mode,
                result = res,
                gained = gained,
                newExp = newExp,
                newScore = newScore,
                newCoins = newCoins,
                newSpinIndex = newIdx,
                quota = quota,
                spins = spins,
                destroyDevice = destroyDevice,
                badSpin = badSpin,
                prayMiracle = prayMiracle,
                cmdCost = cmdCost,
            }.WithNotes(outcomeNotes);
        }

        // 주의: 거부 outcome은 result==null, quota/spins/gained==0 — 호출측(S4 포함)은 rejected 확인 후 다른 필드에 접근 금지.
        private static SpinOutcome Rejected(SpinMode mode, string reason) => new SpinOutcome
        {
            rejected = true, rejectReason = reason, mode = mode,
        };

        // StageFlow 등 외부에서 스핀 자체를 시작하지 못할 때 쓰는 거부 outcome (동일 주의사항 적용).
        public static SpinOutcome RejectedOutcome(string reason) => new SpinOutcome
        {
            rejected = true, rejectReason = reason, mode = SpinMode.N,
        };

        private static List<string> Concat(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var list = new List<string>(a);
            list.AddRange(b);
            return list;
        }

        private static int AdjPairCount(IReadOnlyList<Cell> cells)
        {
            int p = 0;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                var a = cells[i].sym; var b = cells[i + 1].sym;
                if (a.id == b.id && ValueIds.Contains(a.id)) p++;
            }
            return p;
        }

        // Kotlin bumpSymCounts(csv, cells)(SlotV2Service.kt L753-758) — "empty" 제외, id별 누적.
        private static void BumpSymCounts(Dictionary<string, int> counts, IReadOnlyList<Cell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var id = cells[i].sym.id;
                if (id == "empty") continue;
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // S4 훅 인터페이스 — MANIP 장치(dev_reroll/dev_pin/dev_copy/dev_swap)·도박꾼 무료재굴림·POST_SPIN
        // 만회는 "직전 스핀 1개를 조작해 재계산"하는 완전히 별도의 커맨드 플로우다(02_service.md §7-C·
        // §10-7 net-adjust 패턴). 이 슬라이스(S3)는 정의만 내려주고 구현하지 않는다 — 필요한 재료
        // (run.LastCells/LastGain/LastScoreGain/LastCoinGain/LastSet4/LastAdjPairs, RollOne, CellsFromIds,
        // Evaluate)는 전부 이 파일에 이미 공개돼 있다. S4(DeviceActions.cs)가 구현해야 할 계약:
        //   1) 대상 장치의 kind==MANIP(재굴림/고정/복사/교체) 또는 캐릭터==gambler(무료재굴림)인지 검증.
        //   2) 이번 스테이지 usedCmds에 해당 cmd 마커가 없는지 확인(스테이지당 1회, §9-C).
        //   3) 코인 비용 검증·차감(재굴림/고정=3, 복사/교체=5 — Device 데이터클래스엔 없고 desc 텍스트에만
        //      있는 수치라 S4가 별도 상수화해야 함, 01_engine.md 부록A-6).
        //   4) run.LastCells를 CellsFromIds로 복원 → 고정(N번 칸 유지·나머지 RollOne 재굴림) / 복사(N번 칸을
        //      오른쪽 인접 칸에, 오른쪽 끝이면 왼쪽으로) / 교체(N번 칸을 bestValueId 최다종류로, 동점/없음
        //      이면 "star" 폴백) / 전체 재굴림 중 하나로 새 raw 구성.
        //   5) Evaluate(rng, newRaw, mods, run.SpinIndex-1, spins, flamePenalty=false, capMul)로 재평가
        //      (mods는 이 스핀 시점과 동일 조건으로 재구성 — ResolveSpin의 step1-14를 재사용).
        //   6) EXP -10% 페널티(MANIP만, 도박꾼 무료재굴림은 페널티 없음) 적용 후 gained 재산출.
        //   7) net-adjust: run.Score/run.Coins/run.RunSet4/run.RunAdjPairs에서 run.LastGain(EXP는
        //      StageExp)/LastScoreGain/LastCoinGain/LastSet4/LastAdjPairs를 먼저 빼고 새 결과를 더한다
        //      (직전 스핀 "1개 교체" — 반드시 스테이지당 1회 제한과 함께 구현, 아니면 이중 차감 버그).
        //   8) fromPost=true(POST_SPIN 경유)면 결과를 곧바로 StageFlow.ClearStage/HandleFailure로 확정
        //      (재시도 루프 없음, 02_service.md §3-C "POST_SPIN은 1회성").
        //   9) run.RunRerolled = true(MANIP/도박꾼재굴림 1회라도 쓰면 런 끝까지 유지).
        // ════════════════════════════════════════════════════════════════════
    }

    // SpinOutcome.notes는 readonly 컬렉션 초기화 문법과 생성자 없는 object-initializer를 함께 쓰기 위한
    // 얇은 헬퍼(C# 9엔 record with가 없어 이 패턴으로 필드를 채운 뒤 반환한다).
    internal static class SpinOutcomeExt
    {
        public static SpinOutcome WithNotes(this SpinOutcome outcome, List<string> notes)
        {
            outcome.notes.AddRange(notes);
            return outcome;
        }
    }
}
