using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;

namespace JackpotRun.Data
{
    public static class JackpotCatalog
    {
        public const string CatChar = "char";
        public const string CatMac = "mac";
        public const string CatDev = "dev";
        public const string CatAug = "aug";
        public const string CatRel = "rel";
        public const string CatCur = "cur";
        public const string CatItem = "item";
        public const string CatAch = "ach";
        // 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20) — 심화 도감 3탭(심볼72/심볼증강21/심볼유물15).
        // 전량 catalog.json에 없는 신규 카테고리라(art 파이프라인이 이 콘텐츠를 다룬 적이 없음) 100%
        // BuildSyntheticEntries가 합성한다 — §2-(R) "catalog 미스 시 Engine 콘텐츠 직접 합성" 선례 확장.
        public const string CatSym = "sym";
        public const string CatSymAug = "symaug";
        public const string CatSymRel = "symrel";

        public static readonly string[] CategoryOrder =
        {
            CatChar, CatMac, CatDev, CatAug, CatRel, CatCur, CatItem, CatAch, CatSym, CatSymAug, CatSymRel
        };

        private static CatalogData _data;
        private static Dictionary<string, CatalogEntry> _byId;
        private static Dictionary<string, List<CatalogEntry>> _byCategory;

        public static CatalogData Data
        {
            get
            {
                EnsureLoaded();
                return _data;
            }
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;

            var textAsset = Resources.Load<TextAsset>("JackpotRun/catalog");
            if (textAsset == null)
            {
                Debug.LogError("JackpotCatalog: Resources/JackpotRun/catalog.json 을 찾을 수 없습니다.");
                _data = new CatalogData { generatedAt = "", total = 0, entries = new CatalogEntry[0] };
            }
            else
            {
                CatalogData parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<CatalogData>(textAsset.text);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("JackpotCatalog: catalog.json 파싱 실패 - " + ex.Message);
                }

                _data = parsed ?? new CatalogData { generatedAt = "", total = 0, entries = new CatalogEntry[0] };
                if (_data.entries == null) _data.entries = new CatalogEntry[0];
            }

