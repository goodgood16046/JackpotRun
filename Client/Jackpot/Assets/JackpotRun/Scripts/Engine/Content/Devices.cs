using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 장치 16종 — SlotV2Engine.kt L1022-1064 (DEVICES) 전사.
    // kind 값은 Kotlin DevKind enum 이름 그대로("PASSIVE"|"ARMED"|"MANIP"|"PEEK"|"INSTANT").
    // fx 딕셔너리는 applyPassiveDevice(L1121-1127)·applyItemMods 내 "dev_coin" case(L1551)에 실제
    // Mods 오버레이가 있는 장치만 채운다. MANIP(dev_reroll/dev_pin/dev_copy/dev_swap)·PEEK(dev_oracle/
    // dev_syllabus)·INSTANT(dev_bell)·dev_safe·dev_holdfile/dev_retake/dev_major는 SlotV2Engine.kt 안에
    // 셀/게이지 조작 로직이 없다(서비스 레이어 처리 — 01_engine.md §8.2/§8.4 주석) → fx 빈 값.
    // cmd/needsArg/cooldown은 DeviceDef 계약(ENGINE_PORT_DESIGN.md)에 필드가 없어 이 파일에서 다루지 않는다
    // — MANIP 계열 "EXP -10%·N코인" 비용도 Kotlin Device 데이터클래스에 없이 desc 텍스트에만 존재하는
    // 수치라 원본 그대로 구조화하지 않았다(01_engine.md §8 각주·부록A-6, S4 DeviceActions.cs에서 별도 상수화 필요).
    //
    // ── unlockAch 웹 파리티 갱신(WEB_PARITY_DESIGN.md P3-2) ──────────────────────────────────
    // 아래 12종(dev_flame~dev_bell)의 unlockAch는 원래 Kotlin lic_* 면허 업적 id였으나(구 482종
    // Achievements.cs 시절 값), 업적 34종 교체에 맞춰 웹 ACH_DEVICE_REWARD(data.js:818-828) 매핑으로
    // 바꿨다 — 12종 전부 웹 매핑의 "기본" 절반과 정확히 1:1 대응한다(devicesuffix 기준 동일 12종:
    // flame/seal/safe/overheat/subreel/coin/reroll/pin/copy/swap/oracle/bell). AchievementEngine.
    // Evaluate가 "unlockAch == 방금 달성한 업적 id"인 장치를 범용으로 찾아 영구 지급한다(lic_ 접두
    // 특례 제거 — Achievements.cs 헤더 각주 참조).
    // dev_syllabus/dev_holdfile/dev_retake/dev_major(P7 증강 보조 계열) 4종은 웹 DEVICES에 대응 항목이
    // 없다(WEB_PARITY_DESIGN.md §1-B "웹에 없음 — 유지"). Opus 2차 검수·Fable 결정(2026-08-08, §2-C
    // (C-1) 갱신) — "P3에서 정리 판단" 유예를 이번 슬라이스에서 확정했다: 이 4종은 **드랍 전용 장치로
    // 전환**한다. unlockAch를 빈 문자열로 바꿔 업적 해금 경로를 아예 없앴다(구 482종 확장 업적 id
    // prismPick1/item10/shop50/runs50는 새 34종에 존재하지 않아 어차피 영구 미달성이었다 — 그 죽은
    // 참조를 정리한 것). 대신 P1에서 이미 구현된 런 중 드랍(DEVICE 노드/EVENT-6, NodeEvents.cs →
    // RunEvent.deviceGrantedId → StatTracker → PlayerProfile.OwnedDevices, PlayerProfile.cs OwnedDevices
    // 필드 각주 참조)만으로 영구 획득한다 — PlayerProfile.IsDeviceUnlocked는 unlockAch가 비어 있으면
    // OwnedDevices 소속 여부만 보므로(IsDeviceUnlocked 각주) 별도 코드 변경 없이 안전하게 동작한다.
    public static class Devices
    {
        public const int Count = 16;

        public static readonly DeviceDef[] All =
        {
            // ── 패시브 (장착하면 매 스핀 자동, 명령어 없음) ──
            new DeviceDef
            {
                id = "dev_flame", name = "불꽃엔진", emoji = "🔥", desc = "장착 시 모든 스핀 EXP +15%",
                kind = "PASSIVE", rare = true, unlockAch = "score50k", // 웹 ACH_DEVICE_REWARD: score50k→dev_flame
                fx = new Dictionary<string, double> { ["expMul"] = 1.15 },
            },
            new DeviceDef
            {
                id = "dev_seal", name = "봉인장막", emoji = "🔒", desc = "장착 시 모든 스핀 ☠해골 미등장 · EXP +5%",
                kind = "PASSIVE", rare = false, unlockAch = "crown10", // 웹 ACH_DEVICE_REWARD: crown10→dev_seal
                fx = new Dictionary<string, double> { ["expMul"] = 1.05, ["symbolWeightMul.skull"] = 0.0 },
            },
            new DeviceDef
            {
                id = "dev_safe", name = "안전벨트", emoji = "🦺", desc = "장착 시 모든 스핀 최소 EXP 보장(폭망 방지)",
                kind = "PASSIVE", rare = false, unlockAch = "cherry100", // 웹 ACH_DEVICE_REWARD: cherry100→dev_safe
                // applyPassiveDevice의 when-block에 case 없음(else -> base) — 하한 보장은 서비스가 별도 처리.
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_overheat", name = "과열코어", emoji = "♨️", desc = "장착 시 모든 스핀 EXP +18%·☠해골 +1 등장(고위험)",
                kind = "PASSIVE", rare = true, unlockAch = "lastclear5", // 웹 ACH_DEVICE_REWARD: lastclear5→dev_overheat
                fx = new Dictionary<string, double> { ["expMul"] = 1.18, ["weightAdd.skull"] = 1.0 },
            },
            new DeviceDef
            {
                id = "dev_subreel", name = "보조릴", emoji = "➕", desc = "장착 시 항상 6칸 슬롯 · 최종 EXP -30%",
                kind = "PASSIVE", rare = true, unlockAch = "jackpot1", // 웹 ACH_DEVICE_REWARD: jackpot1→dev_subreel
                // reel+1(6칸 확장) 자체는 서비스가 처리, 여기서는 최종 EXP -30% 페널티만.
                fx = new Dictionary<string, double> { ["expMul"] = 0.7 },
            },

            // ── 능동 (명령어) ──
            new DeviceDef
            {
                id = "dev_coin", name = "코인투입구", emoji = "🪙", desc = "코인5 소모 → 다음 스핀 EXP +30%",
                kind = "ARMED", rare = false, unlockAch = "score10k", // 웹 ACH_DEVICE_REWARD: score10k→dev_coin
                fx = new Dictionary<string, double> { ["expMul"] = 1.3 },
            },
            new DeviceDef
            {
                id = "dev_reroll", name = "재굴림기", emoji = "🔄", desc = "직전 스핀 결과 다시 굴림 (EXP -10% · 3🪙)",
                kind = "MANIP", rare = false, unlockAch = "boss1", // 웹 ACH_DEVICE_REWARD: boss1→dev_reroll
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_pin", name = "고정핀", emoji = "📌",
                desc = "직전 결과 N번 칸 유지·나머지 재굴림 (EXP -10% · 3🪙, 예: 고정 3)",
                kind = "MANIP", rare = false, unlockAch = "exact1", // 웹 ACH_DEVICE_REWARD: exact1→dev_pin
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_copy", name = "복사기", emoji = "📑",
                desc = "직전 결과 N번 칸을 옆칸에 복사 (EXP -10% · 5🪙, 예: 복사 3)",
                kind = "MANIP", rare = true, unlockAch = "prism5", // 웹 ACH_DEVICE_REWARD: prism5→dev_copy
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_swap", name = "교체기", emoji = "🔃",
                desc = "직전 결과 N번 칸을 최다 심볼로 교체 (EXP -10% · 5🪙, 예: 교체 2)",
                kind = "MANIP", rare = true, unlockAch = "boss5", // 웹 ACH_DEVICE_REWARD: boss5→dev_swap
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_oracle", name = "예언안경", emoji = "🔮", desc = "다음 스핀을 미리 보고 확정",
                kind = "PEEK", rare = true, unlockAch = "stage10", // 웹 ACH_DEVICE_REWARD: stage10→dev_oracle
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_bell", name = "비상졸업벨", emoji = "🔔", desc = "부족 EXP ≤25면 즉시 클리어 (1회 파괴)",
                kind = "INSTANT", rare = true, unlockAch = "runs20", // 웹 ACH_DEVICE_REWARD: runs20→dev_bell
                fx = new Dictionary<string, double>(),
            },

            // ── P7 증강 보조 계열 — 웹에 없는 Unity 전용 드랍 전용 장치. 업적 해금 없음, 런 중 장치
            // 드랍(P1 DEVICE 노드/EVENT-6)으로만 영구 획득한다(unlockAch="", 파일 헤더 각주 참조). ──
            new DeviceDef
            {
                id = "dev_syllabus", name = "강의계획서", emoji = "📋",
                desc = "장착 시 증강/유물 선택에 '예상 티어' 사전 안내(파워 변화 없음·정보형)",
                kind = "PEEK", rare = false, unlockAch = "",
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_holdfile", name = "보류파일", emoji = "🗂️",
                desc = "증강 선택 후보 1개를 보관 → 다음 증강 노드에서 새 후보와 함께 비교(보류 N)",
                kind = "ARMED", rare = false, unlockAch = "",
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_retake", name = "재시험관", emoji = "🔁",
                desc = "증강 선택지를 코인 소모로 1회 다시 뽑기(스테이지당 1회)",
                kind = "ARMED", rare = true, unlockAch = "",
                fx = new Dictionary<string, double>(),
            },
            new DeviceDef
            {
                id = "dev_major", name = "전공신청서", emoji = "🎓",
                desc = "장착 시 주력 계열 증강 등장확률 소폭↑(직접 파워↑ 아님·메인슬롯 전용)",
                kind = "PASSIVE", rare = false, unlockAch = "",
                fx = new Dictionary<string, double>(),
            },
        };

        public static DeviceDef ById(string id) => Array.Find(All, x => x.id == id);
    }
}
