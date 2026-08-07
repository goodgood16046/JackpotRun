using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // PlayerProfile ↔ 직렬화 가능 DTO 매핑 — 순수 C#(UnityEngine 참조 금지). Assets\JackpotRun\Scripts\Game\
    // ProfileStore.cs(Unity 어댑터)가 이 DTO를 UnityEngine.JsonUtility로 텍스트화한다. ENGINE_PORT_DESIGN.md
    // 원칙 6(저장은 엔진 밖) — 이 파일은 "DTO 매핑"만 담당하고 파일 I/O는 하지 않는다.
    //
    // JsonUtility는 Dictionary를 직렬화하지 못한다(Unity 제약) — PlayerProfile.Stats(Dictionary<string,long>)를
    // 키/값 배열 쌍(statKeys[]/statValues[])으로 풀어 담는다. [System.Serializable]은 순수 BCL 속성이라
    // (UnityEngine이 아님) 여기 붙여도 "Engine엔 UnityEngine 금지" 규칙을 어기지 않는다 — JsonUtility가 이
    // 속성을 보고 필드를 리플렉션 직렬화한다.
    [Serializable]
    public sealed class PlayerProfileDto
    {
        // PlayerProfile.Stats 전체(156개 정식 키 + bld_* 파생 원시 플래그 등) — 순서는 무관, 인덱스로 1:1 대응.
        public string[] statKeys = Array.Empty<string>();
        public long[] statValues = Array.Empty<long>();

        // PlayerProfile.AchievedIds(달성 업적 id 집합) — Kotlin SlotV2AchRow.unlocked CSV 대응.
        public string[] achievedIds = Array.Empty<string>();

        // PlayerProfile.OwnedDevices(면허와 무관하게 영구 보유로 인정된 장치 id) — Kotlin
        // SlotV2ScoreRow.ownedDevices CSV 대응("grandfathered" 포함 개념이나 이 포팅은 순수 신규 저장이라
        // 실질적으로 lic_* 업적 달성 시 AchievementEngine이 여기 추가하는 용도로만 쓰인다).
        public string[] ownedDevices = Array.Empty<string>();

        // SlotV2ScoreRow 잔여 필드(03_meta §3.3) — achievement 판정에 쓰이지 않는 "기록/표시" 전용 필드.
        public long totalScore;      // 통산 누적 점수(리더보드 합산용, bestScore와 별개)
        public string bestChar = ""; // 최고점수 달성 시 캐릭터 id
        public string bestMachine = ""; // 최고점수 달성 시 머신 id
        public long lastPlayedAtUnixMs; // 카톡 전용 startedAt/lastActionAt(§3.1)은 제외 — 이건 프로필(영속) 전용 메타
        public string pinnedChallenge = ""; // 고정한 도전 id(§3.3) — 도전판 진행률 로직 자체는 이 슬라이스 범위 밖(보고 대상)
        public string lastCombo = "";       // 직전 런 조합 CSV "char,machine,device,device2"(§3.3)
    }

    public static class ProfileDto
    {
        public static PlayerProfileDto ToDto(PlayerProfile p)
        {
            if (p == null) return new PlayerProfileDto();

            int n = p.Stats.Count;
            var keys = new string[n];
            var vals = new long[n];
            int i = 0;
            foreach (var kv in p.Stats)
            {
                keys[i] = kv.Key;
                vals[i] = kv.Value;
                i++;
            }

            var achieved = new string[p.AchievedIds.Count];
            p.AchievedIds.CopyTo(achieved);

            var owned = new string[p.OwnedDevices.Count];
            p.OwnedDevices.CopyTo(owned);

            return new PlayerProfileDto
            {
                statKeys = keys,
                statValues = vals,
                achievedIds = achieved,
                ownedDevices = owned,
                totalScore = p.TotalScore,
                bestChar = p.BestChar ?? "",
                bestMachine = p.BestMachine ?? "",
                lastPlayedAtUnixMs = p.LastPlayedAtUnixMs,
                pinnedChallenge = p.PinnedChallenge ?? "",
                lastCombo = p.LastCombo ?? "",
            };
        }

        public static PlayerProfile FromDto(PlayerProfileDto dto)
        {
            var p = new PlayerProfile();
            if (dto == null) return p;

            if (dto.statKeys != null && dto.statValues != null)
            {
                int n = Math.Min(dto.statKeys.Length, dto.statValues.Length);
                for (int i = 0; i < n; i++)
                {
                    var k = dto.statKeys[i];
                    if (string.IsNullOrEmpty(k)) continue;
                    p.Stats[k] = dto.statValues[i];
                }
            }
            if (dto.achievedIds != null)
                foreach (var id in dto.achievedIds)
                    if (!string.IsNullOrEmpty(id)) p.AchievedIds.Add(id);
            if (dto.ownedDevices != null)
                foreach (var id in dto.ownedDevices)
                    if (!string.IsNullOrEmpty(id)) p.OwnedDevices.Add(id);

            p.TotalScore = dto.totalScore;
            p.BestChar = dto.bestChar ?? "";
            p.BestMachine = dto.bestMachine ?? "";
            p.LastPlayedAtUnixMs = dto.lastPlayedAtUnixMs;
            p.PinnedChallenge = dto.pinnedChallenge ?? "";
            p.LastCombo = dto.lastCombo ?? "";
            return p;
        }
    }
}
