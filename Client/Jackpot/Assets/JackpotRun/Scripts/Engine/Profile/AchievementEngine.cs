using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 업적 판정 — WEB_PARITY_DESIGN.md P3-2(§1-A #10, §2-C) 이식: 웹 판정 방식(game.js:2569-2613,
    // "런 종료 시 counters[a.key] >= a.th 일괄 평가")과 이 파일의 Evaluate를 일치시킨다. Achievements.cs가
    // 482종(구 Kotlin 스냅샷)에서 웹 34종으로 교체되며 파생키 계산도 함께 축소했다 — 아래 ComposeStat 각주 참조.
    //
    // [이 슬라이스에서 제거한 구 파생키] lic_dev_*(12종, 구 "면허" 업적 전용) · bldCat_*/bldTotal/
    // bldAllBasic/bldAllMaster(구 "빌드도감" 업적 전용) · accountLevel(ComposeStat 반환값 자체를 읽는
    // 곳이 이 파일 자신의 옛 테스트뿐이었다 — Shop.cs의 Formulas.AccountLevel(stat) 호출은 achievements
    // 인자 없이 별도로 이뤄져 이 값을 쓰지 않는다, GameSession.cs 헤더 주석 참조). 새 34종 중 어느
    // req.key도 이 파생키들을 가리키지 않고(전수 확인), Character/Machine unlockReq도 마찬가지라 안전하게
    // 제거했다 — Formulas.AccountExp/AccountLevel 함수 자체는 그대로 살아있다(Shop.PerkGate가 원재료
    // Stats로 직접 호출 중, 전공/퍽 게이트 폐기는 다음 슬라이스).
    // [P8 §2 스윕에서 제거 완료] distinctCharS10 — 당시엔 "Characters.cs 'prodigy' unlockReq가 여전히
    // 참조한다"며 유지했으나, 같은 날짜의 후속 슬라이스(WEB_PARITY P3-4, §2-(O))가 Character/Machine의
    // unlockReq(StatReq AND)를 웹 OR 5축(unlockRuns/Score/Stage/Level/Ach)으로 전면 교체하면서
    // prodigy도 data.js:161 그대로 `unlockStage=9 OR unlockAch="stage10"`로 바뀌어 이 파생키를 더는
    // 참조하지 않게 됐다(Tests_S5.cs Tests_S5_CharUnlockDerivedKeyGate가 그 이관을 이미 검증 — "죽은
    // 게이트"라고 자체 주석까지 남겼었다). 34종 achievements의 req.key 어디도 이 값을 가리키지 않고
    // (Achievements.cs 전수 확인), Character/Machine unlockReq도 이제 이 필드 자체가 없어 참조 불가능
    // 하다 — 소비처가 완전히 사라진 죽은 계산이라 이번 슬라이스에서 최종 제거했다.
    public static class AchievementEngine
    {
        // ── composeStat 이식 — PlayerProfile.Stats(원재료) 그대로 복사한 딕셔너리를 반환한다(더는
        // 파생키가 없다 — 위 각주 참조). PlayerProfile.Stats 자체는 건드리지 않는다. ──
        public static Dictionary<string, long> ComposeStat(PlayerProfile profile)
        {
            return new Dictionary<string, long>(profile.Stats);
        }

        // ── 신규 달성 업적 판정 — 웹 game.js:2604-2612 "if (!p.unlocked.includes(a.id) &&
        // (cnt[a.key]||0) >= a.th)" 이식. 새로 달성된 AchDef를 profile.AchievedIds에 반영하고, 대응
        // 장치가 있으면(웹 ACH_DEVICE_REWARD, Achievements.cs 헤더 각주) profile.OwnedDevices에 영구
        // 등록한다. ──
        public static List<AchDef> Evaluate(PlayerProfile profile)
        {
            var composed = ComposeStat(profile);
            var newly = new List<AchDef>();

            var all = Achievements.All;
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                if (profile.AchievedIds.Contains(a.id)) continue;
                if (a.req == null || a.req.Length == 0) continue; // L4(Opus 1차 검수) — 방어적 가드
                var req = a.req[0];
                long cur = composed.TryGetValue(req.key, out var v) ? v : 0L;
                if (cur < req.value) continue;

                profile.AchievedIds.Add(a.id);
                newly.Add(a);

                // 웹 ACH_DEVICE_REWARD(data.js:818-828) 대응 — Devices.cs의 unlockAch가 이미 "달성
                // 시 지급할 업적 id"를 직접 담고 있다(웹의 역방향 매핑과 동일 관계) — lic_ 접두 특례
                // 없이 범용으로 찾는다. 매칭되는 장치가 없으면(대부분의 34종) 아무 일도 하지 않는다.
                var dev = Array.Find(Devices.All, d => d.unlockAch == a.id);
                if (dev != null) profile.OwnedDevices.Add(dev.id);

                // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 game.js:2609-2611 ACH_SYMBOL_UNLOCK)
                // — 심화 업적 13종 중 대응 항목이 있으면 심볼 해금(HashSet.Add가 자연히 멱등).
                if (DeepSymbolUnlock.ByAchId.TryGetValue(a.id, out var symId))
                    profile.SymUnlocked.Add(symId);
            }

            return newly;
        }
    }
}
