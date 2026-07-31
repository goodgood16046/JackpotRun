using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JackpotRun.UI2;

namespace JackpotRun.EditorTools
{
    // 씬+화면 생성기 — ENGINE_PORT_DESIGN.md S7 "SceneBuilder 사양". 메뉴 "JackpotRun/Build UI Scene":
    //   ① UiSpriteGen 실행(없는 것만) ② 씬 생성/덮어쓰기(확인 후) ③ Canvas+EventSystem+AppRoot+
    //      ScreenRouter+화면 4종+Overlay 구성, 모든 [SerializeField] 와이어링 ④ Build Settings 씬 목록을
    //      JackpotRun.unity 단독으로 설정 ⑤ 저장.
    // 반복 실행 안전 — 매번 완전히 새 인메모리 씬을 만들어 같은 경로에 덮어쓰므로(기존 씬을 열어
    // 이어붙이지 않음) 몇 번을 다시 실행해도 결과가 결정론적이다.
    //
    // [이번 슬라이스 범위] MenuView/PickView는 실제 컴포넌트와 완전히 와이어링한다. RunScreen/DexScreen은
    // 아직 뷰가 없다(S7b) — 여기서는 자리표시 화면(배경+안내문구+메뉴로 버튼)만 만들고 화면 전환
    // 인프라(ScreenRouter 등록)만 연결한다. S7b가 이 자리에 RunView/DexView를 추가로 붙인다.
    public static class UiSceneBuilder
    {
        private const string ScenesFolder = "Assets/JackpotRun/Scenes";
        private const string ScenePath = ScenesFolder + "/JackpotRun.unity";

        // MenuView.SlotWidth와 반드시 일치해야 한다(캐러셀 슬라이드 폭 계약).
        private const float CarouselSlotWidth = 560f;
        private static readonly string[] CarouselCharIds = { "novice", "gambler", "crowncol" };

