using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 슬롯머신 — 웹 파리티(WEB_PARITY_DESIGN.md P3-4) 전면 개편.
    //
    // [해금 모델 전환] 기존 Kotlin AND(StatReq 리스트, unlockReq)를 폐기하고 웹 game.js:269-276
    // machineUnlocked()의 OR 4축(unlockRuns|unlockScore|unlockLevel|unlockAch, 전부 미사용이면 항상
    // 해금 — 웹은 머신 해금에 unlockStage 축 자체가 없다)으로 교체했다. grandfather 규칙은 웹에 없어
    // 미적용(§2 결정 로그, Characters.cs 헤더 각주와 동일 근거).
    public sealed class Machine
    {
        public string id, name, emoji, desc;
        public double scoreMod;
        public Dictionary<Sym, double> weightMul;
        public Dictionary<Sym, double> weightAdd;
        public long unlockRuns;
        public long unlockScore;
        public int unlockLevel;
        public string unlockAch = "";
    }

    // 머신 19종(기존16+신규3) — data.js:170-193(MACHINES) 전사.
    public static class Machines
    {
        public const int Count = 19;

        public static readonly Machine[] All =
        {
            new Machine
            {
                id = "basic", emoji = "🎰", name = "기본", desc = "표준 확률 (입문)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(),
            },
            new Machine
            {
                id = "cherry", emoji = "🍒", name = "체리", desc = "체리↑·왕관↓ (안정)",
                weightMul = new Dictionary<Sym, double> { [Sym.Cherry] = 1.5, [Sym.Crown] = 0.6 },
                scoreMod = 0.95,
                weightAdd = new Dictionary<Sym, double>(), // data.js:172 — 웹은 unlock 필드가 없다(항상 해금).
            },
            new Machine
            {
                id = "library", emoji = "📚", name = "도서관", desc = "책↑·코인/보석↓ (경험치)",
                weightMul = new Dictionary<Sym, double> { [Sym.Book] = 1.5, [Sym.Coin] = 0.6, [Sym.Gem] = 0.6 },
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(), // data.js:173 — 웹은 unlock 필드가 없다(항상 해금).
            },
            new Machine
            {
                id = "gem", emoji = "💎", name = "보석", desc = "보석↑·체리/책↓ (점수)",
                weightMul = new Dictionary<Sym, double> { [Sym.Gem] = 1.7, [Sym.Book] = 0.6, [Sym.Cherry] = 0.6 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockScore = 1500, unlockAch = "jackpot1",
            },
            new Machine
            {
                id = "magnet", emoji = "🧲", name = "자석", desc = "자석↑ (콤보)",
                weightMul = new Dictionary<Sym, double> { [Sym.Magnet] = 2.5 },
                scoreMod = 1.0,
                weightAdd = new Dictionary<Sym, double>(),
                unlockRuns = 4,
            },
            new Machine
            {
                id = "skull", emoji = "☠", name = "해골", desc = "해골↑·고위험 (점수↑)",
                weightMul = new Dictionary<Sym, double> { [Sym.Skull] = 1.8 },
                scoreMod = 1.10,
                weightAdd = new Dictionary<Sym, double>(),
                unlockScore = 4000, unlockAch = "boss1",
            },
            new Machine
            {
                id = "crown", emoji = "👑", name = "왕관", desc = "왕관↑·기본↓ (운빨 고점)",
                weightMul = new Dictionary<Sym, double> { [Sym.Crown] = 2.0, [Sym.Cherry] = 0.7, [Sym.Book] = 0.7 },
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double>(),
                unlockScore = 6000, unlockAch = "crown30",
            },
            new Machine
            {
                id = "flame", emoji = "🔥", name = "불꽃", desc = "불꽃·해골↑ (배율형)",
                weightMul = new Dictionary<Sym, double> { [Sym.Flame] = 1.8, [Sym.Skull] = 1.4 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockRuns = 6, unlockAch = "score10k",
            },
            new Machine
            {
                id = "bomb", emoji = "💣", name = "폭탄", desc = "폭탄↑ (제거/계산)",
                weightMul = new Dictionary<Sym, double> { [Sym.Bomb] = 2.5 },
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double>(),
                unlockRuns = 8, unlockAch = "boss5",
            },
            new Machine
            {
                id = "star", emoji = "⭐", name = "별빛", desc = "별↑·세트 잘맞음 (콤보)",
                weightMul = new Dictionary<Sym, double> { [Sym.Star] = 2.0, [Sym.Cherry] = 0.8 },
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double>(),
                unlockScore = 3000,
            },
            new Machine
            {
                id = "clover", emoji = "🍀", name = "행운", desc = "희귀·코인·불꽃↑ (행운)",
                weightMul = new Dictionary<Sym, double> { [Sym.Crown] = 1.3, [Sym.Coin] = 1.4, [Sym.Flame] = 1.3 },
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double>(),
                unlockRuns = 5,
            },
            new Machine
            {
                id = "casino", emoji = "🎲", name = "카지노", desc = "🎲주사위 등장·고변동 (운빨)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double> { [Sym.Dice] = 4.0 },
                unlockScore = 4500,
            },
            new Machine
            {
                id = "garden", emoji = "🌱", name = "정원", desc = "🌱씨앗 등장·성장형",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.05,
                weightAdd = new Dictionary<Sym, double> { [Sym.Seed] = 4.0 },
                unlockRuns = 7,
            },
            new Machine
            {
                id = "wildmac", emoji = "🌀", name = "와일드", desc = "🌀와일드 등장 (세트 조작)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.1,
                weightAdd = new Dictionary<Sym, double> { [Sym.Wild] = 3.0 },
                unlockScore = 5000,
            },
            // ── 후반 슬롯머신(플레이어 레벨 해금, data.js:188-190) — 기본보다 특이·고위험 고점 ──
            new Machine
            {
                id = "nightmare", emoji = "😱", name = "악몽 슬롯", desc = "☠해골 대량 등장·해골도 EXP+6 (고위험 고점)",
                weightMul = new Dictionary<Sym, double> { [Sym.Skull] = 2.2 },
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double> { [Sym.Skull] = 2.0 },
                unlockLevel = 10,
            },
            new Machine
            {
                id = "throne", emoji = "👑", name = "빈 왕좌 슬롯",
                desc = "👑왕관 대량·왕관 점수+40·기본 EXP-10% (왕관 특화)",
                weightMul = new Dictionary<Sym, double> { [Sym.Crown] = 2.5 },
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double> { [Sym.Crown] = 2.0 },
                unlockLevel = 12,
            },
            new Machine
            {
                id = "broke", emoji = "💸", name = "파산 슬롯", desc = "코인 안 나옴·🍒📘⭐ 값심볼 EXP+2 (무코인 고점)",
                weightMul = new Dictionary<Sym, double>(),
                scoreMod = 1.2,
                weightAdd = new Dictionary<Sym, double>(),
                unlockLevel = 14,
            },
            // ── 나머지 기존 머신 (unlockRuns/unlockScore, data.js:191-193) ──
            new Machine
            {
                id = "vault", emoji = "🗝", name = "금고", desc = "🗝열쇠 등장·코인↑",
                weightMul = new Dictionary<Sym, double> { [Sym.Coin] = 1.5 },
                scoreMod = 1.10,
                weightAdd = new Dictionary<Sym, double> { [Sym.Key] = 3.0 },
                unlockRuns = 9,
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
                unlockScore = 8000,
            },
        };

        public static readonly Machine Base = All[0]; // BASE_MACHINE = MACHINES[0]

        public static Machine ById(string id)
        {
            var m = Array.Find(All, x => x.id == id);
            return m ?? Base; // Kotlin machine(id): firstOrNull { it.id == id } ?: BASE_MACHINE
        }

        // 최종 가중치 = base(Symbols) × weightMul[sym](없으면 1.0) + weightAdd[sym](없으면 0.0)
        public static double FinalWeight(Machine m, Sym sym)
        {
            double baseW = Symbols.BySym(sym).weight;
            double mul = (m.weightMul != null && m.weightMul.TryGetValue(sym, out var mv)) ? mv : 1.0;
            double add = (m.weightAdd != null && m.weightAdd.TryGetValue(sym, out var av)) ? av : 0.0;
            return baseW * mul + add;
        }
    }
}
