using System;

namespace JackpotRun.Data
{
    [Serializable]
    public class UnlockStat
    {
        public string stat;
        public int value;
    }

    [Serializable]
    public class PickInfo
    {
        public string emoji, name, role, theme, eff, kind, cmd, cool, when, build, unlock;
        public string[] tags, pros, cons;
        public int diff, ceiling, stab, risk;
    }

    [Serializable]
    public class CatalogEntry
    {
        public string id, category, categoryLabel, key, emoji, nameKo, descKo, tier,
            deviceKind, command, unlockAch, itemKind, spritePath;
        public int price, coinCost, cooldown;
        public float scoreMod;
        public bool rare;
        public UnlockStat[] unlockReq;
        public bool hasPick;
        public PickInfo pick;
    }

    [Serializable]
    public class CatalogData
    {
        public string generatedAt;
        public int total;
        public CatalogEntry[] entries;
    }
}
