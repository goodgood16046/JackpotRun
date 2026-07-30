using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 심볼 특수효과 종류 — Kotlin enum class Sp (SlotV2Engine.kt L98).
    public enum Sp
    {
        NONE,
        WILD,
        BOMB,
        MAGNET,
        SKULL,
        DICE,
        COIN,
        KEY,
        FLAME,
        SEED,
    }

    // 심볼 종류 14종 — 01_engine.md §2.2, Kotlin SYMS(SlotV2Engine.kt L113-129) 선언 순서 그대로.
    // 이 순서는 §10.1 weighted()의 누적가중치 스캔 순서를 결정하므로 절대 바꾸지 말 것
    // (동일 총합이라도 절단 지점이 달라져 다른 심볼이 뽑힐 수 있음).
    public enum Sym
    {
        Cherry,
        Book,
        Star,
        Gem,
        Coin,
        Skull,
        Flame,
        Magnet,
        Bomb,
        Crown,
        Key,
        Dice,
        Seed,
        Wild,
    }

    // 심볼 데이터 — Kotlin data class Sym(SlotV2Engine.kt L100-106) 전체 필드 전사.
    //
    // [설계 계약 결손 — 보고 대상] ENGINE_PORT_DESIGN.md 공유 타입 계약의 SymInfo는
    // "{ Sym sym; string emoji; long exp; long score; bool dormant; }" 4+1 필드만 명시한다.
    // 하지만 (a) 콘텐츠 완전 전사 원칙(원칙 3: "콘텐츠 테이블은 C# 코드로 전사"), (b) S1 골든 테스트
    // 요구사항 "Symbols 14종 값", (c) 머신 최종 가중치표 계산(§3, base weight 필요) 모두 Kotlin 원본의
    // id/name/coin/weight/special/rare/tags 필드 없이는 불가능하다. 따라서 이 필드들을 전부 추가해
    // Kotlin data class와 1:1로 확장했다 — 계약에 명시된 5개 필드는 이름·타입 그대로 보존.
    public sealed class SymInfo
    {
        public Sym sym;
        public string id;
        public string emoji;
        public string name;
        public long exp;
        public long score;
        public long coin;
        public int weight; // 기본 가중치 (Kotlin Int weight) — weighted()에서 double로 승격되어 사용됨
        public Sp special;
        public bool rare;
        public bool dormant; // weight == 0 (key/dice/seed/wild 휴면 4종)
        public string[] tags;
    }

    public static class Symbols
    {
        public const int Count = 14;

        // 01_engine.md §2.2 표 / SlotV2Engine.kt L113-129 그대로 전사.
        public static readonly SymInfo[] All =
        {
            new SymInfo
            {
                sym = Sym.Cherry, id = "cherry", emoji = "🍒", name = "체리",
                exp = 3, score = 0, coin = 0, weight = 25, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "생명" },
            },
            new SymInfo
            {
                sym = Sym.Book, id = "book", emoji = "📘", name = "책",
                exp = 6, score = 0, coin = 0, weight = 18, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "학습" },
            },
            new SymInfo
            {
                sym = Sym.Star, id = "star", emoji = "⭐", name = "별",
                exp = 8, score = 0, coin = 0, weight = 13, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "콤보" },
            },
            new SymInfo
            {
                sym = Sym.Gem, id = "gem", emoji = "💎", name = "보석",
                exp = 1, score = 15, coin = 0, weight = 12, special = Sp.NONE, rare = false, dormant = false,
                tags = new[] { "점수" },
            },
            new SymInfo
            {
                sym = Sym.Coin, id = "coin", emoji = "🪙", name = "코인",
                exp = 0, score = 0, coin = 1, weight = 10, special = Sp.COIN, rare = false, dormant = false,
                tags = new[] { "코인" },
            },
            new SymInfo
            {
                sym = Sym.Skull, id = "skull", emoji = "☠", name = "해골",
                exp = 0, score = 0, coin = 0, weight = 10, special = Sp.SKULL, rare = false, dormant = false,
                tags = new[] { "저주" },
            },
            new SymInfo
            {
                sym = Sym.Flame, id = "flame", emoji = "🔥", name = "불꽃",
                exp = 0, score = 0, coin = 0, weight = 5, special = Sp.FLAME, rare = false, dormant = false,
                tags = new[] { "배율" },
            },
            new SymInfo
            {
                sym = Sym.Magnet, id = "magnet", emoji = "🧲", name = "자석",
                exp = 2, score = 0, coin = 0, weight = 4, special = Sp.MAGNET, rare = false, dormant = false,
                tags = new[] { "조작" },
            },
            new SymInfo
            {
                sym = Sym.Bomb, id = "bomb", emoji = "💣", name = "폭탄",
                exp = 5, score = 0, coin = 0, weight = 2, special = Sp.BOMB, rare = false, dormant = false,
                tags = new[] { "폭발" },
            },
            new SymInfo
            {
                sym = Sym.Crown, id = "crown", emoji = "👑", name = "왕관",
                exp = 20, score = 50, coin = 0, weight = 1, special = Sp.NONE, rare = true, dormant = false,
                tags = new[] { "왕관", "희귀" },
            },
            // ── 휴면(특수, weight=0) — 머신 weightAdd/perk wadd()로만 풀에 주입됨 ──
            new SymInfo
            {
                sym = Sym.Key, id = "key", emoji = "🗝", name = "열쇠",
                exp = 6, score = 0, coin = 0, weight = 0, special = Sp.KEY, rare = false, dormant = true,
                tags = new[] { "열쇠" },
            },
            new SymInfo
            {
                sym = Sym.Dice, id = "dice", emoji = "🎲", name = "주사위",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.DICE, rare = false, dormant = true,
                tags = new[] { "운" },
            },
            new SymInfo
            {
                sym = Sym.Seed, id = "seed", emoji = "🌱", name = "씨앗",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.SEED, rare = false, dormant = true,
                tags = new[] { "생명", "성장" },
            },
            new SymInfo
            {
                sym = Sym.Wild, id = "wild", emoji = "🌀", name = "와일드",
                exp = 0, score = 0, coin = 0, weight = 0, special = Sp.WILD, rare = true, dormant = true,
                tags = new[] { "희귀", "조작" },
            },
        };

        // VALUE_IDS — 세트/잭팟/인접/양끝 판정 대상 (SlotV2Engine.kt L131, 원본 선언 순서 유지).
        public static readonly Sym[] ValueIds = { Sym.Cherry, Sym.Star, Sym.Book, Sym.Gem, Sym.Crown };

        // SET_EXP / SET_SCORE — 같은 심볼 N개(와일드 포함) 세트 보너스, index=개수(0~5) (SlotV2Engine.kt L133-135).
        public static readonly int[] SetExp = { 0, 0, 8, 18, 42, 100 };
        public static readonly int[] SetScore = { 0, 0, 3, 9, 24, 70 };

        public static SymInfo BySym(Sym sym)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].sym == sym) return All[i];
            throw new ArgumentOutOfRangeException(nameof(sym), sym, "Unknown Sym value.");
        }

        public static SymInfo ById(string id) => Array.Find(All, s => s.id == id);
    }
}
