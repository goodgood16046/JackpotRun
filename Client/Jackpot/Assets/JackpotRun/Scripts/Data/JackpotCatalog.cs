using System.Collections.Generic;
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

        public static readonly string[] CategoryOrder =
        {
            CatChar, CatMac, CatDev, CatAug, CatRel, CatCur, CatItem, CatAch
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
        }

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
