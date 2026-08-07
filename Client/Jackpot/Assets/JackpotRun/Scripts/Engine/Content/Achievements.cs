using System;

namespace JackpotRun.Engine
{
    // 업적 정의 — 웹 파리티 P3-2(WEB_PARITY_DESIGN.md §1-A #10, §2-C) 슬라이스. public/play/data.js:774-848
    // 의 export const ACHIEVEMENTS(34종 = 기본16 + 후반5 + 심화13, data.js:774-817)를 전사한다. 구
    // Kotlin 스냅샷 기반 482종(기본16+확장466)은 이 34종으로 전량 교체됐다 — WEB_PARITY_DESIGN.md
    // §2-(C) 결정("482종 자산은 삭제가 아니라 Achievements.cs를 웹 34종으로 교체하고 구파일은 git
    // 이력으로만 남긴다") — 구 파일 내용은 git log(이 커밋의 부모)에서 조회 가능하다.
    //
    // ── 필드 매핑 ──────────────────────────────────────────────────────────────────────────
    // 웹 오브젝트 { id, e(emoji), n(name), key, th(threshold), d(desc), deep? }를 그대로 옮긴다
    // (id/name/desc/key/threshold/deep은 데이터 그대로 — 아래 "이모지" 각주만 예외).
    // 웹 쪽에는 cat(카테고리)/tier(등급)/hidden/reward 개념이 아예 없다 — AchDef 계약상 남아있는
    // (StatReq[] req 배열 표현 포함) 이 4개 필드는 구 기본 16종과 동일한 균일 기본값(cat="기타",
    // tier="브론즈", hidden=false, reward="")을 채운다. tier="브론즈"는 Formulas.AccountExp ④
    // 컴포넌트(AchTierExp)가 계속 동작하게 하는 안전한 폴백값이기도 하다(AchTierExp의 default case가
    // 이미 브론즈=20으로 정의돼 있다, Core/Formulas.cs L295-304). 현재 UI(MenuView/GameOverPanel/
    // DexView) 어디도 cat/tier/hidden/reward를 표시하지 않는다(전수 grep 확인) — 판정/향후 확장용
    // 필드일 뿐 표시용이 아니다.
    //
    // ── 이모지 — astral(서로게이트 페어) 대체 ─────────────────────────────────────────────────
    // 레거시 uGUI Text는 astral 이모지(U+10000 이상, 요즘 이모지 대부분)를 렌더링하지 못한다 — 이미
    // JackpotCatalog.CategoryTitle에서 확인된 선례(S8 항목⑤, "🏅(astral)는 렌더링되지 않는다 — 한글
    // 라벨만 사용"). 이 파일의 emoji는 GameOverPanel.BuildAchRows가 실제로 `$"{a.emoji} {a.name} —
    // {a.desc}"` 형태로 레거시 Text에 렌더링하므로(구 482종도 갖고 있던 잠재 버그 — 이번 교체로 함께
    // 정리) 웹 이모지가 astral이면 빈 문자열로 대체한다(같은 선례의 "한글 대체" 그대로 — 이름/설명이
    // 이미 한글이라 아이콘 손실만 발생, 텍스트 정보 손실 없음). BMP(U+FFFF 이하) 이모지 5종(⏰⭐⚠️☠️⚖️)
    // 만 웹 원본 그대로 유지했다. 아래 각 항목 주석에 "(astral→빈 문자열)" 표시.
    public sealed class AchDef
    {
        public string id, name, emoji, desc, cat, tier;
        public bool hidden;
        public bool deep;   // 웹 data.js deep:true 보존(P7 대비) — 판정 로직에는 관여하지 않음(동일한 key>=threshold 판정).
        public string reward;
        public StatReq[] req;   // (key, threshold) 정확히 1원소 — 웹 {key, th}과 1:1.
    }

