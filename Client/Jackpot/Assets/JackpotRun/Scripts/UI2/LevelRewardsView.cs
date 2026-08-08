using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 레벨 보상 화면 — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 B, 웹 단독판 renderLevelRewards
    // ui.js:635-646) 이식. 레벨 카드(비클릭형, MenuView의 클릭형 카드와 동일 골격을 UiSceneBuilder.
    // BuildLevelCard가 공유) + PlayerProfile.LevelUnlocks() 로드맵(캐릭/슬롯/장치/증강/유물이 레벨순,
    // §1-A #9 P3-4에서 엔진에 이미 준비돼 있었다 — 이 슬라이스는 화면만 신설)을 나열한다. RankView.cs와
    // 동일한 "템플릿 clone" 패턴(런타임 코드생성 없음).
    //
    // 자식 경로 계약(Editor/UiSceneBuilder.cs BuildLevelRoadRowTemplate이 이 형태로 짓는다): 행 루트에
    // 배경 Image, "Content/Lv"·"Content/Label"·"Content/Check" 각 Text(BuildRankRowTemplate과 동일 관례).
    public sealed class LevelRewardsView : MonoBehaviour
    {
        private AppRoot appRoot => AppRoot.Instance;

        [SerializeField] private Text levelBadgeText;
        [SerializeField] private Text levelXpText;
        [SerializeField] private RectTransform levelBarFill;
        [SerializeField] private Image levelBarFillImage;
        [SerializeField] private Text roadCountText; // "n/m" — 웹 "레벨 해금 보상 n/m"(자물쇠 이모지 생략)의 숫자 부분
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RectTransform rowTemplate;
        // 웹 roadHtml || '해금 항목 없음' 폴백(ui.js:640) — 실제로는 PlayerProfile.LevelUnlocks()가
        // unlockLevel 있는 콘텐츠(캐릭/슬롯/장치/증강/유물)를 항상 갖고 있어 도달 불가 경로에 가깝지만,
        // 방어적으로 웹과 동일하게 빈 상태 문구를 준비해 둔다(Opus 2차검수 정리).
        [SerializeField] private Text emptyText;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (emptyText != null) emptyText.gameObject.SetActive(false);
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return;

            var lp = profile.LevelProgress();
            if (levelBadgeText != null) levelBadgeText.text = $"Lv.{lp.Level}";
            if (levelXpText != null)
                levelXpText.text = lp.Max ? "MAX" : $"{NumberFormat.Comma(lp.InLevel)} / {NumberFormat.Comma(lp.Need)} XP";
            if (levelBarFill != null)
            {
                float pct = lp.Max ? 1f : (float)lp.Ratio;
                levelBarFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            }
            // 웹은 MAX 레벨이라고 바 색을 바꾸지 않는다(무색 — 항상 Accent, Opus 2차검수 정리) — 그대로 맞춘다.
            if (levelBarFillImage != null) levelBarFillImage.color = UiKit.Accent;

            var road = profile.LevelUnlocks();
            int unlockedCount = 0;
            for (int i = 0; i < road.Count; i++) if (road[i].Unlocked) unlockedCount++;
            if (roadCountText != null) roadCountText.text = $"{unlockedCount}/{road.Count}";

            ClearRows();
            if (road.Count == 0)
            {
                if (emptyText != null) emptyText.gameObject.SetActive(true);
            }
            else
            {
                for (int i = 0; i < road.Count; i++) BuildRow(i, road[i]);
            }
        }

        // 기존 행 제거(rowTemplate·emptyText 제외) — RankView.ClearRows와 동일 패턴.
        private void ClearRows()
        {
            if (emptyText != null) emptyText.gameObject.SetActive(false);
            if (listContent == null) return;
            for (int i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i);
                // emptyText는 Text(Component)라 Transform인 child와 직접 비교할 수 없다 — emptyText.transform으로 비교.
                if (child == rowTemplate || (emptyText != null && child == emptyText.transform)) continue;
                Destroy(child.gameObject);
            }
        }

        private void BuildRow(int index, PlayerProfile.LevelUnlockEntry entry)
        {
            if (listContent == null || rowTemplate == null || entry == null) return;

            var row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            row.name = "Road_" + index;

            var bg = row.GetComponent<Image>();
            if (bg != null) bg.color = entry.Unlocked ? UiKit.CardTop : UiKit.PanelBg;

            var lvText = row.Find("Content/Lv")?.GetComponent<Text>();
            if (lvText != null)
            {
                lvText.text = $"Lv.{entry.Level}";
                lvText.color = entry.Unlocked ? UiKit.Accent : UiKit.TextSecondary;
            }

            var labelText = row.Find("Content/Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                // entry.Label은 엔진이 웹 원문처럼 이모지를 이름 앞에 박아 만든다(예: 캐릭터 카테고리
                // 이모지+이름+"(캐릭터)") — 대부분 astral이라 레거시 Text가 렌더링 못한다(S8 항목⑤
                // 그대로). 표시 직전에만 걸러낸다
                // (GameOverPanel/DexView와 동일 관례 — 데이터 원문은 PlayerProfile.LevelUnlocks() 그대로).
                labelText.text = TextSanitize.StripAstral(entry.Label).TrimStart();
                labelText.color = entry.Unlocked ? UiKit.TextPrimary : UiKit.TextSecondary;
            }

            var checkText = row.Find("Content/Check")?.GetComponent<Text>();
            if (checkText != null)
            {
                // 웹의 자물쇠 이모지(열림/잠김)는 astral이라 렌더링되지 않아 한글 라벨로 대체(DexView
                // "[미해금]"/PickView "잠김"과 동일 관례 — 잠금 이모지 대신 텍스트+색으로 상태를 표시).
                checkText.text = entry.Unlocked ? "해금" : "잠김";
                checkText.color = entry.Unlocked ? UiKit.Good : UiKit.TextSecondary;
            }
        }
    }
}
