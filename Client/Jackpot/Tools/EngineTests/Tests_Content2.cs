using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S2b 콘텐츠 무결성 테스트 — Items(73)/Devices(16)/Sets(33)/Schools(10)/PERK_GATE_OVERRIDES(45).
    // ENGINE_PORT_DESIGN.md 검증 카테고리 2("콘텐츠 무결성")의 S2b분: 개수·id중복 0·catalog.json 교차대조.
    //
    // ⚠️ TestCtx API 가정: 이 파일이 작성되는 시점에 S1 테스트 하네스(Tools/EngineTests/Program.cs, TestCtx
    // 정의)가 아직 저장소에 없다(S2b가 S1보다 먼저 구현됨). 아래는 `TestCtx.Check(bool condition, string message)`
    // — 조건이 참이면 통과, 거짓이면 message와 함께 실패 기록 — 라는 가장 보수적인 미니 어서션 시그니처를
    // 가정하고 작성했다. 실제 S1 TestCtx의 메서드명이 다르면(Assert 등) 이 파일의 `t.Check(...)` 호출부만
    // 일괄 치환하면 된다(그 외 로직은 TestCtx API와 무관).
    // ⚠️ 같은 이유로 Characters/Machines(S1 Content 산출물)도 컴파일 시점에 `{id,name,...}[] All` 형태로
    // 존재한다고 가정하고 참조만 한다(Perks와 동일 원칙 — 생성·수정 금지).
    public static class Tests_Content2
    {
        public static void Run(TestCtx t)
        {
            CheckCounts(t);
            CheckNoDuplicateIds(t);
            CheckCatalogCrossRef(t);
            CheckSetsRequireRealPerkIds(t);
            CheckPerkGateOverrideSchoolsAreValid(t);
            CheckSetGateFields(t);
        }

        // ── 개수 검증 (73/16/33/10/45) ──
        private static void CheckCounts(TestCtx t)
        {
            t.Check(Items.All.Length == 73, $"Items.All.Length == 73 (실제 {Items.All.Length})");
            t.Check(Items.Count == 73, $"Items.Count == 73 (실제 {Items.Count})");

            t.Check(Devices.All.Length == 16, $"Devices.All.Length == 16 (실제 {Devices.All.Length})");
            t.Check(Devices.Count == 16, $"Devices.Count == 16 (실제 {Devices.Count})");

            t.Check(Sets.All.Length == 33, $"Sets.All.Length == 33 (실제 {Sets.All.Length})");
            t.Check(Sets.Count == 33, $"Sets.Count == 33 (실제 {Sets.Count})");

            t.Check(Schools.All.Length == 10, $"Schools.All.Length == 10 (실제 {Schools.All.Length})");
            t.Check(Schools.SchoolCount == 10, $"Schools.SchoolCount == 10 (실제 {Schools.SchoolCount})");
            t.Check(Schools.SchoolReq.Count == 10, $"Schools.SchoolReq.Count == 10 (실제 {Schools.SchoolReq.Count})");
            t.Check(Schools.SchoolResearch.Count == 10, $"Schools.SchoolResearch.Count == 10 (실제 {Schools.SchoolResearch.Count})");

            t.Check(Schools.PerkGateOverrides.Count == 45, $"Schools.PerkGateOverrides.Count == 45 (실제 {Schools.PerkGateOverrides.Count})");
            t.Check(Schools.PerkGateOverrideCount == 45, $"Schools.PerkGateOverrideCount == 45 (실제 {Schools.PerkGateOverrideCount})");
        }

        // ── id 중복 0 ──
        private static void CheckNoDuplicateIds(TestCtx t)
        {
            CheckDistinctIds(t, "Items", Items.All.Select(x => x.id));
            CheckDistinctIds(t, "Devices", Devices.All.Select(x => x.id));
            CheckDistinctIds(t, "Sets", Sets.All.Select(x => x.id));
        }

        private static void CheckDistinctIds(TestCtx t, string label, IEnumerable<string> ids)
        {
            var list = ids.ToList();
            var distinct = new HashSet<string>(list);
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(distinct.Count == list.Count, $"{label}: id 중복 0 (중복: {string.Join(",", dupes)})");
        }

        // ── catalog.json 교차 대조 (item_*/dev_* id) ──
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

            var catalogItemKeys = new HashSet<string>();
            var catalogDevIds = new HashSet<string>();
            foreach (var e in entries.EnumerateArray())
            {
                var category = e.GetProperty("category").GetString();
                if (category == "item")
                {
                    catalogItemKeys.Add(e.GetProperty("key").GetString());
                }
                else if (category == "dev")
                {
                    catalogDevIds.Add(e.GetProperty("id").GetString());
                }
            }

            var engineItemIds = new HashSet<string>(Items.All.Select(x => x.id));
            var engineDevIds = new HashSet<string>(Devices.All.Select(x => x.id));

            var itemOnlyInCatalog = catalogItemKeys.Except(engineItemIds).ToList();
            var itemOnlyInEngine = engineItemIds.Except(catalogItemKeys).ToList();
            t.Check(itemOnlyInCatalog.Count == 0 && itemOnlyInEngine.Count == 0,
                $"catalog.json item_* id == Items.All id (catalog만: {string.Join(",", itemOnlyInCatalog)} / 엔진만: {string.Join(",", itemOnlyInEngine)})");

            var devOnlyInCatalog = catalogDevIds.Except(engineDevIds).ToList();
            var devOnlyInEngine = engineDevIds.Except(catalogDevIds).ToList();
            t.Check(devOnlyInCatalog.Count == 0 && devOnlyInEngine.Count == 0,
                $"catalog.json dev_* id == Devices.All id (catalog만: {string.Join(",", devOnlyInCatalog)} / 엔진만: {string.Join(",", devOnlyInEngine)})");
        }

        // Tools/EngineTests/Tests_Content2.cs 기준 상대경로로 catalog.json을 찾는다(작업 디렉터리 의존 X).
        // Client/Jackpot/Tools/EngineTests -> ../.. -> Client/Jackpot -> Assets/JackpotRun/Resources/JackpotRun/catalog.json
        private static string CatalogPath([CallerFilePath] string here = "")
        {
            var dir = Path.GetDirectoryName(here) ?? throw new InvalidOperationException("CallerFilePath 획득 실패");
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json"));
        }

        // ── Sets.requires가 실존 perk id인지 (S2a Perks 참조, 컴파일 시점에 존재 가정 — 참조만) ──
        // Perks.ById(id)를 직접 쓴다(Perks.All이 배열인지 딕셔너리인지 내부 표현에 의존하지 않기 위함 —
        // 실제 S2a 구현은 IReadOnlyDictionary<string,Perk>).
        private static void CheckSetsRequireRealPerkIds(TestCtx t)
        {
            foreach (var s in Sets.All)
            {
                var missing = s.requires.Where(r => Perks.ById(r) == null).ToList();
                t.Check(missing.Count == 0, $"Sets[{s.id}].requires ⊆ Perks (미존재: {string.Join(",", missing)})");
            }
        }

        // ── PERK_GATE_OVERRIDES의 school 값이 SCHOOL_REQ에 정의된 10종 안에 있는지(부가 무결성) ──
        private static void CheckPerkGateOverrideSchoolsAreValid(TestCtx t)
        {
            var validSchools = new HashSet<string>(Schools.All);
            foreach (var kv in Schools.PerkGateOverrides)
            {
                t.Check(validSchools.Contains(kv.Value.school), $"PerkGateOverrides[{kv.Key}].school '{kv.Value.school}'이 Schools.All 10종 안에 있음");
            }
            foreach (var kv in Schools.SchoolReq)
            {
                t.Check(validSchools.Contains(kv.Value.school), $"SchoolReq[{kv.Key}].school '{kv.Value.school}'이 Schools.All 10종 안에 있음");
            }
        }

        // ── Sets 게이트 필드(reqChar/reqMachine/reqDevice) 검증 — Fable 최종검수 지시(ContentTypes.cs SetEffect
        // 계약 확장 반영). Kotlin SlotV2Engine.kt SETS(L577-610) 기준 게이트 있는 세트는 정확히 14종
        // (아래 GatedSetIds). 두 가지를 검증한다:
        //  1) 게이트 유무가 이 14종 id 집합과 정확히 일치하는지(과다/누락 0건).
        //  2) 값이 채워진 reqChar/reqMachine/reqDevice가 실존 Characters/Machines/Devices id인지.
        // Characters/Machines는 S1 산출물이라 컴파일 시점에 존재한다고 가정하고 참조만 한다(생성·수정 금지,
        // Perks 참조와 동일 원칙). Devices는 본 슬라이스(S2b)가 직접 소유하므로 바로 참조.
        private static readonly string[] GatedSetIds =
        {
            "set_cherry_net", "set_red_harvest", "set_lib_bless", "set_glory_grad", "set_skull_lab",
            "set_black_grad", "set_curse_cycle", "set_crown_rite", "set_kings_order", "set_flame_lab",
            "set_mechanic", "set_gambler", "set_scholarship", "set_bomb_calc",
        };

        private static void CheckSetGateFields(TestCtx t)
        {
            var expectedGated = new HashSet<string>(GatedSetIds);
            t.Check(GatedSetIds.Length == 14, $"게이트 있는 세트는 정확히 14종 (실제 {GatedSetIds.Length})");

            var charIds = new HashSet<string>(Characters.All.Select(c => c.id));
            var machineIds = new HashSet<string>(Machines.All.Select(m => m.id));
            var deviceIds = new HashSet<string>(Devices.All.Select(d => d.id));

            var actualGated = new List<string>();
            foreach (var s in Sets.All)
            {
                bool hasChar = !string.IsNullOrEmpty(s.reqChar);
                bool hasMachine = !string.IsNullOrEmpty(s.reqMachine);
                bool hasDevice = !string.IsNullOrEmpty(s.reqDevice);
                bool hasGate = hasChar || hasMachine || hasDevice;
                if (hasGate) actualGated.Add(s.id);

                bool shouldHaveGate = expectedGated.Contains(s.id);
                t.Check(hasGate == shouldHaveGate,
                    $"Sets[{s.id}] 게이트 유무 == 기대값 (기대 {shouldHaveGate}, 실제 {hasGate})");

                if (hasChar)
                    t.Check(charIds.Contains(s.reqChar), $"Sets[{s.id}].reqChar '{s.reqChar}'가 Characters.All 실존 id");
                if (hasMachine)
                    t.Check(machineIds.Contains(s.reqMachine), $"Sets[{s.id}].reqMachine '{s.reqMachine}'가 Machines.All 실존 id");
                if (hasDevice)
                    t.Check(deviceIds.Contains(s.reqDevice), $"Sets[{s.id}].reqDevice '{s.reqDevice}'가 Devices.All 실존 id");
            }

            var actualGatedSet = new HashSet<string>(actualGated);
            var missing = expectedGated.Where(x => !actualGatedSet.Contains(x)).ToList();
            var extra = actualGatedSet.Where(x => !expectedGated.Contains(x)).ToList();
            t.Check(missing.Count == 0 && extra.Count == 0,
                $"게이트 있는 세트 14종 id 목록 정확히 일치 (누락: {string.Join(",", missing)} / 초과: {string.Join(",", extra)})");
        }
    }
}
