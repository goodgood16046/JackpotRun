using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 캐릭터 — 웹 파리티(WEB_PARITY_DESIGN.md P3-4) 전면 개편.
    //
    // [해금 모델 전환] 기존 Kotlin AND(StatReq 리스트, unlockReq)를 폐기하고 웹 game.js:259-268
    // charUnlocked()의 OR 5축(unlockRuns|unlockScore|unlockStage|unlockLevel|unlockAch, 전부 미사용이면
    // 항상 해금)으로 교체했다. 웹은 캐릭터 해금에 grandfather(cstage_ 유지) 규칙이 없다(전수 grep
    // 결과 없음) — Unity 구버전의 grandfather 절도 함께 폐기(§2 결정 로그 "grandfather는 웹에 있으면
    // 유지" 조건부 지시, 웹에 부재하므로 미적용). 판정 로직은 PlayerProfile.IsCharUnlocked(Profile.cs).
    public sealed class Character
    {
        public string id, name, emoji, desc;
        public double scoreMod;
        public int startCoins;
        public long unlockRuns;
        public long unlockScore;
        public long unlockStage;
        public int unlockLevel;
        public string unlockAch = "";
    }

    // 캐릭터 19종(기존16+신규3) — data.js:145-166(CHARS) 전사. buildMods 실처리는 Mods.cs.
    public static class Characters
    {
        public const int Count = 19;

        public static readonly Character[] All =
        {
            new Character
            {
                id = "novice", emoji = "🎒", name = "초보학생", desc = "요구치↓·점수보정 ×0.9 (입문)",
                scoreMod = 0.9, startCoins = 0,
            },
            new Character
            {
                id = "scholar", emoji = "📗", name = "장학생", desc = "📘책+2·클리어코인+2",
                scoreMod = 1.0, startCoins = 0,
            },
            new Character
            {
                id = "gambler", emoji = "🎲", name = "도박꾼", desc = "점수보정 ×1.1 · 스테이지당 1회 무료 재굴림",
                scoreMod = 1.1, startCoins = 0, // data.js:148 — 웹은 unlock 필드 자체가 없다(항상 해금).
            },
            new Character
            {
                id = "farmer", emoji = "🍒", name = "체리농부", desc = "🍒체리+1·희귀↓ (안정)",
                scoreMod = 0.95, startCoins = 0, unlockRuns = 2, unlockAch = "cherry100",
            },
            new Character
            {
                id = "parttime", emoji = "🪙", name = "알바생", desc = "시작코인+15·첫스핀 -20%",
                scoreMod = 1.0, startCoins = 15, unlockRuns = 3,
            },
            new Character
            {
                id = "jeweler", emoji = "💎", name = "보석상", desc = "💎보석 점수+25·점수보정 ×1.1",
                scoreMod = 1.1, startCoins = 0, unlockScore = 2500, unlockAch = "jackpot1",
            },
            new Character
            {
                id = "honor", emoji = "🎓", name = "수석졸업생", desc = "실버 증강 1개로 시작",
                scoreMod = 1.0, startCoins = 0, unlockStage = 8, unlockAch = "boss5",
            },
            new Character
            {
                id = "cultist", emoji = "💀", name = "해골숭배자", desc = "☠해골 EXP+3·저주당 점수+8%",
                scoreMod = 1.15, startCoins = 0, unlockStage = 5, unlockAch = "boss1",
            },
            new Character
            {
                id = "crowncol", emoji = "👑", name = "왕관수집가", desc = "👑왕관 점수+30·등장↑",
                scoreMod = 1.15, startCoins = 0, unlockScore = 5000, unlockAch = "crown30",
            },
            new Character
            {
                id = "minimalist", emoji = "🍃", name = "미니멀리스트", desc = "유물 3개 이하면 EXP +25%",
                scoreMod = 1.1, startCoins = 0, unlockStage = 7, unlockAch = "exact1",
            },
            new Character
            {
                id = "lucky", emoji = "🍀", name = "행운아", desc = "희귀심볼 등장+25% (한방)",
                scoreMod = 1.05, startCoins = 0, unlockRuns = 4,
            },
            new Character
            {
                id = "highroller", emoji = "💠", name = "큰손", desc = "💎보석 점수+25·시작코인+12",
                scoreMod = 1.1, startCoins = 12, unlockScore = 3500,
            },
            new Character
            {
                id = "monk", emoji = "🧘", name = "수도승", desc = "스핀-1·요구치-10% (속전속결)",
                scoreMod = 1.05, startCoins = 0, unlockStage = 6,
            },
            new Character
            {
                id = "alchemist", emoji = "⚗️", name = "연금술사", desc = "코인+25%·클리어코인+3",
                scoreMod = 1.0, startCoins = 0, unlockScore = 4000,
            },
            new Character
            {
                id = "daredevil", emoji = "😈", name = "무모한도전",
                desc = "모든 EXP+10%·요구치+20% · 남은≤2 EXP+35%·막스핀 +60% (막판형)",
                scoreMod = 1.2, startCoins = 0, unlockStage = 8, unlockAch = "score10k",
            },
            new Character
            {
                id = "prodigy", emoji = "🌟", name = "천재", desc = "모든 EXP+12%·점수보정 ×0.95",
                scoreMod = 0.95, startCoins = 0, unlockStage = 9, unlockAch = "stage10",
            },
            // ── 후반 캐릭터(플레이어 레벨 해금, data.js:163-165) — 강한 대신 룰이 이상하거나 리스크 ──
            new Character
            {
                id = "regent", emoji = "🤴", name = "왕의 대리인",
                desc = "👑왕관 점수+60·등장2배·기본 EXP-10% (왕관 올인)",
                scoreMod = 1.15, startCoins = 0, unlockLevel = 8,
            },
            new Character
            {
                id = "bankrupt", emoji = "💸", name = "파산 졸업생",
                desc = "코인0이면 EXP+50%·코인10↑이면 -20% (무상점 위험)",
                scoreMod = 1.15, startCoins = 0, unlockLevel = 12,
            },
            new Character
            {
                id = "abyss_scholar", emoji = "🌌", name = "심연 장학생",
                desc = "보스 스테이지 EXP+30%·일반 스테이지 -10% (보스 특화)",
                scoreMod = 1.15, startCoins = 0, unlockLevel = 16,
            },
        };

        public static readonly Character Base = All[0]; // BASE_CHAR = CHARS[0]

        public static Character ById(string id)
        {
            var c = Array.Find(All, x => x.id == id);
            return c ?? Base; // Kotlin character(id): firstOrNull { it.id == id } ?: BASE_CHAR
        }
    }
}
