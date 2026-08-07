using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 슬롯머신 — Kotlin data class Machine(SlotV2Engine.kt L227-233) 전사.
    // ENGINE_PORT_DESIGN.md 공유 타입 계약: "{ id,name,emoji; scoreMod; Dictionary<Sym,double> weightMul;
    // Dictionary<Sym,double> weightAdd; List<StatReq> unlockReq }". desc 필드는 계약에 없지만 콘텐츠
    // 완전 전사(원칙 3) 목적으로 추가했다(계약이 명시한 필드는 전부 그대로 보존).
    public sealed class Machine
    {
        public string id, name, emoji, desc;
        public double scoreMod;
        public Dictionary<Sym, double> weightMul;
        public Dictionary<Sym, double> weightAdd;
        public List<StatReq> unlockReq;
    }

    // 머신 16종 — 01_engine.md §3 표 / SlotV2Engine.kt L234-282 (MACHINES) 전사.
    public static class Machines
    {
        public const int Count = 16;

        public static readonly Machine[] All =
        {
            new Machine
            {
                id = "basic", emoji = "🎰", name = "기본", desc = "표준 확률 (입문)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.None(),
            },
            new Machine
            {
                id = "cherry", emoji = "🍒", name = "체리", desc = "체리↑·왕관↓ (안정)",
                weightMul = new Dictionary<Sym, double> { [Sym.Cherry] = 1.5, [Sym.Crown] = 0.6 },
                scoreMod = 0.95,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("cherryTotal", 200L, "bestStage", 4L),
            },
            new Machine
            {
                id = "library", emoji = "📚", name = "도서관", desc = "책↑·코인/보석↓ (경험치)",
                weightMul = new Dictionary<Sym, double> { [Sym.Book] = 1.5, [Sym.Coin] = 0.6, [Sym.Gem] = 0.6 },
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("bookTotal", 200L, "lastSpinClears", 3L),
            },
            new Machine
            {
                id = "gem", emoji = "💎", name = "보석", desc = "보석↑·체리/책↓ (점수)",
                weightMul = new Dictionary<Sym, double> { [Sym.Gem] = 1.7, [Sym.Book] = 0.6, [Sym.Cherry] = 0.6 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("gemTotal", 250L, "bestScore", 4_000L),
            },
            new Machine
            {
                id = "magnet", emoji = "🧲", name = "자석", desc = "자석↑ (콤보)",
                weightMul = new Dictionary<Sym, double> { [Sym.Magnet] = 2.5 },
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("set4Plus", 8L, "bestStage", 6L),
            },
            new Machine
            {
                id = "skull", emoji = "☠", name = "해골", desc = "해골↑·고위험 (점수↑)",
                weightMul = new Dictionary<Sym, double> { [Sym.Skull] = 1.8 },
                scoreMod = 1.10,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("skullTotal", 250L, "curseMax", 3L),
            },
            new Machine
            {
                id = "crown", emoji = "👑", name = "왕관", desc = "왕관↑·기본↓ (운빨 고점)",
                weightMul = new Dictionary<Sym, double> { [Sym.Crown] = 2.0, [Sym.Cherry] = 0.7, [Sym.Book] = 0.7 },
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("crownTotal", 40L, "jackpots", 3L),
            },
            new Machine
            {
                id = "flame", emoji = "🔥", name = "불꽃", desc = "불꽃·해골↑ (배율형)",
                weightMul = new Dictionary<Sym, double> { [Sym.Flame] = 1.8, [Sym.Skull] = 1.4 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("bestScore", 15_000L, "bestStage", 10L),
            },
            new Machine
            {
                id = "bomb", emoji = "💣", name = "폭탄", desc = "폭탄↑ (제거/계산)",
                weightMul = new Dictionary<Sym, double> { [Sym.Bomb] = 2.5 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("bossClears", 5L, "bestStage", 10L),
            },
            new Machine
            {
                id = "star", emoji = "⭐", name = "별빛", desc = "별↑·세트 잘맞음 (콤보)",
                weightMul = new Dictionary<Sym, double> { [Sym.Star] = 2.0, [Sym.Cherry] = 0.8 },
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("starTotal", 200L, "set4Plus", 10L),
            },
            new Machine
            {
                id = "clover", emoji = "🍀", name = "행운", desc = "희귀·코인·불꽃↑ (행운)",
                weightMul = new Dictionary<Sym, double> { [Sym.Crown] = 1.3, [Sym.Coin] = 1.4, [Sym.Flame] = 1.3 },
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("prayClears", 3L, "bestStage", 8L),
            },
            new Machine
            {
                id = "casino", emoji = "🎲", name = "카지노", desc = "🎲주사위 등장·고변동 (운빨)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double> { [Sym.Dice] = 4.0 },
                unlockReq = Req.Of("gambles", 5L, "allinWins", 5L),
            },
            new Machine
            {
                id = "garden", emoji = "🌱", name = "정원", desc = "🌱씨앗 등장·성장형",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double> { [Sym.Seed] = 4.0 },
                unlockReq = Req.Of("cherryTotal", 400L, "bestStage", 9L),
            },
            new Machine
            {
                id = "wildmac", emoji = "🌀", name = "와일드", desc = "🌀와일드 등장 (세트 조작)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double> { [Sym.Wild] = 3.0 },
                unlockReq = Req.Of("prismPicks", 8L, "jackpots", 5L),
            },
            new Machine
            {
                id = "vault", emoji = "🗝", name = "금고", desc = "🗝열쇠 등장·코인↑",
                weightMul = new Dictionary<Sym, double> { [Sym.Coin] = 1.5 },
                scoreMod = 1.10,
                weightAdd = new Dictionary<Sym, double> { [Sym.Key] = 3.0 },
                unlockReq = Req.Of("coinTotal", 600L, "shopBuys", 20L),
            },
            new Machine
            {
                id = "rainbow", emoji = "🌈", name = "무지개", desc = "⭐💎👑 등장↑·🍒📘↓·고변동 (한방)",
                weightMul = new Dictionary<Sym, double>
                {
                    [Sym.Crown] = 1.6, [Sym.Star] = 1.4, [Sym.Gem] = 1.3, [Sym.Cherry] = 0.6, [Sym.Book] = 0.6,
                },
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double>(),
                unlockReq = Req.Of("bestScore", 25_000L, "jackpots", 10L),
            },
        };

        public static readonly Machine Base = All[0]; // BASE_MACHINE = MACHINES[0]

        public static Machine ById(string id)
        {
            var m = Array.Find(All, x => x.id == id);
            return m ?? Base; // Kotlin machine(id): firstOrNull { it.id == id } ?: BASE_MACHINE
        }

        // 최종 가중치 = base(Symbols) × weightMul[sym](없으면 1.0) + weightAdd[sym](없으면 0.0)
        // — SlotV2Engine.kt weighted() (L2079-2091)의 머신 배수/가산 부분과 동일 공식.
        public static double FinalWeight(Machine m, Sym sym)
        {
            double baseW = Symbols.BySym(sym).weight;
            double mul = (m.weightMul != null && m.weightMul.TryGetValue(sym, out var mv)) ? mv : 1.0;
            double add = (m.weightAdd != null && m.weightAdd.TryGetValue(sym, out var av)) ? av : 0.0;
            return baseW * mul + add;
        }
    }
}
