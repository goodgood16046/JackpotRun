using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 스핀 모드 — SlotV2Service.SPIN_CMDS 값("N"/"FOCUS"/"ALLIN"/"PRAY"/"LAST", 02_service.md §2 step1/§7-B).
    // typed action API로 대체(설계 원칙 5 — 카톡 텍스트 명령 파싱 이식 금지).
    public enum SpinMode
    {
        N,
        Focus,
        Allin,
        Pray,
        Last,
    }

    // 릴 1칸 — Kotlin data class Cell(sym, tag)(SlotV2Engine.kt L2056). tag는 표시 보조(와일드치환/복사/
    // 성장/제거 등) 정보일 뿐 수치 로직에는 영향 없다.
    public sealed class Cell
    {
        public readonly SymInfo sym;
        public readonly string tag;

        public Cell(SymInfo sym, string tag = "")
        {
            this.sym = sym;
            this.tag = tag;
        }
    }

    // 스핀 1회 평가 결과 — Kotlin data class SpinResult(SlotV2Engine.kt L2057-2074) 전사.
    // counts/bestSetId/jackpotSym은 Kotlin과 동일하게 심볼id(string) 기반으로 유지한다(Sym enum으로
    // 바꾸면 "empty" 센티널 셀이 자리표시용 Sym 값과 우연히 충돌할 위험이 있어 — 아래 Evaluate 주석 참조).
    public sealed class SpinResult
    {
        public List<Cell> cells;
        // [표시 전용 — 밸런스 무관] Evaluate 입력 스냅샷 — Evaluate "내부" 변형은 폭탄(💥)·자석(🧲)
        // 2종뿐이라 이 스냅샷과 cells가 갈라지는 것도 그 2종뿐이다. 👑/🧽/🌀/🌱→는 Evaluate 호출 이전
        // (RollRaw/ApplyCellOps)에 이미 raw에 반영돼 스냅샷에도 그대로 들어있다(Opus S16 검수 중요-1).
        // UI(ReelView)가 폭탄 폭발 등 "원래 심볼 → 변형" 연출에 쓴다. 로직·RNG·점수는 여전히
        // cells만 참조 — 이 필드를 읽고 쓰는 코드는 표시 계층에 한정된다(ENGINE_PORT_DESIGN.md S16 §B).
        public List<Cell> rawCells;
        public long exp;
        public long score;
        public int coins;
        public Dictionary<string, int> counts;
        public Dictionary<string, int> tagCounts;
        public string bestSetId;   // null = 세트 없음
        public int bestSetCount;
        public int skulls;
        public bool flameNext;
        public bool seedNext;
        public string jackpotSym;  // null = 잭팟 없음
        public List<string> notes;
        public long preMul;  // 전역배수 적용 전 EXP(계산모드 표시용)
        public double mul;   // 적용된 전역 expMul
        public int flat;     // 가산 flatExp

        // ── 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 잭팟 태그) — 웹
        // engine.js:774-865 §9.0 V3 J1/§9.2 J3 반환 신호(res.jackpotTagHit/jackpotStage/feverDelta/
        // bellCount/echoTriggered/jackpotCrownSignal/hasBellFest/hasReachMark/hasRetryReel/hasJpTicket).
        // 전부 mods.deepMode(=일반모드는 항상 이 블록 미진입)가 아니면 기본값(null/0/false) 그대로다.
        public string jackpotTagHit;   // "crown"|"seven"|"coin"|"prism"|"curse"|"bell"|null
        public string jackpotStage;    // "combo"|"reach"|"jackpot"|null
        public int feverDelta;         // §9.1 J2 피버 게이지 충전 신호(콤보+15/리치+25/잭팟+50, 종세트 추가분 포함)
        public int bellCount;          // 이번 스핀 bell 태그 심볼 수(종소리티켓 승격 판정용)
        public bool echoTriggered;     // 울림종 — 종 리치 시 발동(점수+200 이미 반영됨)
        public bool jackpotCrownSignal; // 잭팟왕관 — 잭팟 시 발동 신호(보상등급+1, 소비는 game 계층)
        public bool hasBellFest;       // 🎊축제종 보유(이번 스핀 셀)
        public bool hasReachMark;      // 🎯리치표식 보유
        public bool hasRetryReel;      // 🔁재도전릴 보유
        public bool hasJpTicket;       // 🎟잭팟티켓 보유

        // ── 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종 전면 이식") — 웹
        // engine.js:1026-1082 나머지 반환 신호. DeepRunHooks.ProcessDeepSpinFollowups(웹
        // _applyDeepSpinMeta 대응)가 소비한다. 전부 mods.deepMode 게이트 밖에서도 계산되지만(웹도
        // 동일 — evaluate 자체는 항상 이 필드들을 계산), 신규 special 셀은 일반모드에 절대 등장하지
        // 않으므로(weight=0) 값 자체가 항상 기본값(null/0/false)이라 자연히 무회귀다.
        public string growNext;        // "ANY"|"HIGH"|null — 🌱씨앗/🌿새싹 다음 스핀 성장 예약
        public bool alarmNext;         // ⏰알람 — 다음 스핀 EXP+10%
        public long carryExp;          // ⏳모래시계 — 이번 EXP 30% 다음 스핀 이월
        public bool gearNext;          // ⚙톱니(근사) — 다음 스핀 EXP+10%
        public bool receiptNext;       // 🧾영수증 — 다음 상점 전체 -10%
        public bool couponNext;        // 🎟쿠폰 — 다음 상점 상품 1개 할인
        public bool cartNext;          // 🛒장바구니 — 다음 상점 상품칸 +1
        public bool shieldNext;        // 🛡방패 — 다음 보스 패널티 1회 방어
        public bool exemptNext;        // 📋시험지 — 다음 보스 감점룰 1회 무시
        public bool batteryNext;       // 🔋배터리(근사) — 장치 재사용 1회 허용
        public bool kitNext;           // 🧰정비키트(근사) — 장치 재사용 1회 허용/상점칸+1 폴백
        public bool augChanceNext;     // 🖍형광펜 — 다음 증강 레벨업 확률 +15%
        public bool augLevelNext;      // 📚복습책 — 보유 증강 최저레벨 1개 즉시 레벨업
        public bool setFrag;           // 🧩세트조각(근사) — 세트 형성 시 코인+2
        public int curseGaugeUp;       // 🩸피방울/🧿저주눈 개수 — 저주게이지(불운게이지 근사) 가산량
        public bool curseEyeNext;      // 🧿저주눈 — 다음 주머니 보상 후보 +1
        public bool lucky7;            // 7️⃣ 이번 스핀 럭키7 발동(업적/UX)
        // instant(이번 스핀 소비·DeepRunHooks.ProcessDeepSpinFollowups에서 해당 심볼 덱-1):
        public bool hasBandage;
        public bool hasKnot;
        public bool hasEnergyPack;
        public bool hasFakeCrown;
        public bool hasEvoCore;
        // fuse(조건 도달 시 소비·각 훅 발동 시 소비):
        public bool hasSafePin;        // 🧷 레벨업 실패 시 누적
        public bool hasCrystal;        // 🔮 다음 보상 후보 +1(fuse: 상점 진입 훅)
        public bool hasTempWild;       // 🧲 이번 스핀 와일드 취급(fuse: 릴 추출 시 훅)
        public bool hasFateVortex;     // 🌀 스핀 2회 굴려 유리한 쪽(fuse: 스핀전 훅)
        public bool hasBlackCard;      // 💳 다음 상점 1개 무료+불운+1(fuse: 상점 진입 훅)
        public bool hasShackle;        // ⛓ 보스 관련(상주·주머니 보유 기반, ApplyDeepMods 게이팅)
    }

    // ResolveSpin() 한 번 호출(스핀 1회 전체 파이프라인) 결과. StageFlow가 이 값을 보고 클리어/실패를
    // 판정한다(02_service.md §2 step 26의 분기는 SpinResolver 책임 밖 — StageFlow.cs가 담당).
    public sealed class SpinOutcome
    {
        public bool rejected;      // true면 run 상태 변경/코인차감 전혀 없음(§2 step15 "거부" 규칙)
        public string rejectReason;

        public SpinMode mode;
        public SpinResult result;
        public long gained;        // 최종 이번 스핀 EXP(모드/보스/안전벨트/비상벨 전부 반영 후)
        public long newExp;        // run.stageExp + gained (클리어 판정 기준)
        public long newScore;
        public long newCoins;
        public int newSpinIndex;
        public long quota;
        public int spins;
        public bool destroyDevice;
        public bool badSpin;
        public bool prayMiracle;
        public int cmdCost;
        public readonly List<string> notes = new List<string>(); // res.notes + 모드/보스/안전장치 부가설명
    }

    // 스핀 1회의 정확한 순서 이식 — 02_service.md §2(핵심)·§10.1-10.4, Kotlin SlotV2Engine의
    // weighted/rollRaw/cellsFromIds/rollOne/applyCellOps/evaluate/spinsPerStage/cmdCoinCost와
    // SlotV2Service.handleSpin(L511-715, step 1-25까지 — step26의 클리어/실패 분기는 StageFlow.cs)를 전사.
    public static class SpinResolver
    {
        // ── 10.1 weighted() (Kotlin L2079-2091) ────────────────────────────────
        private static SymInfo Weighted(Rng rng, Mods mods)
        {
            var syms = Symbols.All;
            var w = new double[syms.Length];
            double total = 0.0;
            for (int i = 0; i < syms.Length; i++)
            {
                double x = syms[i].weight;
                if (syms[i].rare) x *= mods.rareWeightMul; // 희귀 = crown·wild만
                x *= mods.symbolWeightMul.TryGetValue(syms[i].sym, out var wm) ? wm : 1.0;
                x += mods.weightAdd.TryGetValue(syms[i].sym, out var wa) ? wa : 0.0;
                w[i] = x;
                total += x;
            }
            double r = rng.NextDouble() * total; // RNG 호출 1회
            for (int i = 0; i < syms.Length; i++)
            {
                r -= w[i];
                if (r <= 0) return syms[i];
            }
            return syms[0]; // 부동소수 오차 폴백
        }

        private static readonly string[] SeedGrowPool = { "book", "star", "crown" };

        // ── 10.2 rollRaw() (Kotlin L2095-2102) ─────────────────────────────────
        // 호출 순서 고정: [reel칸 weighted() 순차 호출] → (seedActive면) [성장심볼 선택 1회] → [칸 인덱스 1회].
        public static List<Cell> RollRaw(Rng rng, Mods mods, int reel, bool seedActive)
        {
            var cells = new List<Cell>(reel);
            for (int i = 0; i < reel; i++) cells.Add(new Cell(Weighted(rng, mods)));
            if (seedActive)
            {
                string grow = rng.Pick(SeedGrowPool);
                int idx = rng.Next(reel);
                cells[idx] = new Cell(Symbols.ById(grow), "🌱→");
            }
            return cells;
        }

        // 웹 파리티 P7-2 blocker(§0, WEB_PARITY_DESIGN.md §2-(AA) 선행 blocker) — "empty"/"random"
        // 주머니 전용 센티널이 LockedNext(문자열 id 리스트) → Cell 왕복에서 조용히 드롭되던 버그를
        // 고쳤다. 이전 구현은 `Symbols.ById(id)`가 null을 반환하는 모든 id(센티널 포함, 순수 오타/미지
        // id 포함)를 그냥 건너뛰어 반환 리스트가 입력보다 짧아질 수 있었다(웹 대조: 웹은 lockedNext에
        // 항상 `res.cells`/`raw`를 그대로 넣으므로 센티널이 섞여도 칸 수가 절대 안 준다) — 5칸 예언
        // 결과에 "empty"가 하나라도 섞이면 다음 스핀이 4칸으로 줄어드는 별도 버그를 유발했다.
        // rng/pouch(둘 다 선택)를 넘기면 "random"도 PouchOps.DrawOne과 동일하게 완전히 재해석해
        // 복원한다(현재 유일한 호출부 ResolveSpin은 항상 run.Rng/run.Pouch를 넘긴다 — "random"이
        // LockedNext에 literal로 남는 경로는 PouchOps.DrawOne이 draw 시점에 이미 실심볼로 해석해
        // 버리므로 사실상 없지만, 방어적으로 완전 지원한다). rng/pouch 생략(테스트 등 기존 호출부)
        // 시에는 "random"을 EmptySym으로 안전 대체한다 — 어느 경로든 **입력 id 개수 = 출력 칸 개수**를
        // 구조적으로 보장한다(미지 id도 더 이상 드롭하지 않고 EmptySym으로 대체).
        public static List<Cell> CellsFromIds(IReadOnlyList<string> ids, Rng rng = null, IReadOnlyDictionary<string, int> pouch = null)
        {
            var list = new List<Cell>();
            if (ids == null) return list;
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (id == "empty") { list.Add(new Cell(EmptySym)); continue; }
                if (id == "random")
                {
                    list.Add(rng != null && pouch != null ? PouchOps.DrawOne(id, rng, pouch) : new Cell(EmptySym, "🎲칸"));
                    continue;
                }
                var info = Symbols.ById(id);
                list.Add(new Cell(info ?? EmptySym));
            }
            return list;
        }

        public static Cell RollOne(Rng rng, Mods mods) => new Cell(Weighted(rng, mods));

        // 웹 파리티 P7-2 blocker(§0) — 심화모드 인식 굴림 단일 진입점. 웹의 단일 `this._roll(mods,
        // seedActive)`(game.js:657-691)에 대응: DeepMode면 mods의 symbolWeightMul/weightAdd/
        // rareWeightMul을 PouchBias로 변환(DeepRunHooks.BuildPouchBias)해 PouchOps.PouchDraw를,
        // 아니면 기존 RollRaw를 그대로 호출한다. PEEK(DeviceActions.HandlePeek)·MANIP 4종
        // (DeviceActions.HandleManip)·도박꾼 무료재굴림(DeviceActions.GamblerReroll)·재시험
        // (ItemUse.UseRetakeForm)·timeline_ticket(ItemUse.ApplyItemPurchase)이 전부 이 헬퍼(와 아래
        // RollCellOne)로 수렴해, 심화 런에서 이 경로들이 주머니 밖 심볼을 섞어 내지 않게 한다 — 이전에는
        // 전부 RollRaw/RollOne(일반 가중추첨)을 직접 호출해 심화 런에서도 72종 전체에서 뽑고 있었다.
        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js:657-691 `_roll`) —
        // §9.0 J1 리치 태그 bias(×1.5, 1스핀)와 §9.2 J3 재도전릴(리치 다음 스핀 1칸 재굴림)을 여기서
        // 소비한다. 웹은 이 둘을 `_roll()` 단일 함수 안에서 처리하므로(bias 조립 직후 리치bias 병합 →
        // PouchDraw → 재도전릴 후처리) 그 구조 그대로 옮긴다. RollCellOne(MANIP 등 1칸씩 굴리는
        // 호출부)에는 의도적으로 확장하지 않는다 — 그쪽은 이미 웹과 RNG 소비 위상이 다르다는 선례가
        // 있고(§2-(BB) LOW 잔여 "dev_pin RNG 소비 위상"), 리치bias/재도전릴은 "메인 스핀 1회"의 부가
        // 효과라 MANIP의 부분 재굴림까지 확장하면 스핀당 다중 소진 위험만 커진다(범위 제한, 보고 대상).
        public static List<Cell> RollCells(RunState run, Mods mods, int reel, bool seedActive)
        {
            if (!run.DeepMode) return RollRaw(run.Rng, mods, reel, seedActive);
            var bias = DeepRunHooks.BuildPouchBias(mods) ?? new PouchBias();
            DeepRunHooks.ApplyReachBias(bias, run);
            var cells = PouchOps.PouchDraw(run.Rng, run.Pouch, reel, bias);
            if (run.RetryReelPending)
            {
                run.RetryReelPending = false;
                int idx = run.Rng.Next(cells.Count);
                var replacement = PouchOps.PouchDraw(run.Rng, run.Pouch, reel, bias);
                cells[idx] = replacement[idx];
            }
            return cells;
        }

        // RollCells의 1칸 버전 — MANIP(부분/전체 재굴림)·재시험처럼 칸을 하나씩 굴리는 호출부용.
        public static Cell RollCellOne(RunState run, Mods mods) =>
            run.DeepMode
                ? PouchOps.PouchDraw(run.Rng, run.Pouch, 1, DeepRunHooks.BuildPouchBias(mods))[0]
                : RollOne(run.Rng, mods);

        // ── 셀 조작 (Kotlin applyCellOps L2114-2125) — NEXTSPIN 아이템의 "셀 치환" 레버, 평가 직전 적용.
        // eraser_old/eraser_fine은 동일 case(최저가치 1칸 제거), eraser_god은 2회 반복.
        // seal_tape/skull_sticker는 여기 관여 없음(ApplyItemMods 소관 — Kotlin 주석과 동일).
        public static void ApplyCellOps(List<Cell> cells, IReadOnlyList<string> armIds, Rng rng)
        {
            if (armIds == null) return;
            for (int i = 0; i < armIds.Count; i++)
            {
                switch (armIds[i])
                {
                    case "eraser_old":
                    case "eraser_fine":
                        RemoveLowestValue(cells);
                        break;
                    case "eraser_god":
                        RemoveLowestValue(cells);
                        RemoveLowestValue(cells);
                        break;
                    case "wild_temp":
                        cells[rng.Next(cells.Count)] = new Cell(Symbols.ById("wild"), "🌀");
                        break;
                    case "fake_crown":
                        ReplaceHighestWithCrown(cells);
                        break;
                }
            }
        }

        // Kotlin cellValue(c) = c.sym.exp + c.sym.score (L2112) — "가장 낮은/높은 칸" 판정 기준.
        private static long CellValue(Cell c) => c.sym.exp + c.sym.score;

        private static void RemoveLowestValue(List<Cell> cells)
        {
            int bestIdx = -1;
            long bestVal = long.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].sym.id == "empty") continue;
                long v = CellValue(cells[i]);
                if (v < bestVal) { bestVal = v; bestIdx = i; } // minByOrNull: 첫 최솟값 유지(엄격 <)
            }
            if (bestIdx >= 0) cells[bestIdx] = new Cell(EmptySym, "🧽");
        }

        private static void ReplaceHighestWithCrown(List<Cell> cells)
        {
            int bestIdx = -1;
            long bestVal = long.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].sym.id == "empty") continue;
                long v = CellValue(cells[i]);
                if (v > bestVal) { bestVal = v; bestIdx = i; } // maxByOrNull: 첫 최댓값 유지(엄격 >)
            }
            if (bestIdx >= 0) cells[bestIdx] = new Cell(Symbols.ById("crown"), "👑");
        }

        // Kotlin `private val EMPTY = Sym("empty", "▫", "빈칸")`(SlotV2Engine.kt L108) — Symbols.cs의
        // 목록(All)엔 포함되지 않는 원본과 동일한 별도 센티널이라 여기서 직접 구성한다(Symbols.cs
        // 수정 금지). sym(Sym enum) 필드값은 empty에 대해 의미가 없다(아래 Evaluate 주석 참조) — 임의로
        // Sym.Cherry를 채우되, 모든 실사용 코드는 .id/.special/.tags만 보고 .sym은 참조하지 않는다.
        // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19) — internal로 승격해 Content/Pouch.cs·
        // Run/DeepRunHooks.cs(주머니 "empty"/pity 처리)가 새 센티널을 중복 정의하지 않고 재사용한다.
        internal static readonly SymInfo EmptySym = new SymInfo
        {
            sym = Sym.Cherry, id = "empty", emoji = "▫", name = "빈칸",
            exp = 0, score = 0, coin = 0, weight = 0, special = Sp.NONE, rare = false, dormant = true,
            tags = Array.Empty<string>(),
        };

        // VALUE_IDS(Kotlin L131) — Symbols.ValueIds(Sym[])에서 파생한 문자열 집합. Symbols.cs를 단일
        // 소스로 유지하되, evaluate() 로직 전체는 Kotlin과 동일하게 문자열 id 비교로 판정한다(Sym enum
        // 비교로 바꾸면 EmptySym의 임의 sym 필드값이 진짜 심볼과 우연히 일치해 오판정할 위험이 있다).
        private static readonly HashSet<string> ValueIds = BuildValueIds();
        private static HashSet<string> BuildValueIds()
        {
            var set = new HashSet<string>();
            var ids = Symbols.ValueIds;
            for (int i = 0; i < ids.Length; i++) set.Add(Symbols.BySym(ids[i]).id);
            return set;
        }
        // 동점(tie) 결정론 처리 — 01_engine.md §11-7: Kotlin `counts.maxByOrNull{}`은 HashMap 버킷 순서에
        // 의존해 재현 불가능하다. 스펙 문서가 권장하는 대로 "심볼 선언 순서를 우선순위로 쓰는 명시적
        // tie-break"로 대체한다(Symbols.ValueIds 선언 순서: cherry,star,book,gem,crown — Kotlin L131
        // setOf 리터럴 순서와 동일). [스펙-Kotlin 불일치 — 의도된 결정론화, 보고 대상]
        private static readonly string[] ValueIdsPriorityOrder = Symbols.ValueIds.Select(s => Symbols.BySym(s).id).ToArray();

        private static int PerSymExpBonus(Mods mods, string id)
        {
            var info = Symbols.ById(id);
            if (info == null) return 0; // "empty" 등 실제 콘텐츠에 없는 id → 항상 0(Kotlin map miss와 동치)
            return mods.perSymbolExp.TryGetValue(info.sym, out var v) ? v : 0;
        }

        private static int PerSymScoreBonus(Mods mods, string id)
        {
            var info = Symbols.ById(id);
            if (info == null) return 0;
            return mods.perSymbolScore.TryGetValue(info.sym, out var v) ? v : 0;
        }

        // 웹 파리티 P7-2(§1-A #19 B, 웹 engine.js:671-672 `famBase`/`archMul`) — 심볼 id → base 계열
        // (상위계열이면 Pouch.UpgradeParent로 환산, 아니면 자기 자신) 기준으로 계열 아키타입 곱셈
        // 증가분을 조회한다. map이 비어있으면(일반모드 항상 이 경우) 즉시 0 — 무회귀.
        private static double ArchMul(IReadOnlyDictionary<string, double> map, string sid)
        {
            if (map == null || map.Count == 0) return 0;
            string fam = Pouch.UpgradeParent.TryGetValue(sid, out var p) ? p : sid;
            return map.TryGetValue(fam, out var v) ? v : 0;
        }

        // ── evaluate() (Kotlin L2131-2356) — 원시 셀 → 폭탄/자석/세트/잭팟/위치/해골/신규16종/전역배수.
        // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): capMul 매개변수 제거 — 웹 engine.js에는 이 함수가 갖던
        // "총배율 캡"(위치/불꽃/첫막스핀/전역배수 곱을 capBase 대비 클램프) 자체가 없다.
        public static SpinResult Evaluate(
            Rng rng, IReadOnlyList<Cell> raw, Mods mods, int spinIndex, int spinsPerStage,
            bool flamePenalty)
        {
            var notes = new List<string>();
            var cells = new List<Cell>(raw);
            int reel = Math.Max(cells.Count, 1);
            // 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종 전면 이식", 웹 engine.js:583)
            // — 누적 특수배수(럭키7×7·검은초·불안정폭탄·프리즘) 전용 변수. 일반경로는 1(무영향)로 남아
            // 아래 특수배수 캡 블록에서 사실상 no-op 처리된다.
            double specialMul = 1.0;

            // 🌱 씨앗 성장 표기(첫 매치만)
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].tag == "🌱→") { notes.Add($"🌱 씨앗→{cells[i].sym.emoji}"); break; }
            }

            // 🧹 정화도구(PURIFY) — 웹 engine.js:584-592. 해골(SKULL) 1개→빈칸, 정화도구 수만큼 앞 해골부터.
            int purifyN = 0;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.PURIFY) purifyN++;
            if (purifyN > 0)
            {
                int purified = 0;
                for (int i = 0; i < reel && purified < purifyN; i++)
                {
                    if (cells[i].sym.special == Sp.SKULL) { cells[i] = new Cell(EmptySym, "🧹"); purified++; }
                }
                if (purified > 0) notes.Add($"🧹 해골 {purified}개 정화");
            }

            // 🪞 거울(MIRROR) — 웹 engine.js:593-598. 1번칸↔마지막칸 상호복사(거울 자체는 소스 제외).
            bool hasMirror = false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.MIRROR) { hasMirror = true; break; }
            if (hasMirror && reel >= 2)
            {
                var mirA = cells[0]; var mirB = cells[reel - 1];
                bool mirAOk = mirA.sym.special != Sp.MIRROR, mirBOk = mirB.sym.special != Sp.MIRROR;
                if (mirAOk && mirBOk)
                {
                    cells[0] = new Cell(mirB.sym, "🪞");
                    cells[reel - 1] = new Cell(mirA.sym, "🪞");
                    notes.Add("🪞 양끝 미러");
                }
            }

            // 🧪 촉매(CATALYST) — 웹 engine.js:599-617. 상위계열 매핑(POUCH_UPGRADE) 가능한 최저등급
            // 심볼 1개를 상위로 강화. 매핑 대상이 없으면 값심볼 존재 시 근사(+3 EXP, catalystApproxExp로
            // per-cell 루프 이후 가산). 촉매 여러개여도 1회만.
            double catalystApproxExp = 0.0;
            {
                bool hasCatalyst = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.CATALYST) { hasCatalyst = true; break; }
                if (hasCatalyst)
                {
                    int catBi = -1;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        string cid = cells[i].sym.id;
                        if (!Pouch.Upgrade.ContainsKey(cid)) continue;
                        if (catBi < 0 || CatalystRank(cid) < CatalystRank(cells[catBi].sym.id)) catBi = i;
                    }
                    if (catBi >= 0)
                    {
                        var catFrom = cells[catBi].sym;
                        var catUp = Symbols.ById(Pouch.Upgrade[catFrom.id]);
                        if (catUp != null)
                        {
                            cells[catBi] = new Cell(catUp, "🧪");
                            notes.Add($"🧪 {catFrom.emoji}→{catUp.emoji} 강화");
                        }
                    }
                    else
                    {
                        bool anyValueSym = false;
                        for (int i = 0; i < cells.Count; i++) if (ValueIds.Contains(cells[i].sym.id)) { anyValueSym = true; break; }
                        if (anyValueSym) { catalystApproxExp = 3; notes.Add("🧪 촉매(강화 +3)"); }
                    }
                }
            }

            // 💣 폭탄 — 등장한 폭탄 개수만큼 각각 양옆 제거 → EXP 환산. 폭탄끼리는 안 지움, 중복 제거 방지.
            int bombExp = 0;
            var bombIdxs = new List<int>();
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.BOMB) bombIdxs.Add(i);
            if (bombIdxs.Count > 0)
            {
                var removedSet = new HashSet<int>();
                for (int bi = 0; bi < bombIdxs.Count; bi++)
                {
                    int b = bombIdxs[bi];
                    foreach (int j in new[] { b - 1, b + 1 })
                    {
                        if (j >= 0 && j < reel && cells[j].sym.special != Sp.BOMB && cells[j].sym.id != "empty" && !removedSet.Contains(j))
                        {
                            removedSet.Add(j);
                            cells[j] = new Cell(EmptySym, "💥");
                        }
                    }
                }
                int removed = removedSet.Count;
                bombExp = removed * Formulas.BOMB_EXP_PER;
                if (removed > 0) notes.Add($"💣{(bombIdxs.Count > 1 ? $"×{bombIdxs.Count} " : " ")}{removed}칸 제거 +{bombExp}");
            }

            // 🧲 자석 — 등장한 자석 개수만큼 각각 옆칸(왼쪽 우선→오른쪽) 실심볼 복사. 소스는 폭탄처리 후
            // 스냅샷(magSrc) 기준 — 자석↔자석 연쇄복사만 차단.
            var magIdxs = new List<int>();
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.MAGNET) magIdxs.Add(i);
            if (magIdxs.Count > 0)
            {
                var magSrc = new List<Cell>(cells);
                for (int mi2 = 0; mi2 < magIdxs.Count; mi2++)
                {
                    int mi = magIdxs[mi2];
                    Cell src = null;
                    foreach (int cand in new[] { mi - 1, mi + 1 })
                    {
                        if (cand < 0 || cand >= reel) continue;
                        var c = magSrc[cand];
                        if (c.sym.special == Sp.NONE && c.sym.id != "empty") { src = c; break; }
                    }
                    if (src != null)
                    {
                        cells[mi] = new Cell(src.sym, "🧲");
                        notes.Add($"🧲 {src.sym.emoji} 복사");
                    }
                }
            }

            // value 심볼 집계 (🌀 와일드는 최다 그룹에 합류)
            var counts = new Dictionary<string, int>();
            int wilds = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                var s = cells[i].sym;
                if (s.special == Sp.WILD) wilds++;
                else if (s.id != "empty" && ValueIds.Contains(s.id)) counts[s.id] = counts.TryGetValue(s.id, out var c) ? c + 1 : 1;
            }
            // 🪄 마법봉(WANDWILD) — 웹 engine.js:648-655. 무작위 1심볼을 와일드 취급(치환 아님·세트/양끝
            // 보조에만). 상한: 마법봉 기여 최대 1 + 실와일드 합이 reel-1 초과 못함. 잭팟 게이트는 마법봉
            // 기여 제외(아래 jackpotCount).
            int wandWilds = 0;
            {
                bool hasWandWild = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.WANDWILD) { hasWandWild = true; break; }
                if (hasWandWild)
                {
                    int cap = Math.Max(0, (reel - 1) - wilds);
                    wandWilds = Math.Min(1, cap);
                    if (wandWilds > 0) notes.Add("🪄 마법봉 와일드");
                }
            }
            int totalWilds = wilds + wandWilds;

            string bestId = null;
            int bestTmp = 0;
            for (int i = 0; i < ValueIdsPriorityOrder.Length; i++)
            {
                var sid = ValueIdsPriorityOrder[i];
                if (counts.TryGetValue(sid, out var cnt) && cnt > bestTmp) { bestTmp = cnt; bestId = sid; }
            }
            if (bestId != null && totalWilds > 0) counts[bestId] = counts[bestId] + totalWilds;
            else if (bestId == null && totalWilds > 0) { bestId = "cherry"; counts["cherry"] = totalWilds; }
            int bestCount = bestId != null && counts.TryGetValue(bestId, out var bc) ? bc : 0;

            // 기본 EXP/점수/코인 + 즉발 심볼효과 + 태그 집계
            double exp = 0.0, score = 0.0;
            // 웹 파리티 P7-2(§1-A #19 B) — 아키타입 코인 곱(조폐국)이 셀당 소수 배율을 낼 수 있어
            // (예 s.coin × 1.10) int 누산이면 매 칸 절삭 오차가 누적된다. 웹은 JS number(부동소수)라
            // 최종 1회만 절삭한다 — coinsAcc를 double로 두고, 최종 결과(SpinResult.coins, int 필드)로
            // 변환하는 아래 finalCoins 시점에만 캐스트한다.
            double coinsAcc = 0.0;
            double jackpotFixed = 0.0;
            int symCoinGain = 0;
            int keyCount = 0;
            var tagCounts = new Dictionary<string, int>();
            int skulls = 0;
            for (int idx = 0; idx < cells.Count; idx++)
            {
                var s = cells[idx].sym;
                double cellExp = s.exp + PerSymExpBonus(mods, s.id);
                if (s.tags != null)
                {
                    for (int ti = 0; ti < s.tags.Length; ti++)
                    {
                        var tag = s.tags[ti];
                        tagCounts[tag] = tagCounts.TryGetValue(tag, out var tc) ? tc + 1 : 1;
                        cellExp += mods.tagExpBonus.TryGetValue(tag, out var teb) ? teb : 0;
                    }
                }
                // 웹 파리티 P7-2(§1-A #19 A, 웹 engine.js:679-683) — 심화 태그강화(정비소 '태그 강화'
                // sv_tagbuff + 심볼퍽 tagBuff류 sa_tag_sense/sa_tag_major/sp_solo_major/sr_compass 병합,
                // Mods.deepTagMul) 곱. 셀이 가진 모든 태그의 배수를 합산해 ±50%로 클램프한 뒤 곱한다 —
                // 아키타입 곱(아래 archMul)과 별개 축, 웹과 동일하게 이쪽을 먼저 적용한다. 일반모드는
                // mods.deepTagMul이 항상 빈 dict라 무회귀.
                if (mods.deepTagMul.Count > 0 && s.tags != null)
                {
                    double tagMul = 0;
                    for (int ti2 = 0; ti2 < s.tags.Length; ti2++)
                        tagMul += mods.deepTagMul.TryGetValue(s.tags[ti2], out var tv) ? tv : 0;
                    tagMul = Math.Max(-0.5, Math.Min(0.5, tagMul));
                    if (tagMul != 0) cellExp *= (1 + tagMul);
                }
                // 웹 파리티 P7-2(§1-A #19 B, 웹 engine.js:685 `archMul(mods.deepFamilyExpMul, s.id)`) —
                // 계열 아키타입 EXP 곱(체리/도서관/화력·강령학파). 태그버프와 별개 축(clamp 무관·순수
                // 계열) — centerExpMul 이전에 곱한다(웹과 동일 순서). 일반모드는 mods.deepFamilyExpMul이
                // 항상 빈 dict라 ArchMul이 0을 반환해 무회귀.
                double aem = ArchMul(mods.deepFamilyExpMul, s.id);
                if (aem != 0) cellExp *= (1 + aem);
                if (idx == reel / 2) cellExp *= mods.centerExpMul; // 가운데 칸 강화
                exp += cellExp;
                // 웹 engine.js:690 — 계열 아키타입 점수 곱(보석상).
                double cellScore = s.score + PerSymScoreBonus(mods, s.id);
                double asm = ArchMul(mods.deepFamilyScoreMul, s.id);
                if (asm != 0) cellScore *= (1 + asm);
                score += cellScore;
                // 웹 engine.js:693 — 계열 아키타입 코인 곱(조폐국).
                double acm = ArchMul(mods.deepFamilyCoinMul, s.id);
                coinsAcc += acm != 0 ? s.coin * (1 + acm) : s.coin;
                switch (s.special)
                {
                    case Sp.DICE:
                        int d = 1 + rng.Next(12); // Kotlin nextInt(1,13) == [1,12]
                        exp += d; notes.Add($"🎲 +{d}");
                        break;
                    case Sp.SKULL:
                    {
                        skulls++;
                        double se = mods.skullExp + mods.perSkullExp;
                        // 웹 engine.js:696 — 강령학파(skull 계열) t2는 해골 EXP 가산분에도 아키타입 곱 적용.
                        double ase = ArchMul(mods.deepFamilyExpMul, s.id);
                        exp += ase != 0 ? se * (1 + ase) : se;
                        score += mods.skullScoreBonus;
                        break;
                    }
                    case Sp.COIN:
                        symCoinGain += (int)s.coin;
                        break;
                    case Sp.KEY:
                        keyCount++;
                        break;
                }
            }
            exp += bombExp;
            if (catalystApproxExp != 0) exp += catalystApproxExp; // 🧪 촉매 근사(+3) — 매핑 대상 없을 때만

            // ── Phase 5 심화: 빈칸활용/빈칸설명서(심화 심볼증강·유물) — 웹 engine.js:701-714 ──
            if (mods.deepEmptyScore != 0 || mods.deepEmptyExp != 0)
            {
                int emptyN = 0;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.id == "empty") emptyN++;
                if (emptyN > 0)
                {
                    if (mods.deepEmptyExp != 0) exp += emptyN * mods.deepEmptyExp;
                    if (mods.deepEmptyScore != 0) score += emptyN * mods.deepEmptyScore;
                    var emptyParts = new List<string>();
                    if (mods.deepEmptyExp != 0) emptyParts.Add($"+{(int)(emptyN * mods.deepEmptyExp)}EXP");
                    if (mods.deepEmptyScore != 0) emptyParts.Add($"+{(int)(emptyN * mods.deepEmptyScore)}점");
                    notes.Add($"▫ 빈칸 {emptyN}개 활용 {string.Join("·", emptyParts)}");
                }
            }

            // 🎯 표적(TARGET) — 웹 engine.js:716-730. 근사: 최고 cellExp 값심볼 칸 1개 효과 +50%(1회만·
            // 중복곱 방지). per-cell 루프의 cellExp 공식(아키타입 곱 제외 — 웹과 동일)을 재현해 최고 칸을
            // 찾고, 그 50%를 exp에 가산.
            {
                bool hasTarget = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.TARGET) { hasTarget = true; break; }
                if (hasTarget)
                {
                    double best = 0;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (!ValueIds.Contains(cells[i].sym.id)) continue;
                        double v = TargetCellExp(cells[i], i, reel, mods);
                        if (v > best) best = v;
                    }
                    if (best > 0)
                    {
                        double bonus = best * 0.5;
                        exp += bonus;
                        notes.Add($"🎯 표적 최고칸 +50% (+{(int)Math.Floor(bonus)})");
                    }
                }
            }

            if (keyCount > 0)
            {
                int keyCoins = keyCount * Formulas.KEY_COIN_PER;
                coinsAcc += keyCoins;
                notes.Add($"🗝 보물 +{keyCoins}🪙");
            }
            if (symCoinGain > 0) notes.Add($"🪙 +{symCoinGain}🪙");
            bool anySeed = false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.SEED) { anySeed = true; break; }
            if (anySeed) notes.Add("🌱 다음 성장↑");

            // 세트 보너스
            if (bestId != null && bestCount >= 2)
            {
                int n = Math.Min(bestCount, Symbols.SetExp.Length - 1);
                double twoMul = bestCount == 2 ? mods.twoSetBonusMul : 1.0;
                double add = Symbols.SetExp[n] * mods.setExpMul * twoMul;
                exp += add; score += Symbols.SetScore[n];
                notes.Add($"{Symbols.ById(bestId).emoji}×{bestCount} 세트 +{(int)add}");
                if (twoMul != 1.0) notes.Add($"👯짝맞춤 +{(int)((twoMul - 1.0) * 100)}%");
            }

            // 🧩 퍼즐(PUZZLE5) — 웹 engine.js:751-758. 서로 다른 값심볼 종류 수 → 점수 보너스(1회만).
            // reel=5·값심볼 5종(왕관 포함)이라 "정확히 5종"은 극희소 → 완화 2단(4종+150/5종+300).
            {
                bool hasPuzzle = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.PUZZLE5) { hasPuzzle = true; break; }
                if (hasPuzzle)
                {
                    var kinds = new HashSet<string>();
                    for (int i = 0; i < cells.Count; i++) if (ValueIds.Contains(cells[i].sym.id)) kinds.Add(cells[i].sym.id);
                    int puzzleBonus = kinds.Count >= 5 ? 300 : kinds.Count >= 4 ? 150 : 0;
                    if (puzzleBonus > 0) { score += puzzleBonus; notes.Add($"🧩 퍼즐 {kinds.Count}종 +{puzzleBonus}점"); }
                }
            }

            // 🎰 잭팟 — 전 칸 동일(와일드 포함) 심볼. ★마법봉 와일드 기여는 잭팟 게이트에서 제외(인위적
            // 잭팟 남발 차단, 웹 engine.js:759-761 jackpotCount). 세트/표기엔 bestCount 유지.
            string jackpotSym = null;
            int jackpotCount = bestCount - wandWilds;
            if (bestId != null && jackpotCount >= reel && reel >= 5)
            {
                jackpotSym = bestId;
                int jb = bestId switch
                {
                    "cherry" => 120, "book" => 320, "star" => 360, "gem" => 160, "crown" => 520, _ => 200,
                };
                jackpotFixed += jb; score += jb * 5;
                notes.Add($"🎰{Symbols.ById(bestId).emoji}×{bestCount} 잭팟! +{jb}EXP·+{jb * 5}점");
            }

            // ── §9.0 V3 J1: 잭팟 태그 판정(심화모드 게이팅) — 웹 engine.js:768-865 그대로 ──────────
            // ★일반모드 완전격리: mods.deepMode는 DeepRunHooks.ApplyDeepMods가 심화 런에서만 세운다.
            // 5칸의 jackpotTag 카운트(와일드·빈칸 미기여) → 최다 태그 3단계(콤보/리치/태그잭팟) 판정.
            // 동일 심볼 잭팟(jackpotSym)과 공존 가능하나 중복 지급 금지(EXP/점수는 jackpotSym이 이미
            // 지급했으면 태그잭팟 쪽 스킵, 배너 신호만 반환).
            string jackpotTagHit = null;
            string jackpotStage = null;
            int feverDelta = 0;
            int bellCount = 0;
            bool echoTriggered = false;
            bool jackpotCrownSignal = false;
            bool hasBellFest = false, hasReachMark = false, hasRetryReel = false, hasJpTicket = false;
            if (mods.deepMode)
            {
                var jtagCount = new Dictionary<string, int>();
                for (int i = 0; i < cells.Count; i++)
                {
                    var s = cells[i].sym;
                    if (s.special == Sp.WILD || s.id == "empty") continue;
                    var jt = Pouch.JackpotTagOf(s.id);
                    if (jt != null) jtagCount[jt] = jtagCount.TryGetValue(jt, out var jc) ? jc + 1 : 1;
                }
                // § 9.2 J3: 슬롯조각(최다 태그 +1, 프리즘 포함)·잭팟마법봉(최다 태그 +1, prism 제외)
                bool hasSlotShard = false, hasJpWand = false;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].sym.special == Sp.SLOT_SHARD) hasSlotShard = true;
                    else if (cells[i].sym.special == Sp.JACKPOT_WAND) hasJpWand = true;
                }
                if (hasSlotShard || hasJpWand)
                {
                    string curBest = null; int curBestN = 0;
                    foreach (var kv in jtagCount) if (kv.Value > curBestN) { curBestN = kv.Value; curBest = kv.Key; }
                    if (hasSlotShard && curBest != null)
                    {
                        jtagCount[curBest] = (jtagCount.TryGetValue(curBest, out var c1) ? c1 : 0) + 1;
                        notes.Add($"🎰 슬롯조각 — {curBest} 태그 +1");
                    }
                    if (hasJpWand)
                    {
                        string wpBest = null; int wpBestN = 0;
                        foreach (var kv in jtagCount)
                        {
                            if (kv.Key == "prism") continue;
                            if (kv.Value > wpBestN) { wpBestN = kv.Value; wpBest = kv.Key; }
                        }
                        if (wpBest != null)
                        {
                            jtagCount[wpBest] = (jtagCount.TryGetValue(wpBest, out var c2) ? c2 : 0) + 1;
                            notes.Add($"🪄 잭팟마법봉 — {wpBest} 태그 +1 (prism 제외)");
                        }
                    }
                }
                // 최다 태그 선택(3개 미만이면 미발동)
                string bestJtag = null; int bestJcount = 0;
                foreach (var kv in jtagCount) if (kv.Value > bestJcount) { bestJcount = kv.Value; bestJtag = kv.Key; }
                if (bestJtag != null && bestJcount >= 3)
                {
                    jackpotTagHit = bestJtag;
                    jackpotStage = bestJcount >= 5 ? "jackpot" : bestJcount >= 4 ? "reach" : "combo";
                }
                bellCount = jtagCount.TryGetValue("bell", out var bc2) ? bc2 : 0;

                // § 9.2 J3: 환호(콤보/리치/잭팟 보너스 +25%)·대폭죽(콤보+500/잭팟+2000)·잭팟왕관(등급+1 신호)
                bool hasCheer = false, hasBigBoom = false, hasJpCrown = false;
                for (int i = 0; i < cells.Count; i++)
                {
                    var sp = cells[i].sym.special;
                    if (sp == Sp.CHEER) hasCheer = true;
                    else if (sp == Sp.BIG_BOOM) hasBigBoom = true;
                    else if (sp == Sp.JACKPOT_CROWN) hasJpCrown = true;
                    else if (sp == Sp.BELL_FEST) hasBellFest = true;
                    else if (sp == Sp.REACH_MARK) hasReachMark = true;
                    else if (sp == Sp.RETRY_REEL) hasRetryReel = true;
                    else if (sp == Sp.JACKPOT_TICKET) hasJpTicket = true;
                }
                double cheerMul = (hasCheer && jackpotStage != null) ? 1.25 : 1.0;

                string tLabel = jackpotTagHit != null ? JackpotTagLabel(jackpotTagHit) : "";
                if (jackpotStage == "combo")
                {
                    const int baseExpBonus = 8;
                    long expBonus = (long)Math.Floor(baseExpBonus * cheerMul);
                    exp += expBonus;
                    feverDelta = 15;
                    long boomBonus = hasBigBoom ? (long)Math.Floor(500 * cheerMul) : 0;
                    if (boomBonus > 0) { score += boomBonus; notes.Add($"💥 대폭죽 콤보 점수+{boomBonus}"); }
                    notes.Add($"🎯 {tLabel} 콤보! (태그 {bestJcount}개) EXP+{expBonus}{(hasCheer ? " 🎉환호×1.25" : "")}");
                }
                else if (jackpotStage == "reach")
                {
                    long reachScore = (long)Math.Floor(300 * cheerMul);
                    if (jackpotSym == null) score += reachScore; // 동일심볼잭팟 공존 시 점수 중복 지급 금지
                    feverDelta = 25;
                    if (jackpotTagHit == "bell")
                    {
                        for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.BELL_ECHO) { echoTriggered = true; break; }
                        if (echoTriggered) { score += 200; notes.Add("🔔 울림종 — 종 리치! 점수+200"); }
                    }
                    notes.Add($"🎯 {tLabel} 리치! (태그 {bestJcount}개){(jackpotSym == null ? $" 점수+{reachScore}" : "")}{(hasCheer ? " 🎉×1.25" : "")} — 1개만 더!");
                }
                else if (jackpotStage == "jackpot")
                {
                    long jExp = (long)Math.Floor(30 * cheerMul);
                    long jScore = (long)Math.Floor(1500 * cheerMul);
                    if (jackpotSym == null) { exp += jExp; score += jScore; } // 동일심볼잭팟 공존 시 중복 지급 금지
                    feverDelta = 50;
                    long boomBonus2 = hasBigBoom ? (long)Math.Floor(2000 * cheerMul) : 0;
                    if (boomBonus2 > 0) { score += boomBonus2; notes.Add($"💥 대폭죽 잭팟 점수+{boomBonus2}"); }
                    if (hasJpCrown) { jackpotCrownSignal = true; notes.Add("👑 잭팟왕관 — 보상등급+1 (스테이지 1회)"); }
                    notes.Add($"🎰 {tLabel} 잭팟!! (태그 {bestJcount}개){(jackpotSym == null ? $" EXP+{jExp}·점수+{jScore}" : "")}{(hasCheer ? " 🎉×1.25" : "")}");
                }

                // § 9.2 J3: 종 세트 피버 추가 충전(작은종/황금종, 태그 3개+)
                if (bellCount >= 3)
                {
                    bool hasSmallBell = false, hasGoldenBell = false;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        var sp = cells[i].sym.special;
                        if (sp == Sp.BELL_SMALL) hasSmallBell = true;
                        else if (sp == Sp.BELL_GOLD) hasGoldenBell = true;
                    }
                    if (hasSmallBell) { feverDelta += 15; notes.Add($"🔔 작은종 — 종 {bellCount}개 피버+15"); }
                    if (hasGoldenBell) { feverDelta += 30; notes.Add($"🔔 황금종 — 종 {bellCount}개 피버+30"); }
                }
            }

            // 인접 판정
            if (mods.adjacentSameExp != 0)
            {
                int pairs = 0;
                for (int i = 0; i < reel - 1; i++)
                {
                    var a = cells[i].sym; var b = cells[i + 1].sym;
                    if (a.id == b.id && ValueIds.Contains(a.id)) pairs++;
                }
                if (pairs > 0)
                {
                    exp += pairs * mods.adjacentSameExp;
                    notes.Add($"🔗 인접 {pairs}쌍 +{pairs * mods.adjacentSameExp}");
                }
            }

            // 위치 판정 — 양끝 동일
            if (mods.endsMatchExpMul != 1.0 && reel >= 2)
            {
                var a = cells[0].sym; var b = cells[reel - 1].sym;
                if (a.id == b.id && ValueIds.Contains(a.id))
                {
                    exp *= mods.endsMatchExpMul;
                    notes.Add($"↔ 양끝 {a.emoji} EXP ×{mods.endsMatchExpMul}");
                }
            }

            // ☠ 해골 페널티/보너스
            if (skulls > 0)
            {
                double skullBonusPer = mods.skullExp + mods.perSkullExp;
                if (skullBonusPer > 0)
                {
                    notes.Add($"☠ {skulls}개 +{(int)(skullBonusPer * skulls)} (해골빌드)");
                }
                else
                {
                    double pen = skulls * Formulas.SKULL_PENALTY * mods.skullPenaltyMul;
                    exp -= pen;
                    if (pen > 0) notes.Add($"☠ {skulls}개 -{(int)pen}");
                }
            }

            // 🩸 피방울(CURSE_BLOOD) — 웹 engine.js:888-890. 셀 exp 8(데이터)+추가 +2/개 = 개당 +10.
            {
                int bloodN = 0;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.CURSE_BLOOD) bloodN++;
                if (bloodN > 0)
                {
                    int bloodAdd = 2 * bloodN;
                    exp += bloodAdd;
                    notes.Add($"🩸 피방울 {bloodN}개 +{8 * bloodN + bloodAdd}");
                }
            }

            // 🕯 검은초(CURSE_CANDLE) — 웹 engine.js:891-895. 해골 수만큼 배율(개당+25%·상한×2.5).
            // 해골 0이면 이번 스핀 EXP 0(저주 리스크).
            {
                bool hasCandle = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.CURSE_CANDLE) { hasCandle = true; break; }
                if (hasCandle)
                {
                    if (skulls > 0)
                    {
                        double cm = Math.Min(1 + 0.25 * skulls, 2.5);
                        specialMul *= cm;
                        notes.Add($"🕯 검은초 ☠{skulls} ×{FmtMul(cm)}");
                    }
                    else
                    {
                        // Opus 2차검수(P7-3b) [MED-6, Fable 결정] — 웹은 잭팟 EXP(jb)를 evaluate 초반에
                        // `exp`에 곧장 합쳐 넣으므로(engine.js:765) 이 exp=0 리셋이 잭팟 가산분까지
                        // 함께 지운다. Unity는 P2 결정(§2-B, 잭팟 고정가산은 전역 expMul 밖)에 따라
                        // jackpotFixed를 별도 누산기로 분리 유지하지만, "심화 전용" 신규 효과(검은초/
                        // 불안정폭탄의 exp=0)만큼은 웹 순서를 그대로 반영해 jackpotFixed도 함께
                        // 지운다(일반 런은 이 심볼 자체가 등장 불가라 무접촉).
                        exp = 0;
                        jackpotFixed = 0;
                        notes.Add("🕯 검은초 — 해골없음 EXP 0");
                    }
                }
            }

            // 🔥 불꽃
            bool anyFlame = false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.FLAME) { anyFlame = true; break; }
            if (anyFlame) { exp *= 1.5; notes.Add("🔥 EXP +50%"); }
            if (flamePenalty) { exp *= 0.5; notes.Add("🔥 여파 EXP -50%"); }

            // 첫/막 스핀 배수
            if (spinIndex == 0) exp *= mods.firstSpinExpMul;
            if (spinIndex == spinsPerStage - 1) exp *= mods.lastSpinExpMul;

            // 신규 16종 per-spin 조건부 배수 (전역배수 이전)
            int rareN = 0;
            for (int i = 0; i < cells.Count; i++) if (cells[i].sym.rare) rareN++;
            if (rareN >= 2 && mods.rareBurstExpMul != 1.0)
            {
                exp *= mods.rareBurstExpMul;
                notes.Add($"💫운명폭발 EXP ×{FmtMul(mods.rareBurstExpMul)}");
            }
            if (bestCount >= 3 && mods.set3ExpMul != 1.0)
            {
                exp *= mods.set3ExpMul;
                notes.Add($"🧩퍼즐 세트{bestCount} EXP ×{FmtMul(mods.set3ExpMul)}");
            }
            if (mods.perfectShapeExpMul != 1.0 && reel >= 3)
            {
                var a = cells[0].sym; var b = cells[reel - 1].sym; var c = cells[reel / 2].sym;
                bool endsWild = a.special == Sp.WILD || b.special == Sp.WILD;
                bool endsSame = (a.id == b.id && ValueIds.Contains(a.id)) ||
                                (endsWild && (ValueIds.Contains(a.id) || ValueIds.Contains(b.id)));
                string endId = ValueIds.Contains(a.id) ? a.id : (ValueIds.Contains(b.id) ? b.id : null);
                bool centerOk = endId != null && (c.id == endId || c.special == Sp.WILD);
                if (endsSame && centerOk)
                {
                    bool withWild = endsWild || c.special == Sp.WILD;
                    double pm = withWild ? 1.7 : mods.perfectShapeExpMul;
                    exp *= pm;
                    notes.Add($"💠완벽한모양 EXP ×{FmtMul(pm)}");
                }
            }

            // 🔥 phoenix_thesis(불사조논문) — 웹 파리티 P3-4(engine.js:930-933), perfectShapeExpMul 직후·
            // 전역 배수 이전(specialMul 위치, 웹과 동일 순서).
            if (mods.cliffBurstExpMul != 1.0)
            {
                exp *= mods.cliffBurstExpMul;
                notes.Add($"🔥불사조 EXP ×{FmtMul(mods.cliffBurstExpMul)}");
            }

            // ══════════════════════════════════════════════════════════════════════
            //  Phase 4 — 배수형/전설 특수심볼(전역배 직전, specialMul 누적 → 캡) — 웹 engine.js:936-1012.
            // ══════════════════════════════════════════════════════════════════════
            bool lucky7 = false;
            // 🧨 불안정폭탄(CURSE_BOOM) — 개당 50% 대폭발(×2)·50% 불발(EXP 0). 여러개면 독립 판정.
            {
                int boomN = 0;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.CURSE_BOOM) boomN++;
                for (int k = 0; k < boomN; k++)
                {
                    if (rng.NextDouble() < 0.5) { specialMul *= 2.0; notes.Add("🧨 대폭발 ×2"); }
                    else
                    {
                        // Opus 2차검수(P7-3b) [MED-6, Fable 결정] — CURSE_CANDLE 분기와 동일 근거로
                        // jackpotFixed도 함께 리셋(웹 순서 그대로, 일반 런 무접촉).
                        exp = 0;
                        jackpotFixed = 0;
                        notes.Add("🧨 불발 — EXP 0");
                        break;
                    }
                }
            }

            // ── V3P4: instant 소모형/일회용 효과(deepMode 전용) — 웹 engine.js:945-991. 아래 결과는
            // DeepRunHooks.ProcessDeepSpinFollowups가 소비 후 해당 심볼 -1 처리(instant 제거). ──
            bool hasBandage = false, hasKnot = false, hasEnergyPack = false, hasFakeCrown = false, hasEvoCore = false;
            for (int i = 0; i < cells.Count; i++)
            {
                var sp2 = cells[i].sym.special;
                if (sp2 == Sp.BANDAGE) hasBandage = true;
                else if (sp2 == Sp.KNOT) hasKnot = true;
                else if (sp2 == Sp.ENERGYPACK) hasEnergyPack = true;
                else if (sp2 == Sp.FAKECROWN) hasFakeCrown = true;
                else if (sp2 == Sp.EVOCORE) hasEvoCore = true;
            }
            //  🩹붕대(BANDAGE): 이번 스핀 해골 패널티 1개분 감소(위에서 이미 음수 반영된 패널티 상쇄).
            if (hasBandage && skulls > 0)
            {
                exp += Formulas.SKULL_PENALTY;
                notes.Add("🩹 붕대 — 해골 패널티 1개분 감소");
            }
            //  🪢매듭(KNOT): 첫 칸·마지막 칸이 같은 심볼(빈칸 제외)이면 EXP+20.
            if (hasKnot && reel >= 2 && cells[0].sym.id != "empty" && cells[0].sym.id == cells[reel - 1].sym.id)
            {
                exp += 20;
                notes.Add($"🪢 매듭 — 양끝 동일({cells[0].sym.emoji}) EXP +20");
            }
            //  🧃에너지팩(ENERGYPACK): 이번 스핀 EXP +30%(specialMul에 가산).
            if (hasEnergyPack) { specialMul *= 1.30; notes.Add("🧃 에너지팩 — 이번 스핀 EXP +30%"); }
            //  👑가짜왕관(FAKECROWN): 왕관과 동등한 EXP/점수 직접 부여(업적 추적 제외는 game 계층 몫).
            if (hasFakeCrown)
            {
                var crownSym = Symbols.ById("crown");
                if (crownSym != null) { exp += crownSym.exp; score += crownSym.score; }
                notes.Add("👑 가짜왕관 — 왕관 취급 (업적 제외)");
            }
            //  🧬진화핵(EVOCORE): 기본 이득 심볼(IsAutoDecayTarget) 1개를 SILVER 특수 랜덤으로 교체
            //  (셀 내 변환. re-evaluate 없음 — 변환 후 기존 cells로 최종 집계).
            if (hasEvoCore)
            {
                var evoBaseIdxs = new List<int>();
                for (int i = 0; i < cells.Count; i++) if (Pouch.IsAutoDecayTarget(cells[i].sym.id)) evoBaseIdxs.Add(i);
                if (evoBaseIdxs.Count > 0)
                {
                    int evoBi = evoBaseIdxs[rng.Next(evoBaseIdxs.Count)];
                    var silverPool = new List<string>();
                    for (int i = 0; i < Pouch.Symbols71.Length; i++)
                    {
                        var pid = Pouch.Symbols71[i];
                        if (Pouch.CatOf(pid) == "special" && Pouch.TierOf(pid) == "SILVER" && Symbols.ById(pid) != null)
                            silverPool.Add(pid);
                    }
                    if (silverPool.Count > 0)
                    {
                        string newId = silverPool[rng.Next(silverPool.Count)];
                        var newSym = Symbols.ById(newId);
                        var evoFrom = cells[evoBi].sym;
                        cells[evoBi] = new Cell(newSym, "🧬");
                        notes.Add($"🧬 진화핵 — {evoFrom.emoji}{evoFrom.name} → {newSym.emoji}{newSym.name} 변환");
                    }
                }
            }

            //  7️⃣ 럭키7(LUCKY7): 3개+ → EXP/점수/코인 7배. ★배수 상한(specialMul 캡)으로 폭주 제어.
            {
                int luckyN = 0;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.id == "lucky7") luckyN++;
                if (luckyN >= 3)
                {
                    lucky7 = true;
                    specialMul *= 7;
                    score *= 7;
                    coinsAcc *= 7;
                    notes.Add($"7️⃣ 럭키7 ×{luckyN} — 7배!{(mods.legendStable ? " 🔏안정" : "")}");
                }
            }
            //  🌈 프리즘(PRISM_SYM): 무작위 프리즘급 미니효과 1택(폭 좁게). 여러개면 각각 1택.
            //  🔏전설봉인기(mods.legendStable): 랜덤 대신 최선 효과(EXP×1.5)로 안정 발동.
            {
                int prismN = 0;
                for (int i = 0; i < cells.Count; i++) if (cells[i].sym.special == Sp.PRISM_SYM) prismN++;
                for (int k = 0; k < prismN; k++)
                {
                    int pick = mods.legendStable ? 3 : rng.Next(4);
                    switch (pick)
                    {
                        case 0: exp += 40; notes.Add("🌈 프리즘 — EXP +40"); break;
                        case 1: score += 120; notes.Add("🌈 프리즘 — 점수 +120"); break;
                        case 2: coinsAcc += 3; notes.Add("🌈 프리즘 — 코인 +3"); break;
                        default:
                            specialMul *= 1.5;
                            notes.Add($"🌈 프리즘 — EXP ×1.5{(mods.legendStable ? " 🔏안정" : "")}");
                            break;
                    }
                }
            }
            //  ★특수배수 누적 캡(럭키7×7·검은초·불안정폭탄·프리즘 곱 폭주 차단). 일반경로는 specialMul=1(무영향).
            // Opus 2차검수(P7-3b) [MED-6, Fable 결정] — 웹은 jb를 evaluate 초반에 `exp`에 곧장 합쳐
            // 넣으므로(engine.js:765) 이 캡도 잭팟 가산분까지 함께 곱한다. jackpotFixed는 심화 전용
            // 신규 경로(specialMul)에만 이렇게 합류하고, 아래 전역 expMul에는 여전히 배제된다(P2 결정
            // 유지 — 잔여 이탈, §2-(DD) 참조). 일반 런은 specialMul이 항상 1이라 무접촉.
            if (specialMul != 1.0)
            {
                specialMul = Math.Min(specialMul, Formulas.MAX_SPIN_EXP_MUL);
                exp *= specialMul;
                jackpotFixed *= specialMul;
            }

            // 전역 배수 + 고정(잭팟은 아직 미포함)
            long preMulExp = Math.Max((long)exp, 0);
            exp = exp * mods.expMul + mods.flatExp;

            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): 총배율 캡 제거 — 웹 engine.js에는 이 자리에서
            // center/ends/flame/first·last/rareBurst/set3/perfectShape/global 곱을 클램프하는 로직이
            // 없다(grep 결과: 웹 MAX_SPIN_EXP_MUL은 specialMul 캡 전용, Formulas.cs 주석 참조). 잭팟
            // 고정가산은 원래도 캡 예외(곱 밖)였으므로 위치는 그대로 유지 — 전역 expMul만 배제(위
            // specialMul/exp=0 리셋은 이제 함께 반영, P7-3b [MED-6] 잔여 이탈로 명시).
            exp += jackpotFixed;

            // 신규 16종 per-spin 점수 배수
            if (rareN >= 2 && mods.rareBurstScoreMul != 1.0) score *= mods.rareBurstScoreMul;
            if (bestCount >= 4 && mods.set4ScoreMul != 1.0) score *= mods.set4ScoreMul;
            if (skulls >= 3 && mods.skull3ScoreMul != 1.0)
            {
                score *= mods.skull3ScoreMul;
                notes.Add($"👁️해골관찰 ☠{skulls} 점수 ×{FmtMul(mods.skull3ScoreMul)}");
            }
            score = score * mods.scoreMul + mods.flatScore;
            int coins = (int)(coinsAcc * mods.coinMul) + Formulas.COIN_BASE;
            long finalExp = Math.Max((long)exp, 0);

            // ── 웹 파리티 P7-3b — 다음스핀/상점/보스/fuse 신호 일괄 집계(웹 engine.js:1026-1082). 전부
            // 최종 cells(폭탄/자석/거울/촉매/진화핵 변형 반영 후) 기준 — 웹과 동일 소스에서 읽는다. ──
            string growNext = null;
            bool alarmNext = false, hourglassPresent = false, gearNext = false;
            bool receiptNext = false, couponNext = false, cartNext = false;
            bool shieldNext = false, exemptNext = false, batteryNext = false, kitNext = false;
            bool augChanceNext = false, augLevelNext = false, setFrag = false, curseEyeNext = false;
            bool hasSafePin = false, hasCrystal = false, hasTempWild = false, hasFateVortex = false;
            bool hasBlackCard = false, hasShackle = false;
            int curseGaugeUp = 0;
            bool seedAnyPresent = false, seedHighPresent = false;
            for (int i = 0; i < cells.Count; i++)
            {
                var sp3 = cells[i].sym.special;
                switch (sp3)
                {
                    case Sp.SEED_ANY: seedAnyPresent = true; break;
                    case Sp.SEED_HIGH: seedHighPresent = true; break;
                    case Sp.ALARM: alarmNext = true; break;
                    case Sp.HOURGLASS: hourglassPresent = true; break;
                    case Sp.GEAR: gearNext = true; break;
                    case Sp.RECEIPT: receiptNext = true; break;
                    case Sp.COUPON: couponNext = true; break;
                    case Sp.CART: cartNext = true; break;
                    case Sp.SHIELD: shieldNext = true; break;
                    case Sp.EXEMPT: exemptNext = true; break;
                    case Sp.DEVCD: batteryNext = true; break;
                    case Sp.KIT: kitNext = true; break;
                    case Sp.AUGCHANCE: augChanceNext = true; break;
                    case Sp.AUGLEVEL: augLevelNext = true; break;
                    case Sp.SETFRAG: setFrag = true; break;
                    case Sp.CURSE_EYE: curseEyeNext = true; curseGaugeUp++; break;
                    case Sp.CURSE_BLOOD: curseGaugeUp++; break;
                    case Sp.SAFEPIN: hasSafePin = true; break;
                    case Sp.CRYSTAL: hasCrystal = true; break;
                    case Sp.TEMPWILD: hasTempWild = true; break;
                    case Sp.FATEVORTEX: hasFateVortex = true; break;
                    case Sp.BLACKCARD: hasBlackCard = true; break;
                    case Sp.SHACKLE: hasShackle = true; break;
                }
            }
            // growNext 우선순위: SEED_ANY("ANY")가 SEED_HIGH("HIGH")보다 우선 — 웹 engine.js:1037-1038 그대로.
            growNext = seedAnyPresent ? "ANY" : (seedHighPresent ? "HIGH" : null);
            long carryExp = hourglassPresent ? (long)Math.Floor(finalExp * 0.3) : 0;

            return new SpinResult
            {
                cells = cells,
                rawCells = new List<Cell>(raw), // [표시 전용] 변형 이전 입력 스냅샷 — Cell은 교체 시 새 인스턴스라 리스트 복사로 충분
                exp = finalExp,
                score = Math.Max((long)score, 0),
                coins = coins,
                counts = counts,
                tagCounts = tagCounts,
                bestSetId = bestId,
                bestSetCount = bestCount,
                skulls = skulls,
                flameNext = false,
                seedNext = anySeed,
                jackpotSym = jackpotSym,
                notes = notes,
                preMul = preMulExp,
                mul = mods.expMul,
                flat = mods.flatExp,
                jackpotTagHit = jackpotTagHit,
                jackpotStage = jackpotStage,
                feverDelta = feverDelta,
                bellCount = bellCount,
                echoTriggered = echoTriggered,
                jackpotCrownSignal = jackpotCrownSignal,
                hasBellFest = hasBellFest,
                hasReachMark = hasReachMark,
                hasRetryReel = hasRetryReel,
                hasJpTicket = hasJpTicket,
                growNext = growNext,
                alarmNext = alarmNext,
                carryExp = carryExp,
                gearNext = gearNext,
                receiptNext = receiptNext,
                couponNext = couponNext,
                cartNext = cartNext,
                shieldNext = shieldNext,
                exemptNext = exemptNext,
                batteryNext = batteryNext,
                kitNext = kitNext,
                augChanceNext = augChanceNext,
                augLevelNext = augLevelNext,
                setFrag = setFrag,
                curseGaugeUp = curseGaugeUp,
                curseEyeNext = curseEyeNext,
                lucky7 = lucky7,
                hasBandage = hasBandage,
                hasKnot = hasKnot,
                hasEnergyPack = hasEnergyPack,
                hasFakeCrown = hasFakeCrown,
                hasEvoCore = hasEvoCore,
                hasSafePin = hasSafePin,
                hasCrystal = hasCrystal,
                hasTempWild = hasTempWild,
                hasFateVortex = hasFateVortex,
                hasBlackCard = hasBlackCard,
                hasShackle = hasShackle,
            };
        }

        // 🧪촉매(CATALYST) 등급 서열 비교 헬퍼 — Pouch.RarityOrder 인덱스(작을수록 낮은 등급).
        private static int CatalystRank(string id) => Array.IndexOf(Pouch.RarityOrder, Pouch.RarityOf(id));

        // 🎯표적(TARGET) 근사 — 웹 engine.js:719-726의 inline cellExpOf를 그대로 옮긴 헬퍼. 본계산의
        // per-cell cellExp 공식과 동일(아키타입 곱(archMul)·해골 분기는 값심볼 한정이라 여기 대상 없음
        // — 웹도 이 헬퍼엔 archMul을 넣지 않는다, VALUE_IDS만 호출측에서 필터).
        private static double TargetCellExp(Cell c, int idx, int reel, Mods mods)
        {
            var s = c.sym;
            double ce = s.exp + PerSymExpBonus(mods, s.id);
            if (s.tags != null)
                for (int ti = 0; ti < s.tags.Length; ti++)
                    ce += mods.tagExpBonus.TryGetValue(s.tags[ti], out var teb) ? teb : 0;
            if (mods.deepTagMul.Count > 0 && s.tags != null)
            {
                double tagMul = 0;
                for (int ti2 = 0; ti2 < s.tags.Length; ti2++)
                    tagMul += mods.deepTagMul.TryGetValue(s.tags[ti2], out var tv) ? tv : 0;
                tagMul = Math.Max(-0.5, Math.Min(0.5, tagMul));
                if (tagMul != 0) ce *= (1 + tagMul);
            }
            if (idx == reel / 2) ce *= mods.centerExpMul;
            return ce;
        }

        // 잭팟 태그 → 표시 라벨 — 웹 engine.js:830 TAG_EMOJI 그대로. internal — DeepRunHooks의 스핀
        // 후속처리(ProcessDeepSpinFollowups)가 배너 조립에 재사용한다(중복 정의 방지).
        internal static string JackpotTagLabel(string tag) => tag switch
        {
            "crown" => "👑 왕관", "seven" => "7️⃣ 럭키7", "coin" => "🪙 코인",
            "prism" => "🌈 프리즘", "curse" => "💀 저주", "bell" => "🔔 종",
            _ => tag,
        };

        // 배율 표기 — 소수 2자리·끝0제거(Kotlin fmtMul, L2077).
        private static string FmtMul(double v)
        {
            string s = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
        // 웹 파리티 P2: FmtMul1(옛 capMul 표기 전용, "%.1f")는 총배율 캡 제거로 호출부가 사라져 삭제.

        // ── spinsPerStage / effSpins / qOf / cmdCoinCost ────────────────────────
        public static int SpinsPerStage(Mods mods) => ModsBuilder.SpinsPerStage(mods);

        public static int EffSpins(RunState run, Mods mods)
        {
            int spins = Math.Max(SpinsPerStage(mods) + run.StageBonusSpins + Bosses.Spins(run.Stage), Formulas.MIN_SPINS);
            // 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종") — ⛓족쇄(SHACKLE) 영구 저주,
            // 웹 game.js:421-422 `if (r.deepMode && mods.shackleActive && r.boss && r.spins > 1) r.spins -= 1;`.
            // 웹은 이 값을 `_beginStage()`에서 스테이지당 1회만 계산해 저장하지만, Unity는 EffSpins 자체가
            // run.StageBonusSpins 기반 순수함수라(저장된 "spins" 필드가 없음) 매 호출마다 동일 조건으로
            // 재계산해도 결과는 스테이지 내내 상수(주머니 shackle 보유량은 스핀 중 변하지 않음) — 웹의
            // "스테이지 시작 시 1회 계산·고정"과 동치.
            // Opus 2차검수(P7-3b) [MED-4] — `mods.shackleActive` 대신 `run.Pouch["shackle"]`을 직접
            // 참조하도록 정정. `mods.shackleActive`는 `DeepRunHooks.ApplyDeepMods`가 채우는데, 이
            // 함수를 거치지 않은 mods 스냅샷(ResolveSpin의 preMods/preMods0, GameSession.
            // PreviewQuotaSpins, DeviceActions/ItemUse의 재계산 mods 등)으로 EffSpins를 호출하는
            // 경로가 여럿 있어 "족쇄를 보유해도 어떤 mods를 넘겼는지에 따라 스핀수가 달라지는" 불일치가
            // 있었다. run.Pouch를 직접 보면 이 4경로 모두 한 번에 일관되게 해결된다(mods 인자 자체는
            // 시그니처 호환을 위해 유지 — 다른 계산엔 여전히 필요).
            if (run.DeepMode && Bosses.For(run.Stage) != null && spins > 1
                && run.Pouch.TryGetValue("shackle", out var shackleN) && shackleN > 0)
                spins -= 1;
            return spins;
        }

        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:423 `r.quota = Math.max(1, Math.floor(
        // E.quota(stage) * mods.quotaMul * E.bossQuotaMul(stage) * am.quotaMul * (r.boss ? am.bossQuotaMul
        // : 1) * (r._bossPhase2 ? 1.3 : 1) * this._deepPenalty())`)` — asc/bossPhase2는 기본값 0/false라
        // 기존 호출부(2-인자 형태)는 전부 무변경 동작. am.quotaMul/am.bossQuotaMul은 asc=0이면 항상 1.0
        // (AscMods.Get(0))이라 무조건 곱해도 안전 — Formulas.IsBossStage(stage)로 보스 스테이지 여부만
        // 별도 판정한다(mods.quotaMul(머신/캐릭/퍽 보정)과는 별개 축).
        // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19) — deepPenaltyMul: 웹 공식의 마지막 곱셈 인자
        // `this._deepPenalty()`(DeepRunHooks.DeepPenalty(run) 대응). 기본값 1.0이라 기존 호출부(4-인자
        // 이하 형태)는 전부 무변경 동작 — asc/bossPhase2와 동일하게 P6에서 다졌던 "트레일링 기본값
        // 추가 + 전 호출부 갱신" 패턴을 그대로 반복한다(호출측이 run.DeepMode를 보고 DeepRunHooks.
        // DeepPenalty(run)을 넘기거나, 심화 무관 호출부는 그냥 생략).
        public static long QuotaOf(int stage, Mods mods, int asc = 0, bool bossPhase2 = false, double deepPenaltyMul = 1.0)
        {
            int baseSpins = SpinsPerStage(mods);
            int bsp = Bosses.Spins(stage);
            double prop = (bsp > 0 && baseSpins > 0) ? (double)(baseSpins + bsp) / baseSpins : 1.0;
            double q = Formulas.Quota(stage) * mods.quotaMul * Bosses.QuotaMulFor(stage) * prop;
            var am = AscMods.Get(asc);
            q *= am.QuotaMul;
            if (Formulas.IsBossStage(stage)) q *= am.BossQuotaMul;
            if (bossPhase2) q *= 1.3;
            q *= deepPenaltyMul;
            return (long)q;
        }

        public static int CmdCoinCost(SpinMode mode, bool boss) => ModsBuilder.CmdCoinCost(mode, boss);

        // ── applyBoss (웹 engine.js:1088-1104 applyBossExp) — 정수 나눗셈(내림)을 그대로 유지 ──
        // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B / 항목3): grad(졸업심사)의 "pace" EXP 룰(expectedPerSpin/
        // augCount 매개변수로 빌드 빈약 여부에 따라 ×0.75/×0.85 페널티를 주던 규칙)을 제거했다 — 웹
        // applyBossExp의 switch에는 finals/strict/luck 3개 case만 있고 grad는 default로 떨어져 아무
        // 보정도 하지 않는다(quotaMul 1.15만 적용, Bosses.cs QuotaMulFor). 그 결과 expectedPerSpin/
        // augCount 매개변수 자체가 더 이상 필요 없어 시그니처에서도 제거했다(웹 함수 시그니처와 동일하게
        // boss/exp/result/spinIndex/spins만 받음 — 원문: exp, boss, spinIndex, spins, result).
        public static (long gained, string note) ApplyBoss(
            Boss boss, long gained, SpinResult res, int spinIndex, int spins)
        {
            switch (boss.id)
            {
                case "finals":
                    // Opus 검수 반영(2026-08-07) 항목4: 웹 engine.js:1091-1094는 첫스핀(spinIndex===0)
                    // 검사를 막스핀(spinIndex===spins-1)보다 먼저 한다 — 순서만 맞춰 파리티 정렬(MIN_SPINS=3
                    // 이라 spins-1==0이 되는 경우가 없어 실질 분기 결과는 이전과 동일, 도달 불가 케이스).
                    if (spinIndex == 0) return (gained * 9 / 10, " · 📝기말 첫스핀-10%");
                    if (spinIndex == spins - 1) return (gained * 2, " · 📝기말 막스핀×2");
                    return (gained, "");
                case "strict":
                    return res.bestSetCount < 3 ? (gained / 2, " · 👨‍🏫콤보없음 ×0.5") : (gained, "");
                case "luck":
                {
                    bool hasRare = false;
                    for (int i = 0; i < res.cells.Count; i++)
                    {
                        var id = res.cells[i].sym.id;
                        if (id == "star" || id == "crown" || id == "wild") { hasRare = true; break; }
                    }
                    return hasRare ? (gained * 18 / 10, " · 🎲희귀 ×1.8") : (gained * 8 / 10, " · 🎲노희귀 ×0.8");
                }
                default:
                    // grad(졸업심사)도 여기로 떨어진다 — 웹과 동일하게 무보정(quotaMul 1.15만 적용).
                    return (gained, "");
            }
        }

        // ── RunCtx 구성 헬퍼 (Kotlin runCtxOf, L71-78 · 웹 game.js:431-440 `_ctx()`) ──
        // internal(구 private) — 웹 파리티 P3.5(WEB_PARITY_DESIGN.md §2-(T) 후속③)에서 ItemUse.
        // UseRetakeForm도 이 헬퍼를 재사용해 재굴림 mods를 ctx 포함으로 빌드하도록 확장했다(웹
        // _freeReroll()이 this._mods() → this._ctx()를 그대로 타는 것과 동일한 값 구성).
        internal static RunCtx RunCtxOf(RunState run, int spinIndex, int spinsPerStage, long quota) => new RunCtx
        {
            stage = run.Stage, spinIndex = spinIndex, spinsPerStage = spinsPerStage,
            stageExp = run.StageExp, quota = quota,
            growthStack = run.GrowthStack, snowStack = run.SnowStack,
            curseCount = run.Curses.Count, unluckyGauge = run.UnluckyGauge,
            boss = Bosses.For(run.Stage) != null,
            coins = run.Coins, // 웹 파리티 P3-4(engine.js:136 makeCtx coins) — bankrupt 캐릭터 조건부 효과용.
        };

        private static string CmdMarker(SpinMode mode) => mode switch
        {
            SpinMode.Focus => "FOCUS", SpinMode.Allin => "ALLIN", SpinMode.Pray => "PRAY", SpinMode.Last => "LAST",
            _ => "N",
        };

        // ══════════════════════════════════════════════════════════════════════
        // ResolveSpin — 02_service.md §2 step 1~25 전사(step26 클리어/실패 분기는 StageFlow가 담당).
        // run을 직접 변형한다(Kotlin의 run.copy() 불변 갱신을 인메모리 가변 상태로 재설계 — 설계 원칙과
        // 일치, RunState는 엔진 내부 가변 상태다). 거부(rejected) 시에는 아무 것도 변형하지 않는다.
        // ══════════════════════════════════════════════════════════════════════
        public static SpinOutcome ResolveSpin(RunState run, SpinMode mode)
        {
            var arm = run.ArmItems;
            var phase = run.PhaseItems;
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var curses = run.Curses;

            // step 3-8: 3단계 mods 재계산(ctx조건부 증강 평가용) — 02_service.md §10-2 주의사항 그대로.
            // 웹 파리티 P3-3: run.PerkLevels(증강 레벨업)를 3단계 전부에 동일하게 전달 — 웹은 단일
            // buildMods 호출(game.js:445)이라 이 3단계 재계산 자체가 Unity 고유 구조지만, 어느 단계든
            // levels를 빠뜨리면 그 단계에서만 레벨업 미반영 값이 섞여 다음 단계 재계산이 오염된다.
            var preMods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device, levels: run.PerkLevels);
            double deepPenalty = DeepRunHooks.DeepPenalty(run);
            var preCtx = RunCtxOf(run, run.SpinIndex, SpinsPerStage(preMods0), QuotaOf(run.Stage, preMods0, run.Asc, run.BossPhase2, deepPenalty));
            var preMods = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device, preCtx, run.PerkLevels);
            int preEffSpins = EffSpins(run, preMods);
            var runCtx = RunCtxOf(run, run.SpinIndex, preEffSpins, QuotaOf(run.Stage, preMods, run.Asc, run.BossPhase2, deepPenalty));
            var baseMods = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, curses, run.Device, runCtx, run.PerkLevels);
            if (mode == SpinMode.Focus) baseMods.rareWeightMul *= 0.5; // 안정화: 고점 억제

            var mods = ModsBuilder.ApplyItemMods(baseMods, Concat(arm, phase));
            var devEq = Devices.ById(run.Device);
            if (devEq != null && devEq.kind == "PASSIVE") mods = ModsBuilder.ApplyPassiveDevice(mods, devEq.id);
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 _mods() A2/A8 규칙) — "실제 롤에 쓰이는
            // 최종 mods"에만 적용(QuotaOf 등 수치 전용 mods에는 불필요). AscRunHooks.cs 참조.
            AscRunHooks.ApplyRunAscMods(mods, run);
            // 웹 파리티 P7-2(§1-A #19 B, 웹 game.js:483-505) — 계열 아키타입(전공) mods 주입. 반드시
            // 위 AscRunHooks 직후(더 이상 클론/오버레이가 없는 최종 mods 자리)에 호출한다. 일반모드는
            // 즉시 반환(무회귀) — DeepRunHooks.ApplyDeepMods 헤더 주석 참조.
            DeepRunHooks.ApplyDeepMods(mods, run);

            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): 배율 상한(hasPrism 기반 capMul 클램프 + lastSpinExpMul
            // 5.0 상한) 제거 — 웹 engine.js에는 이 자리에 해당하는 캡이 없다(Formulas.cs 주석/§2-B 근거
            // 참조). mods.expMul/mods.lastSpinExpMul은 이제 ModsBuilder가 만든 값 그대로 쓰인다.
            int spins = EffSpins(run, mods);
            long quota = QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, deepPenalty);

            bool bossStage = Bosses.For(run.Stage) != null;
            int cmdCost = CmdCoinCost(mode, bossStage);
            // ── WEB_PARITY P1 ①: 특수스핀 첫 사용 무료(런 단위 종류별 1회, 웹 game.js:883-884) ──────
            // 코인 0이어도 발동(검증 자체를 면제) — 실제 발동이 "성공"했을 때만(거부 3경로 통과 후)
            // 아래 상태 반영부에서 CmdFreeUsed에 추가한다(§2-E: 거부 시 미소진). 기존 CmdCoinCost 표
            // (1/2/3/4, 보스+1, 상한5)는 불변 — 무료는 그 위에 얹는 오버레이일 뿐이다.
            bool isFreeUse = mode != SpinMode.N && !run.CmdFreeUsed.Contains(CmdMarker(mode));
            if (isFreeUse) cmdCost = 0;
            if (mode != SpinMode.N)
            {
                if (mode == SpinMode.Last && run.SpinIndex != spins - 1)
                    return Rejected(mode, "LAST_NOT_FINAL_SPIN");
                if (run.UsedCmds.Contains(CmdMarker(mode)))
                    return Rejected(mode, "MODE_ALREADY_USED");
                if (run.Coins < cmdCost)
                    return Rejected(mode, "INSUFFICIENT_COINS");
            }

            int reel = (devEq != null && devEq.id == "dev_subreel") ? Formulas.REEL + 1 : Formulas.REEL;
            // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 game.js:657-691 `_roll`/903) — 심화모드는
            // 가중추첨(Weighted/RollRaw) 대신 주머니 추출(RollCells → PouchOps.PouchDraw)을 탄다.
            // LockedNext(예언/timeline_ticket으로 확정된 다음 스핀)는 웹과 동일하게 fresh 굴림이 아니므로
            // deepPity를 태우지 않는다(웹 `_pityRoll`은 `_roll(...)` 체인에만 걸리고, PEEK만 예외적으로
            // 그 확정 굴림 시점에 자체적으로 pity를 소진한다 — DeviceActions.HandlePeek 참조, §0 blocker).
            // 웹 파리티 P7-2 blocker(§0) — `Symbols.ById("empty")`가 null이라 LockedNext 경로가
            // CellsFromIds(구버전)를 타면 "empty" 칸이 조용히 드롭돼 릴 칸수가 줄어들 수 있었다(위
            // CellsFromIds 헤더 주석 참조) — rng/pouch를 함께 넘겨 "random"까지 안전하게 왕복시킨다.
            // 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종", 웹 game.js:892 `spin()`
            // 프리훅) — 🧲임시와일드(temp_wild) 보유 시 매 스핀 무조건 wild_temp cellOp 주입(자연 등장
            // 여부 무관 — "소유" 자체가 효과, fuse 소비는 실제 temp_wild 심볼이 자연히 드로우됐을 때만
            // DeepRunHooks가 처리한다). "wild_temp"는 실제 Item 카탈로그에 fx 없는 순수 cellOp 코드라
            // (Items.cs — eraser_old/fake_crown과 동일 부류) ApplyItemMods에 섞여 들어가도 무해하다.
            if (run.DeepMode && run.Pouch.TryGetValue("temp_wild", out var twStock) && twStock > 0) arm.Add("wild_temp");

            List<Cell> raw;
            if (run.LockedNext.Count > 0)
            {
                raw = CellsFromIds(run.LockedNext, run.Rng, run.Pouch);
            }
            else
            {
                raw = RollCells(run, mods, reel, run.SeedNext);
                if (run.DeepMode) raw = DeepRunHooks.ApplyGrowNext(run, raw);
                if (run.DeepMode) raw = DeepRunHooks.ApplyDeepPity(run, raw);
            }
            ApplyCellOps(raw, arm, run.Rng);

            var res = Evaluate(run.Rng, raw, mods, run.SpinIndex, spins, run.FlameNext);
            // 웹 파리티 P7-3b — 🌀운명의소용돌이(FATEVORTEX·fuse) 런 1회: 2번째 굴림을 수행해
            // 더 좋은 결과 채택(웹 game.js:907-916). [웹 quirk 재현] 웹 원문의 `!r.lockedNext` 가드는
            // 바로 위 줄(`r.lockedNext = null;`, 이 스핀이 lockedNext를 썼든 안 썼든 무조건 실행)에서
            // 이미 null로 리셋된 뒤라 사실상 항상 참(=죽은 가드)이다 — Unity는 run.LockedNext를 이
            // 시점까지 아직 비우지 않으므로(비우는 시점은 아래 상태 반영부) 문자 그대로 "LockedNext
            // 비어있을 때만" 조건을 그대로 옮기면 웹과 실제로 달라진다(예언 스핀에서 미발동). 웹의
            // "실질 항상-참" 동작을 그대로 재현하기 위해 LockedNext 조건 자체를 넣지 않는다.
            if (run.DeepMode && run.Pouch.TryGetValue("fate_vortex", out var fvStock) && fvStock > 0
                && !run.FateVortexUsed)
            {
                var raw2 = RollCells(run, mods, reel, false);
                var res2 = Evaluate(run.Rng, raw2, mods, run.SpinIndex, spins, run.FlameNext);
                if (res2.exp > res.exp)
                {
                    // Opus 2차검수(P7-3b) [MED-3] — 채택된 굴림(res2)이 실제로 릴에 표시될 셀이므로
                    // raw/rawIds도 함께 res2 쪽으로 교체해야 한다(웹은 애초에 단일 `res`만 다뤄 이런
                    // 괴리가 없음). 1차 구현은 raw/rawIds가 여전히 "버려진" 첫 번째 굴림을 가리켜
                    // run.LastCells(릴 표시용)·CellInfoView(셀 정보 탭)가 실제 채택된 결과와 어긋났었다.
                    raw = raw2;
                    res = res2;
                    res.notes.Add("🌀 운명의소용돌이 — 더 좋은 결과 선택");
                }
                else
                {
                    res.notes.Add("🌀 운명의소용돌이 — 원래 결과 유지");
                }
                run.FateVortexUsed = true;
            }
            // Opus 2차검수(P7-3b) [MED-2] — instant 5종(bandage/knot/energypack/fake_crown_sym/
            // evo_core) 소비를 fate_vortex 채택 "이후"의 최종 res 기준으로 옮겼다(웹 `_applyDeepSpinMeta
            // (res)`가 실제로 쓰이는 res를 소비하는 것과 동일 순서 — 이전엔 Evaluate 호출 전 raw만 보고
            // 선(先)소비해, fate_vortex가 res2를 채택하면 "버려진 첫 굴림" 기준으로 잘못 소비하고 있었다).
            if (run.DeepMode) DeepRunHooks.ConsumeInstantSymbols(run, res);
            var rawIds = raw.Select(c => c.sym.id).ToList();
            long gained = res.exp;
            var outcomeNotes = new List<string>(res.notes);
            bool prayMiracle = false;

            switch (mode)
            {
                case SpinMode.Focus:
                {
                    long floor = (long)(quota / (double)spins * 0.6);
                    if (gained < floor) { gained = floor; outcomeNotes.Add("🎯집중 — 최소 보장"); }
                    else outcomeNotes.Add("🎯집중(안정)");
                    break;
                }
                case SpinMode.Allin:
                    if (res.skulls >= 2) { gained = 0; outcomeNotes.Add("🎲올인 실패! EXP 0"); }
                    else { gained *= 2; outcomeNotes.Add("🎲올인 성공! EXP ×2"); }
                    break;
                case SpinMode.Pray:
                {
                    long low = (long)(quota / (double)spins * 0.5);
                    if (run.Rng.Next(100) < 8) { gained *= 3; outcomeNotes.Add("🙏✨ 기적! EXP ×3"); prayMiracle = true; }
                    else if (gained < low) { gained += 25; outcomeNotes.Add("🙏기도 — 불운보정 +25"); }
                    else outcomeNotes.Add("🙏기도");
                    break;
                }
                case SpinMode.Last:
                    gained = (long)(gained * 1.75);
                    outcomeNotes.Add("⏰최후 EXP ×1.75");
                    break;
            }
            // WEB_PARITY P1 ①: 무료 발동 배너(웹 game.js:1141 "🆓첫 사용 무료") — astral 이모지 금지,
            // 한글만(uGUI Text 렌더 제약, 이 파일 CLAUDE.md 지시).
            if (isFreeUse) outcomeNotes.Add("첫 사용 무료");

            if (run.PendingNextExpMul != 1.0)
            {
                gained = (long)(gained * run.PendingNextExpMul);
                outcomeNotes.Add($"🪙다음스핀 ×{FmtMul(run.PendingNextExpMul)}");
            }

            var boss = Bosses.For(run.Stage);
            if (boss != null)
            {
                // 웹 파리티 P7-3b — 🛡방패(SHIELD)/📋시험지(EXEMPT), 웹 game.js:919-928. 시험지=strict/luck
                // 감점룰 자체를 이번 스핀만 무시(발동 시에만 소비). 방패=보스 패널티(EXP 감소)만 방어(보너스는
                // 유지) — 발동 실제로 패널티가 있었을 때만 소비. 둘 다 심화모드 전용(일반 런은 항상 false).
                if (run.DeepMode && run.BossExempt && (boss.id == "strict" || boss.id == "luck"))
                {
                    run.BossExempt = false;
                    outcomeNotes.Add("📋 시험지 — 보스 감점룰 무시");
                }
                else
                {
                    var (g2, bn) = ApplyBoss(boss, gained, res, run.SpinIndex, spins);
                    if (run.DeepMode && run.BossShield && g2 < gained)
                    {
                        run.BossShield = false;
                        outcomeNotes.Add("🛡 방패 — 보스 패널티 방어");
                    }
                    else
                    {
                        gained = g2;
                        if (!string.IsNullOrEmpty(bn)) outcomeNotes.Add(bn.TrimStart(' ', '·'));
                    }
                }
            }

            if (devEq != null && devEq.id == "dev_safe")
            {
                long fl = (long)(quota / (double)spins * 0.35);
                if (gained < fl) { gained = fl; outcomeNotes.Add("🦺안전벨트 최소 보장"); }
            }

            bool destroyDevice = false;
            if (arm.Contains("dev_bell"))
            {
                gained = Math.Max(gained, Math.Max(quota - run.StageExp, 0) + 1);
                destroyDevice = true;
                outcomeNotes.Add("🔔비상졸업벨 발동! 즉시 클리어");
            }

            // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js:960-1138) — 희귀표본
            // 상자/퍼펙트 드로우/잭팟 태그 후속(bias 예약·승격 심볼 소모)/피버 게이지. 이미 mode/보스/
            // dev_safe/dev_bell 보정까지 끝난 gained를 더 조정하고(res.score/res.coins도 직접 가산),
            // 배너 문자열은 outcomeNotes에 합류한다. 일반모드는 즉시 반환(무회귀).
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, outcomeNotes);

            double expected = spins > 0 ? quota / (double)spins : 0.0;
            bool badSpin = !destroyDevice && (gained <= expected * 0.4 || res.skulls >= 3);
            int newGauge = badSpin ? Math.Min(run.UnluckyGauge + 1, Formulas.UNLUCKY_MAX) : run.UnluckyGauge;
            if (badSpin && newGauge > run.UnluckyGauge)
                outcomeNotes.Add(newGauge >= Formulas.UNLUCKY_MAX
                    ? "🍀불운 가득! 다음 보상 희귀↑ 보장"
                    : $"🍀불운 {newGauge}/{Formulas.UNLUCKY_MAX}");   // Kotlin L598-599

            int adjPairsFired = (mods.adjacentSameExp != 0 && AdjPairCount(res.cells) > 0) ? 1 : 0;
            int set4Fired = res.bestSetCount >= 4 ? 1 : 0;
            int prevSpinIndex = run.SpinIndex;
            // 🔥 한 방 신기록 — 이번 런 최고 EXP 스핀 갱신 시 연출 (Kotlin L605-607)
            if (gained > run.RunBestSpin && gained >= quota / 2 && run.RunBestSpin > 0)
                outcomeNotes.Add("🔥 이번 런 최고의 한 방!");
            long newExp = run.StageExp + gained;
            long newScore = run.Score + res.score;
            long newCoins = run.Coins + res.coins - cmdCost;
            int newIdx = run.SpinIndex + 1;

            // ── run 상태 반영 (Kotlin run.copy(...), L627-643) ──
            run.StageExp = newExp;
            run.Score = newScore;
            run.Coins = newCoins;
            run.SpinIndex = newIdx;
            run.FlameNext = res.flameNext;
            run.SeedNext = res.seedNext;
            run.ArmItems.Clear();
            if (mode != SpinMode.N)
            {
                run.UsedCmds.Add(CmdMarker(mode));
                // WEB_PARITY P1 ①: 무료권은 "발동 성공" 시에만 소진(위에서 거부 3경로를 이미 통과한
                // 뒤라 여기 도달 = 성공). 스테이지 클리어 리셋(StageFlow.ClearStage)은 이 필드를
                // 건드리지 않으므로 런 끝까지 종류별 1회만 유지된다.
                if (isFreeUse) run.CmdFreeUsed.Add(CmdMarker(mode));
            }
            if (destroyDevice) run.Device = "";
            run.LastCells.Clear(); run.LastCells.AddRange(rawIds);
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16, 웹 game.js:1286 `r.lastMods = mods`) — 셀 정보
            // 탭(CellInfoView)이 "지금"이 아니라 "그 칸이 실제로 나온 스핀"의 mods로 분해하도록 캐시.
            run.LastMods = mods;
            // Opus 2차검수 필수①(2026-08-09) — 웹 game.js:1286 `r.lastCells = res.cells.map(...)` 그대로.
            // res(=SpinResult).cells는 폭탄 제거/자석 복사/성장/와일드 주입이 전부 반영된 "최종" 칸이라
            // 위 run.LastCells(rawIds, Evaluate 이전 원시 입력)와 값이 다를 수 있다.
            run.LastCellsFinal.Clear(); run.LastCellsFinal.AddRange(res.cells);
            run.LastNotes.Clear(); if (res.notes != null) run.LastNotes.AddRange(res.notes);
            run.LastGain = gained;
            run.LastScoreGain = res.score;
            run.LastCoinGain = res.coins;
            run.LastSet4 = set4Fired;
            run.LastAdjPairs = adjPairsFired;
            run.LastSpinNo = prevSpinIndex;
            run.PendingNextExpMul = 1.0;
            run.LockedNext.Clear();
            run.RunJackpots += res.jackpotSym != null ? 1 : 0;
            run.RunBestSpin = Math.Max(run.RunBestSpin, gained);
            BumpSymCounts(run.RunSymCounts, res.cells);
            run.UnluckyGauge = newGauge;
            run.RunAdjPairs += adjPairsFired;
            run.RunSet4 += set4Fired;
            run.RunPrayWins += (mode == SpinMode.Pray && (prayMiracle || newExp >= quota)) ? 1 : 0;
            run.RunUsedCmd = run.RunUsedCmd || mode != SpinMode.N;

            return new SpinOutcome
            {
                rejected = false,
                mode = mode,
                result = res,
                gained = gained,
                newExp = newExp,
                newScore = newScore,
                newCoins = newCoins,
                newSpinIndex = newIdx,
                quota = quota,
                spins = spins,
                destroyDevice = destroyDevice,
                badSpin = badSpin,
                prayMiracle = prayMiracle,
                cmdCost = cmdCost,
            }.WithNotes(outcomeNotes);
        }

        // 주의: 거부 outcome은 result==null, quota/spins/gained==0 — 호출측(S4 포함)은 rejected 확인 후 다른 필드에 접근 금지.
        private static SpinOutcome Rejected(SpinMode mode, string reason) => new SpinOutcome
        {
            rejected = true, rejectReason = reason, mode = mode,
        };

        // StageFlow 등 외부에서 스핀 자체를 시작하지 못할 때 쓰는 거부 outcome (동일 주의사항 적용).
        public static SpinOutcome RejectedOutcome(string reason) => new SpinOutcome
        {
            rejected = true, rejectReason = reason, mode = SpinMode.N,
        };

        private static List<string> Concat(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var list = new List<string>(a);
            list.AddRange(b);
            return list;
        }

        private static int AdjPairCount(IReadOnlyList<Cell> cells)
        {
            int p = 0;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                var a = cells[i].sym; var b = cells[i + 1].sym;
                if (a.id == b.id && ValueIds.Contains(a.id)) p++;
            }
            return p;
        }

        // Kotlin bumpSymCounts(csv, cells)(SlotV2Service.kt L753-758) — "empty" 제외, id별 누적.
        private static void BumpSymCounts(Dictionary<string, int> counts, IReadOnlyList<Cell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var id = cells[i].sym.id;
                if (id == "empty") continue;
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // S4 훅 인터페이스 — MANIP 장치(dev_reroll/dev_pin/dev_copy/dev_swap)·도박꾼 무료재굴림·POST_SPIN
        // 만회는 "직전 스핀 1개를 조작해 재계산"하는 완전히 별도의 커맨드 플로우다(02_service.md §7-C·
        // §10-7 net-adjust 패턴). 이 슬라이스(S3)는 정의만 내려주고 구현하지 않는다 — 필요한 재료
        // (run.LastCells/LastGain/LastScoreGain/LastCoinGain/LastSet4/LastAdjPairs, RollOne, CellsFromIds,
        // Evaluate)는 전부 이 파일에 이미 공개돼 있다. S4(DeviceActions.cs)가 구현해야 할 계약:
        //   1) 대상 장치의 kind==MANIP(재굴림/고정/복사/교체) 또는 캐릭터==gambler(무료재굴림)인지 검증.
        //   2) 이번 스테이지 usedCmds에 해당 cmd 마커가 없는지 확인(스테이지당 1회, §9-C).
        //   3) 코인 비용 검증·차감(재굴림/고정=3, 복사/교체=5 — Device 데이터클래스엔 없고 desc 텍스트에만
        //      있는 수치라 S4가 별도 상수화해야 함, 01_engine.md 부록A-6).
        //   4) run.LastCells를 CellsFromIds로 복원 → 고정(N번 칸 유지·나머지 RollOne 재굴림) / 복사(N번 칸을
        //      오른쪽 인접 칸에, 오른쪽 끝이면 왼쪽으로) / 교체(N번 칸을 bestValueId 최다종류로, 동점/없음
        //      이면 "star" 폴백) / 전체 재굴림 중 하나로 새 raw 구성.
        //   5) Evaluate(rng, newRaw, mods, run.SpinIndex-1, spins, flamePenalty=false)로 재평가(웹 파리티
        //      P2: capMul 인자 제거됨 — WEB_PARITY_DESIGN §2-B)
        //      (mods는 이 스핀 시점과 동일 조건으로 재구성 — ResolveSpin의 step1-14를 재사용).
        //   6) EXP -10% 페널티(MANIP만, 도박꾼 무료재굴림은 페널티 없음) 적용 후 gained 재산출.
        //   7) net-adjust: run.Score/run.Coins/run.RunSet4/run.RunAdjPairs에서 run.LastGain(EXP는
        //      StageExp)/LastScoreGain/LastCoinGain/LastSet4/LastAdjPairs를 먼저 빼고 새 결과를 더한다
        //      (직전 스핀 "1개 교체" — 반드시 스테이지당 1회 제한과 함께 구현, 아니면 이중 차감 버그).
        //   8) fromPost=true(POST_SPIN 경유)면 결과를 곧바로 StageFlow.ClearStage/HandleFailure로 확정
        //      (재시도 루프 없음, 02_service.md §3-C "POST_SPIN은 1회성").
        //   9) run.RunRerolled = true(MANIP/도박꾼재굴림 1회라도 쓰면 런 끝까지 유지).
        // ════════════════════════════════════════════════════════════════════
    }

    // SpinOutcome.notes는 readonly 컬렉션 초기화 문법과 생성자 없는 object-initializer를 함께 쓰기 위한
    // 얇은 헬퍼(C# 9엔 record with가 없어 이 패턴으로 필드를 채운 뒤 반환한다).
    internal static class SpinOutcomeExt
    {
        public static SpinOutcome WithNotes(this SpinOutcome outcome, List<string> notes)
        {
            outcome.notes.AddRange(notes);
            return outcome;
        }
    }
}