    // 업적 34종 — data.js:774-817 (ACHIEVEMENTS). 배열 순서도 웹 원본 그대로(기본16 → 후반5 → 심화13).
    //
    // ── 장치 보상(웹 ACH_DEVICE_REWARD, data.js:818-828) ─────────────────────────────────────
    // 웹은 "업적 id → 장치 id" 21건(기본 12 + 심화 9)을 별도 딕셔너리로 관리하지만, Unity Devices.cs는
    // 이미 unlockAch 필드(업적 id를 담는 역방향 매핑)로 같은 역할을 하므로 별도 테이블을 새로 두지
    // 않는다 — 기본 12건은 Devices.cs 12종의 unlockAch를 이 파일의 새 id로 직접 갱신했다:
    //   jackpot1→dev_subreel, boss1→dev_reroll, crown10→dev_seal, cherry100→dev_safe, exact1→dev_pin,
    //   lastclear5→dev_overheat, score10k→dev_coin, stage10→dev_oracle, prism5→dev_copy, boss5→dev_swap,
    //   runs20→dev_bell, score50k→dev_flame (AchievementEngine.Evaluate가 unlockAch==달성id인 장치를
    //   범용으로 찾아 OwnedDevices에 추가 — lic_ 접두 특례 제거, 자세한 내용은 AchievementEngine.cs 참조).
    // 심화 9건은 대응 장치 자체가 Devices.cs에 없다(전부 P7 심화 전용 신규 장치) — 데이터/주석만
    // 남기고 미적용:
    //   d_ach_compress1→dev_compress_gauge, d_ach_big_pouch→dev_expand_scale, d_ach_risk_compress→dev_baton,
    //   d_ach_purifier→dev_purify_glove, d_ach_balance→dev_tag_scanner, d_ach_rare10→dev_rare_detector,
    //   d_ach_legend5→dev_legend_seal, d_ach_master→dev_pouch_shuffler, d_ach_crown→dev_call_bell.
    //
    // ── 심볼 해금(웹 ACH_SYMBOL_UNLOCK, data.js:834-848) ─────────────────────────────────────
    // 심화 업적 달성 → POUCH_SYMBOLS 잠금 심볼 해금(13건). Unity에는 주머니/심볼 시스템 자체가 없다
    // (P7 범위) — 데이터를 옮기지 않는다. P7에서 심화모드를 이식할 때 data.js:834-848을 참조할 것.
    public static class Achievements
    {
        public const int Count = 34;
        public const int BaseCount = 16;   // 웹 "기본 16" 구간 (data.js:775-790)
        public const int LateCount = 5;    // 웹 "후반 업적(레벨/졸업/심화 학기)" 구간 (data.js:792-796)
        public const int DeepCount = 13;   // 웹 "Phase 5: 심화모드 전용" 구간, 전부 deep=true (data.js:800-816)

