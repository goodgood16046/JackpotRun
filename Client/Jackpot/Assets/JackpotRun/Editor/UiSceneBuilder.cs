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
                (ScreenRouter.ScreenId.Dex, dex.root, dex.group));

            var introSo = new SerializedObject(introRoot);
            introSo.FindProperty("router").objectReferenceValue = router;
            introSo.FindProperty("titleView").objectReferenceValue = title.view;
            introSo.FindProperty("loginView").objectReferenceValue = login.view;
            introSo.FindProperty("menuView").objectReferenceValue = menu.view;
            introSo.FindProperty("pickView").objectReferenceValue = pick.view;
            introSo.FindProperty("dexView").objectReferenceValue = dex.view;
            introSo.FindProperty("auroraRect").objectReferenceValue = auroraRect;
            introSo.ApplyModifiedPropertiesWithoutUndo();

            WireTitleView(title);
            WireMenuView(menu);
            WirePickView(pick);
            WireDexView(dex, dexDetail);

            // 순수 내비게이션 버튼(AppRoot는 DontDestroyOnLoad라 에디터 시점엔 존재하지 않으므로
            // UnityEventTools.AddPersistentListener로 직접 가리킬 수 없다 — NavButton.cs 헤더 참조).
            // Title의 시작 버튼은 닉네임 유무에 따라 Login/Menu로 갈라져야 해서 NavButton(고정 대상 1개)
            // 대신 TitleView.OnStartClicked가 직접 판정한다(설계 S12 지시, TitleView.cs 헤더 참조).
            AddNavButton(menu.startButton, NavButton.Target.Pick);
            AddNavButton(menu.dexButton, NavButton.Target.Dex);
            AddNavButton(pick.backButton, NavButton.Target.Menu);
            AddNavButton(dex.backButton, NavButton.Target.Menu);

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

            SaveScene(scene, PlayScenePath);
        }

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
            public RectTransform expBarFill;
            public Image expBarFillImage;
            public Outline hudOutline;
            public Image[] unluckyPips;
            public CanvasGroup bossBannerGroup;
            public RectTransform bossBannerRect;
            public Text bossBannerText;

            public RectTransform reelSectionRoot;
            public RectTransform reelRow;
            public RectTransform cellTemplate;
            public (string id, Sprite sprite)[] symbolSprites;
            public CanvasGroup flashOverlay;
            public CanvasGroup jackpotBannerGroup;
            public RectTransform jackpotBannerRect;

            public Text resultLineText; // 릴과 노트 사이 "스테이지 정보 영역" — 획득 요약 큰 텍스트

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
            var toastRoot = UiKit.Panel(canvasRoot, "Toast", UiKit.PanelBg, UiSpriteGen.Load("chip_r999"));
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
            UiKit.SizeHint(btnSlot, preferredHeight: 120, flexibleHeight: 0);
            var pillSprite = UiSpriteGen.Load("w_pill");
            result.startButton = UiKit.Button(btnSlot, "▶ 탭하여 시작", new Vector2(460f, 120f), UiKit.Accent, UiKit.Ink, null, pillSprite);
            var startRt = result.startButton.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.5f, 0.5f);
            startRt.anchorMax = new Vector2(0.5f, 0.5f);
            startRt.pivot = new Vector2(0.5f, 0.5f);
            startRt.sizeDelta = new Vector2(460f, 120f);
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

            var footerSpacer = UiKit.Panel(col, "FooterSpacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(footerSpacer, flexibleHeight: 1);

            return result;
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
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── PickView 화면 — S10: public/jackpotpick/index.html DOM 순서 그대로 재구성 ──────
        // head(타이틀+lead+who) → tabs → recos → toolbar(chips+sort) → sechead → grid → summary.
        // 뒤로가기 버튼은 웹에 없는 앱 전용 내비게이션이라 head 위 별도 소형 행으로 유지한다.
        private static PickBuildResult BuildPickScreen(Transform canvasRoot)
        {
            var result = new PickBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");
            var pillSprite = UiSpriteGen.Load("chip_r999");
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
            var chip = UiKit.Panel(parent, "ChipTemplate", UiKit.Card, UiSpriteGen.Load("chip_r999"));
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
            // 대신 IconSlot/Info 둘 다 실제 RectTransform.sizeDelta를 83으로 직접 고정하고, Top의
            // childAlignment(HGroup 기본 MiddleLeft)로 그 83짜리 두 블록을 행 높이(92) 안에서 세로
            // 중앙 정렬한다.
            var top = UiKit.HGroup(body, 18, new RectOffset(0, 0, 0, 0), true, false);
            top.name = "Top";
            UiKit.SizeHint(top, preferredHeight: 92, flexibleHeight: 0);

            var iconSlot = UiKit.Panel(top, "IconSlot", UiKit.Hex("#0E1019"), r11);
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
            info.sizeDelta = new Vector2(info.sizeDelta.x, 83f); // 아이콘과 같은 높이로 고정 — 내부 정렬 기준선 통일.
            info.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            var nameRow = UiKit.HGroup(info, 8, new RectOffset(0, 0, 0, 0), true, true);
            nameRow.name = "NameRow";
            UiKit.SizeHint(nameRow, preferredHeight: 34, flexibleHeight: 0);
            var nameText = UiKit.Text(nameRow, "", 25, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            nameText.name = "Name";
            UiKit.SizeHint(nameText, flexibleWidth: 1, flexibleHeight: 0);
            var (badgeRoot, badgeBg, badgeLabel) = BuildAutoPill(nameRow, "Badge", r7, 17, new RectOffset(11, 11, 3, 3), true);
            badgeLabel.color = UiKit.Hex("#15161F"); // pick.css .b-diff{color:#15161f} 고정 어두운 글자

            var role = UiKit.Text(info, "", 18, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            role.name = "Role";
            UiKit.SizeHint(role, preferredHeight: 24, flexibleHeight: 0);

            // ── Eff: 효과 박스(.jc-eff) ──
            var eff = UiKit.Panel(body, "Eff", new Color(1f, 1f, 1f, 0.035f), r9);
            UiKit.SizeHint(eff, preferredHeight: 52, flexibleHeight: 0);
            var effText = UiKit.Text(eff, "", 20, UiKit.Hex("#CDD3E6"), TextAnchor.UpperLeft);
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
            var pill999 = UiSpriteGen.Load("chip_r999");

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
            // 행 높이 계약(S9 육안 검수 반영): 릴을 화면 상단부에 붙이고 죽은 여백을 없앤다.
            // Hud 210 · 릴 위 여백 60(고정) · ReelSection 260 · StageInfo 120 · NotesFeed(flex 1,
            // 잔여 전부 — 스핀 노트가 쌓일수록 채워진다) · Controls 300.
            // 이전 구성은 릴 위/아래 flex 스페이서 2개가 750px를 반씩 먹어 화면 절반이 빈 채로 보였다.

            BuildRunHud(col, result);
            AddFixedSpacer(col, 60f);
            BuildRunReel(col, result);
            BuildRunStageInfo(col, result);
            BuildRunNotesFeed(col, result);
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

            result.view = root.gameObject.AddComponent<UI2.RunView>();
            return result;
        }

        // HUD와 릴 사이의 고정 여백(S9) — flex 스페이서는 잔여 공간을 다 먹어 화면이 비어 보였다.
        private static void AddFixedSpacer(RectTransform col, float height)
        {
            var spacer = UiKit.Panel(col, "Spacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(spacer, preferredHeight: height, flexibleHeight: 0);
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
            result.cursesText = UiKit.Text(topRow, "", 20, UiKit.Bad, TextAnchor.MiddleRight, true);
            UiKit.SizeHint(result.cursesText, preferredWidth: 180, flexibleHeight: 0);

            var barBg = UiKit.Panel(hudCol, "ExpBarBg", UiKit.Hex("#2A3048"), UiSpriteGen.Load("bar_bg_r12"));
            UiKit.SizeHint(barBg, preferredHeight: 36, flexibleHeight: 0);
            result.expBarFill = UiKit.Panel(barBg, "ExpBarFill", UiKit.Good, UiSpriteGen.Load("bar_fill_r12"));
            UiKit.SetAnchors(result.expBarFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            result.expBarFillImage = result.expBarFill.GetComponent<Image>();
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
            for (int i = 0; i < 5; i++)
            {
                var pip = UiKit.Panel(gaugeRow, "Pip_" + i, UiKit.Card, UiSpriteGen.Load("chip_r999"));
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
            UiKit.SizeHint(section, preferredHeight: 260, flexibleHeight: 0);
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

        // 릴 셀 템플릿 — S8 항목⑤: 이모지 Text 오버레이("Emoji")를 완전히 제거했다(심볼은 UiSpriteGen이
        // 그린 도형 스프라이트만으로 표현). 자식 경로 계약(ReelView.cs): "Icon"(Image)/"Tag"(Text) + Outline(글로우).
        private static RectTransform BuildReelCellTemplate(Transform parent)
        {
            const float cellSize = 196f; // (1080 - 패딩48 - 스페이싱48)/5 ≈ 196.8의 근사 정사각.
            var cell = UiKit.Panel(parent, "CellTemplate", UiKit.Card, UiSpriteGen.Load("cell_inset"));
            UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: cellSize, flexibleHeight: 0);
            UiKit.AddGlowOutline(cell.gameObject, UiKit.Accent, 3f);

            var icon = UiKit.Image(cell, null, Color.white);
            icon.name = "Icon";
            UiKit.Fill(icon.rectTransform);

            var tag = UiKit.Text(cell, "", 16, UiKit.Accent, TextAnchor.UpperRight, true);
            tag.name = "Tag";
            UiKit.SetAnchors(tag.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -28), new Vector2(-6, -4));

            cell.gameObject.SetActive(false);
            return cell;
        }

        // S8 항목⑥: 고정 120(기존 flexibleHeight:1에서 축소 — 잔여 공간은 이제 릴 위/아래 스페이서가
        // 가져간다). 스핀 획득 요약(EXP/점수/코인)을 중앙 표시 — 이모지 대신 한글 라벨만 사용.
        private static void BuildRunStageInfo(RectTransform col, RunBuildResult result)
        {
            var panel = UiKit.Panel(col, "StageInfo", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(panel, preferredHeight: 120, flexibleHeight: 0);
            var inner = UiKit.VGroup(panel, 0, new RectOffset(24, 24, 8, 8), true, true);
            UiKit.Fill(inner);
            inner.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            result.resultLineText = UiKit.Text(inner, "", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(result.resultLineText, flexibleHeight: 1);
        }

        // Fable 육안 검수 지시(2026-07-31): 최근 6줄 고정 표시(280, flex 아님), 20pt, 좌우 패딩 24,
        // 최신 줄이 위로, 각 줄에 은은한 배경(카드색 알파 40%). 단일 Text 블록 대신 행 템플릿으로 재구성.
        private static void BuildRunNotesFeed(RectTransform col, RunBuildResult result)
        {
            // S9: 잔여 공간을 노트 피드가 흡수하되(고정 280이면 죽은 여백이 남는다), 패널 배경은
            // 투명하게 둔다 — 줄마다 자체 배경이 있어서, 빈 영역이 "빈 상자"로 보이지 않는다.
            var panel = UiKit.Panel(col, "NotesFeed", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(panel, preferredHeight: 280, flexibleHeight: 1);
            result.notesRoot = panel;
            var inner = UiKit.VGroup(panel, 0, new RectOffset(0, 0, 10, 10), true, true);
            UiKit.Fill(inner);

            var scroll = UiKit.Scroll(inner, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 24, 0, 6);

            result.notesRowsContent = rowsContent;
            result.notesRowTemplate = BuildNotesRowTemplate(rowsContent);
        }

        // 자식 경로 계약(NotesFeed.cs): "Label"(Text).
        private static RectTransform BuildNotesRowTemplate(Transform parent)
        {
            var bg = UiKit.Card;
            bg.a = 0.4f;
            var row = UiKit.Panel(parent, "NoteRowTemplate", bg);
            UiKit.SizeHint(row, preferredHeight: 44, flexibleHeight: 0);
            var label = UiKit.Text(row, "", 20, UiKit.TextPrimary, TextAnchor.MiddleLeft);
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

            // 특수모드 4버튼(순서: 집중/올인/기도/막판 — RunView.ModeOrder와 일치해야 함). 비용은 상수라
            // 빌드 시점에 라벨을 굽는다(사용가능 조건은 엔진 거부 → 토스트로 안내, 여기서 사전 비활성화 안 함).
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
            so.FindProperty("resultLineText").objectReferenceValue = r.resultLineText;
            so.FindProperty("notesFeed").objectReferenceValue = WireNotesFeed(r);
            so.FindProperty("controlsGroup").objectReferenceValue = r.controlsGroup;
            SetObjectArray(so, "modeButtons", r.modeButtons);
            so.FindProperty("spinButton").objectReferenceValue = r.spinButton;
            so.FindProperty("bagButton").objectReferenceValue = r.bagButton;
            so.FindProperty("bagButtonLabel").objectReferenceValue = r.bagButtonLabel;
            so.FindProperty("deviceRow").objectReferenceValue = r.deviceRow;
            so.FindProperty("deviceButtonTemplate").objectReferenceValue = r.deviceButtonTemplate;
            so.FindProperty("nodePanel").objectReferenceValue = overlay.nodePanel;
            so.FindProperty("perkOfferPanel").objectReferenceValue = overlay.perkOfferPanel;
            so.FindProperty("shopPanel").objectReferenceValue = overlay.shopPanel;
            so.FindProperty("postSpinPanel").objectReferenceValue = overlay.postSpinPanel;
            so.FindProperty("gameOverPanel").objectReferenceValue = overlay.gameOverPanel;
            so.FindProperty("bagPopup").objectReferenceValue = overlay.bagPopup;
            so.FindProperty("manipPickPopup").objectReferenceValue = overlay.manipPickPopup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UI2.HudView WireHudView(RunBuildResult r)
        {
            var view = r.hudRoot.gameObject.AddComponent<UI2.HudView>();
            var so = new SerializedObject(view);
            so.FindProperty("stageText").objectReferenceValue = r.stageText;
            so.FindProperty("cursesText").objectReferenceValue = r.cursesText;
            so.FindProperty("expBarFill").objectReferenceValue = r.expBarFill;
            so.FindProperty("expBarFillImage").objectReferenceValue = r.expBarFillImage;
            so.FindProperty("expBarText").objectReferenceValue = r.expBarText;
            so.FindProperty("spinsText").objectReferenceValue = r.spinsText;
            so.FindProperty("coinsText").objectReferenceValue = r.coinsText;
            so.FindProperty("scoreText").objectReferenceValue = r.scoreText;
            so.FindProperty("hudOutline").objectReferenceValue = r.hudOutline;
            SetObjectArray(so, "unluckyPips", r.unluckyPips);
            so.FindProperty("bossBannerGroup").objectReferenceValue = r.bossBannerGroup;
            so.FindProperty("bossBannerRect").objectReferenceValue = r.bossBannerRect;
            so.FindProperty("bossBannerText").objectReferenceValue = r.bossBannerText;
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
            };
        }

        // ── NodePanel ────────────────────────────────────────────────────────────────
        private static UI2.NodePanel BuildNodePanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "NodePanel", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);

            var bannerPanel = UiKit.Panel(scrim, "Banner", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            bannerPanel.anchorMin = bannerPanel.anchorMax = new Vector2(0.5f, 1f);
            bannerPanel.pivot = new Vector2(0.5f, 1f);
            bannerPanel.sizeDelta = new Vector2(860f, 230f);
            bannerPanel.anchoredPosition = new Vector2(0f, -140f);
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

            var cardRect = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(940f, 1300f);
            var cardCol = UiKit.VGroup(cardRect, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(cardCol);

            var title = UiKit.Text(cardCol, "다음 노드를 선택하세요", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(title, preferredHeight: 64, flexibleHeight: 0);

            var scroll = UiKit.Scroll(cardCol, out var cardsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(cardsContent, 28, 12, 16);
            var cardTemplate = BuildNodeCardTemplate(cardsContent);

            var view = scrim.gameObject.AddComponent<UI2.NodePanel>();
            var so = new SerializedObject(view);
            so.FindProperty("bannerGroup").objectReferenceValue = bannerGroup;
            so.FindProperty("bannerRect").objectReferenceValue = bannerPanel;
            so.FindProperty("bannerGradeText").objectReferenceValue = gradeText;
            so.FindProperty("bannerScoreText").objectReferenceValue = scoreText;
            so.FindProperty("bannerSubText").objectReferenceValue = subText;
            so.FindProperty("cardRect").objectReferenceValue = cardRect;
            so.FindProperty("cardsContent").objectReferenceValue = cardsContent;
            so.FindProperty("cardTemplate").objectReferenceValue = cardTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(NodePanel.cs): "Head"(Text)/"Body"(Text).
        private static RectTransform BuildNodeCardTemplate(Transform parent)
        {
            var card = UiKit.Panel(parent, "NodeCardTemplate", UiKit.Card, UiSpriteGen.Load("card_grad"));
            UiKit.SizeHint(card, preferredHeight: 210, flexibleHeight: 0);
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

            card.gameObject.SetActive(false);
            return card;
        }

        // ── PerkOfferPanel ───────────────────────────────────────────────────────────
        private static UI2.PerkOfferPanel BuildPerkOfferPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "PerkOfferPanel", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(980f, 1560f);
            var col = UiKit.VGroup(card, 8, new RectOffset(0, 0, 20, 20), true, true);
            UiKit.Fill(col);

            var titleText = UiKit.Text(col, "", 30, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 48, flexibleHeight: 0);
            var bannerText = UiKit.Text(col, "", 19, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(bannerText, preferredHeight: 30, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var cardsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(cardsContent, 28, 16, 14);
            var cardTemplate = BuildPerkCardTemplate(cardsContent);

            var retakeButton = UiKit.Button(col, "", new Vector2(0, 78), UiKit.Blue, UiKit.Bg, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(retakeButton, preferredHeight: 78, flexibleHeight: 0);
            var retakeLabel = retakeButton.GetComponentInChildren<Text>();

            var view = scrim.gameObject.AddComponent<UI2.PerkOfferPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("bannerText").objectReferenceValue = bannerText;
            so.FindProperty("cardsContent").objectReferenceValue = cardsContent;
            so.FindProperty("cardTemplate").objectReferenceValue = cardTemplate;
            so.FindProperty("retakeButton").objectReferenceValue = retakeButton;
            so.FindProperty("retakeButtonLabel").objectReferenceValue = retakeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(PerkOfferPanel.cs) — Transform.Find는 "/" 없이는 직계 자식만 찾으므로 중간
        // 레이아웃 컨테이너에도 전부 이름을 박아 전체 경로로 찾는다:
        //   "Content/TopRow/IconSlot/Icon"·"Content/TopRow/IconSlot/IconEmoji"
        //   "Content/TopRow/NameCol/Name"·".../Tier"·".../Badges"
        //   "Content/Desc", "Content/ButtonRow/PickButton"·"Content/ButtonRow/HoldButton"
        // 카드 루트에 Outline(시너지 보라 테두리).
        private static RectTransform BuildPerkCardTemplate(Transform parent)
        {
            var card = UiKit.Panel(parent, "PerkCardTemplate", UiKit.Card, UiSpriteGen.Load("card_grad"));
            UiKit.SizeHint(card, preferredHeight: 340, flexibleHeight: 0);
            UiKit.AddGlowOutline(card.gameObject, UiKit.Purple, 3f);

            var col = UiKit.VGroup(card, 8, new RectOffset(20, 20, 16, 16), true, true);
            col.name = "Content";
            UiKit.Fill(col);

            var topRow = UiKit.HGroup(col, 14, new RectOffset(0, 0, 0, 0), true, true);
            topRow.name = "TopRow";
            UiKit.SizeHint(topRow, preferredHeight: 92, flexibleHeight: 0);
            BuildIconSlot(topRow, 80, 46);

            var nameCol = UiKit.VGroup(topRow, 2, new RectOffset(0, 0, 0, 0), true, true);
            nameCol.name = "NameCol";
            UiKit.SizeHint(nameCol, flexibleWidth: 1, flexibleHeight: 0);
            var name = UiKit.Text(nameCol, "", 25, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            name.name = "Name";
            UiKit.SizeHint(name, preferredHeight: 34, flexibleHeight: 0);
            var tier = UiKit.Text(nameCol, "", 16, UiKit.TextSecondary, TextAnchor.MiddleLeft);
            tier.name = "Tier";
            UiKit.SizeHint(tier, preferredHeight: 22, flexibleHeight: 0);
            var badges = UiKit.Text(nameCol, "", 16, UiKit.Purple, TextAnchor.MiddleLeft, true);
            badges.name = "Badges";
            UiKit.SizeHint(badges, preferredHeight: 22, flexibleHeight: 0);

            var desc = UiKit.Text(col, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            desc.name = "Desc";
            UiKit.SizeHint(desc, flexibleHeight: 1);

            var btnRow = UiKit.HGroup(col, 10, new RectOffset(0, 0, 0, 0), true, true);
            btnRow.name = "ButtonRow";
            UiKit.SizeHint(btnRow, preferredHeight: 66, flexibleHeight: 0);
            var pickBtn = UiKit.Button(btnRow, "선택", new Vector2(0, 66), UiKit.Accent, UiKit.Bg, null, UiSpriteGen.Load("panel_r24"));
            pickBtn.name = "PickButton";
            UiKit.SizeHint(pickBtn, flexibleWidth: 1, preferredHeight: 66, flexibleHeight: 0);
            // S8 항목⑤: 🗂️(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var holdBtn = UiKit.Button(btnRow, "보류", new Vector2(0, 66), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
            holdBtn.name = "HoldButton";
            UiKit.SizeHint(holdBtn, flexibleWidth: 1, preferredHeight: 66, flexibleHeight: 0);

            card.gameObject.SetActive(false);
            return card;
        }

        // ── ShopPanel ────────────────────────────────────────────────────────────────
        private static UI2.ShopPanel BuildShopPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "ShopPanel", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(980f, 1600f);
            var col = UiKit.VGroup(card, 10, new RectOffset(0, 0, 20, 20), true, true);
            UiKit.Fill(col);

            var titleText = UiKit.Text(col, "", 28, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 54, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 24, 8, 12);
            var emptyText = UiKit.Text(rowsContent, "오퍼가 없습니다.", 20, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(emptyText, preferredHeight: 60, flexibleHeight: 0);
            var rowTemplate = BuildShopRowTemplate(rowsContent);

            var btnRow = UiKit.HGroup(col, 14, new RectOffset(24, 24, 4, 4), true, true);
            UiKit.SizeHint(btnRow, preferredHeight: 90, flexibleHeight: 0);
            var rerollButton = UiKit.Button(btnRow, "", new Vector2(0, 90), UiKit.Blue, UiKit.Bg, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(rerollButton, flexibleWidth: 1, preferredHeight: 90, flexibleHeight: 0);
            var rerollLabel = rerollButton.GetComponentInChildren<Text>();
            var leaveButton = UiKit.Button(btnRow, "나가기", new Vector2(0, 90), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
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
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(ShopPanel.cs): IconSlot/Icon·IconSlot/IconEmoji, "Name"/"Desc"(Text),
        // "PriceButton"(Button)+"PriceButton/PriceLabel"(Text).
        private static RectTransform BuildShopRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "ShopRowTemplate", UiKit.Card, UiSpriteGen.Load("card_r16"));
            UiKit.SizeHint(row, preferredHeight: 140, flexibleHeight: 0);
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
            priceImg.sprite = UiSpriteGen.Load("chip_r999");
            priceImg.type = Image.Type.Sliced;
            priceImg.color = UiKit.Accent;
            priceGo.GetComponent<Button>().targetGraphic = priceImg;
            UiKit.SizeHint(priceGo.GetComponent<Button>(), preferredWidth: 150, preferredHeight: 84, flexibleWidth: 0, flexibleHeight: 0);
            var priceLabel = UiKit.Text(priceRt, "", 22, UiKit.Bg, TextAnchor.MiddleCenter, true);
            priceLabel.name = "PriceLabel";
            UiKit.Fill(priceLabel.rectTransform);

            row.gameObject.SetActive(false);
            return row;
        }

        // ── PostSpinPanel ────────────────────────────────────────────────────────────
        private static UI2.PostSpinPanel BuildPostSpinPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "PostSpinPanel", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var dimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(900f, 900f);
            var col = UiKit.VGroup(card, 18, new RectOffset(28, 28, 26, 26), true, true);
            UiKit.Fill(col);

            // S8 항목⑤: 💥(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var head = UiKit.Text(col, "클리어 실패", 32, UiKit.Bad, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(head, preferredHeight: 46, flexibleHeight: 0);
            var subText = UiKit.Text(col, "", 19, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(subText, preferredHeight: 56, flexibleHeight: 0);

            var scroll = UiKit.Scroll(col, out var manipButtonsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(manipButtonsContent, 0, 0, 14);
            var manipTemplate = BuildLabeledButtonTemplateNamed(manipButtonsContent, UiSpriteGen.Load("panel_r24"), "ManipButtonTemplate", 92);

            var giveUpButton = UiKit.Button(col, "포기", new Vector2(0, 84), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(giveUpButton, preferredHeight: 84, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.PostSpinPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
            so.FindProperty("subText").objectReferenceValue = subText;
            so.FindProperty("manipButtonsContent").objectReferenceValue = manipButtonsContent;
            so.FindProperty("manipButtonTemplate").objectReferenceValue = manipTemplate;
            so.FindProperty("giveUpButton").objectReferenceValue = giveUpButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── GameOverPanel ────────────────────────────────────────────────────────────
        private static UI2.GameOverPanel BuildGameOverPanel(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "GameOverPanel", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var dimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
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

            var menuButton = UiKit.Button(outerCol, "메뉴로", new Vector2(0, 96), UiKit.Accent, UiKit.Bg, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(menuButton, preferredHeight: 96, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.GameOverPanel>();
            var so = new SerializedObject(view);
            so.FindProperty("dimGroup").objectReferenceValue = dimGroup;
            so.FindProperty("cardRect").objectReferenceValue = card;
            so.FindProperty("titleScoreText").objectReferenceValue = titleScoreText;
            so.FindProperty("finalScoreText").objectReferenceValue = finalScoreText;
            so.FindProperty("stageReachedText").objectReferenceValue = stageReachedText;
            so.FindProperty("recordsText").objectReferenceValue = recordsText;
            so.FindProperty("achHeaderRow").objectReferenceValue = achHeaderRow;
            so.FindProperty("achHeaderText").objectReferenceValue = achHeaderText;
            so.FindProperty("achContent").objectReferenceValue = achContent;
            so.FindProperty("achRowTemplate").objectReferenceValue = achRowTemplate;
            so.FindProperty("achTotalText").objectReferenceValue = achTotalText;
            so.FindProperty("menuButton").objectReferenceValue = menuButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(GameOverPanel.cs): "Label"(Text) — 단순 한 줄 업적 행.
        private static RectTransform BuildAchRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "AchRowTemplate", UiKit.Card, UiSpriteGen.Load("card_r16"));
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
            var scrim = UiKit.Panel(overlay, "BagPopup", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(900f, 1200f);
            var cardBlocker = card.gameObject.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;
            var outerCol = UiKit.VGroup(card, 10, new RectOffset(0, 0, 20, 20), true, true);
            UiKit.Fill(outerCol);

            var titleText = UiKit.Text(outerCol, "", 26, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 52, flexibleHeight: 0);

            var scroll = UiKit.Scroll(outerCol, out var rowsContent, vertical: true);
            UiKit.SizeHint(scroll, flexibleHeight: 1);
            SetupStackContent(rowsContent, 24, 8, 12);
            var emptyText = UiKit.Text(rowsContent, "가방이 비어 있습니다.", 20, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(emptyText, preferredHeight: 60, flexibleHeight: 0);
            var rowTemplate = BuildBagRowTemplate(rowsContent);

            var closeButton = UiKit.Button(outerCol, "닫기", new Vector2(0, 80), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(closeButton, preferredHeight: 80, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.BagPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = card;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("rowsContent").objectReferenceValue = rowsContent;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("emptyText").objectReferenceValue = emptyText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 자식 경로 계약(BagPopup.cs): IconSlot/Icon·IconSlot/IconEmoji, "Name"/"Desc"(Text), "UseButton"(Button).
        private static RectTransform BuildBagRowTemplate(Transform parent)
        {
            var row = UiKit.Panel(parent, "BagRowTemplate", UiKit.Card, UiSpriteGen.Load("card_r16"));
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

            var useBtn = UiKit.Button(inner, "사용", new Vector2(120, 76), UiKit.Accent, UiKit.Bg, null, UiSpriteGen.Load("panel_r24"));
            useBtn.name = "UseButton";
            UiKit.SizeHint(useBtn, preferredWidth: 120, preferredHeight: 76, flexibleWidth: 0, flexibleHeight: 0);

            row.gameObject.SetActive(false);
            return row;
        }

        // ── ManipPickPopup ───────────────────────────────────────────────────────────
        private static UI2.ManipPickPopup BuildManipPickPopup(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "ManipPickPopup", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(860f, 680f);
            var cardBlocker = card.gameObject.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;
            var col = UiKit.VGroup(card, 16, new RectOffset(28, 28, 26, 26), true, true);
            UiKit.Fill(col);

            var headText = UiKit.Text(col, "", 26, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(headText, preferredHeight: 40, flexibleHeight: 0);
            var descText = UiKit.Text(col, "", 18, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(descText, preferredHeight: 76, flexibleHeight: 0);

            var cellsContent = UiKit.HGroup(col, 10, new RectOffset(0, 0, 10, 10), true, true);
            UiKit.SizeHint(cellsContent, preferredHeight: 100, flexibleHeight: 0);
            var cellTemplate = BuildLabeledButtonTemplateNamed(cellsContent, UiSpriteGen.Load("panel_r24"), "CellButtonTemplate", 96);

            var spacer = UiKit.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiKit.SizeHint(spacer, flexibleHeight: 1);

            var cancelButton = UiKit.Button(col, "취소", new Vector2(0, 76), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(cancelButton, preferredHeight: 76, flexibleHeight: 0);

            var view = scrim.gameObject.AddComponent<UI2.ManipPickPopup>();
            var so = new SerializedObject(view);
            so.FindProperty("scrimButton").objectReferenceValue = scrimButton;
            so.FindProperty("cardRect").objectReferenceValue = card;
            so.FindProperty("headText").objectReferenceValue = headText;
            so.FindProperty("descText").objectReferenceValue = descText;
            so.FindProperty("cellsContent").objectReferenceValue = cellsContent;
            so.FindProperty("cellButtonTemplate").objectReferenceValue = cellTemplate;
            so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 라벨 1개짜리 버튼 템플릿(공용) — 자식 경로 계약: "Label"(Text). name/height를 호출측이 지정한다.
        private static RectTransform BuildLabeledButtonTemplateNamed(Transform parent, Sprite panelSprite, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = UiKit.Blue;
            if (panelSprite != null) { img.sprite = panelSprite; img.type = Image.Type.Sliced; }
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: height, flexibleHeight: 0);

            var label = UiKit.Text(rt, "", 22, UiKit.Bg, TextAnchor.MiddleCenter, true);
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
        private static DexBuildResult BuildDexScreen(Transform canvasRoot, UI2.DexDetailPopup detailPopup)
        {
            var result = new DexBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

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
            result.backButton = UiKit.Button(header, "← 메뉴", new Vector2(160, 70), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 160, preferredHeight: 70, flexibleWidth: 0, flexibleHeight: 0);

            // 통계 4타일 — 96. S8 항목⑤: astral 이모지(🏆🧗🔁📈)는 렌더링되지 않는다 — 한글 라벨만 사용.
            var statsRow = UiKit.HGroup(col, 12, new RectOffset(24, 24, 4, 12), true, true);
            UiKit.SizeHint(statsRow, preferredHeight: 96, flexibleHeight: 0);
            result.statBestScoreText = BuildStatTile(statsRow, "최고점수");
            result.statBestStageText = BuildStatTile(statsRow, "최고 스테이지");
            result.statRunsText = BuildStatTile(statsRow, "런");
            result.statTotalScoreText = BuildStatTile(statsRow, "통산 점수");

            // 카테고리 탭 — 96, JackpotCatalog.CategoryOrder 8종 고정(제목은 JackpotCatalog.CategoryTitle,
            // S8에서 이모지 제거됨).
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
                tabImg.sprite = panelSprite;
                tabImg.type = Image.Type.Sliced;
                tabImg.color = UiKit.PanelBg;
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

        private static Text BuildStatTile(RectTransform row, string label)
        {
            var cell = UiKit.Panel(row, "Stat", UiKit.PanelBg);
            UiKit.SizeHint(cell, flexibleWidth: 1, preferredHeight: 96, flexibleHeight: 0);
            var col = UiKit.VGroup(cell, 2, new RectOffset(8, 8, 10, 10), true, true);
            UiKit.Fill(col);
            var l = UiKit.Text(col, label, 15, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(l, preferredHeight: 22, flexibleHeight: 0);
            var v = UiKit.Text(col, "-", 25, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(v, flexibleHeight: 1);
            return v;
        }

        // S10 — jackpotdex/style.css .card: 가로 배치(아이콘 좌 48px→77 + 이름/설명 우). 자식 경로
        // 계약(DexView.cs): "Content/IconSlot/Icon"·"IconSlot/IconEmoji", "Content/Name"/"Desc"/"Sub",
        // "Lock"(GameObject)+"Lock/Hint"(Text).
        private static RectTransform BuildDexCardTemplate(Transform parent)
        {
            var r11 = UiSpriteGen.Load("rrect_r11");
            var card = UiKit.Panel(parent, "DexCardTemplate", UiKit.Panel2, UiSpriteGen.Load("card_grad_r15"));
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card.GetComponent<Image>();
            card.gameObject.AddComponent<PressFx>();

            // "Content" 이름 계약(DexView.cs) — Transform.Find는 직계 자식만 찾으므로 Icon/Name/Desc/Sub는
            // "Content/..."로 찾는다(NodePanel/PerkOfferPanel/ShopPanel과 동일 이유).
            var row = UiKit.HGroup(card, 14, new RectOffset(14, 14, 12, 12), true, true);
            row.name = "Content";
            UiKit.Fill(row);

            var iconSlot = UiKit.Panel(row, "IconSlot", UiKit.Hex("#0E1019"), r11);
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
        private static UI2.DexDetailPopup BuildDexDetailPopup(Transform overlay)
        {
            var scrim = UiKit.Panel(overlay, "DexDetailPopup", new Color(0f, 0f, 0f, 0.66f));
            UiKit.Fill(scrim);
            scrim.gameObject.SetActive(false);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = UiKit.Panel(scrim, "Card", UiKit.PanelBg, UiSpriteGen.Load("panel_r24"));
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

            var closeButton = UiKit.Button(cardCol, "닫기", new Vector2(0, 84), UiKit.Hex("#2A3048"), UiKit.TextPrimary, null, UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(closeButton, preferredHeight: 84, flexibleHeight: 0);

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
