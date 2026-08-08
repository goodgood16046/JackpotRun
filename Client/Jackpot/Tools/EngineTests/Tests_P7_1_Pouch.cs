using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P7-1 골든 테스트 — 심화모드(심볼 덱/주머니) 코어 1/4 슬라이스, WEB_PARITY_DESIGN.md §1-A #19.
    // 범위: 심볼 카탈로그 72종 전사·주머니 추출(PouchOps.PouchDraw)·덱 검증 7규칙(Pouch.Validate)·
    // 압축 패널티+EARLY_QUOTA(DeepRunHooks.DeepPenalty)·deepPity(DeepRunHooks.ApplyDeepPity)·instant
    // 소모(DeepRunHooks.ConsumeInstantSymbols)·점수 격리(BestDeepScore/BestDeepStage)·asc 상호배제·
    // 자동플레이 스모크. 심볼퍽/정비소/전공/잭팟태그(발동)/피버/오퍼는 P7-2/3, UI 보드는 P7-4 — 이
    // 파일은 그 영역을 검증하지 않는다(데이터/게이트만 있고 로직은 아직 없음).
    internal static class Tests_P7_1_Pouch
    {
        public static void Run(TestCtx t)
        {
            NewSymbolsGolden(t);
            SymbolCatalogCrossReference(t);
            StartPouchGolden(t);
            PouchTotalAndCompressionPenalty(t);
            EarlyQuotaTable(t);
            PouchValidateRules(t);
            PouchDrawDistribution(t);
            PouchDrawEmptyRandomHandling(t);
            PouchDrawIgnoresUnknownIds(t);
            DeepPityReplacement(t);
            InstantSymbolConsumption(t);
            DeepModeAscMutualExclusion(t);
            ScoreIsolationDeep(t);
            DeepRunGetsPlayerXp(t);
            DeepRunAutoplaySmoke(t);
        }

        // ── 신규 58종 손전사 골든 — 웹 data.js SYMS L45-130을 다시 읽어 옮긴 표(Symbols.cs를 보지
        // 않고 별도로 옮긴 축, Tests_P3_4_ContentGolden.cs와 동일한 취지의 독립 대조) ──
        private static void NewSymbolsGolden(TestCtx t)
        {
            AssertNewSym(t, Sym.CherryRipe, "cherry_ripe", "🍑", "숙성체리", 6, 0, 0, Sp.NONE, false, new[] { "생명" });
            AssertNewSym(t, Sym.Tome, "tome", "📖", "족보", 12, 0, 0, Sp.NONE, false, new[] { "학습" });
            AssertNewSym(t, Sym.GemCut, "gem_cut", "💠", "연마보석", 2, 32, 0, Sp.NONE, false, new[] { "점수" });
            AssertNewSym(t, Sym.CoinBag, "coin_bag", "💰", "돈주머니", 0, 0, 3, Sp.COIN, false, new[] { "코인" });
            AssertNewSym(t, Sym.SkullBlack, "skull_black", "💀", "검은해골", 0, 0, 0, Sp.SKULL, false, new[] { "저주" });
            AssertNewSym(t, Sym.Ember, "ember", "🎇", "불씨", 0, 0, 0, Sp.FLAME, false, new[] { "배율" });
            AssertNewSym(t, Sym.SeedBasic, "seed_basic", "🌱", "씨앗", 0, 0, 0, Sp.SEED_ANY, false, new[] { "생명", "성장" });
            AssertNewSym(t, Sym.Sprout, "sprout", "🌿", "새싹", 0, 0, 0, Sp.SEED_HIGH, false, new[] { "생명", "성장" });
            AssertNewSym(t, Sym.Catalyst, "catalyst", "🧪", "촉매", 0, 0, 0, Sp.CATALYST, false, new[] { "변환" });
            AssertNewSym(t, Sym.Purifier, "purifier", "🧹", "정화도구", 0, 0, 0, Sp.PURIFY, false, new[] { "변환", "정화" });
            AssertNewSym(t, Sym.MagicWand, "magic_wand", "🪄", "마법봉", 0, 0, 0, Sp.WANDWILD, true, new[] { "변환", "조작", "희귀" });
            AssertNewSym(t, Sym.MirrorSym, "mirror_sym", "🪞", "거울", 0, 0, 0, Sp.MIRROR, false, new[] { "변환" });
            AssertNewSym(t, Sym.Target, "target", "🎯", "표적", 0, 0, 0, Sp.TARGET, false, new[] { "위치" });
            AssertNewSym(t, Sym.Jigsaw, "jigsaw", "🧩", "퍼즐", 0, 0, 0, Sp.PUZZLE5, false, new[] { "위치" });
            AssertNewSym(t, Sym.Alarm, "alarm", "⏰", "알람", 0, 0, 0, Sp.ALARM, false, new[] { "시간" });
            AssertNewSym(t, Sym.Hourglass, "hourglass", "⏳", "모래시계", 0, 0, 0, Sp.HOURGLASS, true, new[] { "시간", "희귀" });
            AssertNewSym(t, Sym.Receipt, "receipt", "🧾", "영수증", 0, 0, 0, Sp.RECEIPT, false, new[] { "상점" });
            AssertNewSym(t, Sym.Coupon, "coupon", "🎟", "쿠폰", 0, 0, 0, Sp.COUPON, true, new[] { "상점", "희귀" });
            AssertNewSym(t, Sym.Cart, "cart", "🛒", "장바구니", 0, 0, 0, Sp.CART, true, new[] { "상점", "희귀" });
            AssertNewSym(t, Sym.Highlighter, "highlighter", "🖍", "형광펜", 0, 0, 0, Sp.AUGCHANCE, false, new[] { "증강" });
            AssertNewSym(t, Sym.ReviewBook, "review_book", "📚", "복습책", 0, 0, 0, Sp.AUGLEVEL, true, new[] { "증강", "희귀" });
            AssertNewSym(t, Sym.BatterySym, "battery_sym", "🔋", "배터리", 0, 0, 0, Sp.DEVCD, false, new[] { "장치" });
            AssertNewSym(t, Sym.Gear, "gear", "⚙", "톱니바퀴", 0, 0, 0, Sp.GEAR, false, new[] { "장치" });
            AssertNewSym(t, Sym.RepairKit, "repair_kit", "🧰", "정비키트", 0, 0, 0, Sp.KIT, true, new[] { "장치", "희귀" });
            AssertNewSym(t, Sym.SetPiece, "set_piece", "🧩", "세트조각", 0, 0, 0, Sp.SETFRAG, false, new[] { "세트" });
            AssertNewSym(t, Sym.KeyGold, "key_gold", "🗝", "열쇠", 6, 0, 0, Sp.KEY, false, new[] { "세트", "열쇠" });
            AssertNewSym(t, Sym.Shield, "shield", "🛡", "방패", 0, 0, 0, Sp.SHIELD, false, new[] { "보스" });
            AssertNewSym(t, Sym.ExamPaper, "exam_paper", "📋", "시험지", 0, 0, 0, Sp.EXEMPT, false, new[] { "보스" });
            AssertNewSym(t, Sym.Bloodrop, "bloodrop", "🩸", "피방울", 8, 0, 0, Sp.CURSE_BLOOD, false, new[] { "저주" });
            AssertNewSym(t, Sym.BlackCandleSym, "black_candle_sym", "🕯", "검은초", 0, 0, 0, Sp.CURSE_CANDLE, false, new[] { "저주" });
            AssertNewSym(t, Sym.UnstableBomb, "unstable_bomb", "🧨", "불안정폭탄", 0, 0, 0, Sp.CURSE_BOOM, false, new[] { "저주" });
            AssertNewSym(t, Sym.CurseEye, "curse_eye", "🧿", "저주눈", 0, 0, 0, Sp.CURSE_EYE, false, new[] { "저주" });
            AssertNewSym(t, Sym.Lucky7, "lucky7", "7️⃣", "럭키7", 0, 0, 0, Sp.LUCKY7, true, new[] { "전설", "희귀" });
            AssertNewSym(t, Sym.PrismSym, "prism_sym", "🌈", "프리즘", 0, 0, 0, Sp.PRISM_SYM, true, new[] { "전설", "희귀" });
            AssertNewSym(t, Sym.Bandage, "bandage", "🩹", "붕대", 0, 0, 0, Sp.BANDAGE, false, new[] { "보호" });
            AssertNewSym(t, Sym.Knot, "knot", "🪢", "매듭", 0, 0, 0, Sp.KNOT, false, new[] { "위치" });
            AssertNewSym(t, Sym.Safepin, "safepin", "🧷", "안전핀노트", 0, 0, 0, Sp.SAFEPIN, false, new[] { "증강" });
            AssertNewSym(t, Sym.Energypack, "energypack", "🧃", "에너지팩", 0, 0, 0, Sp.ENERGYPACK, false, new[] { "배율" });
            AssertNewSym(t, Sym.Crystal, "crystal", "🔮", "수정구", 0, 0, 0, Sp.CRYSTAL, false, new[] { "보상" });
            AssertNewSym(t, Sym.TempWild, "temp_wild", "🧲", "임시와일드", 0, 0, 0, Sp.TEMPWILD, false, new[] { "조작" });
            AssertNewSym(t, Sym.FakeCrownSym, "fake_crown_sym", "👑", "가짜왕관", 0, 0, 0, Sp.FAKECROWN, true, new[] { "희귀", "왕관" });
            AssertNewSym(t, Sym.FateVortex, "fate_vortex", "🌀", "운명의소용돌이", 0, 0, 0, Sp.FATEVORTEX, true, new[] { "희귀", "운" });
            AssertNewSym(t, Sym.EvoCore, "evo_core", "🧬", "진화핵", 0, 0, 0, Sp.EVOCORE, true, new[] { "희귀", "변환" });
            AssertNewSym(t, Sym.BlackCard, "black_card", "💳", "검은카드", 0, 0, 0, Sp.BLACKCARD, false, new[] { "저주", "상점" });
            AssertNewSym(t, Sym.Shackle, "shackle", "⛓", "족쇄", 0, 0, 0, Sp.SHACKLE, false, new[] { "저주" });
            AssertNewSym(t, Sym.SmallBell, "small_bell", "🔔", "작은종", 0, 0, 0, Sp.BELL_SMALL, false, new[] { "종" });
            AssertNewSym(t, Sym.EchoBell, "echo_bell", "🔔", "울림종", 0, 0, 0, Sp.BELL_ECHO, false, new[] { "종" });
            AssertNewSym(t, Sym.GoldenBell, "golden_bell", "🔔", "황금종", 0, 0, 0, Sp.BELL_GOLD, false, new[] { "종" });
            AssertNewSym(t, Sym.FestivalBell, "festival_bell", "🎊", "축제종", 0, 0, 0, Sp.BELL_FEST, false, new[] { "종" });
            AssertNewSym(t, Sym.BellTicket, "bell_ticket", "🎟", "종소리티켓", 0, 0, 0, Sp.BELL_TICKET, false, new[] { "종" });
            AssertNewSym(t, Sym.ReachMark, "reach_mark", "🎯", "리치표식", 0, 0, 0, Sp.REACH_MARK, false, new[] { "리치" });
            AssertNewSym(t, Sym.RetryReel, "retry_reel", "🔁", "재도전릴", 0, 0, 0, Sp.RETRY_REEL, false, new[] { "리치" });
            AssertNewSym(t, Sym.JackpotTicket, "jackpot_ticket", "🎟", "잭팟티켓", 0, 0, 0, Sp.JACKPOT_TICKET, false, new[] { "리치" });
            AssertNewSym(t, Sym.SlotShard, "slot_shard", "🎰", "슬롯조각", 0, 0, 0, Sp.SLOT_SHARD, false, new[] { "잭팟" });
            AssertNewSym(t, Sym.JackpotWand, "jackpot_wand", "🪄", "잭팟마법봉", 0, 0, 0, Sp.JACKPOT_WAND, false, new[] { "잭팟" });
            AssertNewSym(t, Sym.Cheer, "cheer", "📣", "환호", 0, 0, 0, Sp.CHEER, false, new[] { "잭팟" });
            AssertNewSym(t, Sym.BigBoom, "big_boom", "💥", "대폭죽", 0, 0, 0, Sp.BIG_BOOM, false, new[] { "잭팟" });
            AssertNewSym(t, Sym.JackpotCrown, "jackpot_crown", "👑", "잭팟왕관", 0, 0, 0, Sp.JACKPOT_CROWN, true, new[] { "잭팟", "희귀" });
        }

        private static void AssertNewSym(
            TestCtx t, Sym sym, string id, string emoji, string name,
            long exp, long score, long coin, Sp special, bool rare, string[] tags)
        {
            var s = Symbols.BySym(sym);
            t.Eq(id, s.id, $"{id}.id");
            t.Eq(emoji, s.emoji, $"{id}.emoji");
            t.Eq(name, s.name, $"{id}.name");
            t.Eq(exp, s.exp, $"{id}.exp");
            t.Eq(score, s.score, $"{id}.score");
            t.Eq(coin, s.coin, $"{id}.coin");
            t.Eq(0, s.weight, $"{id}.weight==0(휴면)");
            t.Eq(special, s.special, $"{id}.special");
            t.Eq(rare, s.rare, $"{id}.rare");
            t.True(s.dormant, $"{id}.dormant==true");
            TestHelpers.CollectionEq(t, tags, s.tags, $"{id}.tags");
            var byId = Symbols.ById(id);
            t.True(byId != null && byId.id == id, $"Symbols.ById(\"{id}\") resolves");
        }

        // ── 심볼 카탈로그 ↔ 주머니 후보 풀(Pouch.Symbols71) 교차 대조 ──────────────────────────
        // SYMS 72종 - {key,dice,seed}(머신 전용, 주머니 MVP 제외) + {empty,random}(비-SYMS 센티널)
        // = POUCH_SYMBOLS 71종. Pouch.Cat/Pouch.Rarity도 이 71종을 키로 갖는다(전수 커버리지).
        private static void SymbolCatalogCrossReference(TestCtx t)
        {
            t.Eq(71, Pouch.Symbols71.Length, "Pouch.Symbols71 개수(웹 POUCH_SYMBOLS)");
            t.Eq(71, new HashSet<string>(Pouch.Symbols71).Count, "Pouch.Symbols71 중복 없음");
            t.Eq(58, Pouch.DefaultUnlocked.Count, "Pouch.DefaultUnlocked 개수(웹 DEFAULT_UNLOCKED_SYMS)");
            t.Eq(71, Pouch.Cat.Count, "Pouch.Cat 개수(71 — empty/random 포함)");
            t.Eq(71, Pouch.Rarity.Count, "Pouch.Rarity 개수(71 — empty/random 포함)");
            t.Eq(12, Pouch.Use.Count, "Pouch.Use 개수(instant+fuse 12종)");

            var excludedFromPouch = new HashSet<string> { "key", "dice", "seed" };
            var pouchSet = new HashSet<string>(Pouch.Symbols71);
            foreach (var s in Symbols.All)
            {
                if (excludedFromPouch.Contains(s.id))
                    t.True(!pouchSet.Contains(s.id), $"{s.id}는 머신 전용이라 Pouch.Symbols71 제외");
                else
                    t.True(pouchSet.Contains(s.id), $"{s.id}는 Pouch.Symbols71에 포함돼야 함");
            }
            t.True(pouchSet.Contains("empty"), "empty(빈칸 센티널) 포함");
            t.True(pouchSet.Contains("random"), "random(랜덤칸 센티널) 포함");
            t.True(Symbols.ById("empty") == null, "empty는 Symbols.All(SYMS)에 없음(센티널)");
            t.True(Symbols.ById("random") == null, "random은 Symbols.All(SYMS)에 없음(센티널)");

            // DefaultUnlocked ⊆ Symbols71(후보 풀 밖의 심볼을 해금할 수는 없음).
            foreach (var id in Pouch.DefaultUnlocked)
                t.True(pouchSet.Contains(id), $"DefaultUnlocked[{id}] ⊆ Symbols71");

            // Cat/Rarity 키 집합도 Symbols71과 정확히 일치.
            TestHelpers.CollectionEq(t,
                pouchSet.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                Pouch.Cat.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                "Pouch.Cat 키 집합 == Symbols71(정렬 비교)");
            TestHelpers.CollectionEq(t,
                pouchSet.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                Pouch.Rarity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                "Pouch.Rarity 키 집합 == Symbols71(정렬 비교)");
        }

        // ── START_POUCH(총 30, 왕관/empty/random 시작 제외) ──────────────────────────────────
        private static void StartPouchGolden(TestCtx t)
        {
            var p = Pouch.NewStartPouch();
            t.Eq(9, p.Count, "START_POUCH kinds == 9");
            var expected = new Dictionary<string, int>
            {
                { "cherry", 6 }, { "book", 5 }, { "star", 4 }, { "gem", 4 }, { "coin", 4 },
                { "skull", 3 }, { "flame", 2 }, { "magnet", 1 }, { "bomb", 1 },
            };
            foreach (var kv in expected)
                t.True(p.TryGetValue(kv.Key, out var n) && n == kv.Value, $"START_POUCH[{kv.Key}] == {kv.Value}(실제 {(p.TryGetValue(kv.Key, out var n2) ? n2 : -1)})");
            t.Eq(30, Pouch.Total(p), "START_POUCH total == 30(DECK_BASE)");
            t.True(!p.ContainsKey("crown"), "START_POUCH는 왕관 제외");
            t.True(!p.ContainsKey("empty") && !p.ContainsKey("random"), "START_POUCH는 empty/random 제외");

            // 매번 새 Dictionary 반환(런 간 공유 금지).
            p["cherry"] = 999;
            var p2 = Pouch.NewStartPouch();
            t.Eq(6, p2["cherry"], "NewStartPouch는 매번 새 Dictionary(이전 호출 결과와 공유 아님)");

            var validated = Pouch.Validate(p2);
            t.True(validated.Ok, "START_POUCH 자체는 항상 유효한 덱(7규칙 전부 통과)");
            t.Eq(0, validated.Errors.Count, "START_POUCH validate 에러 0건");
        }

        // ── pouchTotal / compressionPenalty ──────────────────────────────────────────────────
        private static void PouchTotalAndCompressionPenalty(TestCtx t)
        {
            t.Eq(0, Pouch.Total(null), "Total(null) == 0");
            var d = new Dictionary<string, int> { { "cherry", 5 }, { "book", -3 }, { "star", 0 } };
            t.Eq(5, Pouch.Total(d), "Total은 양수 카운트만 합산(음수/0 무시)");

            // 압축 패널티 표(le 오름차순, total<=le 첫 매치) — 웹 DEEP.COMPRESSION 그대로.
            t.EqTol(1.15, Pouch.CompressionPenalty(20), "total=20(<=22) -> 1.15");
            t.EqTol(1.15, Pouch.CompressionPenalty(22), "total=22(==22 경계) -> 1.15");
            t.EqTol(1.09, Pouch.CompressionPenalty(23), "total=23(>22,<=24) -> 1.09");
            t.EqTol(1.09, Pouch.CompressionPenalty(24), "total=24(==24 경계) -> 1.09");
            t.EqTol(1.06, Pouch.CompressionPenalty(25), "total=25(>24,<=26) -> 1.06");
            t.EqTol(1.06, Pouch.CompressionPenalty(26), "total=26(==26 경계) -> 1.06");
            t.EqTol(1.03, Pouch.CompressionPenalty(27), "total=27(>26,<=28) -> 1.03");
            t.EqTol(1.03, Pouch.CompressionPenalty(28), "total=28(==28 경계) -> 1.03");
            t.EqTol(1.0, Pouch.CompressionPenalty(29), "total=29(>28) -> 1(패널티 없음)");
            t.EqTol(1.0, Pouch.CompressionPenalty(30), "total=30(시작덱 기본) -> 1");
            t.EqTol(1.0, Pouch.CompressionPenalty(40), "total=40(최대) -> 1");
        }

        // ── EARLY_QUOTA(stage 1~4 완화 램프, 5+ 무배율) ──────────────────────────────────────
        private static void EarlyQuotaTable(TestCtx t)
        {
            t.EqTol(0.75, Pouch.EarlyQuotaMul(1), "stage1 -> 0.75");
            t.EqTol(0.85, Pouch.EarlyQuotaMul(2), "stage2 -> 0.85");
            t.EqTol(0.90, Pouch.EarlyQuotaMul(3), "stage3 -> 0.90");
            t.EqTol(0.95, Pouch.EarlyQuotaMul(4), "stage4 -> 0.95");
            t.EqTol(1.0, Pouch.EarlyQuotaMul(5), "stage5 -> 1.0(무배율)");
            t.EqTol(1.0, Pouch.EarlyQuotaMul(15), "stage15 -> 1.0");

            // DeepRunHooks.DeepPenalty 배선 — 일반모드는 항상 1, 심화모드는 base*compress*earlyQuota.
            var normalRun = S4TestHelpers.NewRun(9001L);
            t.EqTol(1.0, DeepRunHooks.DeepPenalty(normalRun), "[배선] DeepMode=false면 DeepPenalty 항상 1.0");

            var deepRun = MakeDeepRun(9002L);
            deepRun.Stage = 1; // EARLY_QUOTA[1]=0.75, 시작덱 total=30 -> compressionPenalty=1.0
            t.EqTol(0.75, DeepRunHooks.DeepPenalty(deepRun), "[배선] stage1 시작덱 -> compression(1.0)*early(0.75)=0.75");

            deepRun.Stage = 5;
            t.EqTol(1.0, DeepRunHooks.DeepPenalty(deepRun), "[배선] stage5 -> early(1.0) 무배율");

            // 압축된 덱(총량 22 이하) + stage1 조합 손계산.
            var compressedPouch = new Dictionary<string, int> { { "cherry", 22 } };
            deepRun.Pouch.Clear();
            foreach (var kv in compressedPouch) deepRun.Pouch[kv.Key] = kv.Value;
            deepRun.Stage = 1;
            t.EqTol(1.15 * 0.75, DeepRunHooks.DeepPenalty(deepRun), "[배선] total=22(압축1.15) * stage1(0.75) = 0.8625");

            // deepCompressExtra(정비소 누적, 이 슬라이스는 항상 0이지만 필드 자체 배선 확인).
            deepRun.DeepCompressExtra = 0.05;
            deepRun.Stage = 5;
            t.EqTol(1.15 * 1.05, DeepRunHooks.DeepPenalty(deepRun), "[배선] deepCompressExtra(+5%) 반영 = compression(1.15)*(1+0.05)");
        }

        // ── PouchValidate 7규칙 경계 — 각 규칙을 단독으로(또는 웹과 동일하게 자연히 겹치는 경우
        //    포함해) 위반하는 손구성 덱으로 검증. 유효 덱(baseline)은 0 에러여야 한다. ──────────────
        private static void PouchValidateRules(TestCtx t)
        {
            // 유효 baseline — 7규칙 전부 통과(총량30·종류7·왕관0·와일드0·특수0·태그최대16.7%·잭팟태그0).
            var baseline = new Dictionary<string, int>
            {
                { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 4 }, { "flame", 3 }, { "magnet", 3 },
            };
            var rBase = Pouch.Validate(baseline);
            t.True(rBase.Ok, "[validate] baseline 유효 덱 — Ok=true");
            t.Eq(0, rBase.Errors.Count, "[validate] baseline 에러 0건");
            t.Eq(30, rBase.Total, "[validate] baseline total=30");
            t.Eq(7, rBase.Kinds, "[validate] baseline kinds=7");

            // 경계값 — 총량 정확히 20(최소)·40(최대)도 통과해야 함(strict < / > 비교 확인).
            var totalMinBoundary = new Dictionary<string, int>
            { { "cherry", 3 }, { "book", 3 }, { "star", 3 }, { "gem", 3 }, { "coin", 3 }, { "flame", 3 }, { "magnet", 2 } };
            t.True(Pouch.Validate(totalMinBoundary).Ok, "[validate] total==20(최소 경계) — 통과");
            var totalMaxBoundary = new Dictionary<string, int>
            { { "cherry", 6 }, { "book", 6 }, { "star", 6 }, { "gem", 6 }, { "coin", 6 }, { "flame", 6 }, { "magnet", 4 } };
            t.True(Pouch.Validate(totalMaxBoundary).Ok, "[validate] total==40(최대 경계) — 통과");

            // ① 총량 < 20.
            var lowTotal = new Dictionary<string, int>
            { { "cherry", 3 }, { "book", 3 }, { "star", 3 }, { "gem", 3 }, { "coin", 3 }, { "flame", 2 }, { "magnet", 2 } };
            var r1 = Pouch.Validate(lowTotal);
            t.True(!r1.Ok, "[validate①] 총량<20 — Ok=false");
            t.True(r1.Errors.Any(e => e.Contains("총량 19 < 최소 20")), "[validate①] 에러 메시지 확인");

            // ① 총량 > 40.
            var highTotal = new Dictionary<string, int>
            { { "cherry", 6 }, { "book", 6 }, { "star", 6 }, { "gem", 6 }, { "coin", 6 }, { "flame", 6 }, { "magnet", 5 } };
            var r1b = Pouch.Validate(highTotal);
            t.True(!r1b.Ok, "[validate①b] 총량>40 — Ok=false");
            t.True(r1b.Errors.Any(e => e.Contains("총량 41 > 최대 40")), "[validate①b] 에러 메시지 확인");

            // ② 종류 < 7.
            var fewKinds = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "flame", 5 } };
            var r2 = Pouch.Validate(fewKinds);
            t.True(!r2.Ok, "[validate②] 종류<7 — Ok=false");
            t.True(r2.Errors.Any(e => e.Contains("심볼 종류 6 < 최소 7")), "[validate②] 에러 메시지 확인");

            // ③ 왕관 > 2(동시에 PRISM 특수 상한도 자연히 넘김 — 웹과 동일한 겹침, crown=PRISM 유일).
            var tooManyCrowns = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 4 }, { "coin", 4 }, { "flame", 2 }, { "magnet", 1 }, { "crown", 3 } };
            var r3 = Pouch.Validate(tooManyCrowns);
            t.True(!r3.Ok, "[validate③] 왕관>2 — Ok=false");
            t.True(r3.Errors.Any(e => e.Contains("👑왕관 3 > 상한 2")), "[validate③] 에러 메시지 확인");

            // ③ 경계 — 왕관 == 2(정확히 상한) 통과. crown이 유일한 PRISM-tier 특수라 PRISM_special도
            // 정확히 2로 상한과 일치 — 둘 다 통과해야 한다(Opus 2차검수 [LOW]⑤ "정확히 상한" 케이스).
            var crownAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 4 }, { "flame", 2 }, { "magnet", 1 }, { "crown", 2 } };
            var r3b = Pouch.Validate(crownAtCap);
            t.True(r3b.Ok, "[validate③ 경계] 왕관==2(정확히 상한) — 통과");
            t.Eq(0, r3b.Errors.Count, "[validate③ 경계] 왕관==2 에러 0건(PRISM_special도 동시에 2==2로 통과)");

            // ④ 와일드 > 4(GOLD 특수 상한(6) 이내라 단독 위반).
            var tooManyWilds = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 1 }, { "wild", 5 } };
            var r4 = Pouch.Validate(tooManyWilds);
            t.True(!r4.Ok, "[validate④] 와일드>4 — Ok=false");
            t.True(r4.Errors.Any(e => e.Contains("🌀와일드 5 > 상한 4")), "[validate④] 에러 메시지 확인");
            t.True(!r4.Errors.Any(e => e.Contains("GOLD")), "[validate④] GOLD 특수 상한(6)은 안 넘겨 단독 위반");

            // ④ 경계 — 와일드 == 4(정확히 상한) 통과.
            var wildAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 1 }, { "wild", 4 } };
            var r4b = Pouch.Validate(wildAtCap);
            t.True(r4b.Ok, "[validate④ 경계] 와일드==4(정확히 상한) — 통과");
            t.Eq(0, r4b.Errors.Count, "[validate④ 경계] 와일드==4 에러 0건");

            // ⑤ 특수 티어 상한 — SILVER > 10.
            var silverOver = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 4 }, { "gem", 4 }, { "coin", 4 }, { "magnet", 1 }, { "cherry_ripe", 11 } };
            var r5a = Pouch.Validate(silverOver);
            t.True(!r5a.Ok, "[validate⑤a] SILVER 특수>10 — Ok=false");
            t.True(r5a.Errors.Any(e => e.Contains("특수 SILVER 11 > 상한 10")), "[validate⑤a] 에러 메시지 확인");

            // ⑤ 경계 — SILVER 특수 == 10(정확히 상한) 통과.
            var silverAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 4 }, { "gem", 4 }, { "coin", 4 }, { "magnet", 1 }, { "cherry_ripe", 10 } };
            var r5aBoundary = Pouch.Validate(silverAtCap);
            t.True(r5aBoundary.Ok, "[validate⑤a 경계] SILVER 특수==10(정확히 상한) — 통과");
            t.Eq(0, r5aBoundary.Errors.Count, "[validate⑤a 경계] SILVER==10 에러 0건");

            // ⑤ 특수 티어 상한 — GOLD > 6.
            var goldOver = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 4 }, { "magnet", 1 }, { "magic_wand", 7 } };
            var r5b = Pouch.Validate(goldOver);
            t.True(!r5b.Ok, "[validate⑤b] GOLD 특수>6 — Ok=false");
            t.True(r5b.Errors.Any(e => e.Contains("특수 GOLD 7 > 상한 6")), "[validate⑤b] 에러 메시지 확인");

            // ⑤ 경계 — GOLD 특수 == 6(정확히 상한) 통과.
            var goldAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 4 }, { "magnet", 1 }, { "magic_wand", 6 } };
            var r5bBoundary = Pouch.Validate(goldAtCap);
            t.True(r5bBoundary.Ok, "[validate⑤b 경계] GOLD 특수==6(정확히 상한) — 통과");
            t.Eq(0, r5bBoundary.Errors.Count, "[validate⑤b 경계] GOLD==6 에러 0건");

            // ⑤ 특수 티어 상한 — PRISM > 2(lucky7, crown과 별개 케이스).
            var prismOver = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 2 }, { "lucky7", 3 } };
            var r5c = Pouch.Validate(prismOver);
            t.True(!r5c.Ok, "[validate⑤c] PRISM 특수>2 — Ok=false");
            t.True(r5c.Errors.Any(e => e.Contains("특수 PRISM 3 > 상한 2")), "[validate⑤c] 에러 메시지 확인");

            // ⑤ 경계 — PRISM 특수 == 2(정확히 상한) 통과.
            var prismAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 2 }, { "lucky7", 2 } };
            var r5cBoundary = Pouch.Validate(prismAtCap);
            t.True(r5cBoundary.Ok, "[validate⑤c 경계] PRISM 특수==2(정확히 상한) — 통과");
            t.Eq(0, r5cBoundary.Errors.Count, "[validate⑤c 경계] PRISM==2 에러 0건");

            // ⑥ 같은 태그 > 60%(저주 태그, harmful이라 특수 상한/잭팟태그 상한과 무관하게 단독 위반).
            var tagOver = new Dictionary<string, int>
            { { "skull", 19 }, { "cherry", 2 }, { "book", 2 }, { "star", 2 }, { "gem", 2 }, { "coin", 2 }, { "flame", 2 } };
            var r6 = Pouch.Validate(tagOver);
            t.True(!r6.Ok, "[validate⑥] 태그 비중>60% — Ok=false");
            t.True(r6.Errors.Any(e => e.Contains("#저주") && e.Contains("> 60%")), "[validate⑥] 에러 메시지 확인(#저주 ... > 60%)");
            t.True(!r6.Errors.Any(e => e.Contains("특수") || e.Contains("잭팟태그")), "[validate⑥] 특수/잭팟태그 상한과 무관하게 단독 위반");

            // 태그 비중 == 60% 정확히는 통과해야 함(strict > 비교 확인) — kinds=7 충족하며
            // skull(저주)=12/20=0.60 정확히 되도록 구성: total = 12+2+2+1+1+1+1 = 20.
            var tag60 = new Dictionary<string, int>
            { { "skull", 12 }, { "cherry", 2 }, { "book", 2 }, { "star", 1 }, { "gem", 1 }, { "coin", 1 }, { "magnet", 1 } };
            var r6b = Pouch.Validate(tag60);
            t.Eq(20, r6b.Total, "[validate⑥ 경계] total=20");
            t.True(!r6b.Errors.Any(e => e.Contains("#저주")), "[validate⑥ 경계] 태그비중==60% 정확히는 통과(strict > 비교)");

            // ⑦ 잭팟태그 특수심볼 > 8(SILVER 특수 상한(10) 이내라 단독 위반, bell 태그).
            var jackpotTagOver = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 1 }, { "small_bell", 9 } };
            var r7 = Pouch.Validate(jackpotTagOver);
            t.True(!r7.Ok, "[validate⑦] 잭팟태그 특수>8 — Ok=false");
            t.True(r7.Errors.Any(e => e.Contains("잭팟태그 #bell 특수심볼 9 > 상한 8")), "[validate⑦] 에러 메시지 확인");
            t.True(!r7.Errors.Any(e => e.Contains("특수 SILVER")), "[validate⑦] SILVER 특수 상한(10)은 안 넘겨 단독 위반");

            // ⑦ 경계 — 잭팟태그 특수 == 8(정확히 상한) 통과.
            var jackpotTagAtCap = new Dictionary<string, int>
            { { "cherry", 5 }, { "book", 5 }, { "star", 5 }, { "gem", 5 }, { "coin", 5 }, { "magnet", 1 }, { "small_bell", 8 } };
            var r7Boundary = Pouch.Validate(jackpotTagAtCap);
            t.True(r7Boundary.Ok, "[validate⑦ 경계] 잭팟태그 특수==8(정확히 상한) — 통과");
            t.Eq(0, r7Boundary.Errors.Count, "[validate⑦ 경계] 잭팟태그==8 에러 0건");

            // opts 오버라이드 — totalMin/totalMax/tagMaxRatio를 바꾸면 그 값 기준으로 판정(P7-2/3
            // 정비소 확장·전공 태그완화가 쓸 구조, 이 슬라이스는 항상 기본값만 호출하지만 구조 검증).
            var smallPouch = new Dictionary<string, int> { { "cherry", 15 } };
            var rOverride = Pouch.Validate(smallPouch, totalMinOverride: 10, totalMaxOverride: 50, tagMaxRatioOverride: 1.0);
            t.True(!rOverride.Errors.Any(e => e.Contains("총량")), "[validate opts] totalMin/Max 오버라이드 반영");
        }

        // ── PouchOps.PouchDraw — 비중 가중 + 결정론(같은 seed -> 같은 결과열) ──────────────────
        private static void PouchDrawDistribution(TestCtx t)
        {
            var pouch = new Dictionary<string, int> { { "cherry", 3 }, { "book", 1 } }; // 75% / 25%
            var rng = new Rng(12345);
            int cherryCount = 0, bookCount = 0;
            const int trials = 20000;
            for (int i = 0; i < trials; i++)
            {
                var cells = PouchOps.PouchDraw(rng, pouch, 1);
                if (cells[0].sym.id == "cherry") cherryCount++;
                else if (cells[0].sym.id == "book") bookCount++;
                else t.Fail("[dist] 예상 밖 심볼", cells[0].sym.id);
            }
            t.Eq(trials, cherryCount + bookCount, "[dist] 2종만 있으면 전부 그 둘 중 하나");
            double cherryRatio = (double)cherryCount / trials;
            t.True(Math.Abs(cherryRatio - 0.75) < 0.02, $"[dist] cherry 비중 ~75%(실측 {cherryRatio:P2})");

            // 결정론 — 같은 seed면 같은 결과열(자체 재현성, ENGINE_PORT_DESIGN.md 원칙 2).
            var biggerPouch = Pouch.NewStartPouch();
            var seqA = new List<string>();
            var seqB = new List<string>();
            var rngA = new Rng(555);
            var rngB = new Rng(555);
            for (int i = 0; i < 50; i++)
            {
                seqA.AddRange(PouchOps.PouchDraw(rngA, biggerPouch, 5).Select(c => c.sym.id));
                seqB.AddRange(PouchOps.PouchDraw(rngB, biggerPouch, 5).Select(c => c.sym.id));
            }
            TestHelpers.CollectionEq(t, seqA, seqB, "[dist] 같은 seed -> 같은 결과열(자체 재현성)");

            // 총량 0/빈 주머니는 전부 빈칸(방어).
            var emptyPouch = new Dictionary<string, int>();
            var cellsEmpty = PouchOps.PouchDraw(new Rng(1), emptyPouch, 5);
            foreach (var c in cellsEmpty) t.Eq("empty", c.sym.id, "[dist] 빈 주머니 -> 전부 빈칸");
        }

        // ── empty/random 특수 칸 처리 ─────────────────────────────────────────────────────────
        private static void PouchDrawEmptyRandomHandling(TestCtx t)
        {
            var onlyEmpty = new Dictionary<string, int> { { "empty", 5 } };
            var cells = PouchOps.PouchDraw(new Rng(1), onlyEmpty, 5);
            foreach (var c in cells) t.Eq("empty", c.sym.id, "[empty전용] 전부 빈칸");

            // random만 있고 실심볼이 없으면 -> 재추첨 pool 없음 -> 빈칸 폴백.
            var onlyRandom = new Dictionary<string, int> { { "random", 5 } };
            var cells2 = PouchOps.PouchDraw(new Rng(2), onlyRandom, 5);
            foreach (var c in cells2) t.Eq("empty", c.sym.id, "[random전용,실심볼없음] 빈칸 폴백");

            // random + cherry(유일한 실심볼) — random 경로든 직접 경로든 결과는 항상 cherry.
            var randomPlusCherry = new Dictionary<string, int> { { "random", 10 }, { "cherry", 1 } };
            var rng3 = new Rng(3);
            bool sawRandomTag = false;
            for (int i = 0; i < 200; i++)
            {
                var c = PouchOps.PouchDraw(rng3, randomPlusCherry, 1)[0];
                t.Eq("cherry", c.sym.id, "[random+cherry] 어느 경로든 결과는 cherry(유일한 실심볼)");
                if (c.tag == "🎲칸") sawRandomTag = true;
            }
            t.True(sawRandomTag, "[random+cherry] random 재추첨 경로가 실제로 발동(🎲칸 태그 관측)");
        }

        // ── Opus 2차검수(P7-1, 2026-08-09) [LOW]⑤ — PouchOps의 미지 id 방어. `Pouch.Symbols71`에
        // 없는 id가 파운치 딕셔너리에 섞여도 PouchDraw는 그 항목을 절대 뽑지 않는다(스캔 자체가
        // Symbols71 고정 목록만 순회 — PouchOps.cs 헤더 각주 참조). 이 테스트는 그 계약을 직접
        // 관측한다: 미지 id "totally_unknown_id"에 압도적 카운트를 줘도 매 추출 결과가 여전히
        // 알려진 id(cherry)로만 나오고, `Pouch.Total()`은(딕셔너리 값을 그대로 합산하므로) 미지
        // id의 카운트까지 포함해 "실제로 뽑히는 총량"보다 커질 수 있음도 함께 확인한다.
        private static void PouchDrawIgnoresUnknownIds(TestCtx t)
        {
            // 결정론적 구조 불변식(확률 시행이 아니다 — 매 추출이 항상 성립해야 함)이라 소수 반복으로도
            // 충분하다(20회×5칸=100 관측).
            var pouch = new Dictionary<string, int> { { "cherry", 1 }, { "totally_unknown_id", 999 } };
            var rng = new Rng(42);
            for (int i = 0; i < 20; i++)
            {
                var cells = PouchOps.PouchDraw(rng, pouch, 5);
                foreach (var c in cells)
                    t.Eq("cherry", c.sym.id, "[미지id방어] Symbols71 밖 id는 절대 뽑히지 않음(cherry만 등장)");
            }
            // Pouch.Total()은 딕셔너리 값을 그대로 합산하므로 미지 id의 카운트(999)까지 포함해
            // 1000을 반환한다 — 위 500회 추출은 전부 cherry뿐이었으므로("실제로 뽑히는 총량"은
            // 사실상 1) Total()과 draw 확률 공간이 갈라질 수 있다는 주석 근거를 직접 실증한다.
            t.Eq(1000, Pouch.Total(pouch), "[미지id방어] Pouch.Total()은 미지 id 카운트도 그대로 합산(1+999)");
        }

        // ── deepPity — 획득 심볼 2스핀 보장(DeepRunHooks.ApplyDeepPity) ──────────────────────
        private static void DeepPityReplacement(TestCtx t)
        {
            // 자연 등장 시 소진(치환 없음).
            var run1 = MakeDeepRun(101L);
            run1.DeepPity = new DeepPityState("crown", 2);
            var raw1 = new List<Cell>
            {
                new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("crown")),
                new Cell(Symbols.ById("book")), new Cell(Symbols.ById("star")), new Cell(Symbols.ById("gem")),
            };
            var result1 = DeepRunHooks.ApplyDeepPity(run1, raw1);
            t.True(run1.DeepPity == null, "[pity] 자연 등장 시 소진(null)");
            t.Eq("crown", result1[1].sym.id, "[pity] 자연 등장 셀은 그대로 유지");

            // 미등장 시 무작위 1칸 강제 치환.
            var run2 = MakeDeepRun(102L);
            run2.DeepPity = new DeepPityState("lucky7", 2);
            var raw2 = new List<Cell>
            {
                new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("book")),
                new Cell(Symbols.ById("star")), new Cell(Symbols.ById("gem")), new Cell(Symbols.ById("coin")),
            };
            var result2 = DeepRunHooks.ApplyDeepPity(run2, raw2);
            t.True(run2.DeepPity == null, "[pity] 미등장이어도 1회 치환 후 소진");
            t.Eq(1, result2.Count(c => c.sym.id == "lucky7"), "[pity] 치환은 정확히 1칸만");
            t.True(result2.Any(c => c.tag == "✨→"), "[pity] 치환 셀 태그 ✨→");

            // DeepMode=false면 무시(일반 런은 pity 개념 자체가 없음 — 상태도 건드리지 않음).
            var run3 = S4TestHelpers.NewRun(103L);
            run3.DeepPity = new DeepPityState("lucky7", 2);
            var raw3 = new List<Cell>(raw2.Select(c => new Cell(c.sym, c.tag)));
            var result3 = DeepRunHooks.ApplyDeepPity(run3, raw3);
            t.True(run3.DeepPity != null, "[pity] DeepMode=false면 상태 자체를 건드리지 않음");
            TestHelpers.CollectionEq(t, raw3.Select(c => c.sym.id).ToList(), result3.Select(c => c.sym.id).ToList(), "[pity] DeepMode=false면 셀 무변형");

            // spinsLeft 만료 안전망 — 이미 소진 임계인 상태에서 미등장 시 그냥 소진(치환 없음).
            var run4 = MakeDeepRun(104L);
            run4.DeepPity = new DeepPityState("lucky7", 0);
            var raw4 = new List<Cell>
            {
                new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("book")),
                new Cell(Symbols.ById("star")), new Cell(Symbols.ById("gem")), new Cell(Symbols.ById("coin")),
            };
            var result4 = DeepRunHooks.ApplyDeepPity(run4, raw4);
            t.True(run4.DeepPity == null, "[pity] 만료 안전망도 소진");
            t.True(!result4.Any(c => c.sym.id == "lucky7"), "[pity] 만료 시 강제 치환 없음");
        }

        // ── instant 소모(작업 지시 6번 단순화 버전) — 등장 즉시 덱에서 "id당 최대 1개" 제거, fuse는
        // 이번 슬라이스 미소비. Opus 2차검수(P7-1, 2026-08-09) [MED]③ — 웹 game.js:814-828은
        // evaluate()가 뽑은 "이번 스핀에 등장했는가"(불리언, 개수 아님)만 보고 remove n:1을 최대 1회
        // 적용한다 — 같은 instant 심볼이 5칸 중 여러 번 등장해도 덱에서는 정확히 1개만 빠진다.
        private static void InstantSymbolConsumption(TestCtx t)
        {
            var run = MakeDeepRun(301L);
            run.Pouch.Clear();
            run.Pouch["bandage"] = 1;
            var raw = new List<Cell>
            {
                new Cell(Symbols.ById("bandage")), new Cell(Symbols.ById("cherry")),
                new Cell(Symbols.ById("bandage")), new Cell(Symbols.ById("book")), new Cell(Symbols.ById("star")),
            };
            DeepRunHooks.ConsumeInstantSymbols(run, raw);
            t.True(!run.Pouch.ContainsKey("bandage"), "[instant] 소진되면 키 자체 제거(0 하한)");

            var run2 = MakeDeepRun(302L);
            run2.Pouch.Clear();
            run2.Pouch["safepin"] = 3;
            var raw2 = new List<Cell> { new Cell(Symbols.ById("safepin")) };
            DeepRunHooks.ConsumeInstantSymbols(run2, raw2);
            t.Eq(3, run2.Pouch["safepin"], "[instant] fuse 심볼은 이번 슬라이스에서 미소비(그대로 3)");

            // 웹 game.js:814-828 golden — 같은 instant 심볼(knot)이 5칸 중 2번 등장해도 "등장했는가"만
            // 보므로 덱에서는 정확히 1개만 빠진다(5→4, 중복 등장이 추가로 깎지 않음).
            var run3 = MakeDeepRun(303L);
            run3.Pouch.Clear();
            run3.Pouch["knot"] = 5;
            var raw3 = new List<Cell> { new Cell(Symbols.ById("knot")), new Cell(Symbols.ById("knot")) };
            DeepRunHooks.ConsumeInstantSymbols(run3, raw3);
            t.Eq(4, run3.Pouch["knot"], "[instant] 웹 golden — 같은 id 중복 등장(2회)도 덱에서는 1개만 차감(5-1=4)");

            // 서로 다른 instant id 2종이 각각 등장하면 각자 최대 1개씩(합쳐서 2개) 빠진다 — "id당" 규칙 확인.
            var run5 = MakeDeepRun(305L);
            run5.Pouch.Clear();
            run5.Pouch["knot"] = 5;
            run5.Pouch["bandage"] = 5;
            var raw5 = new List<Cell>
            {
                new Cell(Symbols.ById("knot")), new Cell(Symbols.ById("knot")), new Cell(Symbols.ById("knot")),
                new Cell(Symbols.ById("bandage")), new Cell(Symbols.ById("bandage")),
            };
            DeepRunHooks.ConsumeInstantSymbols(run5, raw5);
            t.Eq(4, run5.Pouch["knot"], "[instant] knot 3회 등장해도 1개만 차감(5-1=4)");
            t.Eq(4, run5.Pouch["bandage"], "[instant] bandage 2회 등장해도 1개만 차감(5-1=4, id별 독립)");

            var run4 = S4TestHelpers.NewRun(304L);
            run4.Pouch["bandage"] = 1;
            DeepRunHooks.ConsumeInstantSymbols(run4, raw);
            t.Eq(1, run4.Pouch["bandage"], "[instant] DeepMode=false면 무시");
        }

        // ── deep/asc 상호배제 ─────────────────────────────────────────────────────────────────
        private static void DeepModeAscMutualExclusion(TestCtx t)
        {
            var rc = new RunController("novice", "basic", "", 201L, S4TestHelpers.GenerousStat(), asc: 7, deep: true);
            t.Eq(0, rc.State.Asc, "[상호배제] deep=true면 asc가 항상 0으로 강제(요청 asc=7 무시)");
            t.True(rc.State.DeepMode, "[상호배제] DeepMode=true 반영");
            t.Eq(30, Pouch.Total(rc.State.Pouch), "[상호배제] 시작 주머니 총량 30");
            t.True(rc.State.DeepStats != null, "[상호배제] DeepStats 골격 생성");
            t.Eq(30, rc.State.DeepStats.MaxTotal, "[상호배제] DeepStats.MaxTotal = 시작 총량");

            var rc2 = new RunController("novice", "basic", "", 202L, S4TestHelpers.GenerousStat(), asc: 7, deep: false);
            t.Eq(7, rc2.State.Asc, "[대조군] deep=false면 asc 그대로 반영");
            t.True(!rc2.State.DeepMode, "[대조군] DeepMode=false");
            t.Eq(0, rc2.State.Pouch.Count, "[대조군] 일반 런은 Pouch 비어있음");
            t.True(rc2.State.DeepStats == null, "[대조군] DeepStats null");

            // 방어적 재클램프 — 음수 asc를 넘겨도 deep=true면 여전히 0.
            var rc3 = new RunController("novice", "basic", "", 203L, S4TestHelpers.GenerousStat(), asc: -5, deep: true);
            t.Eq(0, rc3.State.Asc, "[상호배제] deep=true + asc=-5 요청도 0");
        }

        // ── 점수 격리 — BestDeepScore/BestDeepStage(bestScore/bestAscScore 오염 없음) ──────────
        private static void ScoreIsolationDeep(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["bestScore"] = 1000L;
            profile.BestAscScore = 2000L;

            var run = MakeDeepRun(401L);
            run.Score = 50000;
            run.Stage = 9;
            var failure = StageFlow.ForceGameOver(run, 0);
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure } }, scratch);

            t.Eq(1000L, profile.GetStat("bestScore"), "[deep점수격리] deep 런은 bestScore 불변");
            t.Eq(2000L, profile.BestAscScore, "[deep점수격리] deep 런은 bestAscScore도 불변");
            t.Eq(failure.finalScore, profile.BestDeepScore, "[deep점수격리] bestDeepScore 갱신");
            t.Eq(9, profile.BestDeepStage, "[deep점수격리] bestDeepStage 갱신");
            t.Eq(failure.finalScore, profile.TotalScore, "[deep점수격리] totalScore는 deep 무관 항상 누적");
            t.Eq(9L, profile.GetStat("bestStage"), "[deep점수격리] bestStage는 deep 무관 항상 갱신");
            t.Eq(1L, profile.GetStat("runs"), "[deep점수격리] runs는 deep 무관 항상 갱신");
            t.Eq("", profile.BestChar, "[deep점수격리] BestChar 오염 없음(deep 런 무반영)");
            t.Eq("", profile.BestMachine, "[deep점수격리] BestMachine 오염 없음(deep 런 무반영)");

            // 더 낮은 점수의 후속 deep 런은 BestDeepScore/Stage를 낮추지 않음(SetMax류 규칙).
            var run2 = MakeDeepRun(402L);
            run2.Score = 1;
            run2.Stage = 2;
            var failure2 = StageFlow.ForceGameOver(run2, 0);
            StatTracker.Apply(profile, run2, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure2 } }, new StatTracker.RunScratch());
            t.Eq(failure.finalScore, profile.BestDeepScore, "[deep점수격리] 더 낮은 점수는 BestDeepScore 갱신 안 함");
            t.Eq(9, profile.BestDeepStage, "[deep점수격리] 더 낮은 점수는 BestDeepStage도 불변");
            t.Eq(2L, profile.GetStat("runs"), "[deep점수격리] runs는 두 런 모두 누적(2)");

            // 대조군 — 일반 런(asc=0, deep=false)은 정상적으로 bestScore를 갱신.
            var profile2 = new PlayerProfile();
            var run3 = S4TestHelpers.NewRun(403L);
            run3.Score = 999999;
            var failure3 = StageFlow.ForceGameOver(run3, 0);
            StatTracker.Apply(profile2, run3, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure3 } }, new StatTracker.RunScratch());
            t.Eq(failure3.finalScore, profile2.GetStat("bestScore"), "[대조군] 일반 런은 bestScore 정상 갱신");
            t.Eq(0L, profile2.BestDeepScore, "[대조군] 일반 런은 BestDeepScore 불변(0)");
        }

        // ── 작업 지시 8번 첫 절 "심화 런도 XP 부여(웹 대조)" — 코드 변경 없이 자연 동작함을 확인.
        //    PlayerLevelTracker.ApplyRunEnd는 run.Stage/finalScore/RunBossClears/newAchievementCount
        //    만 읽는 범용 공식(웹 game.js:2619 runXp)이라 deep/asc 어느 쪽도 게이트하지 않는다. ──────
        private static void DeepRunGetsPlayerXp(TestCtx t)
        {
            var profile = new PlayerProfile();
            long xpBefore = profile.PlayerXp;

            var run = MakeDeepRun(501L);
            run.Stage = 6;
            run.RunBossClears = 1;
            var failure = StageFlow.ForceGameOver(run, 0);
            PlayerLevelTracker.ApplyRunEnd(profile, run, failure, newAchievementCount: 2);

            long expectedXp = Formulas.PlayerRunXp(run.Stage, failure.finalScore, run.RunBossClears, 2);
            t.Eq(xpBefore + expectedXp, profile.PlayerXp, "[deep XP] 심화 런도 웹과 동일한 runXp 공식으로 XP 획득");
            t.True(profile.PlayerXp > xpBefore, "[deep XP] deep 런 종료 후 PlayerXp가 실제로 증가");
        }

        // ── 자동플레이 스모크(deep 런, 웹 _harness.mjs 동형 불변식 — NaN/음수 없음) ──────────────
        private static readonly HashSet<string> KnownEventTypesDeep = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED",
            "DEVICE_OFFER", "PERK_LEVELED", "STAGE_STARTED", "BOSS_PHASE2",
        };

        private static void DeepRunAutoplaySmoke(TestCtx t)
        {
            for (long seed = 5001; seed < 5006; seed++)
            {
                var rc = new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat(), asc: 0, deep: true);
                t.True(rc.State.DeepMode, $"[deep-autoplay seed={seed}] DeepMode 반영");
                t.Eq(0, rc.State.Asc, $"[deep-autoplay seed={seed}] asc=0 강제");
                bool reachedGameOver = AutoPlayDeep(rc, 20_000, seed);
                t.True(reachedGameOver, $"[deep-autoplay seed={seed}] 예외 없이 게임오버까지 진행(stage={rc.State.Stage})");

                t.True(rc.State.Score >= 0, $"[deep-autoplay seed={seed}] Score 음수 아님");
                t.True(rc.State.Coins >= 0, $"[deep-autoplay seed={seed}] Coins 음수 아님");
                t.True(!double.IsNaN(rc.State.Score), $"[deep-autoplay seed={seed}] Score NaN 아님");
                t.True(!double.IsNaN(rc.State.StageExp), $"[deep-autoplay seed={seed}] StageExp NaN 아님");
                foreach (var kv in rc.State.Pouch)
                    t.True(kv.Value >= 0, $"[deep-autoplay seed={seed}] Pouch[{kv.Key}]={kv.Value} 음수 아님");
            }
        }

        private static bool AutoPlayDeep(RunController rc, int guardMax, long seed)
        {
            int guard = 0;
            int shopStep = 0;
            while (rc.State.Phase != RunPhase.GameOver && guard < guardMax)
            {
                guard++;
                var phase = rc.State.Phase;
                IReadOnlyList<RunEvent> events;
                switch (phase)
                {
                    case RunPhase.Spin:
                        events = rc.Do(new Spin(SpinMode.N));
                        break;
                    case RunPhase.PostSpin:
                        events = rc.Do(new Continue());
                        break;
                    case RunPhase.NodeSelect:
                        events = rc.Do(new ChooseNode(0));
                        break;
                    case RunPhase.EventAugment:
                    case RunPhase.EventRelic:
                    case RunPhase.EventAugLevel:
                        events = rc.Do(new PickOffer(0));
                        break;
                    case RunPhase.EventShop:
                        if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                        else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                        break;
                    case RunPhase.DeviceNode:
                        events = rc.Do(new TakeDevice(true));
                        break;
                    case RunPhase.RewardDone:
                        events = rc.Do(new ProceedToStage());
                        break;
                    default:
                        throw new InvalidOperationException($"[deep-autoplay seed={seed}] 처리 불가 Phase=" + phase);
                }
                foreach (var e in events)
                {
                    if (!KnownEventTypesDeep.Contains(e.type))
                        throw new InvalidOperationException($"[deep-autoplay seed={seed}] 알 수 없는 RunEvent.type=" + e.type);
                }
            }
            return rc.State.Phase == RunPhase.GameOver;
        }

        private static RunState MakeDeepRun(long seed)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            return run;
        }
    }
}
