using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // WEB_PARITY_DESIGN.md P3-2(§1-A #10, §2-C) 콘텐츠 무결성 테스트 — Achievements: 웹 data.js:774-817
    // ACHIEVEMENTS 34종(기본16+후반5+심화13) 전사 검증. 구 482종(기본16+확장466) 시절 테스트를 전면
    // 재작성했다(개수 482→34, 카테고리/티어 분포·면허 lic_* 매핑 검증 등 34종 체계에서 의미 없어진 항목
    // 삭제, 새로 생긴 deep 플래그·장치 보상 매핑을 새 검증으로 대체).
    //
    // ⚠️ TestCtx API: `TestCtx.Check(bool,string)` 별칭을 쓴다(ENGINE_PORT_DESIGN.md 계약 확정 이력).
    public static class Tests_Ach
    {
        public static void Run(TestCtx t)
        {
            CheckCounts(t);
            CheckNoDuplicateIds(t);
            CheckGoldenTable(t);
            CheckCatalogCrossRef(t);
            CheckReqKeysKnown(t);
            CheckDeepFlag(t);
            CheckUniformMetaFields(t);
            CheckDeviceRewardMapping(t);
        }

        // ── 웹 data.js:774-817 ACHIEVEMENTS 34행 손전사 골든 테이블 (Opus 2차 검수 필수②) ────────────
        // Achievements.cs를 베껴 만든 게 아니라 이 파일을 작성하며 public/play/data.js를 다시 열어
        // 독립적으로 옮겨 적은 값이다(id, name, desc, key, th, deep) — 전사 슬라이스(웹 34종 → Unity)의
        // 핵심 회귀망: 이후 누구든 Achievements.cs를 실수로 고치면(오타·키 변경·threshold 변경 등) 이
        // 표와 어긋나 즉시 잡힌다. 순서는 data.js 원문 그대로(기본16 → 후반5 → 심화13).
        private static readonly (string id, string name, string desc, string key, long th, bool deep)[] GoldenTable =
        {
            ("cherry100", "체리 수확가", "🍒체리 누적 100개", "cherryTotal", 100L, false),
            ("cherry500", "체리 중독", "🍒체리 누적 500개", "cherryTotal", 500L, false),
            ("crown10", "왕관 수집가", "👑왕관 누적 10개", "crownTotal", 10L, false),
            ("crown30", "대관식", "👑왕관 누적 30개", "crownTotal", 30L, false),
            ("jackpot1", "첫 잭팟", "5칸 잭팟 1회", "jackpots", 1L, false),
            ("jackpot10", "잭팟 헌터", "5칸 잭팟 10회", "jackpots", 10L, false),
            ("boss1", "중간고사 통과", "보스 1회 클리어", "bossClears", 1L, false),
            ("boss5", "졸업반", "보스 5회 클리어", "bossClears", 5L, false),
            ("stage10", "10층 등반", "스테이지 10 도달", "bestStage", 10L, false),
            ("stage15", "최종보스 도달", "스테이지 15 도달", "bestStage", 15L, false),
            ("lastclear5", "벼락치기 천재", "마지막 스핀 클리어 5회", "lastSpinClears", 5L, false),
            ("exact1", "완벽한 계산", "요구 EXP 정확히 클리어", "exactClears", 1L, false),
            ("prism5", "규칙 파괴자", "프리즘 증강 5회 선택", "prismPicks", 5L, false),
            ("score10k", "만점왕", "최고 점수 10,000", "bestScore", 10000L, false),
            ("score50k", "슬롯의 지배자", "최고 점수 50,000", "bestScore", 50000L, false),
            ("runs20", "단골", "20런 플레이", "runs", 20L, false),
            ("grad1", "졸업생", "스테이지 15 클리어(졸업) 1회", "graduations", 1L, false),
            ("lv20", "베테랑", "플레이어 레벨 20 달성", "playerLevel", 20L, false),
            ("asc3", "심화 3 수료", "심화 학기 3 졸업", "ascMax", 3L, false),
            ("asc5", "심화 5 석사", "심화 학기 5 졸업", "ascMax", 5L, false),
            ("lv40", "고인물", "플레이어 레벨 40 달성", "playerLevel", 40L, false),
            ("d_ach_start", "심볼연구 시작", "[심화] 심화모드 첫 플레이", "deepRuns", 1L, true),
            ("d_ach_compress1", "첫 압축", "[심화] 총량 27↓로 스테이지 클리어", "deepCompress95", 1L, true),
            ("d_ach_risk_compress", "위험한 압축", "[심화] 총량 85↓로 보스 클리어", "deepCompress85Boss", 1L, true),
            ("d_ach_big_pouch", "대형 주머니", "[심화] 총량 36↑ 달성", "deepMaxTotal", 36L, true),
            ("d_ach_cherry_major", "체리 전공", "[심화] 체리 계열(🍒+🍑) 비중 50%↑로 보스 클리어", "deepCherry50Boss", 1L, true),
            ("d_ach_curse_major", "저주 전공", "[심화] 해골 40%↑로 보스 클리어", "deepSkull40Boss", 1L, true),
            ("d_ach_gem_major", "보석 전공", "[심화] 보석 계열(💎+💠) 비중 50%↑·점수 3만↑ 보스 클리어", "deepGem50Score30k", 1L, true),
            ("d_ach_crown", "왕관 연구", "[심화] 주머니 왕관 2개로 보스 클리어", "deepCrown2Boss", 1L, true),
            ("d_ach_balance", "완벽한 균형", "[심화] 모든 태그 20%↓ 균형으로 보스 클리어", "deepBalanceBoss", 1L, true),
            ("d_ach_purifier", "정화자", "[심화] 주머니 해골 0으로 보스 클리어", "deepSkull0Boss", 1L, true),
            ("d_ach_rare10", "희귀 수집가", "[심화] 희귀 등급 심볼 6종 발견", "deepRaresSeen", 6L, true),
            ("d_ach_legend5", "전설 연구자", "[심화] 전설 등급 심볼 3종 발견", "deepLegendsSeen", 3L, true),
            ("d_ach_master", "심볼 마스터", "[심화] 심화모드 보스 통산 10회 클리어", "deepBossClears", 10L, true),
        };

        private static void CheckGoldenTable(TestCtx t)
        {
            t.Check(GoldenTable.Length == 34, $"골든 테이블(웹 data.js 손전사) 행 개수 == 34 (실제 {GoldenTable.Length})");

            var byId = Achievements.All.ToDictionary(a => a.id);
            var goldenIds = new HashSet<string>();
            var dupGolden = new HashSet<string>();
            foreach (var g in GoldenTable)
            {
                if (!goldenIds.Add(g.id)) dupGolden.Add(g.id);

                if (!byId.TryGetValue(g.id, out var a))
                {
                    t.Check(false, $"[golden] 골든 테이블 id \"{g.id}\"가 Achievements.All에 없음");
                    continue;
                }
                t.Check(a.name == g.name, $"[golden] {g.id}.name == \"{g.name}\" (실제 \"{a.name}\")");
                t.Check(a.desc == g.desc, $"[golden] {g.id}.desc == \"{g.desc}\" (실제 \"{a.desc}\")");
                bool reqOk = a.req != null && a.req.Length == 1;
                t.Check(reqOk && a.req[0].key == g.key,
                    $"[golden] {g.id}.req[0].key == \"{g.key}\" (실제 {(reqOk ? a.req[0].key : "req 형태 이상")})");
                t.Check(reqOk && a.req[0].value == g.th,
                    $"[golden] {g.id}.req[0].value == {g.th} (실제 {(reqOk ? a.req[0].value.ToString() : "req 형태 이상")})");
                t.Check(a.deep == g.deep, $"[golden] {g.id}.deep == {g.deep} (실제 {a.deep})");
            }
            t.Check(dupGolden.Count == 0, $"[golden] 골든 테이블 자체 id 중복 0 (중복: {string.Join(",", dupGolden)})");

            // 반대 방향(전수 대조) — Achievements.All에만 있고 골든 테이블에 없는 id, 또는 그 반대가
            // 0건이어야 한다(34종 그 무엇도 빠지거나 더해지지 않았는지).
            var engineIds = new HashSet<string>(Achievements.All.Select(a => a.id));
            var onlyInEngine = engineIds.Except(goldenIds).ToList();
            var onlyInGolden = goldenIds.Except(engineIds).ToList();
            t.Check(onlyInEngine.Count == 0 && onlyInGolden.Count == 0,
                $"[golden] Achievements.All id 집합 == 골든 테이블 id 집합 (엔진만: {string.Join(",", onlyInEngine)} / 골든만: {string.Join(",", onlyInGolden)})");
        }

        // ── 개수 검증 (기본 16 / 후반 5 / 심화 13 / 합계 34) ──
        private static void CheckCounts(TestCtx t)
        {
            t.Check(Achievements.All.Length == 34, $"Achievements.All.Length == 34 (실제 {Achievements.All.Length})");
            t.Check(Achievements.Count == 34, $"Achievements.Count == 34 (실제 {Achievements.Count})");
            t.Check(Achievements.BaseCount == 16, $"Achievements.BaseCount == 16 (실제 {Achievements.BaseCount})");
            t.Check(Achievements.LateCount == 5, $"Achievements.LateCount == 5 (실제 {Achievements.LateCount})");
            t.Check(Achievements.DeepCount == 13, $"Achievements.DeepCount == 13 (실제 {Achievements.DeepCount})");
            t.Check(16 + 5 + 13 == 34, "16+5+13 == 34 (자기검증)");

            // 배열 순서도 웹 data.js와 동일해야 한다: [0..16)=기본, [16..21)=후반, [21..34)=심화.
            var baseIds = new HashSet<string>
            {
                "cherry100", "cherry500", "crown10", "crown30", "jackpot1", "jackpot10",
                "boss1", "boss5", "stage10", "stage15", "lastclear5", "exact1", "prism5",
                "score10k", "score50k", "runs20",
            };
            var actualBaseIds = new HashSet<string>(Achievements.All.Take(16).Select(a => a.id));
            t.Check(actualBaseIds.SetEquals(baseIds),
                $"Achievements.All[0..16) id 집합 == 기본 16종 (실제: {string.Join(",", actualBaseIds)})");

            var lateIds = new HashSet<string> { "grad1", "lv20", "asc3", "asc5", "lv40" };
            var actualLateIds = new HashSet<string>(Achievements.All.Skip(16).Take(5).Select(a => a.id));
            t.Check(actualLateIds.SetEquals(lateIds),
                $"Achievements.All[16..21) id 집합 == 후반 5종 (실제: {string.Join(",", actualLateIds)})");

            var deepIds = new HashSet<string>
            {
                "d_ach_start", "d_ach_compress1", "d_ach_risk_compress", "d_ach_big_pouch",
                "d_ach_cherry_major", "d_ach_curse_major", "d_ach_gem_major", "d_ach_crown",
                "d_ach_balance", "d_ach_purifier", "d_ach_rare10", "d_ach_legend5", "d_ach_master",
            };
            var actualDeepIds = new HashSet<string>(Achievements.All.Skip(21).Select(a => a.id));
            t.Check(actualDeepIds.SetEquals(deepIds),
                $"Achievements.All[21..34) id 집합 == 심화 13종 (실제: {string.Join(",", actualDeepIds)})");
        }

        // ── id 중복 0 (34종 통합) ──
        private static void CheckNoDuplicateIds(TestCtx t)
        {
            var list = Achievements.All.Select(a => a.id).ToList();
            var distinct = new HashSet<string>(list);
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(distinct.Count == list.Count, $"Achievements: id 중복 0 (중복: {string.Join(",", dupes)})");
        }

        // ── catalog.json ach_* 16종 교차 대조 — 34종 중 아트가 있는 기본 16종만 catalog에 있다(부분집합).
        // catalog entry: category=="ach", id="ach_<achId>", key=="<achId>"(엔진 bare id).
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

            var engineIds = new HashSet<string>(Achievements.All.Select(a => a.id));
            var engineBaseIds = new HashSet<string>(Achievements.All.Take(16).Select(a => a.id));

            // 34종 교체 후에도 catalog.json(아트 있는 16종)은 그대로다 — 이 16종은 여전히 새 34종
            // 안에 부분집합으로 존재해야 한다(정확히 옛 "기본 16"과 id가 1:1 일치 — data.js도 그대로
            // 이식했으므로 동일해야 정상).
            var onlyInCatalog = catalogAchIds.Except(engineIds).ToList();
            t.Check(onlyInCatalog.Count == 0,
                $"catalog.json ach key가 전부 Achievements.All(34종) 안에 존재 (catalog에만 있는 것: {string.Join(",", onlyInCatalog)})");
            t.Check(catalogAchIds.SetEquals(engineBaseIds),
                $"catalog.json ach 16종 id 집합 == 엔진 기본 16종 id 집합 (일치 여부만 검증, 새 후반5/심화13은 아트 없음이 정상)");
            t.Check(catalogAchIds.Count == 16, $"catalog.json ach 항목 개수 == 16 (실제 {catalogAchIds.Count})");
        }

        // Tools/EngineTests/Tests_Ach.cs 기준 상대경로로 catalog.json을 찾는다(작업 디렉터리 의존 X).
        private static string CatalogPath([CallerFilePath] string here = "")
        {
            var dir = Path.GetDirectoryName(here) ?? throw new InvalidOperationException("CallerFilePath 획득 실패");
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json"));
        }

        // ── req 스탯 키가 "실존 카운터"(StatTracker가 실제로 수집) 또는 "예약 키"(P6/P7 대비, 아직
        // 카운터 없음)인지 확인. 작업 지시 7번 "불일치가 있으면 우회하지 말고 보고" — 하드 실패가 아니라
        // Report로 남긴다(기존 CheckStatKeysInDictionary와 동일한 관용 원칙, 사전 밖 키가 있어도 실패
        // 취급하지 않음 — 단 이 사전은 34종 전용으로 새로 구성했다).
        private static readonly HashSet<string> KnownCounterKeys = new HashSet<string>
        {
            // 실제 StatTracker.ApplyClearTracking/ApplyGameOverTracking이 setMax/inc하는 34종 관련 원시 키.
            "cherryTotal", "crownTotal", "jackpots", "bossClears", "bestStage",
            "lastSpinClears", "exactClears", "prismPicks", "bestScore", "runs",
            // WEB_PARITY P3-2로 새로 추가된 원시 키(StatTracker가 직접 기록, Content/StatTracker.cs 참조).
            "graduations", "playerLevel",
        };

        private static readonly HashSet<string> ReservedFutureKeys = new HashSet<string>
        {
            // P6(승천) 대비 — Unity에 승천 시스템이 아직 없어 영원히 0(asc3/asc5 영구 미달성, 데이터만 보존).
            "ascMax",
            // P7(심화모드) 대비 — 심화 13종 전부 deep=true, StatTracker가 아직 수집하지 않는 deep* 카운터.
            "deepRuns", "deepCompress95", "deepCompress85Boss", "deepMaxTotal", "deepCherry50Boss",
            "deepSkull40Boss", "deepGem50Score30k", "deepCrown2Boss", "deepBalanceBoss", "deepSkull0Boss",
            "deepRaresSeen", "deepLegendsSeen", "deepBossClears",
        };

        private static void CheckReqKeysKnown(TestCtx t)
        {
            t.Check(KnownCounterKeys.Count + ReservedFutureKeys.Count == 26,
                $"실존 카운터(12) + 예약 키(14) 사전 크기 자기검증 (실제 {KnownCounterKeys.Count + ReservedFutureKeys.Count})");

            var allKnown = new HashSet<string>(KnownCounterKeys);
            allKnown.UnionWith(ReservedFutureKeys);

            var outside = Achievements.All
                .Where(a => a.req != null && a.req.Length > 0)
                .Select(a => a.req[0].key)
                .Distinct()
                .Where(k => !allKnown.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            t.Report("Achievements req 스탯 키 사전 밖 목록", outside.Count == 0
                ? "없음 (34종 전부 실존 카운터 또는 예약 키로 설명됨)"
                : $"{outside.Count}건: {string.Join(", ", outside)}");
            t.Check(outside.Count == 0, $"Achievements req 키가 전부 실존 카운터/예약 키 사전 안에 포함 (밖: {string.Join(",", outside)})");

            // req는 정확히 1개씩만 있어야 한다(웹 {key, th} 단일 쌍).
            var badReqShape = Achievements.All.Where(a => a.req == null || a.req.Length != 1).Select(a => a.id).ToList();
            t.Check(badReqShape.Count == 0, $"모든 업적의 req는 정확히 1원소 (위반: {string.Join(",", badReqShape)})");

            // deep achievements의 key가 실제로 deep* 예약 키를 가리키는지(작업 지시 1번 "deep 플래그 보존"
            // 취지 — key/deep 플래그 불일치 시 향후 P7 이식이 헷갈린다).
            var deepKeyMismatch = Achievements.All.Where(a => a.deep && a.req != null && a.req.Length > 0
                && !a.req[0].key.StartsWith("deep", StringComparison.Ordinal)).Select(a => a.id).ToList();
            t.Check(deepKeyMismatch.Count == 0, $"deep=true 업적의 req.key는 전부 \"deep\" 접두 (위반: {string.Join(",", deepKeyMismatch)})");
        }

        // ── deep 플래그 — 심화 13종만 true, 나머지 21종은 false ──
        private static void CheckDeepFlag(TestCtx t)
        {
            int deepTrueCount = Achievements.All.Count(a => a.deep);
            t.Check(deepTrueCount == 13, $"deep==true 개수 == 13 (실제 {deepTrueCount})");

            var deepTrueIds = Achievements.All.Where(a => a.deep).Select(a => a.id).ToList();
            bool allDAchPrefix = deepTrueIds.All(id => id.StartsWith("d_ach_", StringComparison.Ordinal));
            t.Check(allDAchPrefix, $"deep==true 업적은 전부 \"d_ach_\" 접두 id (실제: {string.Join(",", deepTrueIds)})");

            int nonDeepWithPrefix = Achievements.All.Count(a => !a.deep && a.id.StartsWith("d_ach_", StringComparison.Ordinal));
            t.Check(nonDeepWithPrefix == 0, $"\"d_ach_\" 접두인데 deep==false인 업적 0건 (실제 {nonDeepWithPrefix})");
        }

        // ── cat/tier/hidden/reward 균일 기본값 — 웹에는 이 4개 개념이 없어 34종 전부 구 "기본 16"과
        // 동일한 균일값(cat="기타", tier="브론즈", hidden=false, reward="")을 채웠다(Achievements.cs 헤더
        // 각주). 이 테스트는 그 설계 결정이 실제로 34종 전부에 일관되게 적용됐는지 확인한다.
        private static void CheckUniformMetaFields(TestCtx t)
        {
            int badCat = Achievements.All.Count(a => a.cat != "기타");
            int badTier = Achievements.All.Count(a => a.tier != "브론즈");
            int badHidden = Achievements.All.Count(a => a.hidden);
            int badReward = Achievements.All.Count(a => !string.IsNullOrEmpty(a.reward));
            t.Check(badCat == 0, $"cat==\"기타\" 34종 전부 (위반 {badCat}건)");
            t.Check(badTier == 0, $"tier==\"브론즈\" 34종 전부 (위반 {badTier}건)");
            t.Check(badHidden == 0, $"hidden==false 34종 전부 (위반 {badHidden}건)");
            t.Check(badReward == 0, $"reward==\"\" 34종 전부 (위반 {badReward}건)");
        }

        // ── 웹 ACH_DEVICE_REWARD(data.js:818-828) 기본 12건 매핑 무결성 — Devices.cs 12종의 unlockAch가
        // 새 34종 id를 정확히 가리키고, 그 id가 Achievements.All에 실존하는지 확인. 심화 9건은 대응
        // 장치가 Devices.cs에 없어(P7 전용 신규 장치) 검증 대상이 아니다(Achievements.cs 헤더 각주 참조).
        private static readonly Dictionary<string, string> ExpectedDeviceReward = new Dictionary<string, string>
        {
            ["jackpot1"] = "dev_subreel", ["boss1"] = "dev_reroll", ["crown10"] = "dev_seal",
            ["cherry100"] = "dev_safe", ["exact1"] = "dev_pin", ["lastclear5"] = "dev_overheat",
            ["score10k"] = "dev_coin", ["stage10"] = "dev_oracle", ["prism5"] = "dev_copy",
            ["boss5"] = "dev_swap", ["runs20"] = "dev_bell", ["score50k"] = "dev_flame",
        };

        private static void CheckDeviceRewardMapping(TestCtx t)
        {
            t.Check(ExpectedDeviceReward.Count == 12, $"기본 장치 보상 매핑 개수 == 12 (실제 {ExpectedDeviceReward.Count})");

            var engineIds = new HashSet<string>(Achievements.All.Select(a => a.id));
            var deviceIds = new HashSet<string>(Devices.All.Select(d => d.id));

            foreach (var kv in ExpectedDeviceReward)
            {
                t.Check(engineIds.Contains(kv.Key), $"장치 보상 매핑의 업적 id \"{kv.Key}\"가 Achievements.All(34종) 안에 실존");
                t.Check(deviceIds.Contains(kv.Value), $"장치 보상 매핑의 장치 id \"{kv.Value}\"가 Devices.All 안에 실존");

                var dev = Devices.ById(kv.Value);
                t.Check(dev != null && dev.unlockAch == kv.Key,
                    $"Devices.ById(\"{kv.Value}\").unlockAch == \"{kv.Key}\" (실제 {(dev != null ? dev.unlockAch : "장치 없음")})");
            }

            // 반대 방향: Devices.All 중 unlockAch가 새 34종 id를 가리키는 장치는 정확히 이 12개뿐이어야
            // 한다(dev_syllabus/dev_holdfile/dev_retake/dev_major 4종은 구 확장 업적 id를 그대로 가리켜서
            // 34종 어디에도 매치되지 않는 게 의도된 동작 — WEB_PARITY_DESIGN.md §1-B, Devices.cs 헤더 각주).
            var matchedDevices = Devices.All.Where(d => engineIds.Contains(d.unlockAch)).Select(d => d.id).ToList();
            t.Check(new HashSet<string>(matchedDevices).SetEquals(ExpectedDeviceReward.Values),
                $"Devices.All 중 unlockAch가 34종 업적을 가리키는 장치 == 기본 12종 정확히 일치 (실제: {string.Join(",", matchedDevices.OrderBy(x => x, StringComparer.Ordinal))})");
        }
    }
}
