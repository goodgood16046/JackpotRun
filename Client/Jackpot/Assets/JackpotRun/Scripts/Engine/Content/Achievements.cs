using System;

namespace JackpotRun.Engine
{
    // 업적 정의 — Kotlin data class SlotV2Engine.Achievement(id,emoji,name,key,threshold,desc,cat,tier,reward,hidden) 전사.
    // req는 설계 계약(ENGINE_PORT_DESIGN.md)에 없던 필드다. Kotlin Achievement는 key(단일 stat 키)+threshold(단일
    // 임계값) 쌍 하나만 가지는데(다중 조건 없음), 이 프로젝트 공용 StatReq{key,value} 타입(Core/StatReq.cs)이
    // 그 표현과 정확히 일치하고 Machine/Character의 unlockReq 패턴과도 통일되므로, key/threshold를 별도 string+long
    // 필드로 두는 대신 StatReq[] req(항상 정확히 1원소)로 옮겼다 — [S5a 설계 조정, 보고 대상].
    public sealed class AchDef
    {
        public string id, name, emoji, desc, cat, tier;
        public bool hidden;
        public string reward;
        public StatReq[] req;   // Kotlin Achievement.key/threshold 1:1 대응 (req[0].key/req[0].value), 항상 1원소
    }

    // 업적 482종 = 기본 16(SlotV2Engine.kt L1470-1487 ACHIEVEMENTS_BASE) + 확장 466(SlotV2AchievementsExt.kt
    // L5-681 LIST). SlotV2Engine.kt: `val ACHIEVEMENTS: List<Achievement> = ACHIEVEMENTS_BASE + SlotV2AchievementsExt.LIST`
    // — 이 파일의 All 배열도 동일 순서(기본 16 먼저, 그다음 확장 466을 Kotlin 파일 등장 순서 그대로)로 전사했다.
    //
    // 기본 16개는 Kotlin에서 positional 인자 6개(id,emoji,name,key,threshold,desc)만 넘기고 나머지는 데이터클래스
    // 기본값을 그대로 쓴다 — cat="기타", tier="브론즈", reward="", hidden=false. 03_meta.md는 이 기본 16개의 실제
    // 필드를 "미확인"이라 적었지만(추출 문서가 SlotV2Engine.kt를 커버하지 않음), 이 슬라이스는 SlotV2Engine.kt
    // 원본을 직접 대조해 위 기본값까지 확정했다.
    //
    // 장치 면허(lic_*, cat="면허" 12종)·장치 숙련/장인(dm_*, cat="장치면허" 24종)은 Kotlin에도 별도 매핑 구조가
    // 없다(SlotV2AchievementsExt.kt에 평범한 Achievement 원소로만 존재, 03_meta.md §2.1-2.2) — 그래서 이 파일도
    // 별도 정적 테이블을 추가하지 않았다. id/key 명명 패턴(lic_dev_<deviceId>, dvuse_dev_<deviceId>,
    // dvstage_dev_<deviceId>)으로 Devices.cs id와 대조 가능하며, 그 무결성은 Tests_Ach.cs가 검증한다.
    public static class Achievements
    {
        public const int Count = 482;
        public const int BaseCount = 16;
        public const int ExtCount = 466;

