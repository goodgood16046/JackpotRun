package com.ashersoft.kakaobot.data

import androidx.room.Entity
import androidx.room.Index

/**
 * 잭팟런 v2 (단일라인 5칸 로그라이크) — 진행 중인 런 상태 (한 사람당 1런, 휘발).
 *
 * 3화폐: stageExp(이번 스테이지 진행) / score(리더보드 누적) / coins(상점, 휘발).
 * 캐릭터+머신 선택 → 스핀 5회 안에 요구 EXP 달성 → 노드 선택 → 반복. 실패=게임오버.
 * v1(slot_run)과 별도 테이블로 병행. ownerKey = "u<userId>"/"n<nick>".
 */
@Entity(tableName = "slot_v2_run", primaryKeys = ["linkId", "ownerKey"])
data class SlotV2RunRow(
    val linkId: Long,
    val ownerKey: String,
    val ownerNick: String,
    val ownerUserId: Long = 0,
    /** CHAR_SELECT / MACHINE_SELECT / SPIN / NODE_SELECT / EVENT_AUGMENT / EVENT_RELIC / EVENT_SHOP / EVENT_ITEMSHOP / EVENT_GAMBLE / EVENT_REST / EVENT_CURSE */
    val state: String = "CHAR_SELECT",
    val charId: String = "",
    val machineId: String = "",
    val stage: Int = 1,
    val spinIndex: Int = 0,        // 이번 스테이지에 쓴 스핀 수
    val stageExp: Long = 0,        // 이번 스테이지 누적 EXP (쿼터 관문)
    val score: Long = 0,           // 런 누적 점수 (리더보드)
    val coins: Long = 0,
    val perks: String = "",        // 증강/유물 id CSV
    val curses: String = "",       // 저주 id CSV
    val items: String = "",        // (예비) 보유 소모성 아이템 id CSV
    val armItems: String = "",     // NEXTSPIN 아이템 — 다음 스핀에 적용 후 소거
    val phaseItems: String = "",   // PHASE 아이템 — 이번 스테이지 내내, 클리어 시 소거
    val stageBonusSpins: Int = 0,  // 이번 스테이지 한정 추가 스핀(응급처치), 클리어 시 0
    val usedCmds: String = "",     // 이번 스테이지에 쓴 특수 스핀명령/장치 CSV, 클리어 시 0
    val device: String = "",       // 장착 장치 id (메인 장치 슬롯 — 모든 장치 가능)
    val device2: String = "",      // 보조 장치 id (극후반 해금 슬롯 — ARMED/PEEK만·약화·계열제한). "" = 미장착/미해금
    val pendingOptions: String = "", // 현재 선택지 직렬화
    val flameNext: Boolean = false,  // 다음 스핀 EXP -50%
    val seedNext: Boolean = false,   // 다음 스핀 씨앗 성장
    // ── 장치(직전결과 조작/예언/과열) 상태 ──
    val lastCells: String = "",      // 직전 스핀 원시 심볼 id CSV (재굴림/고정/복사/교체)
    val lastGain: Long = 0,          // 직전 스핀이 더한 EXP (조작 시 되돌림)
    val lastScoreGain: Long = 0,     // 직전 스핀이 더한 점수
    val lastCoinGain: Int = 0,       // 직전 스핀이 더한 코인
    val lastSet4: Int = 0,           // 직전 스핀이 runSet4 에 더한 기여(0/1) — 재굴림/조작 교체 시 net-adjust(되돌림)
    val lastAdjPairs: Int = 0,       // 직전 스핀이 runAdjPairs 에 더한 기여(0/1) — 재굴림/조작 교체 시 net-adjust(되돌림)
    val lastSpinNo: Int = -1,        // 직전 스핀의 spinIndex(0-base), -1=없음
    val pendingNextExpMul: Double = 1.0, // 다음 스핀 EXP 배수(과열 여파 등), 적용 후 1.0
    val lockedNext: String = "",     // 예언으로 확정된 다음 스핀 원시 심볼 id CSV
    val devCooldown: Int = 0,        // 장치 충전(쿨다운) 남은 스테이지 (점화)
    // ── 도파민: 런 종료 리포트용 통계 ──
    val runJackpots: Int = 0,        // 이번 런 잭팟 횟수
    val runBestSpin: Long = 0,       // 이번 런 한 스핀 최고 EXP
    val displayMode: String = "NORMAL", // 표시 모드: SIMPLE(간단)/NORMAL(상세)
    val runSymCounts: String = "",   // 이번 런 심볼 등장수 "id:n,id:n" (실패 리포트 최다심볼)
    val unluckyGauge: Int = 0,       // 불운 게이지(나쁜 스핀 누적) — 가득 차면 다음 보상 희귀↑ 보장
    val closestClear: Int = -1,      // 이번 런 가장 아슬아슬한 클리어 마진(초과EXP 최소), -1=없음
    val survive: Boolean = false,    // (f) 보험증서 — 이번 스테이지 실패 1회 생존
    val debtStages: Int = 0,         // (i) 빚문서 — 남은 무보상 스테이지 수
    val phasePerks: String = "",     // (k) 깨진프리즘 — 이번 스테이지 한정 임시 perk CSV(클리어 소거)
    val heldAug: String = "",        // (P7·dev_holdfile 보류파일) 보관 중인 증강 후보 id 1개("" = 없음). 다음 증강 노드서 새 후보와 함께 비교. 런종료/클리어 시 소거 가능.
    val usedItemThisRun: Boolean = false, // 이번 런에 아이템을 1개라도 썼는지 (수도승 '아이템 없이 S8' 런조건 트래킹)
    // ── v68 신규 빌드 축: 런 추적 카운터 (전부 default 0) ──
    val runAdjPairs: Int = 0,        // 인접쌍 보너스 발동 횟수 (연쇄반응 bld_chain)
    val runPrayWins: Int = 0,        // 기도 성공 횟수 (운명의손 bld_fate_hand)
    val runLastSpinClears: Int = 0,  // 막스핀 클리어 횟수 (벼랑끝합격 bld_cliff_pass)
    val runCloseClears: Int = 0,     // 아슬아슬(부족≤10) 클리어 횟수
    val runFastClears: Int = 0,      // 남은스핀≥2 클리어 횟수 (빠른입학 bld_fast_start)
    val runSet4: Int = 0,            // 세트4+ 발동 횟수 (끌어당기는졸업 bld_magnet_grad)
    // ── v68 신규 증강 상태 (전부 default 0) ──
    val growthStack: Int = 0,        // 성장일지 스택 (0~5, 클리어 누적·실패 리셋)
    val snowStack: Int = 0,          // 눈덩이 스택 (0~4, 남은스핀≥2 클리어 누적·보스후 -1)
    val fateBellUsed: Int = 0,       // 운명의종 사용 여부 (0/1, 런 1회)
    // ── v70 런 제한도전 플래그 (무명령/무조작 챌린지 추적, 전부 default 0) ──
    val runUsedCmd: Int = 0,         // 이번 런 스핀명령(focus/allin/pray/last 등) 사용=1
    val runRerolled: Int = 0,        // 이번 런 재굴림/고정/복사/교체 장치 사용=1
    val startedAt: Long = 0,
    val lastActionAt: Long = 0,
)

