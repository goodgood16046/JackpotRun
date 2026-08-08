namespace JackpotRun.Engine
{
    // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 C) — 심화모드 상점 '심볼 정비' 서비스
    // 카탈로그. 웹 data.js DEEP.REPAIR_SERVICES(11종, §1119-1132) 그대로 전사. 실행 로직은
    // Run/RepairShop.cs(RunState 의존이라 이 파일에는 없음 — Content 계층 규약).
    public sealed class RepairServiceDef
    {
        public string id;
        public string emoji;
        public string name;
        public int price;
        // kind: addBasic|addHigh|addRare|remove|swap|upgrade|purify|expand|compress|tagbuff|curseCleanse.
        public string kind;
        public int n2;       // 심볼 증감량(대부분) — expand는 상한 델타, compress는 하한 델타.
        public double pct;   // 배수증가분(compress의 요구EXP·tagbuff의 태그효과). 0=미사용.
        public string rarity; // addBasic/addHigh/addRare 대상 풀 희귀도("기본"|"고급"|"희귀"). 그 외 "".
        public bool targetPick; // 대상 선택(심볼/태그) 필요 여부.
        public string desc;
    }

    public static class RepairServices
    {
        public static readonly RepairServiceDef[] All =
        {
            new RepairServiceDef { id = "sv_add_basic",   emoji = "🟢", name = "기본 심볼 추가",  price = 8,  kind = "addBasic",     n2 = 5, pct = 0,    rarity = "기본", targetPick = true,  desc = "선택한 기본 심볼 +5개" },
            new RepairServiceDef { id = "sv_add_high",    emoji = "🔵", name = "고급 심볼 추가",  price = 15, kind = "addHigh",      n2 = 3, pct = 0,    rarity = "고급", targetPick = true,  desc = "선택한 고급 심볼 +3개" },
            new RepairServiceDef { id = "sv_add_rare",    emoji = "🟣", name = "희귀 심볼 추가",  price = 30, kind = "addRare",      n2 = 1, pct = 0,    rarity = "희귀", targetPick = true,  desc = "선택한 희귀 심볼 +1개" },
            new RepairServiceDef { id = "sv_remove",      emoji = "🗑️", name = "심볼 제거",      price = 12, kind = "remove",       n2 = 3, pct = 0,    rarity = "",     targetPick = true,  desc = "선택한 심볼 -3개" },
            new RepairServiceDef { id = "sv_swap",        emoji = "🔁", name = "심볼 교체",      price = 18, kind = "swap",         n2 = 3, pct = 0,    rarity = "",     targetPick = true,  desc = "A 심볼 3개 → B 심볼 3개 (총량 유지)" },
            new RepairServiceDef { id = "sv_upgrade",     emoji = "⬆️", name = "심볼 업그레이드", price = 25, kind = "upgrade",      n2 = 5, pct = 0,    rarity = "",     targetPick = true,  desc = "기본 심볼 5개 → 상위 등급 5개 (총량 유지)" },
            new RepairServiceDef { id = "sv_purify",      emoji = "🕊️", name = "해골 정화",      price = 20, kind = "purify",       n2 = 3, pct = 0,    rarity = "",     targetPick = false, desc = "☠해골 -3, ▫빈칸 +3 (총량 유지)" },
            new RepairServiceDef { id = "sv_expand",      emoji = "📦", name = "덱 확장",        price = 15, kind = "expand",       n2 = 5, pct = 0,    rarity = "",     targetPick = false, desc = "총량 상한 +5" },
            new RepairServiceDef { id = "sv_compress",    emoji = "🗜️", name = "덱 압축",        price = 25, kind = "compress",     n2 = 5, pct = 0.05, rarity = "",     targetPick = false, desc = "총량 최소치 -5, 요구 EXP +5%" },
            new RepairServiceDef { id = "sv_tagbuff",     emoji = "🏷️", name = "태그 강화",      price = 22, kind = "tagbuff",      n2 = 0, pct = 0.10, rarity = "",     targetPick = true,  desc = "선택한 태그 심볼 효과 +10%" },
            // 배치 A Step 5: 심화 전용 저주 정화 서비스(저주 보유 시만 노출 — RepairShop.Available 게이팅).
            new RepairServiceDef { id = "curse_cleanse",  emoji = "🕊️", name = "저주 정화",      price = 35, kind = "curseCleanse", n2 = 1, pct = 0,    rarity = "",     targetPick = false, desc = "현재 저주 1개 제거 (가장 최근 획득)" },
        };

        public static RepairServiceDef ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i];
            return null;
        }
    }
}
