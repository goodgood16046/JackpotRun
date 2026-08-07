using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // Tests_Fx — 콘텐츠 "수치" 회귀 스냅샷 테스트 (Opus 1차검수 지적사항 반영).
    //
    // 배경: 기존 콘텐츠 무결성 테스트(Tests_Perks.cs/Tests_Content2.cs)는 개수·id중복·catalog.json
    // 교차대조·게이트 유무만 검증한다. fx 딕셔너리 안의 실제 수치(예: expMul=1.10 이 1.01로 오타나는
    // 경우)는 어느 기존 테스트도 잡지 못한다 — "그물망에 구멍"이 있는 상태였다. 이 파일은 퍼크 157종·
    // 아이템 73종·장치 16종·세트 33종의 fx 수치를 고정 스냅샷(해시) 또는 명시값으로 박아 대조해
    // 회귀(의도치 않은 수치 변경)를 감지한다. 캐릭터 16종·스쿨 10종은 수치가 적어 스냅샷 없이
    // 명시값 전수 대조로 처리한다.
    //
    // 해시 방식: FNV-1a 64bit를 이 파일 안에서 직접 구현해 사용한다(object.GetHashCode()/문자열
    // GetHashCode()는 실행마다·프로세스마다 값이 달라질 수 있어(.NET 문자열 해시 랜덤화) 소스에 박아
    // 넣는 영속 스냅샷 상수로 쓸 수 없다 — 반드시 결정적이고 자체구현된 해시가 필요하다).
    //   · fx 캐노니컬 문자열 = fx 딕셔너리를 key 기준 오디널(Ordinal) 정렬 후 "key=value"를 ";"로 조인.
    //     value는 "R"(round-trip) 포맷으로 직렬화해 부동소수 값을 정밀도 손실 없이 재현 가능한 문자열로 만든다.
    //     빈 딕셔너리(또는 null) → 빈 문자열("") → FNV 오프셋베이시스 그대로(해시 0xCBF29CE484222325).
    //   · 퍼크 meta 캐노니컬 문자열 = {name,emoji,desc,price,tier,school} 6개 필드를 동일 규칙(키 오디널
    //     정렬 후 "key=value" ";" 조인)으로 만든다.
    //
    // 스냅샷 상수의 근거: 이 표는 "Kotlin 원본이 이래야 한다"가 아니라 "2026-07-30 시점 C# 코드가
    // 실제로 내는 값"을 그대로 얼려 놓은 것이다 — 그 시점 값의 정확성은 같은 날 Opus 1차검수가
    // Perks.cs/Items.cs/Devices.cs/Sets.cs 전 항목을 Kotlin 원본(SlotV2Engine.kt)과 전수 대조해
    // 확증했다(WORKLOG.md 참조). 목적은 원본과의 최초 일치 확인이 아니라 "이후 수치가 바뀌면 바로
    // 드러나게" 하는 회귀 방지이므로, 값은 실제 소스(Content/*.cs)에서 FNV-1a로 직접 계산해 얻었다
    // (수동 계산 아님 — 스크래치의 임시 콘솔 생성기로 Core+Content만 컴파일해 뽑았다. 생성기는 결과물이
    // 아니므로 저장소에 남기지 않았다).
    // 예외 — 캐릭터 16종(§CheckCharacterExactValues)만은 스냅샷이 아니라 kotlin-reference\game\
    // SlotV2Engine.kt CHARS(L316-363)와 직접 대조한 명시값이다(작업 지시서 요구사항).
    public static class Tests_Fx
    {
        public static void Run(TestCtx t)
        {
            CheckPerkFxSnapshots(t);
            CheckPerkMetaSnapshots(t);
            CheckItemExactValues(t);
            CheckItemFxSnapshots(t);
            CheckDeviceExactValues(t);
            CheckDeviceFxSnapshots(t);
            CheckSetExactValues(t);
            CheckSetFxSnapshots(t);
            CheckCharacterExactValues(t);
            CheckSchoolReqExactValues(t);
        }

        // ═══════════════════════ FNV-1a 64bit (자체구현 — GetHashCode 금지) ═══════════════════════
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong Fnv1a(string s)
        {
            ulong hash = FnvOffset;
            var bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }
            return hash;
        }

        private static string FxCanon(IReadOnlyDictionary<string, double> fx)
        {
            if (fx == null || fx.Count == 0) return "";
            return string.Join(";", fx.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static ulong FxHash(IReadOnlyDictionary<string, double> fx) => Fnv1a(FxCanon(fx));

        private static string MetaCanon(string name, string emoji, string desc, int price, string tier, string school)
        {
            var d = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = name ?? "",
                ["emoji"] = emoji ?? "",
                ["desc"] = desc ?? "",
                ["price"] = price.ToString(CultureInfo.InvariantCulture),
                ["tier"] = tier ?? "",
                ["school"] = school ?? "",
            };
            return string.Join(";", d.Select(kv => kv.Key + "=" + kv.Value));
        }

        private static ulong MetaHash(Perk p) =>
            Fnv1a(MetaCanon(p.name, p.emoji, p.desc, p.price, p.tier.ToString(), p.school));

        // ═══════════════════════ ① 퍼크 157종 fx 스냅샷 ═══════════════════════
        private static void CheckPerkFxSnapshots(TestCtx t)
        {
            t.Eq(ExpectedPerkFxHash.Count, Perks.All.Count, "퍼크 fx 스냅샷 테이블 크기 == Perks.All.Count(157)");
            foreach (var p in Perks.All.Values)
            {
                if (!ExpectedPerkFxHash.TryGetValue(p.id, out var expected))
                {
                    t.Fail($"Perks[{p.id}].fx", "fx 스냅샷 테이블에 항목 없음(신규 퍼크 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(expected, FxHash(p.fx), $"Perks[{p.id}].fx 스냅샷 해시");
            }
        }

        // ═══════════════════════ ② 퍼크 157종 name/emoji/desc/price/tier/school 스냅샷 ═══════════════════════
        private static void CheckPerkMetaSnapshots(TestCtx t)
        {
            t.Eq(ExpectedPerkMetaHash.Count, Perks.All.Count, "퍼크 meta 스냅샷 테이블 크기 == Perks.All.Count(157)");
            foreach (var p in Perks.All.Values)
            {
                if (!ExpectedPerkMetaHash.TryGetValue(p.id, out var expected))
                {
                    t.Fail($"Perks[{p.id}].meta", "meta 스냅샷 테이블에 항목 없음(신규 퍼크 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(expected, MetaHash(p), $"Perks[{p.id}].(name/emoji/desc/price/tier/school) 스냅샷 해시");
            }
        }

        // ═══════════════════════ ③ 아이템 73종 — coinCost·kind 명시 대조 + fx 스냅샷 ═══════════════════════
        private static void CheckItemExactValues(TestCtx t)
        {
            t.Eq(ExpectedItemExact.Count, Items.All.Length, "아이템 exact 테이블 크기 == Items.All.Length(73)");
            foreach (var it in Items.All)
            {
                if (!ExpectedItemExact.TryGetValue(it.id, out var exp))
                {
                    t.Fail($"Items[{it.id}]", "exact 테이블에 항목 없음(신규 아이템 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(exp.coinCost, it.coinCost, $"Items[{it.id}].coinCost");
                t.Eq(exp.kind, it.kind, $"Items[{it.id}].kind");
            }
        }

        private static void CheckItemFxSnapshots(TestCtx t)
        {
            t.Eq(ExpectedItemFxHash.Count, Items.All.Length, "아이템 fx 스냅샷 테이블 크기 == Items.All.Length(73)");
            foreach (var it in Items.All)
            {
                if (!ExpectedItemFxHash.TryGetValue(it.id, out var expected))
                {
                    t.Fail($"Items[{it.id}].fx", "fx 스냅샷 테이블에 항목 없음(신규 아이템 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(expected, FxHash(it.fx), $"Items[{it.id}].fx 스냅샷 해시");
            }
        }

        // ═══════════════════════ ④ 장치 16종 — kind/rare/unlockAch 명시 대조 + fx 스냅샷 ═══════════════════════
        private static void CheckDeviceExactValues(TestCtx t)
        {
            t.Eq(ExpectedDeviceExact.Count, Devices.All.Length, "장치 exact 테이블 크기 == Devices.All.Length(16)");
            foreach (var d in Devices.All)
            {
                if (!ExpectedDeviceExact.TryGetValue(d.id, out var exp))
                {
                    t.Fail($"Devices[{d.id}]", "exact 테이블에 항목 없음(신규 장치 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(exp.kind, d.kind, $"Devices[{d.id}].kind");
                t.Eq(exp.rare, d.rare, $"Devices[{d.id}].rare");
                t.Eq(exp.unlockAch, d.unlockAch, $"Devices[{d.id}].unlockAch");
            }
        }

        private static void CheckDeviceFxSnapshots(TestCtx t)
        {
            t.Eq(ExpectedDeviceFxHash.Count, Devices.All.Length, "장치 fx 스냅샷 테이블 크기 == Devices.All.Length(16)");
            foreach (var d in Devices.All)
            {
                if (!ExpectedDeviceFxHash.TryGetValue(d.id, out var expected))
                {
                    t.Fail($"Devices[{d.id}].fx", "fx 스냅샷 테이블에 항목 없음(신규 장치 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(expected, FxHash(d.fx), $"Devices[{d.id}].fx 스냅샷 해시");
            }
        }

        // ═══════════════════════ ⑤ 세트 33종 — requires·게이트 3필드 명시 대조 + fx 스냅샷 ═══════════════════════
        private static void CheckSetExactValues(TestCtx t)
        {
            t.Eq(ExpectedSetExact.Count, Sets.All.Length, "세트 exact 테이블 크기 == Sets.All.Length(33)");
            foreach (var s in Sets.All)
            {
                if (!ExpectedSetExact.TryGetValue(s.id, out var exp))
                {
                    t.Fail($"Sets[{s.id}]", "exact 테이블에 항목 없음(신규 세트 추가 시 표 갱신 필요)");
                    continue;
                }
                var actualRequires = s.requires ?? Array.Empty<string>();
                t.True(exp.requires.SequenceEqual(actualRequires, StringComparer.Ordinal),
                    $"Sets[{s.id}].requires == [{string.Join(",", exp.requires)}]",
                    $"실제 [{string.Join(",", actualRequires)}]");
                t.Eq(exp.reqChar, s.reqChar ?? "", $"Sets[{s.id}].reqChar");
                t.Eq(exp.reqMachine, s.reqMachine ?? "", $"Sets[{s.id}].reqMachine");
                t.Eq(exp.reqDevice, s.reqDevice ?? "", $"Sets[{s.id}].reqDevice");
            }
        }

        private static void CheckSetFxSnapshots(TestCtx t)
        {
            t.Eq(ExpectedSetFxHash.Count, Sets.All.Length, "세트 fx 스냅샷 테이블 크기 == Sets.All.Length(33)");
            foreach (var s in Sets.All)
            {
                if (!ExpectedSetFxHash.TryGetValue(s.id, out var expected))
                {
                    t.Fail($"Sets[{s.id}].fx", "fx 스냅샷 테이블에 항목 없음(신규 세트 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(expected, FxHash(s.fx), $"Sets[{s.id}].fx 스냅샷 해시");
            }
        }

        // ═══════════════════════ ⑥ 캐릭터 19종 — scoreMod/startCoins/unlock OR 5축 (웹 data.js 직접대조) ═══════════════════════
        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §0 "정답지 전환" — 원본이 kotlin-reference에서 public/play/
        // data.js로 교체됨) — kotlin CHARS(L316-363) 대조값을 대체해 data.js:145-166 CHARS와 1:1 대조한
        // 명시값으로 갱신했다. unlockReq(StatReq AND 리스트)는 폐기되고 OR 5축(unlockRuns/Score/Stage/
        // Level/Ach)으로 바뀌었다(Characters.cs 헤더 각주).
        private static void CheckCharacterExactValues(TestCtx t)
        {
            t.Eq(ExpectedCharacterExact.Count, Characters.All.Length, "캐릭터 exact 테이블 크기 == Characters.All.Length(19)");
            foreach (var c in Characters.All)
            {
                if (!ExpectedCharacterExact.TryGetValue(c.id, out var exp))
                {
                    t.Fail($"Characters[{c.id}]", "exact 테이블에 항목 없음(신규 캐릭터 추가 시 표 갱신 필요)");
                    continue;
                }
                t.Eq(exp.scoreMod, c.scoreMod, $"Characters[{c.id}].scoreMod");
                t.Eq(exp.startCoins, c.startCoins, $"Characters[{c.id}].startCoins");
                t.Eq(exp.unlockRuns, c.unlockRuns, $"Characters[{c.id}].unlockRuns");
                t.Eq(exp.unlockScore, c.unlockScore, $"Characters[{c.id}].unlockScore");
                t.Eq(exp.unlockStage, c.unlockStage, $"Characters[{c.id}].unlockStage");
                t.Eq(exp.unlockLevel, c.unlockLevel, $"Characters[{c.id}].unlockLevel");
                t.Eq(exp.unlockAch, c.unlockAch ?? "", $"Characters[{c.id}].unlockAch");
            }
        }

        // ═══════════════════════ ⑦ Schools — SCHOOL_REQ minLevel/req 10종 전수 ═══════════════════════
        private static void CheckSchoolReqExactValues(TestCtx t)
        {
            t.Eq(ExpectedSchoolReq.Count, Schools.SchoolReq.Count, "SCHOOL_REQ exact 테이블 크기 == Schools.SchoolReq.Count(10)");
            foreach (var kv in ExpectedSchoolReq)
            {
                if (!Schools.SchoolReq.TryGetValue(kv.Key, out var gate))
                {
                    t.Fail($"Schools.SchoolReq[{kv.Key}]", "Schools.SchoolReq에 해당 school 없음");
                    continue;
                }
                t.Eq(kv.Value.minLevel, gate.minLevel, $"Schools.SchoolReq[{kv.Key}].minLevel");
                t.Eq(kv.Key, gate.school, $"Schools.SchoolReq[{kv.Key}].school");
                CheckReq(t, $"Schools.SchoolReq[{kv.Key}].req", kv.Value.req, gate.req);
            }
        }

        // req(key,value) AND-리스트 순서 포함 비교 헬퍼 — Characters.unlockReq(List<StatReq>)와
        // Schools.SchoolReq[].req(StatReq[])가 둘 다 IReadOnlyList<StatReq>라 공용으로 쓴다.
        private static void CheckReq(TestCtx t, string label, (string key, long val)[] expected, IReadOnlyList<StatReq> actual)
        {
            var actualList = actual ?? Array.Empty<StatReq>();
            t.Eq(expected.Length, actualList.Count, $"{label} 개수");
            int n = Math.Min(expected.Length, actualList.Count);
            for (int i = 0; i < n; i++)
            {
                t.Eq(expected[i].key, actualList[i].key, $"{label}[{i}].key");
                t.Eq(expected[i].val, actualList[i].value, $"{label}[{i}].value");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 스냅샷/명시값 상수 테이블
        // 아래 표는 2026-07-30 시점 Content/*.cs 실제 값을 이 파일과 동일한 FNV-1a 알고리즘으로
        // 계산해 얻었다(정확성은 같은 날 Opus 1차검수의 Kotlin 전수대조로 확증됨). 회귀 방지 목적 —
        // 의도된 콘텐츠 변경이라면 이 표를 함께 갱신할 것.
        // ═══════════════════════════════════════════════════════════════════════

        // ── 퍼크 157종 fx 스냅샷 해시 ──
        private static readonly Dictionary<string, ulong> ExpectedPerkFxHash = new Dictionary<string, ulong>
        {
            ["all_in"] = 0xCB03FA9431D08AFBUL,
            ["all_or_nothing"] = 0xC68020234CD96103UL,
            ["auto_pen"] = 0x5A5479D0B0766021UL,
            ["battery"] = 0x15937FB995535912UL,
            ["black_candle"] = 0xEF7E885CF200C07CUL,
            ["black_diploma"] = 0xAA574E6F0E3A64C8UL,
            ["black_report"] = 0xEF7E885CF200C07CUL,
            ["blackout"] = 0x37A4E479D3A7AE96UL,
            ["bloody_coupon"] = 0x3BED882474C449ACUL,
            ["book_up"] = 0x5A5479D0B0766021UL,
            ["bookmark"] = 0xC383C8B1EA98D853UL,
            ["broken_crown"] = 0xCA016BDCEF112780UL,
            ["bullseye"] = 0xFA2D003D4EE137CFUL,
            ["calculator"] = 0x8C573F704E622261UL,
            ["center"] = 0x9538683B9B6EF9B8UL,
            ["chain"] = 0xF77DEAF3D55631C9UL,
            ["charm_relic"] = 0xB1AB7483F884F929UL,
            ["cherry_can"] = 0x0DD2E1217C8A45F2UL,
            ["cherry_candy"] = 0x0DD2E2217C8A47A5UL,
            ["cherry_farm"] = 0xB2A27736492F5410UL,
            ["cherry_jam"] = 0x0DD2E1217C8A45F2UL,
            ["cherry_press"] = 0x0DD2E2217C8A47A5UL,
            ["cherry_up"] = 0x0DD2E2217C8A47A5UL,
            ["cliff_focus"] = 0xFE011EBAFEA910F1UL,
            ["clover"] = 0xB1A80483F8820502UL,
            ["coffee"] = 0xD1F757F96198B7ACUL,
            ["coin_luck"] = 0x4B07300CF39591DFUL,
            ["coin_pouch"] = 0x4B072F0CF395902CUL,
            ["combo_note"] = 0x6A15A0FC84430C56UL,
            ["combo_trophy"] = 0x85CCF014BDEE0C39UL,
            ["cram"] = 0xE1A9F8B4983EC784UL,
            ["crammer_tag"] = 0x934E091249C8D693UL,
            ["crown_jewel"] = 0xC9FB04DCEF0C0CCDUL,
            ["crown_seek"] = 0xCBC93A953B8E25B2UL,
            ["crown_stand"] = 0xC9F77BDCEF08EE2BUL,
            ["crumpled_coupon"] = 0x4B072F0CF395902CUL,
            ["crystal_ball"] = 0x9BF650E013A02A1CUL,
            ["cursed_skulls"] = 0xFE6CF479F4D9F2A7UL,
            ["cursed_wallet"] = 0x6C452E7509218067UL,
            ["deep_read"] = 0xC383C8B1EA98D853UL,
            ["desk_lamp"] = 0xD1F756F96198B5F9UL,
            ["diligence"] = 0x15937CB9955353F9UL,
            ["diploma_pressure"] = 0xD7A8EF4E78A924AAUL,
            ["diploma_relic"] = 0xC383CEB1EA98E285UL,
            ["domino"] = 0xF7816AF3D5594120UL,
            ["early_adapt"] = 0x6E9C55F4FEDAE99EUL,
            ["early_prep"] = 0x5D625BABF5F9704DUL,
            ["endgame_rush"] = 0xA31B9365DB6D5B31UL,
            ["eraser"] = 0x5A5479D0B0766021UL,
            ["evening"] = 0xC67AC262DA8F582BUL,
            ["exam_week"] = 0xD7A8EF4E78A924AAUL,
            ["fate_bell"] = 0xCBF29CE484222325UL,
            ["fate_burst"] = 0x82BB827E111F7910UL,
            ["fate_handle"] = 0xA3B919C1592AB9CEUL,
            ["flame_canister"] = 0xB1A80483F8820502UL,
            ["focus_fire"] = 0xDF207C3D3F41E855UL,
            ["focus_ring"] = 0xFA2D063D4EE14201UL,
            ["fortune_check"] = 0xFCAE2417EB486DAFUL,
            ["four_clover"] = 0x54F8FE17414E9381UL,
            ["frugal_vow"] = 0x53A2D60CF8719C3FUL,
            ["gamblers_dice"] = 0x8228470284D10E63UL,
            ["gamblers_eye"] = 0x9BF651E013A02BCFUL,
            ["gem_buff"] = 0x8C573F704E622261UL,
            ["gem_cert"] = 0x8C5740704E622414UL,
            ["gem_dust"] = 0x8C573D704E621EFBUL,
            ["gem_invest"] = 0x8C4D30704E59B45FUL,
            ["gem_obsession"] = 0x2C20835C2EF05668UL,
            ["gem_polish"] = 0x8C573D704E621EFBUL,
            ["gem_tiara"] = 0x8C4D2B704E59ABE0UL,
            ["glass_cannon"] = 0x1A6B6B56AC0ADD8BUL,
            ["great_harvest"] = 0x10752ADEBAB90A33UL,
            ["greed"] = 0xB1A10583F87BE807UL,
            ["greed_calc"] = 0xB1AB7583F884FADCUL,
            ["greed_goblet"] = 0x54F8FE17414E9381UL,
            ["growth_log"] = 0xB6BD7693AE52AC56UL,
            ["hard_exam"] = 0xAFE3CF4DDCF78F9CUL,
            ["hex_allornothing"] = 0x731E5CFC897C0DF8UL,
            ["high_roller"] = 0x571C21D1C776083DUL,
            ["high_stakes"] = 0xD7AC6B4E78AC2D35UL,
            ["honor_student"] = 0xC383CBB1EA98DD6CUL,
            ["hot_handle"] = 0xB1A80583F88206B5UL,
            ["hourglass_r"] = 0xBE5725DA69C8ED7CUL,
            ["insurance"] = 0x67B76F40DBBAE80BUL,
            ["iron_chain"] = 0xF7816CF3D5594486UL,
            ["jackpot"] = 0xF31D6651B96E57FAUL,
            ["joker"] = 0xF89BADCDAB53A768UL,
            ["key_master"] = 0x238EA79D2CC54287UL,
            ["kings_ledger"] = 0x31513500E1CADCE3UL,
            ["lapidary"] = 0x8C4D33704E59B978UL,
            ["late_bloomer"] = 0x75788F7AB3FE7DBEUL,
            ["late_focus"] = 0xB73643D1DBA06A8DUL,
            ["library"] = 0xFF7E9093F5F2A4D2UL,
            ["library_card"] = 0x6FD12DC2DA439A95UL,
            ["luck_accum"] = 0xEA12848E04066DF5UL,
            ["lucky"] = 0x9BF651E013A02BCFUL,
            ["lucky_eraser"] = 0xA3BC89C1592DADF5UL,
            ["magnet_chip"] = 0x95A429782DC1047EUL,
            ["magnet_up"] = 0x95A42A782DC10631UL,
            ["magnifier"] = 0xA3BC89C1592DADF5UL,
            ["mega_jackpot"] = 0xC519AD9F7401D72FUL,
            ["mini_scope"] = 0xA3BC89C1592DADF5UL,
            ["mirror"] = 0xFD03E88076516094UL,
            ["morning"] = 0x6CDCD07AAF2248E3UL,
            ["necromancer"] = 0xEF7E8C5CF200C748UL,
            ["note_take"] = 0x15937EB99553575FUL,
            ["old_book"] = 0x5A5478D0B0765E6EUL,
            ["old_wallet"] = 0x4B072F0CF395902CUL,
            ["ominous_skull"] = 0xEF7E895CF200C22FUL,
            ["overdrive"] = 0x54F8FF17414E9534UL,
            ["overheat_formula"] = 0xB1AB7683F884FC8FUL,
            ["pair_match"] = 0x42BFCD1D8A45760EUL,
            ["paperclip"] = 0x85C97014BDEAFCE2UL,
            ["pencil"] = 0x1D8622779747F6B4UL,
            ["perfect_shape"] = 0x508921010015B45FUL,
            ["piggy_bank"] = 0x555F8273CEC732B7UL,
            ["polish_work"] = 0x8C4D30704E59B45FUL,
            ["polymath"] = 0x54F8FB17414E8E68UL,
            ["pop_quiz"] = 0xF7C2CD355EBAD3BAUL,
            ["practice_pad"] = 0x5A5479D0B0766021UL,
            ["preview"] = 0x1D7B9277973EAD7FUL,
            ["puzzle_sense"] = 0x79A8B1965F185EDBUL,
            ["red_safetynet"] = 0x0DD2E2217C8A47A5UL,
            ["review"] = 0xD1ECE7F9618FA4D7UL,
            ["rich_richer"] = 0xED4FF40793E2618BUL,
            ["royal_decree"] = 0x31514000E1CAEF94UL,
            ["ruler"] = 0x1D8621779747F501UL,
            ["rusty_coin"] = 0x4B072F0CF395902CUL,
            ["sacrifice"] = 0x3D736F01F6E45195UL,
            ["seed_garden"] = 0xD8A293DAE34BE615UL,
            ["set_charm"] = 0x85CCF014BDEE0C39UL,
            ["set_sense"] = 0x6A15A1FC84430E09UL,
            ["short_day"] = 0x35E768DE57F460C0UL,
            ["silver_mirror"] = 0xFD03DE8076514F96UL,
            ["skull_idol"] = 0xEF7E8A5CF200C3E2UL,
            ["skull_study"] = 0xEF7E8A5CF200C3E2UL,
            ["skull_watch"] = 0xD427AACBB470390AUL,
            ["sleep_debt"] = 0xFD492F58BCE29E96UL,
            ["small_candle"] = 0xEF7E875CF200BEC9UL,
            ["snowball"] = 0xA8CA96C3051C692EUL,
            ["spare_token"] = 0x67B76F40DBBAE80BUL,
            ["speed_test"] = 0xF7C2CD355EBAD3BAUL,
            ["star_chart"] = 0x2C22B75903C8408CUL,
            ["star_sticker"] = 0xAA81B67975AB3D11UL,
            ["star_up"] = 0x2C22B75903C8408CUL,
            ["star_up2"] = 0x2C22B85903C8423FUL,
            ["student_debt"] = 0x53A2D70CF8719DF2UL,
            ["study"] = 0x54F8FE17414E9381UL,
            ["study_tag"] = 0xC383CDB1EA98E0D2UL,
            ["supernova"] = 0x54F90017414E96E7UL,
            ["symmetry"] = 0x3DA78EBE0F53C108UL,
            ["thick_tome"] = 0x5A547BD0B0766387UL,
            ["thorny_path"] = 0x9540B11E79D36E65UL,
            ["time_warp"] = 0x9504931C6AD7CAD9UL,
            ["tunnel_vision"] = 0x54ED907D4C9AB91DUL,
            ["twins"] = 0x9A080BC535AA5ED0UL,
            ["wide_lens"] = 0xFA2D033D4EE13CE8UL,
            ["wild_world"] = 0xF89BB0CDAB53AC81UL,
                    // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) 신규 증강9·유물12 ──
            ["discount"] = 0x9CD9292CC5A14150UL,
            ["thrifty"] = 0x99F56B01CC248C1CUL,
            ["item_bag"] = 0xF1645B270362696FUL,
            ["vip"] = 0xB2A80D9D96429CC7UL,
            ["refund"] = 0xCBF29CE484222325UL,
            ["crown_burst"] = 0x833409D6BFDC0405UL,
            ["curse_grad"] = 0x8C7284B81BECE03UL,
            ["extreme_overload"] = 0x8A686F1615FDF26DUL,
            ["abyss_lore"] = 0x200D0C034178FC74UL,
            ["prism_diploma"] = 0xF522B965DB729149UL,
            ["golden_ratio"] = 0xE9A63DEF49134544UL,
            ["starlight_crown"] = 0xB3BE32104E949E53UL,
            ["endless_recess"] = 0xA98F3068A81E5F21UL,
            ["fortunes_wheel"] = 0x285F353A2A2BCCA2UL,
            ["set_resonator"] = 0xECDF56C160D3330CUL,
            ["reapers_pact"] = 0x26B0873ECCA37E7EUL,
            ["phoenix_thesis"] = 0xDB585156122493A3UL,
            ["crown_monolith"] = 0xCE2DEC3D56F2DF21UL,
            ["black_grad_photo"] = 0x8C72D4B81BED682UL,
            ["last_roll"] = 0xE1C1486E86F11117UL,
            ["nameless_cup"] = 0x938DDF13722DA6CBUL,
        };

        // ── 퍼크 157종 name/emoji/desc/price/tier/school 스냅샷 해시 ──
        private static readonly Dictionary<string, ulong> ExpectedPerkMetaHash = new Dictionary<string, ulong>
        {
            ["all_in"] = 0x31C016326FAE9EADUL,
            ["all_or_nothing"] = 0xE036F025E9DF1B91UL,
            ["auto_pen"] = 0x99DF9CEE14BD631CUL,
            ["battery"] = 0x0A3C6FA1784B3492UL,
            ["black_candle"] = 0xA09B330C28269B48UL,
            ["black_diploma"] = 0xE0FD92934A066C28UL,
            ["black_report"] = 0x7C15408CAF5F6E26UL,
            ["blackout"] = 0x8A47ABD036299D10UL,
            ["bloody_coupon"] = 0x9FC389FD8FC40C7AUL,
            ["book_up"] = 0xA682B2864090B05BUL,
            ["bookmark"] = 0x7A41541E13D0ABDCUL,
            ["broken_crown"] = 0x70A36834A24A6EF5UL,
            ["bullseye"] = 0xB6E9DB57DE4CC935UL,
            ["calculator"] = 0x3516E4992BD590B2UL,
            ["center"] = 0xF0378269527DC9BBUL,
            ["chain"] = 0x09930365F39BD4B1UL,
            ["charm_relic"] = 0xFEDC3E9C416DFB92UL,
            ["cherry_can"] = 0x6894C4F8530FD6E4UL,
            ["cherry_candy"] = 0xF33F3D9A27944D8EUL,
            ["cherry_farm"] = 0x1476DAF2613ACF61UL,
            ["cherry_jam"] = 0xC21F8EF38A6F241DUL,
            ["cherry_press"] = 0xDF1D6F0009039224UL,
            ["cherry_up"] = 0x5B8D11520FD6B371UL,
            ["cliff_focus"] = 0xA63B65546A48A594UL,
            ["clover"] = 0xE9201D3FE9EB5523UL,
            ["coffee"] = 0xB90316FD23FF09C1UL,
            ["coin_luck"] = 0x702D0A495B9BCB30UL,
            ["coin_pouch"] = 0xA8692AB213BF8873UL,
            ["combo_note"] = 0x7D14D5C4659A308AUL,
            ["combo_trophy"] = 0xAC496F47DF196F47UL,
            ["cram"] = 0xD0A74C5A30CED501UL,
            ["crammer_tag"] = 0x1B61BDE3DF885731UL,
            ["crown_jewel"] = 0x1CFC5D40ED0DAAD5UL,
            ["crown_seek"] = 0x2D3D0EC82FF4E829UL,
            ["crown_stand"] = 0xD873F2B424A27404UL,
            ["crumpled_coupon"] = 0x28D53FA9DD2E099CUL,
            ["crystal_ball"] = 0x8C14017F05847618UL,
            ["cursed_skulls"] = 0x2C3E8F46AD37EEAFUL,
            ["cursed_wallet"] = 0x5BCB10A387478104UL,
            ["deep_read"] = 0xEC3671D80B92A34DUL,
            ["desk_lamp"] = 0x02D21C55466EE090UL,
            ["diligence"] = 0x4F2AD647146059E4UL,
            ["diploma_pressure"] = 0x51554E12B563EBBAUL,
            ["diploma_relic"] = 0xD6384DD2DA9F2486UL,
            ["domino"] = 0x208416C756D65670UL,
            ["early_adapt"] = 0x9B63C4303EA42943UL,
            ["early_prep"] = 0x1948F6A66AC86D49UL,
            ["endgame_rush"] = 0x9DB6FC754DAC9610UL,
            ["eraser"] = 0x2213EF677C201659UL,
            ["evening"] = 0xBA54A997A202B1EBUL,
            ["exam_week"] = 0x474E1B0B80C58151UL,
            ["fate_bell"] = 0x9EEFEE86E06C5610UL,
            ["fate_burst"] = 0xE0AF0D293532E137UL,
            ["fate_handle"] = 0x3A29E8391F454832UL,
            ["flame_canister"] = 0xF47E1287E93E15CAUL,
            ["focus_fire"] = 0x58E4EFB3CA03ACCDUL,
            ["focus_ring"] = 0x3DE0D37CBAEF957BUL,
            ["fortune_check"] = 0xC152BA44E8BD5251UL,
            ["four_clover"] = 0x72E4FB91457922F3UL,
            ["frugal_vow"] = 0x74133E862A6A0CECUL,
            ["gamblers_dice"] = 0xC508C70EEA289285UL,
            ["gamblers_eye"] = 0xCDB0D4B6F7C7A160UL,
            ["gem_buff"] = 0x26B52CFBFD6153C9UL,
            ["gem_cert"] = 0xB81B63E2A01AAC61UL,
            ["gem_dust"] = 0x759AFD4AA795A8BDUL,
            ["gem_invest"] = 0x36206AF16491E585UL,
            ["gem_obsession"] = 0xD0E81E169FA982DUL,
            ["gem_polish"] = 0x614D0A46ED2C9E27UL,
            ["gem_tiara"] = 0xBCCEDC96B774FCC6UL,
            ["glass_cannon"] = 0x0DF8169380CF7341UL,
            ["great_harvest"] = 0x891DF960E4EE7E7FUL,
            ["greed"] = 0x74D3235F43E8DAC6UL,
            ["greed_calc"] = 0x52778E7E40EAA845UL,
            ["greed_goblet"] = 0x19F1FC7DF0A99EBEUL,
            ["growth_log"] = 0xD129200EE670AF05UL,
            ["hard_exam"] = 0xAFFD1C263E4F042AUL,
            ["hex_allornothing"] = 0x7CFBC6F271496A53UL,
            ["high_roller"] = 0x762A55C14D45709AUL,
            ["high_stakes"] = 0x856E1387FA817ECCUL,
            ["honor_student"] = 0x48035BC4DC91FDD2UL,
            ["hot_handle"] = 0xE90912E24B3376F5UL,
            ["hourglass_r"] = 0x30288BAD26033374UL,
            ["insurance"] = 0x47DB1FE9E42292F3UL,
            ["iron_chain"] = 0xB27F758C402D8576UL,
            ["jackpot"] = 0x36F734D69E66CDA4UL,
            ["joker"] = 0x1CBB1992BE8EEEF3UL,
            ["key_master"] = 0xEB85FC0C849B0B46UL,
            ["kings_ledger"] = 0xFF7B40B8C3EFD36DUL,
            ["lapidary"] = 0x79D843CCD77C466FUL,
            ["late_bloomer"] = 0x8375A18644D518E3UL,
            ["late_focus"] = 0xF18A38D563541515UL,
            ["library"] = 0xAE5534F32724922FUL,
            ["library_card"] = 0xCE6FB38BBCF22287UL,
            ["luck_accum"] = 0x244F5B51118059CFUL,
            ["lucky"] = 0x757BCE573CDBE499UL,
            ["lucky_eraser"] = 0x6E99FC5BFE1D6B1DUL,
            ["magnet_chip"] = 0x8A05215ABAEB88A8UL,
            ["magnet_up"] = 0x09AE4DF59F8D4D5BUL,
            ["magnifier"] = 0xC68F6B8E304CBA0EUL,
            ["mega_jackpot"] = 0xECAAA532F8370AA7UL,
            ["mini_scope"] = 0x877C3C13D89743C7UL,
            ["mirror"] = 0x75E44691840A7F36UL,
            ["morning"] = 0xAABF89E2C99B53CEUL,
            ["necromancer"] = 0xC6AA2F15C2CA3A69UL,
            ["note_take"] = 0xF222D34C9F649905UL,
            ["old_book"] = 0xCD535462A14EF129UL,
            ["old_wallet"] = 0x9B40567859026397UL,
            ["ominous_skull"] = 0x10AE7A271C6D38B0UL,
            ["overdrive"] = 0x14F3C51514C2CC1AUL,
            ["overheat_formula"] = 0x8A5968BF8E790432UL,
            ["pair_match"] = 0x7B3C4A2BEFB63C35UL,
            ["paperclip"] = 0x2F542FAA833C267DUL,
            ["pencil"] = 0xEF2CE3C176A317FDUL,
            ["perfect_shape"] = 0x0906F8880E1BEE4EUL,
            ["piggy_bank"] = 0xA631FB0112ECB3D8UL,
            ["polish_work"] = 0x7C81E7D60FBFA6FDUL,
            ["polymath"] = 0xF745F747E2B344EAUL,
            ["pop_quiz"] = 0x2085E8F0646488ADUL,
            ["practice_pad"] = 0xE5A925E82C2D39F4UL,
            ["preview"] = 0xE2C4D4BEBF4DE328UL,
            ["puzzle_sense"] = 0x7FAC2B7C4817B6ABUL,
            ["red_safetynet"] = 0x56554EBBB3918CD0UL,
            ["review"] = 0x52BF430FF76B2D02UL,
            ["rich_richer"] = 0x78BD1998825A4F7EUL,
            ["royal_decree"] = 0x15BE83DD0F0DFA08UL,
            ["ruler"] = 0x97C1EC7D53B8A441UL,
            ["rusty_coin"] = 0x0A08109975C95E99UL,
            ["sacrifice"] = 0x0A36E011EECF15F1UL,
            ["seed_garden"] = 0x8679C669DE32B1A9UL,
            ["set_charm"] = 0xC2B74887F1BBA5B1UL,
            ["set_sense"] = 0x83AD7505C17A9EB1UL,
            ["short_day"] = 0x1CC36720130063B7UL,
            ["silver_mirror"] = 0x1B58114741BDDE64UL,
            ["skull_idol"] = 0x5962193CD886A334UL,
            ["skull_study"] = 0x4F29FEE12FF9D0E7UL,
            ["skull_watch"] = 0x349B71099D0FE1DCUL,
            ["sleep_debt"] = 0x49167F12EE06C945UL,
            ["small_candle"] = 0x287D076F224D2FA7UL,
            ["snowball"] = 0x8C1D5FB40D057B17UL,
            ["spare_token"] = 0xF49DF7CA7059A7A2UL,
            ["speed_test"] = 0xA55112628A26A561UL,
            ["star_chart"] = 0x0D9D69739D09B145UL,
            ["star_sticker"] = 0x8BDC63822923FB10UL,
            ["star_up"] = 0xA3219D4E64D43AABUL,
            ["star_up2"] = 0x4E3997B54AE42D28UL,
            ["student_debt"] = 0xE1F53CDED7B767CAUL,
            ["study"] = 0xC33FF508431A4CD9UL,
            ["study_tag"] = 0x6F711F6F1B02A8E4UL,
            ["supernova"] = 0x67B05651451DF252UL,
            ["symmetry"] = 0x27FC783A5E458E7DUL,
            ["thick_tome"] = 0x59C3D786A668F42CUL,
            ["thorny_path"] = 0x91D5D8513129C43DUL,
            ["time_warp"] = 0x8BF6D197AD23A17AUL,
            ["tunnel_vision"] = 0x59F4ADFB5EF010DCUL,
            ["twins"] = 0xAC4D8F266422CC79UL,
            ["wide_lens"] = 0xF4D344B498403DE7UL,
            ["wild_world"] = 0xAF877F125A34E619UL,
                    // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) 신규 증강9·유물12 ──
            ["discount"] = 0xE44A90F79D5A0151UL,
            ["thrifty"] = 0x797AAF3B2CD3579FUL,
            ["item_bag"] = 0xB6CD5B24710EA27BUL,
            ["vip"] = 0x7F3D95A19C1C1274UL,
            ["refund"] = 0xC47F67DC8B262C8FUL,
            ["crown_burst"] = 0x7F19C5C9818720A2UL,
            ["curse_grad"] = 0x3A8E01AF5FFC35DDUL,
            ["extreme_overload"] = 0x605AE20D1710B2D7UL,
            ["abyss_lore"] = 0x34E14C7773290614UL,
            ["prism_diploma"] = 0x2B9DCBE765E0B802UL,
            ["golden_ratio"] = 0xB9D6FADF696C1FDDUL,
            ["starlight_crown"] = 0xEEBA49F523A7D597UL,
            ["endless_recess"] = 0x16EC2708A1608C20UL,
            ["fortunes_wheel"] = 0xF68495A18B4E239UL,
            ["set_resonator"] = 0x2580E8F1EB629708UL,
            ["reapers_pact"] = 0xEB0E626FEC2514F8UL,
            ["phoenix_thesis"] = 0x124FDC9E34551545UL,
            ["crown_monolith"] = 0xC7728164E9836294UL,
            ["black_grad_photo"] = 0xB54E3FCC28395022UL,
            ["last_roll"] = 0x74553E228BB10293UL,
            ["nameless_cup"] = 0x4BD5BA6F96BAC290UL,
        };

        // ── 아이템 73종 exact (coinCost, kind) ──
        private static readonly Dictionary<string, (int coinCost, string kind)> ExpectedItemExact = new Dictionary<string, (int, string)>
        {
            ["energy_drink"] = (18, "NEXTSPIN"),
            ["magnify"] = (15, "NEXTSPIN"),
            ["loaded_dice"] = (22, "NEXTSPIN"),
            ["ward_charm"] = (10, "NEXTSPIN"),
            ["adrenaline"] = (30, "NEXTSPIN"),
            ["rare_scope"] = (18, "NEXTSPIN"),
            ["crown_inject"] = (24, "NEXTSPIN"),
            ["wild_inject"] = (22, "NEXTSPIN"),
            ["cherry_juice"] = (5, "NEXTSPIN"),
            ["bookmark2"] = (5, "NEXTSPIN"),
            ["sparkle_dust"] = (6, "NEXTSPIN"),
            ["gold_chalk"] = (13, "NEXTSPIN"),
            ["focus_candy"] = (5, "NEXTSPIN"),
            ["small_snack"] = (4, "NEXTSPIN"),
            ["cherry_basket"] = (7, "NEXTSPIN"),
            ["gem_loupe"] = (10, "NEXTSPIN"),
            ["seal_tape"] = (9, "NEXTSPIN"),
            ["skull_sticker"] = (12, "NEXTSPIN"),
            ["eraser_old"] = (8, "NEXTSPIN"),
            ["eraser_fine"] = (12, "NEXTSPIN"),
            ["eraser_god"] = (20, "NEXTSPIN"),
            ["wild_temp"] = (16, "NEXTSPIN"),
            ["fake_crown"] = (24, "NEXTSPIN"),
            ["espresso"] = (20, "PHASE"),
            ["study_streak"] = (12, "PHASE"),
            ["rare_lure"] = (16, "PHASE"),
            ["coin_magnet"] = (14, "PHASE"),
            ["dbl_nothing"] = (12, "PHASE"),
            ["last_minute"] = (18, "PHASE"),
            ["tutor"] = (18, "PHASE"),
            ["fortune_incense"] = (16, "PHASE"),
            ["coin_press"] = (16, "PHASE"),
            ["overtime"] = (16, "PHASE"),
            ["cram_note"] = (14, "PHASE"),
            ["rich_lure"] = (16, "PHASE"),
            ["prof_bribe"] = (24, "PHASE"),
            ["sugar_powder"] = (12, "PHASE"),
            ["cherry_cracker"] = (14, "PHASE"),
            ["book_copy"] = (12, "PHASE"),
            ["allnight_note"] = (16, "PHASE"),
            ["summary_note"] = (13, "PHASE"),
            ["gem_pouch"] = (16, "PHASE"),
            ["greed_lens"] = (18, "PHASE"),
            ["black_candle_i"] = (14, "PHASE"),
            ["curse_amp"] = (16, "PHASE"),
            ["gold_chalk_box"] = (20, "PHASE"),
            ["skull_shield"] = (14, "PHASE"),
            ["combo_mega"] = (16, "PHASE"),
            ["cram_note_x2"] = (16, "PHASE"),
            ["overload_potion"] = (20, "PHASE"),
            ["first_aid"] = (30, "INSTANT"),
            ["cram"] = (12, "INSTANT"),
            ["answer_sheet"] = (40, "INSTANT"),
            ["grad_cert"] = (100, "INSTANT"),
            ["double_aid"] = (55, "INSTANT"),
            ["cheat_sheet"] = (20, "INSTANT"),
            ["honor_roll"] = (60, "INSTANT"),
            ["dev_battery"] = (8, "INSTANT"),
            ["score_sticker"] = (5, "INSTANT"),
            ["old_coin"] = (4, "INSTANT"),
            ["grad_copy"] = (70, "INSTANT"),
            ["score_calc"] = (22, "INSTANT"),
            ["mini_coupon"] = (5, "INSTANT"),
            ["price_hack"] = (12, "INSTANT"),
            ["grad_ring"] = (50, "INSTANT"),
            ["gold_grad_bell"] = (90, "INSTANT"),
            ["insurance_cert"] = (45, "INSTANT"),
            ["debt_note"] = (0, "INSTANT"),
            ["retake_form"] = (28, "INSTANT"),
            ["black_lottery"] = (18, "INSTANT"),
            ["devil_contract"] = (20, "INSTANT"),
            ["timeline_ticket"] = (26, "INSTANT"),
            ["broken_prism"] = (22, "INSTANT"),
                    // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #12/#14, data.js:765-770) 신규 아이템 5종 ──
            ["study_note"] = (18, "INSTANT"),
            ["aug_catalyst"] = (12, "INSTANT"),
            ["gold_marker"] = (30, "INSTANT"),
            ["prism_ink"] = (35, "INSTANT"),
            ["overcharge"] = (26, "INSTANT"),
        };

        // ── 아이템 73종 fx 스냅샷 해시 ──
        private static readonly Dictionary<string, ulong> ExpectedItemFxHash = new Dictionary<string, ulong>
        {
            ["adrenaline"] = 0xB1D073DC50A5B786UL,
            ["allnight_note"] = 0x287816655B375F41UL,
            ["answer_sheet"] = 0xCBF29CE484222325UL,
            ["black_candle_i"] = 0xD0C2927F4F751076UL,
            ["black_lottery"] = 0xCBF29CE484222325UL,
            ["book_copy"] = 0x57791D8247FA8103UL,
            ["bookmark2"] = 0xFD3BC1C802117652UL,
            ["broken_prism"] = 0xCBF29CE484222325UL,
            ["cheat_sheet"] = 0xCBF29CE484222325UL,
            ["cherry_basket"] = 0x2411EA7BE2C7A2ECUL,
            ["cherry_cracker"] = 0xD5B22BAE595187B2UL,
            ["cherry_juice"] = 0x19B732EBF51EE422UL,
            ["coin_magnet"] = 0xF16207CC876FF352UL,
            ["coin_press"] = 0xBE9B1533EBD0F722UL,
            ["combo_mega"] = 0x213A4E62C5A67E94UL,
            ["cram"] = 0xCBF29CE484222325UL,
            ["cram_note"] = 0xE02B435AD39A81E9UL,
            ["cram_note_x2"] = 0xE02B435AD39A81E9UL,
            ["crown_inject"] = 0x178FB7552D0BD398UL,
            ["curse_amp"] = 0xAE504D128BDDB499UL,
            ["dbl_nothing"] = 0xF01B345DA5125238UL,
            ["debt_note"] = 0xCBF29CE484222325UL,
            ["dev_battery"] = 0xCBF29CE484222325UL,
            ["devil_contract"] = 0xCBF29CE484222325UL,
            ["double_aid"] = 0xCBF29CE484222325UL,
            ["energy_drink"] = 0xB1D074DC50A5B939UL,
            ["eraser_fine"] = 0xCBF29CE484222325UL,
            ["eraser_god"] = 0xCBF29CE484222325UL,
            ["eraser_old"] = 0xCBF29CE484222325UL,
            ["espresso"] = 0xFCEA2F58BC920A12UL,
            ["fake_crown"] = 0xCBF29CE484222325UL,
            ["first_aid"] = 0xCBF29CE484222325UL,
            ["focus_candy"] = 0xB1AB7583F884FADCUL,
            ["fortune_incense"] = 0x9BF64DE013A02503UL,
            ["gem_loupe"] = 0xD2FA7A22B7D93875UL,
            ["gem_pouch"] = 0xBAEF6591B8D9E9C5UL,
            ["gold_chalk"] = 0xB1D074DC50A5B939UL,
            ["gold_chalk_box"] = 0x54F90217414E9A4DUL,
            ["gold_grad_bell"] = 0xCBF29CE484222325UL,
            ["grad_cert"] = 0xCBF29CE484222325UL,
            ["grad_copy"] = 0xCBF29CE484222325UL,
            ["grad_ring"] = 0xCBF29CE484222325UL,
            ["greed_lens"] = 0xE32EDB7C13B2AD12UL,
            ["honor_roll"] = 0xCBF29CE484222325UL,
            ["insurance_cert"] = 0xCBF29CE484222325UL,
            ["last_minute"] = 0xE02B435AD39A81E9UL,
            ["loaded_dice"] = 0xCB7B982394689B79UL,
            ["magnify"] = 0xA118A1A4F461574CUL,
            ["mini_coupon"] = 0xCBF29CE484222325UL,
            ["old_coin"] = 0xCBF29CE484222325UL,
            ["overload_potion"] = 0x93CA8F55573F862AUL,
            ["overtime"] = 0xE02B435AD39A81E9UL,
            ["price_hack"] = 0xCBF29CE484222325UL,
            ["prof_bribe"] = 0x5962A8463545FABFUL,
            ["rare_lure"] = 0xA1189FA4F46153E6UL,
            ["rare_scope"] = 0xA118A0A4F4615599UL,
            ["retake_form"] = 0xCBF29CE484222325UL,
            ["rich_lure"] = 0xA118A0A4F4615599UL,
            ["score_calc"] = 0xCBF29CE484222325UL,
            ["score_sticker"] = 0xCBF29CE484222325UL,
            ["seal_tape"] = 0xFA7E13DB5A8BDA67UL,
            ["skull_shield"] = 0xFA7E13DB5A8BDA67UL,
            ["skull_sticker"] = 0x37E32BAF93631BB5UL,
            ["small_snack"] = 0xFA7E13DB5A8BDA67UL,
            ["sparkle_dust"] = 0x7275ACB82A44EC6EUL,
            ["study_streak"] = 0x15937FB995535912UL,
            ["sugar_powder"] = 0xD21ADB8D9103B422UL,
            ["summary_note"] = 0x159382B995535E2BUL,
            ["timeline_ticket"] = 0xCBF29CE484222325UL,
            ["tutor"] = 0xFCEA2C58BC9204F9UL,
            ["ward_charm"] = 0xFA7E13DB5A8BDA67UL,
            ["wild_inject"] = 0xF89BB0CDAB53AC81UL,
            ["wild_temp"] = 0xCBF29CE484222325UL,
                    // 신규 5종 — 전부 fx 없음(ItemUse.ApplyItemPurchase가 직접 처리, Items.cs 헤더 각주).
            ["study_note"] = 0xCBF29CE484222325UL,
            ["aug_catalyst"] = 0xCBF29CE484222325UL,
            ["gold_marker"] = 0xCBF29CE484222325UL,
            ["prism_ink"] = 0xCBF29CE484222325UL,
            ["overcharge"] = 0xCBF29CE484222325UL,
        };

        // ── 장치 16종 exact (kind, rare, unlockAch) ──
        // WEB_PARITY P3-2(업적 34종 교체) — 12종의 unlockAch가 구 lic_* 면허 업적 id에서 웹
        // ACH_DEVICE_REWARD(data.js:818-828) 매핑값으로 바뀌었다(Devices.cs 헤더 각주 참조). 나머지
        // 4종(dev_syllabus/dev_holdfile/dev_retake/dev_major)은 Opus 2차 검수·Fable 결정(2026-08-08,
        // 필수④)으로 드랍 전용 장치로 확정돼 unlockAch=""로 바뀌었다(업적 해금 경로 없음 — 런 중
        // 장치 드랍으로만 영구 획득, Devices.cs 헤더 각주 참조).
        private static readonly Dictionary<string, (string kind, bool rare, string unlockAch)> ExpectedDeviceExact =
            new Dictionary<string, (string, bool, string)>
        {
            ["dev_flame"] = ("PASSIVE", true, "score50k"),
            ["dev_seal"] = ("PASSIVE", false, "crown10"),
            ["dev_safe"] = ("PASSIVE", false, "cherry100"),
            ["dev_overheat"] = ("PASSIVE", true, "lastclear5"),
            ["dev_subreel"] = ("PASSIVE", true, "jackpot1"),
            ["dev_coin"] = ("ARMED", false, "score10k"),
            ["dev_reroll"] = ("MANIP", false, "boss1"),
            ["dev_pin"] = ("MANIP", false, "exact1"),
            ["dev_copy"] = ("MANIP", true, "prism5"),
            ["dev_swap"] = ("MANIP", true, "boss5"),
            ["dev_oracle"] = ("PEEK", true, "stage10"),
            ["dev_bell"] = ("INSTANT", true, "runs20"),
            ["dev_syllabus"] = ("PEEK", false, ""),
            ["dev_holdfile"] = ("ARMED", false, ""),
            ["dev_retake"] = ("ARMED", true, ""),
            ["dev_major"] = ("PASSIVE", false, ""),
                    // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #9/#14) 신규 장치 3종 — 레벨 자동지급(unlockAch="") ──
            ["dev_reaper"] = ("PASSIVE", true, ""),
            ["dev_abyss"] = ("PASSIVE", true, ""),
            ["dev_reactor"] = ("PASSIVE", true, ""),
        };

        // ── 장치 16종 fx 스냅샷 해시 ──
        private static readonly Dictionary<string, ulong> ExpectedDeviceFxHash = new Dictionary<string, ulong>
        {
            ["dev_bell"] = 0xCBF29CE484222325UL,
            ["dev_coin"] = 0x54F8FC17414E901BUL,
            ["dev_copy"] = 0xCBF29CE484222325UL,
            ["dev_flame"] = 0xB1AB7583F884FADCUL,
            ["dev_holdfile"] = 0xCBF29CE484222325UL,
            ["dev_major"] = 0xCBF29CE484222325UL,
            ["dev_oracle"] = 0xCBF29CE484222325UL,
            ["dev_overheat"] = 0x43B68B49209E13FAUL,
            ["dev_pin"] = 0xCBF29CE484222325UL,
            ["dev_reroll"] = 0xCBF29CE484222325UL,
            ["dev_retake"] = 0xCBF29CE484222325UL,
            ["dev_safe"] = 0xCBF29CE484222325UL,
            ["dev_seal"] = 0x57277B0B8DFC782AUL,
            ["dev_subreel"] = 0x5E6E191746E35570UL,
            ["dev_swap"] = 0xCBF29CE484222325UL,
            ["dev_syllabus"] = 0xCBF29CE484222325UL,
                    ["dev_reaper"] = 0xF4B7EFEF373E4E09UL,
            ["dev_abyss"] = 0xD1EE3ED07BA83CF7UL,
            ["dev_reactor"] = 0x22A6A014745CF7CDUL,
        };

        // ── 세트 33종 exact (requires, reqChar, reqMachine, reqDevice) ──
        // reqChar/reqMachine/reqDevice가 없는 경우 ""(null과 동치, ContentTypes.cs SetEffect 주석 참조).
        private static readonly Dictionary<string, (string[] requires, string reqChar, string reqMachine, string reqDevice)> ExpectedSetExact =
            new Dictionary<string, (string[], string, string, string)>
        {
            ["set_orchard"] = (new[] { "cherry_up", "cherry_farm" }, "", "", ""),
            ["set_library"] = (new[] { "book_up", "library", "study_tag" }, "", "", ""),
            ["set_necro"] = (new[] { "skull_study", "black_candle" }, "", "", ""),
            ["set_appraiser"] = (new[] { "gem_polish", "gem_invest", "gem_cert" }, "", "", ""),
            ["set_royal"] = (new[] { "crown_seek", "jackpot" }, "", "", ""),
            ["set_align"] = (new[] { "center", "twins", "chain" }, "", "", ""),
            ["set_combo"] = (new[] { "set_sense", "set_charm" }, "", "", ""),
            ["set_diurnal"] = (new[] { "morning", "evening" }, "", "", ""),
            ["set_necro2"] = (new[] { "necromancer", "skull_idol" }, "", "", ""),
            ["set_jewels"] = (new[] { "gem_buff", "lapidary", "gem_tiara" }, "", "", ""),
            ["set_combo2"] = (new[] { "combo_note", "combo_trophy" }, "", "", ""),
            ["set_royal2"] = (new[] { "royal_decree", "crown_jewel" }, "", "", ""),
            ["set_cherry_net"] = (new[] { "cherry_up", "cherry_jam" }, "farmer", "", ""),
            ["set_red_harvest"] = (new[] { "cherry_farm", "great_harvest" }, "", "cherry", ""),
            ["set_student"] = (new[] { "study", "diligence", "note_take" }, "", "", ""),
            ["set_lib_bless"] = (new[] { "book_up", "library", "thick_tome" }, "", "library", ""),
            ["set_greed"] = (new[] { "greed", "rich_richer" }, "", "", ""),
            ["set_glory_grad"] = (new[] { "diploma_relic", "honor_student" }, "honor", "", ""),
            ["set_skull_lab"] = (new[] { "skull_study", "skull_idol" }, "cultist", "", ""),
            ["set_black_grad"] = (new[] { "necromancer", "black_candle", "skull_idol" }, "", "skull", ""),
            ["set_curse_cycle"] = (new[] { "set_charm" }, "", "", "dev_seal"),
            ["set_crown_rite"] = (new[] { "crown_seek", "crown_jewel" }, "crowncol", "", ""),
            ["set_kings_order"] = (new[] { "royal_decree", "jackpot" }, "", "crown", ""),
            ["set_flame_lab"] = (new[] { "all_or_nothing" }, "", "flame", "dev_flame"),
            ["set_last_ignite"] = (new[] { "review", "endgame_rush" }, "", "", ""),
            ["set_mechanic"] = (new[] { "set_sense" }, "", "", "dev_subreel"),
            ["set_battery"] = (new[] { "battery", "diligence" }, "", "", ""),
            ["set_gambler"] = (new[] { "high_stakes", "high_roller" }, "gambler", "", ""),
            ["set_shop_reg"] = (new[] { "coin_luck", "piggy_bank" }, "", "", ""),
            ["set_scholarship"] = (new[] { "study_tag", "diploma_relic" }, "scholar", "", ""),
            ["set_bomb_calc"] = (new[] { "center", "focus_fire" }, "", "bomb", ""),
            ["set_perfect_calc"] = (new[] { "center", "twins", "chain" }, "", "", ""),
            ["set_safe_grad"] = (new[] { "insurance", "clover" }, "", "", ""),
        };

        // ── 세트 33종 fx 스냅샷 해시 ──
        private static readonly Dictionary<string, ulong> ExpectedSetFxHash = new Dictionary<string, ulong>
        {
            ["set_align"] = 0xF78170F3D5594B52UL,
            ["set_appraiser"] = 0x8C4D2B704E59ABE0UL,
            ["set_battery"] = 0x15937FB995535912UL,
            ["set_black_grad"] = 0x86F9B296708DDE0FUL,
            ["set_bomb_calc"] = 0xC2014E9EB1145462UL,
            ["set_cherry_net"] = 0xAB2632AD861A63C4UL,
            ["set_combo"] = 0x6A15A0FC84430C56UL,
            ["set_combo2"] = 0x6A15A0FC84430C56UL,
            ["set_crown_rite"] = 0xC519AC9F7401D57CUL,
            ["set_curse_cycle"] = 0x6A15A1FC84430E09UL,
            ["set_diurnal"] = 0x457B89EFCF6AC054UL,
            ["set_flame_lab"] = 0x4CAF1419D5C83F14UL,
            ["set_gambler"] = 0x705F95C7C5DAF3D7UL,
            ["set_glory_grad"] = 0x70837545828EB702UL,
            ["set_greed"] = 0xD7672DA0A34A2CE1UL,
            ["set_jewels"] = 0x8C4D2B704E59ABE0UL,
            ["set_kings_order"] = 0xF31D6751B96E59ADUL,
            ["set_last_ignite"] = 0x4BD499469CCBDC07UL,
            ["set_lib_bless"] = 0xFF7E9093F5F2A4D2UL,
            ["set_library"] = 0x6FD12DC2DA439A95UL,
            ["set_mechanic"] = 0x85CCF014BDEE0C39UL,
            ["set_necro"] = 0xEF7E885CF200C07CUL,
            ["set_necro2"] = 0xEF7E895CF200C22FUL,
            ["set_orchard"] = 0x10521A500466ECA3UL,
            ["set_perfect_calc"] = 0x9D2FEA9E3B1DB478UL,
            ["set_red_harvest"] = 0x10521A500466ECA3UL,
            ["set_royal"] = 0xC519AC9F7401D57CUL,
            ["set_royal2"] = 0x0B2F920E6DA29DE7UL,
            ["set_safe_grad"] = 0xC470305D941856B6UL,
            ["set_scholarship"] = 0xE3119755AE33A3A1UL,
            ["set_shop_reg"] = 0x531E9DEC27DCD88EUL,
            ["set_skull_lab"] = 0xEF7E8A5CF200C3E2UL,
            ["set_student"] = 0x15937DB9955355ACUL,
        };

        // ── 캐릭터 16종 exact (scoreMod, startCoins, unlockReq) — Kotlin CHARS(L316-363) 직접대조 ──
        // (scoreMod, startCoins, unlockRuns, unlockScore, unlockStage, unlockLevel, unlockAch) — data.js:145-166.
        private static readonly Dictionary<string, (double scoreMod, int startCoins, long unlockRuns, long unlockScore, long unlockStage, int unlockLevel, string unlockAch)> ExpectedCharacterExact =
            new Dictionary<string, (double, int, long, long, long, int, string)>
        {
            ["novice"] = (0.9, 0, 0, 0, 0, 0, ""),
            ["scholar"] = (1.0, 0, 0, 0, 0, 0, ""),
            ["gambler"] = (1.1, 0, 0, 0, 0, 0, ""), // data.js:148 — 웹은 unlock 필드 자체가 없다(항상 해금).
            ["farmer"] = (0.95, 0, 2, 0, 0, 0, "cherry100"),
            ["parttime"] = (1.0, 15, 3, 0, 0, 0, ""),
            ["jeweler"] = (1.1, 0, 0, 2500, 0, 0, "jackpot1"),
            ["honor"] = (1.0, 0, 0, 0, 8, 0, "boss5"),
            ["cultist"] = (1.15, 0, 0, 0, 5, 0, "boss1"),
            ["crowncol"] = (1.15, 0, 0, 5000, 0, 0, "crown30"),
            ["minimalist"] = (1.1, 0, 0, 0, 7, 0, "exact1"),
            ["lucky"] = (1.05, 0, 4, 0, 0, 0, ""),
            ["highroller"] = (1.1, 12, 0, 3500, 0, 0, ""),
            ["monk"] = (1.05, 0, 0, 0, 6, 0, ""),
            ["alchemist"] = (1.0, 0, 0, 4000, 0, 0, ""),
            ["daredevil"] = (1.2, 0, 0, 0, 8, 0, "score10k"),
            ["prodigy"] = (0.95, 0, 0, 0, 9, 0, "stage10"),
            // ── 신규 3종(data.js:163-165) — 전부 플레이어 레벨 해금 ──
            ["regent"] = (1.15, 0, 0, 0, 0, 8, ""),
            ["bankrupt"] = (1.15, 0, 0, 0, 0, 12, ""),
            ["abyss_scholar"] = (1.15, 0, 0, 0, 0, 16, ""),
        };

        // ── Schools SCHOOL_REQ 10종 exact (minLevel, req) — Kotlin SCHOOL_REQ(L716-727) 대조 ──
        private static readonly Dictionary<string, (int minLevel, (string key, long val)[] req)> ExpectedSchoolReq =
            new Dictionary<string, (int, (string, long)[])>
        {
            ["성장학"] = (5, new (string, long)[] { ("cherryTotal", 200L), ("bestStage", 5L) }),
            ["계산학"] = (7, new (string, long)[] { ("set4Plus", 3L), ("exactClears", 1L) }),
            ["경제학"] = (8, new (string, long)[] { ("coinTotal", 300L), ("shopBuys", 5L) }),
            ["운명학"] = (9, new (string, long)[] { ("prayClears", 1L), ("gambles", 3L) }),
            ["왕관학"] = (10, new (string, long)[] { ("crownTotal", 30L), ("jackpots", 1L) }),
            ["저주학"] = (11, new (string, long)[] { ("skullTotal", 100L), ("curseMax", 1L) }),
            ["시간학"] = (8, new (string, long)[] { ("lastSpinClears", 3L), ("closeClears", 5L) }),
            ["프리즘공학"] = (12, new (string, long)[] { ("prismPicks", 3L), ("bossClears", 3L), ("bestStage", 10L) }),
            ["씨앗학"] = (12, new (string, long)[] { ("mstage_garden", 8L) }),
            ["와일드학"] = (13, new (string, long)[] { ("set4Plus", 10L) }),
        };
    }
}
