using System;
using System.IO;
using JackpotRun.Engine;
using UnityEngine;

namespace JackpotRun.Game
{
    // Unity 저장 어댑터 — ENGINE_PORT_DESIGN.md 원칙 6("저장은 엔진 밖: 엔진은 인메모리 상태만, Unity
    // 어댑터가 JsonUtility DTO로 영속화"). Assets\JackpotRun\Scripts\Engine\** 밖(namespace JackpotRun.Game)
    // 이라 UnityEngine 참조가 허용된다 — dotnet EngineTests 빌드에는 이 파일이 포함되지 않는다(csproj가
    // Engine\**\*.cs만 컴파일). 순수 변환 로직(PlayerProfile↔DTO 매핑)은 Engine\Profile\ProfileDto.cs에
    // 있고, 이 파일은 그 DTO를 JsonUtility로 텍스트화 + 파일 I/O만 담당한다.
    public static class ProfileStore
    {
        private const string FileName = "jackpotrun_profile.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // 저장된 프로필이 없거나(최초 실행) 로드에 실패하면(파일 손상 등) 빈 새 프로필로 안전하게
        // 폴백한다 — 저장 실패와 마찬가지로 예외를 호출측까지 전파하지 않는다(Unity 어댑터 원칙).
        public static PlayerProfile Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return new PlayerProfile();

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return new PlayerProfile();

                var dto = JsonUtility.FromJson<PlayerProfileDto>(json);
                return dto != null ? ProfileDto.FromDto(dto) : new PlayerProfile();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileStore] 프로필 로드 실패 — 새 프로필로 시작합니다: {ex.Message}");
                return new PlayerProfile();
            }
        }

        // 임시파일에 먼저 쓰고 교체(저장 도중 강종/크래시로 인한 파일 손상 방지) — 실패 시 false를 반환하고
        // 예외를 던지지 않는다(호출측이 재시도/알림 여부를 판단).
        // M3(Opus 1차 검수, 2026-07-31): 기존 코드는 File.Delete(path) 다음에 File.Move(tmpPath, path)를
        // 별도 호출했다 — 그 사이에 강종되면 path도 tmpPath도 온전한 자리에 없어 세이브가 통째로 유실될
        // 수 있었다. File.Replace(source, destination, null)은 "기존 파일을 새 파일 내용으로 교체"를 단일
        // 파일시스템 연산으로 수행해(백업 경로 null=백업 안 만듦) 그 gap을 없앤다. 대상 파일이 아직 없으면
        // (최초 저장) Replace가 실패하므로 그 경우만 Move를 쓴다. File.Replace는 netstandard2.0부터 있는
        // API라 Unity(.NET Standard 2.1 프로필)에서도 사용 가능하다.
        public static bool Save(PlayerProfile profile)
        {
            if (profile == null) return false;
            try
            {
                // L2(Opus 1차 검수): 저장 시점 = "마지막 플레이 시각"으로 스탬프. Engine\Profile\PlayerProfile.cs
                // 는 순수 C#이라 DateTime을 쓰지 않지만(엔진은 게임 로직에 벽시계 의존 금지), 여기는 Unity
                // 어댑터 계층이라 허용된다(원칙 6 — 저장은 엔진 밖).
                profile.LastPlayedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var dto = ProfileDto.ToDto(profile);
                string json = JsonUtility.ToJson(dto);

                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmpPath = path + ".tmp";
                File.WriteAllText(tmpPath, json);
                if (File.Exists(path)) File.Replace(tmpPath, path, null);
                else File.Move(tmpPath, path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileStore] 프로필 저장 실패: {ex.Message}");
                return false;
            }
        }

        // 저장 파일 존재 여부(최초 실행 판별 등에 사용) — 예외 시 "없음"으로 안전하게 취급.
        public static bool Exists()
        {
            try { return File.Exists(FilePath); }
            catch { return false; }
        }
    }
}
