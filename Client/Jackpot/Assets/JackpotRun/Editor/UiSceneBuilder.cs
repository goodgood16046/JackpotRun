using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JackpotRun.Data;
using JackpotRun.Engine;
using JackpotRun.UI2;

namespace JackpotRun.EditorTools
{
    // 씬+화면 생성기 — ENGINE_PORT_DESIGN.md S8 "SceneBuilder 개편": BuildAll()(메뉴
    // "JackpotRun/Build UI Scenes") → BuildIntroScene() + BuildPlayScene(), 각각 개별 메뉴 항목도
    // 제공. 비대화형 BuildAllUnattended()는 MCP/CI용으로 공개 유지. 공통 골격(카메라·캔버스·FxLayer·
    // Toast)은 헬퍼로 공유하고, 두 씬 모두 S7c 카메라/ScreenSpaceCamera 전환 규칙을 적용한다.
    //
    // 반복 실행 안전 — 매번 완전히 새 인메모리 씬을 만들어 같은 경로에 덮어쓰므로(기존 씬을 열어
    // 이어붙이지 않음) 몇 번을 다시 실행해도 결과가 결정론적이다.
    public static class UiSceneBuilder
    {
        private const string ScenesFolder = "Assets/JackpotRun/Scenes";
        private const string IntroScenePath = ScenesFolder + "/Intro.unity";
        private const string PlayScenePath = ScenesFolder + "/Play.unity";

        // S7c: UICamera orthographicSize — 캔버스가 ScreenSpaceCamera 모드이고 CanvasScaler가 실제
        // 픽셀 단위를 결정하므로 카메라 orthographicSize 절대값 자체는 화면에 영향이 없다(파티클은
        // 플레인 GameObject라 이 카메라의 시야 안에만 있으면 된다).
        private const float CameraOrthoSize = 5f;
        private const float CameraPlaneDistance = 100f;
        private const int CanvasSortingOrder = 100;

        // S12c §6 — ".sheet{max-height:82vh}" — 캔버스 1920 기준 82% (Node/Perk/Shop/PostSpin/Bag/Manip
        // 시트 공용 상한, BuildSheetChrome 호출측이 이보다 작은 값을 주면 그 값을 그대로 쓴다).
        private const float SheetMaxHeight = 1574f;

        [MenuItem("JackpotRun/Build UI Scenes")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                "JackpotRun UI 씬 빌드",
                $"{IntroScenePath}, {PlayScenePath} 를 (다시) 생성합니다. 기존 씬 내용은 완전히 대체됩니다. 계속할까요?",
                "생성", "취소"))
            {
                return;
            }
            BuildAllUnattended();
        }

        [MenuItem("JackpotRun/Build Intro Scene")]
        public static void BuildIntroSceneMenuItem()
        {
            if (!EditorUtility.DisplayDialog("JackpotRun Intro 씬 빌드",
                $"{IntroScenePath} 를 (다시) 생성합니다. 계속할까요?", "생성", "취소")) return;
            UiSpriteGen.GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildIntroScene();
        }

        [MenuItem("JackpotRun/Build Play Scene")]
        public static void BuildPlaySceneMenuItem()
        {
            if (!EditorUtility.DisplayDialog("JackpotRun Play 씬 빌드",
                $"{PlayScenePath} 를 (다시) 생성합니다. 계속할까요?", "생성", "취소")) return;
            UiSpriteGen.GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildPlayScene();
        }

        /// <summary>확인 다이얼로그 없이 Intro+Play 씬을 전부 빌드 — MCP/CI 등 비대화형 경로용
        /// (설계 S8 "BuildAllUnattended() 유지").</summary>
        public static void BuildAllUnattended()
        {
            UiSpriteGen.GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildIntroScene();
            BuildPlayScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(IntroScenePath, true),
                new EditorBuildSettingsScene(PlayScenePath, true),
            };
            AssetDatabase.SaveAssets();

            Debug.Log("[JackpotRun] Intro/Play UI 씬 빌드 완료");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // Intro 씬 — Title/Login/Menu/Pick/Dex, 빌드 인덱스 0
        // ══════════════════════════════════════════════════════════════════════════════
        public static void BuildIntroScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEventSystem();
            var cam = BuildUiCamera();
            var canvasRoot = BuildCanvas("IntroCanvas", cam);

            // S12 §5 "배경" — 캔버스 최하단(첫 형제 = 맨 뒤에 그려짐)에 오로라+비네트를 깔고 그 위에
            // 화면들을 쌓는다. Title/Menu(S12a) 루트는 투명 컨테이너라 이 배경이 그대로 비쳐 보이고,
            // Login/Pick/Dex(이번 슬라이스 범위 밖, S12b/c 예정)는 아직 기존 불투명 배경을 유지한다.
            var auroraRect = BuildAuroraBackground(canvasRoot);

            var title = BuildTitleScreen(canvasRoot);
            var login = BuildLoginScreen(canvasRoot);
            var menu = BuildMenuScreen(canvasRoot);
            var pick = BuildPickScreen(canvasRoot);

            // OverlayLayer는 DexDetailPopup보다 먼저 Transform을 확보해야 그 자식으로 지을 수 있다 —
            // 화면 위에 그려지도록 하는 형제 순서는 아래에서 dex를 만든 뒤 SetAsLastSibling으로 되돌린다.
            var overlay = BuildOverlayLayer(canvasRoot);
            var dexDetail = BuildDexDetailPopup(overlay);
            var dex = BuildDexScreen(canvasRoot, dexDetail);
            var rank = BuildRankScreen(canvasRoot); // S15: 글로벌 랭킹
            var levelRewards = BuildLevelRewardsScreen(canvasRoot); // 웹 파리티 P4(§1-A #15 B)
            // 웹 파리티 P4(§1-A #15 A.5) — 데이터 초기화 확인 시트. overlay가 확보된 뒤에만 지을 수 있어
            // BuildMenuScreen 안이 아니라 여기서 채운다(giveUpConfirmPopup과 동일하게 overlay 산하).
            menu.resetConfirmPopup = BuildConfirmSheetPopup(overlay, "ResetConfirmPopup", dismissOnScrimClick: true);
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 설정 시트(홈 진입점).
            menu.settingsSheet = BuildSettingsSheet(overlay, "MenuSettingsSheet", includeReset: true);
            ((RectTransform)overlay).SetAsLastSibling();

            var toast = BuildToast(canvasRoot);
            BuildFxLayer(canvasRoot);

            var rootGo = new GameObject("IntroSceneRoot");
            var router = rootGo.AddComponent<ScreenRouter>();
            var introRoot = rootGo.AddComponent<IntroSceneRoot>();

            WireScreens(router, overlay, toast,
                (ScreenRouter.ScreenId.Title, title.root, title.group),
                (ScreenRouter.ScreenId.Login, login.root, login.group),
                (ScreenRouter.ScreenId.Menu, menu.root, menu.group),
                (ScreenRouter.ScreenId.Pick, pick.root, pick.group),
                (ScreenRouter.ScreenId.Dex, dex.root, dex.group),
                (ScreenRouter.ScreenId.Rank, rank.root, rank.group),
                (ScreenRouter.ScreenId.LevelRewards, levelRewards.root, levelRewards.group));

            var introSo = new SerializedObject(introRoot);
            introSo.FindProperty("router").objectReferenceValue = router;
            introSo.FindProperty("titleView").objectReferenceValue = title.view;
            introSo.FindProperty("loginView").objectReferenceValue = login.view;
            introSo.FindProperty("menuView").objectReferenceValue = menu.view;
            introSo.FindProperty("pickView").objectReferenceValue = pick.view;
            introSo.FindProperty("dexView").objectReferenceValue = dex.view;
            introSo.FindProperty("rankView").objectReferenceValue = rank.view;
            introSo.FindProperty("levelRewardsView").objectReferenceValue = levelRewards.view;
            introSo.FindProperty("auroraRect").objectReferenceValue = auroraRect;
            introSo.ApplyModifiedPropertiesWithoutUndo();

            WireTitleView(title);
            WireMenuView(menu);
            WirePickView(pick);
            WireDexView(dex, dexDetail);
            WireRankView(rank);
            WireLevelRewardsView(levelRewards);

            // 순수 내비게이션 버튼(AppRoot는 DontDestroyOnLoad라 에디터 시점엔 존재하지 않으므로
            // UnityEventTools.AddPersistentListener로 직접 가리킬 수 없다 — NavButton.cs 헤더 참조).
            // Title의 시작 버튼은 닉네임 유무에 따라 Login/Menu로 갈라져야 해서 NavButton(고정 대상 1개)
            // 대신 TitleView.OnStartClicked가 직접 판정한다(설계 S12 지시, TitleView.cs 헤더 참조).
            AddNavButton(menu.startButton, NavButton.Target.Pick);
            AddNavButton(menu.dexButton, NavButton.Target.Dex);
            AddNavButton(pick.backButton, NavButton.Target.Menu);
            AddNavButton(dex.backButton, NavButton.Target.Menu);
            AddNavButton(rank.backButton, NavButton.Target.Menu);
            AddNavButton(levelRewards.backButton, NavButton.Target.Menu);

            CheckLayoutOverlaps(canvasRoot); // S13 §C 회귀 방지 자가 점검
            SaveScene(scene, IntroScenePath);
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // Play 씬 — 런 플레이 단독, 빌드 인덱스 1
        // ══════════════════════════════════════════════════════════════════════════════
        public static void BuildPlayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEventSystem();
            var cam = BuildUiCamera();
            var canvasRoot = BuildCanvas("PlayCanvas", cam);

            var overlay = BuildOverlayLayer(canvasRoot);
            var runOverlay = BuildRunOverlayPanels(overlay);

            var run = BuildRunScreen(canvasRoot);
            ((RectTransform)overlay).SetAsLastSibling();

            var toast = BuildToast(canvasRoot);
            BuildFxLayer(canvasRoot);

            WireRunView(run, runOverlay);

            var rootGo = new GameObject("PlaySceneRoot");
            var playRoot = rootGo.AddComponent<PlaySceneRoot>();
            var playSo = new SerializedObject(playRoot);
            playSo.FindProperty("runView").objectReferenceValue = run.view;
            playSo.FindProperty("overlayLayer").objectReferenceValue = overlay;
            playSo.FindProperty("toast").objectReferenceValue = toast;
            playSo.ApplyModifiedPropertiesWithoutUndo();

            CheckLayoutOverlaps(canvasRoot); // S13 §C 회귀 방지 자가 점검
            SaveScene(scene, PlayScenePath);
        }

#if UNITY_EDITOR
        // 자가 점검이 안전하게 임시 활성화해도 되는 컴포넌트만 허용하는 화이트리스트 — Node/Perk/Shop
        // 패널처럼 커스텀 MonoBehaviour(OnEnable에서 AppRoot.Instance 등 실행 중 게임 상태를 가정)를
        // 가진 오버레이는 절대 건드리지 않는다(Editor 스크립트 시점엔 그런 상태가 없어 예외 위험).
        // 카드/칩/버튼 템플릿류는 순수 uGUI 구성요소 + PressFx뿐이라 항상 이 목록 안에 들어온다.
        private static readonly System.Type[] SafeToggleComponentTypes =
        {
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RawImage), typeof(Text),
            typeof(Button), typeof(Outline), typeof(Shadow), typeof(CanvasGroup), typeof(RectMask2D), typeof(Mask),
            typeof(LayoutElement), typeof(VerticalLayoutGroup), typeof(HorizontalLayoutGroup), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter), typeof(ScrollRect), typeof(Scrollbar), typeof(PressFx), typeof(InputField),
        };

        private static bool IsSafeToTemporarilyActivate(GameObject go)
        {
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue; // 누락된 스크립트 참조
                if (System.Array.IndexOf(SafeToggleComponentTypes, comp.GetType()) < 0) return false;
            }
            return true;
        }

        // S13 §C 회귀 방지 — "빌더 마지막에 레이아웃 그룹 자식 rect 겹침 발견 시 경고 로그"(설계 지시
        // 그대로). Role/Eff처럼 controlChildHeight=false인 그룹의 자식이 실제 sizeDelta 보정을
        // 빠뜨려 서로 겹치는 사례를 다음에도 잡기 위한 휴리스틱 — 빌드를 막지는 않는다(오탐 가능성
        // 있는 자가 점검이라 최종 판단은 Fable 육안 검수).
        //
        // ⚠️ 구현 함정(실측으로 발견): CardTemplate 등은 그리드/레이아웃 부모가 "비활성 자식은 크기를
        // 배정하지 않는다"는 Unity 규칙 때문에 기본값(100×100)에 방치된 채로 남는다 — 그 상태 그대로
        // 내부(Top/Eff 등)를 재보면 진짜 겹침이 전혀 없어도 실제 런타임 크기(예: 500×320)가 아니라서
        // 온갖 "겹침"이 대량으로 오탐된다. 그래서 점검 직전 "화이트리스트에 든" 비활성 자식만 잠깐
        // 켜서 부모(GridLayoutGroup 등)가 진짜 크기를 배정하게 한 뒤 재고, 끝나면 원상복구한다.
        private static void CheckLayoutOverlaps(RectTransform canvasRoot)
        {
            if (canvasRoot == null) return;

            var toggledOn = new System.Collections.Generic.List<GameObject>();
            var groupsInitial = canvasRoot.GetComponentsInChildren<LayoutGroup>(true);
            foreach (var group in groupsInitial)
            {
                var parent = (RectTransform)group.transform;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child.gameObject.activeSelf || !IsSafeToTemporarilyActivate(child.gameObject)) continue;
                    child.gameObject.SetActive(true);
                    toggledOn.Add(child.gameObject);
                }
            }

            // 여러 단 중첩된 그룹/ContentSizeFitter가 서로의 크기에 의존하므로 한 프레임으로는 수렴하지
            // 않을 수 있다 — 몇 차례 반복해 안정시킨다(정확한 반복 횟수는 설계 미명시, 실측으로 충분함 확인).
            for (int pass = 0; pass < 4; pass++) Canvas.ForceUpdateCanvases();

            int warnings = 0;
            var groups = canvasRoot.GetComponentsInChildren<LayoutGroup>(true);
            foreach (var group in groups)
            {
                var parent = (RectTransform)group.transform;
                var children = new System.Collections.Generic.List<RectTransform>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i) as RectTransform;
                    if (child != null && child.gameObject.activeSelf) children.Add(child);
                }

                for (int i = 0; i < children.Count; i++)
                {
                    var ri = WorldRect(children[i]);
                    for (int j = i + 1; j < children.Count; j++)
                    {
                        if (!ri.Overlaps(WorldRect(children[j]))) continue;
                        Debug.LogWarning($"[JackpotRun] S13 §C 자가 점검 — 레이아웃 겹침 의심: " +
                            $"{PathOf(parent)} 안의 '{children[i].name}' ↔ '{children[j].name}'");
                        warnings++;
                    }
                }
            }

            for (int i = toggledOn.Count - 1; i >= 0; i--)
                if (toggledOn[i] != null) toggledOn[i].SetActive(false);

            if (warnings == 0) Debug.Log("[JackpotRun] S13 §C 레이아웃 겹침 자가 점검: 0건.");
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // [0]=좌하단 [2]=우상단(회전 없음 전제 — 이 프로젝트엔 회전 UI 없음)
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return "";
            string path = t.name;
            var p = t.parent;
            for (int depth = 0; p != null && depth < 4; depth++, p = p.parent) path = p.name + "/" + path;
            return path;
        }
