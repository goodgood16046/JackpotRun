using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace JackpotRun.Data
{
    /// <summary>
    /// 조합 시너지 결과 — jackpotpick/meta.js evaluate() 의 반환값 포팅.
    /// </summary>
    public class EvalResult
    {
        public string grade;
        public Color gradeColor;
        public int ceiling, stability, difficulty;
        public string ceilingStars, stabilityStars, difficultyStars, diffLabel, blurb;
        public List<string> warns, pros, cons, buildTokens;
    }

    /// <summary>
    /// jackpotpick/meta.js 완전 포팅 — 시너지 엔진 + 정렬/추천 상수.
    /// pick 원본 데이터(CHARS/MACHINES/DEVICES)는 여기서 다시 들고 있지 않고
    /// JackpotCatalog 의 CatalogEntry.pick 을 그대로 사용한다.
    /// </summary>
    public static class PickMeta
    {
        // ── app.js CHAR_ORDER/MAC_ORDER/DEV_ORDER 그대로 ──────────────
        public static readonly string[] CharOrder =
        {
            "novice", "scholar", "gambler", "farmer", "parttime", "jeweler", "honor", "cultist",
            "crowncol", "minimalist", "lucky", "highroller", "monk", "alchemist", "daredevil", "prodigy"
        };

        public static readonly string[] MacOrder =
        {
            "basic", "cherry", "library", "gem", "magnet", "skull", "crown", "flame",
            "bomb", "star", "clover", "casino", "garden", "wildmac", "vault", "rainbow"
        };

        public static readonly string[] DevOrder =
        {
            "dev_flame", "dev_seal", "dev_safe", "dev_overheat", "dev_subreel", "dev_coin",
            "dev_reroll", "dev_pin", "dev_copy", "dev_swap", "dev_oracle", "dev_bell"
        };

        // ── app.js CHIP_VOCAB 23종 그대로 ──────────────────────────────
        public static readonly string[] ChipVocab =
        {
            "초보추천", "안정형", "고점형", "위험", "상급자용", "체리", "책", "보석", "해골", "왕관",
            "코인", "콤보", "세트", "한방", "재굴림", "증강", "도전", "성장형", "속전속결", "랜덤성↑",
            "패시브", "능동", "만능"
        };

        // ── app.js TAG_CLASS 그대로 ────────────────────────────────────
        public static readonly Dictionary<string, string> TagClass = new Dictionary<string, string>
        {
            { "위험", "hot" },
            { "상급자용", "hot" },
            { "초보추천", "good" },
            { "안정형", "good" },
            { "고점형", "high" },
            { "한방", "high" },
        };

        // ── meta.js DIFF_LABEL / DIFF_COLOR ────────────────────────────
        private const string DefaultDiffColorHex = "#2a3048";

        public static string DiffLabel(int diff)
        {
            switch (diff)
            {
                case 1: return "입문";
                case 2: return "초중급";
                case 3: return "중급";
                case 4: return "고급";
                default: return "";
            }
        }

        public static Color DiffColor(int diff)
        {
            string hex;
            switch (diff)
            {
                case 1: hex = "#34d3c0"; break;
                case 2: hex = "#5b8cff"; break;
                case 3: hex = "#a974ff"; break;
                case 4: hex = "#ffd23f"; break;
                default: hex = DefaultDiffColorHex; break;
            }
            return ParseColor(hex);
        }

        // ── meta.js GRADE_COLOR / gradeColor() ─────────────────────────
        private const string DefaultGradeColorHex = "#8b93a7";

        public static Color GradeColor(string grade)
        {
            string hex;
            switch (grade)
            {
                case "S": hex = "#ff7adb"; break;
                case "A": hex = "#ffd23f"; break;
                case "B": hex = "#5b8cff"; break;
                case "C": hex = "#8b93a7"; break;
                default: hex = DefaultGradeColorHex; break;
            }
            return ParseColor(hex);
        }

        private static Color ParseColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ── 내부 시너지 테이블 ─────────────────────────────────────────
        private class Pair
        {
            public float s;
            public string b;
        }

        private class DevFit
        {
            public float s;
            public string[] chars;
            public string[] macs;
            public string[] themes;
            public string b;
            public string[] anti;
            public string antiB;
        }

        // meta.js PAIRS 21건 그대로.
        private static readonly Dictionary<string, Pair> Pairs = new Dictionary<string, Pair>
        {
            { "farmer|cherry", new Pair { s = 3, b = "🍒체리 등장·EXP가 겹쳐 흔들림 없이 굴러가는 안정형 정석." } },
            { "scholar|library", new Pair { s = 3, b = "📘책 EXP가 두 곳에서 쌓여 경험치 빌드의 핵심 엔진이 됩니다." } },
            { "jeweler|gem", new Pair { s = 3, b = "💎보석 점수가 두 배로 불어나 리더보드 고점이 폭발합니다." } },
            { "highroller|gem", new Pair { s = 3, b = "💎보석 점수 + 시작코인으로 점수와 상점을 동시에 챙깁니다." } },
            { "cultist|skull", new Pair { s = 3, b = "☠해골이 점수·EXP로 바뀌어 고위험·고점 빌드가 완성됩니다." } },
            { "crowncol|crown", new Pair { s = 3, b = "👑왕관이 쏟아지고 점수가 붙어 단숨에 큰 점수를 노립니다." } },
            { "gambler|magnet", new Pair { s = 3, b = "같은 심볼이 뭉치고, 나쁜 스핀은 무료 재굴림으로 복구합니다." } },
            { "lucky|rainbow", new Pair { s = 3, b = "희귀 심볼이 쏟아지는 초고변동 한방 빌드 — 터지면 최고점." } },
            { "gambler|star", new Pair { s = 2, b = "⭐별 콤보를 노리다 실패하면 재굴림으로 다시 시도." } },
            { "parttime|vault", new Pair { s = 2, b = "🗝열쇠·코인으로 상점을 빠르게 돌리는 경제 빌드." } },
            { "alchemist|vault", new Pair { s = 2, b = "코인 증식 + 열쇠 등장으로 상점이 폭주합니다." } },
            { "highroller|vault", new Pair { s = 2, b = "시작코인 + 열쇠로 초반부터 상점을 장악." } },
            { "daredevil|rainbow", new Pair { s = 2, b = "고위험·고변동 — 최고점을 노리는 극단 도박 빌드." } },
            { "daredevil|casino", new Pair { s = 2, b = "EXP +20%로 초고변동 카지노를 밀어붙이는 도박." } },
            { "honor|library", new Pair { s = 2, b = "증강 선출발 + 책 학습으로 스노볼이 매우 빠릅니다." } },
            { "scholar|gem", new Pair { s = 1, b = "책으로 EXP, 보석으로 점수 — 균형형이지만 자원이 분산." } },
            { "minimalist|basic", new Pair { s = 2, b = "유물 최소화 도전과 가장 잘 맞는 표준 머신." } },
            { "cultist|flame", new Pair { s = 2, b = "불꽃 배율 + 해골 EXP로 화력형 고점." } },
            { "crowncol|rainbow", new Pair { s = 2, b = "왕관 보너스 + 희귀 머신으로 한방 고점 극대화." } },
            { "prodigy|basic", new Pair { s = 1, b = "EXP +12%가 표준 머신을 무난하게 받쳐줍니다." } },
            { "monk|cherry", new Pair { s = 1, b = "짧은 스핀을 안정적인 체리로 빠르게 클리어." } },
        };

        // meta.js DEV_FIT 12건 그대로.
        private static readonly Dictionary<string, DevFit> DevFits = new Dictionary<string, DevFit>
        {
            {
                "dev_reroll", new DevFit
                {
                    s = 1.5f, chars = new[] { "gambler" }, macs = new[] { "magnet", "star", "garden" },
                    themes = new[] { "재굴림", "콤보", "안정" }, b = "실패한 스핀을 다시 굴려 콤보 성공률을 끌어올립니다."
                }
            },
            {
                "dev_safe", new DevFit
                {
                    s = 1.2f, chars = new[] { "novice" }, macs = new[] { "cherry", "basic", "skull", "casino", "rainbow" },
                    themes = new[] { "안정" }, b = "최소 EXP를 보장해 폭망을 막아줍니다."
                }
            },
            {
                "dev_seal", new DevFit
                {
                    s = 1.2f, themes = new[] { "안정" }, b = "해골을 막아 안정성을 올립니다.",
                    anti = new[] { "skull", "flame" }, antiB = "해골/불꽃 머신의 핵심 심볼을 막아 빌드와 충돌할 수 있습니다."
                }
            },
            {
                "dev_flame", new DevFit
                {
                    s = 1.2f, themes = new[] { "만능", "고점", "배율", "해골", "왕관", "보석" },
                    b = "모든 스핀 EXP +15% — 어떤 빌드에도 잘 붙는 만능 화력."
                }
            },
            {
                "dev_overheat", new DevFit
                {
                    s = 1.5f, chars = new[] { "cultist", "daredevil" }, macs = new[] { "skull", "flame" },
                    themes = new[] { "해골", "고점" }, b = "EXP를 크게 올리는 대신 해골 위험을 감수하는 고점용."
                }
            },
            {
                "dev_coin", new DevFit
                {
                    s = 1.5f, chars = new[] { "parttime", "alchemist", "highroller" }, macs = new[] { "vault", "clover" },
                    themes = new[] { "코인" }, b = "남는 코인을 EXP로 환전해 경제 빌드를 가속합니다."
                }
            },
            {
                "dev_copy", new DevFit
                {
                    s = 1.5f, macs = new[] { "wildmac", "star", "magnet" }, themes = new[] { "세트", "콤보" },
                    b = "심볼을 복사해 세트·콤보를 인위적으로 완성합니다."
                }
            },
            {
                "dev_swap", new DevFit
                {
                    s = 1.5f, macs = new[] { "wildmac", "magnet" }, themes = new[] { "세트" },
                    b = "최다 심볼로 교체해 세트를 마무리합니다."
                }
            },
            {
                "dev_pin", new DevFit
                {
                    s = 1.2f, macs = new[] { "magnet", "star", "garden" }, themes = new[] { "콤보" },
                    b = "좋은 칸을 고정하고 나머지만 재굴림 — 콤보를 안정화."
                }
            },
            {
                "dev_subreel", new DevFit
                {
                    s = 1.0f, macs = new[] { "magnet", "star", "wildmac" }, themes = new[] { "콤보", "세트" },
                    b = "6칸으로 콤보 폭이 넓어집니다 (최종 EXP -30% 감수)."
                }
            },
            {
                "dev_oracle", new DevFit
                {
                    s = 1.0f, chars = new[] { "minimalist" }, themes = new[] { "도전", "안정" },
                    b = "다음 스핀을 미리 보고 확정해 변수를 제거합니다."
                }
            },
            {
                "dev_bell", new DevFit
                {
                    s = 1.2f, macs = new[] { "rainbow", "crown", "casino" }, themes = new[] { "한방" },
                    b = "아슬아슬한 스테이지를 즉시 졸업시켜 줍니다 (1회)."
                }
            },
        };

        // ── 별 문자열 / 메터 계산 ───────────────────────────────────────
        private static string Stars(int n)
        {
            int clamped = Mathf.Clamp(n, 1, 5);
            return new string('★', clamped) + new string('☆', 5 - clamped);
        }

        private static int Clamp5(double n)
        {
            double rounded = Math.Floor(n + 0.5);
            return (int)Math.Max(1, Math.Min(5, rounded));
        }

        private static int MeterFrom(double raw, double lo, double hi)
        {
            return Clamp5(1 + ((raw - lo) / (hi - lo)) * 4);
        }

        private static bool ArrayContains(string[] arr, string value)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == value) return true;
            return false;
        }

        private static List<string> UniqueTake(IEnumerable<string> items, int max, bool filterEmpty = false)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var it in items)
            {
                if (result.Count >= max) break;
                if (it == null) continue;
                if (filterEmpty && it.Length == 0) continue;
                if (seen.Add(it)) result.Add(it);
            }
            return result;
        }

        // ── 조합 시너지/메터 계산 — meta.js evaluate() 수식·분기 그대로 ──
        public static EvalResult Evaluate(string charKey, string macKey, string devKey)
        {
            var cEntry = JackpotCatalog.Get("char_" + charKey);
            var mEntry = JackpotCatalog.Get("mac_" + macKey);
            var c = cEntry != null && cEntry.hasPick ? cEntry.pick : null;
            var m = mEntry != null && mEntry.hasPick ? mEntry.pick : null;
            if (c == null || m == null) return null;

            PickInfo d = null;
            if (!string.IsNullOrEmpty(devKey))
            {
                var dEntry = JackpotCatalog.Get(devKey);
                d = dEntry != null && dEntry.hasPick ? dEntry.pick : null;
            }

            // 1) 시너지 점수(0~) — 핵심 페어 → 테마일치 → 만능/증강 폴백
            float syn = 0f;
            var notes = new List<string>();
            var warns = new List<string>();

            string pairKey = charKey + "|" + macKey;
            if (Pairs.TryGetValue(pairKey, out var pair))
            {
                syn += pair.s;
                notes.Add(pair.b);
            }
            else if (c.theme == m.theme)
            {
                syn += 2f;
                notes.Add(c.theme + " 테마가 겹쳐 빌드 방향이 또렷합니다.");
            }
            else if (c.theme == "만능" || c.theme == "증강")
            {
                syn += 1f;
                notes.Add(c.name + "은(는) 어떤 머신과도 무난히 어울립니다.");
            }
            else if (m.theme == "만능")
            {
                syn += 0.8f;
                notes.Add("표준 머신이라 캐릭터 개성을 무난히 받쳐줍니다.");
            }

            // 2) 장치 적합
            if (d != null)
            {
                if (DevFits.TryGetValue(devKey, out var fit))
                {
                    bool hit = ArrayContains(fit.chars, charKey) || ArrayContains(fit.macs, macKey);
                    if (!hit && fit.themes != null)
                    {
                        foreach (var t in fit.themes)
                        {
                            string tag = t == "안정" ? "안정형" : t;
                            if (t == c.theme || t == m.theme || ArrayContains(c.tags, tag) || ArrayContains(m.tags, tag))
                            {
                                hit = true;
                                break;
                            }
                        }
                    }
                    if (hit)
                    {
                        syn += fit.s;
                        notes.Add(fit.b);
                    }
                    if (fit.anti != null && ArrayContains(fit.anti, macKey)) warns.Add(fit.antiB);
                }
            }

            // 3) 캐릭/머신 위험 충돌 경고
            if (c.risk >= 2 && m.risk >= 2 && !(d != null && (devKey == "dev_safe" || devKey == "dev_seal")))
                warns.Add("캐릭터·머신 둘 다 변동이 커서 기복이 매우 큽니다. 안전벨트/봉인장막을 고려하세요.");
            if (m.theme == "해골" && charKey != "cultist" && devKey != "dev_seal" && devKey != "dev_safe")
                warns.Add("해골 대비책(해골숭배자/봉인장막/안전벨트)이 없으면 폭망 위험이 큽니다.");

            // 4) 메터(1~5)
            int riskRaw = c.risk + m.risk + (d != null ? d.risk : 0);
            double ceilRaw = c.ceiling + m.ceiling + (d != null ? d.ceiling * 0.6 : 0) + syn * 0.4;
            double stabRaw = c.stab + m.stab + (d != null ? d.stab * 0.6 : 0) - riskRaw * 0.6;
            int ceiling = MeterFrom(ceilRaw, 0.5, 8);
            int stability = MeterFrom(stabRaw, -3, 7);
            int diffBase = Math.Max(c.diff, m.diff) + (riskRaw >= 5 ? 1 : 0);
            int difficulty = Clamp5(diffBase);

            // 5) 등급
            string grade = syn >= 4f ? "S" : syn >= 2.7f ? "A" : syn >= 1.3f ? "B" : "C";

            // 6) 빌드/장단점 취합
            var builds = new List<string>();
            if (!string.IsNullOrEmpty(c.build)) builds.Add(c.build);
            if (!string.IsNullOrEmpty(m.build)) builds.Add(m.build);
            if (d != null && !string.IsNullOrEmpty(d.when)) builds.Add(d.when);
            string joinedBuilds = string.Join(" / ", builds);
            var rawTokens = Regex.Split(joinedBuilds, @"\s*/\s*");
            var trimmedTokens = new List<string>();
            foreach (var t in rawTokens)
            {
                var tt = t.Trim();
                if (tt.Length > 0) trimmedTokens.Add(tt);
            }
            var buildTokens = UniqueTake(trimmedTokens, 4);

            var prosSrc = new List<string>();
            if (c.pros != null) prosSrc.AddRange(c.pros);
            if (m.pros != null) prosSrc.AddRange(m.pros);
            var pros = UniqueTake(prosSrc, 4);

            var consSrc = new List<string>();
            if (c.cons != null) consSrc.AddRange(c.cons);
            if (m.cons != null) consSrc.AddRange(m.cons);
            consSrc.AddRange(warns);
            var cons = UniqueTake(consSrc, 4);

            string[] diffLabels = { "", "낮음", "낮음", "보통", "높음", "매우 높음" };

            return new EvalResult
            {
                grade = grade,
                gradeColor = GradeColor(grade),
                ceiling = ceiling,
                stability = stability,
                difficulty = difficulty,
                ceilingStars = Stars(ceiling),
                stabilityStars = Stars(stability),
                difficultyStars = Stars(difficulty),
                diffLabel = diffLabels[difficulty],
                blurb = notes.Count > 0 ? string.Join(" ", notes) : "무난한 조합입니다. 마음에 드는 테마로 골라보세요.",
                warns = warns,
                pros = pros,
                cons = cons,
                buildTokens = buildTokens,
            };
        }

        // ── 추천 조합 — meta.js recommend() 그대로(beginner/high/challenge). random 은 호출측 처리. ──
        public static bool TryRecommend(string kind, IList<string> chars, IList<string> macs, IList<string> devs,
            out string c, out string m, out string d)
        {
            c = null;
            m = null;
            d = "";

            if (kind == "beginner")
            {
                c = FirstOf(new[] { "novice", "scholar", "farmer", "prodigy" }, chars);
                m = FirstOf(new[] { "basic", "cherry", "library" }, macs);
                d = DevOf(new[] { "dev_safe", "dev_seal", "dev_reroll" }, devs);
                return true;
            }
            if (kind == "high")
            {
                c = FirstOf(new[] { "gambler", "crowncol", "jeweler", "cultist", "daredevil" }, chars);
                m = FirstOf(new[] { "crown", "gem", "rainbow", "magnet" }, macs);
                d = DevOf(new[] { "dev_flame", "dev_overheat", "dev_reroll" }, devs);
                return true;
            }
            if (kind == "challenge")
            {
                c = FirstOf(new[] { "daredevil", "minimalist", "cultist", "monk" }, chars);
                m = FirstOf(new[] { "rainbow", "casino", "skull" }, macs);
                d = DevOf(new[] { "dev_overheat", "dev_bell", "dev_oracle" }, devs);
                return true;
            }
            // "random" 등 그 외 kind — meta.js recommend() 도 null 반환(호출측이 처리).
            return false;
        }

        private static string FirstOf(string[] cands, IList<string> pool)
        {
            if (pool != null)
            {
                foreach (var id in cands)
                    if (pool.Contains(id))
                        return id;
            }
            return pool != null && pool.Count > 0 ? pool[0] : null;
        }

        private static string DevOf(string[] cands, IList<string> ownedDevs)
        {
            if (ownedDevs != null)
            {
                foreach (var id in cands)
                    if (ownedDevs.Contains(id))
                        return id;
            }
            return "";
        }

        // ── 카드 정렬 키 — meta.js unlockOrder() 그대로 ─────────────────
        private static readonly Regex ScoreRe = new Regex(@"(\d+)점");
        private static readonly Regex RunsRe = new Regex(@"(\d+)런");
        private static readonly Regex StageRe = new Regex(@"S(\d+)");

        public static int UnlockOrder(PickInfo p)
        {
            if (p == null || string.IsNullOrEmpty(p.unlock)) return 0;
            string text = p.unlock.Replace(",", "");
            var scoreMatch = ScoreRe.Match(text);
            var runsMatch = RunsRe.Match(text);
            var stageMatch = StageRe.Match(text);
            int score = scoreMatch.Success ? int.Parse(scoreMatch.Groups[1].Value) : 0;
            int runs = runsMatch.Success ? int.Parse(runsMatch.Groups[1].Value) : 0;
            int stage = stageMatch.Success ? int.Parse(stageMatch.Groups[1].Value) : 0;
            return score + runs * 700 + stage * 800 + 1;
        }
    }
}
