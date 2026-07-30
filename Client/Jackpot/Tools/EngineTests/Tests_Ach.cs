using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S5a 콘텐츠 무결성 테스트 — Achievements: 기본 16 + 확장 466 = 482종.
    // ENGINE_PORT_DESIGN.md 검증 카테고리 2("콘텐츠 무결성")의 S5a분: 개수(482, 16/466 분리)·id중복 0·
    // catalog.json ach_ 16종 교차대조·req 스탯 키 사전(03_meta.md §5) 소속 여부·hidden 28·면허 매핑 무결성.
    //
    // ⚠️ TestCtx API: Tests_Content2.cs/Tests_Perks.cs와 동일하게 `TestCtx.Check(bool,string)` 별칭을 쓴다
    // (ENGINE_PORT_DESIGN.md "계약 확정 이력" — TestCtx에 Check(bool,string) 별칭 허용 확정).
    public static class Tests_Ach
    {
        public static void Run(TestCtx t)
        {
            CheckCounts(t);
            CheckNoDuplicateIds(t);
            CheckCatalogCrossRef(t);
            CheckStatKeysInDictionary(t);
            CheckHiddenCount(t);
            CheckLicenseMapping(t);
            CheckCategoryAndTierDistribution(t);
        }

        // ── 개수 검증 (기본 16 / 확장 466 / 합계 482) ──
        private static void CheckCounts(TestCtx t)
        {
            t.Check(Achievements.All.Length == 482, $"Achievements.All.Length == 482 (실제 {Achievements.All.Length})");
            t.Check(Achievements.Count == 482, $"Achievements.Count == 482 (실제 {Achievements.Count})");
            t.Check(Achievements.BaseCount == 16, $"Achievements.BaseCount == 16 (실제 {Achievements.BaseCount})");
            t.Check(Achievements.ExtCount == 466, $"Achievements.ExtCount == 466 (실제 {Achievements.ExtCount})");

            // 기본 16종은 배열 선두에 Kotlin ACHIEVEMENTS_BASE 순서 그대로 위치한다(ACHIEVEMENTS_BASE + LIST).
            var baseIds = new HashSet<string>
            {
                "cherry100", "cherry500", "crown10", "crown30", "jackpot1", "jackpot10",
                "boss1", "boss5", "stage10", "stage15", "lastclear5", "exact1", "prism5",
                "score10k", "score50k", "runs20",
            };
            t.Check(baseIds.Count == 16, $"기본 16종 id 목록 정의 개수 == 16 (실제 {baseIds.Count})");

            var actualBaseIds = new HashSet<string>(Achievements.All.Take(16).Select(a => a.id));
            t.Check(actualBaseIds.SetEquals(baseIds),
                $"Achievements.All[0..16) id 집합 == 기본 16종 (실제: {string.Join(",", actualBaseIds)})");

            int extCount = Achievements.All.Length - 16;
            t.Check(extCount == 466, $"Achievements.All[16..] 개수 == 466 (실제 {extCount})");
        }

        // ── id 중복 0 (482종 통합) ──
        private static void CheckNoDuplicateIds(TestCtx t)
        {
            var list = Achievements.All.Select(a => a.id).ToList();
            var distinct = new HashSet<string>(list);
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(distinct.Count == list.Count, $"Achievements: id 중복 0 (중복: {string.Join(",", dupes)})");
        }

        // ── catalog.json ach_* 16종 교차 대조 (기본 16종만 — 확장 466종은 catalog에 없음, catalog 실측 확인) ──
        // catalog entry: category=="ach", id="ach_<achId>", key=="<achId>"(Perks/Items와 동일 규칙 — "key" 필드가
        // 엔진 bare id).
        private static void CheckCatalogCrossRef(TestCtx t)
        {
            string path;
            try
            {
                path = CatalogPath();
            }
            catch (Exception ex)
            {
                t.Check(false, $"catalog.json 경로 계산 실패: {ex.Message}");
                return;
            }

            if (!File.Exists(path))
            {
                t.Check(false, $"catalog.json 없음: {path}");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var entries = doc.RootElement.GetProperty("entries");

            var catalogAchIds = new HashSet<string>();
            foreach (var e in entries.EnumerateArray())
            {
                if (e.GetProperty("category").GetString() == "ach")
                    catalogAchIds.Add(e.GetProperty("key").GetString());
            }

            var engineBaseIds = new HashSet<string>(Achievements.All.Take(16).Select(a => a.id));

            var onlyInCatalog = catalogAchIds.Except(engineBaseIds).ToList();
            var onlyInEngine = engineBaseIds.Except(catalogAchIds).ToList();
            t.Check(onlyInCatalog.Count == 0 && onlyInEngine.Count == 0,
                $"catalog.json ach key == 기본 16종 id (catalog만: {string.Join(",", onlyInCatalog)} / 엔진만: {string.Join(",", onlyInEngine)})");
            t.Check(catalogAchIds.Count == 16, $"catalog.json ach 항목 개수 == 16 (실제 {catalogAchIds.Count})");
        }

        // Tools/EngineTests/Tests_Ach.cs 기준 상대경로로 catalog.json을 찾는다(작업 디렉터리 의존 X).
        // Client/Jackpot/Tools/EngineTests -> ../.. -> Client/Jackpot -> Assets/JackpotRun/Resources/JackpotRun/catalog.json
        private static string CatalogPath([CallerFilePath] string here = "")
        {
            var dir = Path.GetDirectoryName(here) ?? throw new InvalidOperationException("CallerFilePath 획득 실패");
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json"));
        }

        // ── req 스탯 키가 03_meta.md §5.3 "고유 key 전체 목록"(156개, SlotV2AchievementsExt.LIST 기준)에
        // 전부 속하는지 확인. 기본 16종의 키(cherryTotal/crownTotal/jackpots/bossClears/bestStage/
        // lastSpinClears/exactClears/prismPicks/bestScore/runs)도 §5.1 SlotV2AchRow 전용 컬럼 10개와
        // 전부 겹치므로 이 사전 안에 있다. 사전 밖 키가 있어도 실패로 취급하지 않고 Report만 한다
        // (작업 지시: "사전 밖 키는 목록 Report — 실패는 아님").
        private static readonly HashSet<string> StatKeyDict156 = new HashSet<string>
        {
            "allinBusts", "allinWins", "basicOnlyBestStage", "bestScore",
            "bestStage", "bldAllBasic", "bldAllMaster", "bldCat_성장형",
            "bldCat_역전형", "bldCat_운명형", "bldCat_위험형", "bldCat_조합형",
            "bldTotal", "bombTotal", "bookTotal", "bossAllinClear",
            "bossClear_finals", "bossClear_grad", "bossClear_luck", "bossClear_strict",
            "bossClears", "bossCounterClear_finals", "bossCounterClear_grad", "bossCounterClear_luck",
            "bossCounterClear_strict", "bossNoDeviceClears", "bossNoItemClears", "bossOverkillClears",
            "bossStreak3", "cherryTotal", "closeClears", "cmdCoinTotal",
            "cmdCoin_allin", "cmdCoin_focus", "cmdCoin_pray", "coinTotal",
            "crownJackpots", "crownTotal", "cstage_alchemist", "cstage_crowncol",
            "cstage_cultist", "cstage_daredevil", "cstage_farmer", "cstage_gambler",
            "cstage_highroller", "cstage_honor", "cstage_jeweler", "cstage_lucky",
            "cstage_minimalist", "cstage_monk", "cstage_novice", "cstage_parttime",
            "cstage_prodigy", "cstage_scholar", "curse5Stage", "curseMax",
            "debtBossClears", "deviceUses", "devicesOwned", "diceTotal",
            "dvstage_dev_bell", "dvstage_dev_coin", "dvstage_dev_copy", "dvstage_dev_flame",
            "dvstage_dev_oracle", "dvstage_dev_overheat", "dvstage_dev_pin", "dvstage_dev_reroll",
            "dvstage_dev_safe", "dvstage_dev_seal", "dvstage_dev_subreel", "dvstage_dev_swap",
            "dvuse_dev_bell", "dvuse_dev_coin", "dvuse_dev_copy", "dvuse_dev_flame",
            "dvuse_dev_oracle", "dvuse_dev_overheat", "dvuse_dev_pin", "dvuse_dev_reroll",
            "dvuse_dev_safe", "dvuse_dev_seal", "dvuse_dev_subreel", "dvuse_dev_swap",
            "exactClears", "flameTotal", "focusUses", "gambles",
            "gemTotal", "itemsUsed", "jackpots", "keyTotal",
            "lastClears", "lastSpinClears", "lastUses", "lic_dev_bell",
            "lic_dev_coin", "lic_dev_copy", "lic_dev_flame", "lic_dev_oracle",
            "lic_dev_overheat", "lic_dev_pin", "lic_dev_reroll", "lic_dev_safe",
            "lic_dev_seal", "lic_dev_subreel", "lic_dev_swap", "magnetTotal",
            "maxBookSpin", "maxCherrySpin", "maxCoinSpin", "maxGemSpin",
            "maxOverPct", "maxRunJackpots", "maxSkullSpin", "minimalistS10",
            "mstage_basic", "mstage_bomb", "mstage_casino", "mstage_cherry",
            "mstage_clover", "mstage_crown", "mstage_flame", "mstage_garden",
            "mstage_gem", "mstage_library", "mstage_magnet", "mstage_rainbow",
            "mstage_skull", "mstage_star", "mstage_vault", "mstage_wildmac",
            "noCommandBestStage", "noDevStage", "noGoldBestStage", "noItemMaxS",
            "noPrismBestStage", "noRelicBestStage", "noRerollBestStage", "noShopS10",
            "pinUses", "prayClears", "prayFails", "prismPicks",
            "relicsMax", "rerollUses", "runs", "seedTotal",
            "set4Plus", "shopBuys", "skullTotal", "starTotal",
            "totalSpins", "wildJackpots", "wildTotal", "zeroCoinClears",
        };

        private static void CheckStatKeysInDictionary(TestCtx t)
        {
            t.Check(StatKeyDict156.Count == 156, $"03_meta.md §5.3 스탯 키 사전 로컬 사본 개수 == 156 (실제 {StatKeyDict156.Count})");

            var outside = Achievements.All
                .Where(a => a.req != null && a.req.Length > 0)
                .Select(a => a.req[0].key)
                .Distinct()
                .Where(k => !StatKeyDict156.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            t.Report("Achievements req 스탯 키 사전 밖 목록", outside.Count == 0
                ? "없음 (482종 전부 156종 사전 안에 포함)"
                : $"{outside.Count}건: {string.Join(", ", outside)}");

            // req가 정확히 1개씩만 있는지도 함께 확인 (Kotlin Achievement는 key/threshold 단일 쌍).
            var badReqShape = Achievements.All.Where(a => a.req == null || a.req.Length != 1).Select(a => a.id).ToList();
            t.Check(badReqShape.Count == 0, $"모든 업적의 req는 정확히 1원소 (위반: {string.Join(",", badReqShape)})");
        }

        // ── hidden 개수 28 (확장 466 안에만 존재, 기본 16은 전부 hidden=false) ──
        private static void CheckHiddenCount(TestCtx t)
        {
            int hiddenTotal = Achievements.All.Count(a => a.hidden);
            t.Check(hiddenTotal == 28, $"hidden == true 전체 개수 == 28 (실제 {hiddenTotal})");

            int hiddenInBase = Achievements.All.Take(16).Count(a => a.hidden);
            t.Check(hiddenInBase == 0, $"기본 16종 중 hidden == true 개수 == 0 (실제 {hiddenInBase})");

            int hiddenInExt = Achievements.All.Skip(16).Count(a => a.hidden);
            t.Check(hiddenInExt == 28, $"확장 466종 중 hidden == true 개수 == 28 (실제 {hiddenInExt})");
        }

        // ── 면허 매핑 무결성 (실존 장치 id) ──
        // 03_meta.md §2.1: 장치 면허(lic_*, cat="면허") 12종 — id "lic_<deviceSuffix>", key "lic_dev_<deviceSuffix>".
        // 03_meta.md §2.2: 장치 숙련/장인(dm_*, cat="장치면허") 24종 — id "dm_dev_<deviceSuffix>_use"/"_master",
        //   key "dvuse_dev_<deviceSuffix>"/"dvstage_dev_<deviceSuffix>". 두 그룹 모두 12 메인 장치
        //   (bell/coin/copy/flame/oracle/overheat/pin/reroll/safe/seal/subreel/swap) 전용이며, 대응하는
        //   Devices.All의 실존 id("dev_<deviceSuffix>")가 있어야 한다(Devices.cs는 S2b 산출물, 참조만 — 수정 금지).
        private static readonly string[] ExpectedLicenseDeviceSuffixes =
        {
            "bell", "coin", "copy", "flame", "oracle", "overheat", "pin", "reroll", "safe", "seal", "subreel", "swap",
        };

        private static void CheckLicenseMapping(TestCtx t)
        {
            var deviceIds = new HashSet<string>(Devices.All.Select(d => d.id));

            // 2.1 장치 면허 12종
            var licAchs = Achievements.All.Where(a => a.cat == "면허" && a.id.StartsWith("lic_", StringComparison.Ordinal)).ToList();
            t.Check(licAchs.Count == 12, $"cat==\"면허\" lic_* 업적 개수 == 12 (실제 {licAchs.Count})");

            var licSuffixes = new List<string>();
            foreach (var a in licAchs)
            {
                string suffix = a.id.Substring("lic_".Length);
                licSuffixes.Add(suffix);

                string expectedKey = "lic_dev_" + suffix;
                t.Check(a.req.Length == 1 && a.req[0].key == expectedKey,
                    $"lic_{suffix}.req[0].key == \"{expectedKey}\" (실제 {(a.req.Length == 1 ? a.req[0].key : "req 형태 이상")})");

                string deviceId = "dev_" + suffix;
                t.Check(deviceIds.Contains(deviceId), $"lic_{suffix} → 실존 장치 id \"{deviceId}\" (Devices.All에 없음)");
            }
            t.Check(new HashSet<string>(licSuffixes).SetEquals(ExpectedLicenseDeviceSuffixes),
                $"lic_* 대상 장치 suffix 집합 == 12 메인 장치 (실제: {string.Join(",", licSuffixes.OrderBy(x => x, StringComparer.Ordinal))})");

            // 2.2 장치 숙련/장인 24종 (12 use + 12 master)
            var dmAchs = Achievements.All.Where(a => a.cat == "장치면허" && a.id.StartsWith("dm_dev_", StringComparison.Ordinal)).ToList();
            t.Check(dmAchs.Count == 24, $"cat==\"장치면허\" dm_* 업적 개수 == 24 (실제 {dmAchs.Count})");

            var useSuffixes = new List<string>();
            var masterSuffixes = new List<string>();
            foreach (var a in dmAchs)
            {
                string rest = a.id.Substring("dm_dev_".Length); // "<suffix>_use" | "<suffix>_master"
                string suffix; string expectedKeyPrefix;
                if (rest.EndsWith("_use", StringComparison.Ordinal))
                {
                    suffix = rest.Substring(0, rest.Length - "_use".Length);
                    expectedKeyPrefix = "dvuse_dev_";
                    useSuffixes.Add(suffix);
                }
                else if (rest.EndsWith("_master", StringComparison.Ordinal))
                {
                    suffix = rest.Substring(0, rest.Length - "_master".Length);
                    expectedKeyPrefix = "dvstage_dev_";
                    masterSuffixes.Add(suffix);
                }
                else
                {
                    t.Check(false, $"dm_dev_* id \"{a.id}\"가 _use/_master로 끝나지 않음");
                    continue;
                }

                string expectedKey = expectedKeyPrefix + suffix;
                t.Check(a.req.Length == 1 && a.req[0].key == expectedKey,
                    $"{a.id}.req[0].key == \"{expectedKey}\" (실제 {(a.req.Length == 1 ? a.req[0].key : "req 형태 이상")})");

                string deviceId = "dev_" + suffix;
                t.Check(deviceIds.Contains(deviceId), $"{a.id} → 실존 장치 id \"{deviceId}\" (Devices.All에 없음)");
            }
            t.Check(new HashSet<string>(useSuffixes).SetEquals(ExpectedLicenseDeviceSuffixes),
                $"dm_*_use 대상 장치 suffix 집합 == 12 메인 장치 (실제: {string.Join(",", useSuffixes.OrderBy(x => x, StringComparer.Ordinal))})");
            t.Check(new HashSet<string>(masterSuffixes).SetEquals(ExpectedLicenseDeviceSuffixes),
                $"dm_*_master 대상 장치 suffix 집합 == 12 메인 장치 (실제: {string.Join(",", masterSuffixes.OrderBy(x => x, StringComparer.Ordinal))})");
        }

        // ── 카테고리별/티어별 개수 — 03_meta.md §1.1 표(확장 466개 기준) 전수 대조. 수치 하나도 다르면 안
        // 된다는 작업 지시에 맞춰 30개 카테고리 + 4개 티어 분포를 전부 하드코딩 대조한다. ──
        private static readonly Dictionary<string, int> ExpectedCatCounts = new Dictionary<string, int>
        {
            ["캐릭터숙련"] = 64, ["머신숙련"] = 64, ["심볼"] = 31, ["연구"] = 30, ["히든"] = 28, ["경제"] = 25,
            ["보스공략"] = 25, ["장치면허"] = 24, ["특수심볼"] = 22, ["제한도전"] = 20, ["빌드도감"] = 16,
            ["명령어"] = 12, ["면허"] = 12, ["반복"] = 11, ["고점"] = 8, ["빌드"] = 8, ["장치"] = 7, ["역전"] = 7,
            ["입문"] = 6, ["클리어"] = 6, ["유물"] = 5, ["아슬아슬"] = 5, ["아이템"] = 4, ["상점"] = 4, ["점수"] = 4,
            ["저주"] = 4, ["도전"] = 4, ["정밀"] = 4, ["잭팟"] = 4, ["보스"] = 2,
        };
        private static readonly Dictionary<string, int> ExpectedTierCounts = new Dictionary<string, int>
        {
            ["브론즈"] = 70, ["실버"] = 103, ["골드"] = 163, ["프리즘"] = 130,
        };

        private static void CheckCategoryAndTierDistribution(TestCtx t)
        {
            t.Check(ExpectedCatCounts.Values.Sum() == 466, $"기대 카테고리 합계 == 466 (실제 {ExpectedCatCounts.Values.Sum()})");
            t.Check(ExpectedTierCounts.Values.Sum() == 466, $"기대 티어 합계 == 466 (실제 {ExpectedTierCounts.Values.Sum()})");

            var ext = Achievements.All.Skip(16).ToList(); // 확장 466종만 (기본 16은 cat="기타"/tier="브론즈" 고정값이라 표 대상 아님)
            t.Check(ext.Count == 466, $"확장분 개수 == 466 (실제 {ext.Count})");

            var actualCat = ext.GroupBy(a => a.cat).ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in ExpectedCatCounts)
            {
                actualCat.TryGetValue(kv.Key, out int actual);
                t.Check(actual == kv.Value, $"cat==\"{kv.Key}\" 개수 == {kv.Value} (실제 {actual})");
            }
            var extraCat = actualCat.Keys.Except(ExpectedCatCounts.Keys).ToList();
            t.Check(extraCat.Count == 0, $"03_meta.md §1.1 표에 없는 cat 값 0건 (실제: {string.Join(",", extraCat)})");

            var actualTier = ext.GroupBy(a => a.tier).ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in ExpectedTierCounts)
            {
                actualTier.TryGetValue(kv.Key, out int actual);
                t.Check(actual == kv.Value, $"tier==\"{kv.Key}\" 개수 == {kv.Value} (실제 {actual})");
            }
            var extraTier = actualTier.Keys.Except(ExpectedTierCounts.Keys).ToList();
            t.Check(extraTier.Count == 0, $"03_meta.md §1.1 표에 없는 tier 값 0건 (실제: {string.Join(",", extraTier)})");
        }
    }
}
