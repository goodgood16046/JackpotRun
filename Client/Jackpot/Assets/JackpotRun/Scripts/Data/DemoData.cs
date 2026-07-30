using System.Collections.Generic;

namespace JackpotRun.Data
{
    public class UnlockHint
    {
        public string text;
        public int pct;
        public bool done;
    }

    public class PlayerState
    {
        public string nick;
        public HashSet<string> chars, machines, ownedDevices;
        public Dictionary<string, UnlockHint> charHints, machineHints, deviceHints;
    }

    /// <summary>
    /// 백엔드(RTDB) 연동 전까지 사용하는 데모 데이터.
    /// jackpotpick/app.js demoData() 내용을 그대로 포팅.
    /// </summary>
    public static class DemoData
    {
        public static PlayerState Demo()
        {
            var state = new PlayerState
            {
                nick = "데모플레이어",
                chars = new HashSet<string>
                {
                    "novice", "scholar", "gambler", "farmer", "parttime", "jeweler", "cultist", "lucky", "prodigy"
                },
                machines = new HashSet<string>
                {
                    "basic", "cherry", "library", "gem", "magnet", "skull", "star", "clover"
                },
                ownedDevices = new HashSet<string>
                {
                    "dev_safe", "dev_reroll", "dev_seal", "dev_coin"
                },
                charHints = new Dictionary<string, UnlockHint>
                {
                    { "honor", new UnlockHint { text = "최고점수 8,000 (6,400/8,000) · 보스 5회 (3/5)", pct = 72, done = false } },
                    { "minimalist", new UnlockHint { text = "최고도달 S12 (S9/S12) · 무유물 클리어 1회 (0/1)", pct = 45, done = false } },
                    { "monk", new UnlockHint { text = "최고도달 S6 (S6/S6) · 아슬아슬 3회 (2/3)", pct = 83, done = false } },
                    { "daredevil", new UnlockHint { text = "최고점수 15,000 (6,400/15,000)", pct = 42, done = false } },
                },
                machineHints = new Dictionary<string, UnlockHint>
                {
                    { "crown", new UnlockHint { text = "👑왕관 누적 40 (28/40) · 잭팟 3회 (2/3)", pct = 68, done = false } },
                    { "flame", new UnlockHint { text = "최고점수 15,000 (6,400/15,000) · 최고도달 S10 (S9/S10)", pct = 65, done = false } },
                    { "bomb", new UnlockHint { text = "보스 5회 (3/5) · 최고도달 S10 (S9/S10)", pct = 75, done = false } },
                    { "rainbow", new UnlockHint { text = "최고점수 25,000 (6,400/25,000) · 잭팟 10회 (2/10)", pct = 23, done = false } },
                },
                deviceHints = new Dictionary<string, UnlockHint>
                {
                    { "dev_flame", new UnlockHint { text = "최고점수 50,000 (6,400/50,000) · 최고도달 S20 (S9/S20)", pct = 28, done = false } },
                    { "dev_overheat", new UnlockHint { text = "막판 클리어 10회 (4/10) · 최고점수 20,000 (6,400/20,000)", pct = 36, done = false } },
                    { "dev_oracle", new UnlockHint { text = "최고도달 S15 (S9/S15) · 기도 클리어 3회 (1/3)", pct = 47, done = false } },
                },
            };
            return state;
        }
    }
}
