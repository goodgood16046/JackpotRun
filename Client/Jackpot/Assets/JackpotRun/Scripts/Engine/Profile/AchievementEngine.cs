using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 업적 판정 — kotlin-reference\game\SlotV2Service.kt의 private fun composeStat(...)(L2208-2250) 이식
    // + Achievements.All(482종) 전수 판정. Kotlin composeStat은 "AchRow(전용컬럼+counters CSV) + ScoreRow"를
    // 합쳐 파생키(distinctCharS10/lic_dev_*/bldCat_*/accountLevel)까지 계산한 stat 맵을 만드는데, 이
    // 파생키들은 DB에 저장되지 않고 호출마다 즉석 계산된다(원본 그대로) — 그래서 PlayerProfile.Stats에는
    // StatTracker가 실제로 setMax/inc한 "원재료" 키만 있고, 이 파일이 매 호출 그 위에 파생키를 얹는다.
    public static class AchievementEngine
    {
        // Formulas.AccountExp ④ 컴포넌트가 필요로 하는 (key,threshold,tier) 튜플 — Achievements.All에서
        // 1회만 뽑아 재사용(482종을 매 ComposeStat 호출마다 다시 Select하지 않도록).
        // L4(Opus 1차 검수): req가 null/빈 배열인 AchDef가 섞여도(계약 위반이지만 방어적으로) 여기서
        // 죽지 않도록 미리 걸러낸다 — Achievements.cs는 우리 소유 파일이 아니라 재정의/수정 불가.
        private static readonly (string key, long threshold, string tier)[] AchievementExpTable =
            Achievements.All.Where(a => a.req != null && a.req.Length > 0)
                .Select(a => (a.req[0].key, a.req[0].value, a.tier)).ToArray();

        // 12 메인 장치 면허(lic_dev_<id>) 판정 — Kotlin composeStat L2230-2242, 조건 원문 그대로.
        private static readonly (string deviceId, Func<IReadOnlyDictionary<string, long>, bool> cond)[] LicenseConditions =
        {
            ("dev_safe",     s => G(s, "closeClears") >= 5 && G(s, "bestStage") >= 6),
            ("dev_seal",     s => G(s, "skullTotal") >= 200 && G(s, "bestStage") >= 8),
            ("dev_reroll",   s => G(s, "bossClears") >= 3 && G(s, "lastSpinClears") >= 3),
            ("dev_pin",      s => G(s, "exactClears") >= 3 && G(s, "bestStage") >= 8),
            ("dev_coin",     s => G(s, "coinTotal") >= 500 && G(s, "shopBuys") >= 15),
            ("dev_subreel",  s => G(s, "jackpots") >= 5 && G(s, "set4Plus") >= 10),
            ("dev_overheat", s => G(s, "lastSpinClears") >= 10 && G(s, "bestScore") >= 20000),
            ("dev_oracle",   s => G(s, "prayClears") >= 3 && G(s, "bestStage") >= 15),
            ("dev_copy",     s => G(s, "prismPicks") >= 10 && G(s, "set4Plus") >= 10),
            ("dev_swap",     s => G(s, "bossClears") >= 10 && G(s, "bestStage") >= 15),
            ("dev_bell",     s => G(s, "closeClears") >= 30 && G(s, "bossClears") >= 8),
            ("dev_flame",    s => G(s, "bestScore") >= 50000 && G(s, "bestStage") >= 20),
        };

        private static long G(IReadOnlyDictionary<string, long> s, string key) => s.TryGetValue(key, out var v) ? v : 0L;

        // ── composeStat 이식 — PlayerProfile.Stats(원재료) 위에 파생키를 얹은 새 딕셔너리를 반환한다.
        // PlayerProfile.Stats 자체는 건드리지 않는다(Kotlin도 파생키를 DB에 쓰지 않음, 03_meta §6-4 취지). ──
        public static Dictionary<string, long> ComposeStat(PlayerProfile profile)
        {
            var stat = new Dictionary<string, long>(profile.Stats);

            // 파생키: 서로다른 캐릭 N명 S10 (Kotlin L2225).
            long distinctCharS10 = 0;
            foreach (var kv in profile.Stats)
                if (kv.Key.StartsWith("cstage_", StringComparison.Ordinal) && kv.Value >= 10) distinctCharS10++;
            stat["distinctCharS10"] = distinctCharS10;

            // 파생키: 장치 면허 12종 (Kotlin L2229-2242) — lic_<deviceId> = AND 조건 0/1.
            for (int i = 0; i < LicenseConditions.Length; i++)
            {
                var (deviceId, cond) = LicenseConditions[i];
                stat["lic_" + deviceId] = cond(stat) ? 1L : 0L;
            }

            // 파생키: 빌드도감 집계(themeBuildStats, Kotlin L1327-1344) — StatTracker가 setMax한 bld_<id>
            // raw 플래그(25종)를 카테고리별/총합/전공(≥1)/마스터(전부)로 환산.
            long bldTotal = 0, bldAllBasic = 0, bldAllMaster = 0;
            foreach (var kv in StatTracker.ThemeBuildCategoryIds)
            {
                var ids = kv.Value;
                long done = ids.Count(id => G(stat, id) > 0);
                stat["bldCat_" + kv.Key] = done;
                bldTotal += done;
                if (done >= 1) bldAllBasic++;
                if (done == ids.Length) bldAllMaster++;
            }
            stat["bldTotal"] = bldTotal;
            stat["bldAllBasic"] = bldAllBasic;
            stat["bldAllMaster"] = bldAllMaster;

            // 파생키: 졸업레벨 — lic_*/bldCat_* 등 위 파생키가 전부 채워진 *뒤*에 계산해야 정확하다
            // (Kotlin 주석 "⚠️ lic_* 보다 뒤에 둠 — accountExp가 면허 업적을 tier 합산하므로", L2246-2248).
            stat["accountLevel"] = Formulas.AccountLevel(stat, AchievementExpTable);

            return stat;
        }

        // ── 신규 달성 업적 판정 — Kotlin bumpAch의 "achValue(row,key)>=threshold && id !in before" 필터
        // (L1959) 이식. 새로 달성된 AchDef를 profile.AchievedIds에 반영하고, lic_* 업적이면 대응 장치를
        // profile.OwnedDevices에 영구 등록한다(작업 지시 3번 "면허 달성 시 장치 해금 반영"). ──
        public static List<AchDef> Evaluate(PlayerProfile profile)
        {
            var composed = ComposeStat(profile);
            var newly = new List<AchDef>();

            var all = Achievements.All;
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                if (profile.AchievedIds.Contains(a.id)) continue;
                if (a.req == null || a.req.Length == 0) continue; // L4(Opus 1차 검수) — 방어적 가드
                var req = a.req[0];
                long cur = composed.TryGetValue(req.key, out var v) ? v : 0L;
                if (cur < req.value) continue;

                profile.AchievedIds.Add(a.id);
                newly.Add(a);

                if (a.id.StartsWith("lic_", StringComparison.Ordinal))
                {
                    var dev = Array.Find(Devices.All, d => d.unlockAch == a.id);
                    if (dev != null) profile.OwnedDevices.Add(dev.id);
                }
            }

            return newly;
        }
    }
}
