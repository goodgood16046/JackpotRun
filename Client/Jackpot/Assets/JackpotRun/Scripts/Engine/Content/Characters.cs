using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 캐릭터 — Kotlin data class Character(SlotV2Engine.kt L310-315) 전사.
    // ENGINE_PORT_DESIGN.md 공유 타입 계약: "{ id,name,emoji; List<StatReq> unlockReq }".
    //
    // [설계 계약 결손 — 보고 대상] 계약에는 scoreMod/startCoins 필드가 없다. 하지만 01_engine.md §4
    // 표와 Kotlin 원본 둘 다 캐릭터마다 scoreMod(리더보드 점수 보정)와 startCoins(시작 코인)가
    // 핵심 수치로 존재하고(예: parttime startCoins=15, highroller startCoins=12), S1 골든 테스트
    // 요구사항("캐릭터/머신 개수·id 중복 0")과 이후 S3 buildMods 구현 모두 이 값을 필요로 한다.
    // 콘텐츠 완전 전사 원칙(원칙 3)에 따라 두 필드를 추가했다 — 계약이 명시한 필드는 그대로 보존.
    // desc 필드도 콘텐츠 전사 목적으로 함께 추가했다.
    public sealed class Character
    {
        public string id, name, emoji, desc;
        public double scoreMod;
        public int startCoins;
        public List<StatReq> unlockReq;
    }

    // 캐릭터 16종 — 01_engine.md §4 표 / SlotV2Engine.kt L316-363 (CHARS) 전사.
    // buildMods 실처리(효과)는 S3 Mods.cs에서 Kotlin buildMods와 동일 구조의 id별 case로 구현된다
    // (이 파일은 콘텐츠 데이터만, 효과 로직 없음).
    public static class Characters
    {
        public const int Count = 16;

        public static readonly Character[] All =
        {
            new Character
            {
                id = "novice", emoji = "🎒", name = "초보학생", desc = "요구치↓·점수보정 ×0.9 (입문)",
                scoreMod = 0.9, startCoins = 0, unlockReq = Req.None(),
            },
            new Character
            {
                id = "scholar", emoji = "📗", name = "장학생", desc = "📘책+2·클리어코인+2",
                scoreMod = 1.0, startCoins = 0, unlockReq = Req.None(),
            },
            new Character
            {
                id = "gambler", emoji = "🎲", name = "도박꾼", desc = "점수보정 ×1.1 · 스테이지당 1회 무료 재굴림",
                scoreMod = 1.1, startCoins = 0, unlockReq = Req.Of("gambles", 12L, "allinWins", 5L),
            },
            new Character
            {
                id = "farmer", emoji = "🍒", name = "체리농부", desc = "🍒체리+1·희귀↓ (안정)",
                scoreMod = 0.95, startCoins = 0, unlockReq = Req.Of("cherryTotal", 1200L, "mstage_cherry", 8L),
            },
            new Character
            {
                id = "parttime", emoji = "🪙", name = "알바생", desc = "시작코인+15·첫스핀 -20%",
                scoreMod = 1.0, startCoins = 15, unlockReq = Req.Of("coinTotal", 1000L, "shopBuys", 20L),
            },
            new Character
            {
                id = "jeweler", emoji = "💎", name = "보석상", desc = "💎보석 점수+25·점수보정 ×1.1",
                scoreMod = 1.1, startCoins = 0, unlockReq = Req.Of("gemTotal", 1200L, "bestScore", 15_000L),
            },
            new Character
            {
                id = "honor", emoji = "🎓", name = "수석졸업생", desc = "실버 증강 1개로 시작",
                scoreMod = 1.0, startCoins = 0, unlockReq = Req.Of("exactClears", 6L, "bestStage", 12L),
            },
            new Character
            {
                id = "cultist", emoji = "💀", name = "해골숭배자", desc = "☠해골 EXP+3·저주당 점수+8%",
                scoreMod = 1.15, startCoins = 0, unlockReq = Req.Of("skullTotal", 1200L, "curseMax", 5L),
            },
            new Character
            {
                id = "crowncol", emoji = "👑", name = "왕관수집가", desc = "👑왕관 점수+30·등장↑",
                scoreMod = 1.15, startCoins = 0, unlockReq = Req.Of("crownTotal", 250L, "jackpots", 6L),
            },
            new Character
            {
                id = "minimalist", emoji = "🍃", name = "미니멀리스트", desc = "유물 3개 이하면 EXP +25%",
                scoreMod = 1.1, startCoins = 0, unlockReq = Req.Of("minimalistS10", 2L),
            },
            new Character
            {
                id = "lucky", emoji = "🍀", name = "행운아", desc = "희귀심볼 등장+25% (한방)",
                scoreMod = 1.05, startCoins = 0, unlockReq = Req.Of("prayClears", 8L),
            },
            new Character
            {
                id = "highroller", emoji = "💠", name = "큰손", desc = "💎보석 점수+25·시작코인+12",
                scoreMod = 1.1, startCoins = 12, unlockReq = Req.Of("coinTotal", 2500L, "shopBuys", 40L),
            },
            new Character
            {
                id = "monk", emoji = "🧘", name = "수도승", desc = "스핀-1·요구치-10% (속전속결)",
                scoreMod = 1.05, startCoins = 0, unlockReq = Req.Of("noItemS8", 2L),
            },
            new Character
            {
                id = "alchemist", emoji = "⚗️", name = "연금술사", desc = "코인+25%·클리어코인+3",
                scoreMod = 1.0, startCoins = 0, unlockReq = Req.Of("richBossClears", 3L),
            },
            new Character
            {
                id = "daredevil", emoji = "😈", name = "무모한도전",
                desc = "모든 EXP+10%·요구치+20% · 남은≤2 EXP+35%·막스핀 +60% (막판형)",
                scoreMod = 1.2, startCoins = 0, unlockReq = Req.Of("allinWins", 18L, "bestStage", 14L),
            },
            new Character
            {
                id = "prodigy", emoji = "🌟", name = "천재", desc = "모든 EXP+12%·점수보정 ×0.95",
                scoreMod = 0.95, startCoins = 0, unlockReq = Req.Of("distinctCharS10", 7L),
            },
        };

        public static readonly Character Base = All[0]; // BASE_CHAR = CHARS[0]

        public static Character ById(string id)
        {
            var c = Array.Find(All, x => x.id == id);
            return c ?? Base; // Kotlin character(id): firstOrNull { it.id == id } ?: BASE_CHAR
        }

        // grandfather 규칙 (SlotV2Engine.kt L368-369): unlockReq 미충족이어도 해당 캐릭터로 이미
        // 클리어 경험(cstage_<id> > 0)이 있으면 계속 해금 유지.
        public static bool Unlocked(Character c, IReadOnlyDictionary<string, long> stat)
        {
            if (Unlocks.Meets(c.unlockReq, stat)) return true;
            long cstage = 0L;
            if (stat != null) stat.TryGetValue("cstage_" + c.id, out cstage);
            return cstage > 0L;
        }
    }
}