#endif

        private static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/JackpotRun", "Scenes");

            bool saved = EditorSceneManager.SaveScene(scene, path);
            if (!saved)
            {
                Debug.LogError("[JackpotRun] 씬 저장 실패: " + path);
                return;
            }
            AssetDatabase.SaveAssets();
        }

        private static void AddNavButton(Button button, NavButton.Target target)
        {
            if (button == null) return;
            var nav = button.gameObject.AddComponent<NavButton>();
            var so = new SerializedObject(nav);
            so.FindProperty("target").enumValueIndex = (int)target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 결과 전달용 컨테이너(생성 직후 값을 여러 곳으로 넘기기 위함, 씬에는 남지 않는다) ──────
        private sealed class TitleBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public TitleView view;
            public RectTransform contentRoot;
            public RectTransform[] reelTiles;
            public Image[] reelIcons;
            public Outline[] reelGlows;
            public Sprite[] symbolSprites;
            public Text bestText;
            public Button startButton;
            public RectTransform reelsRow; // S13 §E — fx_title_spark 앵커
        }

        private sealed class LoginBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public LoginView view;
        }

        private sealed class MenuBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public MenuView view;
            public Button startButton;
            public Button rankButton;
            public Button dexButton;
            public Text hudTitleText;
            public Text statScoreValue;
            public Text statStageValue;
            public Text statPlaysValue;
            public Text summaryText;

            // ── 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 A) — 레벨 카드/게임 모드 선택기/데이터 초기화 ──
            public Button levelCardButton;
            public Text levelBadgeText;
            public Text levelXpText;
            public RectTransform levelBarFill;
            public Image levelBarFillImage;
            public Button modeDeepButton;

            // ── 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — 승천(심화 학기) 선택기(웹 ascSelector()) ──
            public RectTransform ascSectionRoot;
            public Text ascBadgeText;
            public Text ascLevelText;
            public Text ascRuleText;
            public Text ascHintText;
            public Button ascPrevButton;
            public Button ascNextButton;

            public Button resetButton;
            // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17) — 홈 소리 토글(reset 링크 버튼과 같은 행).
            public Button soundToggleButton;
            public Text soundToggleLabel;
            // BuildIntroScene이 overlay 확보 후 별도로 채운다(BuildMenuScreen 시점엔 overlay가 아직 없음).
            public UI2.ConfirmSheetPopup resetConfirmPopup;
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 설정 진입점(우상단 ⚙). settingsSheet도
            // resetConfirmPopup과 동일하게 overlay 확보 후 BuildIntroScene이 채운다.
            public Button settingsButton;
            public UI2.SettingsSheet settingsSheet;
        }

        private sealed class PickBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public PickView view;
            public Button backButton;
            public Text headWhoText; // S10 head 블록 "@닉 — ..." — index.html #who
            public Button[] recoButtons;
            public Button[] tabButtons;
            public Image[] tabButtonImages;
            public Text[] tabNumTexts; // S10 .tnum(① 등, 완료 시 " ✓" 그린 접미)
            public Text[] tabLabelTexts; // .tpick
            public RectTransform chipsContent;
            public RectTransform chipTemplate;
            public Button[] sortButtons;
            public Image[] sortButtonImages;
            public Text sectionTitleText; // S10 .sechead h2
            public Text sectionCountText; // S10 .sechead .cnt "해금 n/m"
            public RectTransform gridContent;
            public CanvasGroup gridCanvasGroup;
            public RectTransform cardTemplate;
            public Text comboText;
            public Text comboBuildText; // S10 .sum-combo .bl(골드 빌드토큰 요약줄)
            public Text gradeText;
            public Image gradeBadgeImage; // S10 .sum-grade 배지 배경(등급색 저알파 틴트)
            public Text ceilingValueText;
            public Text stabilityValueText;
            public Text difficultyValueText;
            public Text difficultyLabelText;
            public Text blurbText;
            public Text prosText;
            public Text consText;
            public RectTransform buildChipsContent; // S10 .sd-builds 칩 로우
            public RectTransform buildChipTemplate;
            public Button startButton;
        }

        // ── S7b/S8 결과 컨테이너(RunScreen 및 그 오버레이 패널) ────────────────────────
        private sealed class RunBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public UI2.RunView view;

            public RectTransform hudRoot;
            public Text stageText, cursesText, expBarText, spinsText, coinsText, scoreText;
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — HUD 승천 배지(웹 ui.js:704 asc-hud).
            public Text ascBadgeText;
            public RectTransform expBarFill;
            public Image expBarFillImage;
            public RectTransform expLeadDot; // S14 §D — EXP 바 선두 광점
            public Outline hudOutline;
            public Image[] unluckyPips;
            public CanvasGroup bossBannerGroup;
            public RectTransform bossBannerRect;
            public Text bossBannerText;
            public CanvasGroup bossVignetteGroup; // S14 §D — 보스 진입 적색 비네트 펄스
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — HUD "?" 튜토리얼 재시작 + "⚙" 설정 진입점.
            public Button tutorialButton;
            public Button settingsButton;

            public RectTransform reelSectionRoot;
            public RectTransform reelRow;
            public RectTransform cellTemplate;
            public (string id, Sprite sprite)[] symbolSprites;
            public CanvasGroup flashOverlay;
            public CanvasGroup jackpotBannerGroup;
            public RectTransform jackpotBannerRect;

            // S16 — 구 resultLineText(한 줄 요약)를 대체하는 스핀 결과 패널(GainPanel.cs).
            public RectTransform gainPanelRoot;
            public Text gainBigText; // "+{N} EXP" 대문짝
            public RectTransform gainScoreChipRoot;
            public Image gainScoreChipBg;
            public Text gainScoreChipLabel;
            public RectTransform gainCoinChipRoot;
            public Image gainCoinChipBg;
            public Text gainCoinChipLabel;
            public RectTransform gainRowsContent; // 기여 내역 행 스택(행 템플릿+비활성 원본)
            public RectTransform gainRowTemplate;
            public RectTransform gainSetExplainRoot; // 세트 성립 시에만 노출하는 보라 테두리 설명 박스
            public Text gainSetExplainText;

            public RectTransform notesRoot;
            public RectTransform notesRowsContent;
            public RectTransform notesRowTemplate;

            public CanvasGroup controlsGroup;
            public Button[] modeButtons;
            public Button spinButton;
            public Button bagButton;
            public Text bagButtonLabel;
            public RectTransform deviceRow;
            public RectTransform deviceButtonTemplate;
            // WEB_PARITY P1 ⑤: "게임 포기" 액션바 진입점(웹 ui.js:849-871).
            public Button giveUpButton;

            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 튜토리얼 오버레이 + 설정 시트(overlay
            // 확보 후 BuildPlayScene이 별도로 채운다 — BuildRunScreen 시점엔 overlay가 아직 없음).
            public UI2.TutorialOverlay tutorialOverlay;
            public UI2.SettingsSheet settingsSheet;
        }

        // S12c §6 — BuildSheetChrome(...)이 반환하는 "시트" 골격 3요소. scrim(전체화면, 클릭 차단) →
        // dimGroup(rgba(0,0,0,.62) 딤, CanvasGroup 알파 0 시작 — 각 패널의 Show()가 0→1 페이드) →
        // card(w_sheet_top 배경, 하단 고정 앵커라 슬라이드업의 대상) → cardCol(card를 채우는 빈
        // VGroup, 호출측이 제목/스크롤/버튼을 여기 쌓는다).
        private struct SheetChrome
        {
            public RectTransform scrim;
            public CanvasGroup dimGroup;
            public RectTransform card;
            public RectTransform cardCol;
        }

        private sealed class RunOverlayResult
        {
            public UI2.NodePanel nodePanel;
            public UI2.PerkOfferPanel perkOfferPanel;
            public UI2.ShopPanel shopPanel;
            public UI2.PostSpinPanel postSpinPanel;
            public UI2.GameOverPanel gameOverPanel;
            public UI2.BagPopup bagPopup;
            public UI2.ManipPickPopup manipPickPopup;
            // WEB_PARITY P1 ⑤/④: 범용 확인 시트 2개 — 포기 확인 / DEVICE 노드 오퍼(장착·코인).
            public UI2.ConfirmSheetPopup giveUpConfirmPopup;
            public UI2.ConfirmSheetPopup deviceOfferPopup;
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — RewardDone(보상 획득 → 다음 스테이지 인트로).
            public UI2.RewardDonePanel rewardDonePanel;
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 정보 탭(openCellSheet 대응).
            public UI2.CellInfoSheet cellInfoSheet;
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 튜토리얼 오버레이 + 설정 시트.
            public UI2.TutorialOverlay tutorialOverlay;
            public UI2.SettingsSheet settingsSheet;
        }

        private sealed class DexBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public UI2.DexView view;
            public Button backButton;
            public Text statBestScoreText, statBestStageText, statRunsText, statTotalScoreText;
            public Image[] tabImages;
            public RectTransform gridContent;
            public RectTransform cardTemplate;
        }

        // S15 — 글로벌 랭킹 화면(RankView.cs) 결과 컨테이너.
        private sealed class RankBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public UI2.RankView view;
            public Button backButton;
            public Text statusText;
            public RectTransform listContent;
            public RectTransform rowTemplate;
        }

        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 B) — 레벨 보상 화면(LevelRewardsView.cs) 결과 컨테이너.
        // BuildRankScreen과 동일 골격(헤더 → 레벨 카드 → 로드맵 헤더 → 세로 스크롤 행 목록).
        private sealed class LevelRewardsBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public UI2.LevelRewardsView view;
            public Button backButton;
            public Text levelBadgeText;
            public Text levelXpText;
            public RectTransform levelBarFill;
            public Image levelBarFillImage;
            public Text roadCountText;
            public RectTransform listContent;
            public RectTransform rowTemplate;
            public Text emptyText; // 웹 roadHtml || '해금 항목 없음' 폴백(Opus 2차검수 정리)
        }

        // BuildLevelCard(...)가 돌려주는 공용 결과 — MenuScreen(클릭형)·LevelRewardsScreen(비클릭형)이
        // 같은 레벨 카드 골격을 공유한다(웹 lvlCard(lp, clickable), ui.js:591-602 그대로 한 헬퍼로 통합).
        private struct LevelCardResult
        {
            public Button button; // clickable=false면 null
            public Text badgeText;
            public Text xpText;
            public RectTransform barFill;
            public Image barFillImage;
        }

        // ── 씬 공통 골격 ─────────────────────────────────────────────────────────────
        private static void BuildEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // S7c "렌더링 전환": Orthographic, size 5, depth 0, clearFlags SolidColor(#0B0E1A),
        // tag "MainCamera", position (0,0,-100), cullingMask Everything.
        private static Camera BuildUiCamera()
        {
            var go = new GameObject("UICamera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraOrthoSize;
            cam.depth = 0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Bg;
            cam.cullingMask = ~0; // Everything
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -100f);
            return cam;
        }

        // S7c "JackpotRunCanvas": renderMode = ScreenSpaceCamera, worldCamera = UICamera,
        // planeDistance = 100, sortingOrder 100 유지 → 캔버스 로컬 1unit = 1px.
        private static RectTransform BuildCanvas(string name, Camera uiCamera)
        {
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = CameraPlaneDistance;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)canvasGo.transform;
        }

        // S12 §5 "배경" — 오로라(w_aurora, 애니메이션 대상이라 RectTransform을 반환) + 비네트
        // (w_vignette, 정적) 순서로 깔아 캔버스 최하단(첫 형제)에 둔다. 둘 다 투명 컨테이너라
        // raycastTarget=false(§7 공통 규칙).
        private static RectTransform BuildAuroraBackground(Transform canvasRoot)
        {
            var aurora = UiKit.Panel(canvasRoot, "Aurora", Color.white);
            UiKit.Fill(aurora);
            var auroraImg = aurora.GetComponent<Image>();
            auroraImg.sprite = UiSpriteGen.Load("w_aurora");
            auroraImg.type = Image.Type.Simple;
            auroraImg.preserveAspect = false;
            auroraImg.raycastTarget = false;

            var vignette = UiKit.Panel(canvasRoot, "Vignette", Color.white);
            UiKit.Fill(vignette);
            var vignetteImg = vignette.GetComponent<Image>();
            vignetteImg.sprite = UiSpriteGen.Load("w_vignette");
            vignetteImg.type = Image.Type.Simple;
            vignetteImg.preserveAspect = false;
            vignetteImg.raycastTarget = false;

            return aurora;
        }

        // §0 "--gloss" 상단 광택 오버레이 — 카드/칩/릴 상단 40~50%에 얹는다. w_gloss는 라운딩 없는
        // 전체-사각 텍스처라(UiSpriteGen 헤더 참조) Type.Simple로 원하는 높이만큼 늘려서 쓴다.
        private static void AddGloss(RectTransform parent, float height)
        {
            var go = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = UiSpriteGen.Load("w_gloss");
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private static RectTransform BuildOverlayLayer(Transform canvasRoot)
        {
            var overlay = UiKit.Panel(canvasRoot, "OverlayLayer", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(overlay);
            // 투명 컨테이너지만 Image의 raycastTarget 기본값이 true라, 마지막 형제로서 화면 전체의
            // 클릭을 전부 삼켜버린다(버튼이 눌리지 않던 원인). 자식 팝업은 각자 raycastTarget을 갖는다.
            var img = overlay.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            return overlay;
        }

        // S7c "FxLayer" — 캔버스 하위, CanvasGroup{blocksRaycasts=false, interactable=false}(FxKit.Awake가
        // 스스로 보장) + FxKit 컴포넌트. 캔버스가 ScreenSpaceCamera라 이 레이어의 로컬 좌표계가 곧
        // 1080×1920 레퍼런스 픽셀 좌표계다(FxKit.ToLocal 계약과 일치).
        private static FxKit BuildFxLayer(Transform canvasRoot)
        {
            var layer = UiKit.Panel(canvasRoot, "FxLayer", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(layer);
            var fxImg = layer.GetComponent<Image>();
            if (fxImg != null) fxImg.raycastTarget = false; // CanvasGroup과 별개로 이중 안전장치
            return layer.gameObject.AddComponent<FxKit>();
        }

        private static ToastManager BuildToast(Transform canvasRoot)
        {
            // S13 §A 실측 위반 1/7: 800×84에 chip_r999(border 128)를 쓰면 상하 128px 경계가 84px
            // 높이보다 커서 타원으로 늘어난다 — UiKit.PillSprite(84)로 교체.
            var toastRoot = UiKit.Panel(canvasRoot, "Toast", UiKit.PanelBg, UiKit.PillSprite(84f));
            var group = toastRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            toastRoot.anchorMin = new Vector2(0.5f, 0f);
            toastRoot.anchorMax = new Vector2(0.5f, 0f);
            toastRoot.pivot = new Vector2(0.5f, 0f);
            toastRoot.sizeDelta = new Vector2(800f, 84f);
            toastRoot.anchoredPosition = new Vector2(0f, 64f);

            var padded = UiKit.HGroup(toastRoot, 0, new RectOffset(28, 28, 8, 8), true, true);
            UiKit.Fill(padded);
            var label = UiKit.Text(padded, "", 22, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(label, flexibleWidth: 1);

            var toast = toastRoot.gameObject.AddComponent<ToastManager>();
            var so = new SerializedObject(toast);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            return toast;
        }

        // ── TitleView 화면(S12 §3 신규 — 웹 단독판 renderIntro) ─────────────────────────
        // 수치는 전부 설계 §3의 "(→NNN)" 최종 캔버스 px 값(이미 ×1.9 스케일 반영됨)을 그대로 쓴다.
        private static TitleBuildResult BuildTitleScreen(Transform canvasRoot)
        {
            var result = new TitleBuildResult();

            // 배경은 캔버스 최하단 오로라+비네트가 담당한다 — 이 화면 루트는 투명 컨테이너(§7 공통
            // 규칙: 투명 컨테이너 패널은 raycastTarget=false).
            var root = UiKit.Panel(canvasRoot, "TitleScreen", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(root);
            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = false;
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<TitleView>();

            // §3 "전체 화면 세로 중앙, gap 13(→25), padding 24(→46)" — controlChildHeight도 켜서
            // SizeHint(preferredHeight)가 실제로 각 자식 높이를 결정하게 한다(BuildLoginScreen과 동일
            // 검증된 패턴).
            var col = UiKit.VGroup(root, 25, new RectOffset(46, 46, 46, 46), true, true);
            UiKit.SetAnchors(col, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(46f, -430f), new Vector2(-46f, 430f));
            col.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            result.contentRoot = col;

            // ── 릴 타일 3개 — 118×152, gap 19, w_reel 배경 + 골드 Outline(테두리+글로우 근사) ──────
            var reelsRow = UiKit.HGroup(col, 19, new RectOffset(), false, false);
            UiKit.SizeHint(reelsRow, preferredHeight: 152, flexibleHeight: 0);
            reelsRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            result.reelsRow = reelsRow; // S13 §E — fx_title_spark 앵커(릴 타일 3개를 담는 행 전체)

            var reelSprite = UiSpriteGen.Load("w_reel");
            result.reelTiles = new RectTransform[3];
            result.reelIcons = new Image[3];
            result.reelGlows = new Outline[3];
            for (int i = 0; i < 3; i++)
            {
                var tile = UiKit.Panel(reelsRow, "Reel" + i, Color.white, reelSprite);
                tile.sizeDelta = new Vector2(118f, 152f);
                var glow = UiKit.AddGlowOutline(tile.gameObject, UiKit.Accent, 4f);
                glow.enabled = true;

                var icon = UiKit.Image(tile, null, Color.white);
                icon.name = "Icon";
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(76f, 76f);
                icon.rectTransform.anchoredPosition = Vector2.zero;

                AddGloss(tile, 64f); // 42% of 152 ≈ 64

                result.reelTiles[i] = tile;
                result.reelIcons[i] = icon;
                result.reelGlows[i] = glow;
            }

            // 심볼 스프라이트 14종 — 런타임은 Editor 전용 UiSpriteGen을 참조할 수 없어 빌드 시점에
            // 구워 넘긴다(ReelView.symbolSprites와 동일한 "빌더가 와이어링" 관례).
            var syms = JackpotRun.Engine.Symbols.All;
            var symSprites = new Sprite[syms.Length];
            for (int i = 0; i < syms.Length; i++) symSprites[i] = UiSpriteGen.Load("sym_" + syms[i].id);
            result.symbolSprites = symSprites;

            // ── 타이틀/부제/최고기록/시작 버튼/힌트 ──────────────────────────────────────
            // 그라데이션 텍스트(#ffe87a→gold2)는 uGUI 불가 → #ffdd5c 단색 + 골드 글로우(Outline)로 근사
            // (§7 재해석 규칙). 웹 원문 "🎰 잭팟 슬롯"은 astral 이모지+웹 전용 제품명이라, 기존
            // Unity 화면들과 통일된 "잭팟런"으로 대체했다(LoginView 등과 동일 브랜딩, 임의 변경 아님).
            var title = UiKit.Text(col, "잭팟런", 91, UiKit.Hex("#ffdd5c"), TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(title, preferredHeight: 112, flexibleHeight: 0);
            UiKit.AddGlowOutline(title.gameObject, new Color(1f, 210f / 255f, 63f / 255f, 0.45f), 3f).enabled = true;

            var sub = UiKit.Text(col, "텍스트 로그라이크 슬롯머신 · 웹 단독판", 26, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(sub, preferredHeight: 36, flexibleHeight: 0);

            result.bestText = UiKit.Text(col, "", 25, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.bestText, preferredHeight: 36, flexibleHeight: 0);

            // pill 버튼은 전체폭이 아니라 내재 크기라 VGroup의 forceExpandWidth를 그대로 받으면 안
            // 된다 — 투명 풀와이드 슬롯 안에 고정 크기로 수동 중앙 배치한다(§7 재해석: 한 텍스처=한
            // 반경 제약 때문에 w_pill을 금색으로 틴트해 대신 쓴다, UiSpriteGen.cs 주석 참조).
            var btnSlot = UiKit.Panel(col, "StartBtnSlot", new Color(0f, 0f, 0f, 0f));
            var btnSlotImg = btnSlot.GetComponent<Image>();
            if (btnSlotImg != null) btnSlotImg.raycastTarget = false;
            UiKit.SizeHint(btnSlot, preferredHeight: 128, flexibleHeight: 0);
            // 높이는 스프라이트 경계 합(64+64)과 같은 128로 맞춘다 — 그래야 9-slice가 찌그러지지 않는다.
            var pillSprite = UiSpriteGen.Load("w_pill_btn");
            result.startButton = UiKit.Button(btnSlot, "▶ 탭하여 시작", new Vector2(460f, 128f), UiKit.Accent, UiKit.Ink, null, pillSprite);
            var startRt = result.startButton.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.5f, 0.5f);
            startRt.anchorMax = new Vector2(0.5f, 0.5f);
            startRt.pivot = new Vector2(0.5f, 0.5f);
            startRt.sizeDelta = new Vector2(460f, 128f);
            startRt.anchoredPosition = Vector2.zero;

            var hint = UiKit.Text(col, "소리 ON · 첫 탭에서 활성화돼요", 21, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(hint, preferredHeight: 30, flexibleHeight: 0);

            return result;
        }

        private static void WireTitleView(TitleBuildResult r)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("contentRoot").objectReferenceValue = r.contentRoot;
            SetObjectArray(so, "reelTiles", r.reelTiles);
            SetObjectArray(so, "reelIcons", r.reelIcons);
            SetObjectArray(so, "reelGlows", r.reelGlows);
            SetObjectArray(so, "symbolSprites", r.symbolSprites);
            so.FindProperty("bestText").objectReferenceValue = r.bestText;
            so.FindProperty("startButton").objectReferenceValue = r.startButton;
            so.FindProperty("reelsRow").objectReferenceValue = r.reelsRow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── LoginView 화면(S8 신규) ──────────────────────────────────────────────────
        private static LoginBuildResult BuildLoginScreen(Transform canvasRoot)
        {
            var result = new LoginBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

            var root = UiKit.Panel(canvasRoot, "LoginScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<LoginView>();

            var col = UiKit.VGroup(root, 24, new RectOffset(80, 80, 0, 0), true, true);
            UiKit.SetAnchors(col, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(80f, -430f), new Vector2(-80f, 430f));
            col.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            // S8 항목⑤: 🎰(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var title = UiKit.Text(col, "잭팟런", UiKit.TextStyle.Title, TextAnchor.MiddleCenter);
            UiKit.SizeHint(title, preferredHeight: 120, flexibleHeight: 0);

            var subtitle = UiKit.Text(col, "닉네임을 입력하고 시작하세요", 24, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(subtitle, preferredHeight: 40, flexibleHeight: 0);

            var input = BuildInputField(col, "닉네임 (2~12자)");
            UiKit.SizeHint(input, preferredHeight: 88, flexibleHeight: 0);

            var startButton = UiKit.Button(col, "시작하기", new Vector2(0, 108), UiKit.Accent, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(startButton, preferredHeight: 108, flexibleHeight: 0);

            var guestButton = UiKit.Button(col, "게스트로 시작", new Vector2(0, 84), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(guestButton, preferredHeight: 84, flexibleHeight: 0);

            var so = new SerializedObject(result.view);
            so.FindProperty("nicknameInput").objectReferenceValue = input;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("guestButton").objectReferenceValue = guestButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return result;
        }

        // 레거시 InputField(닉네임) — Text + Placeholder(Text) 자식 구성.
        private static InputField BuildInputField(Transform parent, string placeholder)
        {
            var panelSprite = UiSpriteGen.Load("panel_r24");
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var bgImg = go.GetComponent<Image>();
            bgImg.color = UiKit.Card;
            if (panelSprite != null) { bgImg.sprite = panelSprite; bgImg.type = Image.Type.Sliced; }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            UiKit.SetAnchors(textRt, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f));
            var text = textGo.GetComponent<Text>();
            text.font = UiKit.Kor();
            text.fontSize = 26;
            text.color = UiKit.TextPrimary;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            var placeholderRt = (RectTransform)placeholderGo.transform;
            placeholderRt.SetParent(rt, false);
            UiKit.SetAnchors(placeholderRt, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f));
            var placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.font = UiKit.Kor();
            placeholderText.fontSize = 26;
            placeholderText.color = UiKit.TextSecondary;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;
            placeholderText.fontStyle = FontStyle.Italic;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = bgImg;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.characterLimit = 12;
            input.lineType = InputField.LineType.SingleLine;

            return input;
        }

        // ── MenuView 화면 ────────────────────────────────────────────────────────────
        // ── MenuView 화면(S12 §4 — 웹 단독판 renderHome) ────────────────────────────────
        // scr-title → hud 카드(칭호+3칸 통계) → 요약줄 → bigbtn+ghost×2 → 설명 2줄. 수치는 §4의
        // "(→NNN)" 최종 캔버스 px 값을 그대로 쓴다.
        private static MenuBuildResult BuildMenuScreen(Transform canvasRoot)
        {
            var result = new MenuBuildResult();

            // 배경은 캔버스 최하단 오로라+비네트가 담당한다(§7 공통 규칙: 투명 컨테이너 raycastTarget=false).
            var root = UiKit.Panel(canvasRoot, "MenuScreen", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(root);
            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = false;
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<MenuView>();

            var col = UiKit.VGroup(root, 30, new RectOffset(46, 46, 70, 46), true, true);
            UiKit.Fill(col);

            // ── scr-title: h1 51 골드 + sub 25 txt2 ───────────────────────────────────
            // 웹 원문 "🎰 잭팟 슬롯"은 astral 이모지+웹 전용 제품명이라 TitleView와 동일하게 "잭팟런"
            // 으로 통일했다(임의 변경이 아니라 기존 Unity 화면들과의 브랜딩 일관성).
            var title = UiKit.Text(col, "잭팟런", 51, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(title, preferredHeight: 66, flexibleHeight: 0);
            var sub = UiKit.Text(col, "텍스트 로그라이크 슬롯머신 · 웹 단독판", 25, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(sub, preferredHeight: 34, flexibleHeight: 0);

            // ── P4 A.1: 레벨 카드(클릭형 → 레벨 보상 화면) — 웹 lvlCard(lp,true) 순서 그대로 title 다음 ──
            var levelCard = BuildLevelCard(col, clickable: true);
            result.levelCardButton = levelCard.button;
            result.levelBadgeText = levelCard.badgeText;
            result.levelXpText = levelCard.xpText;
            result.levelBarFill = levelCard.barFill;
            result.levelBarFillImage = levelCard.barFillImage;

            // ── P4 A.2: 게임 모드 선택기(일반/심화) — 웹 deepSelector() ─────────────────────
            BuildGameModeSelector(col, result);

            // ── P6: 승천(심화 학기) 선택기 — 웹 ascSelector()(WEB_PARITY_DESIGN.md §1-A #18) ──────
            BuildAscSelector(col, result);

            // ── hud 카드: w_panel_grad + bd 테두리 + r-xl ───────────────────────────────
            var panelGradSprite = UiSpriteGen.Load("w_panel_grad");
            var hud = UiKit.Panel(col, "Hud", Color.white, panelGradSprite);
            UiKit.SizeHint(hud, preferredHeight: 336, flexibleHeight: 0);
            UiKit.AddGlowOutline(hud.gameObject, UiKit.Bd, 2f).enabled = true;

            var hudCol = UiKit.VGroup(hud, 18, new RectOffset(26, 26, 22, 20), true, true);
            UiKit.Fill(hudCol);

            result.hudTitleText = UiKit.Text(hudCol, "", 29, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.hudTitleText, preferredHeight: 38, flexibleHeight: 0);

            var statsRow = UiKit.HGroup(hudCol, 15, new RectOffset(), true, true);
            UiKit.SizeHint(statsRow, preferredHeight: 130, flexibleHeight: 0);
            result.statScoreValue = BuildHudStatCell(statsRow, "최고 점수");
            result.statStageValue = BuildHudStatCell(statsRow, "최고 스테이지");
            result.statPlaysValue = BuildHudStatCell(statsRow, "플레이");

            // ── 요약줄: "업적 n/482 · 장치 n/16 해금" ───────────────────────────────────
            result.summaryText = UiKit.Text(col, "", 22, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.summaryText, preferredHeight: 32, flexibleHeight: 0);

            // ── bigbtn "게임 시작" + ghost×2(랭킹/도감) ─────────────────────────────────
            var goldBtnSprite = UiSpriteGen.Load("w_gold_btn");
            var ghostBtnSprite = UiSpriteGen.Load("w_ghost_btn");

            result.startButton = UiKit.Button(col, "▶ 게임 시작", new Vector2(0f, 150f), UiKit.Accent, UiKit.Ink, null, goldBtnSprite);
            UiKit.SizeHint(result.startButton, preferredHeight: 150, flexibleHeight: 0);

            var ghostRow = UiKit.HGroup(col, 16, new RectOffset(), true, true);
            UiKit.SizeHint(ghostRow, preferredHeight: 120, flexibleHeight: 0);

            result.rankButton = UiKit.Button(ghostRow, "🏆 랭킹", new Vector2(0f, 120f), UiKit.Panel2, UiKit.TextPrimary, null, ghostBtnSprite);
            UiKit.SizeHint(result.rankButton, flexibleWidth: 1, preferredHeight: 120, flexibleHeight: 0);
            UiKit.AddGlowOutline(result.rankButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            result.dexButton = UiKit.Button(ghostRow, "📚 도감", new Vector2(0f, 120f), UiKit.Panel2, UiKit.TextPrimary, null, ghostBtnSprite);
            UiKit.SizeHint(result.dexButton, flexibleWidth: 1, preferredHeight: 120, flexibleHeight: 0);
            UiKit.AddGlowOutline(result.dexButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            // ── 설명 2줄 ───────────────────────────────────────────────────────────────
            var desc = UiKit.Text(col,
                "캐릭터 · 슬롯머신 · 장치를 골라 스테이지를 클리어하세요.\n증강/유물/장치로 빌드를 키워 고득점에 도전!",
                21, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(desc, preferredHeight: 66, flexibleHeight: 0);

            // ── P4 A.5 + P5: 소리 토글 + 데이터 초기화(웹 renderHome, ui.js:630 한 행에 나란히
            // `<button class="reset-link sndtog withlabel" data-act="soundToggle">🔊 소리</button>
            // <button class="reset-link" data-act="resetAsk">⚠️ 데이터 초기화</button>`) ─────────────
            // ⚠(U+26A0)는 BMP 문자라 레거시 Text에서 정상 렌더링되지만 🔊/🔇(astral)는 안 되므로(S8
            // 항목⑤) 소리 토글은 아이콘 없이 "소리 켜짐/꺼짐" 텍스트만 쓴다(MenuView.RefreshSoundToggle).
            var linkRow = UiKit.HGroup(col, 16, new RectOffset(), true, true);
            UiKit.SizeHint(linkRow, preferredHeight: 60, flexibleHeight: 0);

            result.soundToggleButton = UiKit.Button(linkRow, "소리 켜짐", new Vector2(0f, 60f),
                new Color(0f, 0f, 0f, 0f), UiKit.TextPrimary, null);
            UiKit.SizeHint(result.soundToggleButton, flexibleWidth: 1, preferredHeight: 60, flexibleHeight: 0);
            result.soundToggleLabel = result.soundToggleButton.GetComponentInChildren<Text>();

            result.resetButton = UiKit.Button(linkRow, "⚠ 데이터 초기화", new Vector2(0f, 60f),
                new Color(0f, 0f, 0f, 0f), UiKit.Bad, null);
            UiKit.SizeHint(result.resetButton, flexibleWidth: 1, preferredHeight: 60, flexibleHeight: 0);

            var footerSpacer = UiKit.Panel(col, "FooterSpacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(footerSpacer, flexibleHeight: 1);

            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 gearbtn — 인트로/플레이 화면 우상단
            // 고정 진입점) — MenuView는 화면 전체가 col(VerticalLayoutGroup) 흐름이라, root의 최상위
            // 형제로 절대좌표 아이콘 버튼을 얹는다(BuildDropBanner와 동일 top-anchor 기법).
            result.settingsButton = BuildCornerIconButton(root, "⚙", "SettingsButton");

            return result;
        }

        // 우상단 고정 원형 아이콘 버튼(⚙ 설정 등 — U+2699 BMP라 레거시 Text에서 정상 렌더링된다, S8
        // 항목⑤ 기준 안전) — MenuView/RunHud 양쪽의 설정 진입점이 공유하는 작은 헬퍼.
        private static Button BuildCornerIconButton(Transform parent, string glyph, string name)
        {
            var btn = UiKit.Button(parent, glyph, new Vector2(64f, 64f), UiKit.Panel2, UiKit.TextPrimary, null, UiKit.PillSprite(64f));
            var rt = btn.GetComponent<RectTransform>();
            rt.name = name;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            UiKit.AddGlowOutline(btn.gameObject, UiKit.Bd2, 1.5f).enabled = true;
            return btn;
        }

        // hud-stats 한 칸 — rgba(0,0,0,.25) + bd + r-md + 상단 gloss, k(라벨)/v(값) 텍스트. v를
        // 반환해 MenuView가 매 Refresh마다 값만 바꿔 쓸 수 있게 한다.
        private static Text BuildHudStatCell(RectTransform row, string label)
        {
            var cell = UiKit.Panel(row, "Cell", new Color(0f, 0f, 0f, 0.25f), UiSpriteGen.Load("w_r12"));
            UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: 130, flexibleHeight: 0);
            UiKit.AddGlowOutline(cell.gameObject, UiKit.Bd, 1.5f).enabled = true;

            var inner = UiKit.VGroup(cell, 4, new RectOffset(6, 6, 12, 10), true, true);
            UiKit.Fill(inner);
            var k = UiKit.Text(inner, label, 19, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(k, preferredHeight: 26, flexibleHeight: 0);
            var v = UiKit.Text(inner, "0", 29, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(v, preferredHeight: 42, flexibleHeight: 0);

            AddGloss(cell, 65f); // 50% of 130

            return v;
        }

        private static void WireMenuView(MenuBuildResult r)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("hudTitleText").objectReferenceValue = r.hudTitleText;
            so.FindProperty("statScoreValue").objectReferenceValue = r.statScoreValue;
            so.FindProperty("statStageValue").objectReferenceValue = r.statStageValue;
            so.FindProperty("statPlaysValue").objectReferenceValue = r.statPlaysValue;
            so.FindProperty("summaryText").objectReferenceValue = r.summaryText;
            so.FindProperty("rankButton").objectReferenceValue = r.rankButton;
            so.FindProperty("mainButtonRect").objectReferenceValue =
                r.startButton != null ? r.startButton.GetComponent<RectTransform>() : null;
            so.FindProperty("levelCardButton").objectReferenceValue = r.levelCardButton;
            so.FindProperty("levelBadgeText").objectReferenceValue = r.levelBadgeText;
            so.FindProperty("levelXpText").objectReferenceValue = r.levelXpText;
            so.FindProperty("levelBarFill").objectReferenceValue = r.levelBarFill;
            so.FindProperty("levelBarFillImage").objectReferenceValue = r.levelBarFillImage;
            so.FindProperty("modeDeepButton").objectReferenceValue = r.modeDeepButton;
            so.FindProperty("ascSectionRoot").objectReferenceValue = r.ascSectionRoot;
            so.FindProperty("ascBadgeText").objectReferenceValue = r.ascBadgeText;
            so.FindProperty("ascLevelText").objectReferenceValue = r.ascLevelText;
            so.FindProperty("ascRuleText").objectReferenceValue = r.ascRuleText;
            so.FindProperty("ascHintText").objectReferenceValue = r.ascHintText;
            so.FindProperty("ascPrevButton").objectReferenceValue = r.ascPrevButton;
            so.FindProperty("ascNextButton").objectReferenceValue = r.ascNextButton;
            so.FindProperty("resetButton").objectReferenceValue = r.resetButton;
            so.FindProperty("resetConfirmPopup").objectReferenceValue = r.resetConfirmPopup;
            so.FindProperty("settingsButton").objectReferenceValue = r.settingsButton;
            so.FindProperty("settingsSheet").objectReferenceValue = r.settingsSheet;
            so.FindProperty("soundToggleButton").objectReferenceValue = r.soundToggleButton;
            so.FindProperty("soundToggleLabel").objectReferenceValue = r.soundToggleLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 웹 파리티 P4(§1-A #15 A.1) — 레벨 카드(웹 lvlCard(lp,clickable), ui.js:591-602) ─────────
        // MenuScreen(clickable=true, 탭하면 레벨 보상 화면)과 LevelRewardsScreen(clickable=false, 그
        // 화면 자신의 헤더 카드) 둘 다 이 헬퍼로 짓는다 — 배지("Lv.N") + 상단 라벨/XP 텍스트 + 진행바.
        private static LevelCardResult BuildLevelCard(Transform parent, bool clickable)
        {
            var result = new LevelCardResult();
            var panelSprite = UiSpriteGen.Load("w_panel_grad");
            var root = UiKit.Panel(parent, "LevelCard", Color.white, panelSprite);
            UiKit.SizeHint(root, preferredHeight: 150, flexibleHeight: 0);
            UiKit.AddGlowOutline(root.gameObject, UiKit.Bd, 2f).enabled = true;

            if (clickable)
            {
                var btn = root.gameObject.AddComponent<Button>();
                btn.targetGraphic = root.GetComponent<Image>();
                root.gameObject.AddComponent<PressFx>();
                result.button = btn;
            }

            // controlChildW=true — badge(preferredWidth 108,flexible 0)/body(flexibleWidth 1)가 실제로
            // 그 값대로 배정되려면 HorizontalLayoutGroup.childControlWidth가 켜져 있어야 한다(꺼져 있으면
            // LayoutElement 폭 지정이 무시되고 각 자식의 기존 RectTransform 크기가 그대로 쓰인다).
            var row = UiKit.HGroup(root, 20, new RectOffset(24, 24, 18, 18), true, true);
            UiKit.Fill(row);
            // Opus 2차검수(P4 1/3) 폴리시③ — Unity uGUI HorizontalLayoutGroup은 childForceExpandHeight=true
            // 이면 명시적 flexibleHeight=0인 자식도 내부적으로 Mathf.Max(flexible,1)로 강제 승격해 버려
            // (badge 108→행의 가용 높이만큼 늘어남·body가 위쪽에 붙어보임의 실제 원인) — UiKit.HGroup의
            // childControlHeight(=true, 위 true,true)는 유지한 채 이 필드만 꺼서 badge/body가 자기
            // preferred/flexible 값 그대로(강제 확장 없이) 배정되게 한다. childAlignment=MiddleLeft
            // (UiKit.HGroup 고정값)가 세로 중앙 정렬을 담당한다.
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;

            var badgeBg = UiKit.Panel(row, "Badge", UiKit.Hex("#2A3048"), UiSpriteGen.Load("w_r16"));
            UiKit.SizeHint(badgeBg, preferredWidth: 108, preferredHeight: 108, flexibleWidth: 0, flexibleHeight: 0);
            result.badgeText = UiKit.Text(badgeBg, "", 28, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Fill(result.badgeText.rectTransform);

            var body = UiKit.VGroup(row, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(body, flexibleWidth: 1, flexibleHeight: 0);

            var topRow = UiKit.HGroup(body, 8, new RectOffset(), true, true);
            UiKit.SizeHint(topRow, preferredHeight: 30, flexibleHeight: 0);
            var label = UiKit.Text(topRow, clickable ? "플레이어 레벨 · 보상 보기 ›" : "플레이어 레벨",
                20, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(label, flexibleWidth: 1, flexibleHeight: 0);
            result.xpText = UiKit.Text(topRow, "", 19, UiKit.TextSecondary, TextAnchor.MiddleRight);
            UiKit.SizeHint(result.xpText, preferredWidth: 260, flexibleHeight: 0);

            var barBg = UiKit.Panel(body, "Bar", UiKit.Hex("#2A3048"), UiSpriteGen.Load("bar_bg_r12"));
            UiKit.SizeHint(barBg, preferredHeight: 22, flexibleHeight: 0);
            result.barFill = UiKit.Panel(barBg, "Fill", UiKit.Accent, UiSpriteGen.Load("bar_fill_r12"));
            UiKit.SetAnchors(result.barFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            result.barFillImage = result.barFill.GetComponent<Image>();

            return result;
        }

        // ── 웹 파리티 P4(§1-A #15 A.2) — 게임 모드 선택기(웹 deepSelector(), ui.js:559-570) ─────────
        // Opus 2차검수(P4 1/3) 폴리시⑤ — 웹은 두 카드 다 <button>(탭 시 눌림 피드백)이라 "일반" 카드도
        // Button+PressFx를 붙인다(BuildModeCard가 항상 붙임). "일반"은 P7 이전엔 다른 모드로 전환할
        // 수단이 없어(항상 선택 상태) 클릭 리스너는 달지 않는다 — 존재 이유는 순수 터치 피드백(눌림
        // 스케일 애니메이션) 파리티다. "심화 · 심볼 덱"만 실제 리스너(토스트 안내)를 건다.
        private static void BuildGameModeSelector(RectTransform col, MenuBuildResult result)
        {
            var header = UiKit.Text(col, "게임 모드", 22, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(header, preferredHeight: 30, flexibleHeight: 0);

            var row = UiKit.HGroup(col, 14, new RectOffset(), true, true);
            UiKit.SizeHint(row, preferredHeight: 132, flexibleHeight: 0);

            BuildModeCard(row, "일반", "고정 확률 (기본)", selected: true, locked: false);
            var deepCard = BuildModeCard(row, "심화 · 심볼 덱", "주머니 확률 · 덱빌딩", selected: false, locked: true);
            result.modeDeepButton = deepCard.GetComponent<Button>();
        }

        private static RectTransform BuildModeCard(RectTransform parent, string title, string desc, bool selected, bool locked)
        {
            var card = UiKit.Panel(parent, "Mode_" + title, Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.SizeHint(card, flexibleWidth: 1, preferredHeight: 132, flexibleHeight: 0);
            UiKit.AddGlowOutline(card.gameObject, selected ? UiKit.Accent : UiKit.Bd, 2f).enabled = true;
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card.GetComponent<Image>();
            card.gameObject.AddComponent<PressFx>();

            var inner = UiKit.VGroup(card, 4, new RectOffset(16, 16, 16, 14), true, true);
            UiKit.Fill(inner);
            var nameText = UiKit.Text(inner, title, 21, selected ? UiKit.Accent : UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(nameText, preferredHeight: 28, flexibleHeight: 0);
            var descText = UiKit.Text(inner, desc, 16, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(descText, preferredHeight: 22, flexibleHeight: 0);
            if (locked)
            {
                var badge = UiKit.Text(inner, "준비 중", 15, UiKit.TextSecondary, TextAnchor.MiddleCenter, true);
                UiKit.SizeHint(badge, preferredHeight: 22, flexibleHeight: 0);
            }

            return card;
        }

        // ── 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — 승천(심화 학기) 선택기(웹 ascSelector(),
        // ui.js:572-590) — header(제목+배지) → 카드(◀ 배지/점수보정·규칙문구 ▶) → 힌트 1줄.
        // MenuView.RefreshAscSelector가 profile.AscUnlocked()==false면 sectionRoot 전체를 SetActive(false)
        // 한다(웹은 렌더 자체를 생략 — Unity는 씬 구조 유지 + 비활성으로 동등 구현, BuildModeCard와
        // 동일한 카드 룩을 재사용해 게임 모드 선택기 바로 아래 자리에 놓는다).
        private static void BuildAscSelector(RectTransform col, MenuBuildResult result)
        {
            var section = UiKit.VGroup(col, 10, new RectOffset(), true, true, autoSizeH: true);
            result.ascSectionRoot = section;

            var headerRow = UiKit.HGroup(section, 10, new RectOffset(), true, true);
            UiKit.SizeHint(headerRow, preferredHeight: 30, flexibleHeight: 0);
            var header = UiKit.Text(headerRow, "심화 학기", 22, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(header, flexibleWidth: 1, flexibleHeight: 0);
            result.ascBadgeText = UiKit.Text(headerRow, "", 20, UiKit.Accent, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(result.ascBadgeText, preferredWidth: 140, flexibleHeight: 0);

            var card = UiKit.Panel(section, "AscCard", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.SizeHint(card, preferredHeight: 146, flexibleHeight: 0);
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd, 2f).enabled = true;

            var cardCol = UiKit.VGroup(card, 6, new RectOffset(16, 16, 14, 12), true, true);
            UiKit.Fill(cardCol);

            var ctlRow = UiKit.HGroup(cardCol, 10, new RectOffset(), true, true);
            UiKit.SizeHint(ctlRow, preferredHeight: 60, flexibleHeight: 0);
            var pillBtn56 = UiKit.PillSprite(56f);
            result.ascPrevButton = UiKit.Button(ctlRow, "◀", new Vector2(56f, 56f), UiKit.Panel2, UiKit.TextPrimary, null, pillBtn56);
            UiKit.SizeHint(result.ascPrevButton, preferredWidth: 56, preferredHeight: 56, flexibleWidth: 0, flexibleHeight: 0);

            var midCol = UiKit.VGroup(ctlRow, 2, new RectOffset(), true, true);
            UiKit.SizeHint(midCol, flexibleWidth: 1, flexibleHeight: 0);
            result.ascLevelText = UiKit.Text(midCol, "", 19, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.ascLevelText, preferredHeight: 26, flexibleHeight: 0);
            result.ascRuleText = UiKit.Text(midCol, "", 15, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.ascRuleText, preferredHeight: 22, flexibleHeight: 0);

            result.ascNextButton = UiKit.Button(ctlRow, "▶", new Vector2(56f, 56f), UiKit.Panel2, UiKit.TextPrimary, null, pillBtn56);
            UiKit.SizeHint(result.ascNextButton, preferredWidth: 56, preferredHeight: 56, flexibleWidth: 0, flexibleHeight: 0);

            result.ascHintText = UiKit.Text(cardCol, "", 14, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.ascHintText, preferredHeight: 40, flexibleHeight: 0);
        }

        // ── PickView 화면 — S10: public/jackpotpick/index.html DOM 순서 그대로 재구성 ──────
        // head(타이틀+lead+who) → tabs → recos → toolbar(chips+sort) → sechead → grid → summary.
        // 뒤로가기 버튼은 웹에 없는 앱 전용 내비게이션이라 head 위 별도 소형 행으로 유지한다.
        private static PickBuildResult BuildPickScreen(Transform canvasRoot)
        {
            var result = new PickBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");
            // S13 §A: recos 4버튼(높이 62)에 chip_r999(border 128)를 쓰면 늘어난다 — PillSprite(62)로 교체.
            var pillSprite = UiKit.PillSprite(62f);
            var r13Sprite = UiSpriteGen.Load("rrect_r13");

            var root = UiKit.Panel(canvasRoot, "PickScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<PickView>();

            var col = UiKit.VGroup(root, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);
            // 루트 VGroup은 childForceExpandHeight=false(UiKit.VGroup 고정값) — 아래 각 행은 전부
            // preferredHeight+flexibleHeight=0으로 "명시"해 잔여 공간을 나눠 갖지 못하게 한다.
            // 그리드 스크롤만 flexibleHeight=1로 잔여 전부를 가져간다(S9 Fable 육안 검수 수정 지시 유지).

            // 뒤로가기 — 36 소형 행(웹에는 없는 앱 전용 내비게이션, 최소한으로 축소).
            var navRow = UiKit.HGroup(col, 0, new RectOffset(24, 24, 10, 0), true, false);
            UiKit.SizeHint(navRow, preferredHeight: 46, flexibleHeight: 0);
            result.backButton = UiKit.Button(navRow, "← 메뉴", new Vector2(140, 46), UiKit.PanelBg, UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 140, preferredHeight: 46, flexibleWidth: 0, flexibleHeight: 0);

            // 1) head — index.html .head: h1(sub만 골드) + .lead + .who. 값은 폰트 ×1.6(13.5→22, 14→22).
            var head = UiKit.VGroup(col, 4, new RectOffset(24, 24, 4, 0), true, true);
            head.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.SizeHint(head, preferredHeight: 108, flexibleHeight: 0);
            var headTitle = UiKit.Text(head, "잭팟런 — <color=#FFD23F>시작 조합 선택</color>", 38, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            headTitle.supportRichText = true;
            UiKit.SizeHint(headTitle, preferredHeight: 46, flexibleHeight: 0);
            var headLead = UiKit.Text(head, "캐릭터 + 슬롯머신 + 장치 조합으로 이번 런의 방향을 정하세요.", 22, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(headLead, preferredHeight: 28, flexibleHeight: 0);
            result.headWhoText = UiKit.Text(head, "", 22, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.headWhoText, preferredHeight: 30, flexibleHeight: 0);

            // 2) tabs — .tabs 3탭: .tnum(① 등, 완료 시 그린 ✓) + 제목 + .tpick(선택값).
            var tabsRow = UiKit.HGroup(col, 12, new RectOffset(24, 24, 10, 8), true, true);
            UiKit.SizeHint(tabsRow, preferredHeight: 128, flexibleHeight: 0);
            string[] tabNums = { "①", "②", "③" };
            string[] tabTitles = { "캐릭터", "슬롯머신", "장치" };
            result.tabButtons = new Button[3];
            result.tabButtonImages = new Image[3];
            result.tabNumTexts = new Text[3];
            result.tabLabelTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var (btn, bg, num, label) = BuildTabButton(tabsRow, tabNums[i], tabTitles[i], r13Sprite);
                result.tabButtons[i] = btn;
                result.tabButtonImages[i] = bg;
                result.tabNumTexts[i] = num;
                result.tabLabelTexts[i] = label;
            }

            // 3) recos — .recos 4 pill(입문=teal/고점=pink/도전=red 테두리, 랜덤=기본). astral 이모지
            // (🌱🔥😈🎲)는 렌더링되지 않아 한글 라벨만 사용(S8 항목⑤ 관례).
            var recoRow = UiKit.HGroup(col, 10, new RectOffset(24, 24, 4, 10), true, false);
            UiKit.SizeHint(recoRow, preferredHeight: 62, flexibleHeight: 0);
            recoRow.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string[] recoLabels = { "입문 추천", "고점 추천", "도전 조합", "랜덤" };
            Color[] recoBorders = { UiKit.Teal, UiKit.Pink, UiKit.Red, UiKit.Bd2 };
            result.recoButtons = new Button[recoLabels.Length];
            for (int i = 0; i < recoLabels.Length; i++)
            {
                var btn = UiKit.Button(recoRow, recoLabels[i], new Vector2(0, 62), new Color(1f, 1f, 1f, 0.03f), UiKit.TextPrimary, null, pillSprite);
                UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 62, flexibleHeight: 0);
                var recoOutline = UiKit.AddGlowOutline(btn.gameObject, recoBorders[i], 1.5f);
                recoOutline.enabled = true; // 상시 표시 — pick.css .reco.beginner/.high/.challenge 테두리색 재해석(글로우로 근사).
                result.recoButtons[i] = btn;
            }

            // 4) toolbar — .chips(필터, 가로 스크롤+템플릿) + .sortrow(정렬 4버튼 — Unity 표준 select가
            // 없어 기존 버튼열 그대로 유지, 색만 통일. 재해석 항목으로 보고 대상).
            var chipScroll = UiKit.Scroll(col, out var chipsContent, vertical: false);
            UiKit.SizeHint(chipScroll, preferredHeight: 60, flexibleHeight: 0);
            var chipsHlg = chipsContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipsHlg.spacing = 10;
            chipsHlg.padding = new RectOffset(20, 20, 6, 6);
            chipsHlg.childControlWidth = false;
            chipsHlg.childControlHeight = true;
            chipsHlg.childForceExpandWidth = false;
            chipsHlg.childForceExpandHeight = false;
            chipsHlg.childAlignment = TextAnchor.MiddleLeft;
            var chipsCsf = chipsContent.gameObject.AddComponent<ContentSizeFitter>();
            chipsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            result.chipsContent = chipsContent;
            result.chipTemplate = BuildChipTemplate(chipsContent);

            var sortRow = UiKit.HGroup(col, 10, new RectOffset(24, 24, 6, 10), true, false);
            UiKit.SizeHint(sortRow, preferredHeight: 60, flexibleHeight: 0);
            sortRow.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string[] sortLabels = { "추천순", "난이도순", "고점순", "최근해금순" };
            result.sortButtons = new Button[sortLabels.Length];
            result.sortButtonImages = new Image[sortLabels.Length];
            for (int i = 0; i < sortLabels.Length; i++)
            {
                var btn = UiKit.Button(sortRow, sortLabels[i], new Vector2(0, 60), UiKit.PanelBg, UiKit.TextPrimary, null, panelSprite);
                UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 60, flexibleHeight: 0);
                result.sortButtons[i] = btn;
                result.sortButtonImages[i] = btn.GetComponent<Image>();
            }

            // 5) sechead — .sechead: 제목 + "해금 n/m".
            var sechead = UiKit.HGroup(col, 8, new RectOffset(24, 24, 6, 6), true, true);
            UiKit.SizeHint(sechead, preferredHeight: 40, flexibleHeight: 0);
            result.sectionTitleText = UiKit.Text(sechead, "캐릭터", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(result.sectionTitleText, flexibleWidth: 1, flexibleHeight: 0);
            result.sectionCountText = UiKit.Text(sechead, "", 19, UiKit.TextSecondary, TextAnchor.MiddleRight);
            UiKit.SizeHint(result.sectionCountText, preferredWidth: 240, flexibleWidth: 0, flexibleHeight: 0);

            // 6) 카드 그리드(세로 스크롤 + 템플릿) — 잔여 전부(flexibleHeight=1), 이 행만 flexible.
            // 셀 320(Fable 육안 검수 수정 지시: 460은 과함 — 카드 실제 콘텐츠 예산 ≈300~316에 맞춤).
            var gridScroll = UiKit.Scroll(col, out var gridContent, vertical: true);
            UiKit.SizeHint(gridScroll, preferredHeight: 0, flexibleHeight: 1);
            UiKit.Grid(gridContent, new Vector2(500, 320), new Vector2(16, 16), 2);
            // 하단 패딩에 요약시트 높이(560)만큼 여유를 더해, 마지막 줄 카드가 요약시트에 가려지지
            // 않고 끝까지 스크롤해 볼 수 있게 한다(Fable 육안 검수 수정 지시 D).
            gridContent.gameObject.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 12, 8, 20 + 560);
            result.gridContent = gridContent;
            result.gridCanvasGroup = gridContent.gameObject.AddComponent<CanvasGroup>();
            result.cardTemplate = BuildCardTemplate(gridContent);

            // 7) summary — 하단 고정 시트
            BuildSummaryPanel(col, result, panelSprite);

            return result;
        }

        // S10 — pick.css .tab: .tnum(11px dim2, 완료 시 " ✓" green 접미는 PickView가 텍스트로 append)
        // + 제목(13.5px 800) + .tpick(11px gold). 반경 13(r13Sprite)으로 pick.css .tab{border-radius:13px}.
        private static (Button btn, Image bg, Text num, Text label) BuildTabButton(Transform parent, string num, string title, Sprite sprite)
        {
            var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = UiKit.PanelBg;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            // 탭 행(tabsRow) 자체는 128 고정(BuildPickScreen), 탭 버튼은 forceExpandHeight=true로 그
            // 행 전체 높이를 채우는 큰 터치영역이 의도다 — preferredHeight는 채워질 값의 기준선일 뿐.
            UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 114, flexibleHeight: 0);

            var col = UiKit.VGroup(rt, 3, new RectOffset(8, 8, 10, 10), true, true);
            UiKit.Fill(col);
            // 3줄(tnum+제목+tpick, 합계 ~74px)이 채워진 탭 높이 안에서 위로 쏠리지 않도록 중앙 정렬
            // (UiKit.VGroup 공용 헬퍼는 UpperCenter 고정이라 여기서 컴포넌트를 직접 덮어쓴다).
            col.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var numText = UiKit.Text(col, num, 18, UiKit.Dim2, TextAnchor.MiddleCenter, true);
            numText.supportRichText = true;
            UiKit.SizeHint(numText, preferredHeight: 24, flexibleHeight: 0);
            var titleText = UiKit.Text(col, title, 22, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 30, flexibleHeight: 0);
            var labelText = UiKit.Text(col, "선택 전", 18, UiKit.Accent, TextAnchor.MiddleCenter);
            UiKit.SizeHint(labelText, preferredHeight: 24, flexibleHeight: 0);

            return (btn, img, numText, labelText);
        }

        // 필터 칩 템플릿 — HorizontalLayoutGroup+ContentSizeFitter로 라벨 길이에 맞춰 스스로 너비를
        // 정한다(부모 chipsContent는 childControlWidth=false라 이 자기-크기결정과 충돌하지 않는다).
        private static RectTransform BuildChipTemplate(Transform parent)
        {
            // S13 §A: ContentSizeFitter 자기-사이징 높이 근사(폰트20 줄높이×1.2 + 상하 패딩10×2 ≈ 44) —
            // chip_r999(border 128)는 이 높이에서 크게 늘어난다. PillSprite(44)로 교체.
            var chip = UiKit.Panel(parent, "ChipTemplate", UiKit.Card, UiKit.PillSprite(44f));
            var chipImg = chip.GetComponent<Image>();
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = chipImg;
            chip.gameObject.AddComponent<PressFx>();

            var hlg = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 10, 10);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            var csf = chip.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = UiKit.Text(chip, "라벨", 20, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            label.name = "Label";

            chip.gameObject.SetActive(false);
            return chip;
        }

        // 자기 텍스트 길이에 맞춰 스스로 폭을 정하는 필 배지 — 난이도 배지(Badge)/태그 칩(Tag0..3)/
        // 코너 배지(Corner)/요약 빌드칩 공용(BuildChipTemplate과 동일 기법: HorizontalLayoutGroup+
        // ContentSizeFitter로 라벨 길이만큼 폭을 정한다). 부모가 LayoutGroup이 아니면(예: 카드 루트에
        // 바로 얹는 Corner) 세로도 스스로 정해야 하므로 verticalFit도 같이 켠다(SizeHint로 부모가
        // 통제하는 경우는 그 값이 우선한다).
        private static (RectTransform root, Image bg, Text label) BuildAutoPill(
            Transform parent, string name, Sprite sprite, int fontSize, RectOffset padding, bool bold)
        {
            var pill = UiKit.Panel(parent, name, UiKit.PanelBg, sprite);
            var img = pill.GetComponent<Image>();
            var hlg = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = padding;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            var csf = pill.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = UiKit.Text(pill, "", fontSize, UiKit.TextPrimary, TextAnchor.MiddleCenter, bold);
            label.name = "Label";
            return (pill, img, label);
        }

        // 카드 템플릿 — S10: pick.css .jcard 구조 그대로 이식(좌측 난이도색 스트라이프 + 좌측 아이콘83×83
        // + 우측 이름/역할 + 효과 박스 + 태그 칩 + 장단점·추천빌드(또는 잠금 시 해금 박스) + 우상단
        // 코너 배지). 자식 경로 계약(PickView.cs Find 경로와 정확히 일치해야 한다): "Stripe"(Image),
        // "Body/Top/IconSlot/Icon"·"IconEmoji", "Body/Top/Info/NameRow/Name"·"Badge"(+"/Label"),
        // "Body/Top/Info/Role", "Body/Eff/Text", "Body/Tags/Tag0".."Tag3"(+"/Label"), "Body/ProsCons",
        // "Body/Foot", "Body/UnlockBox/Text", "Corner"(+"/Label"). 폰트/치수는 pick.css px × 1.6
        // (S10 설계 지시), 라운드 반경은 UiSpriteGen rrect_rN 이름 그대로(스케일 없음).
        private static RectTransform BuildCardTemplate(Transform parent)
        {
            var r7 = UiSpriteGen.Load("rrect_r7");
            var r9 = UiSpriteGen.Load("rrect_r9");
            var r11 = UiSpriteGen.Load("rrect_r11");
            var r13 = UiSpriteGen.Load("rrect_r13");
            var cardGrad15 = UiSpriteGen.Load("card_grad_r15");

            var card = UiKit.Panel(parent, "CardTemplate", UiKit.Panel2, cardGrad15);
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card.GetComponent<Image>();
            card.gameObject.AddComponent<PressFx>();
            UiKit.AddGlowOutline(card.gameObject, UiKit.Accent, 3f); // 선택 시 펄스(PickView.PulseOutline 그대로 유지)
            card.gameObject.AddComponent<CanvasGroup>(); // 잠금 시 알파 .62 — pick.css .jcard.locked{opacity:.62} 재해석

            // 좌측 4px→6px 난이도색 스트라이프(.jcard::before) — 카드 루트 직계 자식, Body 패딩 왼쪽에
            // 그만큼(6) 여유를 둬 겹치지 않게 한다.
            var stripe = UiKit.Panel(card, "Stripe", UiKit.Bd);
            stripe.name = "Stripe";
            UiKit.SetAnchors(stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));

            // Fable 육안 검수 수정(2026-07-31): 카드 전체 예산을 320 셀에 맞춰 재budget(패딩/스페이싱
            // 축소 + Top 92 고정 + Eff/Tags/ProsCons/Foot 축소). 잠금 시(UnlockBox=90)도 동일 총합이
            // 되도록 맞춰(ProsCons60+Foot20+gap10=90) 카드가 상태에 따라 커지지 않는다.
            var body = UiKit.VGroup(card, 10, new RectOffset(25, 19, 14, 12), true, true);
            body.name = "Body";
            UiKit.Fill(body);

            // ── Top: 아이콘83×83(좌) + 이름/배지/역할(우). align-items:center 재현 — BuildTabButton과
            // 달리 Top은 controlChildH=**false**로 자식을 강제로 늘리지 않는다(Image인 IconSlot을
            // forceExpandHeight로 늘리면 세로로 찌그러진다 — Text만 다루는 BuildTabButton과의 차이).
            // 대신 IconSlot/Info 둘 다 실제 RectTransform.sizeDelta를 직접 고정하고, Top의
            // childAlignment(HGroup 기본 MiddleLeft)로 그 두 블록을 행 높이 안에서 세로 중앙 정렬한다.
            //
            // S13 §C 겹침 수정: Info(controlChildH=false)의 자식 NameRow/Role도 같은 이유로 실측
            // 사이즈를 직접 고정해야 했다 — LayoutElement(SizeHint)만으로는 "포지션 계산"(pos 누적)엔
            // 반영되지만 "자기 자신의 렌더 크기"엔 반영되지 않는다(Unity LayoutGroup이 controlSize=false
            // 축은 자식 RectTransform.sizeDelta를 건드리지 않는다 — 위 IconSlot/Info 주석과 동일 함정).
            // 그 결과 NameRow/Role이 새 RectTransform 기본값(100×100)인 채로 남아 Info의 83px 안에서
            // "34+24" 슬롯 계산과 무관하게 100px씩 그려지며 아래로 Eff 박스를 침범했다 — 이번에 실제
            // 크기(34/24)를 고정하고, Info 높이도 실제 내용 합(34+spacing3+24=61)으로, Top 높이도
            // 그 결과(binding 제약은 여전히 아이콘 83)에 맞춰 92→83으로 재계산했다.
            var top = UiKit.HGroup(body, 18, new RectOffset(0, 0, 0, 0), true, false);
            top.name = "Top";
            UiKit.SizeHint(top, preferredHeight: 83, flexibleHeight: 0);

            // S15 §D: S10 리터럴 #0E1019(≈Bg1과 거의 동일) → §0 표 토큰(Bg1)으로 교체.
            var iconSlot = UiKit.Panel(top, "IconSlot", UiKit.Bg1, r11);
            UiKit.SizeHint(iconSlot, preferredWidth: 83, preferredHeight: 83, flexibleWidth: 0, flexibleHeight: 0);
            iconSlot.sizeDelta = new Vector2(83f, 83f); // controlChildH=false라 LayoutElement만으론 부족 — 실측 크기 직접 고정.
            var icon = UiKit.Image(iconSlot, null, Color.white);
            icon.name = "Icon";
            UiKit.Fill(icon.rectTransform);
            var iconEmoji = UiKit.Text(iconSlot, "", 34, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            iconEmoji.name = "IconEmoji";
            UiKit.Fill(iconEmoji.rectTransform);

            var info = UiKit.VGroup(top, 3, new RectOffset(0, 0, 0, 0), true, false);
            info.name = "Info";
            UiKit.SizeHint(info, flexibleWidth: 1);
            info.sizeDelta = new Vector2(info.sizeDelta.x, 61f); // NameRow34+spacing3+Role24 = 61(실제 내용 합).
            info.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            var nameRow = UiKit.HGroup(info, 8, new RectOffset(0, 0, 0, 0), true, true);
            nameRow.name = "NameRow";
            UiKit.SizeHint(nameRow, preferredHeight: 34, flexibleHeight: 0);
            nameRow.sizeDelta = new Vector2(nameRow.sizeDelta.x, 34f); // Info가 controlChildH=false라 실측 크기 직접 고정.
            var nameText = UiKit.Text(nameRow, "", 25, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            nameText.name = "Name";
            UiKit.SizeHint(nameText, flexibleWidth: 1, flexibleHeight: 0);
            var (badgeRoot, badgeBg, badgeLabel) = BuildAutoPill(nameRow, "Badge", r7, 17, new RectOffset(11, 11, 3, 3), true);
            // S15 §D: S10 리터럴 #15161F(≈Ink와 거의 동일) → §0 표 토큰(Ink)으로 교체.
            badgeLabel.color = UiKit.Ink;

            var role = UiKit.Text(info, "", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            role.name = "Role";
            UiKit.SizeHint(role, preferredHeight: 24, flexibleHeight: 0);
            role.rectTransform.sizeDelta = new Vector2(role.rectTransform.sizeDelta.x, 24f); // 위와 동일한 이유.

            // ── Eff: 효과 박스(.jc-eff) ──
            var eff = UiKit.Panel(body, "Eff", new Color(1f, 1f, 1f, 0.035f), r9);
            UiKit.SizeHint(eff, preferredHeight: 52, flexibleHeight: 0);
            // S15 §D: S10 리터럴 #CDD3E6(카드 본문 보조 텍스트) → §0 표 토큰(Txt2)으로 교체.
            var effText = UiKit.Text(eff, "", 20, UiKit.Txt2, TextAnchor.UpperLeft);
            effText.name = "Text";
            UiKit.SetAnchors(effText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));

            // ── Tags: 태그 칩(.jc-tags) 최대 4개 고정 슬롯 — Unity uGUI에 flex-wrap이 없어 한 줄
            // 비줄바꿈으로 재해석(대부분 엔트리가 1~3개라 실질 영향 적음, S10 보고 대상).
            var tags = UiKit.HGroup(body, 8, new RectOffset(0, 0, 0, 0), false, true);
            tags.name = "Tags";
            UiKit.SizeHint(tags, preferredHeight: 26, flexibleHeight: 0);
            for (int i = 0; i < 4; i++)
                BuildAutoPill(tags, "Tag" + i, r7, 17, new RectOffset(11, 11, 3, 3), true);

            // ── 장점(최대2)/주의(최대1) 리치텍스트(.jc-pc) + 추천빌드(.jc-foot) — 잠금 시 숨기고
            // UnlockBox로 대체(app.js "unlocked ? bodyExtra : lockHint"와 동일 분기, PickView가 토글).
            var prosCons = UiKit.Text(body, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            prosCons.name = "ProsCons";
            prosCons.supportRichText = true;
            UiKit.SizeHint(prosCons, preferredHeight: 60, flexibleHeight: 0);

            var foot = UiKit.Text(body, "", 18, UiKit.TextSecondary, TextAnchor.UpperLeft);
            foot.name = "Foot";
            foot.supportRichText = true;
            UiKit.SizeHint(foot, preferredHeight: 20, flexibleHeight: 0);

            // ── UnlockBox(.jc-unlock, 점선 테두리는 Image 단색 한계로 생략 — S10 재해석 항목) — 잠금 시
            // ProsCons+Foot(합 90, gap 포함) 자리를 그대로 대신 차지해 카드 총 높이가 변하지 않는다.
            var unlockBox = UiKit.Panel(body, "UnlockBox", new Color(1f, 0.824f, 0.247f, 0.08f), r9);
            unlockBox.name = "UnlockBox";
            UiKit.SizeHint(unlockBox, preferredHeight: 90, flexibleHeight: 0);
            var unlockText = UiKit.Text(unlockBox, "", 18, UiKit.Accent, TextAnchor.UpperLeft);
            unlockText.name = "Text";
            unlockText.supportRichText = true;
            UiKit.SetAnchors(unlockText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-14f, -10f));

            // ── Corner: 잠금/선택 배지(app.js corner = !unlocked ? lock : selected ? check : "" —
            // 상호배타라 노드 하나를 재사용, 배경·글자색은 PickView가 상태별로 다시 칠한다).
            // Fable 육안 검수 2차 수정(2026-07-31): chip_r999(9-slice border 128)를 이 작은 배지에
            // 쓰면서 ContentSizeFitter 자기-사이징과 경합해 지름 300px짜리 원으로 폭주하는 버그가
            // 있었다 — LayoutGroup/ContentSizeFitter를 아예 쓰지 않고 고정 크기(130×40, 작은 반경
            // r13)로 직접 명시한다. 텍스트는 Fill로 겹쳐 중앙 정렬(내용이 "잠김"/"선택됨 ✓" 둘뿐이라
            // 자기-사이징이 필요 없다).
            var corner = UiKit.Panel(card, "Corner", UiKit.PanelBg, r13);
            corner.anchorMin = corner.anchorMax = new Vector2(1f, 1f);
            corner.pivot = new Vector2(1f, 1f);
            corner.sizeDelta = new Vector2(130f, 40f);
            corner.anchoredPosition = new Vector2(-12f, -12f);
            var cornerLabel = UiKit.Text(corner, "", 18, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            cornerLabel.name = "Label";
            UiKit.Fill(cornerLabel.rectTransform);
            corner.gameObject.SetActive(false);

            card.gameObject.SetActive(false);
            return card;
        }

        // S10 — pick.css .summary/.sum-compact/.sd-* 이식. 고정 560(안의 각 줄 전부 flexibleHeight=0
        // 명시, colsRow(장점/주의)만 flexibleHeight=1로 잔여를 가져간다 — 예산:
        // 36+24+68+64+90(min)+36+112 + spacing8×6=48 + padding32 = 510, 560-510=50이 colsRow 여유).
        private static void BuildSummaryPanel(Transform parent, PickBuildResult result, Sprite panelSprite)
        {
            var r9 = UiSpriteGen.Load("rrect_r9");
            var r11 = UiSpriteGen.Load("rrect_r11");
            // S13 §A: 빌드 칩(BuildAutoPill 자기-사이징, 폰트17 줄높이×1.2 + 상하 패딩5×2 ≈ 30) —
            // chip_r999(border 128)는 늘어난다. PillSprite(30)로 교체.
            var pill999 = UiKit.PillSprite(30f);

            var panel = UiKit.Panel(parent, "Summary", UiKit.PanelBg, panelSprite);
            UiKit.SizeHint(panel, preferredHeight: 560, flexibleHeight: 0);
            var col = UiKit.VGroup(panel, 8, new RectOffset(24, 24, 16, 16), true, true);
            UiKit.Fill(col);

            // .sum-combo(조합명, 좌) + .sum-grade(등급 배지, 우) 한 행에.
            var comboRow = UiKit.HGroup(col, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(comboRow, preferredHeight: 36, flexibleHeight: 0);
            result.comboText = UiKit.Text(comboRow, "", 26, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(result.comboText, flexibleWidth: 1, flexibleHeight: 0);
            var gradeBadge = UiKit.Panel(comboRow, "GradeBadge", new Color(0f, 0f, 0f, 0f), r9);
            UiKit.SizeHint(gradeBadge, preferredWidth: 148, preferredHeight: 34, flexibleWidth: 0, flexibleHeight: 0);
            result.gradeBadgeImage = gradeBadge.GetComponent<Image>();
            result.gradeText = UiKit.Text(gradeBadge, "", 19, UiKit.TextSecondary, TextAnchor.MiddleCenter, true);
            UiKit.Fill(result.gradeText.rectTransform);

            // .bl(빌드토큰 골드 요약줄).
            result.comboBuildText = UiKit.Text(col, "", 18, UiKit.Accent, TextAnchor.MiddleLeft);
            UiKit.SizeHint(result.comboBuildText, preferredHeight: 24, flexibleHeight: 0);

            var meterRow = UiKit.HGroup(col, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(meterRow, preferredHeight: 68, flexibleHeight: 0);
            var ceil = BuildMeterCell(meterRow, "점수 고점", r11);
            var stab = BuildMeterCell(meterRow, "안정성", r11);
            var diff = BuildMeterCell(meterRow, "난이도", r11);
            result.ceilingValueText = ceil.value;
            result.stabilityValueText = stab.value;
            result.difficultyValueText = diff.value;
            result.difficultyLabelText = diff.label;

            // .sd-blurb(골드 톤 박스).
            var blurbPanel = UiKit.Panel(col, "Blurb", new Color(1f, 0.824f, 0.247f, 0.06f), r11);
            UiKit.SizeHint(blurbPanel, preferredHeight: 64, flexibleHeight: 0);
            result.blurbText = UiKit.Text(blurbPanel, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            result.blurbText.supportRichText = true;
            UiKit.SetAnchors(result.blurbText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));

            // .sd-cols(장점▲/주의▼ 2열, ▲▼ 프리픽스는 PickView가 리치텍스트로 넣는다).
            var colsRow = UiKit.HGroup(col, 20, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(colsRow, minHeight: 90, flexibleHeight: 1);
            result.prosText = BuildListCell(colsRow, "장점", UiKit.Green);
            result.consText = BuildListCell(colsRow, "주의", UiKit.ConWarn);

            // .sd-builds(빌드 토큰 칩 로우) — 템플릿은 자기 텍스트 길이만큼 폭을 정하는 필(BuildAutoPill),
            // 인스턴스는 PickView.UpdateSummary가 buildChipsContent 아래로 복제한다(필터 칩과 동일 기법).
            var buildRow = UiKit.HGroup(col, 8, new RectOffset(0, 0, 0, 0), false, true);
            buildRow.name = "BuildChips";
            UiKit.SizeHint(buildRow, preferredHeight: 36, flexibleHeight: 0);
            result.buildChipsContent = buildRow;
            result.buildChipTemplate = BuildAutoPill(buildRow, "BuildChipTemplate", pill999, 17, new RectOffset(12, 12, 5, 5), true).root;
            result.buildChipTemplate.gameObject.SetActive(false);

            // 활성(interactable) 상태 = Accent(#FFD23F) 배경 + 검정(UiKit.Bg) 글자로 또렷하게. 비활성일
            // 때만 Button.disabledColor/PressFx 알파로 흐려진다 — 캐릭터·머신 미선택 상태의 기본값.
            result.startButton = UiKit.Button(col, "이 조합으로 시작", new Vector2(0, 112), UiKit.Accent, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(result.startButton, preferredHeight: 112, flexibleHeight: 0);
        }

        private static (Text label, Text value) BuildMeterCell(RectTransform row, string label, Sprite sprite)
        {
            var cellPanel = UiKit.Panel(row, "Meter", UiKit.PanelBg, sprite);
            UiKit.SizeHint(cellPanel, flexibleWidth: 1, flexibleHeight: 0);
            var cell = UiKit.VGroup(cellPanel, 2, new RectOffset(10, 10, 8, 8), true, true);
            UiKit.Fill(cell);
            var l = UiKit.Text(cell, label, 18, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(l, preferredHeight: 22);
            var v = UiKit.Text(cell, "", 22, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(v, preferredHeight: 28);
            return (l, v);
        }

        private static Text BuildListCell(RectTransform row, string title, Color color)
        {
            var cell = UiKit.VGroup(row, 4, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(cell, flexibleWidth: 1);
            var t = UiKit.Text(cell, title, 19, color, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(t, preferredHeight: 26);
            var body = UiKit.Text(cell, "", 18, UiKit.TextPrimary, TextAnchor.UpperLeft);
            body.supportRichText = true;
            UiKit.SizeHint(body, flexibleHeight: 1);
            return body;
        }

        private static void WirePickView(PickBuildResult r)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("headWhoText").objectReferenceValue = r.headWhoText;
            SetObjectArray(so, "recoButtons", r.recoButtons);
            SetObjectArray(so, "tabButtons", r.tabButtons);
            SetObjectArray(so, "tabButtonImages", r.tabButtonImages);
            SetObjectArray(so, "tabNumTexts", r.tabNumTexts);
            SetObjectArray(so, "tabLabelTexts", r.tabLabelTexts);
            so.FindProperty("chipsContent").objectReferenceValue = r.chipsContent;
            so.FindProperty("chipTemplate").objectReferenceValue = r.chipTemplate;
            SetObjectArray(so, "sortButtons", r.sortButtons);
            SetObjectArray(so, "sortButtonImages", r.sortButtonImages);
            so.FindProperty("sectionTitleText").objectReferenceValue = r.sectionTitleText;
            so.FindProperty("sectionCountText").objectReferenceValue = r.sectionCountText;
            so.FindProperty("gridContent").objectReferenceValue = r.gridContent;
            so.FindProperty("gridCanvasGroup").objectReferenceValue = r.gridCanvasGroup;
            so.FindProperty("cardTemplate").objectReferenceValue = r.cardTemplate;
            so.FindProperty("comboText").objectReferenceValue = r.comboText;
            so.FindProperty("comboBuildText").objectReferenceValue = r.comboBuildText;
            so.FindProperty("gradeText").objectReferenceValue = r.gradeText;
            so.FindProperty("gradeBadgeImage").objectReferenceValue = r.gradeBadgeImage;
            so.FindProperty("ceilingValueText").objectReferenceValue = r.ceilingValueText;
            so.FindProperty("stabilityValueText").objectReferenceValue = r.stabilityValueText;
            so.FindProperty("difficultyValueText").objectReferenceValue = r.difficultyValueText;
            so.FindProperty("difficultyLabelText").objectReferenceValue = r.difficultyLabelText;
            so.FindProperty("blurbText").objectReferenceValue = r.blurbText;
            so.FindProperty("prosText").objectReferenceValue = r.prosText;
            so.FindProperty("consText").objectReferenceValue = r.consText;
            so.FindProperty("buildChipsContent").objectReferenceValue = r.buildChipsContent;
            so.FindProperty("buildChipTemplate").objectReferenceValue = r.buildChipTemplate;
            so.FindProperty("startButton").objectReferenceValue = r.startButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // RunScreen — Play 씬 단독 화면. HUD/릴/노트/조작부는 화면 자신의 자식, 페이즈 패널/팝업
        // (RunOverlayResult)은 전역 OverlayLayer 산하(런타임에 RunView.OnDisable이 명시적으로 닫는다).
        // ══════════════════════════════════════════════════════════════════════════════
        private static RunBuildResult BuildRunScreen(Transform canvasRoot)
        {
            var result = new RunBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

            var root = UiKit.Panel(canvasRoot, "RunScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();

            var col = UiKit.VGroup(root, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);
            // 행 높이 계약(S16 육안 검수 갱신): Hud 210 · 릴 위 여백 60(고정) · ReelSection(preferred
            // 260 · flex 1) · GainPanel(preferred 300 · flex 0 — 대문짝+칩+기여 내역) ·
            // NotesFeed(preferred 120 · flex 0 — 최근 3줄만) · 바닥 스페이서(flex 1) · Controls 300.
            // 잔여 공간(기기에 따라 600px 이상)은 릴 위아래 여백과 조작부 위 여백 두 곳으로 반씩
            // 흘려보낸다 — GainPanel에 flex를 주면 세트 설명 박스와 로그 사이에 그 잔여가 통째로
            // 몰려 화면 중앙에 검은 구멍이 생겼다(첫 구현의 실제 증상).
            // S9 교훈(릴 위 flex 스페이서 금지 — 화면 절반이 비어 보였다)은 고정 60px로 유지한다.

            BuildRunHud(col, result);
            AddFixedSpacer(col, 60f);
            BuildRunReel(col, result);
            BuildRunGainPanel(col, result);
            BuildRunNotesFeed(col, result);
            AddFlexSpacer(col);
            BuildRunControls(col, result, panelSprite);

            // 화면 플래시/배너는 RunScreen 자신의 "root" 직계 자식(HudView/ReelView가 소유) — root는
            // LayoutGroup이 없어(레이아웃 제어를 받는 것은 그 자식 "col"뿐) 커스텀 앵커 오버레이를
            // 자유롭게 얹을 수 있다. 전역 오버레이가 아니라 화면이 꺼지면 같이 사라져야 자연스러운
            // 순수 시각 연출이라 로컬로 둔다.
            result.flashOverlay = BuildScreenFlash(root);
            // S8 항목⑤: 🎰(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            (result.jackpotBannerGroup, result.jackpotBannerRect) = BuildDropBanner(root, "JackpotBanner", "JACKPOT!", UiKit.Accent);
            (result.bossBannerGroup, result.bossBannerRect) = BuildDropBanner(root, "BossBanner", "", UiKit.Bad);
            result.bossBannerText = result.bossBannerRect.GetComponentInChildren<Text>();
            // S14 §D — 보스 진입 적색 비네트 펄스(w_vignette 재사용, 붉게 틴트). 평소엔 완전 투명.
            result.bossVignetteGroup = BuildColorVignette(root, UiKit.Bad);

            result.view = root.gameObject.AddComponent<UI2.RunView>();
            return result;
        }

        // S14 §D — w_vignette(비네트 형태 알파 텍스처)를 임의 색으로 틴트해 화면 가장자리가 그 색으로
        // 은은하게 물드는 펄스 오버레이를 만든다(보스 진입 등). BuildScreenFlash(단색 전체화면)와
        // 달리 가장자리만 강조되는 모양이 필요할 때 쓴다.
        private static CanvasGroup BuildColorVignette(Transform parent, Color color)
        {
            var panel = UiKit.Panel(parent, "BossVignette", color, UiSpriteGen.Load("w_vignette"));
            panel.GetComponent<Image>().type = Image.Type.Simple;
            UiKit.Fill(panel);
            panel.GetComponent<Image>().raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        // HUD와 릴 사이의 고정 여백(S9) — flex 스페이서는 잔여 공간을 다 먹어 화면이 비어 보였다.
        private static void AddFixedSpacer(RectTransform col, float height)
        {
            var spacer = UiKit.Panel(col, "Spacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(spacer, preferredHeight: height, flexibleHeight: 0);
        }

        // 잔여 공간 흡수용 스페이서 — 콘텐츠 행이 전부 flex 0일 때 남는 높이를 "여기로 몰기" 위한 것.
        private static void AddFlexSpacer(RectTransform col)
        {
            var spacer = UiKit.Panel(col, "FlexSpacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(spacer, preferredHeight: 0, flexibleHeight: 1);
        }

        private static void BuildRunHud(RectTransform col, RunBuildResult result)
        {
            var hud = UiKit.Panel(col, "Hud", UiKit.PanelBg);
            UiKit.SizeHint(hud, preferredHeight: 210, flexibleHeight: 0);
            result.hudRoot = hud;
            result.hudOutline = UiKit.AddGlowOutline(hud.gameObject, UiKit.Bad, 4f);

            var hudCol = UiKit.VGroup(hud, 8, new RectOffset(24, 24, 16, 12), true, true);
            UiKit.Fill(hudCol);

            var topRow = UiKit.HGroup(hudCol, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(topRow, preferredHeight: 44, flexibleHeight: 0);
            result.stageText = UiKit.Text(topRow, "", 26, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(result.stageText, flexibleWidth: 1, flexibleHeight: 0);
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 ui.js:704 asc-hud) — 승천 런에서만 텍스트가
            // 채워진다(HudView.RefreshStageCurses, 일반 런은 빈 문자열이라 자리만 차지하지 않게 보임).
            result.ascBadgeText = UiKit.Text(topRow, "", 18, UiKit.Accent, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(result.ascBadgeText, preferredWidth: 130, flexibleHeight: 0);
            result.cursesText = UiKit.Text(topRow, "", 20, UiKit.Bad, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(result.cursesText, preferredWidth: 140, flexibleHeight: 0);

            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 ❓ 튜토리얼 재시작 + gearbtn 설정) — HUD
            // 우측 소형 아이콘 2개. "?"는 ASCII, "⚙"(U+2699)는 BMP라 둘 다 레거시 Text에서 안전(S8 항목⑤).
            result.tutorialButton = UiKit.Button(topRow, "?", new Vector2(40f, 40f), UiKit.Panel2, UiKit.TextPrimary, null, UiKit.PillSprite(40f));
            UiKit.SizeHint(result.tutorialButton, preferredWidth: 40, preferredHeight: 40, flexibleWidth: 0, flexibleHeight: 0);
            result.settingsButton = UiKit.Button(topRow, "⚙", new Vector2(40f, 40f), UiKit.Panel2, UiKit.TextPrimary, null, UiKit.PillSprite(40f));
            UiKit.SizeHint(result.settingsButton, preferredWidth: 40, preferredHeight: 40, flexibleWidth: 0, flexibleHeight: 0);

            var barBg = UiKit.Panel(hudCol, "ExpBarBg", UiKit.Hex("#2A3048"), UiSpriteGen.Load("bar_bg_r12"));
            UiKit.SizeHint(barBg, preferredHeight: 36, flexibleHeight: 0);
            result.expBarFill = UiKit.Panel(barBg, "ExpBarFill", UiKit.Good, UiSpriteGen.Load("bar_fill_r12"));
            UiKit.SetAnchors(result.expBarFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            result.expBarFillImage = result.expBarFill.GetComponent<Image>();

            // S14 §D — EXP 바 "선두에 흐르는 광점". expBarFill의 우측 끝(anchorMin/Max x=1)에 자식으로
            // 붙여서 expBarFill.anchorMax.x가 바뀔 때마다(SetExpBarImmediate) 채움 끝점을 자동으로
            // 따라간다(별도 위치 갱신 코드 불필요 — HudView는 알파 펄스만 담당).
            var leadDotGo = new GameObject("ExpLeadDot", typeof(RectTransform), typeof(Image));
            result.expLeadDot = (RectTransform)leadDotGo.transform;
            result.expLeadDot.SetParent(result.expBarFill, false);
            result.expLeadDot.anchorMin = new Vector2(1f, 0.5f);
            result.expLeadDot.anchorMax = new Vector2(1f, 0.5f);
            result.expLeadDot.pivot = new Vector2(0.5f, 0.5f);
            result.expLeadDot.sizeDelta = new Vector2(14f, 14f);
            var leadDotImg = leadDotGo.GetComponent<Image>();
            leadDotImg.sprite = UiKit.PillSprite(14f);
            leadDotImg.type = Image.Type.Sliced;
            leadDotImg.color = Color.white;
            leadDotImg.raycastTarget = false;

            result.expBarText = UiKit.Text(barBg, "", 19, UiKit.Bg, TextAnchor.MiddleCenter, true);
            UiKit.Fill(result.expBarText.rectTransform);

            var statsRow = UiKit.HGroup(hudCol, 16, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(statsRow, preferredHeight: 40, flexibleHeight: 0);
            result.spinsText = UiKit.Text(statsRow, "", 20, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(result.spinsText, flexibleWidth: 1, flexibleHeight: 0);
            result.coinsText = UiKit.Text(statsRow, "", 20, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.coinsText, flexibleWidth: 1, flexibleHeight: 0);
            result.scoreText = UiKit.Text(statsRow, "", 20, UiKit.Blue, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(result.scoreText, flexibleWidth: 1, flexibleHeight: 0);

            // 불운 게이지 5칸(UNLUCKY_MAX) — 고정 5개. S8 항목⑤: 🍀(astral)는 렌더링되지 않는다 —
            // 한글 라벨("행운")로 대체.
            var gaugeRow = UiKit.HGroup(hudCol, 8, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(gaugeRow, preferredHeight: 28, flexibleHeight: 0);
            UiKit.Text(gaugeRow, "행운", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            result.unluckyPips = new Image[5];
            // S13 §A 실측 위반: Pip_0~4(24×24)에 chip_r999(border 128)를 쓰면 늘어난다 — PillSprite(24)로 교체.
            var pipSprite = UiKit.PillSprite(24f);
            for (int i = 0; i < 5; i++)
            {
                var pip = UiKit.Panel(gaugeRow, "Pip_" + i, UiKit.Card, pipSprite);
                UiKit.SizeHint(pip, preferredWidth: 24, preferredHeight: 24, flexibleWidth: 0, flexibleHeight: 0);
                result.unluckyPips[i] = pip.GetComponent<Image>();
            }
            var gaugeSpacer = UiKit.Panel(gaugeRow, "Spacer", new Color(0, 0, 0, 0));
            UiKit.SizeHint(gaugeSpacer, flexibleWidth: 1, flexibleHeight: 0);
        }

        // 상단 드롭형 배너(보스 진입/잭팟 등 공용) — 부모의 맨 위에 겹쳐 놓이는 오버레이라 부모가
        // VerticalLayoutGroup이어도 레이아웃에 관여하지 않도록 앵커를 top-stretch로 고정한다.
        private static (CanvasGroup group, RectTransform rect) BuildDropBanner(Transform parent, string name, string text, Color textColor)
        {
            var panel = UiKit.Panel(parent, name, UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(760f, 96f);
            panel.anchoredPosition = new Vector2(0f, 0f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; // 빌드 시점에 명시적으로 0 — BuildToast와 동일 규칙(Awake 재설정에만 기대지 않는다).
            group.blocksRaycasts = false;
            group.interactable = false;

            var label = UiKit.Text(panel, text, 30, textColor, TextAnchor.MiddleCenter, true);
            UiKit.Fill(label.rectTransform);
            return (group, panel);
        }

        // 화면 전체를 덮는 흰색 플래시(set4/잭팟 공용) — 라이캐스트를 막지 않도록 Image.raycastTarget=false.
        private static CanvasGroup BuildScreenFlash(Transform parent)
        {
            var panel = UiKit.Panel(parent, "ScreenFlash", Color.white);
            UiKit.Fill(panel);
            panel.GetComponent<Image>().raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        // S8 항목⑥: 셀을 정사각으로 — 폭 = (1080 - 패딩48 - 스페이싱48)/5 ≈ 196.8, ReelSection을
        // 셀 높이(196) + 상하 여백(32×2)에 맞춰 260으로 축소.
        private static void BuildRunReel(RectTransform col, RunBuildResult result)
        {
            var section = UiKit.Panel(col, "ReelSection", new Color(0, 0, 0, 0));
            // flex 1 — 잔여 공간의 절반을 여기로 받아 릴이 위아래 여백에 감싸인 채 세로 중앙에 놓인다
            // (셀 크기는 reelRow의 childForceExpandHeight=false 덕에 늘어나지 않는다).
            UiKit.SizeHint(section, preferredHeight: 260, flexibleHeight: 1);
            result.reelSectionRoot = section;
            result.reelRow = UiKit.HGroup(section, 12, new RectOffset(24, 24, 32, 32), true, true);
            // childControlHeight=true(생성 인자)만으로는 childForceExpandHeight도 true가 되어 셀이
            // 부모 높이 전체로 늘어나 버린다(HGroup 헬퍼는 controlChildH와 forceExpandHeight를 같은
            // 값으로 묶는다) — 정사각 셀을 위해 이 한 줄로 forceExpand만 끄고 controlHeight(=
            // preferredHeight 반영)는 유지한다. BuildPickScreen의 header/recoRow/sortRow와 동일한
            // 이미 검증된 패턴.
            result.reelRow.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            UiKit.Fill(result.reelRow);

            result.cellTemplate = BuildReelCellTemplate(section);

            // 심볼 스프라이트 14종을 빌드 시점에 구워 ReelView.symbolSprites로 넘긴다(런타임은 Editor 전용
            // UiSpriteGen을 참조할 수 없다 — ReelView.cs 헤더 주석 "빌더가 와이어링" 참조).
            var syms = JackpotRun.Engine.Symbols.All;
            var sprites = new (string id, Sprite sprite)[syms.Length];
            for (int i = 0; i < syms.Length; i++)
                sprites[i] = (syms[i].id, UiSpriteGen.Load("sym_" + syms[i].id));
            result.symbolSprites = sprites;
        }

        // 릴 셀 템플릿 — S13 §D 재설계: 세로 스트립(위2/중앙/아래2 5칸)이 RectMask2D 안에서 무한
        // 스크롤하며 정지하는 구조로 교체(과거 "제자리에서 무작위 교체 후 툭 바뀜" 연출 폐기).
        // 구조 그대로: Reel_i(셀, 정사각, RectMask2D + 배경 w_reel + 테두리) → Strip(세로 5칸,
        // 각 칸 높이 = 셀 높이 UiKit.ReelCellSize) → Slot_k: Icon(Image, preserveAspect)+Tag(Text).
        // 자식 경로 계약(ReelView.cs): "Strip"(RectTransform) → "Slot0".."Slot4"(각 "Icon"/"Tag") +
        // Outline 2개(첫 번째=상시 2px 테두리, 두 번째=매치 글로우 — ReelView가 GetComponents<Outline>()[1]로
        // 글로우만 골라 쓴다. AddGlowOutline 헬퍼는 GetComponent<Outline>() 재사용이라 2개를 만들 수
        // 없어 여기서는 직접 AddComponent<Outline>()을 두 번 호출한다).
        private static RectTransform BuildReelCellTemplate(Transform parent)
        {
            float cellSize = UiKit.ReelCellSize; // 뷰포트(셀 프레임) — S14 §A "196 유지"
            float slotSize = UiKit.ReelSlotSize; // 스트립 개별 슬롯 — S14 §A "130으로 축소"
            var cell = UiKit.Panel(parent, "CellTemplate", Color.white, UiSpriteGen.Load("w_reel"));
            UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: cellSize, flexibleHeight: 0);
            cell.gameObject.AddComponent<RectMask2D>(); // Strip 무한 스크롤 클리핑(설계 구조 필수 요소)

            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 셀 정보 탭(openCellSheet 대응). 기존 Image
            // (배경)를 targetGraphic으로 삼는 Button만 추가한다 — 색 트랜지션은 릴 연출과 충돌하므로 끈다
            // (ReelView.EnsureCellCount가 인덱스별로 onClick을 다시 건다).
            var cellButton = cell.gameObject.AddComponent<Button>();
            cellButton.transition = Selectable.Transition.None;

            var border = cell.gameObject.AddComponent<Outline>(); // 상시 2px 테두리(설계 "테두리")
            border.effectColor = UiKit.Bd2;
            border.effectDistance = new Vector2(2f, -2f);
            border.enabled = true;

            var glow = cell.gameObject.AddComponent<Outline>(); // 매치 글로우 — ReelView가 enabled 토글(기존 연출 유지)
            glow.effectColor = UiKit.Accent;
            glow.effectDistance = new Vector2(3f, -3f);
            glow.enabled = false;

            var strip = new GameObject("Strip", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(cell, false);
            strip.anchorMin = new Vector2(0f, 0.5f);
            strip.anchorMax = new Vector2(1f, 0.5f);
            strip.pivot = new Vector2(0.5f, 0.5f);
            strip.sizeDelta = new Vector2(0f, slotSize * 5f); // 위2/중앙/아래2
            strip.anchoredPosition = Vector2.zero;

            for (int k = 0; k < 5; k++)
            {
                var slot = new GameObject("Slot" + k, typeof(RectTransform)).GetComponent<RectTransform>();
                slot.SetParent(strip, false);
                slot.anchorMin = new Vector2(0f, 0.5f);
                slot.anchorMax = new Vector2(1f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(0f, slotSize); // S14 §A "각 칸 높이=슬롯 높이"(뷰포트보다 작다 → 이웃 노출)
                slot.anchoredPosition = new Vector2(0f, (2 - k) * slotSize); // k=0(맨위,+2칸)..k=4(맨아래,-2칸)

                // S14 §A — 심볼은 슬롯을 꽉 채우지 않고 고정 크기(UiKit.ReelSymbolSize=95)로 중앙에
                // 앉는다(preserveAspect=true, UiKit.Image 공통 수정은 유지) — 이웃 슬롯 스케일(0.88)이
                // 이 고정 크기에 곱해져 "중앙 95px·이웃 ~84px"가 된다.
                var icon = UiKit.Image(slot, null, Color.white);
                icon.name = "Icon";
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(UiKit.ReelSymbolSize, UiKit.ReelSymbolSize);
                icon.rectTransform.anchoredPosition = Vector2.zero;

                var tag = UiKit.Text(slot, "", 16, UiKit.Accent, TextAnchor.UpperRight, true);
                tag.name = "Tag";
                UiKit.SetAnchors(tag.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -28), new Vector2(-6, -4));
            }

            // S14 §B — 최고속 모션 스트릭 오버레이(w_streak, 평소 알파 0 — ReelView.ApplyMaxSpeedStyle이
            // 알파만 토글). Strip 위, 페이드 마스크 아래에 셀 전체를 덮는다.
            var streak = UiKit.Panel(cell, "Streak", new Color(1f, 1f, 1f, 0f), UiSpriteGen.Load("w_streak"));
            UiKit.Fill(streak);
            var streakImg = streak.GetComponent<Image>();
            streakImg.type = Image.Type.Simple;
            streakImg.raycastTarget = false;

            // S14 §A — 상/하단 28px 페이드 마스크(w_reel_fade, 자식 경로 계약: ReelView는 이름으로
            // 찾지 않는다 — 순수 장식이라 런타임 참조 불필요).
            AddReelFadeMask(cell, top: true);
            AddReelFadeMask(cell, top: false);

            cell.gameObject.SetActive(false);
            return cell;
        }

        // S14 §A — 릴 셀 상/하단 페이드 오버레이 1장. w_reel_fade는 "위쪽(텍스처 y=size-1)이 불투명,
        // 아래쪽이 투명"으로 구워져 있어(UiSpriteGen.CreateVerticalFade) top=true는 그대로, top=false는
        // 세로로 뒤집어(localScale.y=-1) 붙여 같은 스프라이트를 재사용한다.
        private static void AddReelFadeMask(Transform cell, bool top)
        {
            var go = new GameObject(top ? "FadeTop" : "FadeBottom", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cell, false);
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, UiKit.ReelFadeHeight);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = new Vector3(1f, top ? 1f : -1f, 1f);

            var img = go.GetComponent<Image>();
            img.sprite = UiSpriteGen.Load("w_reel_fade");
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
        }

        // S16 — 구 StageInfo(한 줄 요약)를 스핀 결과 패널(GainPanel.cs)로 교체. preferred 300 ·
        // flexible 1(내역 줄이 많아지면 조금 더 자란다 — 최대치는 GainPanel의 6줄 캡이 사실상 제한).
        // 자식 경로 계약은 GainPanel.cs 헤더 주석 그대로("bigNumberText" 등).
        private static void BuildRunGainPanel(RectTransform col, RunBuildResult result)
        {
            var panel = UiKit.Panel(col, "GainPanel", new Color(0f, 0f, 0f, 0f));
            // flex 0 — 잔여 공간은 릴 섹션과 바닥 스페이서가 나눠 갖는다(BuildRunScreen 행 높이 계약).
            UiKit.SizeHint(panel, preferredHeight: 300, flexibleHeight: 0);
            result.gainPanelRoot = panel;

            var innerCol = UiKit.VGroup(panel, 6, new RectOffset(24, 24, 8, 8), true, true);
            UiKit.Fill(innerCol);
            innerCol.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

            // 획득 대문짝 — "+{N} EXP" 46pt w900(레거시 Text는 Bold로 근사) 골드.
            result.gainBigText = UiKit.Text(innerCol, "", 46, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.gainBigText, preferredHeight: 64, flexibleHeight: 0);

            // 칩 2개 — 점수(blue) · 코인(gold). BuildAutoPill(자기 텍스트 길이만큼 스스로 폭을 정하는
            // 필 — S13 §A 9-slice 관례)을 그대로 재사용, 색은 런타임(GainPanel.SetChip)이 매 스핀 입힌다.
            var chipsRow = UiKit.HGroup(innerCol, 12, new RectOffset(0, 0, 0, 0), false, true);
            chipsRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.SizeHint(chipsRow, preferredHeight: 36, flexibleHeight: 0);
            var scoreChip = BuildAutoPill(chipsRow, "ScoreChip", UiKit.PillSprite(36f), 18, new RectOffset(18, 18, 7, 7), true);
            result.gainScoreChipRoot = scoreChip.root; result.gainScoreChipBg = scoreChip.bg; result.gainScoreChipLabel = scoreChip.label;
            var coinChip = BuildAutoPill(chipsRow, "CoinChip", UiKit.PillSprite(36f), 18, new RectOffset(18, 18, 7, 7), true);
            result.gainCoinChipRoot = coinChip.root; result.gainCoinChipBg = coinChip.bg; result.gainCoinChipLabel = coinChip.label;

            // 기여 내역 리스트 — 최대 6줄, 행 템플릿+비활성 원본 패턴(BuildNotesRowTemplate과 동일 기법).
            // rowsContent 자신도 VerticalLayoutGroup이라 ILayoutElement로서 "실제 활성 자식(줄) 기준
            // preferredHeight"를 innerCol에 그대로 보고한다 — 여기에 flexibleHeight를 주면(이전 실수,
            // 육안 검수로 발견) 줄이 몇 개 없을 때도 잔여 공간을 통째로 떠먹어 내용과 세트 설명 박스
            // 사이에 큰 빈 공간이 생긴다. ContentSizeFitter는 붙이지 않는다 — innerCol이 이미
            // childControlHeight=true라 부모가 매기는 값과 이중으로 다투게 된다.
            var rowsContent = UiKit.VGroup(innerCol, 2, new RectOffset(0, 0, 4, 4), true, true);
            result.gainRowsContent = rowsContent;
            result.gainRowTemplate = BuildGainRowTemplate(rowsContent);

            // 세트 설명 박스 — bestSetCount>=2일 때만 GainPanel이 활성화한다.
            result.gainSetExplainRoot = BuildSetExplainBox(innerCol, out result.gainSetExplainText);

            // 트레일 스페이서 — preferred(300)와 실제 콘텐츠 높이의 차이를 아래쪽에서 흡수해 대문짝이
            // 항상 릴 바로 아래에 붙게 한다(줄 수가 스핀마다 달라져도 위치가 흔들리지 않는다).
            var trailSpacer = UiKit.Panel(innerCol, "TrailSpacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(trailSpacer, preferredHeight: 0, flexibleHeight: 1);
        }

        // 자식 경로 계약(GainPanel.cs): 루트에 CanvasGroup(스태거 페이드) + "Inner"(RectTransform,
        // 8px 상승 트윈 대상) 아래 "Label"(Text, 좌측 62%)·"Value"(Text, 우측 38%, 굵게 우측정렬).
        // "Inner"를 한 겹 더 두는 이유: rowsContent의 VerticalLayoutGroup은 직접 자식(행 루트)의
        // anchoredPosition만 재계산한다 — 위치 트윈 대상을 손자뻘(Inner)로 두면 스태거 도중 다음 행이
        // Instantiate되어 레이아웃이 다시 계산돼도 진행 중이던 상승 애니메이션이 덮어써지지 않는다.
        private static RectTransform BuildGainRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "GainRowTemplate", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(row, preferredHeight: 34, flexibleHeight: 0);
            row.gameObject.AddComponent<CanvasGroup>();

            var innerGo = new GameObject("Inner", typeof(RectTransform));
            var inner = (RectTransform)innerGo.transform;
            inner.SetParent(row, false);
            UiKit.Fill(inner);

            var label = UiKit.Text(inner, "", 20, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            label.name = "Label";
            UiKit.SetAnchors(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(8f, 0f), new Vector2(-6f, 0f));

            var value = UiKit.Text(inner, "", 20, UiKit.TextSecondary, TextAnchor.MiddleRight, true);
            value.name = "Value";
            UiKit.SetAnchors(value.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-8f, 0f));

            row.gameObject.SetActive(false);
            return row;
        }

        // S16 — "세트 설명 박스"(웹 .set-explain 재해석): 그라데이션 배경 대신 UiKit 팔레트의 보라
        // (Purple)를 낮은 알파로 깔고 상시 Outline으로 테두리를 흉내낸다(AddGlowOutline은 다른 곳에선
        // 토글용으로 기본 꺼두지만, 여기는 그 자체가 상시 스타일이라 즉시 enabled=true).
        private static RectTransform BuildSetExplainBox(Transform parent, out Text text)
        {
            var bg = UiKit.Purple; bg.a = 0.14f;
            var box = UiKit.Panel(parent, "SetExplainBox", bg, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(box, preferredHeight: 44, flexibleHeight: 0);
            UiKit.AddGlowOutline(box.gameObject, UiKit.Purple, 2f).enabled = true;

            text = UiKit.Text(box, "", 22, UiKit.Purple, TextAnchor.MiddleCenter, true);
            UiKit.Fill(text.rectTransform);
            box.gameObject.SetActive(false);
            return box;
        }

        // S16 — 로그는 축소: 최근 3줄만(NotesFeed.Cap=3), 배경 없이(투명), 18pt dim, 행 높이 34.
        // 이전엔 여기가 "로그 나열" 인상을 주는 주역이었지만 이제 GainPanel이 그 역할을 넘겨받았다.
        private static void BuildRunNotesFeed(RectTransform col, RunBuildResult result)
        {
            var panel = UiKit.Panel(col, "NotesFeed", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(panel, preferredHeight: 120, flexibleHeight: 0);
            result.notesRoot = panel;
            var inner = UiKit.VGroup(panel, 0, new RectOffset(0, 0, 6, 6), true, true);
            UiKit.Fill(inner);

            var scroll = UiKit.Scroll(inner, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 24, 0, 4);

            result.notesRowsContent = rowsContent;
            result.notesRowTemplate = BuildNotesRowTemplate(rowsContent);
        }

        // 자식 경로 계약(NotesFeed.cs): "Label"(Text). S16 — 배경 제거(투명), 18pt dim, 행 높이 34
        // (기존 44·20pt·카드색 배경 40%에서 축소 — "로그 나열" 인상을 없앤다는 설계 지시 그대로).
        private static RectTransform BuildNotesRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "NoteRowTemplate", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(row, preferredHeight: 34, flexibleHeight: 0);
            var label = UiKit.Text(row, "", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            label.name = "Label";
            UiKit.SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            row.gameObject.SetActive(false);
            return row;
        }

        private static void BuildRunControls(RectTransform col, RunBuildResult result, Sprite panelSprite)
        {
            var controlsRoot = UiKit.Panel(col, "Controls", new Color(0, 0, 0, 0));
            UiKit.SizeHint(controlsRoot, preferredHeight: 300, flexibleHeight: 0);
            result.controlsGroup = controlsRoot.gameObject.AddComponent<CanvasGroup>();

            var controls = UiKit.VGroup(controlsRoot, 10, new RectOffset(20, 20, 10, 20), true, true);
            UiKit.Fill(controls);

            // 특수모드 4버튼(순서: 집중/올인/기도/막판 — RunView.ModeOrder와 일치해야 함). 여기(빌드
            // 시점)에 굽는 라벨은 정적 기본값일 뿐이다 — WEB_PARITY P1 ①(2026-08-07) 이후
            // RunView.RefreshModeButtons()가 매 액션 배치 처리 후 실제 라벨("무료"/정가)과
            // interactable(무료가 아니고 코인 부족·이번 스테이지 이미 사용이면 비활성)을 런타임에
            // 덮어쓴다 — "사전 비활성화 안 함"은 더 이상 사실이 아니다(빌드 시점 한정 이야기).
            // S8 항목⑤: astral 이모지(🎯🎲🙏⏰)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var modeRow = UiKit.HGroup(controls, 8, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(modeRow, preferredHeight: 68, flexibleHeight: 0);
            string[] modeLabels =
            {
                $"집중({Formulas.CMD_COST_FOCUS})", $"올인({Formulas.CMD_COST_ALLIN})",
                $"기도({Formulas.CMD_COST_PRAY})", $"막판({Formulas.CMD_COST_LAST})",
            };
            result.modeButtons = new Button[modeLabels.Length];
            for (int i = 0; i < modeLabels.Length; i++)
            {
                var btn = UiKit.Button(modeRow, modeLabels[i], new Vector2(0, 68), UiKit.Card, UiKit.TextPrimary, null, panelSprite);
                UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 68, flexibleHeight: 0);
                result.modeButtons[i] = btn;
            }

            // S8 항목⑤: 🎰(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            result.spinButton = UiKit.Button(controls, "스핀", new Vector2(0, 108), UiKit.Accent, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(result.spinButton, preferredHeight: 108, flexibleHeight: 0);

            var toolRow = UiKit.HGroup(controls, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(toolRow, preferredHeight: 80, flexibleHeight: 0);

            // S8 항목⑤: 🎒(astral)는 렌더링되지 않는다 — 초기 라벨은 "가방"(런타임에 RunView가
            // "가방 N/M"으로 즉시 갱신한다).
            result.bagButton = UiKit.Button(toolRow, "가방", new Vector2(0, 80), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.bagButton, preferredWidth: 170, preferredHeight: 80, flexibleWidth: 0, flexibleHeight: 0);
            result.bagButtonLabel = result.bagButton.GetComponentInChildren<Text>();

            result.deviceRow = UiKit.HGroup(toolRow, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(result.deviceRow, flexibleWidth: 1, preferredHeight: 80, flexibleHeight: 0);
            result.deviceButtonTemplate = BuildLabeledButtonTemplate(result.deviceRow, panelSprite);

            // WEB_PARITY P1 ⑤: "게임 포기" — 웹 액션바 giveUpBtn()과 같은 취지로 작고 눈에 덜 띄는
            // 톤(Panel2/ghost) + 구석 배치(toolRow 맨 끝, 오작동 방지). 클릭 시 확인 시트를 거친다
            // (RunView.OnGiveUpClicked).
            result.giveUpButton = UiKit.Button(toolRow, "포기", new Vector2(0, 80), UiKit.Panel2, UiKit.TextSecondary, null, panelSprite);
            UiKit.SizeHint(result.giveUpButton, preferredWidth: 110, preferredHeight: 80, flexibleWidth: 0, flexibleHeight: 0);
        }

        // 라벨 1개짜리 버튼 템플릿(장치열/PostSpin 만회/ManipPick 칸선택 등 공용) — 자식 경로 계약: "Label"(Text).
        private static RectTransform BuildLabeledButtonTemplate(Transform parent, Sprite panelSprite)
        {
            var go = new GameObject("ButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = UiKit.Blue;
            if (panelSprite != null) { img.sprite = panelSprite; img.type = Image.Type.Sliced; }
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 80, flexibleHeight: 0);

            var label = UiKit.Text(rt, "", 19, UiKit.Bg, TextAnchor.MiddleCenter, true);
            label.name = "Label";
            UiKit.Fill(label.rectTransform);

            go.SetActive(false);
            return rt;
        }

        private static void WireRunView(RunBuildResult r, RunOverlayResult overlay)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("hudView").objectReferenceValue = WireHudView(r);
            so.FindProperty("reelView").objectReferenceValue = WireReelView(r);
            so.FindProperty("gainPanel").objectReferenceValue = WireGainPanel(r);
            so.FindProperty("notesFeed").objectReferenceValue = WireNotesFeed(r);
            so.FindProperty("controlsGroup").objectReferenceValue = r.controlsGroup;
            SetObjectArray(so, "modeButtons", r.modeButtons);
            so.FindProperty("spinButton").objectReferenceValue = r.spinButton;
            so.FindProperty("bagButton").objectReferenceValue = r.bagButton;
            so.FindProperty("bagButtonLabel").objectReferenceValue = r.bagButtonLabel;
            so.FindProperty("deviceRow").objectReferenceValue = r.deviceRow;
            so.FindProperty("deviceButtonTemplate").objectReferenceValue = r.deviceButtonTemplate;
            so.FindProperty("giveUpButton").objectReferenceValue = r.giveUpButton;
            so.FindProperty("nodePanel").objectReferenceValue = overlay.nodePanel;
            so.FindProperty("perkOfferPanel").objectReferenceValue = overlay.perkOfferPanel;
            so.FindProperty("shopPanel").objectReferenceValue = overlay.shopPanel;
            so.FindProperty("postSpinPanel").objectReferenceValue = overlay.postSpinPanel;
            so.FindProperty("gameOverPanel").objectReferenceValue = overlay.gameOverPanel;
            so.FindProperty("bagPopup").objectReferenceValue = overlay.bagPopup;
            so.FindProperty("manipPickPopup").objectReferenceValue = overlay.manipPickPopup;
            so.FindProperty("giveUpConfirmPopup").objectReferenceValue = overlay.giveUpConfirmPopup;
            so.FindProperty("deviceOfferPopup").objectReferenceValue = overlay.deviceOfferPopup;
            so.FindProperty("rewardDonePanel").objectReferenceValue = overlay.rewardDonePanel;
            so.FindProperty("cellInfoSheet").objectReferenceValue = overlay.cellInfoSheet;
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 튜토리얼 + 설정.
            so.FindProperty("tutorialOverlay").objectReferenceValue = overlay.tutorialOverlay;
            so.FindProperty("settingsSheet").objectReferenceValue = overlay.settingsSheet;
            so.FindProperty("tutorialButton").objectReferenceValue = r.tutorialButton;
            so.FindProperty("settingsButton").objectReferenceValue = r.settingsButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UI2.HudView WireHudView(RunBuildResult r)
        {
            var view = r.hudRoot.gameObject.AddComponent<UI2.HudView>();
            var so = new SerializedObject(view);
            so.FindProperty("stageText").objectReferenceValue = r.stageText;
            so.FindProperty("cursesText").objectReferenceValue = r.cursesText;
            so.FindProperty("ascBadgeText").objectReferenceValue = r.ascBadgeText;
            so.FindProperty("expBarFill").objectReferenceValue = r.expBarFill;
            so.FindProperty("expBarFillImage").objectReferenceValue = r.expBarFillImage;
            so.FindProperty("expBarText").objectReferenceValue = r.expBarText;
            so.FindProperty("expLeadDot").objectReferenceValue = r.expLeadDot;
            so.FindProperty("spinsText").objectReferenceValue = r.spinsText;
            so.FindProperty("coinsText").objectReferenceValue = r.coinsText;
            so.FindProperty("scoreText").objectReferenceValue = r.scoreText;
            so.FindProperty("hudOutline").objectReferenceValue = r.hudOutline;
            SetObjectArray(so, "unluckyPips", r.unluckyPips);
            so.FindProperty("bossBannerGroup").objectReferenceValue = r.bossBannerGroup;
            so.FindProperty("bossBannerRect").objectReferenceValue = r.bossBannerRect;
            so.FindProperty("bossBannerText").objectReferenceValue = r.bossBannerText;
            so.FindProperty("bossVignetteGroup").objectReferenceValue = r.bossVignetteGroup;
            so.FindProperty("runScreenRoot").objectReferenceValue = r.root;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static UI2.ReelView WireReelView(RunBuildResult r)
        {
            var view = r.reelSectionRoot.gameObject.AddComponent<UI2.ReelView>();
            var so = new SerializedObject(view);
            so.FindProperty("reelRow").objectReferenceValue = r.reelRow;
            so.FindProperty("cellTemplate").objectReferenceValue = r.cellTemplate;
            var symProp = so.FindProperty("symbolSprites");
            symProp.arraySize = r.symbolSprites.Length;
            for (int i = 0; i < r.symbolSprites.Length; i++)
            {
                var el = symProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = r.symbolSprites[i].id;
                el.FindPropertyRelative("sprite").objectReferenceValue = r.symbolSprites[i].sprite;
            }
            so.FindProperty("flashOverlay").objectReferenceValue = r.flashOverlay;
            so.FindProperty("jackpotBannerGroup").objectReferenceValue = r.jackpotBannerGroup;
            so.FindProperty("jackpotBannerRect").objectReferenceValue = r.jackpotBannerRect;
            so.FindProperty("runScreenRoot").objectReferenceValue = r.root;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static UI2.NotesFeed WireNotesFeed(RunBuildResult r)
        {
            var view = r.notesRoot.gameObject.AddComponent<UI2.NotesFeed>();
            var so = new SerializedObject(view);
            so.FindProperty("rowsContent").objectReferenceValue = r.notesRowsContent;
            so.FindProperty("rowTemplate").objectReferenceValue = r.notesRowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // S16 — GainPanel.cs 자식 경로 계약 그대로 배선.
        private static UI2.GainPanel WireGainPanel(RunBuildResult r)
        {
            var view = r.gainPanelRoot.gameObject.AddComponent<UI2.GainPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("bigNumberText").objectReferenceValue = r.gainBigText;
            so.FindProperty("scoreChipRoot").objectReferenceValue = r.gainScoreChipRoot;
            so.FindProperty("scoreChipBg").objectReferenceValue = r.gainScoreChipBg;
            so.FindProperty("scoreChipLabel").objectReferenceValue = r.gainScoreChipLabel;
            so.FindProperty("coinChipRoot").objectReferenceValue = r.gainCoinChipRoot;
            so.FindProperty("coinChipBg").objectReferenceValue = r.gainCoinChipBg;
            so.FindProperty("coinChipLabel").objectReferenceValue = r.gainCoinChipLabel;
            so.FindProperty("rowsContent").objectReferenceValue = r.gainRowsContent;
            so.FindProperty("rowTemplate").objectReferenceValue = r.gainRowTemplate;
            so.FindProperty("setExplainRoot").objectReferenceValue = r.gainSetExplainRoot;
            so.FindProperty("setExplainText").objectReferenceValue = r.gainSetExplainText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── 아이콘/이모지 폴백 공용 헬퍼 ─────────────────────────────────────────────────
        // 스프라이트가 없을 때 이모지로 폴백하는 카드/행이 여럿(PerkOfferPanel/ShopPanel/BagPopup/DexView)
        // 있어 "IconSlot"(래퍼) 하나만 부모 레이아웃 그룹의 셀로 참여시키고 그 안에 Icon/IconEmoji를
        // Fill로 완전히 겹친다 — 형제로 나란히 두면 부모가 HGroup/VGroup일 때 칸이 어긋난다(PickView의
        // "Body 직계 자식 + 수동 겹침" 방식은 세로 스택 1건에는 맞았지만 여러 모양의 가로 행에는
        // 매번 다른 오프셋 계산이 필요해 이 헬퍼로 일반화했다). 자식 경로 계약: "IconSlot/Icon"·
        // "IconSlot/IconEmoji".
        private static void BuildIconSlot(Transform parent, float size, int emojiSize)
        {
            var slot = UiKit.Panel(parent, "IconSlot", new Color(0, 0, 0, 0));
            UiKit.SizeHint(slot, preferredWidth: size, preferredHeight: size, flexibleWidth: 0, flexibleHeight: 0);

            var icon = UiKit.Image(slot, null, Color.white);
            icon.name = "Icon";
            UiKit.Fill(icon.rectTransform);

            var emoji = UiKit.Text(slot, "", emojiSize, Color.white, TextAnchor.MiddleCenter);
            emoji.name = "IconEmoji";
            UiKit.Fill(emoji.rectTransform);
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // Run 페이즈 패널/팝업 — ENGINE_PORT_DESIGN.md S7 Run/Panels/*.cs. 전부 전역 OverlayLayer 산하
        // (RunView.OnDisable이 명시적으로 닫는다 — RunView.cs 주석 참조). 시작 상태는 비활성.
        // ══════════════════════════════════════════════════════════════════════════════
        private static RunOverlayResult BuildRunOverlayPanels(Transform overlay)
        {
            return new RunOverlayResult
            {
                nodePanel = BuildNodePanel(overlay),
                perkOfferPanel = BuildPerkOfferPanel(overlay),
                shopPanel = BuildShopPanel(overlay),
                postSpinPanel = BuildPostSpinPanel(overlay),
                gameOverPanel = BuildGameOverPanel(overlay),
                bagPopup = BuildBagPopup(overlay),
                manipPickPopup = BuildManipPickPopup(overlay),
                // 포기 확인은 취소 가능(스크림 탭=계속 플레이). DEVICE 오퍼는 NodeSelect/PerkOffer와
                // 같은 "필수 결정" 모달이라 스크림 탭으로 빠져나갈 수 없다(NodePanel dismissOnScrimClick:
                // false와 동일 관례) — 실수로 닫아도 다음 갱신에서 다시 뜨긴 하지만, 애초에 닫히지
                // 않는 편이 UX상 명확하다.
                giveUpConfirmPopup = BuildConfirmSheetPopup(overlay, "GiveUpConfirmPopup", dismissOnScrimClick: true),
                deviceOfferPopup = BuildConfirmSheetPopup(overlay, "DeviceOfferPopup", dismissOnScrimClick: false),
                rewardDonePanel = BuildRewardDonePanel(overlay),
                cellInfoSheet = BuildCellInfoSheet(overlay),
                // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 튜토리얼 오버레이 + 설정 시트.
                tutorialOverlay = BuildTutorialOverlay(overlay),
                // Opus 2차검수 필수⑥ — 런 화면 설정 시트엔 데이터 초기화 행을 짓지 않는다(웹 설정
                // 시트에 없는 요소, 홈 전용 진입점만 유지).
                settingsSheet = BuildSettingsSheet(overlay, "RunSettingsSheet", includeReset: false),
            };
        }

        // S12c §6 — 시트 공용 골격("바텀시트" — Node/Perk/Shop/PostSpin/Bag/Manip 6개 패널이 전부 이
        // 헬퍼로 짓는다). scrim은 전체화면 투명(클릭 차단, Image 기본 raycastTarget=true) —
        // dismissOnScrimClick=true면 빈 Button을 추가해 각 패널의 Awake()가 Hide를 리스너로 붙인다
        // (기존 BagPopup/ManipPickPopup 관례 그대로, 리스너 연결은 런타임 쪽 책임 — 빌더는 컴포넌트만
        // 붙인다). card는 하단 고정 앵커(anchorMin/Max.y=0, pivot.y=0) — 런타임이 anchoredPosition을
        // (0,-card.rect.height)에서 (0,0)으로 슬라이드한다(오프스크린 높이는 card 자신의 rect에서
        // 읽으므로 이 함수가 굳이 반환할 필요 없음). w_sheet_top(상단만 r-2xl) + bd2 테두리 +
        // 그랩 핸들(.grab 재해석) 장식.
        private static SheetChrome BuildSheetChrome(Transform overlay, string name, float maxHeight,
            bool dismissOnScrimClick)
        {
            var scrim = UiKit.Panel(overlay, name, new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            if (dismissOnScrimClick)
            {
                var scrimBtn = scrim.gameObject.AddComponent<Button>();
                scrimBtn.transition = Selectable.Transition.None;
                // Opus 2차검수 항목5(2026-08-09) — 웹 `.sheet-bg`(data-act="closeSheet")도 전역
                // tap 위임을 그대로 타 딤 배경을 눌러 닫을 때 tap음이 난다. 이 Button은 UiKit.Button
                // 헬퍼를 거치지 않는 수동 생성이라 PressFx가 자동으로 안 붙는다 — 여기서 직접 추가
                // (골드 버튼이 아니므로 파티클/진동은 안 나고 tap 사운드만, PressFx 기본값 그대로).
                scrimBtn.gameObject.AddComponent<PressFx>();
            }

            var dimOverlay = UiKit.Panel(scrim, "DimOverlay", new Color(0f, 0f, 0f, 0.62f));
            UiKit.Fill(dimOverlay);
            dimOverlay.GetComponent<Image>().raycastTarget = false;
            var dimGroup = dimOverlay.gameObject.AddComponent<CanvasGroup>();
            dimGroup.blocksRaycasts = false;
            dimGroup.interactable = false;
            dimGroup.alpha = 0f;

            var card = UiKit.Panel(scrim, "Card", Color.white, UiSpriteGen.Load("w_sheet_top"));
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd2, 2f).enabled = true;
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(1f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.sizeDelta = new Vector2(0f, Mathf.Min(maxHeight, SheetMaxHeight));
            card.anchoredPosition = Vector2.zero;
            if (dismissOnScrimClick)
            {
                var cardBlocker = card.gameObject.AddComponent<Button>();
                cardBlocker.transition = Selectable.Transition.None;
            }

            AddGrabHandle(card);

            var cardCol = UiKit.VGroup(card, 0, new RectOffset(0, 0, 0, 0), true, true);
            cardCol.name = "Content";
            // 상단 34(그랩 핸들 10+8 아래로 여유 16) · 좌우 27(14px×1.9) · 하단 20(§6 padding 20px 그대로).
            UiKit.SetAnchors(cardCol, Vector2.zero, Vector2.one, new Vector2(27f, 20f), new Vector2(-27f, -34f));

            return new SheetChrome { scrim = scrim, dimGroup = dimGroup, card = card, cardCol = cardCol };
        }

        // .grab 재해석 — 시트 상단 중앙 40×8(스케일 20×1.9≈40, 4×1.9≈8) bd2색 pill 장식. 높이 8px는
        // 9-slice 후보 중 가장 작은 반경(w_r9=9)도 초과하는 "찌그러짐 구간"이라(§13 §A 경고 그대로 —
        // 대상 크기보다 border가 크면 늘어난다) 9-slice 없이 단색 사각(Image.Type.Simple)으로 굽는다 —
        // 8px 높이에서는 둥근 끝인지 각진 끝인지 시각적으로 거의 구분되지 않는다(재해석). 장식용이라
        // raycastTarget=false(§7 공통 규칙 "투명 컨테이너는 raycastTarget=false"와 동일 취지).
        private static void AddGrabHandle(RectTransform card)
        {
            var grab = UiKit.Panel(card, "Grab", UiKit.Bd2);
            grab.GetComponent<Image>().raycastTarget = false;
            grab.anchorMin = new Vector2(0.5f, 1f);
            grab.anchorMax = new Vector2(0.5f, 1f);
            grab.pivot = new Vector2(0.5f, 1f);
            grab.sizeDelta = new Vector2(40f, 8f);
            grab.anchoredPosition = new Vector2(0f, -10f);
        }

        // ── NodePanel ────────────────────────────────────────────────────────────────
        // S12c §6 — 카드 목록 부분을 BuildSheetChrome(바텀시트)으로 교체. Banner(클리어 등급 배너)는
        // 웹 원본 .sheet에 없는 Unity 전용 연출이라 그대로 유지하되(§7 재해석 대상 아님 — 임의 삭제
        // 금지), 배경만 토큰 스프라이트(w_panel_grad+bd2)로 갱신한다. Banner의 자체 CanvasGroup 페이드
        // /드롭 애니메이션은 NodePanel.cs가 그대로 담당(변경 없음) — chrome.dimGroup과는 별개.
        private static UI2.NodePanel BuildNodePanel(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "NodePanel", 1300f, dismissOnScrimClick: false);
            var scrim = chrome.scrim;

            var bannerPanel = UiKit.Panel(scrim, "Banner", Color.white, UiSpriteGen.Load("w_panel_grad"));
            UiKit.AddGlowOutline(bannerPanel.gameObject, UiKit.Bd2, 2f).enabled = true;
            bannerPanel.anchorMin = bannerPanel.anchorMax = new Vector2(0.5f, 1f);
            bannerPanel.pivot = new Vector2(0.5f, 1f);
            bannerPanel.sizeDelta = new Vector2(860f, 230f);
            bannerPanel.anchoredPosition = new Vector2(0f, -140f);
            bannerPanel.SetAsLastSibling(); // 시트(Card)보다 위 형제로 — 배너가 카드에 가리지 않게.
            var bannerGroup = bannerPanel.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;

            var bannerCol = UiKit.VGroup(bannerPanel, 4, new RectOffset(24, 24, 16, 16), true, true);
            UiKit.Fill(bannerCol);
            var gradeText = UiKit.Text(bannerCol, "", 32, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(gradeText, preferredHeight: 48, flexibleHeight: 0);
            var scoreText = UiKit.Text(bannerCol, "+0점", 28, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(scoreText, preferredHeight: 42, flexibleHeight: 0);
            var subText = UiKit.Text(bannerCol, "", 17, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(subText, flexibleHeight: 1);

            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 renderStageClear ui.js:1627-1671) — 카드
            // 스크롤 영역(뜬 배너와 달리 이미 스크롤 가능해 가변 높이를 안전하게 담는다) 맨 위에 삽입.
            // 뜬 배너(위 bannerPanel) 자체 구조/좌표/애니메이션은 건드리지 않는다.
            var (detailSection, expLabel, expFill, spinsLabel, spinsFill, cellTexts, gainText, notesText,
                totalText, toggleBtn, toggleLabel, detailRowsRoot, detailRowsContent, detailRowTemplate)
                = BuildNodeClearDetail(chrome.cardCol);

            var title = UiKit.Text(chrome.cardCol, "다음 노드를 선택하세요", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(title, preferredHeight: 56, flexibleHeight: 0);

            var scroll = UiKit.Scroll(chrome.cardCol, out var cardsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(cardsContent, 4, 12, 16);
            var cardTemplate = BuildNodeCardTemplate(cardsContent);
            detailSection.SetAsFirstSibling(); // 스크롤/타이틀보다 위(웹 순서: 클리어 상세 → 다음 노드)

            var view = scrim.gameObject.AddComponent<UI2.NodePanel>();
            var so = new SerializedObject(view);
            so.FindProperty("bannerGroup").objectReferenceValue = bannerGroup;
            so.FindProperty("bannerRect").objectReferenceValue = bannerPanel;
            so.FindProperty("bannerGradeText").objectReferenceValue = gradeText;
            so.FindProperty("bannerScoreText").objectReferenceValue = scoreText;
            so.FindProperty("bannerSubText").objectReferenceValue = subText;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("cardsContent").objectReferenceValue = cardsContent;
            so.FindProperty("cardTemplate").objectReferenceValue = cardTemplate;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("expDetailLabel").objectReferenceValue = expLabel;
            so.FindProperty("expDetailBarFill").objectReferenceValue = expFill;
            so.FindProperty("spinsDetailLabel").objectReferenceValue = spinsLabel;
            so.FindProperty("spinsDetailBarFill").objectReferenceValue = spinsFill;
            SetObjectArray(so, "lastCellTexts", cellTexts);
            so.FindProperty("lastGainText").objectReferenceValue = gainText;
            so.FindProperty("lastNotesText").objectReferenceValue = notesText;
            so.FindProperty("totalScoreText").objectReferenceValue = totalText;
            so.FindProperty("detailToggleButton").objectReferenceValue = toggleBtn;
            so.FindProperty("detailToggleLabel").objectReferenceValue = toggleLabel;
            so.FindProperty("detailRowsRoot").objectReferenceValue = detailRowsRoot;
            so.FindProperty("detailRowsContent").objectReferenceValue = detailRowsContent;
            so.FindProperty("detailRowTemplate").objectReferenceValue = detailRowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 클리어 상세 블록 — 2바(EXP%·사용 스핀) + 마지막 스핀 5칸/획득 내역 + 누적 총점 + "점수 상세"
        // 토글. VerticalLayoutGroup+ContentSizeFitter(autoSizeH) 조합으로 스크롤 콘텐츠 흐름에 자연스럽게
        // 얹힌다(GameOverPanel xpBlock과 동일 관례 — 고정 높이 없음).
        private static (RectTransform section, Text expLabel, RectTransform expFill, Text spinsLabel,
            RectTransform spinsFill, Text[] cellTexts, Text gainText, Text notesText, Text totalText,
            Button toggleBtn, Text toggleLabel, RectTransform detailRowsRoot, RectTransform detailRowsContent,
            RectTransform detailRowTemplate) BuildNodeClearDetail(RectTransform parent)
        {
            var section = UiKit.VGroup(parent, 10, new RectOffset(0, 0, 0, 12), true, true, autoSizeH: true);
            section.name = "ClearDetail";

            var (expLabel, expFill) = BuildMiniBarRow(section, "ExpBar");
            var (spinsLabel, spinsFill) = BuildMiniBarRow(section, "SpinsBar");

            var cellsRow = UiKit.HGroup(section, 6, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(cellsRow, preferredHeight: 54, flexibleHeight: 0);
            var cellTexts = new Text[Formulas.REEL];
            for (int i = 0; i < cellTexts.Length; i++)
            {
                var cell = UiKit.Panel(cellsRow, "Cell_" + i, UiKit.Hex("#2A3048"), UiSpriteGen.Load("w_r12"));
                UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: 54, flexibleHeight: 0);
                cellTexts[i] = UiKit.Text(cell, "", 13, UiKit.TextSecondary, TextAnchor.MiddleCenter, true);
                UiKit.Fill(cellTexts[i].rectTransform);
            }

            var gainText = UiKit.Text(section, "", 17, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(gainText, preferredHeight: 24, flexibleHeight: 0);
            var notesText = UiKit.Text(section, "", 15, UiKit.TextSecondary, TextAnchor.UpperCenter);
            UiKit.SizeHint(notesText, preferredHeight: 40, flexibleHeight: 0);

            var totalText = UiKit.Text(section, "", 19, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(totalText, preferredHeight: 28, flexibleHeight: 0);

            var toggleBtn = UiKit.Button(section, "▼ 점수 상세", new Vector2(0, 48), UiKit.Panel2, UiKit.TextSecondary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(toggleBtn, preferredHeight: 48, flexibleHeight: 0);
            var toggleLabel = toggleBtn.GetComponentInChildren<Text>();

            // DetailRows — 배경 패널 자신에 VerticalLayoutGroup+ContentSizeFitter를 직접 얹는다(xpBlock
            // 관례 그대로, GameOverPanel BuildGameOverPanel 참조) — 별도 Fill 자식 래퍼를 두면
            // ContentSizeFitter(세로 자동)와 Fill(부모 높이로 늘림)이 서로 충돌해 높이가 0으로 접힌다.
            var detailRowsRoot = UiKit.Panel(section, "DetailRows", new Color(0f, 0f, 0f, 0.18f), UiSpriteGen.Load("w_r16"));
            var detailRowsVlg = detailRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            detailRowsVlg.padding = new RectOffset(16, 16, 10, 10);
            detailRowsVlg.spacing = 2;
            detailRowsVlg.childControlWidth = true;
            detailRowsVlg.childControlHeight = true;
            detailRowsVlg.childForceExpandWidth = true;
            detailRowsVlg.childForceExpandHeight = false;
            UiKit.SizeHint(detailRowsRoot, preferredHeight: 0, flexibleHeight: 0, minHeight: 0);
            var detailRowsCsf = detailRowsRoot.gameObject.AddComponent<ContentSizeFitter>();
            detailRowsCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var detailRowTemplate = BuildRewardStatRowTemplate(detailRowsRoot);

            return (section, expLabel, expFill, spinsLabel, spinsFill, cellTexts, gainText, notesText,
                totalText, toggleBtn, toggleLabel, detailRowsRoot, detailRowsRoot, detailRowTemplate);
        }

        // 라벨(위) + 바(아래) 1행 — NodePanel의 EXP%/사용 스핀 2바 공용(BuildLevelCard의 barBg+Fill 관례).
        private static (Text label, RectTransform fill) BuildMiniBarRow(RectTransform parent, string name)
        {
            var col = UiKit.VGroup(parent, 4, new RectOffset(0, 0, 0, 0), true, true);
            col.name = name;
            UiKit.SizeHint(col, preferredHeight: 40, flexibleHeight: 0);
            var label = UiKit.Text(col, "", 15, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(label, preferredHeight: 20, flexibleHeight: 0);
            var barBg = UiKit.Panel(col, "Bar", UiKit.Hex("#2A3048"), UiSpriteGen.Load("bar_bg_r12"));
            UiKit.SizeHint(barBg, preferredHeight: 16, flexibleHeight: 0);
            var fill = UiKit.Panel(barBg, "Fill", UiKit.Accent, UiSpriteGen.Load("bar_fill_r12"));
            UiKit.SetAnchors(fill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            return (label, fill);
        }

        // ── TutorialOverlay ─────────────────────────────────────────────────────────
        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 TOUR/tutSpot ui.js:1741-1785). 1단(스포트라이트
        // 6스텝) + 2/3단 공용 배너(결과 해설·라이브 안내). RunView가 소유·구동(TutorialOverlay.cs 헤더 참조).
        private static UI2.TutorialOverlay BuildTutorialOverlay(Transform overlay)
        {
            var root = UiKit.Panel(overlay, "TutorialOverlay", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(root);
            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = false;

            // ── 1단: 스포트라이트 ──
            var spotRoot = UiKit.Panel(root, "SpotRoot", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(spotRoot);
            spotRoot.GetComponent<Image>().raycastTarget = false;

            var dimImg = UiKit.Panel(spotRoot, "Dim", new Color(0f, 0f, 0f, 0.62f));
            UiKit.Fill(dimImg);
            var spotDimGroup = dimImg.gameObject.AddComponent<CanvasGroup>();
            spotDimGroup.alpha = 1f;
            // block:!action 이 실제 클릭 통과를 결정 — Image.raycastTarget은 항상 켜 두고
            // CanvasGroup.blocksRaycasts를 스텝마다 토글한다(TutorialOverlay.RenderTourStep).

            // 대상 하이라이트 — 진짜 컷아웃(투명 구멍) 대신 골드 테두리 프레임으로 강조(§7 "코드생성
            // uGUI로 실현 가능한 방식" 재해석, 작업 지시가 명시적으로 허용한 대안).
            var highlightFrame = UiKit.Panel(spotRoot, "HighlightFrame", new Color(0f, 0f, 0f, 0f), UiSpriteGen.Load("w_r16"));
            highlightFrame.GetComponent<Image>().raycastTarget = false;
            highlightFrame.anchorMin = highlightFrame.anchorMax = new Vector2(0.5f, 0.5f);
            highlightFrame.pivot = new Vector2(0.5f, 0.5f);
            UiKit.AddGlowOutline(highlightFrame.gameObject, UiKit.Accent, 4f).enabled = true;

            // 툴팁 카드 — 화면 중앙 폭 고정, 세로 위치는 TutorialOverlay가 대상 위/아래로 옮긴다.
            var tooltip = UiKit.Panel(spotRoot, "Tooltip", Color.white, UiSpriteGen.Load("w_panel_grad"));
            UiKit.AddGlowOutline(tooltip.gameObject, UiKit.Bd2, 2f).enabled = true;
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            tooltip.pivot = new Vector2(0.5f, 0.5f);
            tooltip.sizeDelta = new Vector2(860f, 320f);
            // Opus 2차검수 필수③ — 배경 자신은 라캐스트를 받지 않게 한다. dim의 blocksRaycasts가
            // action 스텝에서 false가 되어 클릭을 실제 스핀버튼까지 통과시켜야 하는데, 배경 Image가
            // raycastTarget=true인 채로 남아 있으면(기하 계산이 어긋나 툴팁이 버튼과 겹치는 극단적
            // 경우) 그 배경이 클릭을 대신 삼켜버릴 수 있다. Skip/Next 버튼은 각자 자기 Image를
            // targetGraphic으로 쓰는 별도 Button이라 이 설정과 무관하게 계속 클릭된다.
            tooltip.GetComponent<Image>().raycastTarget = false;

            var tipCol = UiKit.VGroup(tooltip, 10, new RectOffset(26, 26, 20, 20), true, true);
            UiKit.Fill(tipCol);
            var stepRow = UiKit.HGroup(tipCol, 8, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(stepRow, preferredHeight: 26, flexibleHeight: 0);
            var stepLabelText = UiKit.Text(stepRow, "", 16, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(stepLabelText, flexibleWidth: 1, flexibleHeight: 0);
            var dotsRow = UiKit.HGroup(stepRow, 6, new RectOffset(0, 0, 0, 0), false, true);
            UiKit.SizeHint(dotsRow, preferredWidth: 160, flexibleWidth: 0, flexibleHeight: 0);
            var dotImages = new Image[UI2.TutorialOverlay.TourStepCount]; // TOUR 스텝 수와 반드시 일치
            var dotSprite = UiKit.PillSprite(14f);
            for (int i = 0; i < dotImages.Length; i++)
            {
                var dot = UiKit.Panel(dotsRow, "Dot_" + i, UiKit.Card, dotSprite);
                UiKit.SizeHint(dot, preferredWidth: 14, preferredHeight: 14, flexibleWidth: 0, flexibleHeight: 0);
                dotImages[i] = dot.GetComponent<Image>();
            }

            var tourTitleText = UiKit.Text(tipCol, "", 26, UiKit.Accent, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(tourTitleText, preferredHeight: 36, flexibleHeight: 0);
            var tourBodyText = UiKit.Text(tipCol, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(tourBodyText, flexibleHeight: 1);

            var tipBtnRow = UiKit.HGroup(tipCol, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(tipBtnRow, preferredHeight: 64, flexibleHeight: 0);
            var skipButton = UiKit.Button(tipBtnRow, "건너뛰기", new Vector2(0, 64), UiKit.Panel2, UiKit.TextSecondary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(skipButton, flexibleWidth: 1, preferredHeight: 64, flexibleHeight: 0);
            var nextButton = UiKit.Button(tipBtnRow, "다음 ▶", new Vector2(0, 64), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(nextButton, flexibleWidth: 1, preferredHeight: 64, flexibleHeight: 0);

            // ── 2/3단: 배너(결과 해설 · 라이브 안내 공용) ──
            var bannerRoot = UiKit.Panel(root, "BannerRoot", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(bannerRoot);
            bannerRoot.SetAsLastSibling();
            var bannerDim = UiKit.Panel(bannerRoot, "Dim", new Color(0f, 0f, 0f, 0.72f));
            UiKit.Fill(bannerDim);
            var bannerDimGroup = bannerDim.gameObject.AddComponent<CanvasGroup>();
            bannerDimGroup.alpha = 1f;

            var bannerCard = UiKit.Panel(bannerRoot, "Card", Color.white, UiSpriteGen.Load("w_panel_grad"));
            UiKit.AddGlowOutline(bannerCard.gameObject, UiKit.Bd2, 2f).enabled = true;
            bannerCard.anchorMin = bannerCard.anchorMax = new Vector2(0.5f, 0.5f);
            bannerCard.pivot = new Vector2(0.5f, 0.5f);
            bannerCard.sizeDelta = new Vector2(880f, 0f);
            var bannerVlg = bannerCard.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVlg.padding = new RectOffset(28, 28, 26, 26);
            bannerVlg.spacing = 20;
            bannerVlg.childControlWidth = true;
            bannerVlg.childControlHeight = true;
            bannerVlg.childForceExpandWidth = true;
            bannerVlg.childForceExpandHeight = false;
            var bannerCsf = bannerCard.gameObject.AddComponent<ContentSizeFitter>();
            bannerCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bannerBodyText = UiKit.Text(bannerCard, "", 21, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(bannerBodyText, preferredHeight: 160, flexibleHeight: 0);
            var bannerOkButton = UiKit.Button(bannerCard, "확인 ▶", new Vector2(0, 76), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(bannerOkButton, preferredHeight: 76, flexibleHeight: 0);

            var view = root.gameObject.AddComponent<UI2.TutorialOverlay>();
            var so = new SerializedObject(view);
            so.FindProperty("spotRoot").objectReferenceValue = spotRoot;
            so.FindProperty("spotDimGroup").objectReferenceValue = spotDimGroup;
            so.FindProperty("highlightFrame").objectReferenceValue = highlightFrame;
            so.FindProperty("tooltipRect").objectReferenceValue = tooltip;
            so.FindProperty("stepLabelText").objectReferenceValue = stepLabelText;
            SetObjectArray(so, "dotImages", dotImages);
            so.FindProperty("tourTitleText").objectReferenceValue = tourTitleText;
            so.FindProperty("tourBodyText").objectReferenceValue = tourBodyText;
            so.FindProperty("skipButton").objectReferenceValue = skipButton;
            so.FindProperty("nextButton").objectReferenceValue = nextButton;
            so.FindProperty("bannerRoot").objectReferenceValue = bannerRoot;
            so.FindProperty("bannerDimGroup").objectReferenceValue = bannerDimGroup;
            so.FindProperty("bannerBodyText").objectReferenceValue = bannerBodyText;
            so.FindProperty("bannerOkButton").objectReferenceValue = bannerOkButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(true); // 자식 spotRoot/bannerRoot 자체는 Awake에서 비활성화된다.
            spotRoot.gameObject.SetActive(false);
            bannerRoot.gameObject.SetActive(false);
            return view;
        }

        // 자식 경로 계약(NodePanel.cs): "Head"(Text)/"Body"(Text). S12c §2 — .pcard 톤(w_card_grad r16
        // + bd 1.5 + 상단 40% gloss). 카드 자체가 클릭 대상(Button+PressFx)이라 눌림 시 자동 scale .96.
        private static RectTransform BuildNodeCardTemplate(Transform parent)
        {
            var card = UiKit.Panel(parent, "NodeCardTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.SizeHint(card, preferredHeight: 200, flexibleHeight: 0);
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd, 1.5f).enabled = true;
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            card.gameObject.AddComponent<PressFx>();

            // "Content"(VGroup)은 Find 경로 계약의 일부다 — Head/Body가 card의 직계 자식이 아니라
            // Content의 자식이라 NodePanel.cs가 "Content/Head"·"Content/Body"로 찾는다(Transform.Find는
            // "/" 없이는 직계 자식만 검색 — 이름 계약 주의).
            var inner = UiKit.VGroup(card, 6, new RectOffset(22, 22, 16, 16), true, true);
            inner.name = "Content";
            UiKit.Fill(inner);
            var head = UiKit.Text(inner, "", 27, UiKit.Accent, TextAnchor.MiddleLeft, true);
            head.name = "Head";
            UiKit.SizeHint(head, preferredHeight: 40, flexibleHeight: 0);
            var body = UiKit.Text(inner, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            body.name = "Body";
            UiKit.SizeHint(body, flexibleHeight: 1);

            AddGloss(card, 80f); // .pcard::after height:40% of 200 — MenuView 관례처럼 콘텐츠 위 마지막 형제.

            card.gameObject.SetActive(false);
            return card;
        }

        // ── PerkOfferPanel ───────────────────────────────────────────────────────────
        // S15 §C 전면 재설계 — 하단 시트(BuildSheetChrome)를 버리고 로그라이크 표준 모달로 교체:
        // 전체화면 딤 0.72(baked, BuildSheetChrome의 0.62와 다른 값이라 헬퍼를 공유하지 않는다) +
        // 화면 중앙 고정 ModalRoot(헤더 + 카드 3장 가로열 + 하단 보조 행). 유물 노드도 같은 모달을
        // 재사용(PerkOfferPanel.Show가 헤더 문구만 분기) — 이 빌더 함수는 변경 없이 공용.
        private static UI2.PerkOfferPanel BuildPerkOfferPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "PerkOfferPanel", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);

            // 딤 0.72(설계 명시) — Image 자체에 굽고 CanvasGroup은 0→1 페이드 전용 배율(기존 BuildSheetChrome
            // DimOverlay와 동일 기법, 알파값만 0.62→0.72로 교체).
            var dimOverlay = UiKit.Panel(scrim, "DimOverlay", new Color(0f, 0f, 0f, 0.72f));
            UiKit.Fill(dimOverlay);
            dimOverlay.GetComponent<Image>().raycastTarget = false;
            var dimGroup = dimOverlay.gameObject.AddComponent<CanvasGroup>();
            dimGroup.blocksRaycasts = false;
            dimGroup.interactable = false;
            dimGroup.alpha = 0f;

            // ModalRoot — 화면 중앙 고정, 높이는 ContentSizeFitter 자동(하단 보조 행이 꺼지면 다시
            // 접히도록). 폭은 카드열 실폭(320*3+28*2=1016)에 여유를 더한 1040 고정.
            var modalRoot = UiKit.VGroup(scrim, 28f, new RectOffset(0, 0, 0, 0), true, true, autoSizeH: true);
            modalRoot.name = "ModalRoot";
            modalRoot.anchorMin = modalRoot.anchorMax = new Vector2(0.5f, 0.5f);
            modalRoot.pivot = new Vector2(0.5f, 0.5f);
            modalRoot.sizeDelta = new Vector2(1040f, 0f);
            modalRoot.anchoredPosition = Vector2.zero;
            modalRoot.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            // ── 헤더: 티어 배지 + "증강 선택" 46 + 부제 24 ──
            var header = UiKit.VGroup(modalRoot, 10f, new RectOffset(0, 0, 0, 0), true, true);
            header.name = "Header";
            UiKit.SizeHint(header, preferredHeight: 150f, flexibleHeight: 0);
            header.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            var badgeRow = UiKit.HGroup(header, 0, new RectOffset(0, 0, 0, 0), false, true);
            badgeRow.name = "BadgeRow";
            UiKit.SizeHint(badgeRow, preferredHeight: 40f, flexibleHeight: 0);
            badgeRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var (tierBadgeRoot, tierBadgeImg, tierBadgeLabel) = BuildAutoPill(
                badgeRow, "TierBadge", UiKit.PillSprite(40f), 20, new RectOffset(20, 20, 8, 8), true);

            var titleText = UiKit.Text(header, "", 46, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            titleText.name = "Title";
            UiKit.SizeHint(titleText, preferredHeight: 58f, flexibleHeight: 0);

            var subtitleText = UiKit.Text(header, "", 24, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            subtitleText.name = "Subtitle";
            UiKit.SizeHint(subtitleText, preferredHeight: 32f, flexibleHeight: 0);

            // ── 카드 3장 가로열(320×620, gap 28) ──
            var cardsRow = UiKit.HGroup(modalRoot, 28f, new RectOffset(0, 0, 0, 0), false, false);
            cardsRow.name = "CardsRow";
            UiKit.SizeHint(cardsRow, preferredHeight: 620f, flexibleHeight: 0);
            cardsRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var cardTemplate = BuildPerkCardTemplate(cardsRow);

            // ── 하단 보조 행: 재추첨(전체, dev_retake 보유 시) — 보류는 카드별 HoldCorner로 이동
            // (onHold(idx)가 "어느 카드"를 보류할지 요구하는 시그니처라 모달 단일 버튼으로는 표현할
            // 수 없다 — S15 §C가 "[보류]"를 단일 버튼처럼 적어 놓은 부분과의 재해석/충돌, 보고 대상).
            var bottomRow = UiKit.HGroup(modalRoot, 16f, new RectOffset(0, 0, 0, 0), false, true);
            bottomRow.name = "BottomRow";
            UiKit.SizeHint(bottomRow, preferredHeight: 64f, flexibleHeight: 0);
            bottomRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var retakeButton = UiKit.Button(bottomRow, "", new Vector2(300, 64), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(retakeButton, preferredWidth: 300f, preferredHeight: 64f, flexibleWidth: 0, flexibleHeight: 0);
            UiKit.AddGlowOutline(retakeButton.gameObject, UiKit.Bd2, 2f).enabled = true;
            var retakeLabel = retakeButton.GetComponentInChildren<Text>();

            var view = scrim.gameObject.AddComponent<UI2.PerkOfferPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
            so.FindProperty("tierBadgeImage").objectReferenceValue = tierBadgeImg;
            so.FindProperty("tierBadgeText").objectReferenceValue = tierBadgeLabel;
            so.FindProperty("cardsContent").objectReferenceValue = cardsRow;
            so.FindProperty("cardTemplate").objectReferenceValue = cardTemplate;
            so.FindProperty("retakeButton").objectReferenceValue = retakeButton;
            so.FindProperty("retakeButtonLabel").objectReferenceValue = retakeLabel;
            so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(PerkOfferPanel.cs) — Transform.Find는 "/" 없이는 직계 자식만 찾으므로 중간
        // 레이아웃 컨테이너에도 전부 이름을 박아 전체 경로로 찾는다:
        //   "Content/Art/Icon"·"Content/Art/IconEmoji", "Content/Name",
        //   "Content/TierRow/TierRibbon"(+"/Label"), "Content/Badges", "Content/Desc",
        //   "Content/PickButton", "HoldCorner"(카드 루트 직계, 코너 오버레이 — PickView Corner와 동일 톤)
        // S15 §C 카드 구성: 아트 260 정사각(티어색 3px 테두리+글로우) → 이름 32 w900 → 티어 리본 →
        // 효과 설명 24 txt2(3줄) → [선택] 골드 버튼. 시너지 주입 카드는 카드 루트에 보라 Outline을
        // 별도로 켠다(런타임이 enabled 토글).
        private static RectTransform BuildPerkCardTemplate(Transform parent)
        {
            var card = UiKit.Panel(parent, "PerkCardTemplate", UiKit.Panel2, UiSpriteGen.Load("w_card_grad"));
            card.sizeDelta = new Vector2(320f, 620f); // cardsRow(controlChildW/H=false) — 실측 크기 직접 고정.
            UiKit.AddGlowOutline(card.gameObject, UiKit.Purple, 3f); // 시너지 카드 전용(런타임이 enabled 토글, 기본 비활성)
            card.gameObject.AddComponent<CanvasGroup>(); // 선택 시 나머지 카드 페이드아웃 대상(S15 §C)

            // Content 패딩 좌우 30(320-60=260) — Art가 controlChildW=true로 전체 폭을 받으면 정확히
            // 260 정사각이 되도록 역산(별도 정렬용 래퍼 불필요).
            var col = UiKit.VGroup(card, 14f, new RectOffset(30, 30, 22, 20), true, true);
            col.name = "Content";
            UiKit.Fill(col);

            var art = UiKit.Panel(col, "Art", UiKit.Bg1, UiSpriteGen.Load("w_r16"));
            art.name = "Art";
            UiKit.SizeHint(art, preferredHeight: 260f, flexibleHeight: 0);
            UiKit.AddGlowOutline(art.gameObject, UiKit.Bd, 3f).enabled = true; // 색은 런타임이 tierColor로 덮어씀
            var icon = UiKit.Image(art, null, Color.white);
            icon.name = "Icon";
            UiKit.Fill(icon.rectTransform);
            var iconEmoji = UiKit.Text(art, "", 96, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            iconEmoji.name = "IconEmoji";
            UiKit.Fill(iconEmoji.rectTransform);

            var name = UiKit.Text(col, "", 32, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 42f, flexibleHeight: 0);

            var tierRow = UiKit.HGroup(col, 0, new RectOffset(0, 0, 0, 0), false, true);
            tierRow.name = "TierRow";
            UiKit.SizeHint(tierRow, preferredHeight: 28f, flexibleHeight: 0);
            tierRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            BuildAutoPill(tierRow, "TierRibbon", UiKit.PillSprite(28f), 17, new RectOffset(16, 16, 4, 4), true);

            // 보류/시너지 배지 — S15 §C 카드 구성 목록엔 없지만 기존(S7~S12c) 정보를 묵시적으로
            // 삭제하지 않기 위해 티어 리본 아래 작은 캡션으로 유지(보고 대상).
            var badges = UiKit.Text(col, "", 16, UiKit.Purple, TextAnchor.MiddleCenter, true);
            badges.name = "Badges";
            UiKit.SizeHint(badges, preferredHeight: 22f, flexibleHeight: 0);

            var desc = UiKit.Text(col, "", 24, UiKit.Txt2, TextAnchor.UpperCenter);
            desc.name = "Desc";
            UiKit.SizeHint(desc, flexibleHeight: 1);

            // .bigbtn(골드+ink) — 주 액션. PressFx(UiKit.Button 자동 부착)의 PressedScale=0.96이
            // 설계 "누름 시 scale .96"과 그대로 일치 — 별도 구현 없이 재사용.
            var pickBtn = UiKit.Button(col, "선택", new Vector2(0, 66), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            pickBtn.name = "PickButton";
            UiKit.SizeHint(pickBtn, preferredHeight: 66f, flexibleHeight: 0);

            AddGloss(card, 248f); // 40% of 620

            // HoldCorner — 카드별 보류(dev_holdfile 보유 시만 표시, PickView Corner와 동일 톤의 고스트 필).
            var holdCorner = UiKit.Button(card, "보류", new Vector2(108, 40), UiKit.Panel2, UiKit.TextPrimary, null, UiKit.PillSprite(40f));
            holdCorner.name = "HoldCorner";
            var holdRt = holdCorner.GetComponent<RectTransform>();
            holdRt.anchorMin = holdRt.anchorMax = new Vector2(1f, 1f);
            holdRt.pivot = new Vector2(1f, 1f);
            holdRt.anchoredPosition = new Vector2(-14f, -14f);
            UiKit.AddGlowOutline(holdCorner.gameObject, UiKit.Bd2, 1.5f).enabled = true;
            holdCorner.gameObject.SetActive(false); // 기본 숨김 — 런타임이 canHold일 때만 켠다.

            card.gameObject.SetActive(false);
            return card;
        }

        // ── ShopPanel ────────────────────────────────────────────────────────────────
        private static UI2.ShopPanel BuildShopPanel(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "ShopPanel", 1560f, dismissOnScrimClick: false);
            var scrim = chrome.scrim;
            var col = chrome.cardCol;

            var titleText = UiKit.Text(col, "", 28, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 50, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 4, 8, 12);
            var emptyText = UiKit.Text(rowsContent, "오퍼가 없습니다.", 20, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(emptyText, preferredHeight: 60, flexibleHeight: 0);
            var rowTemplate = BuildShopRowTemplate(rowsContent);

            // 웹 renderShop foot 순서 그대로: 새로고침=ghost(보조) · 나가기=bigbtn(주 액션, 골드).
            var btnRow = UiKit.HGroup(col, 14, new RectOffset(0, 0, 4, 0), true, true);
            UiKit.SizeHint(btnRow, preferredHeight: 90, flexibleHeight: 0);
            var rerollButton = UiKit.Button(btnRow, "", new Vector2(0, 90), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(rerollButton, flexibleWidth: 1, preferredHeight: 90, flexibleHeight: 0);
            UiKit.AddGlowOutline(rerollButton.gameObject, UiKit.Bd2, 2f).enabled = true;
            var rerollLabel = rerollButton.GetComponentInChildren<Text>();
            var leaveButton = UiKit.Button(btnRow, "나가기 ▶", new Vector2(0, 90), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(leaveButton, flexibleWidth: 1, preferredHeight: 90, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.ShopPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("rowsContent").objectReferenceValue = rowsContent;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("emptyText").objectReferenceValue = emptyText;
            so.FindProperty("rerollButton").objectReferenceValue = rerollButton;
            so.FindProperty("rerollButtonLabel").objectReferenceValue = rerollLabel;
            so.FindProperty("leaveButton").objectReferenceValue = leaveButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(ShopPanel.cs): IconSlot/Icon·IconSlot/IconEmoji, "Name"/"Desc"(Text),
        // "PriceButton"(Button)+"PriceButton/PriceLabel"(Text). S12c §2 — .pcard 톤.
        private static RectTransform BuildShopRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "ShopRowTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.SizeHint(row, preferredHeight: 140, flexibleHeight: 0);
            UiKit.AddGlowOutline(row.gameObject, UiKit.Bd, 1.5f).enabled = true;
            // "Content"/"InfoCol" 이름 계약(ShopPanel.cs) — Transform.Find는 직계 자식만 찾으므로 중간
            // 컨테이너에도 이름이 필요하다(NodePanel/PerkOfferPanel과 동일 이유).
            var inner = UiKit.HGroup(row, 14, new RectOffset(16, 16, 10, 10), true, true);
            inner.name = "Content";
            UiKit.Fill(inner);

            BuildIconSlot(inner, 88, 48);

            var infoCol = UiKit.VGroup(inner, 2, new RectOffset(0, 0, 0, 0), true, true);
            infoCol.name = "InfoCol";
            UiKit.SizeHint(infoCol, flexibleWidth: 1, flexibleHeight: 0);
            var name = UiKit.Text(infoCol, "", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 30, flexibleHeight: 0);
            var desc = UiKit.Text(infoCol, "", 16, UiKit.TextSecondary, TextAnchor.UpperLeft);
            desc.name = "Desc";
            UiKit.SizeHint(desc, flexibleHeight: 1);

            var priceGo = new GameObject("PriceButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var priceRt = (RectTransform)priceGo.transform;
            priceRt.SetParent(inner, false);
            var priceImg = priceGo.GetComponent<Image>();
            // S13 §A 실측 위반: PriceButton(150×84)에 chip_r999(border 128)를 쓰면 늘어난다 — PillSprite(84)로 교체.
            priceImg.sprite = UiKit.PillSprite(84f);
            priceImg.type = Image.Type.Sliced;
            priceImg.color = UiKit.Accent;
            priceGo.GetComponent<Button>().targetGraphic = priceImg;
            // 골드 배경(Accent)이라 UiKit.Button과 동일 기준으로 fx_btn_press 대상(S13 §E) — 이 버튼은
            // UiKit.Button 헬퍼를 거치지 않는 수동 생성이라 골드 판정이 자동으로 안 걸린다.
            priceGo.GetComponent<PressFx>().SetGold(true);
            UiKit.SizeHint(priceGo.GetComponent<Button>(), preferredWidth: 150, preferredHeight: 84, flexibleWidth: 0, flexibleHeight: 0);
            var priceLabel = UiKit.Text(priceRt, "", 22, UiKit.Bg, TextAnchor.MiddleCenter, true);
            priceLabel.name = "PriceLabel";
            UiKit.Fill(priceLabel.rectTransform);

            AddGloss(row, 56f); // .pcard::after 40% of 140

            row.gameObject.SetActive(false);
            return row;
        }

        // ── PostSpinPanel ────────────────────────────────────────────────────────────
        private static UI2.PostSpinPanel BuildPostSpinPanel(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "PostSpinPanel", 900f, dismissOnScrimClick: false);
            var scrim = chrome.scrim;
            var col = chrome.cardCol;

            // S8 항목⑤: 💥(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var head = UiKit.Text(col, "클리어 실패", 32, UiKit.Bad, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(head, preferredHeight: 46, flexibleHeight: 0);
            var subText = UiKit.Text(col, "", 19, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(subText, preferredHeight: 56, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var manipButtonsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(manipButtonsContent, 0, 8, 14);
            // 만회 수단 = 주 액션(.bigbtn 골드).
            var manipTemplate = BuildLabeledButtonTemplateNamed(manipButtonsContent, UiSpriteGen.Load("w_gold_btn"),
                "ManipButtonTemplate", 92, UiKit.Accent, UiKit.Ink);

            // 포기 = 보조/이탈 액션(.bigbtn.ghost).
            var giveUpButton = UiKit.Button(col, "포기", new Vector2(0, 84), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(giveUpButton, preferredHeight: 84, flexibleHeight: 0);
            UiKit.AddGlowOutline(giveUpButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            var view = scrim.gameObject.AddComponent<UI2.PostSpinPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("subText").objectReferenceValue = subText;
            so.FindProperty("manipButtonsContent").objectReferenceValue = manipButtonsContent;
            so.FindProperty("manipButtonTemplate").objectReferenceValue = manipTemplate;
            so.FindProperty("giveUpButton").objectReferenceValue = giveUpButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── RewardDonePanel ─────────────────────────────────────────────────────────
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 웹 renderRewardDone(ui.js:1897-1936) 구성 순서
        // 그대로: 보상 메시지 → 보유 효과 목록(BagPopup 행 템플릿 관례 재사용) → 현재 능력치(GainPanel
        // Inner/Label·Value 행 관례 재사용) → 다음 스테이지 프리뷰 → [스테이지 N 시작](주 액션 .bigbtn 골드).
        // dismissOnScrimClick:false — NodePanel/PerkOfferPanel과 동일하게 "필수 결정" 화면(스크림 탭으로
        // 못 빠져나감, 다음 스테이지는 버튼으로만 진행).
        private static UI2.RewardDonePanel BuildRewardDonePanel(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "RewardDonePanel", 1400f, dismissOnScrimClick: false);
            var scrim = chrome.scrim;
            var col = chrome.cardCol;

            var messageText = UiKit.Text(col, "", 24, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(messageText, preferredHeight: 60, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var scrollContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(scrollContent, 0, 14, 16);

            // ── 보유 효과 ──
            var buildTitle = UiKit.Text(scrollContent, "보유 효과", 20, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(buildTitle, preferredHeight: 28, flexibleHeight: 0);
            // rowsContent 자신도 VerticalLayoutGroup이라 ILayoutElement로서 실제 활성 자식(행) 기준
            // preferredHeight를 scrollContent에 그대로 보고한다(GainPanel.BuildRunGainPanel의 동일 관례 —
            // SizeHint를 걸면 LayoutElement가 이 자연 보고를 덮어써 항상 0으로 접혀버린다).
            var buildRowsContent = UiKit.VGroup(scrollContent, 8, new RectOffset(0, 0, 0, 0), true, true);
            var buildEmptyText = UiKit.Text(scrollContent, "아직 획득한 증강·유물이 없어요.", 16, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(buildEmptyText, preferredHeight: 40, flexibleHeight: 0);
            var buildRowTemplate = BuildRewardBuildRowTemplate(buildRowsContent);

            // ── 현재 능력치 ──
            var statTitle = UiKit.Text(scrollContent, "현재 능력치", 20, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(statTitle, preferredHeight: 28, flexibleHeight: 0);
            var statRowsContent = UiKit.VGroup(scrollContent, 2, new RectOffset(0, 0, 0, 0), true, true);
            var statEmptyText = UiKit.Text(scrollContent, "보정 없음 — 기본 능력치", 16, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(statEmptyText, preferredHeight: 32, flexibleHeight: 0);
            var statRowTemplate = BuildRewardStatRowTemplate(statRowsContent);
            var symLineText = UiKit.Text(scrollContent, "", 15, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(symLineText, preferredHeight: 60, flexibleHeight: 0);

            // ── 다음 스테이지 프리뷰 ──
            var nextBoxColor = UiKit.Purple; nextBoxColor.a = 0.14f;
            var nextBox = UiKit.Panel(scrollContent, "NextPreview", nextBoxColor, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(nextBox, preferredHeight: 120, flexibleHeight: 0);
            UiKit.AddGlowOutline(nextBox.gameObject, UiKit.Purple, 2f).enabled = true;
            var nextCol = UiKit.VGroup(nextBox, 4, new RectOffset(16, 16, 12, 12), true, true);
            UiKit.Fill(nextCol);
            var nextTitleText = UiKit.Text(nextCol, "", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(nextTitleText, preferredHeight: 30, flexibleHeight: 0);
            var nextBossDescText = UiKit.Text(nextCol, "", 16, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(nextBossDescText, preferredHeight: 40, flexibleHeight: 0);
            var nextSubText = UiKit.Text(nextCol, "", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(nextSubText, preferredHeight: 28, flexibleHeight: 0);

            var startButton = UiKit.Button(col, "", new Vector2(0, 92), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(startButton, preferredHeight: 92, flexibleHeight: 0);
            UiKit.AddGlowOutline(startButton.gameObject, UiKit.Bd2, 2f).enabled = true;
            var startButtonLabel = startButton.GetComponentInChildren<Text>();

            var view = scrim.gameObject.AddComponent<UI2.RewardDonePanel>();
            var so = new SerializedObject(view);
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("messageText").objectReferenceValue = messageText;
            so.FindProperty("buildRowsContent").objectReferenceValue = buildRowsContent;
            so.FindProperty("buildRowTemplate").objectReferenceValue = buildRowTemplate;
            so.FindProperty("buildEmptyText").objectReferenceValue = buildEmptyText;
            so.FindProperty("statRowsContent").objectReferenceValue = statRowsContent;
            so.FindProperty("statRowTemplate").objectReferenceValue = statRowTemplate;
            so.FindProperty("statEmptyText").objectReferenceValue = statEmptyText;
            so.FindProperty("symLineText").objectReferenceValue = symLineText;
            so.FindProperty("nextTitleText").objectReferenceValue = nextTitleText;
            so.FindProperty("nextSubText").objectReferenceValue = nextSubText;
            so.FindProperty("nextBossDescText").objectReferenceValue = nextBossDescText;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("startButtonLabel").objectReferenceValue = startButtonLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(RewardDonePanel.cs BuildBuildRows): Content/IconSlot/Icon·IconSlot/IconEmoji,
        // Content/InfoCol/Name·Desc·Kind. BagPopup의 BuildBagRowTemplate과 동일 톤(.pcard)이되 UseButton
        // 없이 Kind(작은 보조 라벨, 티어/분류)만 추가한다 — 이 시트는 조회 전용이라 행동 버튼이 없다.
        private static RectTransform BuildRewardBuildRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "RewardBuildRowTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.AddGlowOutline(row.gameObject, UiKit.Bd, 1.5f).enabled = true;
            UiKit.SizeHint(row, preferredHeight: 110, flexibleHeight: 0);
            var inner = UiKit.HGroup(row, 12, new RectOffset(14, 14, 8, 8), true, true);
            inner.name = "Content";
            UiKit.Fill(inner);

            BuildIconSlot(inner, 64, 38);

            var infoCol = UiKit.VGroup(inner, 2, new RectOffset(0, 0, 0, 0), true, true);
            infoCol.name = "InfoCol";
            UiKit.SizeHint(infoCol, flexibleWidth: 1, flexibleHeight: 0);
            var name = UiKit.Text(infoCol, "", 20, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 26, flexibleHeight: 0);
            var kind = UiKit.Text(infoCol, "", 14, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            kind.name = "Kind";
            UiKit.SizeHint(kind, preferredHeight: 18, flexibleHeight: 0);
            var desc = UiKit.Text(infoCol, "", 15, UiKit.TextSecondary, TextAnchor.UpperLeft);
            desc.name = "Desc";
            UiKit.SizeHint(desc, flexibleHeight: 1);

            row.gameObject.SetActive(false);
            return row;
        }

        // 자식 경로 계약(RewardDonePanel.cs BuildStatRows): "Inner/Label"·"Inner/Value" — GainPanel의
        // BuildGainRowTemplate과 동일 골격(스태거 애니메이션은 쓰지 않지만 Inner 한 겹 구조는 그대로
        // 재사용해 두 패널이 같은 관례를 공유하게 했다).
        private static RectTransform BuildRewardStatRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "RewardStatRowTemplate", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(row, preferredHeight: 30, flexibleHeight: 0);

            var innerGo = new GameObject("Inner", typeof(RectTransform));
            var inner = (RectTransform)innerGo.transform;
            inner.SetParent(row, false);
            UiKit.Fill(inner);

            var label = UiKit.Text(inner, "", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            label.name = "Label";
            UiKit.SetAnchors(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(4f, 0f), new Vector2(-6f, 0f));

            var value = UiKit.Text(inner, "", 18, UiKit.TextSecondary, TextAnchor.MiddleRight, true);
            value.name = "Value";
            UiKit.SetAnchors(value.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-4f, 0f));

            row.gameObject.SetActive(false);
            return row;
        }

        // ── CellInfoSheet ───────────────────────────────────────────────────────────
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 웹 openCellSheet(ui.js:959-1010) 구성 순서:
        // 헤더(심볼+칸 위치) → 태그 → 특수효과 → EXP/점수 분해(계산줄) → 전체배수/세트보너스 안내 →
        // 영향 항목 목록 → 활성 세트. 행 템플릿은 RewardDonePanel과 동일한 두 관례(Inner/Label·Value,
        // Content/IconSlot+InfoCol)를 재사용한다(BuildRewardStatRowTemplate/BuildRewardBuildRowTemplate).
        // dismissOnScrimClick:true — BagPopup/ManipPickPopup과 동일하게 언제든 스크림 탭으로 닫힌다.
        private static UI2.CellInfoSheet BuildCellInfoSheet(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "CellInfoSheet", 1300f, dismissOnScrimClick: true);
            var scrim = chrome.scrim;
            var scrimButton = scrim.GetComponent<Button>();
            var col = chrome.cardCol;

            var titleText = UiKit.Text(col, "", 24, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(titleText, preferredHeight: 36, flexibleHeight: 0);
            var tagsText = UiKit.Text(col, "", 16, UiKit.Purple, TextAnchor.MiddleLeft);
            UiKit.SizeHint(tagsText, preferredHeight: 26, flexibleHeight: 0);
            var specialsText = UiKit.Text(col, "", 16, UiKit.Accent, TextAnchor.UpperLeft);
            UiKit.SizeHint(specialsText, preferredHeight: 50, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var scrollContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(scrollContent, 0, 12, 14);

            var calcRowsContent = UiKit.VGroup(scrollContent, 2, new RectOffset(0, 0, 0, 0), true, true);
            var calcRowTemplate = BuildRewardStatRowTemplate(calcRowsContent);

            var muNoteText = UiKit.Text(scrollContent, "", 15, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(muNoteText, preferredHeight: 50, flexibleHeight: 0);
            var setNoteText = UiKit.Text(scrollContent, "", 15, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(setNoteText, preferredHeight: 70, flexibleHeight: 0);

            var affectingTitle = UiKit.Text(scrollContent, "이 칸에 영향 주는 증강·유물·캐릭터", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(affectingTitle, preferredHeight: 26, flexibleHeight: 0);
            var affectingRowsContent = UiKit.VGroup(scrollContent, 8, new RectOffset(0, 0, 0, 0), true, true);
            var affectingRowTemplate = BuildRewardBuildRowTemplate(affectingRowsContent);
            var affectingEmptyText = UiKit.Text(scrollContent, "이 칸에 직접 영향 주는 항목이 아직 없어요.", 15, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(affectingEmptyText, preferredHeight: 40, flexibleHeight: 0);

            var setsText = UiKit.Text(scrollContent, "", 15, UiKit.Purple, TextAnchor.UpperLeft);
            UiKit.SizeHint(setsText, preferredHeight: 60, flexibleHeight: 0);

            var closeButton = UiKit.Button(col, "닫기", new Vector2(0, 80), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(closeButton, preferredHeight: 80, flexibleHeight: 0);
            UiKit.AddGlowOutline(closeButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            var view = scrim.gameObject.AddComponent<UI2.CellInfoSheet>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("tagsText").objectReferenceValue = tagsText;
            so.FindProperty("specialsText").objectReferenceValue = specialsText;
            so.FindProperty("calcRowsContent").objectReferenceValue = calcRowsContent;
            so.FindProperty("calcRowTemplate").objectReferenceValue = calcRowTemplate;
            so.FindProperty("muNoteText").objectReferenceValue = muNoteText;
            so.FindProperty("setNoteText").objectReferenceValue = setNoteText;
            so.FindProperty("affectingRowsContent").objectReferenceValue = affectingRowsContent;
            so.FindProperty("affectingRowTemplate").objectReferenceValue = affectingRowTemplate;
            so.FindProperty("affectingEmptyText").objectReferenceValue = affectingEmptyText;
            so.FindProperty("setsText").objectReferenceValue = setsText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── GameOverPanel ────────────────────────────────────────────────────────────
        // S12c §1 — 전체 화면 결과라 시트(하단 슬라이드업) 대신 기존 중앙 팝업 배치를 유지하되, 배경·
        // 테두리·버튼은 §0 토큰으로 정리한다(w_panel_grad+bd2, 메뉴 버튼은 .bigbtn 골드+ink).
        private static UI2.GameOverPanel BuildGameOverPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "GameOverPanel", new Color(0f, 0f, 0f, 0.62f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var dimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

            var card = UiKit.Panel(scrim, "Card", Color.white, UiSpriteGen.Load("w_panel_grad"));
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd2, 2f).enabled = true;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(980f, 1560f);
            var outerCol = UiKit.VGroup(card, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(outerCol);

            var scroll = UiKit.Scroll(outerCol, out var content, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(content, 32, 24, 12);

            var titleScoreText = UiKit.Text(content, "", 26, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleScoreText, preferredHeight: 36, flexibleHeight: 0);
            var finalScoreText = UiKit.Text(content, "", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(finalScoreText, preferredHeight: 42, flexibleHeight: 0);
            var stageReachedText = UiKit.Text(content, "", 22, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(stageReachedText, preferredHeight: 32, flexibleHeight: 0);

            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 ui.js:2132 `.asc-result`) — 승천 런에서만
            // GameOverPanel.Show가 SetActive(true)로 켠다(기본은 꺼둠).
            var ascResultRoot = UiKit.Panel(content, "AscResult", new Color(0, 0, 0, 0));
            UiKit.SizeHint(ascResultRoot, preferredHeight: 30, flexibleHeight: 0);
            var ascResultText = UiKit.Text(ascResultRoot, "", 19, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.Fill(ascResultText.rectTransform);
            ascResultRoot.gameObject.SetActive(false);

            var recordsText = UiKit.Text(content, "", 19, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(recordsText, preferredHeight: 30, flexibleHeight: 0);

            var achHeaderRow = UiKit.Panel(content, "AchHeader", new Color(0, 0, 0, 0));
            UiKit.SizeHint(achHeaderRow, preferredHeight: 34, flexibleHeight: 0);
            var achHeaderText = UiKit.Text(achHeaderRow, "", 22, UiKit.Good, TextAnchor.MiddleLeft, true);
            UiKit.Fill(achHeaderText.rectTransform);

            var achContent = UiKit.VGroup(content, 8, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(achContent, preferredHeight: 0, flexibleHeight: 0, minHeight: 0);
            var achContentCsf = achContent.gameObject.AddComponent<ContentSizeFitter>();
            achContentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var achRowTemplate = BuildAchRowTemplate(achContent);

            var achTotalText = UiKit.Text(content, "", 19, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(achTotalText, preferredHeight: 30, flexibleHeight: 0);

            // ── 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 C, 웹 renderEnd endxp 블록 ui.js:2119-2124) ──
            // 신규 업적 리스트 아래 · (Unity엔 없는) 랭킹 위젯이 있었을 자리 위 — 웹 배치 순서 그대로.
            // Opus 2차검수(P4 1/3) 폴리시④ — 고정 높이 대신 achContent(바로 위)와 동일한 자동높이 조합
            // (VerticalLayoutGroup+ContentSizeFitter)을 xpBlock 자신(배경 Image가 있는 패널)에 직접
            // 얹었다 — 레벨업 미표시(levelUp 행 비활성, 대부분의 런)일 때 하단 공백이 생기던 문제 해소.
            var xpBlock = UiKit.Panel(content, "XpBlock", new Color(0f, 0f, 0f, 0.22f), UiSpriteGen.Load("w_r16"));
            UiKit.AddGlowOutline(xpBlock.gameObject, UiKit.Bd, 1.5f).enabled = true;
            var xpVlg = xpBlock.gameObject.AddComponent<VerticalLayoutGroup>();
            xpVlg.padding = new RectOffset(20, 20, 14, 14);
            xpVlg.spacing = 8;
            xpVlg.childControlWidth = true;
            xpVlg.childControlHeight = true;
            xpVlg.childForceExpandWidth = true;
            xpVlg.childForceExpandHeight = false;
            UiKit.SizeHint(xpBlock, preferredHeight: 0, flexibleHeight: 0, minHeight: 0);
            var xpBlockCsf = xpBlock.gameObject.AddComponent<ContentSizeFitter>();
            xpBlockCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var xpTopRow = UiKit.HGroup(xpBlock, 8, new RectOffset(), true, true);
            UiKit.SizeHint(xpTopRow, preferredHeight: 30, flexibleHeight: 0);
            var xpTopLevelText = UiKit.Text(xpTopRow, "", 20, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(xpTopLevelText, flexibleWidth: 1, flexibleHeight: 0);
            var xpGainText = UiKit.Text(xpTopRow, "", 20, UiKit.Accent, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(xpGainText, preferredWidth: 200, flexibleHeight: 0);

            var xpLevelUpRoot = UiKit.Panel(xpBlock, "LevelUp", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(xpLevelUpRoot, preferredHeight: 30, flexibleHeight: 0);
            var xpLevelUpText = UiKit.Text(xpLevelUpRoot, "", 19, UiKit.Good, TextAnchor.MiddleCenter, true);
            UiKit.Fill(xpLevelUpText.rectTransform);
            xpLevelUpRoot.gameObject.SetActive(false);

            var xpBarBg = UiKit.Panel(xpBlock, "Bar", UiKit.Hex("#2A3048"), UiSpriteGen.Load("bar_bg_r12"));
            UiKit.SizeHint(xpBarBg, preferredHeight: 22, flexibleHeight: 0);
            var xpBarFill = UiKit.Panel(xpBarBg, "Fill", UiKit.Accent, UiSpriteGen.Load("bar_fill_r12"));
            UiKit.SetAnchors(xpBarFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var xpBarFillImage = xpBarFill.GetComponent<Image>();

            var xpNextText = UiKit.Text(xpBlock, "", 17, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(xpNextText, preferredHeight: 24, flexibleHeight: 0);

            var menuButton = UiKit.Button(outerCol, "메뉴로", new Vector2(0, 96), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(menuButton, preferredHeight: 96, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.GameOverPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
            so.FindProperty("cardRect").objectReferenceValue = card;
            so.FindProperty("titleScoreText").objectReferenceValue = titleScoreText;
            so.FindProperty("finalScoreText").objectReferenceValue = finalScoreText;
            so.FindProperty("stageReachedText").objectReferenceValue = stageReachedText;
            so.FindProperty("ascResultRoot").objectReferenceValue = ascResultRoot;
            so.FindProperty("ascResultText").objectReferenceValue = ascResultText;
            so.FindProperty("recordsText").objectReferenceValue = recordsText;
            so.FindProperty("achHeaderRow").objectReferenceValue = achHeaderRow;
            so.FindProperty("achHeaderText").objectReferenceValue = achHeaderText;
            so.FindProperty("achContent").objectReferenceValue = achContent;
            so.FindProperty("achRowTemplate").objectReferenceValue = achRowTemplate;
            so.FindProperty("achTotalText").objectReferenceValue = achTotalText;
            so.FindProperty("xpTopLevelText").objectReferenceValue = xpTopLevelText;
            so.FindProperty("xpGainText").objectReferenceValue = xpGainText;
            so.FindProperty("xpLevelUpRoot").objectReferenceValue = xpLevelUpRoot;
            so.FindProperty("xpLevelUpText").objectReferenceValue = xpLevelUpText;
            so.FindProperty("xpBarFill").objectReferenceValue = xpBarFill;
            so.FindProperty("xpBarFillImage").objectReferenceValue = xpBarFillImage;
            so.FindProperty("xpNextText").objectReferenceValue = xpNextText;
            so.FindProperty("menuButton").objectReferenceValue = menuButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(GameOverPanel.cs): "Label"(Text) — 단순 한 줄 업적 행. S12c §2 — .pcard 톤.
        private static RectTransform BuildAchRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "AchRowTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.AddGlowOutline(row.gameObject, UiKit.Bd, 1.5f).enabled = true;
            UiKit.SizeHint(row, preferredHeight: 56, flexibleHeight: 0);
            var label = UiKit.Text(row, "", 18, UiKit.TextPrimary, TextAnchor.MiddleLeft);
            label.name = "Label";
            var le = UiKit.SizeHint(label, flexibleWidth: 1, flexibleHeight: 1);
            UiKit.SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            row.gameObject.SetActive(false);
            return row;
        }

        // ── BagPopup ─────────────────────────────────────────────────────────────────
        private static UI2.BagPopup BuildBagPopup(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "BagPopup", 1200f, dismissOnScrimClick: true);
            var scrim = chrome.scrim;
            var scrimButton = scrim.GetComponent<Button>();
            var outerCol = chrome.cardCol;

            var titleText = UiKit.Text(outerCol, "", 26, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 48, flexibleHeight: 0);

            var scroll = UiKit.Scroll(outerCol, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 4, 8, 12);
            var emptyText = UiKit.Text(rowsContent, "가방이 비어 있습니다.", 20, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(emptyText, preferredHeight: 60, flexibleHeight: 0);
            var rowTemplate = BuildBagRowTemplate(rowsContent);

            var closeButton = UiKit.Button(outerCol, "닫기", new Vector2(0, 80), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(closeButton, preferredHeight: 80, flexibleHeight: 0);
            UiKit.AddGlowOutline(closeButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            var view = scrim.gameObject.AddComponent<UI2.BagPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("rowsContent").objectReferenceValue = rowsContent;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("emptyText").objectReferenceValue = emptyText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(BagPopup.cs): IconSlot/Icon·IconSlot/IconEmoji, "Name"/"Desc"(Text), "UseButton"(Button).
        // S12c §2 — .pcard 톤.
        private static RectTransform BuildBagRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "BagRowTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.AddGlowOutline(row.gameObject, UiKit.Bd, 1.5f).enabled = true;
            UiKit.SizeHint(row, preferredHeight: 130, flexibleHeight: 0);
            // "Content"/"InfoCol" 이름 계약(BagPopup.cs) — ShopRowTemplate과 동일 이유.
            var inner = UiKit.HGroup(row, 12, new RectOffset(14, 14, 8, 8), true, true);
            inner.name = "Content";
            UiKit.Fill(inner);

            BuildIconSlot(inner, 72, 42);

            var infoCol = UiKit.VGroup(inner, 2, new RectOffset(0, 0, 0, 0), true, true);
            infoCol.name = "InfoCol";
            UiKit.SizeHint(infoCol, flexibleWidth: 1, flexibleHeight: 0);
            var name = UiKit.Text(infoCol, "", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 30, flexibleHeight: 0);
            var desc = UiKit.Text(infoCol, "", 16, UiKit.TextSecondary, TextAnchor.UpperLeft);
            desc.name = "Desc";
            UiKit.SizeHint(desc, flexibleHeight: 1);

            var useBtn = UiKit.Button(inner, "사용", new Vector2(120, 76), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            useBtn.name = "UseButton";
            UiKit.SizeHint(useBtn, preferredWidth: 120, preferredHeight: 76, flexibleWidth: 0, flexibleHeight: 0);

            AddGloss(row, 52f); // 40% of 130

            row.gameObject.SetActive(false);
            return row;
        }

        // ── ManipPickPopup ───────────────────────────────────────────────────────────
        private static UI2.ManipPickPopup BuildManipPickPopup(Transform overlay)
        {
            var chrome = BuildSheetChrome(overlay, "ManipPickPopup", 680f, dismissOnScrimClick: true);
            var scrim = chrome.scrim;
            var scrimButton = scrim.GetComponent<Button>();
            var col = chrome.cardCol;

            var headText = UiKit.Text(col, "", 26, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(headText, preferredHeight: 40, flexibleHeight: 0);
            var descText = UiKit.Text(col, "", 18, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(descText, preferredHeight: 76, flexibleHeight: 0);

            var cellsContent = UiKit.HGroup(col, 10, new RectOffset(0, 0, 10, 10), true, true);
            UiKit.SizeHint(cellsContent, preferredHeight: 100, flexibleHeight: 0);
            // 칸 선택 = 주 액션(.bigbtn 골드).
            var cellTemplate = BuildLabeledButtonTemplateNamed(cellsContent, UiSpriteGen.Load("w_gold_btn"),
                "CellButtonTemplate", 96, UiKit.Accent, UiKit.Ink);

            var spacer = UiKit.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiKit.SizeHint(spacer, flexibleHeight: 1);

            var cancelButton = UiKit.Button(col, "취소", new Vector2(0, 76), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(cancelButton, preferredHeight: 76, flexibleHeight: 0);
            UiKit.AddGlowOutline(cancelButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            var view = scrim.gameObject.AddComponent<UI2.ManipPickPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("headText").objectReferenceValue = headText;
            so.FindProperty("descText").objectReferenceValue = descText;
            so.FindProperty("cellsContent").objectReferenceValue = cellsContent;
            so.FindProperty("cellButtonTemplate").objectReferenceValue = cellTemplate;
            so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── ConfirmSheetPopup(범용 2버튼 확인 시트) ─────────────────────────────────────
        // WEB_PARITY P1 ⑤(포기 확인)·P1 ④(DEVICE 노드 오퍼) 공용 — ManipPickPopup과 동일 골격(제목+
        // 설명+버튼 2개), 다만 칸 목록 대신 주/보조 버튼 각 1개뿐이다. 내용(제목/설명/버튼 라벨)은
        // Show() 호출부가 그때그때 채우므로 여기서는 빈 텍스트로 짓는다.
        private static UI2.ConfirmSheetPopup BuildConfirmSheetPopup(Transform overlay, string name, bool dismissOnScrimClick)
        {
            var chrome = BuildSheetChrome(overlay, name, 620f, dismissOnScrimClick);
            var scrim = chrome.scrim;
            var scrimButton = scrim.GetComponent<Button>();
            var col = chrome.cardCol;

            var headText = UiKit.Text(col, "", 26, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(headText, preferredHeight: 40, flexibleHeight: 0);
            var descText = UiKit.Text(col, "", 18, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(descText, preferredHeight: 100, flexibleHeight: 0);

            var spacer = UiKit.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiKit.SizeHint(spacer, flexibleHeight: 1);

            // 주 액션(.bigbtn 골드) 위 · 보조/이탈 액션(.bigbtn.ghost) 아래 — PostSpinPanel 만회/포기
            // 버튼 순서와 동일 관례.
            var primaryButton = UiKit.Button(col, "", new Vector2(0, 88), UiKit.Accent, UiKit.Ink, null, UiSpriteGen.Load("w_gold_btn"));
            UiKit.SizeHint(primaryButton, preferredHeight: 88, flexibleHeight: 0);
            var primaryLabel = primaryButton.GetComponentInChildren<Text>();

            var secondaryButton = UiKit.Button(col, "", new Vector2(0, 76), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(secondaryButton, preferredHeight: 76, flexibleHeight: 0);
            UiKit.AddGlowOutline(secondaryButton.gameObject, UiKit.Bd2, 2f).enabled = true;
            var secondaryLabel = secondaryButton.GetComponentInChildren<Text>();

            var view = scrim.gameObject.AddComponent<UI2.ConfirmSheetPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("headText").objectReferenceValue = headText;
            so.FindProperty("descText").objectReferenceValue = descText;
            so.FindProperty("primaryButton").objectReferenceValue = primaryButton;
            so.FindProperty("primaryButtonLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("secondaryButton").objectReferenceValue = secondaryButton;
            so.FindProperty("secondaryButtonLabel").objectReferenceValue = secondaryLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── SettingsSheet ────────────────────────────────────────────────────────────
        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 openSettings ui.js:881-908) — 진동 토글(즉시
        // 동작) + 소리/볼륨(P5에서 완성, WEB_PARITY_DESIGN.md §1-A #17 — 아래 소리/볼륨 행 참조) +
        // 데이터 초기화(자기 자신의 ConfirmSheetPopup 재사용) + 닫기. Intro(MenuView)·Play(RunView)
        // 양쪽에서 각자 인스턴스로 호출한다.
        // Opus 2차검수 필수⑥(2026-08-09) — 웹 설정 시트(ui.js:881-908 openSettings)엔 데이터 초기화
        // 행이 없다(그건 홈 화면 전용 `.reset-link`, ui.js:630 — 별개 UI 요소). includeReset=false면
        // resetButton/resetConfirmPopup 자체를 짓지 않는다(SettingsSheet.cs는 두 필드 모두 null 안전).
        private static UI2.SettingsSheet BuildSettingsSheet(Transform overlay, string name, bool includeReset)
        {
            var chrome = BuildSheetChrome(overlay, name, 760f, dismissOnScrimClick: true);
            var scrim = chrome.scrim;
            var scrimButton = scrim.GetComponent<Button>();
            var col = chrome.cardCol;

            var titleRow = UiKit.HGroup(col, 8, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(titleRow, preferredHeight: 48, flexibleHeight: 0);
            var titleText = UiKit.Text(titleRow, "⚙ 설정", 26, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(titleText, flexibleWidth: 1, flexibleHeight: 0);
            var closeButton = UiKit.Button(titleRow, "닫기", new Vector2(120f, 48f), UiKit.Panel2, UiKit.TextSecondary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(closeButton, preferredWidth: 120, preferredHeight: 48, flexibleWidth: 0, flexibleHeight: 0);

            var (vibeRow, vibeToggleButton, vibeToggleLabel) = BuildSettingsToggleRow(col, "진동", true);

            // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 openSettings ui.js:886-896) — 소리 토글
            // (진동과 동일한 토글 행 헬퍼 재사용) + 볼륨 슬라이더(신규 BuildSettingsVolumeRow).
            var (soundRow, soundToggleButton, soundToggleLabel) = BuildSettingsToggleRow(col, "소리", true);
            var (volumeRow, volumeSlider, volumeValueText) = BuildSettingsVolumeRow(col, "볼륨");
            var hint = UiKit.Text(col,
                "볼륨을 움직이면 예시음이 들려요.\n위쪽 [소리]로 전체 음소거, [진동]으로 탭 진동을 끌 수 있어요.",
                15, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(hint, preferredHeight: 52, flexibleHeight: 0);

            var spacer = UiKit.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiKit.SizeHint(spacer, flexibleHeight: 1);

            Button resetButton = null;
            UI2.ConfirmSheetPopup resetConfirmPopup = null;
            if (includeReset)
            {
                resetButton = UiKit.Button(col, "⚠ 데이터 초기화", new Vector2(0f, 76f), UiKit.Panel2, UiKit.Bad, null, UiSpriteGen.Load("w_ghost_btn"));
                UiKit.SizeHint(resetButton, preferredHeight: 76, flexibleHeight: 0);
                UiKit.AddGlowOutline(resetButton.gameObject, UiKit.Bd2, 2f).enabled = true;
                // giveUpConfirmPopup/deviceOfferPopup과 동일 관례 — overlay 산하에 별도 GameObject로 짓는다.
                resetConfirmPopup = BuildConfirmSheetPopup(overlay, name + "_ResetConfirm", dismissOnScrimClick: true);
            }

            var view = scrim.gameObject.AddComponent<UI2.SettingsSheet>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = chrome.card;
            so.FindProperty("dimGroup").objectReferenceValue = chrome.dimGroup;
            so.FindProperty("vibeToggleButton").objectReferenceValue = vibeToggleButton;
            so.FindProperty("vibeToggleLabel").objectReferenceValue = vibeToggleLabel;
            so.FindProperty("soundToggleButton").objectReferenceValue = soundToggleButton;
            so.FindProperty("soundToggleLabel").objectReferenceValue = soundToggleLabel;
            so.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
            so.FindProperty("volumeValueText").objectReferenceValue = volumeValueText;
            so.FindProperty("resetButton").objectReferenceValue = resetButton;
            so.FindProperty("resetConfirmPopup").objectReferenceValue = resetConfirmPopup;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 토글 행(라벨 + on/off 버튼) — 진동/소리 공용. 반환한 Button/Text를 SettingsSheet가 직접
        // 채운다(웹 .set-tog 관례를 라벨색 반전으로 근사).
        private static (RectTransform row, Button toggle, Text label) BuildSettingsToggleRow(RectTransform parent, string title, bool defaultOn)
        {
            var row = UiKit.HGroup(parent, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(row, preferredHeight: 56, flexibleHeight: 0);
            var label = UiKit.Text(row, title, 19, UiKit.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(label, flexibleWidth: 1, flexibleHeight: 0);
            var toggle = UiKit.Button(row, defaultOn ? "켜짐" : "꺼짐", new Vector2(120f, 48f),
                defaultOn ? UiKit.Accent : UiKit.Panel2, defaultOn ? UiKit.Bg : UiKit.TextSecondary, null, UiKit.PillSprite(48f));
            UiKit.SizeHint(toggle, preferredWidth: 120, preferredHeight: 48, flexibleWidth: 0, flexibleHeight: 0);
            var toggleLabel = toggle.GetComponentInChildren<Text>();
            return (row, toggle, toggleLabel);
        }

        // 볼륨 행(라벨 + 슬라이더 + "N%" 값) — 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹
        // #volrange 행 ui.js:892-896). 반환한 Slider/Text를 SettingsSheet가 직접 채운다.
        private static (RectTransform row, Slider slider, Text valueText) BuildSettingsVolumeRow(RectTransform parent, string title)
        {
            var row = UiKit.HGroup(parent, 12, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(row, preferredHeight: 56, flexibleHeight: 0);
            var label = UiKit.Text(row, title, 19, UiKit.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.SizeHint(label, preferredWidth: 90, flexibleWidth: 0, flexibleHeight: 0);

            var sliderRt = BuildSlider(row, "VolumeSlider");
            UiKit.SizeHint(sliderRt, flexibleWidth: 1, preferredHeight: 36, flexibleHeight: 0);
            var slider = sliderRt.GetComponent<Slider>();

            var valueText = UiKit.Text(row, "70%", 17, UiKit.TextSecondary, TextAnchor.MiddleRight);
            UiKit.SizeHint(valueText, preferredWidth: 66, flexibleWidth: 0, flexibleHeight: 0);

            return (row, slider, valueText);
        }

        // 표준 uGUI Slider 골격(Background/Fill Area→Fill/Handle Slide Area→Handle) — 이 프로젝트에는
        // 지금까지 Slider 소비처가 없어(기존 진행바는 전부 anchorMax 기반 정적 표시 바) 신규로 짓는다.
        // 값 범위는 항상 0..1(SoundKit.Volume과 동일 스케일)로 고정 — 호출측이 SetValueWithoutNotify로 채운다.
        private static RectTransform BuildSlider(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();

            // Opus 2차검수 항목5(2026-08-09) — 트랙 두께 10f는 S13 §A 9-slice 위반 구간(border합
            // 18px > 대상 10px, PillSprite가 폴백으로 w_r9를 억지로 눌러써 늘어난 타원이 됨)이라
            // 18px 이상으로 올린다(터치 영역 체감도 함께 개선).
            const float trackThickness = 18f;
            var bg = UiKit.Panel(rt, "Background", UiKit.Panel2, UiKit.PillSprite(trackThickness));
            bg.anchorMin = new Vector2(0f, 0.5f);
            bg.anchorMax = new Vector2(1f, 0.5f);
            bg.sizeDelta = new Vector2(0f, trackThickness);
            bg.anchoredPosition = Vector2.zero;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            var fillArea = (RectTransform)fillAreaGo.transform;
            fillArea.SetParent(rt, false);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-8f, trackThickness);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = UiKit.Panel(fillArea, "Fill", UiKit.Accent, UiKit.PillSprite(trackThickness));
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = Vector2.zero;
            fill.anchoredPosition = Vector2.zero;

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            var handleArea = (RectTransform)handleAreaGo.transform;
            handleArea.SetParent(rt, false);
            UiKit.Fill(handleArea);

            // Opus 2차검수 항목2(2026-08-09) — Slider.UpdateVisuals가 방향축과 직교하는 축(여기서는
            // y)의 handle anchorMin/anchorMax를 무조건 (0,1)(Handle Slide Area 전체로 스트레치)로
            // 덮어쓴다(LeftToRight 기준 x만 정규값으로 고정, y는 항상 스트레치). 스트레치 앵커에서
            // sizeDelta.y는 "부모 대비 오프셋"이 되어 최종 높이=parentHeight+sizeDelta.y가 된다 —
            // 26f를 그대로 두면 슬라이더 행(36px)보다 훨씬 큰 62px로 튀어나온다. y=0으로 두면 정확히
            // Handle Slide Area 높이(=슬라이더 행 높이)를 그대로 채운다(x는 앵커가 점으로 붕괴돼
            // sizeDelta.x가 그대로 절대폭이 된다 — Unity 기본 Slider 프리팹의 (20,0) 관례와 동일 원리).
            var handle = UiKit.Panel(handleArea, "Handle", UiKit.TextPrimary, UiKit.PillSprite(26f));
            handle.anchorMin = new Vector2(0f, 0.5f);
            handle.anchorMax = new Vector2(0f, 0.5f);
            handle.sizeDelta = new Vector2(26f, 0f);
            handle.anchoredPosition = Vector2.zero;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0.7f; // DefaultVolume(SoundKit.cs)와 동일값 — 실값은 호출측이 SetValueWithoutNotify로 다시 채운다.

            return rt;
        }

        // 라벨 1개짜리 버튼 템플릿(공용) — 자식 경로 계약: "Label"(Text). name/height를 호출측이 지정한다.
        // S12c §2 — bg/fg를 호출측이 지정(PostSpinPanel 만회 버튼·ManipPickPopup 칸 버튼 모두 .bigbtn
        // 골드 톤으로 통일 — 이전엔 하드코딩된 Blue였다, §0 토큰표에 없는 임의색이라 정리 대상).
        private static RectTransform BuildLabeledButtonTemplateNamed(Transform parent, Sprite panelSprite, string name,
            float height, Color bg, Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            if (panelSprite != null) { img.sprite = panelSprite; img.type = Image.Type.Sliced; }
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: height, flexibleHeight: 0);
            // S13 §E — fx_btn_press는 골드 버튼만. UiKit.Button 헬퍼를 안 거치는 수동 생성이라 직접 설정.
            go.GetComponent<PressFx>().SetGold(bg == UiKit.Accent);

            var label = UiKit.Text(rt, "", 22, fg, TextAnchor.MiddleCenter, true);
            label.name = "Label";
            UiKit.Fill(label.rectTransform);

            go.SetActive(false);
            return rt;
        }

        // 스크롤 Content 공용 설정(VerticalLayoutGroup+ContentSizeFitter) — 여러 패널의 카드/행 목록이
        // 전부 이 조합이라 헬퍼로 묶었다(PerkOfferPanel/ShopPanel/BagPopup/GameOverPanel/NodePanel).
        private static void SetupStackContent(RectTransform content, int paddingH, int paddingV, float spacing)
        {
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(paddingH, paddingH, paddingV, paddingV);
            vlg.spacing = spacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // DexScreen — 카테고리 탭(가로 스크롤 pill) + 3열 그리드 + 상세 팝업(DexDetailPopup, 전역
        // OverlayLayer 산하). 이관 원본: Scripts/UI/DexScreen.cs·DetailPopup.cs.
        // ══════════════════════════════════════════════════════════════════════════════
        // S12c §3 — 토큰 정리: 헤더 뒤로가기=.bigbtn.ghost, 통계 4타일=.hud-stats 톤(rgba(0,0,0,.25)+bd+
        // r-md+상단 gloss, MenuView.BuildHudStatCell과 동일 관례), 카테고리 탭=pill(UiKit.PillSprite —
        // 9-slice 늘어남 금지 지시 그대로, 96 높이에 맞는 최대 안전 반경 자동 선택).
        private static DexBuildResult BuildDexScreen(Transform canvasRoot, UI2.DexDetailPopup detailPopup)
        {
            var result = new DexBuildResult();
            var ghostBtnSprite = UiSpriteGen.Load("w_ghost_btn");

            var root = UiKit.Panel(canvasRoot, "DexScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<UI2.DexView>();

            var col = UiKit.VGroup(root, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);

            // 헤더 — 90. S8 항목⑤: 📖(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var header = UiKit.HGroup(col, 16, new RectOffset(24, 24, 16, 8), true, true);
            UiKit.SizeHint(header, preferredHeight: 90, flexibleHeight: 0);
            var title = UiKit.Text(header, "잭팟런 도감", UiKit.TextStyle.H1, TextAnchor.MiddleLeft);
            UiKit.SizeHint(title, flexibleWidth: 1, flexibleHeight: 0);
            result.backButton = UiKit.Button(header, "← 메뉴", new Vector2(160, 70), UiKit.Panel2, UiKit.TextPrimary, null, ghostBtnSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 160, preferredHeight: 70, flexibleWidth: 0, flexibleHeight: 0);
            UiKit.AddGlowOutline(result.backButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            // 통계 4타일 — 96. S8 항목⑤: astral 이모지(🏆🧗🔁📈)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var statsRow = UiKit.HGroup(col, 12, new RectOffset(24, 24, 4, 12), true, true);
            UiKit.SizeHint(statsRow, preferredHeight: 96, flexibleHeight: 0);
            result.statBestScoreText = BuildStatTile(statsRow, "최고점수");
            result.statBestStageText = BuildStatTile(statsRow, "최고 스테이지");
            result.statRunsText = BuildStatTile(statsRow, "런");
            result.statTotalScoreText = BuildStatTile(statsRow, "통산 점수");

            // 카테고리 탭 — 96, JackpotCatalog.CategoryOrder 8종 고정(제목은 JackpotCatalog.CategoryTitle,
            // S8에서 이모지 제거됨). pill(UiKit.PillSprite(96)) — 활성/비활성은 배경색만 토글
            // (Panel3/PanelBg, §0 토큰), 테두리는 정적 bd(활성 시 amber로 바뀌는 CSS 디테일은 생략 —
            // 재해석 보고 대상).
            var tabPill = UiKit.PillSprite(96f);
            var tabScroll = UiKit.Scroll(col, out var tabsContent, vertical: false);
            UiKit.SizeHint(tabScroll, preferredHeight: 96, flexibleHeight: 0);
            var tabsHlg = tabsContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsHlg.spacing = 10;
            tabsHlg.padding = new RectOffset(20, 20, 8, 8);
            tabsHlg.childControlWidth = true;
            tabsHlg.childControlHeight = true;
            tabsHlg.childForceExpandWidth = false;
            tabsHlg.childForceExpandHeight = false;
            tabsHlg.childAlignment = TextAnchor.MiddleLeft;
            var tabsCsf = tabsContent.gameObject.AddComponent<ContentSizeFitter>();
            tabsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var order = JackpotCatalog.CategoryOrder;
            result.tabImages = new Image[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                string cat = order[i];
                var tabGo = new GameObject("Tab_" + cat, typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
                var tabRt = (RectTransform)tabGo.transform;
                tabRt.SetParent(tabsContent, false);
                var tabImg = tabGo.GetComponent<Image>();
                tabImg.sprite = tabPill;
                tabImg.type = Image.Type.Sliced;
                tabImg.color = UiKit.PanelBg;
                UiKit.AddGlowOutline(tabGo, UiKit.Bd, 1.5f).enabled = true;
                result.tabImages[i] = tabImg;
                var tabBtn = tabGo.GetComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                UiKit.SizeHint(tabBtn, preferredWidth: 168, preferredHeight: 96, flexibleWidth: 0, flexibleHeight: 0);

                var tabLabel = UiKit.Text(tabRt, JackpotCatalog.CategoryTitle(cat), 19, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
                UiKit.Fill(tabLabel.rectTransform);

                UnityEditor.Events.UnityEventTools.AddStringPersistentListener(tabBtn.onClick, result.view.SetCategory, cat);
            }

            // 카드 그리드(3열) — S10: jackpotdex/style.css .card는 가로 배치(아이콘 좌 + 이름/설명 우)라
            // 세로 아트 카드보다 훨씬 낮다 — 셀 높이를 420→150으로 축소.
            var gridScroll = UiKit.Scroll(col, out var gridContent, vertical: true);
            UiKit.SizeHint(gridScroll, flexibleHeight: 1);
            UiKit.Grid(gridContent, new Vector2(328, 150), new Vector2(16, 16), 3);
            gridContent.gameObject.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 20, 20, 20);
            result.gridContent = gridContent;
            result.cardTemplate = BuildDexCardTemplate(gridContent);

            return result;
        }

        // .hud-stats 칸 톤 — rgba(0,0,0,.25) + bd + r-md + 상단 50% gloss (MenuView.BuildHudStatCell과
        // 동일 관례, S12c §3 "S12 토큰 적용").
        private static Text BuildStatTile(RectTransform row, string label)
        {
            var cell = UiKit.Panel(row, "Stat", new Color(0f, 0f, 0f, 0.25f), UiSpriteGen.Load("w_r12"));
            UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: 96, flexibleHeight: 0);
            UiKit.AddGlowOutline(cell.gameObject, UiKit.Bd, 1.5f).enabled = true;
            var col = UiKit.VGroup(cell, 2, new RectOffset(8, 8, 10, 10), true, true);
            UiKit.Fill(col);
            var l = UiKit.Text(col, label, 15, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(l, preferredHeight: 22, flexibleHeight: 0);
            var v = UiKit.Text(col, "-", 25, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(v, flexibleHeight: 1);
            AddGloss(cell, 48f); // 50% of 96
            return v;
        }

        // S10/S12c §3 — jackpotdex/style.css .card: 가로 배치(아이콘 좌 48px→77 + 이름/설명 우). .pcard
        // 톤(w_card_grad r16 + bd 1.5 + 상단 40% gloss)으로 갱신. 자식 경로 계약(DexView.cs):
        // "Content/IconSlot/Icon"·"IconSlot/IconEmoji", "Content/Name"/"Desc"/"Sub", "Lock"(GameObject)+
        // "Lock/Hint"(Text).
        private static RectTransform BuildDexCardTemplate(Transform parent)
        {
            var r11 = UiSpriteGen.Load("rrect_r11");
            var card = UiKit.Panel(parent, "DexCardTemplate", Color.white, UiSpriteGen.Load("w_card_grad"));
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd, 1.5f).enabled = true;
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card.GetComponent<Image>();
            card.gameObject.AddComponent<PressFx>();

            // "Content" 이름 계약(DexView.cs) — Transform.Find는 직계 자식만 찾으므로 Icon/Name/Desc/Sub는
            // "Content/..."로 찾는다(NodePanel/PerkOfferPanel/ShopPanel과 동일 이유).
            var row = UiKit.HGroup(card, 14, new RectOffset(14, 14, 12, 12), true, true);
            row.name = "Content";
            UiKit.Fill(row);

            var iconSlot = UiKit.Panel(row, "IconSlot", UiKit.Bg1, r11);
            UiKit.SizeHint(iconSlot, preferredWidth: 77, preferredHeight: 77, flexibleWidth: 0, flexibleHeight: 0);
            var icon = UiKit.Image(iconSlot, null, Color.white);
            icon.name = "Icon";
            UiKit.Fill(icon.rectTransform);
            var iconEmoji = UiKit.Text(iconSlot, "", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            iconEmoji.name = "IconEmoji";
            UiKit.Fill(iconEmoji.rectTransform);

            var info = UiKit.VGroup(row, 3, new RectOffset(0, 0, 0, 0), true, false);
            UiKit.SizeHint(info, flexibleWidth: 1);
            info.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            var name = UiKit.Text(info, "", 20, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 26, flexibleHeight: 0);

            var desc = UiKit.Text(info, "", 16, UiKit.TextSecondary, TextAnchor.UpperLeft);
            desc.name = "Desc";
            UiKit.SizeHint(desc, preferredHeight: 40, flexibleHeight: 0);

            var sub = UiKit.Text(info, "", 15, UiKit.Accent, TextAnchor.UpperLeft);
            sub.name = "Sub";
            UiKit.SizeHint(sub, preferredHeight: 20, flexibleHeight: 0);

            // 잠금 — jackpotdex .card.masked 재해석: 전체를 어둡게(알파) + "❓ ???" 마스킹은 DexView가
            // Name/Desc 텍스트 자체를 바꿔치기하고, 이 오버레이는 우측 상단에 조건 힌트만 작게 보여준다.
            var lockOverlay = UiKit.Panel(card, "Lock", UiKit.LockScrim);
            UiKit.Fill(lockOverlay);
            var lockCol = UiKit.VGroup(lockOverlay, 4, new RectOffset(14, 14, 10, 10), true, true);
            lockCol.name = "Content";
            UiKit.Fill(lockCol);
            lockCol.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            var lockHint = UiKit.Text(lockCol, "", 15, UiKit.TextSecondary, TextAnchor.LowerLeft);
            lockHint.name = "Hint";
            UiKit.SizeHint(lockHint, flexibleHeight: 1);
            lockOverlay.gameObject.SetActive(false);

            AddGloss(card, 60f); // .pcard::after 40% of 150

            card.gameObject.SetActive(false);
            return card;
        }

        private static void WireDexView(DexBuildResult r, UI2.DexDetailPopup detailPopup)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("statBestScoreText").objectReferenceValue = r.statBestScoreText;
            so.FindProperty("statBestStageText").objectReferenceValue = r.statBestStageText;
            so.FindProperty("statRunsText").objectReferenceValue = r.statRunsText;
            so.FindProperty("statTotalScoreText").objectReferenceValue = r.statTotalScoreText;
            SetObjectArray(so, "tabImages", r.tabImages);
            so.FindProperty("gridContent").objectReferenceValue = r.gridContent;
            so.FindProperty("cardTemplate").objectReferenceValue = r.cardTemplate;
            so.FindProperty("detailPopup").objectReferenceValue = detailPopup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── DexDetailPopup(전역 OverlayLayer 산하) ──────────────────────────────────────
        // S12c §3 — "DexView 톤 정리"에 포함(같은 파일의 상세 팝업). 시트 목록(①) 대상은 아니라 기존
        // 중앙 팝업 배치는 유지하고 배경/테두리/닫기 버튼만 토큰으로 정리(GameOverPanel과 동일 취지).
        private static UI2.DexDetailPopup BuildDexDetailPopup(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "DexDetailPopup", new Color(0f, 0f, 0f, 0.62f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = UiKit.Panel(scrim, "Card", Color.white, UiSpriteGen.Load("w_panel_grad"));
            UiKit.AddGlowOutline(card.gameObject, UiKit.Bd2, 2f).enabled = true;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(900f, 1600f);
            var cardBlocker = card.gameObject.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;
            var cardCol = UiKit.VGroup(card, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(cardCol);

            var scroll = UiKit.Scroll(cardCol, out var content, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(content, 32, 24, 12);

            // 아트(512) — 스프라이트 없으면 대형 이모지로 폴백(카탈로그 데이터의 emoji 필드 — 카탈로그
            // 카드는 전부 실제 아트가 있어 이 경로를 거의 타지 않는다).
            var iconRow = UiKit.Panel(content, "IconRow", new Color(0, 0, 0, 0));
            UiKit.SizeHint(iconRow, preferredHeight: 512, flexibleHeight: 0);
            var iconImage = UiKit.Image(iconRow, null, Color.white);
            iconImage.name = "Icon";
            iconImage.rectTransform.anchorMin = iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.sizeDelta = new Vector2(512f, 512f);
            var iconEmojiText = UiKit.Text(iconRow, "", 220, Color.white, TextAnchor.MiddleCenter);
            iconEmojiText.name = "IconEmoji";
            UiKit.Fill(iconEmojiText.rectTransform);

            var titleText = UiKit.Text(content, "", 38, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 52, flexibleHeight: 0);
            var metaText = UiKit.Text(content, "", 21, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(metaText, preferredHeight: 30, flexibleHeight: 0);
            var descText = UiKit.Text(content, "", 23, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(descText, preferredHeight: 90, flexibleHeight: 0);
            var unlockText = UiKit.Text(content, "", 19, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(unlockText, preferredHeight: 50, flexibleHeight: 0);

            var pickSection = UiKit.VGroup(content, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(pickSection, preferredHeight: 0, flexibleHeight: 0);
            var pickCsf = pickSection.gameObject.AddComponent<ContentSizeFitter>();
            pickCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var pickRoleEffText = UiKit.Text(pickSection, "", 21, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(pickRoleEffText, preferredHeight: 60, flexibleHeight: 0);
            var pickBuildTagsText = UiKit.Text(pickSection, "", 19, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(pickBuildTagsText, preferredHeight: 50, flexibleHeight: 0);
            var pickMetersText = UiKit.Text(pickSection, "", 21, UiKit.Accent, TextAnchor.UpperLeft, true);
            UiKit.SizeHint(pickMetersText, preferredHeight: 32, flexibleHeight: 0);

            var colsRow = UiKit.HGroup(pickSection, 20, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(colsRow, preferredHeight: 220, flexibleHeight: 0);
            var prosText = BuildDetailListCell(colsRow, "장점", UiKit.Good);
            var consText = BuildDetailListCell(colsRow, "주의", UiKit.Bad);

            var closeButton = UiKit.Button(cardCol, "닫기", new Vector2(0, 84), UiKit.Panel2, UiKit.TextPrimary, null, UiSpriteGen.Load("w_ghost_btn"));
            UiKit.SizeHint(closeButton, preferredHeight: 84, flexibleHeight: 0);
            UiKit.AddGlowOutline(closeButton.gameObject, UiKit.Bd2, 2f).enabled = true;

            var view = scrim.gameObject.AddComponent<UI2.DexDetailPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = card;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("iconEmojiText").objectReferenceValue = iconEmojiText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("metaText").objectReferenceValue = metaText;
            so.FindProperty("descText").objectReferenceValue = descText;
            so.FindProperty("unlockText").objectReferenceValue = unlockText;
            so.FindProperty("pickRoleEffText").objectReferenceValue = pickRoleEffText;
            so.FindProperty("pickBuildTagsText").objectReferenceValue = pickBuildTagsText;
            so.FindProperty("pickMetersText").objectReferenceValue = pickMetersText;
            so.FindProperty("pickSection").objectReferenceValue = pickSection;
            so.FindProperty("prosText").objectReferenceValue = prosText;
            so.FindProperty("consText").objectReferenceValue = consText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // RankScreen — S15: 글로벌 랭킹(jackpotrank/$pid, 앱+웹 공용). BuildDexScreen 컨벤션 그대로
        // (헤더 → 상태 문구 → 세로 스크롤 행 목록). 이관 원본 없음(신규 화면).
        // ══════════════════════════════════════════════════════════════════════════════
        private static RankBuildResult BuildRankScreen(Transform canvasRoot)
        {
            var result = new RankBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

            var root = UiKit.Panel(canvasRoot, "RankScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<UI2.RankView>();

            var col = UiKit.VGroup(root, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);

            // 헤더 — 90.
            var header = UiKit.HGroup(col, 16, new RectOffset(24, 24, 16, 8), true, true);
            UiKit.SizeHint(header, preferredHeight: 90, flexibleHeight: 0);
            // "🏆 랭킹"이 아니라 한글만 — astral 이모지는 레거시 Text가 렌더링하지 못한다(S8 항목⑤).
            var title = UiKit.Text(header, "잭팟런 랭킹", UiKit.TextStyle.H1, TextAnchor.MiddleLeft);
            UiKit.SizeHint(title, flexibleWidth: 1, flexibleHeight: 0);
            result.backButton = UiKit.Button(header, "← 메뉴", new Vector2(160, 70), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 160, preferredHeight: 70, flexibleWidth: 0, flexibleHeight: 0);

            // 상태 문구(로딩/빈 목록/오류) — RankView.OnEnable·OnFetchOk·OnFetchError가 채운다.
            result.statusText = UiKit.Text(col, "", UiKit.TextStyle.BodySecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.statusText, preferredHeight: 64, flexibleHeight: 0);

            // 세로 스크롤 목록 — 상위 100행(RankView가 채움).
            var listScroll = UiKit.Scroll(col, out var listContent, vertical: true);
            UiKit.SizeHint(listScroll, flexibleHeight: 1);
            var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(20, 20, 12, 20);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var listCsf = listContent.gameObject.AddComponent<ContentSizeFitter>();
            listCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            result.listContent = listContent;
            result.rowTemplate = BuildRankRowTemplate(listContent);

            return result;
        }

        // 자식 경로 계약(RankView.cs): 루트 자신에 행 배경 Image, "Content/RankNo"·"Content/Nick"·
        // "Content/Score" 각 Text. UiKit.HGroup이 만드는 중간 GameObject를 "Content"로 개명한다 —
        // Transform.Find는 직계 자식만 찾으므로(BuildDexCardTemplate과 동일 이유, Opus S15 치명-1).
        private static RectTransform BuildRankRowTemplate(Transform parent)
        {
            var r11 = UiSpriteGen.Load("rrect_r11");
            var row = UiKit.Panel(parent, "RankRowTemplate", UiKit.PanelBg, r11);
            UiKit.SizeHint(row, preferredHeight: 84, flexibleHeight: 0);

            var content = UiKit.HGroup(row, 12, new RectOffset(18, 18, 12, 12), true, true);
            content.name = "Content";
            UiKit.Fill(content);

            var rankNo = UiKit.Text(content, "", 28, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            rankNo.name = "RankNo";
            UiKit.SizeHint(rankNo, preferredWidth: 88, flexibleWidth: 0);

            var nick = UiKit.Text(content, "", 24, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            nick.name = "Nick";
            UiKit.SizeHint(nick, flexibleWidth: 1);

            var score = UiKit.Text(content, "", 22, UiKit.TextSecondary, TextAnchor.MiddleRight);
            score.name = "Score";
            UiKit.SizeHint(score, preferredWidth: 320, flexibleWidth: 0);

            row.gameObject.SetActive(false);
            return row;
        }

        private static void WireRankView(RankBuildResult r)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("statusText").objectReferenceValue = r.statusText;
            so.FindProperty("listContent").objectReferenceValue = r.listContent;
            so.FindProperty("rowTemplate").objectReferenceValue = r.rowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // LevelRewardsScreen — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 B, 웹 renderLevelRewards
        // ui.js:635-646). BuildRankScreen 컨벤션 그대로(헤더 → 레벨 카드 → 로드맵 헤더 → 세로 스크롤
        // 행 목록). 이관 원본 없음(신규 화면) — 로드맵 데이터는 PlayerProfile.LevelUnlocks()(P3-4에서
        // 이미 엔진에 준비됨)를 그대로 읽는다.
        // ══════════════════════════════════════════════════════════════════════════════
        private static LevelRewardsBuildResult BuildLevelRewardsScreen(Transform canvasRoot)
        {
            var result = new LevelRewardsBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

            var root = UiKit.Panel(canvasRoot, "LevelRewardsScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<UI2.LevelRewardsView>();

            // spacing=10 — BuildRankScreen(요소 3개, spacing 0)과 달리 이 화면은 헤더/부제/레벨카드/
            // 로드맵헤더/목록 5개가 쌓여 0이면 서로 맞붙어 답답해 보인다(각 요소 preferredHeight는
            // 자기 내부 패딩만 책임지고, 형제 사이 간격은 이 spacing이 담당).
            var col = UiKit.VGroup(root, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);

            // 헤더 — 90.
            var header = UiKit.HGroup(col, 16, new RectOffset(24, 24, 16, 8), true, true);
            UiKit.SizeHint(header, preferredHeight: 90, flexibleHeight: 0);
            var title = UiKit.Text(header, "레벨 보상", UiKit.TextStyle.H1, TextAnchor.MiddleLeft);
            UiKit.SizeHint(title, flexibleWidth: 1, flexibleHeight: 0);
            result.backButton = UiKit.Button(header, "← 메뉴", new Vector2(160, 70), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 160, preferredHeight: 70, flexibleWidth: 0, flexibleHeight: 0);

            var sub = UiKit.Text(col, "레벨을 올리면 후반 캐릭터·슬롯·장치·증강·유물이 해금돼요",
                UiKit.TextStyle.BodySecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(sub, preferredHeight: 40, flexibleHeight: 0);

            // 세로 패딩은 주지 않는다(VGroup은 자식을 쌓으므로 세로 패딩을 더하면 이 래퍼의
            // "선언한" preferredHeight와 "실제로 필요한" 내부 높이(자식 preferred + 패딩)가 어긋나
            // 카드 하단이 잘리거나 다음 요소와 겹친다 — 세로 여백은 대신 col의 spacing이 담당).
            var cardMargin = UiKit.VGroup(col, 0, new RectOffset(24, 24, 0, 0), true, true);
            UiKit.SizeHint(cardMargin, preferredHeight: 150, flexibleHeight: 0);
            var levelCard = BuildLevelCard(cardMargin, clickable: false);
            result.levelBadgeText = levelCard.badgeText;
            result.levelXpText = levelCard.xpText;
            result.levelBarFill = levelCard.barFill;
            result.levelBarFillImage = levelCard.barFillImage;

            // 로드맵 헤더 — "레벨 해금 보상 n/m"(웹 원문 앞의 자물쇠 이모지는 astral이라 생략).
            var roadHeader = UiKit.HGroup(col, 8, new RectOffset(24, 24, 8, 4), true, true);
            UiKit.SizeHint(roadHeader, preferredHeight: 40, flexibleHeight: 0);
            var roadTitle = UiKit.Text(roadHeader, "레벨 해금 보상", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(roadTitle, flexibleWidth: 1, flexibleHeight: 0);
            result.roadCountText = UiKit.Text(roadHeader, "", 19, UiKit.TextSecondary, TextAnchor.MiddleRight);
            UiKit.SizeHint(result.roadCountText, preferredWidth: 120, flexibleHeight: 0);

            // 세로 스크롤 목록 — LevelRewardsView가 PlayerProfile.LevelUnlocks() 전체를 채운다.
            var listScroll = UiKit.Scroll(col, out var listContent, vertical: true);
            UiKit.SizeHint(listScroll, flexibleHeight: 1);
            var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(20, 20, 8, 20);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var listCsf = listContent.gameObject.AddComponent<ContentSizeFitter>();
            listCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            result.listContent = listContent;
            result.rowTemplate = BuildLevelRoadRowTemplate(listContent);

            // 웹 roadHtml || '해금 항목 없음' 폴백(ui.js:640, Opus 2차검수 정리) — rowTemplate과 나란히
            // listContent의 영구 자식으로 두고, 뷰가 road.Count==0일 때만 활성화한다.
            result.emptyText = UiKit.Text(listContent, "해금 항목 없음", 18, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.emptyText, preferredHeight: 40, flexibleHeight: 0);
            result.emptyText.gameObject.SetActive(false);

            return result;
        }

        // 자식 경로 계약(LevelRewardsView.cs): 루트 자신에 행 배경 Image, "Content/Lv"·"Content/Label"·
        // "Content/Check" 각 Text — BuildRankRowTemplate과 동일 관례(Transform.Find는 직계 자식만 찾으므로
        // UiKit.HGroup이 만드는 중간 GameObject를 "Content"로 개명한다).
        private static RectTransform BuildLevelRoadRowTemplate(Transform parent)
        {
            var r11 = UiSpriteGen.Load("rrect_r11");
            var row = UiKit.Panel(parent, "RoadRowTemplate", UiKit.PanelBg, r11);
            UiKit.SizeHint(row, preferredHeight: 74, flexibleHeight: 0);

            var content = UiKit.HGroup(row, 12, new RectOffset(18, 18, 10, 10), true, true);
            content.name = "Content";
            UiKit.Fill(content);

            var lv = UiKit.Text(content, "", 22, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            lv.name = "Lv";
            UiKit.SizeHint(lv, preferredWidth: 96, flexibleWidth: 0);

            var label = UiKit.Text(content, "", 20, UiKit.TextPrimary, TextAnchor.MiddleLeft);
            label.name = "Label";
            UiKit.SizeHint(label, flexibleWidth: 1);

            var check = UiKit.Text(content, "", 18, UiKit.TextSecondary, TextAnchor.MiddleRight, true);
            check.name = "Check";
            UiKit.SizeHint(check, preferredWidth: 110, flexibleWidth: 0);

            row.gameObject.SetActive(false);
            return row;
        }

        private static void WireLevelRewardsView(LevelRewardsBuildResult r)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("levelBadgeText").objectReferenceValue = r.levelBadgeText;
            so.FindProperty("levelXpText").objectReferenceValue = r.levelXpText;
            so.FindProperty("levelBarFill").objectReferenceValue = r.levelBarFill;
            so.FindProperty("levelBarFillImage").objectReferenceValue = r.levelBarFillImage;
            so.FindProperty("roadCountText").objectReferenceValue = r.roadCountText;
            so.FindProperty("listContent").objectReferenceValue = r.listContent;
            so.FindProperty("rowTemplate").objectReferenceValue = r.rowTemplate;
            so.FindProperty("emptyText").objectReferenceValue = r.emptyText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text BuildDetailListCell(RectTransform row, string title, Color color)
        {
            var cell = UiKit.VGroup(row, 4, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(cell, flexibleWidth: 1, flexibleHeight: 0);
            var t = UiKit.Text(cell, title, 20, color, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(t, preferredHeight: 28, flexibleHeight: 0);
            var body = UiKit.Text(cell, "", 18, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(body, flexibleHeight: 1);
            return body;
        }

        // ── ScreenRouter 와이어링 ────────────────────────────────────────────────────
        private static void WireScreens(ScreenRouter router, RectTransform overlay, ToastManager toast,
            params (ScreenRouter.ScreenId id, RectTransform root, CanvasGroup group)[] entries)
        {
            var so = new SerializedObject(router);
            var screensProp = so.FindProperty("screens");
            screensProp.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var el = screensProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").enumValueIndex = (int)entries[i].id;
                el.FindPropertyRelative("root").objectReferenceValue = entries[i].root;
                el.FindPropertyRelative("group").objectReferenceValue = entries[i].group;
            }
            so.FindProperty("overlayLayer").objectReferenceValue = overlay;
            so.FindProperty("toast").objectReferenceValue = toast;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── SerializedProperty 배열 대입 헬퍼 ────────────────────────────────────────
        private static void SetObjectArray(SerializedObject so, string propertyName, Object[] values)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[JackpotRun] 필드를 찾을 수 없음: {so.targetObject.GetType().Name}.{propertyName}");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