        public static readonly AchDef[] All =
        {
            // ══════════════════════════════════════════════════════════════════
            // 기본 16종 — data.js:775-790
            // ══════════════════════════════════════════════════════════════════
            new AchDef { id = "cherry100", emoji = "", name = "체리 수확가", desc = "🍒체리 누적 100개", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("cherryTotal", 100L) } }, // 🍒(astral→빈 문자열)
            new AchDef { id = "cherry500", emoji = "", name = "체리 중독", desc = "🍒체리 누적 500개", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("cherryTotal", 500L) } }, // 🍒(astral→빈 문자열)
            new AchDef { id = "crown10", emoji = "", name = "왕관 수집가", desc = "👑왕관 누적 10개", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("crownTotal", 10L) } }, // 👑(astral→빈 문자열)
            new AchDef { id = "crown30", emoji = "", name = "대관식", desc = "👑왕관 누적 30개", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("crownTotal", 30L) } }, // 👑(astral→빈 문자열)
            new AchDef { id = "jackpot1", emoji = "", name = "첫 잭팟", desc = "5칸 잭팟 1회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("jackpots", 1L) } }, // 🎰(astral→빈 문자열)
            new AchDef { id = "jackpot10", emoji = "", name = "잭팟 헌터", desc = "5칸 잭팟 10회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("jackpots", 10L) } }, // 🎰(astral→빈 문자열)
            new AchDef { id = "boss1", emoji = "", name = "중간고사 통과", desc = "보스 1회 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bossClears", 1L) } }, // 📝(astral→빈 문자열)
            new AchDef { id = "boss5", emoji = "", name = "졸업반", desc = "보스 5회 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bossClears", 5L) } }, // 🎓(astral→빈 문자열)
            new AchDef { id = "stage10", emoji = "", name = "10층 등반", desc = "스테이지 10 도달", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestStage", 10L) } }, // 🧗(astral→빈 문자열)
            new AchDef { id = "stage15", emoji = "", name = "최종보스 도달", desc = "스테이지 15 도달", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestStage", 15L) } }, // 🏔️(astral→빈 문자열)
            new AchDef { id = "lastclear5", emoji = "⏰", name = "벼락치기 천재", desc = "마지막 스핀 클리어 5회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("lastSpinClears", 5L) } }, // ⏰ U+23F0 BMP — 웹 그대로 유지
            new AchDef { id = "exact1", emoji = "", name = "완벽한 계산", desc = "요구 EXP 정확히 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("exactClears", 1L) } }, // 🎯(astral→빈 문자열)
            new AchDef { id = "prism5", emoji = "", name = "규칙 파괴자", desc = "프리즘 증강 5회 선택", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("prismPicks", 5L) } }, // 🌈(astral→빈 문자열)
            new AchDef { id = "score10k", emoji = "", name = "만점왕", desc = "최고 점수 10,000", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestScore", 10000L) } }, // 💯(astral→빈 문자열)
            new AchDef { id = "score50k", emoji = "", name = "슬롯의 지배자", desc = "최고 점수 50,000", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestScore", 50000L) } }, // 🏆(astral→빈 문자열)
            new AchDef { id = "runs20", emoji = "", name = "단골", desc = "20런 플레이", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("runs", 20L) } }, // 🔁(astral→빈 문자열)

            // ══════════════════════════════════════════════════════════════════
            // 후반 업적(레벨/졸업/심화 학기) 5종 — data.js:792-796
            // ══════════════════════════════════════════════════════════════════
            // grad1: "graduations" 카운터 — 웹 game.js:1401 "stage===15 클리어 = 졸업"을 StatTracker.
            // ApplyClearTracking이 그대로 이식해서 증분한다(다음 파일 참조) — Unity에 승천 시스템이
            // 없어도 "스테이지 15 클리어"만으로 이미 실현 가능한 조건이라 이번 슬라이스에서 함께 켰다.
            new AchDef { id = "grad1", emoji = "", name = "졸업생", desc = "스테이지 15 클리어(졸업) 1회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("graduations", 1L) } }, // 🎓(astral→빈 문자열)
            // lv20/lv40: "playerLevel" 카운터 — StatTracker.ApplyGameOverTracking이 PlayerLevelTracker
            // 실행 *직전*(1런 지연) 스냅샷을 기록한다(웹 game.js:2578과 동일한 지연, 작업 지시 3번).
            new AchDef { id = "lv20", emoji = "⭐", name = "베테랑", desc = "플레이어 레벨 20 달성", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("playerLevel", 20L) } }, // ⭐ U+2B50 BMP — 웹 그대로 유지
            // asc3/asc5: "ascMax" — 승천(심화 학기, P6·WEB_PARITY_DESIGN.md §1-A #18)은 아직 미이식이라
            // ascMax 카운터가 영원히 0(미기록)이다 — 심화 13종과 동일한 "카운터 없음, 데이터만 포함"
            // 상태(작업 지시 1번 유예 대상, deep 플래그는 안 붙지만 실질은 동일).
            new AchDef { id = "asc3", emoji = "", name = "심화 3 수료", desc = "심화 학기 3 졸업", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("ascMax", 3L) } }, // 📈(astral→빈 문자열)
            new AchDef { id = "asc5", emoji = "", name = "심화 5 석사", desc = "심화 학기 5 졸업", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("ascMax", 5L) } }, // 🔥(astral→빈 문자열)
            new AchDef { id = "lv40", emoji = "", name = "고인물", desc = "플레이어 레벨 40 달성", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("playerLevel", 40L) } }, // 🌌(astral→빈 문자열)

            // ══════════════════════════════════════════════════════════════════
            // Phase 5: 심화모드(주머니 덱빌딩) 전용 업적 13종, 전부 deep=true — data.js:800-816
            // key는 전부 deep* 신규 카운터인데 StatTracker가 아직 수집하지 않는다(P7 심화모드 슬라이스
            // 범위) — 작업 지시 1번: "카운터가 아직 없어 영원히 미달성이어도 데이터는 포함".
            // ══════════════════════════════════════════════════════════════════
            new AchDef { id = "d_ach_start", emoji = "", name = "심볼연구 시작", desc = "[심화] 심화모드 첫 플레이", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepRuns", 1L) } }, // 🔬(astral→빈 문자열)
            new AchDef { id = "d_ach_compress1", emoji = "", name = "첫 압축", desc = "[심화] 총량 27↓로 스테이지 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepCompress95", 1L) } }, // 🗜️(astral→빈 문자열)
            new AchDef { id = "d_ach_risk_compress", emoji = "⚠️", name = "위험한 압축", desc = "[심화] 총량 85↓로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepCompress85Boss", 1L) } }, // ⚠️ U+26A0 BMP — 웹 그대로 유지
            new AchDef { id = "d_ach_big_pouch", emoji = "", name = "대형 주머니", desc = "[심화] 총량 36↑ 달성", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepMaxTotal", 36L) } }, // 🎒(astral→빈 문자열)
            new AchDef { id = "d_ach_cherry_major", emoji = "", name = "체리 전공", desc = "[심화] 체리 계열(🍒+🍑) 비중 50%↑로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepCherry50Boss", 1L) } }, // 🍒(astral→빈 문자열)
            new AchDef { id = "d_ach_curse_major", emoji = "☠️", name = "저주 전공", desc = "[심화] 해골 40%↑로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepSkull40Boss", 1L) } }, // ☠️ U+2620 BMP — 웹 그대로 유지
            new AchDef { id = "d_ach_gem_major", emoji = "", name = "보석 전공", desc = "[심화] 보석 계열(💎+💠) 비중 50%↑·점수 3만↑ 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepGem50Score30k", 1L) } }, // 💎(astral→빈 문자열)
            new AchDef { id = "d_ach_crown", emoji = "", name = "왕관 연구", desc = "[심화] 주머니 왕관 2개로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepCrown2Boss", 1L) } }, // 👑(astral→빈 문자열)
            new AchDef { id = "d_ach_balance", emoji = "⚖️", name = "완벽한 균형", desc = "[심화] 모든 태그 20%↓ 균형으로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepBalanceBoss", 1L) } }, // ⚖️ U+2696 BMP — 웹 그대로 유지
            new AchDef { id = "d_ach_purifier", emoji = "", name = "정화자", desc = "[심화] 주머니 해골 0으로 보스 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepSkull0Boss", 1L) } }, // 🕊️(astral→빈 문자열)
            new AchDef { id = "d_ach_rare10", emoji = "", name = "희귀 수집가", desc = "[심화] 희귀 등급 심볼 6종 발견", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepRaresSeen", 6L) } }, // 🔮(astral→빈 문자열)
            new AchDef { id = "d_ach_legend5", emoji = "", name = "전설 연구자", desc = "[심화] 전설 등급 심볼 3종 발견", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepLegendsSeen", 3L) } }, // 🏆(astral→빈 문자열)
            new AchDef { id = "d_ach_master", emoji = "", name = "심볼 마스터", desc = "[심화] 심화모드 보스 통산 10회 클리어", cat = "기타", tier = "브론즈", reward = "", deep = true, req = new[] { new StatReq("deepBossClears", 10L) } }, // 🎓(astral→빈 문자열)
        };

        public static AchDef ById(string id) => Array.Find(All, x => x.id == id);
    }
}