            _byId = new Dictionary<string, CatalogEntry>(_data.entries.Length);
            _byCategory = new Dictionary<string, List<CatalogEntry>>();
            foreach (var entry in _data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                _byId[entry.id] = entry;

                if (!_byCategory.TryGetValue(entry.category, out var list))
                {
                    list = new List<CatalogEntry>();
                    _byCategory[entry.category] = list;
                }
                list.Add(entry);
            }

            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, 작업 지시 C "DexView 신규 콘텐츠 표시") —
            // manifest.json/catalog.json엔 아직 신규 콘텐츠(캐릭3·머신3·장치3·증강9·유물12·아이템5)의
            // 아트/엔트리가 없다. 실제 PNG 생성은 이미지 생성 파이프라인(외부 요청)이 필요한 별도
            // 작업이라 이 슬라이스 범위 밖 — 대신 Engine 콘텐츠를 직접 읽어 spritePath 없는(=이모지
            // 폴백) 합성 엔트리를 이어붙인다. 실제 JSON 파일은 건드리지 않는다(메모리상의 조회 테이블만
            // 확장 — 이후 manifest.json이 갱신되면 정식 엔트리가 자동으로 우선한다, 아래 continue 가드).
            foreach (var entry in BuildSyntheticEntries())
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || _byId.ContainsKey(entry.id)) continue;
                _byId[entry.id] = entry;
                if (!_byCategory.TryGetValue(entry.category, out var list))
                {
                    list = new List<CatalogEntry>();
                    _byCategory[entry.category] = list;
                }
                list.Add(entry);
            }
        }

        // Opus 2차검수(P7-4b) [중대③] — 심볼/심볼퍽 108종(72+21+15)의 emoji는 web data.js 원문을
        // 그대로 옮긴 것이라(Achievements.cs처럼 astral을 데이터 단에서 손질한 적이 없음) 다수가
        // astral(레거시 uGUI Text 미표시). 도감 그리드 카드는 이 emoji가 "유일한 아이콘"(실 스프라이트가
        // 없다)이라 그냥 지워버리면(TextSanitize.StripAstral) 빈 아이콘 박스만 남는다 — 지시대로
        // "이름 첫 글자"로 대체해 최소한 무언가는 보이게 한다. 합성 시점(카탈로그 로드 1회)에 한 번만
        // 계산해 두면 DexView/DexDetailPopup 등 이 CatalogEntry.emoji를 읽는 모든 곳에 자동 적용된다.
        private static string SafeIcon(string emoji, string name)
        {
            if (!string.IsNullOrEmpty(emoji) && TextSanitize.StripAstral(emoji) == emoji) return emoji; // 이미 BMP-safe.
            return !string.IsNullOrEmpty(name) ? name.Substring(0, 1) : "❔";
        }

        private static CatalogEntry Synthetic(
            string id, string category, string key, string emoji, string nameKo, string descKo,
            string tier = "", string deviceKind = "", string command = "", string unlockAch = "",
            string itemKind = "", int price = -1, int coinCost = -1, int cooldown = -1,
            float scoreMod = -1f, bool rare = false, bool hasPick = false, PickInfo pick = null) =>
            new CatalogEntry
            {
                id = id, category = category, categoryLabel = CategoryTitle(category), key = key,
                emoji = emoji, nameKo = nameKo, descKo = descKo, tier = tier, deviceKind = deviceKind,
                command = command, unlockAch = unlockAch, itemKind = itemKind, spritePath = null,
                price = price, coinCost = coinCost, cooldown = cooldown, scoreMod = scoreMod, rare = rare,
                unlockReq = null, hasPick = hasPick, pick = pick,
            };

        private static IEnumerable<CatalogEntry> BuildSyntheticEntries()
        {
            var list = new List<CatalogEntry>();
            // char/mac/dev: 실 catalog.json에 이미 있는 id는 (병합 시 _byId.ContainsKey 가드로도 걸러지지만)
            // 여기서 먼저 스킵해 불필요한 FallbackInfo 계산을 피한다 — 신규분만 실질적으로 합성한다.
            foreach (var c in Characters.All)
            {
                if (_byId.ContainsKey("char_" + c.id)) continue;
                list.Add(Synthetic("char_" + c.id, CatChar, c.id, c.emoji, c.name, c.desc,
                    scoreMod: (float)c.scoreMod, hasPick: true, pick: PickMeta.FallbackInfo(CatChar, c.id)));
            }
            foreach (var m in Machines.All)
            {
                if (_byId.ContainsKey("mac_" + m.id)) continue;
                list.Add(Synthetic("mac_" + m.id, CatMac, m.id, m.emoji, m.name, m.desc,
                    scoreMod: (float)m.scoreMod, hasPick: true, pick: PickMeta.FallbackInfo(CatMac, m.id)));
            }
            foreach (var d in Devices.All)
            {
                if (_byId.ContainsKey(d.id)) continue;
                // DeviceDef엔 cmd 필드가 없다(Devices.cs 헤더 각주 — Kotlin 계약 밖). 신규 3종은 전부
                // PASSIVE(웹 data.js:212-214 cmd:null)라 어차피 빈 명령이 정답이다.
                list.Add(Synthetic(d.id, CatDev, d.id, d.emoji, d.name, d.desc,
                    deviceKind: d.kind, command: "", unlockAch: d.unlockAch ?? "", rare: d.rare,
                    hasPick: true, pick: PickMeta.FallbackInfo(CatDev, d.id)));
            }
            foreach (var p in Perks.Augments)
            {
                if (p.unlockLevel <= 0) continue; // 신규 9종만(기존 80종은 실 catalog가 담당)
                list.Add(Synthetic("aug_" + p.id, CatAug, p.id, p.emoji, p.name, p.desc, tier: p.tier.ToString()));
            }
            // discount/thrifty/item_bag/vip/refund(레벨게이트 없는 신규 증강 4종)도 실 catalog엔 없다.
            foreach (var id in new[] { "discount", "thrifty", "item_bag", "vip", "refund" })
            {
                var p = Perks.ById(id);
                if (p == null) continue;
                list.Add(Synthetic("aug_" + p.id, CatAug, p.id, p.emoji, p.name, p.desc, tier: p.tier.ToString()));
            }
            foreach (var p in Perks.Relics)
            {
                if (p.unlockLevel <= 0 && System.Array.IndexOf(NewNonGatedRelicIds, p.id) < 0) continue;
                list.Add(Synthetic("rel_" + p.id, CatRel, p.id, p.emoji, p.name, p.desc, tier: p.tier.ToString(), price: p.price));
            }
            foreach (var it in Items.All)
            {
                if (System.Array.IndexOf(NewItemIds, it.id) < 0) continue;
                list.Add(Synthetic("item_" + it.id, CatItem, it.id, it.emoji, it.name, it.desc,
                    itemKind: it.kind, coinCost: it.coinCost));
            }

            // ── 웹 파리티 P7-4b/Opus 2차검수(WEB_PARITY_DESIGN.md §1-A #19/#20) — 심화 도감 3탭 ──────
            // [중대④ 정정] 이전엔 Symbols.All(72 — key/dice/seed 등 주머니와 무관한 엔진 내부 심볼
            // 포함)을 그대로 순회했다. 웹 POUCH_SYMBOLS(=Pouch.Symbols71, 71개 — key/dice/seed 제외·
            // empty/random 포함)로 교정한다 — Symbols.ById("empty"/"random")는 null이라(SpinResolver.
            // EmptySym 등 별도 센티널) 이 둘은 카탈로그 룩업 대신 고정 라벨로 합성한다(RunView.SymLabel/
            // ShopPanel.SymLabel과 동일 관례).
            // [중대④ 정정] 잠금 안내도 "심화 업적 달성으로 해금"(카테고리 공용 고정 문구) 대신 실제
            // 해금 업적(Content/DeepSymbolUnlock.cs 역매핑)의 이름·설명을 그대로 인용한다.
            var symToAch = new Dictionary<string, string>();
            foreach (var kv in DeepSymbolUnlock.ByAchId) symToAch[kv.Value] = kv.Key;

            foreach (var symId in Pouch.Symbols71)
            {
                var s = Symbols.ById(symId);
                string emoji, name;
                var descParts = new List<string>();
                bool rare;
                if (s != null)
                {
                    emoji = s.emoji; name = s.name; rare = s.rare;
                    descParts.Add($"EXP+{s.exp} · 점수+{s.score} · 코인+{s.coin}");
                    if (s.tags != null && s.tags.Length > 0) descParts.Add("태그: " + string.Join(", ", s.tags));
                }
                else
                {
                    // empty/random — Pouch 전용 센티널(Symbols.All엔 없음).
                    emoji = symId == "empty" ? "▫" : "◎";
                    name = symId == "empty" ? "빈칸" : "랜덤칸";
                    descParts.Add(symId == "empty" ? "빈 칸(효과 없음)" : "주머니에서 실심볼을 다시 뽑음(없으면 빈칸)");
                    rare = false;
                }

                bool hasPick = false;
                PickInfo pick = null;
                if (symToAch.TryGetValue(symId, out var achId))
                {
                    var ach = Achievements.ById(achId);
                    if (ach != null)
                    {
                        hasPick = true;
                        pick = new PickInfo { unlock = $"심화 업적 '{ach.name}' 달성으로 해금 — {ach.desc}" };
                    }
                }
                list.Add(Synthetic("sym_" + symId, CatSym, symId, SafeIcon(emoji, name), name, string.Join(" · ", descParts),
                    tier: Pouch.TierOf(symId), rare: rare, hasPick: hasPick, pick: pick));
            }
            // 심볼증강 21 + 심볼유물 15 — 레벨/업적 게이트가 없어(SymPerkDef에 unlockLevel 필드 자체가
            // 없음) DexView.IsUnlocked 기본 분기(char/mac/dev/sym 4종 외 항상 true)를 그대로 탄다(잠금 없음).
            foreach (var p in SymPerks.Augments)
                list.Add(Synthetic("symaug_" + p.id, CatSymAug, p.id, SafeIcon(p.emoji, p.name), p.name, p.desc, tier: p.tier));
            foreach (var p in SymPerks.Relics)
                list.Add(Synthetic("symrel_" + p.id, CatSymRel, p.id, SafeIcon(p.emoji, p.name), p.name, p.desc, tier: p.tier));

            return list;
        }

        // 웹 파리티 P3-4 신규 콘텐츠 중 unlockLevel 게이트가 없어(=기존 8종 판별 조건으로 안 걸림)
        // 별도 id 목록으로 골라내야 하는 것들 — Perks.cs/Items.cs 헤더의 "신규" 각주와 동일 목록.
        private static readonly string[] NewNonGatedRelicIds =
        {
            "prism_diploma", "golden_ratio", "starlight_crown", "endless_recess", "fortunes_wheel",
            "set_resonator", "reapers_pact", "phoenix_thesis",
        };

        private static readonly string[] NewItemIds =
        {
            "study_note", "aug_catalyst", "gold_marker", "prism_ink", "overcharge",
        };

        public static CatalogEntry Get(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;
            return _byId.TryGetValue(id, out var entry) ? entry : null;
        }

        public static IReadOnlyList<CatalogEntry> ByCategory(string cat)
        {
            EnsureLoaded();
            if (_byCategory.TryGetValue(cat, out var list)) return list;
            return System.Array.Empty<CatalogEntry>();
        }

        public static Sprite LoadSprite(CatalogEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.spritePath)) return null;
            return Resources.Load<Sprite>(e.spritePath);
        }

        // S8(2026-07-31) 이모지 정리: astral(서로게이트 페어) 이모지는 레거시 uGUI Text에서 렌더링되지
        // 않는다(ENGINE_PORT_DESIGN.md S8 항목⑤) — 한글 라벨만으로 충분해 전부 제거했다.
        public static string CategoryTitle(string cat)
        {
            switch (cat)
            {
                case CatChar: return "캐릭터";
                case CatMac: return "슬롯머신";
                case CatDev: return "장치";
                case CatAug: return "증강";
                case CatRel: return "유물";
                case CatCur: return "저주";
                case CatItem: return "아이템";
                case CatAch: return "업적";
                case CatSym: return "심볼";
                case CatSymAug: return "심볼증강";
                case CatSymRel: return "심볼유물";
                default: return cat;
            }
        }

        public static string PickIdOf(string tab, string key)
        {
            if (tab == CatChar) return "char_" + key;
            if (tab == CatMac) return "mac_" + key;
            return key;
        }
    }
}
