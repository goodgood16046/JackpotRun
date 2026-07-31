using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 도감 화면 — ENGINE_PORT_DESIGN.md S7 "화면 사양" DexView: 카테고리 탭(가로 스크롤 pill), 3열
    // 그리드(아트 300px), 잠금 어둡게+자물쇠, 상세 팝업(아트 512+스탯). 실프로필 진행도 연동(**데모
    // 데이터 제거** — PickView와 동일 원칙: profile.IsCharUnlocked/IsMachineUnlocked/IsDeviceUnlocked,
    // 업적 카테고리는 profile.AchievedIds로 달성 배지 표시). 이관 원본: Scripts/UI/DexScreen.cs·
    // DetailPopup.cs(레이아웃은 버리고 카테고리/락/서브라인/상세 로직만).
    //
    // 상세 팝업은 "DexView 내부"(파일 구성 표 각주의 두 옵션 중 하나) — DexDetailPopup 클래스를 이
    // 파일 하단에 같이 둔다.
    public sealed class DexView : MonoBehaviour
    {
        [SerializeField] private AppRoot appRoot;

        [Header("통계 4타일 — 순서: 최고점수/최고스테이지/런/통산점수")]
        [SerializeField] private Text statBestScoreText;
        [SerializeField] private Text statBestStageText;
        [SerializeField] private Text statRunsText;
        [SerializeField] private Text statTotalScoreText;

        [Header("카테고리 탭 — JackpotCatalog.CategoryOrder와 순서 1:1")]
        [SerializeField] private Image[] tabImages = System.Array.Empty<Image>();

        [SerializeField] private RectTransform gridContent;
        [SerializeField] private RectTransform cardTemplate; // 자식 경로 계약: Icon/Name/Desc/Sub/Lock/Lock-Hint
        [SerializeField] private DexDetailPopup detailPopup;

        private string _cat;

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // DexDetailPopup은 전역 OverlayLayer 산하(DexScreen의 자식이 아님) — 화면을 떠날 때 명시적으로
            // 닫아야 다음 화면 위에 남아있지 않는다(RunView.OnDisable의 동일 조치와 같은 이유).
            detailPopup?.Hide();
        }

        private void OnEnable()
        {
            RefreshStats();
            var order = JackpotCatalog.CategoryOrder;
            SetCategory(order != null && order.Length > 0 ? order[0] : JackpotCatalog.CatChar);
        }

        private void RefreshStats()
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return;
            if (statBestScoreText != null) statBestScoreText.text = NumberFormat.Comma(profile.BestScore);
            if (statBestStageText != null) statBestStageText.text = NumberFormat.Comma(profile.BestStage);
            if (statRunsText != null) statRunsText.text = NumberFormat.Comma(profile.Runs);
            if (statTotalScoreText != null) statTotalScoreText.text = NumberFormat.Comma(profile.TotalScore);
        }

        /// <summary>탭 버튼의 UnityEvent 퍼시스턴트 리스너 대상(UiSceneBuilder가 카테고리 문자열을 인자로 바로 연결).</summary>
        public void SetCategory(string cat)
        {
            _cat = cat;
            UpdateTabHighlight();
            RenderGrid();
        }

        private void UpdateTabHighlight()
        {
            var order = JackpotCatalog.CategoryOrder;
            for (int i = 0; i < tabImages.Length && i < order.Length; i++)
                if (tabImages[i] != null) tabImages[i].color = order[i] == _cat ? UiKit.CardTop : UiKit.PanelBg;
        }

        private void RenderGrid()
        {
            if (gridContent == null || cardTemplate == null) return;
            for (int i = gridContent.childCount - 1; i >= 0; i--)
            {
                var child = gridContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            var entries = JackpotCatalog.ByCategory(_cat);
            if (entries == null) return;
            foreach (var e in entries) BuildCard(e);
        }

        private void BuildCard(CatalogEntry e)
        {
            bool lockable = _cat == JackpotCatalog.CatChar || _cat == JackpotCatalog.CatMac || _cat == JackpotCatalog.CatDev;
            bool unlocked = !lockable || IsUnlocked(e);

            var card = Instantiate(cardTemplate, gridContent);
            card.gameObject.SetActive(true);
            card.name = "Card_" + e.id;

            var sprite = JackpotCatalog.LoadSprite(e);
            var icon = card.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            var iconEmoji = card.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
            if (iconEmoji != null) { iconEmoji.text = e.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

            var nameText = card.Find("Content/Name")?.GetComponent<Text>();
            if (nameText != null) nameText.text = e.nameKo;
            var descText = card.Find("Content/Desc")?.GetComponent<Text>();
            if (descText != null) descText.text = e.descKo;

            string sub = BuildSubline(e);
            var subText = card.Find("Content/Sub")?.GetComponent<Text>();
            if (subText != null)
            {
                subText.text = sub;
                subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }

            var lockRoot = card.Find("Lock");
            if (lockRoot != null)
            {
                lockRoot.gameObject.SetActive(!unlocked);
                var hintText = lockRoot.Find("Content/Hint")?.GetComponent<Text>();
                if (hintText != null)
                    hintText.text = "해금: " + ((e.hasPick && e.pick != null && !string.IsNullOrEmpty(e.pick.unlock)) ? e.pick.unlock : "조건 미정");
            }

            var button = card.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => detailPopup?.Show(e, unlocked));
            }
        }

        private bool IsUnlocked(CatalogEntry e)
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return false;
            if (_cat == JackpotCatalog.CatChar) return profile.IsCharUnlocked(Characters.ById(e.key));
            if (_cat == JackpotCatalog.CatMac) return profile.IsMachineUnlocked(Machines.ById(e.key));
            if (_cat == JackpotCatalog.CatDev) return profile.IsDeviceUnlocked(Devices.ById(e.id));
            return true;
        }

        private string BuildSubline(CatalogEntry e)
        {
            if (_cat == JackpotCatalog.CatRel) return $"가격 {NumberFormat.Comma(e.price)}";
            if (_cat == JackpotCatalog.CatItem) return $"코인 {NumberFormat.Comma(e.coinCost)}";
            if (_cat == JackpotCatalog.CatMac && e.scoreMod >= 0f) return $"점수보정 ×{NumberFormat.Fmt(e.scoreMod)}";
            if (_cat == JackpotCatalog.CatDev)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(e.command)) parts.Add($"명령 .{e.command}");
                parts.Add($"쿨다운 {(e.cooldown == -1 ? "-" : NumberFormat.Comma(e.cooldown))}");
                if (e.rare) parts.Add("희귀");
                return string.Join(" · ", parts);
            }
            if (_cat == JackpotCatalog.CatAch)
            {
                var profile = appRoot != null ? appRoot.Profile : null;
                bool achieved = profile != null && profile.AchievedIds.Contains(e.key);
                return achieved ? "✅ 달성" : "미달성";
            }
            return "";
        }
    }

    // 카탈로그 엔트리 상세 팝업 — 이관 원본 Scripts/UI/DetailPopup.cs(레이아웃은 버리고 필드 조립만).
    // DexView가 카드 클릭 시 이 컴포넌트를 연다(전역 OverlayLayer 산하, UiSceneBuilder가 1개 생성).
    public sealed class DexDetailPopup : MonoBehaviour
    {
        private const float ScaleInDuration = 0.25f; // 설계 미명시 — 팝업 등장 길이 기본값

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text iconEmojiText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text metaText;
        [SerializeField] private Text descText;
        [SerializeField] private Text unlockText;
        [SerializeField] private Text pickRoleEffText;
        [SerializeField] private Text pickBuildTagsText;
        [SerializeField] private Text pickMetersText;
        [SerializeField] private RectTransform pickSection;
        [SerializeField] private Text prosText;
        [SerializeField] private Text consText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            gameObject.SetActive(false);
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        public void Show(CatalogEntry e, bool unlocked)
        {
            if (e == null) return;
            gameObject.SetActive(true);

            var sprite = JackpotCatalog.LoadSprite(e);
            if (iconImage != null) { iconImage.sprite = sprite; iconImage.enabled = sprite != null; }
            if (iconEmojiText != null) { iconEmojiText.text = e.emoji; iconEmojiText.gameObject.SetActive(sprite == null); }

            if (titleText != null) titleText.text = $"{e.emoji} {e.nameKo}";

            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(e.categoryLabel)) metaParts.Add(e.categoryLabel);
            if (!string.IsNullOrEmpty(e.tier)) metaParts.Add(TierLabel(e.tier));
            if (e.price >= 0) metaParts.Add($"가격 {NumberFormat.Comma(e.price)}");
            if (e.coinCost >= 0) metaParts.Add($"코인 {NumberFormat.Comma(e.coinCost)}");
            if (e.scoreMod >= 0f) metaParts.Add($"점수보정 ×{NumberFormat.Fmt(e.scoreMod)}");
            if (!unlocked) metaParts.Add("🔒 미해금");
            if (metaText != null) metaText.text = string.Join(" · ", metaParts);

            if (descText != null) descText.text = e.descKo ?? "";

            if (unlockText != null)
            {
                if (e.unlockReq != null && e.unlockReq.Length > 0)
                {
                    var lines = new List<string>(e.unlockReq.Length);
                    foreach (var req in e.unlockReq) lines.Add($"해금: {req.stat} ≥ {NumberFormat.Comma(req.value)}");
                    unlockText.text = string.Join("\n", lines);
                    unlockText.gameObject.SetActive(true);
                }
                else unlockText.gameObject.SetActive(false);
            }

            bool hasPick = e.hasPick && e.pick != null;
            if (pickSection != null) pickSection.gameObject.SetActive(hasPick);
            if (hasPick) PopulatePick(e.pick);

            if (cardRect != null)
            {
                StopAllCoroutines();
                StartCoroutine(UiTween.ScaleRoutine(cardRect, Vector3.zero, Vector3.one, ScaleInDuration, UiTween.Ease.OutBack));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private void PopulatePick(PickInfo p)
        {
            if (pickRoleEffText != null) pickRoleEffText.text = $"역할: {p.role}\n효과: {p.eff}";

            if (pickBuildTagsText != null)
            {
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(p.build)) lines.Add($"빌드: {p.build}");
                if (p.tags != null && p.tags.Length > 0) lines.Add($"태그: {string.Join(", ", p.tags)}");
                pickBuildTagsText.text = string.Join("\n", lines);
                pickBuildTagsText.gameObject.SetActive(lines.Count > 0);
            }

            if (pickMetersText != null)
                pickMetersText.text = $"난이도 {PickMeta.DiffLabel(p.diff)}   고점 {Stars(p.ceiling)}   안정 {Stars(p.stab)}   위험 {Stars(p.risk)}";

            if (prosText != null)
                prosText.text = (p.pros != null && p.pros.Length > 0) ? string.Join("\n", Prefixed(p.pros, "＋ ")) : "";
            if (consText != null)
                consText.text = (p.cons != null && p.cons.Length > 0) ? string.Join("\n", Prefixed(p.cons, "－ ")) : "";
        }

        private static string TierLabel(string tier)
        {
            switch (tier)
            {
                case "SILVER": return "🥈 실버";
                case "GOLD": return "🥇 골드";
                case "PRISM": return "🌈 프리즘";
                default: return tier;
            }
        }

        private static string Stars(int n) => new string('★', Mathf.Max(0, n));

        private static List<string> Prefixed(string[] items, string prefix)
        {
            var list = new List<string>(items.Length);
            foreach (var s in items) list.Add(prefix + s);
            return list;
        }
    }
}
