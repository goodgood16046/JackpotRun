using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S2a 콘텐츠 무결성 테스트 — Perks: Augments(80)/Relics(61)/Curses(16).
    // ENGINE_PORT_DESIGN.md 검증 카테고리 2("콘텐츠 무결성")의 S2a분: 개수·티어분포·id중복 0·
    // catalog.json 교차대조·fx 빈 목록 리포트.
    //
    // ⚠️ TestCtx API 가정: 이 파일 작성 시점에 S1 테스트 하네스(Tools/EngineTests/Program.cs, TestCtx 정의)가
    // 아직 저장소에 없다. Tests_Content2.cs(S2b)가 이미 `TestCtx.Check(bool condition, string message)`를
    // 가장 보수적인 미니 어서션 시그니처로 가정해 두었으므로 동일하게 맞췄다(두 파일이 서로 다른 API를
    // 가정하면 충돌하므로). 실제 S1 TestCtx의 메서드명이 다르면 두 파일의 t.Check(...) 호출부를 일괄
    // 치환하면 된다(그 외 로직은 TestCtx API와 무관).
    public static class Tests_Perks
    {
        public static void Run(TestCtx t)
        {
            CheckCounts(t);
            CheckNoDuplicateIds(t);
            CheckTierDistribution(t);
            CheckCatalogCrossRef(t);
            CheckEmptyFx(t);
        }

        // ── 개수 검증 (80/61/16) ──
        private static void CheckCounts(TestCtx t)
        {
            t.Check(Perks.Augments.Length == 80, $"Perks.Augments.Length == 80 (실제 {Perks.Augments.Length})");
            t.Check(Perks.AugmentCount == 80, $"Perks.AugmentCount == 80 (실제 {Perks.AugmentCount})");

            t.Check(Perks.Relics.Length == 61, $"Perks.Relics.Length == 61 (실제 {Perks.Relics.Length})");
            t.Check(Perks.RelicCount == 61, $"Perks.RelicCount == 61 (실제 {Perks.RelicCount})");

            t.Check(Perks.Curses.Length == 16, $"Perks.Curses.Length == 16 (실제 {Perks.Curses.Length})");
            t.Check(Perks.CurseCount == 16, $"Perks.CurseCount == 16 (실제 {Perks.CurseCount})");

            t.Check(Perks.All.Count == 157, $"Perks.All.Count == 157 (실제 {Perks.All.Count})");
        }

        // ── id 중복 0 (카테고리별 + 전체 157종 통합) ──
        private static void CheckNoDuplicateIds(TestCtx t)
        {
            CheckDistinctIds(t, "Augments", Perks.Augments.Select(p => p.id));
            CheckDistinctIds(t, "Relics", Perks.Relics.Select(p => p.id));
            CheckDistinctIds(t, "Curses", Perks.Curses.Select(p => p.id));

            var allIds = Perks.Augments.Select(p => p.id)
                .Concat(Perks.Relics.Select(p => p.id))
                .Concat(Perks.Curses.Select(p => p.id));
            CheckDistinctIds(t, "Augments+Relics+Curses(전체 157종)", allIds);
        }

        private static void CheckDistinctIds(TestCtx t, string label, IEnumerable<string> ids)
        {
            var list = ids.ToList();
            var distinct = new HashSet<string>(list);
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(distinct.Count == list.Count, $"{label}: id 중복 0 (중복: {string.Join(",", dupes)})");
        }

        // ── 티어별 개수 — 01_engine.md §5.3/5.4/5.5 서술 + catalog.json 실측(80/61/16 전수)으로 교차 확인한 값.
        //    AUGMENT: SILVER 27 / GOLD 33 / PRISM 20.  RELIC: SILVER 28 / GOLD 33 / PRISM 0(유물은 PRISM 없음).
        //    CURSE: GOLD 16(전부 고정, price=0). ──
        private static void CheckTierDistribution(TestCtx t)
        {
            CheckTierCounts(t, "Augments", Perks.Augments, silver: 27, gold: 33, prism: 20);
            CheckTierCounts(t, "Relics", Perks.Relics, silver: 28, gold: 33, prism: 0);
            CheckTierCounts(t, "Curses", Perks.Curses, silver: 0, gold: 16, prism: 0);
        }

        private static void CheckTierCounts(TestCtx t, string label, Perk[] perks, int silver, int gold, int prism)
        {
            int s = perks.Count(p => p.tier == Tier.SILVER);
            int g = perks.Count(p => p.tier == Tier.GOLD);
            int pr = perks.Count(p => p.tier == Tier.PRISM);
            t.Check(s == silver, $"{label}: SILVER 개수 == {silver} (실제 {s})");
            t.Check(g == gold, $"{label}: GOLD 개수 == {gold} (실제 {g})");
            t.Check(pr == prism, $"{label}: PRISM 개수 == {prism} (실제 {pr})");
        }

        // ── catalog.json aug_*/rel_*/cur_* id 교차 대조 ──
        // catalog entry의 "key" 필드 == 엔진 bare id (catalog "id" = "<category>_" + "key", 예:
        // catalog id "aug_study" ↔ 엔진 id "study"). Kotlin AUGMENTS/RELICS/CURSES 자체엔 카테고리 접두사가
        // 없으므로 이 "key" 필드가 엔진 id와 비교할 정규 매핑이다(catalog.json 실측으로 확인, 디바이스와 달리
        // 예외 허용 목록 없음 — 157종 전수 일치가 기대값).
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

            var catalogAug = new HashSet<string>();
            var catalogRel = new HashSet<string>();
            var catalogCur = new HashSet<string>();
            foreach (var e in entries.EnumerateArray())
            {
                var category = e.GetProperty("category").GetString();
                var key = e.GetProperty("key").GetString();
                if (category == "aug") catalogAug.Add(key);
                else if (category == "rel") catalogRel.Add(key);
                else if (category == "cur") catalogCur.Add(key);
            }

            CrossRefOne(t, "AUGMENT", catalogAug, Perks.Augments.Select(p => p.id));
            CrossRefOne(t, "RELIC", catalogRel, Perks.Relics.Select(p => p.id));
            CrossRefOne(t, "CURSE", catalogCur, Perks.Curses.Select(p => p.id));
        }

        private static void CrossRefOne(TestCtx t, string label, HashSet<string> catalogIds, IEnumerable<string> engineIdsEnum)
        {
            var engineIds = new HashSet<string>(engineIdsEnum);
            var onlyInCatalog = catalogIds.Except(engineIds).ToList();
            var onlyInEngine = engineIds.Except(catalogIds).ToList();
            t.Check(onlyInCatalog.Count == 0 && onlyInEngine.Count == 0,
                $"catalog.json {label} key == Perks id (catalog만: {string.Join(",", onlyInCatalog)} / 엔진만: {string.Join(",", onlyInEngine)})");
        }

        // Tools/EngineTests/Tests_Perks.cs 기준 상대경로로 catalog.json을 찾는다(작업 디렉터리 의존 X).
        // Client/Jackpot/Tools/EngineTests -> ../.. -> Client/Jackpot -> Assets/JackpotRun/Resources/JackpotRun/catalog.json
        // (Tests_Content2.cs와 동일한 방식 — CallerFilePath는 컴파일 시점 소스 경로라 빌드 출력 위치에 무관하다.)
        private static string CatalogPath([CallerFilePath] string here = "")
        {
            var dir = Path.GetDirectoryName(here) ?? throw new InvalidOperationException("CallerFilePath 획득 실패");
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json"));
        }

        // ── fx가 빈 퍼크 리포트 ──
        // 원본 buildMods(L1730-2026)에 case가 없는 퍽은 fate_bell(운명의종) 단 1개뿐이다(L1923 주석 —
        // "buildMods 무효과", 실패직전 자동 추가스핀은 서비스가 run.fateBellUsed 게이트로 별도 처리).
        // 그 외 항목이 비어있다면 buildMods case 전사 누락 의심 — 실패로 취급해 바로 드러나게 한다.
        private static void CheckEmptyFx(TestCtx t)
        {
            var empty = Perks.Augments.Concat(Perks.Relics).Concat(Perks.Curses)
                .Where(p => p.fx == null || p.fx.Count == 0)
                .Select(p => p.id)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine(empty.Count == 0
                ? "[Perks] fx가 빈 퍼크: 없음"
                : $"[Perks] fx가 빈 퍼크 ({empty.Count}건): {string.Join(", ", empty)}");

            var expected = new HashSet<string> { "fate_bell" };
            t.Check(new HashSet<string>(empty).SetEquals(expected),
                $"fx가 빈 퍼크 집합 == {{fate_bell}} (실제 {empty.Count}건: {string.Join(",", empty)})");
        }
    }
}