        [MenuItem("JackpotRun/Build UI Scene")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                "JackpotRun UI 씬 빌드",
                $"{ScenePath} 를 (다시) 생성합니다. 기존 씬 내용은 완전히 대체됩니다. 계속할까요?",
                "생성", "취소"))
            {
                return;
            }
            BuildUnattended();
        }

        /// <summary>확인 다이얼로그 없이 빌드 — MCP/CI 등 비대화형 경로용.</summary>
        public static void BuildUnattended()
        {
            UiSpriteGen.GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEventSystem();
            var canvasRoot = BuildCanvas();

            var menu = BuildMenuScreen(canvasRoot);
            var pick = BuildPickScreen(canvasRoot);
            var run = BuildPlaceholderScreen(canvasRoot, "RunScreen", "🎰 런 화면 — S7b에서 이어 만듭니다");
            var dex = BuildPlaceholderScreen(canvasRoot, "DexScreen", "📖 도감 화면 — S7b에서 이어 만듭니다");
            var overlay = BuildOverlayLayer(canvasRoot);
            var toast = BuildToast(canvasRoot);

            var appRootGo = new GameObject("AppRoot");
            var router = appRootGo.AddComponent<ScreenRouter>();
            var appRoot = appRootGo.AddComponent<AppRoot>();

            WireScreens(router, overlay, toast,
                (ScreenRouter.ScreenId.Menu, menu.root, menu.group),
                (ScreenRouter.ScreenId.Pick, pick.root, pick.group),
                (ScreenRouter.ScreenId.Run, run.root, run.group),
                (ScreenRouter.ScreenId.Dex, dex.root, dex.group));

            var appRootSo = new SerializedObject(appRoot);
            appRootSo.FindProperty("router").objectReferenceValue = router;
            appRootSo.FindProperty("menuView").objectReferenceValue = menu.view;
            appRootSo.FindProperty("pickView").objectReferenceValue = pick.view;
            appRootSo.ApplyModifiedPropertiesWithoutUndo();

            WireMenuView(menu, appRoot);
            WirePickView(pick, appRoot);

            // 화면 간 내비게이션은 AppRoot의 공개 메서드에 UnityEvent 퍼시스턴트 리스너로 직접 연결한다
            // (씬에 구워지므로 뷰가 별도로 이 참조를 들고 있을 필요가 없다).
            UnityEventTools.AddPersistentListener(menu.startButton.onClick, appRoot.ShowPick);
            UnityEventTools.AddPersistentListener(menu.dexButton.onClick, appRoot.ShowDex);
            UnityEventTools.AddPersistentListener(pick.backButton.onClick, appRoot.ShowMenu);
            UnityEventTools.AddPersistentListener(run.backButton.onClick, appRoot.EndRun);
            UnityEventTools.AddPersistentListener(dex.backButton.onClick, appRoot.ShowMenu);

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/JackpotRun", "Scenes");

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[JackpotRun] 씬 저장 실패: " + ScenePath);
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[JackpotRun] UI 씬 빌드 완료 — " + ScenePath);
        }

        // ── 결과 전달용 컨테이너(생성 직후 값을 여러 곳으로 넘기기 위함, 씬에는 남지 않는다) ──────
        private sealed class MenuBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public MenuView view;
            public Button startButton;
            public Button dexButton;
            public Text profileSummaryText;
            public RectTransform carouselTrack;
        }

        private sealed class PickBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public PickView view;
            public Button backButton;
            public Button[] recoButtons;
            public Button[] tabButtons;
            public Image[] tabButtonImages;
            public Text[] tabLabelTexts;
            public RectTransform chipsContent;
            public RectTransform chipTemplate;
            public Button[] sortButtons;
            public Image[] sortButtonImages;
            public RectTransform gridContent;
            public CanvasGroup gridCanvasGroup;
            public RectTransform cardTemplate;
            public Text comboText;
            public Text gradeText;
            public Text ceilingValueText;
            public Text stabilityValueText;
            public Text difficultyValueText;
            public Text difficultyLabelText;
            public Text blurbText;
            public Text prosText;
            public Text consText;
            public Text buildText;
            public Button startButton;
        }

        private sealed class PlaceholderBuildResult
        {
            public RectTransform root;
            public CanvasGroup group;
            public Button backButton;
        }

        // ── 씬 골격 ──────────────────────────────────────────────────────────────────
        private static void BuildEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform BuildCanvas()
        {
            var canvasGo = new GameObject("JackpotRunCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)canvasGo.transform;
        }

        private static RectTransform BuildOverlayLayer(Transform canvasRoot)
        {
            // S7b가 NodePanel/ShopPanel 등 팝업을 붙일 자리 — 이번 슬라이스는 빈 레이어만 만든다.
            var overlay = UiKit.Panel(canvasRoot, "OverlayLayer", new Color(0f, 0f, 0f, 0f));
            UiKit.Fill(overlay);
            return overlay;
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

        // ── MenuView 화면 ────────────────────────────────────────────────────────────
        private static MenuBuildResult BuildMenuScreen(Transform canvasRoot)
        {
            const float titleTopMargin = 140f;
            const float titleHeight = 140f;

            var result = new MenuBuildResult();
            var root = UiKit.Panel(canvasRoot, "MenuScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<MenuView>();

            var title = UiKit.Text(root, "🎰 잭팟런", UiKit.TextStyle.Title, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(960f, titleHeight);
            title.rectTransform.anchoredPosition = new Vector2(0f, -titleTopMargin);

            result.carouselTrack = BuildCarousel(root, titleTopMargin + titleHeight + 24f);

            float bottomTop = titleTopMargin + titleHeight + 24f + CarouselSlotWidth + 40f;
            var bottom = UiKit.VGroup(root, 28, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SetAnchors(bottom, Vector2.zero, Vector2.one, new Vector2(60f, 60f), new Vector2(-60f, -bottomTop));

            result.profileSummaryText = UiKit.Text(bottom, "", 24, UiKit.Good, TextAnchor.MiddleCenter);
            UiKit.SizeHint(result.profileSummaryText, preferredHeight: 40);

            var panelSprite = UiSpriteGen.Load("panel_r24");
            result.startButton = UiKit.Button(bottom, "게임 시작", new Vector2(0, 140), UiKit.Good, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(result.startButton, preferredHeight: 140);

            result.dexButton = UiKit.Button(bottom, "도감", new Vector2(0, 140), UiKit.Blue, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(result.dexButton, preferredHeight: 140);

            var footerSpacer = UiKit.Panel(bottom, "FooterSpacer", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(footerSpacer, flexibleHeight: 1);

            var credit = UiKit.Text(bottom, "잭팟런 — Unity", 20, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(credit, preferredHeight: 50);

            return result;
        }

        // 캐러셀 트랙 — 고유 아트 3장(CarouselCharIds) + 첫 장의 복제본 1장을 CarouselSlotWidth 간격으로
        // 나란히 배치한다. viewport 폭 == CarouselSlotWidth라 한 번에 1장만 보이고, MenuView가
        // anchoredPosition.x를 -CarouselSlotWidth*index로 애니메이션해 넘긴다(MenuView.CarouselLoop).
        private static RectTransform BuildCarousel(Transform parent, float topOffset)
        {
            var viewport = UiKit.Panel(parent, "CarouselViewport", new Color(0f, 0f, 0f, 0f));
            viewport.anchorMin = new Vector2(0.5f, 1f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(CarouselSlotWidth, CarouselSlotWidth);
            viewport.anchoredPosition = new Vector2(0f, -topOffset);
            viewport.gameObject.AddComponent<RectMask2D>();

            var trackGo = new GameObject("Track", typeof(RectTransform));
            var track = (RectTransform)trackGo.transform;
            track.SetParent(viewport, false);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(CarouselSlotWidth * 4f, CarouselSlotWidth);
            track.anchoredPosition = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                string charId = CarouselCharIds[i % CarouselCharIds.Length];
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/JackpotRun/Resources/JackpotRun/Sprites/Characters/char_{charId}.png");
                var img = UiKit.Image(track, sprite, Color.white);
                img.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(512f, 512f);
                img.rectTransform.anchoredPosition = new Vector2(CarouselSlotWidth * i + CarouselSlotWidth / 2f, 0f);
            }

            return track;
        }

        private static void WireMenuView(MenuBuildResult r, AppRoot appRoot)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("appRoot").objectReferenceValue = appRoot;
            so.FindProperty("profileSummaryText").objectReferenceValue = r.profileSummaryText;
            so.FindProperty("carouselTrack").objectReferenceValue = r.carouselTrack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── PickView 화면 ────────────────────────────────────────────────────────────
        private static PickBuildResult BuildPickScreen(Transform canvasRoot)
        {
            var result = new PickBuildResult();
            var panelSprite = UiSpriteGen.Load("panel_r24");

            var root = UiKit.Panel(canvasRoot, "PickScreen", UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();
            result.view = root.gameObject.AddComponent<PickView>();

            var col = UiKit.VGroup(root, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.Fill(col);
            // 루트 VGroup은 childForceExpandHeight=false(UiKit.VGroup 고정값) — 아래 각 행은 전부
            // preferredHeight+flexibleHeight=0으로 "명시"해 잔여 공간을 나눠 갖지 못하게 한다.
            // 그리드 스크롤만 flexibleHeight=1로 잔여 전부를 가져간다(Fable 육안 검수 수정 지시,
            // 2026-07-31: 행 높이 배분 붕괴 — pill/탭/정렬 행이 거대 카드로 부풀고 그리드가 15%로 압축됨).

            // 헤더 — 110, "← 메뉴"는 160×64 소형(행이 늘어나도 늘어나지 않도록 forceExpandHeight=false)
            var header = UiKit.HGroup(col, 16, new RectOffset(24, 24, 16, 12), true, false);
            UiKit.SizeHint(header, preferredHeight: 110, flexibleHeight: 0);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            result.backButton = UiKit.Button(header, "← 메뉴", new Vector2(160, 64), UiKit.PanelBg, UiKit.TextPrimary, null, panelSprite);
            UiKit.SizeHint(result.backButton, preferredWidth: 160, preferredHeight: 64, flexibleWidth: 0, flexibleHeight: 0);
            var headerTitle = UiKit.Text(header, "캐릭터 · 머신 · 장치를 골라 시작하세요", UiKit.TextStyle.Body, TextAnchor.MiddleLeft);
            UiKit.SizeHint(headerTitle, flexibleWidth: 1, flexibleHeight: 0);
            // header가 controlChildH=false라 자식 높이를 만지지 않는다 — 새 RectTransform의 기본
            // sizeDelta(100×100)를 그대로 두면 폭만 맞고 높이가 어긋나므로 back 버튼과 같은 64로 맞춘다.
            headerTitle.rectTransform.sizeDelta = new Vector2(headerTitle.rectTransform.sizeDelta.x, 64f);

            // 추천 4버튼(순서: 입문/고점/도전/랜덤 — PickView.RecoKinds와 일치해야 함) — 행 84, 버튼 64
            var recoRow = UiKit.HGroup(col, 12, new RectOffset(24, 24, 10, 10), true, false);
            UiKit.SizeHint(recoRow, preferredHeight: 84, flexibleHeight: 0);
            recoRow.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string[] recoLabels = { "입문", "고점", "도전", "랜덤" };
            result.recoButtons = new Button[recoLabels.Length];
            for (int i = 0; i < recoLabels.Length; i++)
            {
                var btn = UiKit.Button(recoRow, recoLabels[i], new Vector2(0, 64), UiKit.Card, UiKit.Accent, null, panelSprite);
                UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 64, flexibleHeight: 0);
                result.recoButtons[i] = btn;
            }

            // 탭 3버튼(순서: 캐릭터/머신/장치 — PickView.TabOrder와 일치해야 함) — 행 130(탭은 행 전체를
            // 채우는 큰 터치영역이 맞으므로 forceExpandHeight=true 유지, 내부 제목+라벨은 중앙 정렬로 보정)
            var tabsRow = UiKit.HGroup(col, 12, new RectOffset(24, 24, 8, 8), true, true);
            UiKit.SizeHint(tabsRow, preferredHeight: 130, flexibleHeight: 0);
            string[] tabTitles = { "🎭 캐릭터", "🎰 슬롯머신", "🔧 장치" };
            result.tabButtons = new Button[3];
            result.tabButtonImages = new Image[3];
            result.tabLabelTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var (btn, bg, label) = BuildTabButton(tabsRow, tabTitles[i], panelSprite);
                result.tabButtons[i] = btn;
                result.tabButtonImages[i] = bg;
                result.tabLabelTexts[i] = label;
            }

            // 필터 칩(가로 스크롤 + 템플릿) — 행 64
            var chipScroll = UiKit.Scroll(col, out var chipsContent, vertical: false);
            UiKit.SizeHint(chipScroll, preferredHeight: 64, flexibleHeight: 0);
            var chipsHlg = chipsContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipsHlg.spacing = 10;
            chipsHlg.padding = new RectOffset(20, 20, 8, 8);
            chipsHlg.childControlWidth = false;
            chipsHlg.childControlHeight = true;
            chipsHlg.childForceExpandWidth = false;
            chipsHlg.childForceExpandHeight = false;
            chipsHlg.childAlignment = TextAnchor.MiddleLeft;
            var chipsCsf = chipsContent.gameObject.AddComponent<ContentSizeFitter>();
            chipsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            result.chipsContent = chipsContent;
            result.chipTemplate = BuildChipTemplate(chipsContent);

            // 정렬 4버튼(순서: 추천순/난이도순/고점순/최근해금순 — PickView.SortKeys와 일치해야 함) — 행 72, 버튼 56
            var sortRow = UiKit.HGroup(col, 10, new RectOffset(24, 24, 8, 8), true, false);
            UiKit.SizeHint(sortRow, preferredHeight: 72, flexibleHeight: 0);
            sortRow.gameObject.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string[] sortLabels = { "추천순", "난이도순", "고점순", "최근해금순" };
            result.sortButtons = new Button[sortLabels.Length];
            result.sortButtonImages = new Image[sortLabels.Length];
            for (int i = 0; i < sortLabels.Length; i++)
            {
                var btn = UiKit.Button(sortRow, sortLabels[i], new Vector2(0, 56), UiKit.Card, UiKit.TextPrimary, null, panelSprite);
                UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 56, flexibleHeight: 0);
                result.sortButtons[i] = btn;
                result.sortButtonImages[i] = btn.GetComponent<Image>();
            }

            // 카드 그리드(세로 스크롤 + 템플릿) — 잔여 전부(flexibleHeight=1), 이 행만 flexible
            var gridScroll = UiKit.Scroll(col, out var gridContent, vertical: true);
            UiKit.SizeHint(gridScroll, preferredHeight: 0, flexibleHeight: 1);
            UiKit.Grid(gridContent, new Vector2(500, 460), new Vector2(20, 20), 2);
            gridContent.gameObject.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 12, 20, 20);
            result.gridContent = gridContent;
            result.gridCanvasGroup = gridContent.gameObject.AddComponent<CanvasGroup>();
            result.cardTemplate = BuildCardTemplate(gridContent);

            // 요약 패널
            BuildSummaryPanel(col, result, panelSprite);

            return result;
        }

        private static (Button btn, Image bg, Text label) BuildTabButton(Transform parent, string title, Sprite sprite)
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
            // 탭 행(tabsRow) 자체는 130 고정(BuildPickScreen), 탭 버튼은 forceExpandHeight=true로 그
            // 행 전체 높이를 채우는 큰 터치영역이 의도다 — preferredHeight는 채워질 값의 기준선일 뿐.
            UiKit.SizeHint(btn, flexibleWidth: 1, preferredHeight: 114, flexibleHeight: 0);

            var col = UiKit.VGroup(rt, 2, new RectOffset(8, 8, 10, 10), true, true);
            UiKit.Fill(col);
            // 제목+선택라벨 2줄(합계 ~68px)이 채워진 탭 높이(114) 안에서 위로 쏠리지 않도록 중앙 정렬
            // (UiKit.VGroup 공용 헬퍼는 UpperCenter 고정이라 여기서 컴포넌트를 직접 덮어쓴다).
            col.gameObject.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var titleText = UiKit.Text(col, title, 22, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(titleText, preferredHeight: 38, flexibleHeight: 0);
            var labelText = UiKit.Text(col, "선택 전", 17, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(labelText, preferredHeight: 28, flexibleHeight: 0);

            return (btn, img, labelText);
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

        // 카드 템플릿 — "Body"(VerticalLayoutGroup: 아이콘 300 + 이름행 40 + 효과 1줄 52)는 스태킹
        // 컨테이너, Lock/Selected/IconEmoji는 오버레이라 카드 루트의 직계 자식으로 둔다(PickView.cs의
        // Find 경로 계약과 정확히 일치해야 한다 — "Body/Icon", "Body/Name", "Body/Badge",
        // "Body/Badge/Label", "Body/Eff", "IconEmoji", "Lock", "Lock/Hint", "Selected").
        private static RectTransform BuildCardTemplate(Transform parent)
        {
            var card = UiKit.Panel(parent, "CardTemplate", UiKit.Card, UiSpriteGen.Load("card_grad"));
            var cardImg = card.GetComponent<Image>();
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            card.gameObject.AddComponent<PressFx>();
            UiKit.AddGlowOutline(card.gameObject, UiKit.Accent, 3f);

            var body = UiKit.VGroup(card, 8, new RectOffset(18, 18, 18, 14), true, true);
            body.name = "Body";
            UiKit.Fill(body);

            var icon = UiKit.Image(body, null, Color.white);
            icon.name = "Icon";
            UiKit.SizeHint(icon, preferredHeight: 300);

            var nameRow = UiKit.HGroup(body, 10, new RectOffset(0, 0, 0, 0), true, true);
            nameRow.name = "NameRow"; // PickView가 "Body/NameRow/..." 경로로 바인딩한다 — 이름 계약
            UiKit.SizeHint(nameRow, preferredHeight: 40);

            var nameText = UiKit.Text(nameRow, "이름", 26, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            nameText.name = "Name";
            UiKit.SizeHint(nameText, flexibleWidth: 1, preferredHeight: 40);

            var badge = UiKit.Panel(nameRow, "Badge", UiKit.Blue, UiSpriteGen.Load("chip_r999"));
            UiKit.SizeHint(badge, preferredWidth: 96, flexibleWidth: 0, preferredHeight: 34);
            var badgeLabel = UiKit.Text(badge, "배지", 15, UiKit.Bg, TextAnchor.MiddleCenter, true);
            badgeLabel.name = "Label";
            UiKit.Fill(badgeLabel.rectTransform);

            var effText = UiKit.Text(body, "효과 설명", 19, UiKit.TextSecondary, TextAnchor.UpperLeft);
            effText.name = "Eff";
            UiKit.SizeHint(effText, preferredHeight: 52);

            // 스프라이트가 없을 때(예: "장치 없이" 카드)의 이모지 폴백 — Body 스태킹에 끼지 않도록
            // 카드 루트 직계 자식으로 두고 Icon 슬롯 자리(패딩 18 + 높이 300)를 수동으로 겹친다.
            var iconEmoji = UiKit.Text(card, "🚫", 96, UiKit.TextPrimary, TextAnchor.MiddleCenter);
            iconEmoji.name = "IconEmoji";
            iconEmoji.rectTransform.anchorMin = new Vector2(0f, 1f);
            iconEmoji.rectTransform.anchorMax = new Vector2(1f, 1f);
            iconEmoji.rectTransform.pivot = new Vector2(0.5f, 1f);
            iconEmoji.rectTransform.sizeDelta = new Vector2(0f, 300f);
            iconEmoji.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            iconEmoji.gameObject.SetActive(false);

            var lockOverlay = UiKit.Panel(card, "Lock", UiKit.LockScrim);
            lockOverlay.name = "Lock";
            UiKit.Fill(lockOverlay);
            var lockCol = UiKit.VGroup(lockOverlay, 6, new RectOffset(16, 16, 16, 16), true, true);
            UiKit.Fill(lockCol);
            var lockIcon = UiKit.Text(lockCol, "🔒 잠김", 24, UiKit.TextPrimary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(lockIcon, preferredHeight: 36);
            var lockHint = UiKit.Text(lockCol, "", 16, UiKit.TextSecondary, TextAnchor.UpperLeft);
            lockHint.name = "Hint";
            UiKit.SizeHint(lockHint, flexibleHeight: 1);
            lockOverlay.gameObject.SetActive(false);

            var selectedMark = UiKit.Text(card, "선택됨 ✓", 16, UiKit.Accent, TextAnchor.UpperRight, true);
            selectedMark.name = "Selected";
            UiKit.SetAnchors(selectedMark.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-190, -32), new Vector2(-14, -6));
            selectedMark.gameObject.SetActive(false);

            card.gameObject.SetActive(false);
            return card;
        }

        private static void BuildSummaryPanel(Transform parent, PickBuildResult result, Sprite panelSprite)
        {
            // 고정 500(Fable 육안 검수 수정 지시) — 안의 각 줄도 전부 flexibleHeight=0으로 명시하고,
            // pros/cons 2열(colsRow)만 flexibleHeight=1로 남는 공간을 가져간다(최소 100은 유지되도록
            // 위 고정 줄 합계를 500 예산 안에 맞춰뒀다: 36+32+56+48+30+110 + spacing48 = 360,
            // 500-32(패딩)-360 = 108 ≥ minHeight 100).
            var panel = UiKit.Panel(parent, "Summary", UiKit.PanelBg, panelSprite);
            UiKit.SizeHint(panel, preferredHeight: 500, flexibleHeight: 0);
            var col = UiKit.VGroup(panel, 8, new RectOffset(24, 24, 16, 16), true, true);
            UiKit.Fill(col);

            result.comboText = UiKit.Text(col, "", 26, UiKit.TextPrimary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(result.comboText, preferredHeight: 36, flexibleHeight: 0);

            result.gradeText = UiKit.Text(col, "", 26, UiKit.TextSecondary, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(result.gradeText, preferredHeight: 32, flexibleHeight: 0);

            var meterRow = UiKit.HGroup(col, 20, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(meterRow, preferredHeight: 56, flexibleHeight: 0);
            var ceil = BuildMeterCell(meterRow, "점수 고점");
            var stab = BuildMeterCell(meterRow, "안정성");
            var diff = BuildMeterCell(meterRow, "난이도");
            result.ceilingValueText = ceil.value;
            result.stabilityValueText = stab.value;
            result.difficultyValueText = diff.value;
            result.difficultyLabelText = diff.label;

            result.blurbText = UiKit.Text(col, "", 19, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(result.blurbText, preferredHeight: 48, flexibleHeight: 0);

            var colsRow = UiKit.HGroup(col, 20, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(colsRow, minHeight: 100, flexibleHeight: 1);
            result.prosText = BuildListCell(colsRow, "장점", UiKit.Good);
            result.consText = BuildListCell(colsRow, "주의", UiKit.Bad);

            result.buildText = UiKit.Text(col, "", 17, UiKit.TextSecondary, TextAnchor.UpperLeft);
            UiKit.SizeHint(result.buildText, preferredHeight: 30, flexibleHeight: 0);

            // 활성(interactable) 상태 = Accent(#FFD23F) 배경 + 검정(UiKit.Bg) 글자로 또렷하게(이미 이
            // 색 조합이지만 130→110 높이 조정과 함께 재확인). 비활성일 때만 Button.disabledColor/
            // PressFx 알파로 흐려진다 — 캐릭터·머신 미선택 상태의 기본값이라 정상 동작이다.
            result.startButton = UiKit.Button(col, "시작", new Vector2(0, 110), UiKit.Accent, UiKit.Bg, null, panelSprite);
            UiKit.SizeHint(result.startButton, preferredHeight: 110, flexibleHeight: 0);
        }

        private static (Text label, Text value) BuildMeterCell(RectTransform row, string label)
        {
            var cell = UiKit.VGroup(row, 2, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(cell, flexibleWidth: 1);
            var l = UiKit.Text(cell, label, 18, UiKit.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.SizeHint(l, preferredHeight: 24);
            var v = UiKit.Text(cell, "", 24, UiKit.Accent, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(v, preferredHeight: 30);
            return (l, v);
        }

        private static Text BuildListCell(RectTransform row, string title, Color color)
        {
            var cell = UiKit.VGroup(row, 4, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SizeHint(cell, flexibleWidth: 1);
            var t = UiKit.Text(cell, title, 19, color, TextAnchor.MiddleLeft, true);
            UiKit.SizeHint(t, preferredHeight: 26);
            var body = UiKit.Text(cell, "", 17, UiKit.TextPrimary, TextAnchor.UpperLeft);
            UiKit.SizeHint(body, flexibleHeight: 1);
            return body;
        }

        private static void WirePickView(PickBuildResult r, AppRoot appRoot)
        {
            var so = new SerializedObject(r.view);
            so.FindProperty("appRoot").objectReferenceValue = appRoot;
            SetObjectArray(so, "recoButtons", r.recoButtons);
            SetObjectArray(so, "tabButtons", r.tabButtons);
            SetObjectArray(so, "tabButtonImages", r.tabButtonImages);
            SetObjectArray(so, "tabLabelTexts", r.tabLabelTexts);
            so.FindProperty("chipsContent").objectReferenceValue = r.chipsContent;
            so.FindProperty("chipTemplate").objectReferenceValue = r.chipTemplate;
            SetObjectArray(so, "sortButtons", r.sortButtons);
            SetObjectArray(so, "sortButtonImages", r.sortButtonImages);
            so.FindProperty("gridContent").objectReferenceValue = r.gridContent;
            so.FindProperty("gridCanvasGroup").objectReferenceValue = r.gridCanvasGroup;
            so.FindProperty("cardTemplate").objectReferenceValue = r.cardTemplate;
            so.FindProperty("comboText").objectReferenceValue = r.comboText;
            so.FindProperty("gradeText").objectReferenceValue = r.gradeText;
            so.FindProperty("ceilingValueText").objectReferenceValue = r.ceilingValueText;
            so.FindProperty("stabilityValueText").objectReferenceValue = r.stabilityValueText;
            so.FindProperty("difficultyValueText").objectReferenceValue = r.difficultyValueText;
            so.FindProperty("difficultyLabelText").objectReferenceValue = r.difficultyLabelText;
            so.FindProperty("blurbText").objectReferenceValue = r.blurbText;
            so.FindProperty("prosText").objectReferenceValue = r.prosText;
            so.FindProperty("consText").objectReferenceValue = r.consText;
            so.FindProperty("buildText").objectReferenceValue = r.buildText;
            so.FindProperty("startButton").objectReferenceValue = r.startButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Run/Dex 자리표시 화면(S7b가 내용을 채운다) ────────────────────────────────
        private static PlaceholderBuildResult BuildPlaceholderScreen(Transform canvasRoot, string name, string message)
        {
            var result = new PlaceholderBuildResult();
            var root = UiKit.Panel(canvasRoot, name, UiKit.Bg);
            UiKit.Fill(root);
            result.root = root;
            result.group = root.gameObject.AddComponent<CanvasGroup>();

            var col = UiKit.VGroup(root, 20, new RectOffset(0, 0, 0, 0), true, true);
            UiKit.SetAnchors(col, Vector2.zero, Vector2.one, new Vector2(60f, 60f), new Vector2(-60f, -60f));

            var spacerTop = UiKit.Panel(col, "SpacerTop", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(spacerTop, flexibleHeight: 1);

            var label = UiKit.Text(col, message, 28, UiKit.TextSecondary, TextAnchor.MiddleCenter, true);
            UiKit.SizeHint(label, preferredHeight: 80);

            result.backButton = UiKit.Button(col, "메뉴로", new Vector2(0, 120), UiKit.Card, UiKit.TextPrimary, null,
                UiSpriteGen.Load("panel_r24"));
            UiKit.SizeHint(result.backButton, preferredHeight: 120);

            var spacerBottom = UiKit.Panel(col, "SpacerBottom", new Color(0f, 0f, 0f, 0f));
            UiKit.SizeHint(spacerBottom, flexibleHeight: 1);

            return result;
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