/** 잭팟런 v2 업적/누적 카운터 (플레이어별 영구 누적). */
@Entity(
    tableName = "slot_v2_ach",
    primaryKeys = ["linkId", "ownerKey"],
    indices = [Index(value = ["linkId", "userId"])],
)
data class SlotV2AchRow(
    val linkId: Long,
    val ownerKey: String,
    val ownerNick: String = "",
    val userId: Long? = null,
    val cherryTotal: Long = 0,
    val crownTotal: Long = 0,
    val jackpots: Long = 0,
    val bossClears: Long = 0,
    val lastSpinClears: Long = 0,
    val exactClears: Long = 0,
    val prismPicks: Long = 0,
    val bestStage: Long = 0,
    val runs: Long = 0,
    val bestScore: Long = 0,
    val unlocked: String = "",   // 달성한 업적 id CSV
    val counters: String = "",   // 확장 카운터 맵 "key:val,key:val" (bookTotal/deviceUses/closeClears…)
    val lastAt: Long = 0,
)

/** 잭팟런 v2 최고기록 + 통산 (리더보드 — user_points 무관). */
@Entity(
    tableName = "slot_v2_score",
    primaryKeys = ["linkId", "nickname"],
    indices = [Index(value = ["linkId", "userId"])],
)
data class SlotV2ScoreRow(
    val linkId: Long,
    val nickname: String,
    val bestScore: Long = 0,
    val totalScore: Long = 0,
    val runs: Int = 0,
    val bestStage: Int = 0,
    val bestChar: String = "",
    val bestMachine: String = "",
    val lastPlayedAt: Long = 0,
    val userId: Long? = null,
    /** 영구 소지 장치 id CSV — 런/업적으로 획득, 런 끝나도 유지. 시작 시 장착 선택. */
    val ownedDevices: String = "",
    /** 고정한 도전 id (배치3a, P4) — "목표 <번호>" 로 1개 고정. 리셋/만료 없음. 빈="" = 미고정. */
    val pinnedChallenge: String = "",
    /** 직전 런 조합 CSV "char,machine,device,device2" (지시서11-B 같은조합 재도전). recordRun 시 저장. 빈="" = 없음. */
    val lastCombo: String = "",
)
