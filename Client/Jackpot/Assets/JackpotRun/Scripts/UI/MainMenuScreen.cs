using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>메인 메뉴 화면 — 타이틀/부제/프로필 요약/시작 버튼 2개/크레딧.</summary>
    public static class MainMenuScreen
    {
        public static void Build(RectTransform parent, JackpotRunApp app)
        {
            var col = UiFactory.VGroup(parent, 28, new RectOffset(60, 60, 220, 60), controlChildW: true, controlChildH: true);
            UiFactory.Fill(col);

            var title = UiFactory.Text(col, "🎰 잭팟런", 96, UiFactory.Hex("#FFD23F"), TextAnchor.MiddleCenter, true);
            UiFactory.SizeHint(title, preferredHeight: 140);

            var subtitle = UiFactory.Text(col, "Unity 포팅 · 데모 데이터", 34, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
            UiFactory.SizeHint(subtitle, preferredHeight: 60);

            // 프로필 요약 1줄 — ProfileStore.Load(실패 시 내부에서 새 프로필로 안전 폴백).
            var profile = ProfileStore.Load();
            string summary = $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 런 {NumberFormat.Comma(profile.Runs)}회 · " +
                              $"업적 {profile.AchievedIds.Count}/{Achievements.Count}";
            var profileSummary = UiFactory.Text(col, summary, 24, UiFactory.Hex("#34D3C0"), TextAnchor.MiddleCenter);
            UiFactory.SizeHint(profileSummary, preferredHeight: 40);

            // 버튼 사이 여백
            var spacer = UiFactory.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiFactory.SizeHint(spacer, preferredHeight: 80);

            var pickBtn = UiFactory.Button(col, "시작 조합 선택", new Vector2(0, 140), UiFactory.Hex("#34D3C0"), UiFactory.Hex("#0B0E1A"),
                () => app.ShowPick());
            UiFactory.SizeHint(pickBtn, preferredHeight: 140);

            var dexBtn = UiFactory.Button(col, "도감", new Vector2(0, 140), UiFactory.Hex("#5B8CFF"), UiFactory.Hex("#0B0E1A"),
                () => app.ShowDex());
            UiFactory.SizeHint(dexBtn, preferredHeight: 140);

            var footerSpacer = UiFactory.Panel(col, "FooterSpacer", new Color(0, 0, 0, 0));
            UiFactory.SizeHint(footerSpacer, flexibleHeight: 1);

            var credit = UiFactory.Text(col, "잭팟런 Unity 포팅 데모 빌드", 22, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
            UiFactory.SizeHint(credit, preferredHeight: 60);
        }
    }
}
