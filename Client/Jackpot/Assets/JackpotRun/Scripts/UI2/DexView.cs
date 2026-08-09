using System;
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
        // AppRoot는 DontDestroyOnLoad 싱글턴(S8)이라 씬에 없다 — SceneBuilder가 와이어링할 수 없으므로
        // 정적 인스턴스를 계산 프로퍼티로 읽는다(호출부는 그대로 "appRoot.XXX").
        private AppRoot appRoot => AppRoot.Instance;

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
            // S12c §3 — S12 §0 토큰만 사용(CardTop은 pick.css 전용 파생색이라 정리 대상, .dex-tab.on의
            // 배경색 자체는 §0 표에 없는 커스텀 그라데이션이라 가장 가까운 톤 단계인 Panel3로 근사).
            var order = JackpotCatalog.CategoryOrder;
            for (int i = 0; i < tabImages.Length && i < order.Length; i++)
                if (tabImages[i] != null) tabImages[i].color = order[i] == _cat ? UiKit.Panel3 : UiKit.PanelBg;
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
            // 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20) — 심볼 탭도 "미발견 ???" 마스킹 대상에
            // 추가(IsUnlocked가 이미 실해금 값을 계산하지만, 이 플래그가 없으면 잠금 오버레이 자체가
            // 안 그려져 항상 해금된 것처럼 보인다).
            bool lockable = _cat == JackpotCatalog.CatChar || _cat == JackpotCatalog.CatMac || _cat == JackpotCatalog.CatDev
                || _cat == JackpotCatalog.CatSym;
            bool unlocked = !lockable || IsUnlocked(e);

            var card = Instantiate(cardTemplate, gridContent);
            card.gameObject.SetActive(true);
            card.name = "Card_" + e.id;

            var sprite = JackpotCatalog.LoadSprite(e);
            var icon = card.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            var iconEmoji = card.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
            if (iconEmoji != null) { iconEmoji.text = e.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

            // jackpotdex/style.css .card.masked 재해석 — 잠긴 카드는 이름/설명을 "❓ ???"로 가린다
            // (해금 조건은 Lock 오버레이 하단에 별도 표시).
            // Opus 2차 검수 저⑤ — catalog.json descKo(웹 원문 유래, 예: "🍒체리 누적 100개")는 문장
            // 중간에 astral 이모지가 박혀 있을 수 있어 표시 직전 TextSanitize.StripAstral로 거른다
            // (Core/TextSanitize.cs 헤더 참조 — catalog 데이터 자체는 미수정).
            var nameText = card.Find("Content/Name")?.GetComponent<Text>();
            if (nameText != null) nameText.text = unlocked ? TextSanitize.StripAstral(e.nameKo) : "❓ ???";
            var descText = card.Find("Content/Desc")?.GetComponent<Text>();
            if (descText != null) descText.text = unlocked ? TextSanitize.StripAstral(e.descKo) : "미해금 — 조건을 확인하세요";

            string sub = unlocked ? BuildSubline(e) : "";
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
                if (hintText != null) hintText.text = "해금: " + BuildLockHint(e);
            }

            var button = card.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => detailPopup?.Show(e, unlocked));
            }
        }

        // WEB_PARITY P3-2(Fable 결정 4번) — 장치는 unlockAch가 비어 있으면(dev_syllabus/dev_holdfile/
        // dev_retake/dev_major, Devices.cs 헤더 각주) 업적 해금 경로 자체가 없는 드랍 전용 장치다.
        // 그 4종은 catalog.json의 pick도 항상 null이라(같은 이유로 애초에 PickView 카드가 안 뜬다)
        // 방치하면 "조건 미정"으로만 보였다 — 고정 문구로 실제 획득 경로를 안내한다.
        private string BuildLockHint(CatalogEntry e)
        {
            if (_cat == JackpotCatalog.CatDev)
            {
                var dev = Devices.ById(e.id);
                if (dev != null && string.IsNullOrEmpty(dev.unlockAch)) return "런 중 장치 드랍으로 획득";
            }
            // Opus 2차검수(P7-4b) [중대④ 정정] — 심볼 72종 중 기본 58종 밖(13종, Pouch.Symbols71 기준
            // 71개 중 DefaultUnlocked 58개를 뺀 나머지)은 JackpotCatalog.BuildSyntheticEntries가 이미
            // Content/DeepSymbolUnlock.cs 역매핑으로 실제 해금 업적의 이름·설명을 pick.unlock에 심어
            // 뒀다 — 카테고리 공용 고정 문구 대신 아래 공용 경로(e.pick.unlock)를 그대로 탄다.
            return (e.hasPick && e.pick != null && !string.IsNullOrEmpty(e.pick.unlock)) ? e.pick.unlock : "조건 미정";
        }

        private bool IsUnlocked(CatalogEntry e)
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null) return false;
            if (_cat == JackpotCatalog.CatChar) return profile.IsCharUnlocked(Characters.ById(e.key));
            if (_cat == JackpotCatalog.CatMac) return profile.IsMachineUnlocked(Machines.ById(e.key));
            if (_cat == JackpotCatalog.CatDev) return profile.IsDeviceUnlocked(Devices.ById(e.id));
            // 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20) — 심볼 도감 해금 = 기본 58종 ∪ 심화
            // 업적으로 해금된 추가분(PlayerProfile.EffectiveSymUnlocked, PouchOffer/NodeEvents가 이미
            // 쓰는 실해금 값과 동일 소스). 심볼증강/심볼유물은 레벨/업적 게이트가 없어(§JackpotCatalog
            // 헤더 각주) 아래 기본 true로 자연히 항상 해금 처리된다.
            if (_cat == JackpotCatalog.CatSym) return profile.EffectiveSymUnlocked().Contains(e.key);
            return true;
        }

        private string BuildSubline(CatalogEntry e)
        {
            if (_cat == JackpotCatalog.CatRel) return $"가격 {NumberFormat.Comma(e.price)}";
            if (_cat == JackpotCatalog.CatItem) return $"코인 {NumberFormat.Comma(e.coinCost)}";
            // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #11, 웹 ui.js:1886 masteryStars) — char/mac/dev는
            // IsUnlocked와 동일하게 char/mac은 e.key, dev는 e.id를 컨텐츠 id로 쓴다(JackpotCatalog.CatChar/
            // CatMac/CatDev 문자열이 mastery kind와 그대로 1:1이라 별도 매핑 불필요).
            if (_cat == JackpotCatalog.CatChar) return MasteryStarsText(JackpotCatalog.CatChar, e.key);
            if (_cat == JackpotCatalog.CatMac)
            {
                string stars = MasteryStarsText(JackpotCatalog.CatMac, e.key);
                if (e.scoreMod < 0f) return stars;
                string scoreMod = $"점수보정 ×{NumberFormat.Fmt(e.scoreMod)}";
                return string.IsNullOrEmpty(stars) ? scoreMod : scoreMod + " · " + stars;
            }
            if (_cat == JackpotCatalog.CatDev)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(e.command)) parts.Add($"명령 .{e.command}");
                parts.Add($"쿨다운 {(e.cooldown == -1 ? "-" : NumberFormat.Comma(e.cooldown))}");
                if (e.rare) parts.Add("희귀");
                string stars = MasteryStarsText(JackpotCatalog.CatDev, e.id);
                if (!string.IsNullOrEmpty(stars)) parts.Add(stars);
                return string.Join(" · ", parts);
            }
            if (_cat == JackpotCatalog.CatAch)
            {
                var profile = appRoot != null ? appRoot.Profile : null;
                bool achieved = profile != null && profile.AchievedIds.Contains(e.key);
                return achieved ? "✅ 달성" : "미달성";
            }
            // 웹 파리티 P7-4b — 심볼 도감 3탭 서브라인(정보 완전성 — 티어/희귀도 요약). DexDetailPopup.
            // TierLabel은 private(다른 클래스라 접근 불가)이라 동일 매핑을 이 클래스에도 따로 둔다.
            if (_cat == JackpotCatalog.CatSym) return $"등급 {Pouch.RarityOf(e.key)} · {SymTierLabel(e.tier)}";
            if (_cat == JackpotCatalog.CatSymAug || _cat == JackpotCatalog.CatSymRel) return SymTierLabel(e.tier);
            return "";
        }

        // 웹 파리티 P7-4b — 심볼증강/심볼유물 티어 라벨(DexDetailPopup.TierLabel과 동일 매핑, private라
        // 공유 못 해 이 클래스에 별도로 둔다).
        private static string SymTierLabel(string tier)
        {
            switch (tier)
            {
                case "SILVER": return "실버";
                case "GOLD": return "골드";
                case "PRISM": return "프리즘";
                default: return tier ?? "";
            }
        }

        // 웹 파리티 P3-3 — 숙련도 별 표기(웹 ui.js:1886 그대로: ★채움/☆빈칸 5개 중 충족수). Total<=0
        // (mastery 미대상 kind)이거나 id 없음이면 빈 문자열(줄 자체를 만들지 않음).
        private string MasteryStarsText(string kind, string id)
        {
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile == null || string.IsNullOrEmpty(id)) return "";
            var info = profile.MasteryOf(kind, id);
            if (info.Total <= 0) return "";
            return new string('★', info.Level) + new string('☆', Math.Max(0, info.Total - info.Level));
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
            // Opus 2차검수 필수⑤(2026-08-09, P4 REWARD_DONE/셀 정보 탭 슬라이스 검수 중 동일 패턴
            // 발견 — 이 슬라이스 범위 밖이지만 같은 결함이라 함께 수정) — 여기서
            // gameObject.SetActive(false)를 부르면 안 된다(전체 메커니즘 설명은
            // UI2/Run/Panels/CellInfoSheet.cs Awake 주석 참조). 빌더(BuildDexDetailPopup)가 이미
            // 비활성으로 굽는다.
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

            // Opus 2차 검수 저⑤ — 카탈로그 원문(웹 유래) 표시 직전 astral 새니타이즈(TextSanitize 헤더 참조).
            if (titleText != null) titleText.text = $"{e.emoji} {TextSanitize.StripAstral(e.nameKo)}";

            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(e.categoryLabel)) metaParts.Add(e.categoryLabel);
            if (!string.IsNullOrEmpty(e.tier)) metaParts.Add(TierLabel(e.tier));
            if (e.price >= 0) metaParts.Add($"가격 {NumberFormat.Comma(e.price)}");
            if (e.coinCost >= 0) metaParts.Add($"코인 {NumberFormat.Comma(e.coinCost)}");
            if (e.scoreMod >= 0f) metaParts.Add($"점수보정 ×{NumberFormat.Fmt(e.scoreMod)}");
            if (!unlocked) metaParts.Add("[미해금]");
            if (metaText != null) metaText.text = string.Join(" · ", metaParts);

            if (descText != null) descText.text = TextSanitize.StripAstral(e.descKo ?? "");

            // 웹 파리티 P3-4 Opus 2차검수 필수④(WEB_PARITY_DESIGN.md §2) — catalog.json의 e.unlockReq는
            // 구 Kotlin StatReq AND 문구(manifest.json 미갱신이라 스테일)라 더는 실제 해금 조건과
            // 일치하지 않는다(예: gambler는 웹에서 항상 해금인데 unlockReq엔 구 게이트 문구가 남음) —
            // 렌더를 아예 차단하고, pick.unlock(PickInfo.unlock — 웹 OR 문구. 실 catalog 항목이면
            // convert_manifest.py가 이미 웹 문구로 채운 원본, 신규 콘텐츠면 PickMeta.FallbackInfo가
            // Characters/Machines.cs unlockRuns/Score/Stage/Level/Ach로 즉석 합성)로 대체한다.
            if (unlockText != null)
            {
                bool hasUnlockLine = e.hasPick && e.pick != null && !string.IsNullOrEmpty(e.pick.unlock);
                if (hasUnlockLine)
                {
                    unlockText.text = "해금: " + e.pick.unlock;
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
                case "SILVER": return "실버";
                case "GOLD": return "골드";
                case "PRISM": return "프리즘";
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
