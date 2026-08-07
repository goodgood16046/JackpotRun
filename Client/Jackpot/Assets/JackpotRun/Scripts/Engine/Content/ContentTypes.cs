using System.Collections.Generic;

namespace JackpotRun.Engine
{
    /// <summary>증강·유물·저주 공통 정의. fx 키는 Kotlin Mods 필드명(심볼 오버레이는 "필드.SYM" 점 표기).</summary>
    public sealed class Perk
    {
        public string id;
        public string name;
        public string emoji;
        public string desc;
        public PCat cat;
        public Tier tier;
        public int price;
        public string school;
        public Dictionary<string, double> fx;
        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #9/#13, data.js unlockLevel) — 0=레벨게이트 없음
        // (항상 개방). 8종(증강4·유물4)만 값을 가진다. Shop.PerkUnlocked가 유일한 소비처.
        public int unlockLevel;
    }

    public sealed class ItemDef
    {
        public string id;
        public string name;
        public string emoji;
        public string desc;
        public string kind;      // NEXTSPIN | PHASE | INSTANT
        public int coinCost;
        public Dictionary<string, double> fx;
    }

    public sealed class DeviceDef
    {
        public string id;
        public string name;
        public string emoji;
        public string desc;
        public string kind;      // PASSIVE | ARMED | MANIP | PEEK | INSTANT
        public bool rare;
        public string unlockAch;
        public Dictionary<string, double> fx;
    }

    /// <summary>세트 효과. requires 외에 캐릭터/머신/장치 게이트(req*)가 있으면 그것도 충족해야 발동 (Kotlin buildMods L1958-1960).</summary>
    public sealed class SetEffect
    {
        public string id;
        public string name;
        public string desc;
        public string[] requires;
        public string reqChar;      // null/"" = 무조건
        public string reqMachine;
        public string reqDevice;
        public Dictionary<string, double> fx;
    }
}
