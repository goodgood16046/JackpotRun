using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 슬롯 칸 상세 분해(셀 정보 탭). 웹 game.js:2706-2787
    // `cellInfo(idx)` 그대로 옮긴 표시 전용 함수 — RunState를 전혀 변형하지 않는다. 심화모드(pouch/
    // pouchInfo) 관련 분기는 Unity에 심화모드 자체가 없어(P7 미구현) 옮기지 않았다(웹도 deepMode
    // 게이트 안쪽이라 일반 런에서는 항상 미노출).
    //
    // Opus 2차검수 필수①(2026-08-09) — RunState.LastCellsFinal(웹 `r.lastCells = res.cells`, Evaluate
    // "이후" 최종 칸) 도입. 예전엔 RunState.LastCells(원시 재굴림 입력용, 폭탄/자석 등 Evaluate 내부
    // 변형이 반영되지 않은 스냅샷)를 읽어 셀 정보가 릴에 실제로 보이는 결과와 어긋날 수 있었다 —
    // 이제 LastCellsFinal의 Cell 객체(.sym이 이미 해석된 SymInfo, .tag가 자석/성장/폭탄 표시)를 그대로
    // 사용해 웹과 동일한 소스에서 읽는다.
    public static class CellInfoView
    {
        // 특수효과 한 줄 설명 — 웹 cellInfo의 SP 테이블(game.js:2728) 그대로.
        private static string SpecialText(Sp sp, SymInfo s)
        {
            switch (sp)
            {
                case Sp.COIN: return $"코인 +{(s.coin > 0 ? s.coin : 1)} 획득 (코인 배수 적용)";
                case Sp.BOMB: return $"양옆 칸을 제거하고 칸당 +{Formulas.BOMB_EXP_PER} EXP";
                case Sp.MAGNET: return "한쪽 옆 심볼을 복사";
                case Sp.SKULL: return "기본 무페널티 · 해골빌드면 EXP/점수 가산";
                case Sp.FLAME: return "이번 스핀 전체 EXP +50% (다음 스핀 -50%)";
                case Sp.DICE: return "1~12 무작위 EXP 추가";
                case Sp.WILD: return "최다 심볼 그룹에 합류(세트·잭팟 기여)";
                case Sp.SEED: return "다음 스핀에 책/별/왕관으로 성장";
                // Opus 2차검수 필수③ — "🪙"(astral) 리터럴은 이 엔진 산출 문자열 규약(이모지 없이
                // 한글+숫자만, RewardFlow/NodeEvents.EventRewardMessage와 동일 규약) 자기위반이었다.
                case Sp.KEY: return $"보물 코인 +{Formulas.KEY_COIN_PER}(코인) (열쇠 1개당)";
                default: return "";
            }
        }

        public static CellInfoResult Build(RunState run, int idx)
        {
            if (run == null || run.LastCellsFinal.Count == 0 || idx < 0 || idx >= run.LastCellsFinal.Count) return null;
            var cell = run.LastCellsFinal[idx];
            var s = cell.sym; // 이미 해석된 SymInfo(빈칸이면 SpinResolver.EmptySym과 동일한 필드의 로컬 폴백 — 아래 empty 분기 참조)
            if (s == null) return null;

            var mods = run.LastMods ?? BuildCurrentMods(run);
            int reel = run.LastCellsFinal.Count;
            bool isCenter = idx == reel / 2;
            int lastSpinIdx = run.LastSpinNo >= 0 ? run.LastSpinNo : 0;
            int totalSpins = SpinResolver.EffSpins(run, mods);
            bool isFirst = lastSpinIdx == 0;
            bool isLast = totalSpins > 0 && lastSpinIdx == totalSpins - 1;
            bool empty = s.id == "empty";

            // Opus 2차검수 필수② — 빈칸 SymInfo(SpinResolver.EmptySym)는 .sym(Sym enum) 필드에 의미
            // 없는 자리표시값(Sym.Cherry)이 들어있다(SpinResolver.cs 헤더 주석 "모든 실사용 코드는
            // .id/.special/.tags만 보고 .sym은 참조하지 않는다"). 웹은 문자열 id("empty")로 키를
            // 조회해 자연히 미스(0)가 나지만, Unity의 perSymbolExp/perSymbolScore는 Sym enum이 키라
            // 가드 없이 s.sym으로 조회하면 "빈칸인데 체리 보너스가 새어 들어오는" 오판정이 생긴다 —
            // empty면 무조건 0으로 고정.
            long baseExp = s.exp;
            int perSymExp = empty ? 0 : (mods.perSymbolExp.TryGetValue(s.sym, out var pse) ? pse : 0);
            var tagBonuses = new List<TagBonus>();
            if (!empty && s.tags != null)
            {
                for (int i = 0; i < s.tags.Length; i++)
                {
                    int v = mods.tagExpBonus.TryGetValue(s.tags[i], out var tv) ? tv : 0;
                    if (v != 0) tagBonuses.Add(new TagBonus { tag = s.tags[i], value = v });
                }
            }
            // 웹 quirk 보존(수정 대상 아님) — 웹 cellInfo(game.js:2719)도 실제 스핀 채점(evaluate)의
            // perSkullExp/skull3ScoreMul 등 "해골 개수·세트 의존" 가산과 무관하게 mods.skullExp 하나만
            // 단순 가산한다. 여러 해골이 함께 나온 스핀에서는 이 칸 하나의 분해값 합계가 실제
            // 스핀 EXP와 정확히 일치하지 않을 수 있다 — GainPanel의 "해골 페널티 근사치" 주석과 동일한
            // 종류의 표시 전용 근사이며, 웹 원본을 그대로 옮긴 것이지 Unity가 새로 만든 오차가 아니다.
            int skullExp = (!empty && s.special == Sp.SKULL) ? mods.skullExp : 0;
            long tagSum = 0;
            for (int i = 0; i < tagBonuses.Count; i++) tagSum += tagBonuses[i].value;
            long sub = baseExp + perSymExp + tagSum + skullExp;
            double centerMul = isCenter ? mods.centerExpMul : 1.0;
            long cellExp = System.Math.Max(0, (long)System.Math.Round(sub * centerMul));

            long baseScore = s.score;
            int perSymScore = empty ? 0 : (mods.perSymbolScore.TryGetValue(s.sym, out var pss) ? pss : 0);
            int skullScore = (!empty && s.special == Sp.SKULL) ? mods.skullScoreBonus : 0;
            long cellScore = baseScore + perSymScore + skullScore;

            // Opus 2차검수 필수① — 웹 cellInfo(game.js:2730-2733) 그대로: 특수효과 문구 + c.tag 기반
            // 자석/성장/폭탄 안내. LastCellsFinal이 Cell.tag를 보존하므로 이제 전부 재현 가능(이전
            // 슬라이스의 "재현 불가" 범위축소는 해소됨).
            var specials = new List<string>();
            string spText = SpecialText(s.special, s);
            if (!string.IsNullOrEmpty(spText)) specials.Add(spText);
            if (cell.tag == "🧲") specials.Add("자석으로 복사된 칸");
            if (cell.tag == "🌱→") specials.Add("씨앗이 성장한 칸");
            if (empty || cell.tag == "💥") specials.Add("폭탄으로 제거된 빈 칸 (EXP 0)");

            // ── 이 칸에 영향 주는 증강·유물·캐릭터·저주 (웹 baseM 대비 diff, game.js:2735-2755) ──
            var baseM = ModsBuilder.Build("basic", "gambler", System.Array.Empty<string>(), System.Array.Empty<string>(), "");
            var affecting = new List<AffectingEntry>();

            var charM = ModsBuilder.Build("basic", run.CharId, System.Array.Empty<string>(), System.Array.Empty<string>(), "");
            var charDiff = LabelDiff(charM, baseM, s, empty, isCenter, isFirst, isLast);
            if (charDiff.Count > 0)
            {
                var ch = Characters.ById(run.CharId);
                affecting.Add(new AffectingEntry { id = ch.id, emoji = ch.emoji, name = ch.name, kind = "캐릭터", tier = "", effect = string.Join(" · ", charDiff) });
            }
            for (int i = 0; i < run.Perks.Count; i++)
            {
                var perk = Perks.ById(run.Perks[i]);
                if (perk == null) continue;
                var pm = ModsBuilder.Build("basic", "gambler", new[] { perk.id }, System.Array.Empty<string>(), "");
                var diff = LabelDiff(pm, baseM, s, empty, isCenter, isFirst, isLast);
                if (diff.Count == 0) continue;
                affecting.Add(new AffectingEntry
                {
                    id = perk.id, emoji = perk.emoji, name = perk.name,
                    kind = perk.cat == PCat.AUGMENT ? "증강" : "유물",
                    tier = perk.tier.ToString(), effect = string.Join(" · ", diff),
                });
            }
            for (int i = 0; i < run.Curses.Count; i++)
            {
                var curse = Perks.ById(run.Curses[i]);
                if (curse == null) continue;
                var cm = ModsBuilder.Build("basic", "gambler", System.Array.Empty<string>(), new[] { curse.id }, "");
                var diff = LabelDiff(cm, baseM, s, empty, isCenter, isFirst, isLast);
                if (diff.Count == 0) continue;
                affecting.Add(new AffectingEntry { id = curse.id, emoji = curse.emoji, name = curse.name, kind = "저주", tier = "", effect = string.Join(" · ", diff) });
            }

            var sets = new List<SetInfo>();
            var active = ActiveSets(run);
            for (int i = 0; i < active.Count; i++)
                sets.Add(new SetInfo { name = active[i].name, desc = active[i].desc });

            return new CellInfoResult
            {
                sym = s, idx = idx, isCenter = isCenter, isFirst = isFirst, isLast = isLast, empty = empty,
                baseExp = baseExp, perSymExp = perSymExp, tagBonuses = tagBonuses, skullExp = skullExp,
                centerMul = centerMul, cellExp = cellExp,
                baseScore = baseScore, perSymScore = perSymScore, skullScore = skullScore, cellScore = cellScore,
                specials = specials, affecting = affecting, sets = sets,
                expMul = mods.expMul, flatExp = mods.flatExp,
            };
        }

        private static Mods BuildCurrentMods(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            return ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
        }

        // 웹 cellInfo의 label(m) 클로저(game.js:2736-2749) — m을 baseM과 비교해 "이 콘텐츠 하나가 이
        // 칸에 실제로 기여하는 값"만 델타로 뽑는다. 심볼 표기는 emoji 대신 이름(한글, BMP 안전)을 쓴다.
        // empty 가드는 위 Build()와 동일한 이유(Sym enum 자리표시값 오염 방지).
        private static List<string> LabelDiff(Mods m, Mods baseM, SymInfo s, bool empty, bool isCenter, bool isFirst, bool isLast)
        {
            var outp = new List<string>();
            if (!empty)
            {
                int de = (m.perSymbolExp.TryGetValue(s.sym, out var a1) ? a1 : 0) - (baseM.perSymbolExp.TryGetValue(s.sym, out var b1) ? b1 : 0);
                if (de != 0) outp.Add($"{s.name}EXP {(de > 0 ? "+" : "")}{de}");
                int ds = (m.perSymbolScore.TryGetValue(s.sym, out var a2) ? a2 : 0) - (baseM.perSymbolScore.TryGetValue(s.sym, out var b2) ? b2 : 0);
                if (ds != 0) outp.Add($"{s.name}점수 {(ds > 0 ? "+" : "")}{ds}");
                if (s.tags != null)
                {
                    for (int i = 0; i < s.tags.Length; i++)
                    {
                        string t = s.tags[i];
                        int dv = (m.tagExpBonus.TryGetValue(t, out var a3) ? a3 : 0) - (baseM.tagExpBonus.TryGetValue(t, out var b3) ? b3 : 0);
                        // Opus 2차검수 LOW① — 웹 cellInfo label()(game.js:2740)은 "#" 접두 없이
                        // "{tag}태그 EXP ..."로 적는다("#{tag}"는 openCellSheet의 다른 줄(태그 보너스
                        // 계산 row, game.js:968)에서만 쓰는 별개 표기 — 혼동해 여기 섞으면 안 됨).
                        if (dv != 0) outp.Add($"{t}태그 EXP {(dv > 0 ? "+" : "")}{dv}");
                    }
                }
                if (s.special == Sp.SKULL && m.skullExp != baseM.skullExp)
                {
                    int d = m.skullExp - baseM.skullExp;
                    outp.Add($"해골EXP {(d > 0 ? "+" : "")}{d}");
                }
                if (s.rare && m.rareWeightMul != baseM.rareWeightMul) outp.Add($"희귀등장 ×{FmtMul(m.rareWeightMul)}");
                if (s.special == Sp.COIN && m.coinMul != baseM.coinMul) outp.Add($"코인 ×{FmtMul(m.coinMul)}");
            }
            if (isCenter && m.centerExpMul != baseM.centerExpMul) outp.Add($"가운데 칸 ×{FmtMul(m.centerExpMul)}");
            if (m.expMul != baseM.expMul) outp.Add($"전체EXP ×{FmtMul(m.expMul)}");
            if (m.flatExp != baseM.flatExp)
            {
                int d = m.flatExp - baseM.flatExp;
                outp.Add($"스핀마다 {(d > 0 ? "+" : "")}{d}");
            }
            if (isFirst && m.firstSpinExpMul != baseM.firstSpinExpMul) outp.Add($"첫스핀 ×{FmtMul(m.firstSpinExpMul)}");
            if (isLast && m.lastSpinExpMul != baseM.lastSpinExpMul) outp.Add($"막스핀 ×{FmtMul(m.lastSpinExpMul)}");
            return outp;
        }

        // ModsBuilder.Build의 세트 루프(Mods.cs:257-271)와 동일한 게이트 조건을 읽기 전용으로 재확인한다
        // (웹 E.activeSets, engine.js) — 새 해석기가 아니라 같은 규칙의 조회판.
        private static List<SetEffect> ActiveSets(RunState run)
        {
            var result = new List<SetEffect>();
            var pset = new HashSet<string>(run.Perks);
            var sets = Sets.All;
            for (int i = 0; i < sets.Length; i++)
            {
                var set = sets[i];
                bool ok = true;
                if (set.requires != null)
                {
                    for (int k = 0; k < set.requires.Length; k++)
                        if (!pset.Contains(set.requires[k])) { ok = false; break; }
                }
                if (ok && !string.IsNullOrEmpty(set.reqChar) && set.reqChar != run.CharId) ok = false;
                if (ok && !string.IsNullOrEmpty(set.reqMachine) && set.reqMachine != run.MachineId) ok = false;
                if (ok && !string.IsNullOrEmpty(set.reqDevice) && set.reqDevice != run.Device) ok = false;
                if (ok) result.Add(set);
            }
            return result;
        }

        private static string FmtMul(double v)
        {
            string str = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return str.TrimEnd('0').TrimEnd('.');
        }
    }

    public struct TagBonus { public string tag; public int value; }

    public sealed class AffectingEntry
    {
        public string id;     // 원본 콘텐츠 id(카탈로그 스프라이트 조회용 — UI 표시 전용, 로직 무관)
        public string emoji;
        public string name;
        public string kind;   // "캐릭터" | "증강" | "유물" | "저주"
        public string tier;   // Tier.ToString(), 캐릭터/저주는 빈 문자열
        public string effect; // LabelDiff 조인 문자열
    }

    public sealed class SetInfo
    {
        public string name;
        public string desc;
    }

    public sealed class CellInfoResult
    {
        public SymInfo sym;
        public int idx;
        public bool isCenter;
        public bool isFirst;
        public bool isLast;
        public bool empty;

        public long baseExp;
        public int perSymExp;
        public List<TagBonus> tagBonuses;
        public int skullExp;
        public double centerMul;
        public long cellExp;

        public long baseScore;
        public int perSymScore;
        public int skullScore;
        public long cellScore;

        public List<string> specials;
        public List<AffectingEntry> affecting;
        public List<SetInfo> sets;

        public double expMul;
        public int flatExp;
    }
}