        public static readonly AchDef[] All =
        {
            // ══════════════════════════════════════════════════════════════════
            // 기본 16종 — SlotV2Engine.kt L1470-1487 (ACHIEVEMENTS_BASE)
            // ══════════════════════════════════════════════════════════════════
            new AchDef { id = "cherry100", emoji = "🍒", name = "체리 수확가", desc = "🍒체리 누적 100개 등장", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("cherryTotal", 100L) } },
            new AchDef { id = "cherry500", emoji = "🍒", name = "체리 중독", desc = "🍒체리 누적 500개 등장", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("cherryTotal", 500L) } },
            new AchDef { id = "crown10", emoji = "👑", name = "왕관 수집가", desc = "👑왕관 누적 10개 등장", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("crownTotal", 10L) } },
            new AchDef { id = "crown30", emoji = "👑", name = "대관식", desc = "👑왕관 누적 30개 등장", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("crownTotal", 30L) } },
            new AchDef { id = "jackpot1", emoji = "🎰", name = "첫 잭팟", desc = "5칸 잭팟 1회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("jackpots", 1L) } },
            new AchDef { id = "jackpot10", emoji = "🎰", name = "잭팟 헌터", desc = "5칸 잭팟 10회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("jackpots", 10L) } },
            new AchDef { id = "boss1", emoji = "📝", name = "중간고사 통과", desc = "보스 1회 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bossClears", 1L) } },
            new AchDef { id = "boss5", emoji = "🎓", name = "졸업반", desc = "보스 5회 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bossClears", 5L) } },
            new AchDef { id = "stage10", emoji = "🧗", name = "10층 등반", desc = "스테이지 10 도달", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestStage", 10L) } },
            new AchDef { id = "stage15", emoji = "🏔️", name = "최종보스 도달", desc = "스테이지 15 도달", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestStage", 15L) } },
            new AchDef { id = "lastclear5", emoji = "⏰", name = "벼락치기 천재", desc = "마지막 스핀 클리어 5회", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("lastSpinClears", 5L) } },
            new AchDef { id = "exact1", emoji = "🎯", name = "완벽한 계산", desc = "요구 EXP 정확히 일치 클리어", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("exactClears", 1L) } },
            new AchDef { id = "prism5", emoji = "🌈", name = "규칙 파괴자", desc = "프리즘 증강 5회 선택", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("prismPicks", 5L) } },
            new AchDef { id = "score10k", emoji = "💯", name = "만점왕", desc = "최고 점수 10,000", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestScore", 10000L) } },
            new AchDef { id = "score50k", emoji = "🏆", name = "슬롯의 지배자", desc = "최고 점수 50,000", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("bestScore", 50000L) } },
            new AchDef { id = "runs20", emoji = "🔁", name = "단골", desc = "20런 플레이", cat = "기타", tier = "브론즈", reward = "", req = new[] { new StatReq("runs", 20L) } },

            // ══════════════════════════════════════════════════════════════════
            // 확장 466종 — SlotV2AchievementsExt.kt L5-681 (LIST)
            // ══════════════════════════════════════════════════════════════════
            // ── 입문 ────────────────────────────────────────────────
            new AchDef { id = "intro_firstSpin", emoji = "🎰", name = "첫 스핀", desc = "처음으로 슬롯을 돌렸다", cat = "입문", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("totalSpins", 1L) } },
            new AchDef { id = "intro_firstRun", emoji = "🏫", name = "입학식", desc = "첫 런을 시작했다", cat = "입문", tier = "브론즈", reward = "칭호: 신입생", req = new[] { new StatReq("runs", 1L) } },
            new AchDef { id = "intro_firstBoss", emoji = "👹", name = "첫 보스 격파", desc = "보스를 처음 클리어했다", cat = "입문", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("bossClears", 1L) } },
            new AchDef { id = "intro_firstStage5", emoji = "🪜", name = "5층 도달", desc = "5스테이지까지 도달했다", cat = "입문", tier = "브론즈", reward = "칭호: 초보 모험가", req = new[] { new StatReq("bestStage", 5L) } },
            new AchDef { id = "intro_firstShop", emoji = "🛒", name = "첫 장보기", desc = "상점에서 처음 구매했다", cat = "입문", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("shopBuys", 1L) } },
            new AchDef { id = "intro_firstDevice", emoji = "⚙️", name = "장치 입문", desc = "장치를 처음 사용했다", cat = "입문", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("deviceUses", 1L) } },

            // ── 심볼: 체리 🍒 ───────────────────────────────────────
            new AchDef { id = "cherry1000", emoji = "🍒", name = "체리 농장주", desc = "🍒체리 누적 1000개 등장", cat = "심볼", tier = "골드", reward = "칭호: 체리 농장주", req = new[] { new StatReq("cherryTotal", 1000L) } },
            new AchDef { id = "cherry30", emoji = "🍒", name = "체리 새싹", desc = "🍒체리 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cherryTotal", 30L) } },
            new AchDef { id = "cherry300", emoji = "🍒", name = "체리 과수원", desc = "🍒체리 누적 300개 등장", cat = "심볼", tier = "실버", reward = "🎭캐릭터 해금 힌트: 체리농부", req = new[] { new StatReq("cherryTotal", 300L) } },
            new AchDef { id = "cherry3000", emoji = "🍒", name = "체리 제국", desc = "🍒체리 누적 3000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 체리 황제", req = new[] { new StatReq("cherryTotal", 3000L) } },

            // ── 심볼: 책 📖 ─────────────────────────────────────────
            new AchDef { id = "book30", emoji = "📖", name = "책장 정리", desc = "📖책 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("bookTotal", 30L) } },
            new AchDef { id = "book100", emoji = "📖", name = "다독가", desc = "📖책 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 다독가", req = new[] { new StatReq("bookTotal", 100L) } },
            new AchDef { id = "book300", emoji = "📖", name = "서재의 주인", desc = "📖책 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 서재의 주인", req = new[] { new StatReq("bookTotal", 300L) } },
            new AchDef { id = "book500", emoji = "📖", name = "장서가", desc = "📖책 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 장서가", req = new[] { new StatReq("bookTotal", 500L) } },
            new AchDef { id = "book1000", emoji = "📖", name = "도서관장", desc = "📖책 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 도서관장", req = new[] { new StatReq("bookTotal", 1000L) } },

            // ── 심볼: 별 ⭐ ─────────────────────────────────────────
            new AchDef { id = "star30", emoji = "⭐", name = "별 줍기", desc = "⭐별 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("starTotal", 30L) } },
            new AchDef { id = "star100", emoji = "⭐", name = "별 수집가", desc = "⭐별 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 별 수집가", req = new[] { new StatReq("starTotal", 100L) } },
            new AchDef { id = "star300", emoji = "⭐", name = "별자리 화가", desc = "⭐별 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 별자리 화가", req = new[] { new StatReq("starTotal", 300L) } },
            new AchDef { id = "star500", emoji = "⭐", name = "밤하늘의 주인", desc = "⭐별 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 밤하늘의 주인", req = new[] { new StatReq("starTotal", 500L) } },
            new AchDef { id = "star1000", emoji = "⭐", name = "은하 수집가", desc = "⭐별 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 은하 수집가", req = new[] { new StatReq("starTotal", 1000L) } },

            // ── 심볼: 보석 💎 ───────────────────────────────────────
            new AchDef { id = "gem30", emoji = "💎", name = "원석 줍기", desc = "💎보석 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("gemTotal", 30L) } },
            new AchDef { id = "gem100", emoji = "💎", name = "보석 세공사", desc = "💎보석 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 보석 세공사", req = new[] { new StatReq("gemTotal", 100L) } },
            new AchDef { id = "gem300", emoji = "💎", name = "보석상", desc = "💎보석 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 보석상", req = new[] { new StatReq("gemTotal", 300L) } },
            new AchDef { id = "gem500", emoji = "💎", name = "보석 감정사", desc = "💎보석 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 보석 감정사", req = new[] { new StatReq("gemTotal", 500L) } },
            new AchDef { id = "gem1000", emoji = "💎", name = "다이아 광맥", desc = "💎보석 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 다이아 광맥", req = new[] { new StatReq("gemTotal", 1000L) } },

            // ── 심볼: 왕관 👑 ───────────────────────────────────────
            new AchDef { id = "crown30ext", emoji = "👑", name = "왕관 보관소", desc = "👑왕관 누적 30개 등장", cat = "심볼", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("crownTotal", 30L) } },
            new AchDef { id = "crown100", emoji = "👑", name = "대관식", desc = "👑왕관 누적 100개 등장", cat = "심볼", tier = "골드", reward = "칭호: 즉위한 자", req = new[] { new StatReq("crownTotal", 100L) } },
            new AchDef { id = "crown300", emoji = "👑", name = "왕가의 보고", desc = "👑왕관 누적 300개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 왕중왕", req = new[] { new StatReq("crownTotal", 300L) } },

            // ── 심볼: 해골 💀 ───────────────────────────────────────
            new AchDef { id = "skull30", emoji = "💀", name = "해골 친구", desc = "💀해골 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("skullTotal", 30L) } },
            new AchDef { id = "skull100", emoji = "💀", name = "해골 수집가", desc = "💀해골 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 해골 수집가", req = new[] { new StatReq("skullTotal", 100L) } },
            new AchDef { id = "skull300", emoji = "💀", name = "납골당지기", desc = "💀해골 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 납골당지기", req = new[] { new StatReq("skullTotal", 300L) } },
            new AchDef { id = "skull1000", emoji = "💀", name = "죽음의 군주", desc = "💀해골 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 죽음의 군주", req = new[] { new StatReq("skullTotal", 1000L) } },

            // ── 심볼: 코인 🪙 ───────────────────────────────────────
            new AchDef { id = "coin30", emoji = "🪙", name = "동전 줍기", desc = "🪙코인 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("coinTotal", 30L) } },
            new AchDef { id = "coin100", emoji = "🪙", name = "저금통", desc = "🪙코인 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 저금왕", req = new[] { new StatReq("coinTotal", 100L) } },
            new AchDef { id = "coin300", emoji = "🪙", name = "환전상", desc = "🪙코인 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 환전상", req = new[] { new StatReq("coinTotal", 300L) } },
            new AchDef { id = "coin500", emoji = "🪙", name = "금고지기", desc = "🪙코인 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 금고지기", req = new[] { new StatReq("coinTotal", 500L) } },
            new AchDef { id = "coin1000", emoji = "🪙", name = "조폐국장", desc = "🪙코인 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 조폐국장", req = new[] { new StatReq("coinTotal", 1000L) } },

            // ── 명령어 ──────────────────────────────────────────────
            new AchDef { id = "cmd_focus1", emoji = "🎯", name = "집중 입문", desc = "집중 명령을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("focusUses", 1L) } },
            new AchDef { id = "cmd_focus10", emoji = "🎯", name = "집중의 달인", desc = "집중 명령 10회 사용", cat = "명령어", tier = "실버", reward = "칭호: 집중의 달인", req = new[] { new StatReq("focusUses", 10L) } },
            new AchDef { id = "cmd_focus50", emoji = "🎯", name = "무아지경", desc = "집중 명령 50회 사용", cat = "명령어", tier = "골드", reward = "칭호: 무아지경", req = new[] { new StatReq("focusUses", 50L) } },
            new AchDef { id = "cmd_allin1", emoji = "💥", name = "첫 올인", desc = "올인 스핀을 처음 승리", cat = "명령어", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("allinWins", 1L) } },
            new AchDef { id = "cmd_allin5", emoji = "💥", name = "올인 전문가", desc = "올인 스핀 5회 승리", cat = "명령어", tier = "실버", reward = "칭호: 올인 전문가", req = new[] { new StatReq("allinWins", 5L) } },
            new AchDef { id = "cmd_allin20", emoji = "💥", name = "도박의 신", desc = "올인 스핀 20회 승리", cat = "명령어", tier = "골드", reward = "칭호: 도박의 신", req = new[] { new StatReq("allinWins", 20L) } },
            new AchDef { id = "cmd_pray1", emoji = "🙏", name = "첫 기도", desc = "기도 후 스테이지를 클리어", cat = "명령어", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("prayClears", 1L) } },
            new AchDef { id = "cmd_pray5", emoji = "🙏", name = "기적의 증인", desc = "기도 후 클리어 5회", cat = "명령어", tier = "골드", reward = "칭호: 기적의 증인", req = new[] { new StatReq("prayClears", 5L) } },
            new AchDef { id = "cmd_last1", emoji = "⏳", name = "최후의 한 수", desc = "최후 명령을 처음 사용", cat = "명령어", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("lastUses", 1L) } },
            new AchDef { id = "cmd_last10", emoji = "⏳", name = "벼랑 끝의 명수", desc = "최후 명령 10회 사용", cat = "명령어", tier = "골드", reward = "칭호: 벼랑 끝의 명수", req = new[] { new StatReq("lastUses", 10L) } },
            new AchDef { id = "cmd_reroll1", emoji = "🔄", name = "재굴림 입문", desc = "재굴림을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("rerollUses", 1L) } },
            new AchDef { id = "cmd_pin1", emoji = "📌", name = "고정 입문", desc = "고정을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("pinUses", 1L) } },

            // ── 장치 ────────────────────────────────────────────────
            new AchDef { id = "dev_use10", emoji = "⚙️", name = "장치 애호가", desc = "장치를 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 장치 애호가", req = new[] { new StatReq("deviceUses", 10L) } },
            new AchDef { id = "dev_use50", emoji = "⚙️", name = "기계공", desc = "장치를 50회 사용", cat = "장치", tier = "골드", reward = "칭호: 기계공", req = new[] { new StatReq("deviceUses", 50L) } },
            new AchDef { id = "dev_own1", emoji = "🔧", name = "첫 장치 보유", desc = "장치를 1종 영구 보유", cat = "장치", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("devicesOwned", 1L) } },
            new AchDef { id = "dev_own5", emoji = "🔧", name = "장치 수집가", desc = "장치를 5종 영구 보유", cat = "장치", tier = "골드", reward = "칭호: 장치 수집가", req = new[] { new StatReq("devicesOwned", 5L) } },
            new AchDef { id = "dev_own12", emoji = "🔧", name = "장치 마스터", desc = "장치를 12종 모두 보유", cat = "장치", tier = "프리즘", reward = "칭호: 장치 마스터", req = new[] { new StatReq("devicesOwned", 12L) } },
            new AchDef { id = "dev_reroll10", emoji = "🔄", name = "재굴림 중독", desc = "재굴림 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 재굴림 중독", req = new[] { new StatReq("rerollUses", 10L) } },
            new AchDef { id = "dev_pin10", emoji = "📌", name = "고정의 달인", desc = "고정 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 고정의 달인", req = new[] { new StatReq("pinUses", 10L) } },

            // ── 유물 ────────────────────────────────────────────────
            new AchDef { id = "relic3", emoji = "🏺", name = "유물 수집 시작", desc = "한 런에 유물 3개 동시 보유", cat = "유물", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("relicsMax", 3L) } },
            new AchDef { id = "relic5", emoji = "🏺", name = "유물 애호가", desc = "한 런에 유물 5개 동시 보유", cat = "유물", tier = "실버", reward = "칭호: 유물 애호가", req = new[] { new StatReq("relicsMax", 5L) } },
            new AchDef { id = "relic10", emoji = "🏺", name = "유물 수집광", desc = "한 런에 유물 10개 동시 보유", cat = "유물", tier = "골드", reward = "칭호: 유물 수집광", req = new[] { new StatReq("relicsMax", 10L) } },
            new AchDef { id = "prismPick1", emoji = "🔮", name = "첫 프리즘 유물", desc = "프리즘 증강을 처음 선택", cat = "유물", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("prismPicks", 1L) } },
            new AchDef { id = "prismPick20", emoji = "🔮", name = "프리즘 마니아", desc = "프리즘 증강 20회 선택", cat = "유물", tier = "프리즘", reward = "칭호: 프리즘 마니아", req = new[] { new StatReq("prismPicks", 20L) } },

            // ── 아이템 ──────────────────────────────────────────────
            new AchDef { id = "item1", emoji = "🎒", name = "첫 아이템", desc = "아이템을 처음 사용", cat = "아이템", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("itemsUsed", 1L) } },
            new AchDef { id = "item10", emoji = "🎒", name = "아이템 애용가", desc = "아이템 10회 사용", cat = "아이템", tier = "실버", reward = "칭호: 아이템 애용가", req = new[] { new StatReq("itemsUsed", 10L) } },
            new AchDef { id = "item50", emoji = "🎒", name = "만물상 단골", desc = "아이템 50회 사용", cat = "아이템", tier = "골드", reward = "칭호: 만물상 단골", req = new[] { new StatReq("itemsUsed", 50L) } },
            new AchDef { id = "item100", emoji = "🎒", name = "소비의 화신", desc = "아이템 100회 사용", cat = "아이템", tier = "프리즘", reward = "칭호: 소비의 화신", req = new[] { new StatReq("itemsUsed", 100L) } },

            // ── 상점 ────────────────────────────────────────────────
            new AchDef { id = "shop10", emoji = "🛍️", name = "단골 손님", desc = "상점에서 10회 구매", cat = "상점", tier = "실버", reward = "칭호: 단골 손님", req = new[] { new StatReq("shopBuys", 10L) } },
            new AchDef { id = "shop50", emoji = "🛍️", name = "큰손", desc = "상점에서 50회 구매", cat = "상점", tier = "골드", reward = "칭호: 큰손", req = new[] { new StatReq("shopBuys", 50L) } },
            new AchDef { id = "gamble1", emoji = "🎲", name = "첫 도박", desc = "도박장 노드를 처음 이용", cat = "상점", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("gambles", 1L) } },
            new AchDef { id = "gamble10", emoji = "🎲", name = "도박장 VIP", desc = "도박장 노드 10회 이용", cat = "상점", tier = "골드", reward = "칭호: 도박장 VIP", req = new[] { new StatReq("gambles", 10L) } },

            // ── 클리어 ──────────────────────────────────────────────
            new AchDef { id = "boss5ext", emoji = "👹", name = "보스 사냥꾼", desc = "보스 5회 클리어", cat = "클리어", tier = "실버", reward = "칭호: 보스 사냥꾼", req = new[] { new StatReq("bossClears", 5L) } },
            new AchDef { id = "boss20", emoji = "👹", name = "보스 학살자", desc = "보스 20회 클리어", cat = "클리어", tier = "골드", reward = "칭호: 보스 학살자", req = new[] { new StatReq("bossClears", 20L) } },
            new AchDef { id = "stage10ext", emoji = "🪜", name = "10층 등반가", desc = "10스테이지 도달", cat = "클리어", tier = "실버", reward = "칭호: 10층 등반가", req = new[] { new StatReq("bestStage", 10L) } },
            new AchDef { id = "stage15ext", emoji = "🪜", name = "고지 점령", desc = "15스테이지 도달", cat = "클리어", tier = "골드", reward = "칭호: 고지 점령자", req = new[] { new StatReq("bestStage", 15L) } },
            new AchDef { id = "runs10", emoji = "🔁", name = "꾸준한 도전자", desc = "10런 플레이", cat = "클리어", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("runs", 10L) } },
            new AchDef { id = "runs50", emoji = "🔁", name = "베테랑", desc = "50런 플레이", cat = "클리어", tier = "골드", reward = "칭호: 베테랑", req = new[] { new StatReq("runs", 50L) } },

            // ── 아슬아슬 ────────────────────────────────────────────
            new AchDef { id = "close10", emoji = "😅", name = "아슬아슬 통과", desc = "잔여 EXP 10이하로 10회 클리어", cat = "아슬아슬", tier = "실버", reward = "칭호: 아슬아슬", req = new[] { new StatReq("closeClears", 10L) } },
            new AchDef { id = "close30", emoji = "😅", name = "줄타기 곡예사", desc = "잔여 EXP 10이하로 30회 클리어", cat = "아슬아슬", tier = "골드", reward = "칭호: 줄타기 곡예사", req = new[] { new StatReq("closeClears", 30L) } },
            new AchDef { id = "lastspin1", emoji = "🎯", name = "막판 뒤집기", desc = "마지막 스핀에 클리어", cat = "아슬아슬", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("lastSpinClears", 1L) } },
            new AchDef { id = "lastspin5", emoji = "🎯", name = "끝내기의 명수", desc = "마지막 스핀 클리어 5회", cat = "아슬아슬", tier = "골드", reward = "칭호: 끝내기의 명수", req = new[] { new StatReq("lastSpinClears", 5L) } },
            new AchDef { id = "exact1ext", emoji = "🎯", name = "딱 떨어지게", desc = "요구치와 정확히 일치 클리어", cat = "아슬아슬", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("exactClears", 1L) } },

            // ── 점수 ────────────────────────────────────────────────
            new AchDef { id = "score5k", emoji = "📊", name = "점수 입문", desc = "한 런 5000점 달성", cat = "점수", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("bestScore", 5000L) } },
            new AchDef { id = "score10kext", emoji = "📊", name = "만점 클럽", desc = "한 런 10000점 달성", cat = "점수", tier = "실버", reward = "칭호: 만점 클럽", req = new[] { new StatReq("bestScore", 10000L) } },
            new AchDef { id = "score30k", emoji = "📊", name = "고득점자", desc = "한 런 30000점 달성", cat = "점수", tier = "골드", reward = "칭호: 고득점자", req = new[] { new StatReq("bestScore", 30000L) } },
            new AchDef { id = "score50kext", emoji = "📊", name = "점수 사냥꾼", desc = "한 런 50000점 달성", cat = "점수", tier = "골드", reward = "칭호: 점수 사냥꾼", req = new[] { new StatReq("bestScore", 50000L) } },

            // ── 저주 ────────────────────────────────────────────────
            new AchDef { id = "curse1", emoji = "🩸", name = "첫 저주", desc = "한 런에 저주 1개 보유", cat = "저주", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("curseMax", 1L) } },
            new AchDef { id = "curse3", emoji = "🩸", name = "저주받은 자", desc = "한 런에 저주 3개 동시 보유", cat = "저주", tier = "실버", reward = "칭호: 저주받은 자", req = new[] { new StatReq("curseMax", 3L) } },
            new AchDef { id = "curse5", emoji = "🩸", name = "저주 수집가", desc = "한 런에 저주 5개 동시 보유", cat = "저주", tier = "골드", reward = "칭호: 저주 수집가", req = new[] { new StatReq("curseMax", 5L) } },
            new AchDef { id = "curse7", emoji = "🩸", name = "저주의 그릇", desc = "한 런에 저주 7개 동시 보유", cat = "저주", tier = "프리즘", reward = "칭호: 저주의 그릇", req = new[] { new StatReq("curseMax", 7L) } },

            // ── 도전 ────────────────────────────────────────────────
            new AchDef { id = "chal_stage20", emoji = "🏔️", name = "정상 정복", desc = "20스테이지 도달", cat = "도전", tier = "프리즘", reward = "칭호: 정상 정복자", req = new[] { new StatReq("bestStage", 20L) } },
            new AchDef { id = "chal_boss50", emoji = "💀", name = "백전노장", desc = "보스 50회 클리어", cat = "도전", tier = "프리즘", reward = "칭호: 백전노장", req = new[] { new StatReq("bossClears", 50L) } },
            new AchDef { id = "chal_curse7", emoji = "☠️", name = "저주의 화신", desc = "한 런에 저주 7개 동시 보유", cat = "도전", tier = "프리즘", reward = "칭호: 저주의 화신", req = new[] { new StatReq("curseMax", 7L) } },
            new AchDef { id = "chal_score100k", emoji = "🏆", name = "10만점 돌파", desc = "한 런 100000점 달성", cat = "도전", tier = "프리즘", reward = "칭호: 10만점의 전설", req = new[] { new StatReq("bestScore", 100000L) } },

            // ── 반복 ────────────────────────────────────────────────
            new AchDef { id = "spin100", emoji = "🔂", name = "백 번의 스핀", desc = "누적 100회 스핀", cat = "반복", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("totalSpins", 100L) } },
            new AchDef { id = "spin500", emoji = "🔂", name = "오백 번의 스핀", desc = "누적 500회 스핀", cat = "반복", tier = "실버", reward = "칭호: 손가락 운동", req = new[] { new StatReq("totalSpins", 500L) } },
            new AchDef { id = "spin1000", emoji = "🔂", name = "천 번의 스핀", desc = "누적 1000회 스핀", cat = "반복", tier = "골드", reward = "칭호: 스핀 중독", req = new[] { new StatReq("totalSpins", 1000L) } },
            new AchDef { id = "spin5000", emoji = "🔂", name = "오천 번의 스핀", desc = "누적 5000회 스핀", cat = "반복", tier = "프리즘", reward = "칭호: 스핀의 화신", req = new[] { new StatReq("totalSpins", 5000L) } },
            new AchDef { id = "runs100", emoji = "🔁", name = "백 번의 도전", desc = "100런 플레이", cat = "반복", tier = "프리즘", reward = "칭호: 슬롯의 산증인", req = new[] { new StatReq("runs", 100L) } },

            // ── 히든 ────────────────────────────────────────────────
            new AchDef { id = "hid_exact5", emoji = "🎯", name = "딱 맞췄다", desc = "요구치와 정확히 일치 클리어 5회", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 정밀 저격수", req = new[] { new StatReq("exactClears", 5L) } },
            new AchDef { id = "hid_close1", emoji = "💔", name = "심장 파괴자", desc = "잔여 EXP 10이하로 클리어", cat = "히든", tier = "실버", hidden = true, reward = "칭호: 심장 파괴자", req = new[] { new StatReq("closeClears", 1L) } },
            new AchDef { id = "hid_lastspin20", emoji = "🃏", name = "운명의 마지막", desc = "마지막 스핀 클리어 20회", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 운명의 카드", req = new[] { new StatReq("lastSpinClears", 20L) } },
            new AchDef { id = "hid_jackpot1", emoji = "🎉", name = "첫 잭팟", desc = "5개 일치 잭팟 달성", cat = "히든", tier = "골드", hidden = true, reward = "도감 등록", req = new[] { new StatReq("jackpots", 1L) } },
            new AchDef { id = "hid_jackpot5", emoji = "🎉", name = "잭팟 단골", desc = "잭팟 5회 달성", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 잭팟 메이커", req = new[] { new StatReq("jackpots", 5L) } },
            new AchDef { id = "hid_jackpot20", emoji = "🎰", name = "잭팟런의 주인", desc = "잭팟 20회 달성", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 잭팟런의 주인", req = new[] { new StatReq("jackpots", 20L) } },
            new AchDef { id = "hid_curse7", emoji = "🎓", name = "검은 졸업식", desc = "저주 7개를 안고 살아남았다", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 검은 졸업생", req = new[] { new StatReq("curseMax", 7L) } },
            new AchDef { id = "hid_score100k", emoji = "👾", name = "졸업식의 괴물", desc = "한 런 100000점의 괴물", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 졸업식의 괴물", req = new[] { new StatReq("bestScore", 100000L) } },
            new AchDef { id = "hid_set4_1", emoji = "🍀", name = "행운의 네 잎", desc = "같은 심볼 4개 이상 등장", cat = "히든", tier = "실버", hidden = true, reward = "도감 등록", req = new[] { new StatReq("set4Plus", 1L) } },
            new AchDef { id = "hid_set4_50", emoji = "🍀", name = "사천왕", desc = "같은 심볼 4개 이상 50회", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 사천왕", req = new[] { new StatReq("set4Plus", 50L) } },
            new AchDef { id = "hid_allin20", emoji = "🔥", name = "불꽃의 도박사", desc = "올인 20회 승리의 광기", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 불꽃의 도박사", req = new[] { new StatReq("allinWins", 20L) } },
            new AchDef { id = "hid_skull1000", emoji = "⚰️", name = "사신의 친구", desc = "💀해골과 1000번 마주쳤다", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 사신의 친구", req = new[] { new StatReq("skullTotal", 1000L) } },
            new AchDef { id = "hid_prismPick5", emoji = "🌈", name = "무지개를 쫓는 자", desc = "프리즘 증강 5회 선택", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 무지개를 쫓는 자", req = new[] { new StatReq("prismPicks", 5L) } },
            new AchDef { id = "hid_pray5", emoji = "✨", name = "신앙의 결실", desc = "기도가 모두 응답받았다", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 신앙의 결실", req = new[] { new StatReq("prayClears", 5L) } },

            // ── 캐릭터 숙련 (cstage_<charId> 최고스테이지 S5/10/15/20 = 브론즈/실버/골드/프리즘) ──
            // 🎒 초보학생 (novice)
            new AchDef { id = "cmast_novice_b", emoji = "🎒", name = "첫 등교", desc = "초보학생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_novice", 5L) } },
            new AchDef { id = "cmast_novice_s", emoji = "🎒", name = "성실한 신입", desc = "초보학생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 성실한 신입", req = new[] { new StatReq("cstage_novice", 10L) } },
            new AchDef { id = "cmast_novice_g", emoji = "🎒", name = "모범 신입생", desc = "초보학생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 신입생의 가방", req = new[] { new StatReq("cstage_novice", 15L) } },
            new AchDef { id = "cmast_novice_p", emoji = "🎒", name = "초심의 전설", desc = "초보학생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 초심을 잃지 않은 자", req = new[] { new StatReq("cstage_novice", 20L) } },

            // 📗 장학생 (scholar)
            new AchDef { id = "cmast_scholar_b", emoji = "📗", name = "장학 입문", desc = "장학생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_scholar", 5L) } },
            new AchDef { id = "cmast_scholar_s", emoji = "📗", name = "우등 장학생", desc = "장학생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 우등 장학생", req = new[] { new StatReq("cstage_scholar", 10L) } },
            new AchDef { id = "cmast_scholar_g", emoji = "📗", name = "전액 장학생", desc = "장학생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 장학증서", req = new[] { new StatReq("cstage_scholar", 15L) } },
            new AchDef { id = "cmast_scholar_p", emoji = "📗", name = "장학의 전설", desc = "장학생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 학문의 정점", req = new[] { new StatReq("cstage_scholar", 20L) } },

            // 🎲 도박꾼 (gambler)
            new AchDef { id = "cmast_gambler_b", emoji = "🎲", name = "도박 입문", desc = "도박꾼(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_gambler", 5L) } },
            new AchDef { id = "cmast_gambler_s", emoji = "🎲", name = "도박 숙련", desc = "도박꾼(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 노련한 도박꾼", req = new[] { new StatReq("cstage_gambler", 10L) } },
            new AchDef { id = "cmast_gambler_g", emoji = "🎲", name = "도박 졸업", desc = "도박꾼(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 황금 주사위", req = new[] { new StatReq("cstage_gambler", 15L) } },
            new AchDef { id = "cmast_gambler_p", emoji = "🎲", name = "도박의 전설", desc = "도박꾼(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 운명을 건 자", req = new[] { new StatReq("cstage_gambler", 20L) } },

            // 🍒 체리농부 (farmer)
            new AchDef { id = "cmast_farmer_b", emoji = "🍒", name = "텃밭 가꾸기", desc = "체리농부(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_farmer", 5L) } },
            new AchDef { id = "cmast_farmer_s", emoji = "🍒", name = "능숙한 농부", desc = "체리농부(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 능숙한 농부", req = new[] { new StatReq("cstage_farmer", 10L) } },
            new AchDef { id = "cmast_farmer_g", emoji = "🍒", name = "대농장주", desc = "체리농부(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 풍년의 화환", req = new[] { new StatReq("cstage_farmer", 15L) } },
            new AchDef { id = "cmast_farmer_p", emoji = "🍒", name = "체리의 전설", desc = "체리농부(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 풍요의 수호자", req = new[] { new StatReq("cstage_farmer", 20L) } },

            // 🪙 알바생 (parttime)
            new AchDef { id = "cmast_parttime_b", emoji = "🪙", name = "첫 출근", desc = "알바생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_parttime", 5L) } },
            new AchDef { id = "cmast_parttime_s", emoji = "🪙", name = "성실한 알바", desc = "알바생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 성실한 알바", req = new[] { new StatReq("cstage_parttime", 10L) } },
            new AchDef { id = "cmast_parttime_g", emoji = "🪙", name = "에이스 직원", desc = "알바생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 우수사원 명패", req = new[] { new StatReq("cstage_parttime", 15L) } },
            new AchDef { id = "cmast_parttime_p", emoji = "🪙", name = "알바의 전설", desc = "알바생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 시급의 제왕", req = new[] { new StatReq("cstage_parttime", 20L) } },

            // 💎 보석상 (jeweler)
            new AchDef { id = "cmast_jeweler_b", emoji = "💎", name = "세공 입문", desc = "보석상(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_jeweler", 5L) } },
            new AchDef { id = "cmast_jeweler_s", emoji = "💎", name = "숙련 세공사", desc = "보석상(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 숙련 세공사", req = new[] { new StatReq("cstage_jeweler", 10L) } },
            new AchDef { id = "cmast_jeweler_g", emoji = "💎", name = "보석 명장", desc = "보석상(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 보석 진열장", req = new[] { new StatReq("cstage_jeweler", 15L) } },
            new AchDef { id = "cmast_jeweler_p", emoji = "💎", name = "보석의 전설", desc = "보석상(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 원석의 지배자", req = new[] { new StatReq("cstage_jeweler", 20L) } },

            // 🎓 수석졸업생 (honor)
            new AchDef { id = "cmast_honor_b", emoji = "🎓", name = "우등 입문", desc = "수석졸업생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_honor", 5L) } },
            new AchDef { id = "cmast_honor_s", emoji = "🎓", name = "학년 수석", desc = "수석졸업생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 학년 수석", req = new[] { new StatReq("cstage_honor", 10L) } },
            new AchDef { id = "cmast_honor_g", emoji = "🎓", name = "전체 수석", desc = "수석졸업생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 수석 졸업장", req = new[] { new StatReq("cstage_honor", 15L) } },
            new AchDef { id = "cmast_honor_p", emoji = "🎓", name = "수석의 전설", desc = "수석졸업생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 졸업식의 주인공", req = new[] { new StatReq("cstage_honor", 20L) } },

            // 💀 해골숭배자 (cultist)
            new AchDef { id = "cmast_cultist_b", emoji = "💀", name = "입교 의식", desc = "해골숭배자(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_cultist", 5L) } },
            new AchDef { id = "cmast_cultist_s", emoji = "💀", name = "충실한 신도", desc = "해골숭배자(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 충실한 신도", req = new[] { new StatReq("cstage_cultist", 10L) } },
            new AchDef { id = "cmast_cultist_g", emoji = "💀", name = "교단의 사제", desc = "해골숭배자(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 해골 제단", req = new[] { new StatReq("cstage_cultist", 15L) } },
            new AchDef { id = "cmast_cultist_p", emoji = "💀", name = "숭배의 전설", desc = "해골숭배자(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 죽음을 섬기는 자", req = new[] { new StatReq("cstage_cultist", 20L) } },

            // 👑 왕관수집가 (crowncol)
            new AchDef { id = "cmast_crowncol_b", emoji = "👑", name = "수집 입문", desc = "왕관수집가(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_crowncol", 5L) } },
            new AchDef { id = "cmast_crowncol_s", emoji = "👑", name = "왕관 애호가", desc = "왕관수집가(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 왕관 애호가", req = new[] { new StatReq("cstage_crowncol", 10L) } },
            new AchDef { id = "cmast_crowncol_g", emoji = "👑", name = "왕관 명인", desc = "왕관수집가(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 왕관 진열대", req = new[] { new StatReq("cstage_crowncol", 15L) } },
            new AchDef { id = "cmast_crowncol_p", emoji = "👑", name = "수집의 전설", desc = "왕관수집가(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 왕관의 지배자", req = new[] { new StatReq("cstage_crowncol", 20L) } },

            // 🍃 미니멀리스트 (minimalist)
            new AchDef { id = "cmast_minimalist_b", emoji = "🍃", name = "비움 입문", desc = "미니멀리스트(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_minimalist", 5L) } },
            new AchDef { id = "cmast_minimalist_s", emoji = "🍃", name = "절제의 달인", desc = "미니멀리스트(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 절제의 달인", req = new[] { new StatReq("cstage_minimalist", 10L) } },
            new AchDef { id = "cmast_minimalist_g", emoji = "🍃", name = "비움의 미학", desc = "미니멀리스트(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 단순함의 잎새", req = new[] { new StatReq("cstage_minimalist", 15L) } },
            new AchDef { id = "cmast_minimalist_p", emoji = "🍃", name = "비움의 전설", desc = "미니멀리스트(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 무소유의 현자", req = new[] { new StatReq("cstage_minimalist", 20L) } },

            // 🍀 행운아 (lucky)
            new AchDef { id = "cmast_lucky_b", emoji = "🍀", name = "행운 입문", desc = "행운아(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_lucky", 5L) } },
            new AchDef { id = "cmast_lucky_s", emoji = "🍀", name = "운수 좋은 날", desc = "행운아(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 운수 좋은 자", req = new[] { new StatReq("cstage_lucky", 10L) } },
            new AchDef { id = "cmast_lucky_g", emoji = "🍀", name = "행운의 화신", desc = "행운아(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 네 잎 클로버", req = new[] { new StatReq("cstage_lucky", 15L) } },
            new AchDef { id = "cmast_lucky_p", emoji = "🍀", name = "행운의 전설", desc = "행운아(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 행운의 여신이 택한 자", req = new[] { new StatReq("cstage_lucky", 20L) } },

            // 💠 큰손 (highroller)
            new AchDef { id = "cmast_highroller_b", emoji = "💠", name = "거래 입문", desc = "큰손(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_highroller", 5L) } },
            new AchDef { id = "cmast_highroller_s", emoji = "💠", name = "큰 거래상", desc = "큰손(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 큰 거래상", req = new[] { new StatReq("cstage_highroller", 10L) } },
            new AchDef { id = "cmast_highroller_g", emoji = "💠", name = "VIP 큰손", desc = "큰손(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: VIP 카드", req = new[] { new StatReq("cstage_highroller", 15L) } },
            new AchDef { id = "cmast_highroller_p", emoji = "💠", name = "큰손의 전설", desc = "큰손(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 판을 흔드는 큰손", req = new[] { new StatReq("cstage_highroller", 20L) } },

            // 🧘 수도승 (monk)
            new AchDef { id = "cmast_monk_b", emoji = "🧘", name = "수행 입문", desc = "수도승(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_monk", 5L) } },
            new AchDef { id = "cmast_monk_s", emoji = "🧘", name = "정진하는 자", desc = "수도승(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 정진하는 자", req = new[] { new StatReq("cstage_monk", 10L) } },
            new AchDef { id = "cmast_monk_g", emoji = "🧘", name = "해탈의 경지", desc = "수도승(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 깨달음의 후광", req = new[] { new StatReq("cstage_monk", 15L) } },
            new AchDef { id = "cmast_monk_p", emoji = "🧘", name = "수행의 전설", desc = "수도승(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 무념의 대선사", req = new[] { new StatReq("cstage_monk", 20L) } },

            // ⚗️ 연금술사 (alchemist)
            new AchDef { id = "cmast_alchemist_b", emoji = "⚗️", name = "조합 입문", desc = "연금술사(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_alchemist", 5L) } },
            new AchDef { id = "cmast_alchemist_s", emoji = "⚗️", name = "능숙한 연금술", desc = "연금술사(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 능숙한 연금술사", req = new[] { new StatReq("cstage_alchemist", 10L) } },
            new AchDef { id = "cmast_alchemist_g", emoji = "⚗️", name = "현자의 돌", desc = "연금술사(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 현자의 돌", req = new[] { new StatReq("cstage_alchemist", 15L) } },
            new AchDef { id = "cmast_alchemist_p", emoji = "⚗️", name = "연금의 전설", desc = "연금술사(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 만물의 변환자", req = new[] { new StatReq("cstage_alchemist", 20L) } },

            // 😈 무모한도전 (daredevil)
            new AchDef { id = "cmast_daredevil_b", emoji = "😈", name = "도전 입문", desc = "무모한도전(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_daredevil", 5L) } },
            new AchDef { id = "cmast_daredevil_s", emoji = "😈", name = "겁 없는 자", desc = "무모한도전(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 겁 없는 자", req = new[] { new StatReq("cstage_daredevil", 10L) } },
            new AchDef { id = "cmast_daredevil_g", emoji = "😈", name = "광기의 질주", desc = "무모한도전(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 불타는 뿔", req = new[] { new StatReq("cstage_daredevil", 15L) } },
            new AchDef { id = "cmast_daredevil_p", emoji = "😈", name = "무모함의 전설", desc = "무모한도전(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 한계를 비웃는 자", req = new[] { new StatReq("cstage_daredevil", 20L) } },

            // 🌟 천재 (prodigy)
            new AchDef { id = "cmast_prodigy_b", emoji = "🌟", name = "재능 발현", desc = "천재(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("cstage_prodigy", 5L) } },
            new AchDef { id = "cmast_prodigy_s", emoji = "🌟", name = "빛나는 영재", desc = "천재(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 빛나는 영재", req = new[] { new StatReq("cstage_prodigy", 10L) } },
            new AchDef { id = "cmast_prodigy_g", emoji = "🌟", name = "비범한 천재", desc = "천재(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 천재의 별빛", req = new[] { new StatReq("cstage_prodigy", 15L) } },
            new AchDef { id = "cmast_prodigy_p", emoji = "🌟", name = "천재의 전설", desc = "천재(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 시대를 앞선 천재", req = new[] { new StatReq("cstage_prodigy", 20L) } },

            // ── 머신 숙련 (mstage_<machineId> 최고스테이지 S5/10/15/20 = 브론즈/실버/골드/프리즘) ──
            // 🎰 기본 (basic)
            new AchDef { id = "mmast_basic_b", emoji = "🎰", name = "기본기 다지기", desc = "기본 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_basic", 5L) } },
            new AchDef { id = "mmast_basic_s", emoji = "🎰", name = "정석 플레이어", desc = "기본 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 정석 플레이어", req = new[] { new StatReq("mstage_basic", 10L) } },
            new AchDef { id = "mmast_basic_g", emoji = "🎰", name = "표준의 달인", desc = "기본 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 클래식 슬롯", req = new[] { new StatReq("mstage_basic", 15L) } },
            new AchDef { id = "mmast_basic_p", emoji = "🎰", name = "기본의 전설", desc = "기본 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 정석의 화신", req = new[] { new StatReq("mstage_basic", 20L) } },

            // 🍒 체리 (cherry)
            new AchDef { id = "mmast_cherry_b", emoji = "🍒", name = "체리 머신 입문", desc = "체리 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_cherry", 5L) } },
            new AchDef { id = "mmast_cherry_s", emoji = "🍒", name = "체리 머신 숙련", desc = "체리 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 체리 머신 숙련가", req = new[] { new StatReq("mstage_cherry", 10L) } },
            new AchDef { id = "mmast_cherry_g", emoji = "🍒", name = "체리 머신 명인", desc = "체리 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 체리 릴", req = new[] { new StatReq("mstage_cherry", 15L) } },
            new AchDef { id = "mmast_cherry_p", emoji = "🍒", name = "체리 머신의 전설", desc = "체리 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 체리 릴의 지배자", req = new[] { new StatReq("mstage_cherry", 20L) } },

            // 📚 도서관 (library)
            new AchDef { id = "mmast_library_b", emoji = "📚", name = "도서관 입문", desc = "도서관 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_library", 5L) } },
            new AchDef { id = "mmast_library_s", emoji = "📚", name = "도서관 숙련", desc = "도서관 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 도서관 단골", req = new[] { new StatReq("mstage_library", 10L) } },
            new AchDef { id = "mmast_library_g", emoji = "📚", name = "도서관 명인", desc = "도서관 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 지혜의 서가", req = new[] { new StatReq("mstage_library", 15L) } },
            new AchDef { id = "mmast_library_p", emoji = "📚", name = "도서관의 전설", desc = "도서관 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 지식의 수호자", req = new[] { new StatReq("mstage_library", 20L) } },

            // 💎 보석 (gem)
            new AchDef { id = "mmast_gem_b", emoji = "💎", name = "보석 머신 입문", desc = "보석 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_gem", 5L) } },
            new AchDef { id = "mmast_gem_s", emoji = "💎", name = "보석 머신 숙련", desc = "보석 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 보석 머신 숙련가", req = new[] { new StatReq("mstage_gem", 10L) } },
            new AchDef { id = "mmast_gem_g", emoji = "💎", name = "보석 머신 명인", desc = "보석 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 보석 릴", req = new[] { new StatReq("mstage_gem", 15L) } },
            new AchDef { id = "mmast_gem_p", emoji = "💎", name = "보석 머신의 전설", desc = "보석 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 광채의 지배자", req = new[] { new StatReq("mstage_gem", 20L) } },

            // 🧲 자석 (magnet)
            new AchDef { id = "mmast_magnet_b", emoji = "🧲", name = "자석 머신 입문", desc = "자석 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_magnet", 5L) } },
            new AchDef { id = "mmast_magnet_s", emoji = "🧲", name = "자석 머신 숙련", desc = "자석 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 콤보 장인", req = new[] { new StatReq("mstage_magnet", 10L) } },
            new AchDef { id = "mmast_magnet_g", emoji = "🧲", name = "자석 머신 명인", desc = "자석 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 자기장 릴", req = new[] { new StatReq("mstage_magnet", 15L) } },
            new AchDef { id = "mmast_magnet_p", emoji = "🧲", name = "자석 머신의 전설", desc = "자석 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 인력의 지배자", req = new[] { new StatReq("mstage_magnet", 20L) } },

            // ☠ 해골 (skull)
            new AchDef { id = "mmast_skull_b", emoji = "☠", name = "해골 머신 입문", desc = "해골 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_skull", 5L) } },
            new AchDef { id = "mmast_skull_s", emoji = "☠", name = "해골 머신 숙련", desc = "해골 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 위험의 동반자", req = new[] { new StatReq("mstage_skull", 10L) } },
            new AchDef { id = "mmast_skull_g", emoji = "☠", name = "해골 머신 명인", desc = "해골 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 해골 릴", req = new[] { new StatReq("mstage_skull", 15L) } },
            new AchDef { id = "mmast_skull_p", emoji = "☠", name = "해골 머신의 전설", desc = "해골 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 사신의 도박판", req = new[] { new StatReq("mstage_skull", 20L) } },

            // 👑 왕관 (crown)
            new AchDef { id = "mmast_crown_b", emoji = "👑", name = "왕관 머신 입문", desc = "왕관 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_crown", 5L) } },
            new AchDef { id = "mmast_crown_s", emoji = "👑", name = "왕관 머신 숙련", desc = "왕관 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 운빨의 귀족", req = new[] { new StatReq("mstage_crown", 10L) } },
            new AchDef { id = "mmast_crown_g", emoji = "👑", name = "왕관 머신 명인", desc = "왕관 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 왕관 릴", req = new[] { new StatReq("mstage_crown", 15L) } },
            new AchDef { id = "mmast_crown_p", emoji = "👑", name = "왕관 머신의 전설", desc = "왕관 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 운명을 거머쥔 왕", req = new[] { new StatReq("mstage_crown", 20L) } },

            // 🔥 불꽃 (flame)
            new AchDef { id = "mmast_flame_b", emoji = "🔥", name = "불꽃 머신 입문", desc = "불꽃 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_flame", 5L) } },
            new AchDef { id = "mmast_flame_s", emoji = "🔥", name = "불꽃 머신 숙련", desc = "불꽃 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 배율의 연소자", req = new[] { new StatReq("mstage_flame", 10L) } },
            new AchDef { id = "mmast_flame_g", emoji = "🔥", name = "불꽃 머신 명인", desc = "불꽃 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 화염 릴", req = new[] { new StatReq("mstage_flame", 15L) } },
            new AchDef { id = "mmast_flame_p", emoji = "🔥", name = "불꽃 머신의 전설", desc = "불꽃 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 불꽃의 지배자", req = new[] { new StatReq("mstage_flame", 20L) } },

            // 💣 폭탄 (bomb)
            new AchDef { id = "mmast_bomb_b", emoji = "💣", name = "폭탄 머신 입문", desc = "폭탄 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_bomb", 5L) } },
            new AchDef { id = "mmast_bomb_s", emoji = "💣", name = "폭탄 머신 숙련", desc = "폭탄 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 폭파 전문가", req = new[] { new StatReq("mstage_bomb", 10L) } },
            new AchDef { id = "mmast_bomb_g", emoji = "💣", name = "폭탄 머신 명인", desc = "폭탄 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 폭탄 릴", req = new[] { new StatReq("mstage_bomb", 15L) } },
            new AchDef { id = "mmast_bomb_p", emoji = "💣", name = "폭탄 머신의 전설", desc = "폭탄 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 파괴의 지배자", req = new[] { new StatReq("mstage_bomb", 20L) } },

            // ⭐ 별빛 (star)
            new AchDef { id = "mmast_star_b", emoji = "⭐", name = "별빛 머신 입문", desc = "별빛 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_star", 5L) } },
            new AchDef { id = "mmast_star_s", emoji = "⭐", name = "별빛 머신 숙련", desc = "별빛 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 별빛 항해사", req = new[] { new StatReq("mstage_star", 10L) } },
            new AchDef { id = "mmast_star_g", emoji = "⭐", name = "별빛 머신 명인", desc = "별빛 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 별빛 릴", req = new[] { new StatReq("mstage_star", 15L) } },
            new AchDef { id = "mmast_star_p", emoji = "⭐", name = "별빛 머신의 전설", desc = "별빛 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 별자리의 지배자", req = new[] { new StatReq("mstage_star", 20L) } },

            // 🍀 행운 (clover)
            new AchDef { id = "mmast_clover_b", emoji = "🍀", name = "행운 머신 입문", desc = "행운 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_clover", 5L) } },
            new AchDef { id = "mmast_clover_s", emoji = "🍀", name = "행운 머신 숙련", desc = "행운 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 행운 머신 숙련가", req = new[] { new StatReq("mstage_clover", 10L) } },
            new AchDef { id = "mmast_clover_g", emoji = "🍀", name = "행운 머신 명인", desc = "행운 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 클로버 릴", req = new[] { new StatReq("mstage_clover", 15L) } },
            new AchDef { id = "mmast_clover_p", emoji = "🍀", name = "행운 머신의 전설", desc = "행운 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 행운의 지배자", req = new[] { new StatReq("mstage_clover", 20L) } },

            // 🎲 카지노 (casino)
            new AchDef { id = "mmast_casino_b", emoji = "🎲", name = "카지노 입문", desc = "카지노 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_casino", 5L) } },
            new AchDef { id = "mmast_casino_s", emoji = "🎲", name = "카지노 숙련", desc = "카지노 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 고변동의 베터", req = new[] { new StatReq("mstage_casino", 10L) } },
            new AchDef { id = "mmast_casino_g", emoji = "🎲", name = "카지노 명인", desc = "카지노 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 주사위 릴", req = new[] { new StatReq("mstage_casino", 15L) } },
            new AchDef { id = "mmast_casino_p", emoji = "🎲", name = "카지노의 전설", desc = "카지노 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 운빨의 제왕", req = new[] { new StatReq("mstage_casino", 20L) } },

            // 🌱 정원 (garden)
            new AchDef { id = "mmast_garden_b", emoji = "🌱", name = "정원 머신 입문", desc = "정원 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_garden", 5L) } },
            new AchDef { id = "mmast_garden_s", emoji = "🌱", name = "정원 머신 숙련", desc = "정원 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 성장의 정원사", req = new[] { new StatReq("mstage_garden", 10L) } },
            new AchDef { id = "mmast_garden_g", emoji = "🌱", name = "정원 머신 명인", desc = "정원 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 새싹 릴", req = new[] { new StatReq("mstage_garden", 15L) } },
            new AchDef { id = "mmast_garden_p", emoji = "🌱", name = "정원 머신의 전설", desc = "정원 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 생명의 정원사", req = new[] { new StatReq("mstage_garden", 20L) } },

            // 🌀 와일드 (wildmac)
            new AchDef { id = "mmast_wildmac_b", emoji = "🌀", name = "와일드 입문", desc = "와일드 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_wildmac", 5L) } },
            new AchDef { id = "mmast_wildmac_s", emoji = "🌀", name = "와일드 숙련", desc = "와일드 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 세트 조작가", req = new[] { new StatReq("mstage_wildmac", 10L) } },
            new AchDef { id = "mmast_wildmac_g", emoji = "🌀", name = "와일드 명인", desc = "와일드 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 와일드 릴", req = new[] { new StatReq("mstage_wildmac", 15L) } },
            new AchDef { id = "mmast_wildmac_p", emoji = "🌀", name = "와일드의 전설", desc = "와일드 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 혼돈의 지배자", req = new[] { new StatReq("mstage_wildmac", 20L) } },

            // 🗝 금고 (vault)
            new AchDef { id = "mmast_vault_b", emoji = "🗝", name = "금고 머신 입문", desc = "금고 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_vault", 5L) } },
            new AchDef { id = "mmast_vault_s", emoji = "🗝", name = "금고 머신 숙련", desc = "금고 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 금고털이", req = new[] { new StatReq("mstage_vault", 10L) } },
            new AchDef { id = "mmast_vault_g", emoji = "🗝", name = "금고 머신 명인", desc = "금고 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 황금 열쇠", req = new[] { new StatReq("mstage_vault", 15L) } },
            new AchDef { id = "mmast_vault_p", emoji = "🗝", name = "금고 머신의 전설", desc = "금고 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 보고의 지배자", req = new[] { new StatReq("mstage_vault", 20L) } },

            // 🌈 무지개 (rainbow)
            new AchDef { id = "mmast_rainbow_b", emoji = "🌈", name = "무지개 입문", desc = "무지개 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("mstage_rainbow", 5L) } },
            new AchDef { id = "mmast_rainbow_s", emoji = "🌈", name = "무지개 숙련", desc = "무지개 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 한방의 추격자", req = new[] { new StatReq("mstage_rainbow", 10L) } },
            new AchDef { id = "mmast_rainbow_g", emoji = "🌈", name = "무지개 명인", desc = "무지개 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 무지개 릴", req = new[] { new StatReq("mstage_rainbow", 15L) } },
            new AchDef { id = "mmast_rainbow_p", emoji = "🌈", name = "무지개의 전설", desc = "무지개 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 일곱 빛깔의 지배자", req = new[] { new StatReq("mstage_rainbow", 20L) } },

            // ══════════════════════════════════════════════════════════
            // 2차 확장 — 기존 추적 카운터 전용(신규 추적코드 0). 중복 임계 없음.
            // ══════════════════════════════════════════════════════════
            // ── 역전 (lastSpinClears 10/30, closeClears 5/50) ─────────
            new AchDef { id = "rv_last10", emoji = "🎯", name = "역전의 명수", desc = "마지막 스핀 클리어 10회", cat = "역전", tier = "골드", reward = "칭호: 역전의 명수", req = new[] { new StatReq("lastSpinClears", 10L) } },
            new AchDef { id = "rv_last30", emoji = "🎯", name = "막판 승부사", desc = "마지막 스핀 클리어 30회", cat = "역전", tier = "프리즘", reward = "고급 칭호: 운명의 한 스핀", req = new[] { new StatReq("lastSpinClears", 30L) } },
            new AchDef { id = "rv_close5", emoji = "😰", name = "간발의 차", desc = "잔여 EXP 10이하로 5회 클리어", cat = "역전", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("closeClears", 5L) } },
            new AchDef { id = "rv_close50", emoji = "😰", name = "벼랑 끝 곡예", desc = "잔여 EXP 10이하로 50회 클리어", cat = "역전", tier = "프리즘", reward = "고급 칭호: 외줄타기의 달인", req = new[] { new StatReq("closeClears", 50L) } },

            // ── 정밀 (exactClears 10/20, set4Plus 5/20) ───────────────
            new AchDef { id = "pc_exact10", emoji = "📐", name = "정밀 사격수", desc = "요구치 정확 일치 클리어 10회", cat = "정밀", tier = "골드", reward = "칭호: 정밀 사격수", req = new[] { new StatReq("exactClears", 10L) } },
            new AchDef { id = "pc_exact20", emoji = "📐", name = "0의 미학", desc = "요구치 정확 일치 클리어 20회", cat = "정밀", tier = "프리즘", reward = "고급 칭호: 빈틈없는 계산가", req = new[] { new StatReq("exactClears", 20L) } },
            new AchDef { id = "pc_set4_5", emoji = "🍀", name = "네 잎의 행운", desc = "같은 심볼 4개 이상 5회", cat = "정밀", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("set4Plus", 5L) } },
            new AchDef { id = "pc_set4_20", emoji = "🍀", name = "정렬의 달인", desc = "같은 심볼 4개 이상 20회", cat = "정밀", tier = "골드", reward = "칭호: 정렬의 달인", req = new[] { new StatReq("set4Plus", 20L) } },

            // ── 고점 (bestScore 20k/70k, bestStage 25, maxOverPct 120/150/200/300/500) ──
            new AchDef { id = "ov_score20k", emoji = "📈", name = "이만점 클럽", desc = "한 런 20000점 달성", cat = "고점", tier = "실버", reward = "칭호: 이만점 클럽", req = new[] { new StatReq("bestScore", 20000L) } },
            new AchDef { id = "ov_score70k", emoji = "📈", name = "칠만의 벽", desc = "한 런 70000점 달성", cat = "고점", tier = "골드", reward = "칭호: 칠만의 벽을 넘은 자", req = new[] { new StatReq("bestScore", 70000L) } },
            new AchDef { id = "ov_stage25", emoji = "🗻", name = "끝없는 등반", desc = "25스테이지 도달", cat = "고점", tier = "프리즘", reward = "고급 칭호: 천공의 등반가", req = new[] { new StatReq("bestStage", 25L) } },
            new AchDef { id = "ov_over120", emoji = "💥", name = "여유로운 클리어", desc = "한 스테이지 요구치 120% 초과 달성", cat = "고점", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("maxOverPct", 120L) } },
            new AchDef { id = "ov_over150", emoji = "💥", name = "넉넉한 한 방", desc = "한 스테이지 요구치 150% 초과 달성", cat = "고점", tier = "실버", reward = "칭호: 넉넉한 한 방", req = new[] { new StatReq("maxOverPct", 150L) } },
            new AchDef { id = "ov_over200", emoji = "💥", name = "두 배의 폭발", desc = "한 스테이지 요구치 200% 초과 달성", cat = "고점", tier = "골드", reward = "칭호: 두 배의 폭발", req = new[] { new StatReq("maxOverPct", 200L) } },
            new AchDef { id = "ov_over300", emoji = "💥", name = "압도적 초과", desc = "한 스테이지 요구치 300% 초과 달성", cat = "고점", tier = "골드", reward = "칭호: 압도적 초과", req = new[] { new StatReq("maxOverPct", 300L) } },
            new AchDef { id = "ov_over500", emoji = "💥", name = "초과의 화신", desc = "한 스테이지 요구치 500% 초과 달성", cat = "고점", tier = "프리즘", reward = "고급 칭호: 한계를 부수는 자", req = new[] { new StatReq("maxOverPct", 500L) } },

            // ── 보스 (bossClears 10/30) ───────────────────────────────
            new AchDef { id = "bs_boss10", emoji = "👹", name = "보스 토벌대", desc = "보스 10회 클리어", cat = "보스", tier = "실버", reward = "칭호: 보스 토벌대장", req = new[] { new StatReq("bossClears", 10L) } },
            new AchDef { id = "bs_boss30", emoji = "👹", name = "보스 처형자", desc = "보스 30회 클리어", cat = "보스", tier = "프리즘", reward = "고급 칭호: 보스의 천적", req = new[] { new StatReq("bossClears", 30L) } },

            // ── 경제 ──────────────────────────────────────────────────
            new AchDef { id = "ec_shop5", emoji = "🛍️", name = "첫 단골", desc = "상점에서 5회 구매", cat = "경제", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("shopBuys", 5L) } },
            new AchDef { id = "ec_shop25", emoji = "🛍️", name = "VIP 고객", desc = "상점에서 25회 구매", cat = "경제", tier = "골드", reward = "칭호: 상점 VIP", req = new[] { new StatReq("shopBuys", 25L) } },
            new AchDef { id = "ec_reroll5", emoji = "🔄", name = "다시 한 번", desc = "재굴림 5회 사용", cat = "경제", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("rerollUses", 5L) } },
            new AchDef { id = "ec_reroll30", emoji = "🔄", name = "운명 거부자", desc = "재굴림 30회 사용", cat = "경제", tier = "골드", reward = "칭호: 운명 거부자", req = new[] { new StatReq("rerollUses", 30L) } },
            new AchDef { id = "ec_gamble5", emoji = "🎲", name = "도박장 단골", desc = "도박장 노드 5회 이용", cat = "경제", tier = "실버", reward = "칭호: 도박장 단골", req = new[] { new StatReq("gambles", 5L) } },
            new AchDef { id = "ec_gamble30", emoji = "🎲", name = "하우스의 친구", desc = "도박장 노드 30회 이용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 하우스의 친구", req = new[] { new StatReq("gambles", 30L) } },
            new AchDef { id = "ec_allin10", emoji = "💥", name = "올인 베테랑", desc = "올인 스핀 10회 승리", cat = "경제", tier = "골드", reward = "칭호: 올인 베테랑", req = new[] { new StatReq("allinWins", 10L) } },
            new AchDef { id = "ec_allin50", emoji = "💥", name = "올인의 전설", desc = "올인 스핀 50회 승리", cat = "경제", tier = "프리즘", reward = "고급 칭호: 올인의 전설", req = new[] { new StatReq("allinWins", 50L) } },
            new AchDef { id = "ec_pray15", emoji = "🙏", name = "독실한 신자", desc = "기도 후 클리어 15회", cat = "경제", tier = "골드", reward = "칭호: 독실한 신자", req = new[] { new StatReq("prayClears", 15L) } },
            new AchDef { id = "ec_pray30", emoji = "🙏", name = "기적의 사도", desc = "기도 후 클리어 30회", cat = "경제", tier = "프리즘", reward = "고급 칭호: 기적의 사도", req = new[] { new StatReq("prayClears", 30L) } },
            new AchDef { id = "ec_jackpot25", emoji = "🎰", name = "잭팟 수집가", desc = "5칸 잭팟 25회 달성", cat = "경제", tier = "골드", reward = "칭호: 잭팟 수집가", req = new[] { new StatReq("jackpots", 25L) } },
            new AchDef { id = "ec_jackpot50", emoji = "🎰", name = "잭팟의 화신", desc = "5칸 잭팟 50회 달성", cat = "경제", tier = "프리즘", reward = "고급 칭호: 잭팟의 화신", req = new[] { new StatReq("jackpots", 50L) } },
            new AchDef { id = "ec_prism10", emoji = "🔮", name = "프리즘 애호가", desc = "프리즘 증강 10회 선택", cat = "경제", tier = "골드", reward = "칭호: 프리즘 애호가", req = new[] { new StatReq("prismPicks", 10L) } },
            new AchDef { id = "ec_prism50", emoji = "🔮", name = "프리즘 마스터", desc = "프리즘 증강 50회 선택", cat = "경제", tier = "프리즘", reward = "고급 칭호: 프리즘 마스터", req = new[] { new StatReq("prismPicks", 50L) } },
            new AchDef { id = "ec_dev30", emoji = "⚙️", name = "장치 숙련공", desc = "장치를 30회 사용", cat = "경제", tier = "골드", reward = "칭호: 장치 숙련공", req = new[] { new StatReq("deviceUses", 30L) } },
            new AchDef { id = "ec_dev100", emoji = "⚙️", name = "장치 대가", desc = "장치를 100회 사용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 장치의 대가", req = new[] { new StatReq("deviceUses", 100L) } },
            new AchDef { id = "ec_item25", emoji = "🎒", name = "아이템 애호가", desc = "아이템 25회 사용", cat = "경제", tier = "실버", reward = "칭호: 알뜰 소비자", req = new[] { new StatReq("itemsUsed", 25L) } },
            new AchDef { id = "ec_item200", emoji = "🎒", name = "소비의 정점", desc = "아이템 200회 사용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 소비의 정점", req = new[] { new StatReq("itemsUsed", 200L) } },
            new AchDef { id = "ec_coin3000", emoji = "🪙", name = "조폐국 총재", desc = "🪙코인 누적 3000개 등장", cat = "경제", tier = "프리즘", reward = "고급 칭호: 조폐국 총재", req = new[] { new StatReq("coinTotal", 3000L) } },
            new AchDef { id = "ec_dev_own8", emoji = "🔧", name = "장치 보유 8종", desc = "장치를 8종 영구 보유", cat = "경제", tier = "골드", reward = "칭호: 장치 보유 8종", req = new[] { new StatReq("devicesOwned", 8L) } },

            // ── 빌드 (relicsMax 7/15, curse5Stage·noDevStage·noItemMaxS S10/S15) ──
            new AchDef { id = "bd_relic7", emoji = "🏺", name = "유물 수호자", desc = "한 런에 유물 7개 동시 보유", cat = "빌드", tier = "골드", reward = "칭호: 유물 수호자", req = new[] { new StatReq("relicsMax", 7L) } },
            new AchDef { id = "bd_relic15", emoji = "🏺", name = "유물 박물관장", desc = "한 런에 유물 15개 동시 보유", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 유물 박물관장", req = new[] { new StatReq("relicsMax", 15L) } },
            new AchDef { id = "bd_curse5_10", emoji = "☠️", name = "저주를 안고", desc = "저주 5개 이상 보유로 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 저주를 안은 등반가", req = new[] { new StatReq("curse5Stage", 10L) } },
            new AchDef { id = "bd_curse5_15", emoji = "☠️", name = "저주와 동행", desc = "저주 5개 이상 보유로 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 저주를 다스리는 자", req = new[] { new StatReq("curse5Stage", 15L) } },
            new AchDef { id = "bd_nodev10", emoji = "🚫", name = "맨손의 등반가", desc = "장치 없이 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 맨손의 등반가", req = new[] { new StatReq("noDevStage", 10L) } },
            new AchDef { id = "bd_nodev15", emoji = "🚫", name = "무장치의 달인", desc = "장치 없이 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 무장치의 달인", req = new[] { new StatReq("noDevStage", 15L) } },
            new AchDef { id = "bd_noitem10", emoji = "🧘", name = "비움의 등반", desc = "아이템 없이 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 비움의 등반가", req = new[] { new StatReq("noItemMaxS", 10L) } },
            new AchDef { id = "bd_noitem15", emoji = "🧘", name = "무소유의 경지", desc = "아이템 없이 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 무소유의 경지", req = new[] { new StatReq("noItemMaxS", 15L) } },

            // ── 반복 (totalSpins 2000/10000, runs·명령어 미존재 임계) ──
            new AchDef { id = "rp_spin2000", emoji = "🔂", name = "이천 번의 스핀", desc = "누적 2000회 스핀", cat = "반복", tier = "골드", reward = "칭호: 손목의 장인", req = new[] { new StatReq("totalSpins", 2000L) } },
            new AchDef { id = "rp_spin10000", emoji = "🔂", name = "만 번의 스핀", desc = "누적 10000회 스핀", cat = "반복", tier = "프리즘", reward = "고급 칭호: 스핀의 화신", req = new[] { new StatReq("totalSpins", 10000L) } },
            new AchDef { id = "rp_focus100", emoji = "🎯", name = "집중의 화신", desc = "집중 명령 100회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 집중의 화신", req = new[] { new StatReq("focusUses", 100L) } },
            new AchDef { id = "rp_last50", emoji = "⏳", name = "최후의 화신", desc = "최후 명령 50회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 최후의 화신", req = new[] { new StatReq("lastUses", 50L) } },
            new AchDef { id = "rp_reroll50", emoji = "🔄", name = "재굴림의 화신", desc = "재굴림 50회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 재굴림의 화신", req = new[] { new StatReq("rerollUses", 50L) } },
            new AchDef { id = "rp_pin30", emoji = "📌", name = "고정의 화신", desc = "고정 30회 사용", cat = "반복", tier = "골드", reward = "칭호: 고정의 화신", req = new[] { new StatReq("pinUses", 30L) } },

            // ── 한 런 최다잭팟 (maxRunJackpots 2/3/5 — 업적 미존재 카운터) ──
            new AchDef { id = "rj_run2", emoji = "🎰", name = "더블 잭팟", desc = "한 런에 잭팟 2회", cat = "역전", tier = "실버", reward = "칭호: 더블 잭팟", req = new[] { new StatReq("maxRunJackpots", 2L) } },
            new AchDef { id = "rj_run3", emoji = "🎰", name = "트리플 잭팟", desc = "한 런에 잭팟 3회", cat = "역전", tier = "골드", reward = "칭호: 트리플 잭팟", req = new[] { new StatReq("maxRunJackpots", 3L) } },
            new AchDef { id = "rj_run5", emoji = "🎰", name = "잭팟 폭풍", desc = "한 런에 잭팟 5회", cat = "역전", tier = "프리즘", reward = "고급 칭호: 잭팟 폭풍의 주인", req = new[] { new StatReq("maxRunJackpots", 5L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-3 확장 — 특수심볼 누적 + 스핀단위 히든 (신규 CSV 카운터 추적, 2026-06-30)
            // 추적: SlotV2Service.handleSpin(L633 부근 incMap/spinMax) + gameOver(prayFails).
            // ══════════════════════════════════════════════════════════
            // ── 특수심볼 누적 🌀 와일드 (wildTotal) ──
            new AchDef { id = "sp_wild30", emoji = "🌀", name = "와일드 입문", desc = "🌀와일드 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("wildTotal", 30L) } },
            new AchDef { id = "sp_wild150", emoji = "🌀", name = "혼돈의 조율사", desc = "🌀와일드 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 혼돈의 조율사", req = new[] { new StatReq("wildTotal", 150L) } },
            new AchDef { id = "sp_wild500", emoji = "🌀", name = "와일드 마술사", desc = "🌀와일드 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 와일드 마술사", req = new[] { new StatReq("wildTotal", 500L) } },
            new AchDef { id = "sp_wild1500", emoji = "🌀", name = "혼돈의 지배자", desc = "🌀와일드 누적 1500개 등장", cat = "특수심볼", tier = "프리즘", reward = "고급 칭호: 혼돈의 지배자", req = new[] { new StatReq("wildTotal", 1500L) } },

            // ── 특수심볼 누적 🌱 씨앗 (seedTotal) ──
            new AchDef { id = "sp_seed30", emoji = "🌱", name = "씨 뿌리기", desc = "🌱씨앗 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("seedTotal", 30L) } },
            new AchDef { id = "sp_seed150", emoji = "🌱", name = "성실한 정원사", desc = "🌱씨앗 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 성실한 정원사", req = new[] { new StatReq("seedTotal", 150L) } },
            new AchDef { id = "sp_seed500", emoji = "🌱", name = "생명의 재배자", desc = "🌱씨앗 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 생명의 재배자", req = new[] { new StatReq("seedTotal", 500L) } },

            // ── 특수심볼 누적 🎲 주사위 (diceTotal) ──
            new AchDef { id = "sp_dice30", emoji = "🎲", name = "주사위 굴리기", desc = "🎲주사위 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("diceTotal", 30L) } },
            new AchDef { id = "sp_dice150", emoji = "🎲", name = "운명의 도박사", desc = "🎲주사위 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 운명의 도박사", req = new[] { new StatReq("diceTotal", 150L) } },
            new AchDef { id = "sp_dice500", emoji = "🎲", name = "확률의 지배자", desc = "🎲주사위 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 확률의 지배자", req = new[] { new StatReq("diceTotal", 500L) } },

            // ── 특수심볼 누적 🗝 열쇠 (keyTotal) ──
            new AchDef { id = "sp_key30", emoji = "🗝", name = "열쇠 줍기", desc = "🗝열쇠 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("keyTotal", 30L) } },
            new AchDef { id = "sp_key150", emoji = "🗝", name = "금고 단골", desc = "🗝열쇠 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 금고 단골", req = new[] { new StatReq("keyTotal", 150L) } },
            new AchDef { id = "sp_key500", emoji = "🗝", name = "보고의 열쇠지기", desc = "🗝열쇠 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 보고의 열쇠지기", req = new[] { new StatReq("keyTotal", 500L) } },

            // ── 특수심볼 누적 🔥 불꽃 (flameTotal) ──
            new AchDef { id = "sp_flame30", emoji = "🔥", name = "불씨 점화", desc = "🔥불꽃 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("flameTotal", 30L) } },
            new AchDef { id = "sp_flame150", emoji = "🔥", name = "연소의 달인", desc = "🔥불꽃 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 연소의 달인", req = new[] { new StatReq("flameTotal", 150L) } },
            new AchDef { id = "sp_flame500", emoji = "🔥", name = "화염의 지배자", desc = "🔥불꽃 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 화염의 지배자", req = new[] { new StatReq("flameTotal", 500L) } },

            // ── 특수심볼 누적 🧲 자석 (magnetTotal) ──
            new AchDef { id = "sp_magnet30", emoji = "🧲", name = "자석 입문", desc = "🧲자석 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("magnetTotal", 30L) } },
            new AchDef { id = "sp_magnet150", emoji = "🧲", name = "끌어당기는 손", desc = "🧲자석 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 끌어당기는 손", req = new[] { new StatReq("magnetTotal", 150L) } },
            new AchDef { id = "sp_magnet500", emoji = "🧲", name = "인력의 지배자", desc = "🧲자석 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 인력의 지배자", req = new[] { new StatReq("magnetTotal", 500L) } },

            // ── 특수심볼 누적 💣 폭탄 (bombTotal) ──
            new AchDef { id = "sp_bomb30", emoji = "💣", name = "폭탄 해체반", desc = "💣폭탄 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록", req = new[] { new StatReq("bombTotal", 30L) } },
            new AchDef { id = "sp_bomb150", emoji = "💣", name = "폭파의 전문가", desc = "💣폭탄 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 폭파의 전문가", req = new[] { new StatReq("bombTotal", 150L) } },
            new AchDef { id = "sp_bomb500", emoji = "💣", name = "파괴의 지배자", desc = "💣폭탄 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 파괴의 지배자", req = new[] { new StatReq("bombTotal", 500L) } },

            // ── 잭팟 종류 (crownJackpots / wildJackpots) ──
            new AchDef { id = "jk_crown1", emoji = "👑", name = "왕관 잭팟", desc = "👑왕관 5칸 잭팟 달성", cat = "잭팟", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("crownJackpots", 1L) } },
            new AchDef { id = "jk_crown5", emoji = "👑", name = "황금의 정렬", desc = "👑왕관 잭팟 5회 달성", cat = "잭팟", tier = "프리즘", reward = "고급 칭호: 왕관 잭팟의 제왕", req = new[] { new StatReq("crownJackpots", 5L) } },
            new AchDef { id = "jk_wild1", emoji = "🌀", name = "와일드 잭팟", desc = "🌀와일드를 끼워 잭팟 달성", cat = "잭팟", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("wildJackpots", 1L) } },
            new AchDef { id = "jk_wild10", emoji = "🌀", name = "조작된 운명", desc = "🌀와일드 포함 잭팟 10회", cat = "잭팟", tier = "프리즘", reward = "고급 칭호: 운명을 조작하는 자", req = new[] { new StatReq("wildJackpots", 10L) } },

            // ── 히든: 한 스핀 같은 심볼 풀(5칸 또는 보조릴 6칸) ──
            new AchDef { id = "hid_skull5spin", emoji = "💀", name = "죽음의 한 줄", desc = "한 스핀에 ☠해골 5개", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 죽음의 한 줄", req = new[] { new StatReq("maxSkullSpin", 5L) } },
            new AchDef { id = "hid_coin5spin", emoji = "🪙", name = "동전 벼락", desc = "한 스핀에 🪙코인 5개", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 동전 벼락", req = new[] { new StatReq("maxCoinSpin", 5L) } },
            new AchDef { id = "hid_cherry5spin", emoji = "🍒", name = "체리 만발", desc = "한 스핀에 🍒체리 5개", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 체리 만발", req = new[] { new StatReq("maxCherrySpin", 5L) } },
            new AchDef { id = "hid_book5spin", emoji = "📘", name = "전권 정렬", desc = "한 스핀에 📘책 5개", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 전권 정렬", req = new[] { new StatReq("maxBookSpin", 5L) } },
            new AchDef { id = "hid_gem5spin", emoji = "💎", name = "보석 일렬", desc = "한 스핀에 💎보석 5개", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 보석 일렬", req = new[] { new StatReq("maxGemSpin", 5L) } },

            // ── 히든: 🎲올인 폭망 / 🙏기도 실패 (위험 도전의 흔적) ──
            new AchDef { id = "hid_allinbust1", emoji = "💀", name = "올인의 대가", desc = "올인이 ☠2개로 EXP 0이 되었다", cat = "히든", tier = "브론즈", hidden = true, reward = "도감 등록", req = new[] { new StatReq("allinBusts", 1L) } },
            new AchDef { id = "hid_allinbust10", emoji = "💀", name = "파산의 길", desc = "올인 폭망 10회", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 파산의 길", req = new[] { new StatReq("allinBusts", 10L) } },
            new AchDef { id = "hid_prayfail1", emoji = "🙏", name = "응답 없는 기도", desc = "기도하고도 스테이지 실패", cat = "히든", tier = "브론즈", hidden = true, reward = "도감 등록", req = new[] { new StatReq("prayFails", 1L) } },
            new AchDef { id = "hid_prayfail10", emoji = "🙏", name = "시험받는 신앙", desc = "기도 실패 10회", cat = "히든", tier = "골드", hidden = true, reward = "칭호: 시험받는 신앙", req = new[] { new StatReq("prayFails", 10L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-4 확장 — 제한도전 + 보스별 공략 + 클리어단위 히든 (신규 CSV 카운터, 2026-06-30)
            // 추적: SlotV2Service.addAch4ClearTracking(clearStage). 전부 런상태 파생(DB 스키마 무변경).
            // ══════════════════════════════════════════════════════════
            // ── 제한도전: 프리즘 증강 0개 도달 (noPrismBestStage) ──
            new AchDef { id = "lc_noprism5", emoji = "🚷", name = "무지개 금욕", desc = "🌈프리즘 증강 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("noPrismBestStage", 5L) } },
            new AchDef { id = "lc_noprism10", emoji = "🚷", name = "절제의 등반", desc = "🌈프리즘 증강 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 절제의 등반가", req = new[] { new StatReq("noPrismBestStage", 10L) } },
            new AchDef { id = "lc_noprism15", emoji = "🚷", name = "무채색의 정점", desc = "🌈프리즘 증강 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무채색의 정점", req = new[] { new StatReq("noPrismBestStage", 15L) } },

            // ── 제한도전: 유물 0개 도달 (noRelicBestStage) ──
            new AchDef { id = "lc_norelic5", emoji = "🛡️", name = "맨몸의 도전", desc = "🛡️유물 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("noRelicBestStage", 5L) } },
            new AchDef { id = "lc_norelic10", emoji = "🛡️", name = "유물 없는 길", desc = "🛡️유물 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 무유물 등반가", req = new[] { new StatReq("noRelicBestStage", 10L) } },
            new AchDef { id = "lc_norelic15", emoji = "🛡️", name = "순수 증강주의", desc = "🛡️유물 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 순수 증강주의", req = new[] { new StatReq("noRelicBestStage", 15L) } },

            // ── 제한도전: 골드+프리즘 증강 0개(실버/유물만) 도달 (noGoldBestStage) ──
            new AchDef { id = "lc_nogold5", emoji = "🥈", name = "실버 빌드", desc = "골드↑ 증강 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("noGoldBestStage", 5L) } },
            new AchDef { id = "lc_nogold10", emoji = "🥈", name = "은의 길", desc = "골드↑ 증강 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 은의 길", req = new[] { new StatReq("noGoldBestStage", 10L) } },
            new AchDef { id = "lc_nogold12", emoji = "🥈", name = "겸손한 명인", desc = "골드↑ 증강 없이 S12 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 겸손한 명인", req = new[] { new StatReq("noGoldBestStage", 12L) } },

            // ── 제한도전: 초보캐릭+기본머신 도달 (basicOnlyBestStage) ──
            new AchDef { id = "lc_basic5", emoji = "🐣", name = "맨주먹 신입생", desc = "초보+기본 머신으로 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("basicOnlyBestStage", 5L) } },
            new AchDef { id = "lc_basic10", emoji = "🐣", name = "기본기의 증명", desc = "초보+기본 머신으로 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 기본기의 달인", req = new[] { new StatReq("basicOnlyBestStage", 10L) } },
            new AchDef { id = "lc_basic15", emoji = "🐣", name = "무에서 정점으로", desc = "초보+기본 머신으로 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무에서 정점으로", req = new[] { new StatReq("basicOnlyBestStage", 15L) } },

            // ── 보스별 격파: 📝기말고사 (bossClear_finals) ──
            new AchDef { id = "bc_finals1", emoji = "📝", name = "기말고사 합격", desc = "📝기말고사를 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossClear_finals", 1L) } },
            new AchDef { id = "bc_finals10", emoji = "📝", name = "수석 졸업", desc = "📝기말고사 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 기말 수석", req = new[] { new StatReq("bossClear_finals", 10L) } },
            new AchDef { id = "bc_finals_ctr", emoji = "⏰", name = "최후의 답안", desc = "📝기말고사를 막스핀 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 막판의 천재", req = new[] { new StatReq("bossCounterClear_finals", 5L) } },

            // ── 보스별 격파: 👨‍🏫꼰대교수 (bossClear_strict) ──
            new AchDef { id = "bc_strict1", emoji = "👨‍🏫", name = "꼰대 통과", desc = "👨‍🏫꼰대교수를 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossClear_strict", 1L) } },
            new AchDef { id = "bc_strict10", emoji = "👨‍🏫", name = "교수의 인정", desc = "👨‍🏫꼰대교수 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 교수의 애제자", req = new[] { new StatReq("bossClear_strict", 10L) } },
            new AchDef { id = "bc_strict_ctr", emoji = "🧩", name = "완벽한 세트", desc = "👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 세트의 장인", req = new[] { new StatReq("bossCounterClear_strict", 5L) } },

            // ── 보스별 격파: 🎲운빨심판관 (bossClear_luck) ──
            new AchDef { id = "bc_luck1", emoji = "🎲", name = "심판관 통과", desc = "🎲운빨심판관을 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossClear_luck", 1L) } },
            new AchDef { id = "bc_luck10", emoji = "🎲", name = "운명의 총아", desc = "🎲운빨심판관 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 운명의 총아", req = new[] { new StatReq("bossClear_luck", 10L) } },
            new AchDef { id = "bc_luck_ctr", emoji = "🍀", name = "행운의 정렬", desc = "🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 행운의 화신", req = new[] { new StatReq("bossCounterClear_luck", 5L) } },

            // ── 보스별 격파: 🎓졸업심사 (bossClear_grad) ──
            new AchDef { id = "bc_grad1", emoji = "🎓", name = "졸업 승인", desc = "🎓졸업심사를 처음 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("bossClear_grad", 1L) } },
            new AchDef { id = "bc_grad10", emoji = "🎓", name = "명예 졸업", desc = "🎓졸업심사 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "칭호: 명예 졸업생", req = new[] { new StatReq("bossClear_grad", 10L) } },
            new AchDef { id = "bc_grad_ctr", emoji = "✋", name = "맨손 졸업", desc = "🎓졸업심사를 무장치로 클리어 3회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 맨손의 졸업생", req = new[] { new StatReq("bossCounterClear_grad", 3L) } },

            // ── 보스 공통 제약 (bossNoItemClears / bossNoDeviceClears / bossOverkillClears) ──
            new AchDef { id = "bx_noitem1", emoji = "🧘", name = "무소유의 보스전", desc = "아이템 없이 보스 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("bossNoItemClears", 1L) } },
            new AchDef { id = "bx_noitem10", emoji = "🧘", name = "비움의 정복자", desc = "아이템 없이 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 비움의 정복자", req = new[] { new StatReq("bossNoItemClears", 10L) } },
            new AchDef { id = "bx_nodev1", emoji = "🚫", name = "맨몸 보스전", desc = "장치 없이 보스 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("bossNoDeviceClears", 1L) } },
            new AchDef { id = "bx_nodev10", emoji = "🚫", name = "장치 없는 사냥꾼", desc = "장치 없이 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 장치 없는 사냥꾼", req = new[] { new StatReq("bossNoDeviceClears", 10L) } },
            new AchDef { id = "bx_overkill1", emoji = "💥", name = "보스 오버킬", desc = "초과 500%+ 로 보스 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 오버킬", req = new[] { new StatReq("bossOverkillClears", 1L) } },
            new AchDef { id = "bx_overkill10", emoji = "💥", name = "압도적 우위", desc = "초과 500%+ 로 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 압도적 지배자", req = new[] { new StatReq("bossOverkillClears", 10L) } },

            // ── 한 런 보스 3회 격파 (bossStreak3) ──
            new AchDef { id = "bx_streak3", emoji = "🔥", name = "보스 삼연참", desc = "한 런에 보스 3회 격파(S15+)", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 보스 삼연참", req = new[] { new StatReq("bossStreak3", 1L) } },

            // ── 클리어 히든: 빈 지갑 / 빚더미 보스 ──
            new AchDef { id = "hid_zerocoin1", emoji = "🪙", name = "무일푼 클리어", desc = "코인 0으로 스테이지 클리어", cat = "히든", tier = "실버", hidden = true, reward = "도감 등록", req = new[] { new StatReq("zeroCoinClears", 1L) } },
            new AchDef { id = "hid_zerocoin20", emoji = "🪙", name = "청빈의 도", desc = "코인 0으로 20회 클리어", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 청빈의 도", req = new[] { new StatReq("zeroCoinClears", 20L) } },
            new AchDef { id = "hid_debtboss1", emoji = "🧾", name = "빚더미 보스전", desc = "빚문서 상태로 보스 클리어", cat = "히든", tier = "골드", hidden = true, reward = "도감 등록", req = new[] { new StatReq("debtBossClears", 1L) } },
            new AchDef { id = "hid_debtboss5", emoji = "🧾", name = "채무의 승부사", desc = "빚문서 상태로 보스 5회 클리어", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 채무의 승부사", req = new[] { new StatReq("debtBossClears", 5L) } },

            // ── ACH-4 보강: 기존 추적 카운터의 누락 임계 채움 (제한도전 입문 + 보스 카운터 1회 입문) ──
            // 제한도전: 무장치(noDevStage)/무아이템(noItemMaxS)/무상점(noShopS10)/미니멀(minimalistS10) 입문 임계
            new AchDef { id = "lc_nodev5", emoji = "🚫", name = "맨손 등반 입문", desc = "🚫장치 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("noDevStage", 5L) } },
            new AchDef { id = "lc_noitem8", emoji = "🧘", name = "비움의 등반", desc = "🧘아이템 없이 S8 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("noItemMaxS", 8L) } },
            new AchDef { id = "lc_noshop10", emoji = "🪙", name = "자급자족 졸업", desc = "🛒상점 없이 S10 도달", cat = "제한도전", tier = "골드", reward = "칭호: 자급자족", req = new[] { new StatReq("noShopS10", 10L) } },
            new AchDef { id = "lc_minimalist10", emoji = "🍃", name = "미니멀리스트", desc = "유물 3개 이하로 S10 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("minimalistS10", 1L) } },

            // 보스 카운터(약점 공략) 첫 성공 입문 — 보스별 카운터조건 1회 충족
            new AchDef { id = "bc_finals_ctr1", emoji = "⏰", name = "막판의 한 수", desc = "📝기말고사를 막스핀 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossCounterClear_finals", 1L) } },
            new AchDef { id = "bc_strict_ctr1", emoji = "🧩", name = "세트의 첫 인정", desc = "👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossCounterClear_strict", 1L) } },
            new AchDef { id = "bc_luck_ctr1", emoji = "🍀", name = "첫 행운의 정렬", desc = "🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록", req = new[] { new StatReq("bossCounterClear_luck", 1L) } },
            new AchDef { id = "bc_grad_ctr1", emoji = "✋", name = "첫 맨손 졸업", desc = "🎓졸업심사를 무장치로 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록", req = new[] { new StatReq("bossCounterClear_grad", 1L) } },

            // 보스 공통 제약 중간 임계 (3회)
            new AchDef { id = "bx_noitem3", emoji = "🧘", name = "무소유의 연승", desc = "아이템 없이 보스 3회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 무소유 사냥꾼", req = new[] { new StatReq("bossNoItemClears", 3L) } },
            new AchDef { id = "bx_nodev3", emoji = "🚫", name = "맨몸의 연승", desc = "장치 없이 보스 3회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 맨몸 사냥꾼", req = new[] { new StatReq("bossNoDeviceClears", 3L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-5a 연구(전공) — 10전공 × 입문/심화/박사 (신규 추적코드 0, 전부 기존 추적 카운터).
            // ★입문(rs_*_intro)의 (key,threshold)는 SlotV2Engine.SCHOOL_RESEARCH 와 정확히 일치 →
            // 달성 시 해당 school 의 실버/골드 증강·유물 풀 개방(perkUnlocked OR 경로). 프리즘 perk 는 가드로 제외.
            // 심화/박사 = 더 높은 임계의 코스메틱(칭호/프레임)·풀개방 아님. (key,threshold) 기존 374종과 중복 없음.
            // ══════════════════════════════════════════════════════════
            // ── 🌱 성장학 (cherryTotal) — 입문 300 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_growth_intro", emoji = "🌱", name = "성장학 입문", desc = "🍒체리 누적 300 — 성장학 연구 완료, 성장학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓성장학 증강·유물 풀 개방", req = new[] { new StatReq("cherryTotal", 300L) } },
            new AchDef { id = "rs_growth_adv", emoji = "🌱", name = "성장학 심화", desc = "🍒체리 누적 600 — 성장학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 성장학 연구원", req = new[] { new StatReq("cherryTotal", 600L) } },
            new AchDef { id = "rs_growth_phd", emoji = "🌱", name = "성장학 박사", desc = "🍒체리 누적 2000 — 성장학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 성장학 박사", req = new[] { new StatReq("cherryTotal", 2000L) } },

            // ── 🧮 계산학 (set4Plus) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_calc_intro", emoji = "🧮", name = "계산학 입문", desc = "같은 심볼 4+ 3회 — 계산학 연구 완료, 계산학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓계산학 증강·유물 풀 개방", req = new[] { new StatReq("set4Plus", 3L) } },
            new AchDef { id = "rs_calc_adv", emoji = "🧮", name = "계산학 심화", desc = "같은 심볼 4+ 10회 — 계산학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 계산학 연구원", req = new[] { new StatReq("set4Plus", 10L) } },
            new AchDef { id = "rs_calc_phd", emoji = "🧮", name = "계산학 박사", desc = "같은 심볼 4+ 30회 — 계산학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 계산학 박사", req = new[] { new StatReq("set4Plus", 30L) } },

            // ── 💰 경제학 (coinTotal) — 입문 300 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_econ_intro", emoji = "💰", name = "경제학 입문", desc = "🪙코인 누적 300 — 경제학 연구 완료, 경제학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓경제학 증강·유물 풀 개방", req = new[] { new StatReq("coinTotal", 300L) } },
            new AchDef { id = "rs_econ_adv", emoji = "💰", name = "경제학 심화", desc = "🪙코인 누적 700 — 경제학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 경제학 연구원", req = new[] { new StatReq("coinTotal", 700L) } },
            new AchDef { id = "rs_econ_phd", emoji = "💰", name = "경제학 박사", desc = "🪙코인 누적 1500 — 경제학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 경제학 박사", req = new[] { new StatReq("coinTotal", 1500L) } },

            // ── 🎲 운명학 (gambles) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_fate_intro", emoji = "🎴", name = "운명학 입문", desc = "도박장 3회 — 운명학 연구 완료, 운명학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓운명학 증강·유물 풀 개방", req = new[] { new StatReq("gambles", 3L) } },
            new AchDef { id = "rs_fate_adv", emoji = "🎴", name = "운명학 심화", desc = "도박장 15회 — 운명학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 운명학 연구원", req = new[] { new StatReq("gambles", 15L) } },
            new AchDef { id = "rs_fate_phd", emoji = "🎴", name = "운명학 박사", desc = "도박장 50회 — 운명학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 운명학 박사", req = new[] { new StatReq("gambles", 50L) } },

            // ── 👑 왕관학 (crownTotal) — 입문 30 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_crown_intro", emoji = "👑", name = "왕관학 입문", desc = "👑왕관 누적 30 — 왕관학 연구 완료, 왕관학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓왕관학 증강·유물 풀 개방", req = new[] { new StatReq("crownTotal", 30L) } },
            new AchDef { id = "rs_crown_adv", emoji = "👑", name = "왕관학 심화", desc = "👑왕관 누적 60 — 왕관학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 왕관학 연구원", req = new[] { new StatReq("crownTotal", 60L) } },
            new AchDef { id = "rs_crown_phd", emoji = "👑", name = "왕관학 박사", desc = "👑왕관 누적 200 — 왕관학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 왕관학 박사", req = new[] { new StatReq("crownTotal", 200L) } },

            // ── 💀 저주학 (skullTotal) — 입문 100 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_curse_intro", emoji = "💀", name = "저주학 입문", desc = "💀해골 누적 100 — 저주학 연구 완료, 저주학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓저주학 증강·유물 풀 개방", req = new[] { new StatReq("skullTotal", 100L) } },
            new AchDef { id = "rs_curse_adv", emoji = "💀", name = "저주학 심화", desc = "💀해골 누적 500 — 저주학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 저주학 연구원", req = new[] { new StatReq("skullTotal", 500L) } },
            new AchDef { id = "rs_curse_phd", emoji = "💀", name = "저주학 박사", desc = "💀해골 누적 700 — 저주학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 저주학 박사", req = new[] { new StatReq("skullTotal", 700L) } },

            // ── ⏳ 시간학 (lastSpinClears) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_time_intro", emoji = "⏳", name = "시간학 입문", desc = "막판 스핀 클리어 3회 — 시간학 연구 완료, 시간학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓시간학 증강·유물 풀 개방", req = new[] { new StatReq("lastSpinClears", 3L) } },
            new AchDef { id = "rs_time_adv", emoji = "⏳", name = "시간학 심화", desc = "막판 스핀 클리어 7회 — 시간학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 시간학 연구원", req = new[] { new StatReq("lastSpinClears", 7L) } },
            new AchDef { id = "rs_time_phd", emoji = "⏳", name = "시간학 박사", desc = "막판 스핀 클리어 15회 — 시간학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 시간학 박사", req = new[] { new StatReq("lastSpinClears", 15L) } },

            // ── 🔮 프리즘공학 (prismPicks) — 입문 3 = SCHOOL_RESEARCH 트리거. ⚠️PRISM perk 는 연구로 안 열림(엔진 가드) ──
            new AchDef { id = "rs_prism_intro", emoji = "🔮", name = "프리즘공학 입문", desc = "프리즘 선택 3회 — 프리즘공학 연구 완료, 프리즘공학 실버·골드 증강·유물 풀 개방(프리즘 티어 제외)", cat = "연구", tier = "실버", reward = "🔓프리즘공학 증강·유물 풀 개방(프리즘 티어 제외)", req = new[] { new StatReq("prismPicks", 3L) } },
            new AchDef { id = "rs_prism_adv", emoji = "🔮", name = "프리즘공학 심화", desc = "프리즘 선택 15회 — 프리즘공학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 프리즘공학 연구원", req = new[] { new StatReq("prismPicks", 15L) } },
            new AchDef { id = "rs_prism_phd", emoji = "🔮", name = "프리즘공학 박사", desc = "프리즘 선택 30회 — 프리즘공학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 프리즘공학 박사", req = new[] { new StatReq("prismPicks", 30L) } },

            // ── 🌰 씨앗학 (seedTotal) — 입문 10 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_seed_intro", emoji = "🌰", name = "씨앗학 입문", desc = "🌱씨앗 누적 10 — 씨앗학 연구 완료, 씨앗학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓씨앗학 증강·유물 풀 개방", req = new[] { new StatReq("seedTotal", 10L) } },
            new AchDef { id = "rs_seed_adv", emoji = "🌰", name = "씨앗학 심화", desc = "🌱씨앗 누적 75 — 씨앗학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 씨앗학 연구원", req = new[] { new StatReq("seedTotal", 75L) } },
            new AchDef { id = "rs_seed_phd", emoji = "🌰", name = "씨앗학 박사", desc = "🌱씨앗 누적 300 — 씨앗학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 씨앗학 박사", req = new[] { new StatReq("seedTotal", 300L) } },

            // ── 🌀 와일드학 (wildTotal) — 입문 10 = SCHOOL_RESEARCH 트리거 ──
            new AchDef { id = "rs_wild_intro", emoji = "🌀", name = "와일드학 입문", desc = "🌀와일드 누적 10 — 와일드학 연구 완료, 와일드학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓와일드학 증강·유물 풀 개방", req = new[] { new StatReq("wildTotal", 10L) } },
            new AchDef { id = "rs_wild_adv", emoji = "🌀", name = "와일드학 심화", desc = "🌀와일드 누적 75 — 와일드학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 와일드학 연구원", req = new[] { new StatReq("wildTotal", 75L) } },
            new AchDef { id = "rs_wild_phd", emoji = "🌀", name = "와일드학 박사", desc = "🌀와일드 누적 300 — 와일드학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 와일드학 박사", req = new[] { new StatReq("wildTotal", 300L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-5b 장치 면허 — 12 메인 장치 전용 면허 업적 (#9 정합, 2026-06-30).
            // key = lic_<deviceId> = composeStat 파생키(면허 조건표의 기존 추적 stat AND → 1/0, 신규 추적/DB 0).
            // threshold = 1, tier = 골드(인플레 최소). 달성 = 해당 장치 영구해금(Device.unlockAch 매핑).
            // 보조 4 장치(syllabus/holdfile/retake/major)는 면허 미적용 — 기존 업적 매핑 유지.
            // ══════════════════════════════════════════════════════════
            new AchDef { id = "lic_safe", emoji = "🦺", name = "안전벨트 면허", desc = "아슬아슬 클리어 5회 & S6 도달 — 🦺안전벨트 영구해금", cat = "면허", tier = "골드", reward = "🦺안전벨트 장치 영구해금", req = new[] { new StatReq("lic_dev_safe", 1L) } },
            new AchDef { id = "lic_seal", emoji = "🔒", name = "봉인장막 면허", desc = "💀해골 누적 200 & S8 도달 — 🔒봉인장막 영구해금", cat = "면허", tier = "골드", reward = "🔒봉인장막 장치 영구해금", req = new[] { new StatReq("lic_dev_seal", 1L) } },
            new AchDef { id = "lic_reroll", emoji = "🔄", name = "재굴림기 면허", desc = "보스 3회 클리어 & 막판 클리어 3회 — 🔄재굴림기 영구해금", cat = "면허", tier = "골드", reward = "🔄재굴림기 장치 영구해금", req = new[] { new StatReq("lic_dev_reroll", 1L) } },
            new AchDef { id = "lic_pin", emoji = "📌", name = "고정핀 면허", desc = "정확 클리어 3회 & S8 도달 — 📌고정핀 영구해금", cat = "면허", tier = "골드", reward = "📌고정핀 장치 영구해금", req = new[] { new StatReq("lic_dev_pin", 1L) } },
            new AchDef { id = "lic_coin", emoji = "🪙", name = "코인투입구 면허", desc = "🪙코인 누적 500 & 상점구매 15회 — 🪙코인투입구 영구해금", cat = "면허", tier = "골드", reward = "🪙코인투입구 장치 영구해금", req = new[] { new StatReq("lic_dev_coin", 1L) } },
            new AchDef { id = "lic_subreel", emoji = "➕", name = "보조릴 면허", desc = "잭팟 5회 & 4세트+ 10회 — ➕보조릴 영구해금", cat = "면허", tier = "골드", reward = "➕보조릴 장치 영구해금", req = new[] { new StatReq("lic_dev_subreel", 1L) } },
            new AchDef { id = "lic_overheat", emoji = "♨️", name = "과열코어 면허", desc = "막판 클리어 10회 & 최고점수 20,000 — ♨️과열코어 영구해금", cat = "면허", tier = "골드", reward = "♨️과열코어 장치 영구해금", req = new[] { new StatReq("lic_dev_overheat", 1L) } },
            new AchDef { id = "lic_oracle", emoji = "🔮", name = "예언안경 면허", desc = "기도 클리어 3회 & S15 도달 — 🔮예언안경 영구해금", cat = "면허", tier = "골드", reward = "🔮예언안경 장치 영구해금", req = new[] { new StatReq("lic_dev_oracle", 1L) } },
            new AchDef { id = "lic_copy", emoji = "📑", name = "복사기 면허", desc = "프리즘 선택 10회 & 4세트+ 10회 — 📑복사기 영구해금", cat = "면허", tier = "골드", reward = "📑복사기 장치 영구해금", req = new[] { new StatReq("lic_dev_copy", 1L) } },
            new AchDef { id = "lic_swap", emoji = "🔃", name = "교체기 면허", desc = "보스 10회 클리어 & S15 도달 — 🔃교체기 영구해금", cat = "면허", tier = "골드", reward = "🔃교체기 장치 영구해금", req = new[] { new StatReq("lic_dev_swap", 1L) } },
            new AchDef { id = "lic_bell", emoji = "🔔", name = "비상졸업벨 면허", desc = "아슬아슬 클리어 30회 & 보스 8회 클리어 — 🔔비상졸업벨 영구해금", cat = "면허", tier = "골드", reward = "🔔비상졸업벨 장치 영구해금", req = new[] { new StatReq("lic_dev_bell", 1L) } },
            new AchDef { id = "lic_flame", emoji = "🔥", name = "불꽃엔진 면허", desc = "최고점수 50,000 & S20 도달 — 🔥불꽃엔진 영구해금", cat = "면허", tier = "골드", reward = "🔥불꽃엔진 장치 영구해금", req = new[] { new StatReq("lic_dev_flame", 1L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-5c 장치 숙련/장인 + 무명령/무조작 제한도전 (2026-06-30, 추적코드 선행 완료분 기반).
            // ★숙련 = dvuse_<deviceId> (장착 런수 inc, launchRun) threshold 10.
            // ★장인 = dvstage_<deviceId> (장착 도달 최고 클리어 S, clearStage setMax) threshold 15.
            // 12 메인 장치 각 숙련/장인 = 24. id 접두 dm_<id>_use / dm_<id>_master (유니크).
            // ★무명령(noCommandBestStage)/무조작(noRerollBestStage) = run 플래그 0 일 때 setMax(S).
            // id 접두 rc_nocmd* / rc_noreroll*. 전부 추적확인 키·기존 432종과 (key,threshold)·id 중복 0·코스메틱.
            // ══════════════════════════════════════════════════════════
            // ── 장치 숙련/장인: 🔥불꽃엔진 ──
            new AchDef { id = "dm_dev_flame_use", emoji = "🔥", name = "불꽃엔진 숙련", desc = "🔥불꽃엔진 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 불꽃엔진 숙련자", req = new[] { new StatReq("dvuse_dev_flame", 10L) } },
            new AchDef { id = "dm_dev_flame_master", emoji = "🔥", name = "불꽃엔진 장인", desc = "🔥불꽃엔진 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 불꽃엔진 장인", req = new[] { new StatReq("dvstage_dev_flame", 15L) } },

            // ── 장치 숙련/장인: 🔒봉인장막 ──
            new AchDef { id = "dm_dev_seal_use", emoji = "🔒", name = "봉인장막 숙련", desc = "🔒봉인장막 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 봉인장막 숙련자", req = new[] { new StatReq("dvuse_dev_seal", 10L) } },
            new AchDef { id = "dm_dev_seal_master", emoji = "🔒", name = "봉인장막 장인", desc = "🔒봉인장막 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 봉인장막 장인", req = new[] { new StatReq("dvstage_dev_seal", 15L) } },

            // ── 장치 숙련/장인: 🦺안전벨트 ──
            new AchDef { id = "dm_dev_safe_use", emoji = "🦺", name = "안전벨트 숙련", desc = "🦺안전벨트 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 안전벨트 숙련자", req = new[] { new StatReq("dvuse_dev_safe", 10L) } },
            new AchDef { id = "dm_dev_safe_master", emoji = "🦺", name = "안전벨트 장인", desc = "🦺안전벨트 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 안전벨트 장인", req = new[] { new StatReq("dvstage_dev_safe", 15L) } },

            // ── 장치 숙련/장인: ♨️과열코어 ──
            new AchDef { id = "dm_dev_overheat_use", emoji = "♨️", name = "과열코어 숙련", desc = "♨️과열코어 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 과열코어 숙련자", req = new[] { new StatReq("dvuse_dev_overheat", 10L) } },
            new AchDef { id = "dm_dev_overheat_master", emoji = "♨️", name = "과열코어 장인", desc = "♨️과열코어 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 과열코어 장인", req = new[] { new StatReq("dvstage_dev_overheat", 15L) } },

            // ── 장치 숙련/장인: ➕보조릴 ──
            new AchDef { id = "dm_dev_subreel_use", emoji = "➕", name = "보조릴 숙련", desc = "➕보조릴 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 보조릴 숙련자", req = new[] { new StatReq("dvuse_dev_subreel", 10L) } },
            new AchDef { id = "dm_dev_subreel_master", emoji = "➕", name = "보조릴 장인", desc = "➕보조릴 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 보조릴 장인", req = new[] { new StatReq("dvstage_dev_subreel", 15L) } },

            // ── 장치 숙련/장인: 🪙코인투입구 ──
            new AchDef { id = "dm_dev_coin_use", emoji = "🪙", name = "코인투입구 숙련", desc = "🪙코인투입구 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 코인투입구 숙련자", req = new[] { new StatReq("dvuse_dev_coin", 10L) } },
            new AchDef { id = "dm_dev_coin_master", emoji = "🪙", name = "코인투입구 장인", desc = "🪙코인투입구 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 코인투입구 장인", req = new[] { new StatReq("dvstage_dev_coin", 15L) } },

            // ── 장치 숙련/장인: 🔄재굴림기 ──
            new AchDef { id = "dm_dev_reroll_use", emoji = "🔄", name = "재굴림기 숙련", desc = "🔄재굴림기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 재굴림기 숙련자", req = new[] { new StatReq("dvuse_dev_reroll", 10L) } },
            new AchDef { id = "dm_dev_reroll_master", emoji = "🔄", name = "재굴림기 장인", desc = "🔄재굴림기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 재굴림기 장인", req = new[] { new StatReq("dvstage_dev_reroll", 15L) } },

            // ── 장치 숙련/장인: 📌고정핀 ──
            new AchDef { id = "dm_dev_pin_use", emoji = "📌", name = "고정핀 숙련", desc = "📌고정핀 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 고정핀 숙련자", req = new[] { new StatReq("dvuse_dev_pin", 10L) } },
            new AchDef { id = "dm_dev_pin_master", emoji = "📌", name = "고정핀 장인", desc = "📌고정핀 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 고정핀 장인", req = new[] { new StatReq("dvstage_dev_pin", 15L) } },

            // ── 장치 숙련/장인: 📑복사기 ──
            new AchDef { id = "dm_dev_copy_use", emoji = "📑", name = "복사기 숙련", desc = "📑복사기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 복사기 숙련자", req = new[] { new StatReq("dvuse_dev_copy", 10L) } },
            new AchDef { id = "dm_dev_copy_master", emoji = "📑", name = "복사기 장인", desc = "📑복사기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 복사기 장인", req = new[] { new StatReq("dvstage_dev_copy", 15L) } },

            // ── 장치 숙련/장인: 🔃교체기 ──
            new AchDef { id = "dm_dev_swap_use", emoji = "🔃", name = "교체기 숙련", desc = "🔃교체기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 교체기 숙련자", req = new[] { new StatReq("dvuse_dev_swap", 10L) } },
            new AchDef { id = "dm_dev_swap_master", emoji = "🔃", name = "교체기 장인", desc = "🔃교체기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 교체기 장인", req = new[] { new StatReq("dvstage_dev_swap", 15L) } },

            // ── 장치 숙련/장인: 🔮예언안경 ──
            new AchDef { id = "dm_dev_oracle_use", emoji = "🔮", name = "예언안경 숙련", desc = "🔮예언안경 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 예언안경 숙련자", req = new[] { new StatReq("dvuse_dev_oracle", 10L) } },
            new AchDef { id = "dm_dev_oracle_master", emoji = "🔮", name = "예언안경 장인", desc = "🔮예언안경 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 예언안경 장인", req = new[] { new StatReq("dvstage_dev_oracle", 15L) } },

            // ── 장치 숙련/장인: 🔔비상졸업벨 ──
            new AchDef { id = "dm_dev_bell_use", emoji = "🔔", name = "비상졸업벨 숙련", desc = "🔔비상졸업벨 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 비상졸업벨 숙련자", req = new[] { new StatReq("dvuse_dev_bell", 10L) } },
            new AchDef { id = "dm_dev_bell_master", emoji = "🔔", name = "비상졸업벨 장인", desc = "🔔비상졸업벨 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 비상졸업벨 장인", req = new[] { new StatReq("dvstage_dev_bell", 15L) } },

            // ── 제한도전: 무명령(특수 스핀명령 0회) 도달 (noCommandBestStage) ──
            new AchDef { id = "rc_nocmd10", emoji = "🤐", name = "무언의 등반", desc = "🤐집중/올인/기도/최후 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 무언의 등반가", req = new[] { new StatReq("noCommandBestStage", 10L) } },
            new AchDef { id = "rc_nocmd15", emoji = "🤐", name = "침묵의 정점", desc = "🤐집중/올인/기도/최후 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 침묵의 정점", req = new[] { new StatReq("noCommandBestStage", 15L) } },

            // ── 제한도전: 무조작(재굴림/고정/복사/교체 0회) 도달 (noRerollBestStage) ──
            new AchDef { id = "rc_noreroll10", emoji = "🙌", name = "운명에 맡긴 등반", desc = "🙌재굴림/고정/복사/교체 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 운명에 맡긴 자", req = new[] { new StatReq("noRerollBestStage", 10L) } },
            new AchDef { id = "rc_noreroll15", emoji = "🙌", name = "무조작의 정점", desc = "🙌재굴림/고정/복사/교체 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무조작의 정점", req = new[] { new StatReq("noRerollBestStage", 15L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-6 명령비 지출(경제) — 특수 스핀명령 코인 비용 부과(잭팟런 v3) 연동.
            // 추적: SlotV2Service handleSpin incMap(cmdCoin_focus/pray/allin/total, 차감 시점) +
            // clearStage clearInc(lastClears = ⏰최후로 클리어, bossAllinClear = 👑보스에서 🎲올인+클리어).
            // 신규 추적/DB 0 — 전부 추적확인 키. 기존 460종과 (key,threshold)·id 중복 0. 보상=코스메틱(칭호/힌트).
            // ══════════════════════════════════════════════════════════
            new AchDef { id = "cc_focus10", emoji = "🎯", name = "집중 투자", desc = "🎯집중 명령에 코인 누적 10 지출", cat = "경제", tier = "실버", reward = "칭호: 집중 투자자", req = new[] { new StatReq("cmdCoin_focus", 10L) } },
            new AchDef { id = "cc_pray30", emoji = "🙏", name = "유료 기도", desc = "🙏기도 명령에 코인 누적 30 지출 — 운명/연구의 가호를 산 자", cat = "경제", tier = "골드", reward = "칭호: 유료 기도자", req = new[] { new StatReq("cmdCoin_pray", 30L) } },
            new AchDef { id = "cc_allin50", emoji = "🎲", name = "진짜 올인", desc = "🎲올인 명령에 코인 누적 50 지출", cat = "경제", tier = "골드", reward = "칭호: 진짜 도박사", req = new[] { new StatReq("cmdCoin_allin", 50L) } },
            new AchDef { id = "cc_lastclear5", emoji = "⏰", name = "마지막 결제", desc = "⏰최후 명령으로 스테이지 5회 클리어", cat = "경제", tier = "골드", reward = "칭호: 막판 결제자", req = new[] { new StatReq("lastClears", 5L) } },
            new AchDef { id = "cc_total100", emoji = "🪙", name = "명령비 지출왕", desc = "🪙특수 명령에 코인 누적 100 지출", cat = "경제", tier = "프리즘", reward = "고급 칭호: 명령비 지출왕", req = new[] { new StatReq("cmdCoinTotal", 100L) } },
            new AchDef { id = "cc_bossallin1", emoji = "💸", name = "비싼 졸업", desc = "👑보스에서 🎲올인을 쓰고 클리어", cat = "히든", tier = "프리즘", hidden = true, reward = "칭호: 비싼 졸업생", req = new[] { new StatReq("bossAllinClear", 1L) } },

            // ══════════════════════════════════════════════════════════
            // ACH-6 빌드 도감 — 25 테마빌드(THEME_BUILDS) 완성을 보상(2026-06-30).
            // 추적: bld_<id> 완성 플래그(evalThemeBuilds→setMax 1) → SlotV2Engine.themeBuildStats() 파생키,
            // composeStat 가 stat 에 머지. 신규 추적/DB 0 — 전부 기존 bld_* 의 순수 파생.
            // 파생키: bldCat_<category>(카테고리별 완성수, 5개 카테고리 각 5빌드) · bldTotal(전체완성수) ·
            // bldAllBasic(완성≥1 카테고리 수, =5 전공) · bldAllMaster(전부완성 카테고리 수, =5 마스터).
            // id 접두 bdx_(기존 bd_* 와 분리). 기존 466종과 (key,threshold)·id 중복 0. 보상=코스메틱(칭호/도감장식/프레임).
            // ══════════════════════════════════════════════════════════
            // ── 카테고리 입문(각 1개 완성) — bldCat_<cat> >= 1 ──
            new AchDef { id = "bdx_intro_growth", emoji = "📈", name = "성장형 빌드 입문", desc = "성장형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 성장형 입문자", req = new[] { new StatReq("bldCat_성장형", 1L) } },
            new AchDef { id = "bdx_intro_fate", emoji = "🔮", name = "운명형 빌드 입문", desc = "운명형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 운명형 입문자", req = new[] { new StatReq("bldCat_운명형", 1L) } },
            new AchDef { id = "bdx_intro_reversal", emoji = "🧗", name = "역전형 빌드 입문", desc = "역전형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 역전형 입문자", req = new[] { new StatReq("bldCat_역전형", 1L) } },
            new AchDef { id = "bdx_intro_combo", emoji = "🔗", name = "조합형 빌드 입문", desc = "조합형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 조합형 입문자", req = new[] { new StatReq("bldCat_조합형", 1L) } },
            new AchDef { id = "bdx_intro_risk", emoji = "☠", name = "위험형 빌드 입문", desc = "위험형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 위험형 입문자", req = new[] { new StatReq("bldCat_위험형", 1L) } },

            // ── 카테고리 마스터(전체 5빌드 완성) — bldCat_<cat> >= 5 ──
            new AchDef { id = "bdx_master_growth", emoji = "📈", name = "성장형 빌드 마스터", desc = "성장형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 성장형 마스터", req = new[] { new StatReq("bldCat_성장형", 5L) } },
            new AchDef { id = "bdx_master_fate", emoji = "🔮", name = "운명형 빌드 마스터", desc = "운명형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 운명형 마스터", req = new[] { new StatReq("bldCat_운명형", 5L) } },
            new AchDef { id = "bdx_master_reversal", emoji = "🧗", name = "역전형 빌드 마스터", desc = "역전형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 역전형 마스터", req = new[] { new StatReq("bldCat_역전형", 5L) } },
            new AchDef { id = "bdx_master_combo", emoji = "🔗", name = "조합형 빌드 마스터", desc = "조합형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 조합형 마스터", req = new[] { new StatReq("bldCat_조합형", 5L) } },
            new AchDef { id = "bdx_master_risk", emoji = "☠", name = "위험형 빌드 마스터", desc = "위험형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "프레임: 위험형 마스터", req = new[] { new StatReq("bldCat_위험형", 5L) } },

            // ── 총완성 마일스톤 — bldTotal ──
            new AchDef { id = "bdx_total5", emoji = "📖", name = "빌드 수집 시작", desc = "테마빌드 누적 5종 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 빌드 수집가", req = new[] { new StatReq("bldTotal", 5L) } },
            new AchDef { id = "bdx_total10", emoji = "📚", name = "빌드 도감 절반", desc = "테마빌드 누적 10종 완성", cat = "빌드도감", tier = "골드", reward = "도감 장식: 빌드 책갈피", req = new[] { new StatReq("bldTotal", 10L) } },
            new AchDef { id = "bdx_total15", emoji = "🗂️", name = "빌드 연구가", desc = "테마빌드 누적 15종 완성", cat = "빌드도감", tier = "골드", reward = "도감 장식: 빌드 인장", req = new[] { new StatReq("bldTotal", 15L) } },
            new AchDef { id = "bdx_total25", emoji = "🏆", name = "빌드 도감 완성", desc = "테마빌드 25종 전부 완성", cat = "빌드도감", tier = "프리즘", reward = "프리즘 칭호: 빌드 도감 완성자", req = new[] { new StatReq("bldTotal", 25L) } },

            // ── 전 카테고리 — bldAllBasic / bldAllMaster ──
            new AchDef { id = "bdx_all_basic", emoji = "🎓", name = "전공 선택 완료", desc = "5개 빌드 카테고리에서 각각 1개+ 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 전공 선택 완료", req = new[] { new StatReq("bldAllBasic", 5L) } },
            new AchDef { id = "bdx_all_master", emoji = "👨‍🏫", name = "잭팟런 교수", desc = "5개 빌드 카테고리를 전부 마스터", cat = "빌드도감", tier = "프리즘", reward = "프리즘 칭호: 잭팟런 교수", req = new[] { new StatReq("bldAllMaster", 5L) } },
        };

        public static AchDef ById(string id) => Array.Find(All, x => x.id == id);
    }
}
