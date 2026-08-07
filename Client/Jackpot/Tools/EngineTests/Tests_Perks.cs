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

        // ── 개수 검증 (89/73/16) — 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14): 증강+9·유물+12 신설. ──
        private static void CheckCounts(TestCtx t)
        {
            t.Check(Perks.Augments.Length == 89, $"Perks.Augments.Length == 89 (실제 {Perks.Augments.Length})");
            t.Check(Perks.AugmentCount == 89, $"Perks.AugmentCount == 89 (실제 {Perks.AugmentCount})");

            t.Check(Perks.Relics.Length == 73, $"Perks.Relics.Length == 73 (실제 {Perks.Relics.Length})");
            t.Check(Perks.RelicCount == 73, $"Perks.RelicCount == 73 (실제 {Perks.RelicCount})");

            t.Check(Perks.Curses.Length == 16, $"Perks.Curses.Length == 16 (실제 {Perks.Curses.Length})");
            t.Check(Perks.CurseCount == 16, $"Perks.CurseCount == 16 (실제 {Perks.CurseCount})");

            t.Check(Perks.All.Count == 178, $"Perks.All.Count == 178 (실제 {Perks.All.Count})");
        }

        // ── id 중복 0 (카테고리별 + 전체 178종 통합) ──
        private static void CheckNoDuplicateIds(TestCtx t)
        {
            CheckDistinctIds(t, "Augments", Perks.Augments.Select(p => p.id));
            CheckDistinctIds(t, "Relics", Perks.Relics.Select(p => p.id));
            CheckDistinctIds(t, "Curses", Perks.Curses.Select(p => p.id));

            var allIds = Perks.Augments.Select(p => p.id)
                .Concat(Perks.Relics.Select(p => p.id))
                .Concat(Perks.Curses.Select(p => p.id));
            CheckDistinctIds(t, "Augments+Relics+Curses(전체 178종)", allIds);
        }

        private static void CheckDistinctIds(TestCtx t, string label, IEnumerable<string> ids)
        {
            var list = ids.ToList();
            var distinct = new HashSet<string>(list);
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(distinct.Count == list.Count, $"{label}: id 중복 0 (중복: {string.Join(",", dupes)})");
        }

        // ── 티어별 개수 — 웹 파리티 P3-4 갱신. 기존 27/33/20(AUG)·28/33/0(RELIC)에 신규 9종(SILVER3·
        // GOLD2·PRISM4, data.js discount/thrifty/item_bag=SILVER·vip/refund=GOLD·crown_burst/curse_grad/
        // extreme_overload/abyss_lore=PRISM)과 신규 12종(전부 PRISM, data.js PRISM 유물 8종+후반 4종)을
        // 더한 값. CURSE는 id/tier 불변(fx/desc만 패널티 전용으로 교체, §1-A #14 항목5).
        private static void CheckTierDistribution(TestCtx t)
        {
            CheckTierCounts(t, "Augments", Perks.Augments, silver: 30, gold: 35, prism: 24);
            CheckTierCounts(t, "Relics", Perks.Relics, silver: 28, gold: 33, prism: 12);
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
        // 없으므로 이 "key" 필드가 엔진 id와 비교할 정규 매핑이다.
        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) — 신규 증강9·유물12는 unity-assets/manifest.json에
        // 아직 아트가 없어(작업 지시 "스프라이트 없는 신규 콘텐츠는 이모지 폴백") catalog.json에 대응
        // 항목이 없다. Tests_Ach.cs가 이미 확립한 선례(업적 34종 vs catalog 16종, "catalog ⊆ engine"
        // 부분집합 검증)를 그대로 따른다 — CURSE는 id 집합이 이번 슬라이스에서 안 바뀌었으므로(fx/desc만
        // 교체) 여전히 전수 일치를 기대할 수 있지만, 3카테고리를 동일 헬퍼로 다루기 위해 함께 부분집합
        // 검증으로 통일한다(전수 일치도 부분집합의 특수케이스이므로 CURSE의 실질 검증력은 그대로 유지).
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

            CrossRefOne(t, "AUGMENT", catalogAug, Perks.Augments.Select(p => p.id), ExpectedNewAugIds);
            CrossRefOne(t, "RELIC", catalogRel, Perks.Relics.Select(p => p.id), ExpectedNewRelIds);
            CrossRefOne(t, "CURSE", catalogCur, Perks.Curses.Select(p => p.id), EmptyAllowlist);
        }

        // 웹 파리티 P3-4에서 catalog.json/manifest.json 아트 없이 엔진에만 추가한 콘텐츠(이 슬라이스가
        // 유일한 출처 — 새 슬라이스가 또 추가하면 이 목록도 함께 갱신해야 한다). CURSE는 id 집합이
        // 이번 슬라이스에서 안 바뀌었으므로(fx/desc만 교체) 빈 allowlist(전수 일치 유지).
        private static readonly HashSet<string> ExpectedNewAugIds = new HashSet<string>
        {
            "discount", "thrifty", "item_bag", "vip", "refund",
            "crown_burst", "curse_grad", "extreme_overload", "abyss_lore",
        };
        private static readonly HashSet<string> ExpectedNewRelIds = new HashSet<string>
        {
            "prism_diploma", "golden_ratio", "starlight_crown", "endless_recess", "fortunes_wheel",
            "set_resonator", "reapers_pact", "phoenix_thesis",
            "crown_monolith", "black_grad_photo", "last_roll", "nameless_cup",
        };
        private static readonly HashSet<string> EmptyAllowlist = new HashSet<string>();

        private static void CrossRefOne(TestCtx t, string label, HashSet<string> catalogIds, IEnumerable<string> engineIdsEnum, HashSet<string> allowedEngineOnly)
        {
            var engineIds = new HashSet<string>(engineIdsEnum);
            // 부분집합 검증(catalog ⊆ engine) — catalog에만 있는 id는 여전히 0이어야 한다(죽은 참조 방지).
            var onlyInCatalog = catalogIds.Except(engineIds).ToList();
            t.Check(onlyInCatalog.Count == 0,
                $"catalog.json {label} key ⊆ Perks id (catalog만: {string.Join(",", onlyInCatalog)})");
            // Opus 2차검수 웹 이탈 정리⑨ — engine-only를 "무제한 허용"하면 앞으로 catalog.json 갱신을
            // 깜빡한 새 콘텐츠도 영영 못 잡는다 — 이번 슬라이스가 만든 신규분만 명시 allowlist로 좁힌다.
            var onlyInEngine = engineIds.Except(catalogIds).ToList();
            var unexpected = onlyInEngine.Except(allowedEngineOnly).ToList();
            t.Check(unexpected.Count == 0,
                $"Perks {label} engine-only id ⊆ P3-4 신규 allowlist (예상 밖: {string.Join(",", unexpected)})");
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

            // 웹 파리티 P3-4 — refund(환불 정책)도 buildMods에 case가 없다(engine.js 전수 확인, "keep"
            // 판정은 game.js useItem이 직접 처리 — ItemUse.Use 동일). Perks.cs 헤더 각주 참조.
            var expected = new HashSet<string> { "fate_bell", "refund" };
            t.Check(new HashSet<string>(empty).SetEquals(expected),
                $"fx가 빈 퍼크 집합 == {{fate_bell,refund}} (실제 {empty.Count}건: {string.Join(",", empty)})");
        }
    }
}
