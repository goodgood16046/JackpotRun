using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 팔레트 · 텍스트 스타일 · 티어색 · 공통 생성 헬퍼 — ENGINE_PORT_DESIGN.md S7 "파일 구성" 표의
    // Kit/UiKit.cs("뷰가 참조"). 두 소비자가 있다:
    //   1) Editor/UiSceneBuilder.cs — 씬의 정적 골격(헤더·버튼열·패널·스크롤 등)을 이 헬퍼들로
    //      결정론적으로 생성한다.
    //   2) 런타임 뷰(MenuView/PickView 등) — 데이터 개수에 따라 달라지는 요소(필터 칩·카드 그리드
    //      항목 등)는 SceneBuilder가 미리 만들어 둔 "템플릿"을 Object.Instantiate로 복제해 값만
    //      채우는 것이 기본(설계 원칙 "런타임 코드생성 금지"는 이 정적 골격 재구축 금지를 뜻한다).
    //      다만 팔레트/티어색/폰트 상수와 간단한 보조 GameObject(글로우 아웃라인 등)는 뷰에서도
    //      직접 참조·사용한다.
    public static class UiKit
    {
        // ── 팔레트 (S12a — Docs/WebRef/slot/style.css :root §0 표 그대로 이식) ─────────────
        // 기존 상수명은 유지하고 값을 slot/style.css :root 값으로 교체했다(호출부 전부 그대로 동작).
        // 이전(S10 pick.css) 값에서 바뀐 필드가 대부분이다 — PickView/DexView(S10)는 아직 이 색으로
        // 재검증되지 않았다(S12c "PickView·DexView 토큰 정리" 예정, 그때까지는 시각 변화가 그대로
        // 전파된다 — 보고 대상).
        public static readonly Color Bg = Hex("#07080f"); // --bg0
        public static readonly Color Bg1 = Hex("#0e1020"); // --bg1(헤더 그라데이션 등)
        public static readonly Color Bg2 = Hex("#141833"); // --bg2(신규)
        public static readonly Color PanelBg = Hex("#161a2c"); // --panel
        public static readonly Color Panel2 = Hex("#1c2238"); // --panel2(카드 그라데이션 상단/밝은 패널)
        public static readonly Color Panel3 = Hex("#252c46"); // --panel3(신규 — ghost 버튼 상단 등)
        public static readonly Color Card = Panel2; // 기존 소비처 호환용 별칭 — panel2와 동일
        // 탭/카드 "활성" 강조색 — pick.css(S10) 전용 파생색, slot/style.css :root에는 대응 토큰이
        // 없다(S12c 정리 대상 — 그때까지 유지, "부족한 색 추가"는 §0 표 항목에만 해당).
        public static readonly Color CardTop = Hex("#251F34");
        public static readonly Color Bd = Hex("#2c3454"); // --bd(기본 테두리)
        public static readonly Color Bd2 = Hex("#3f4a76"); // --bd2(hover/강조 테두리)
        public static readonly Color Accent = Hex("#ffd23f"); // --gold
        public static readonly Color Gold = Accent; // pick.css 이름 그대로 쓰고 싶은 호출부용 별칭
        public static readonly Color Gold2 = Hex("#ffb300"); // --gold2(신규)
        public static readonly Color Amber = Hex("#f59e0b"); // --amber
        public static readonly Color Pink = Hex("#ff6ec7"); // --pink
        public static readonly Color TextPrimary = Hex("#eef1fb"); // --txt
        public static readonly Color TextSecondary = Hex("#8b93b5"); // --dim
        public static readonly Color Dim2 = Hex("#6a7299"); // --dim2(탭 번호 등 더 흐린 보조색)
        public static readonly Color Good = Hex("#2ee6c8"); // --teal(기존 소비처 호환 별칭)
        public static readonly Color Teal = Good;
        public static readonly Color Bad = Hex("#ff5d6c"); // --red(기존 소비처 호환 별칭)
        public static readonly Color Red = Bad;
        public static readonly Color Blue = Hex("#5b9bff"); // --blue
        public static readonly Color Purple = Hex("#b07bff"); // --purple
        public static readonly Color Green = Hex("#4ade80"); // --green(장점 프리픽스 등 teal과 별개의 색)
        public static readonly Color Silver = Hex("#cdd6ea"); // --silver(신규)
        public static readonly Color Ink = Hex("#15131f"); // --ink(신규 — 골드 버튼 위 글자색)
        public static readonly Color LockScrim = new Color(Bg.r, Bg.g, Bg.b, 0.62f); // bg0 62%

        // ── 라운드 반경 상수(§0 "라운드" 표) — UiSpriteGen의 w_r9~r22/w_pill 9-slice와 짝을 이룬다.
        // 값 자체(px)는 CSS 리터럴 그대로(스케일 없음 — S10 rrect_r* 선례와 동일 관례, style.css도
        // --r-* 토큰을 --sc로 스케일하지 않는다).
        public const float R9 = 9f; // --r-sm
        public const float R12 = 12f; // --r-md
        public const float R16 = 16f; // --r-lg
        public const float R18 = 18f; // --r-xl
        public const float R22 = 22f; // --r-2xl
        public const float RPill = 999f; // --r-pill(스프라이트는 캔버스 절반인 128을 굽는다)

        // S13 §D — RunView 릴 셀의 정사각 한 변(px). Editor(UiSceneBuilder.BuildReelCellTemplate)와
        // 런타임(ReelView, Strip/Slot 노치 거리·오버슈트 계산)이 같은 값을 공유해야 무한 스크롤
        // 재활용 시 어긋나지 않는다 — 두 곳이 각자 상수를 들고 있으면 한쪽만 바뀔 위험이 있어 여기
        // 하나로 통일했다((1080 - 패딩48 - 스페이싱48)/5 ≈ 196.8의 근사 정사각, S7 이관 값 그대로).
        public const float ReelCellSize = 196f;

        // 등급 3종 — Tier enum(JackpotRun.Engine)과 대응하지만 여기서는 카탈로그 pick.tier 문자열
        // ("SILVER"/"GOLD"/"PRISM")을 그대로 받는다(뷰가 카탈로그 데이터를 직접 다루므로). 별도
        // "정답" 색상표가 없어 심볼 팔레트(gem=보석 #7C5CFF 등)와 톤을 맞춰 새로 정했다 — 사용자
        // 확인 필요 시 보고 대상.
        public static readonly Color TierSilver = Hex("#B9C0D4");
        public static readonly Color TierGold = Hex("#F5C518");
        public static readonly Color TierPrism = Hex("#7C5CFF");

        public static Color TierColor(string tier)
        {
            switch (tier)
            {
                case "SILVER": return TierSilver;
                case "GOLD": return TierGold;
                case "PRISM": return TierPrism;
                default: return TextSecondary;
            }
        }

        // ── 태그 칩 배색 (pick.css .tg.hot/.good/.high + 기본) — PickMeta.TagClass("hot"/"good"/"high")
        // 값 그대로 4종. 배경은 CSS rgba 그대로(半투명), 글자는 밝은 보정색, 테두리는 생략(Image
        // 단일색이라 별도 스트로크 없이 배경만으로 구분 — S10 재해석 항목).
        public static Color TagBg(string cls)
        {
            switch (cls)
            {
                case "hot": return new Color(1f, 107f / 255f, 107f / 255f, 0.14f);
                case "good": return new Color(52f / 255f, 211f / 255f, 192f / 255f, 0.13f);
                case "high": return new Color(1f, 122f / 255f, 219f / 255f, 0.13f);
                default: return new Color(1f, 1f, 1f, 0.05f);
            }
        }

        public static Color TagFg(string cls)
        {
            switch (cls)
            {
                case "hot": return Hex("#FF9B9B");
                case "good": return Hex("#6EE7D8");
                case "high": return Hex("#FFB0E8");
                default: return TextSecondary;
            }
        }

        // pick.css .jc-pc .li.con b(#ff9b9b) — 카드/요약의 "주의·－" 프리픽스 공용색.
        public static readonly Color ConWarn = Hex("#FF9B9B");

        // ── 텍스트 스타일 프리셋 ──────────────────────────────────────────────────────
        public enum TextStyle
        {
            Title, // 72pt 골드 볼드 — 메뉴 타이틀
            H1, // 30pt 볼드 — 패널 제목/콤보명
            H2, // 26pt 볼드 — 카드 이름/탭 제목
            Body, // 22pt 일반 — 본문/효과 설명
            BodySecondary, // 20pt 보조색 — 역할/힌트
            Caption, // 17pt 보조색 — 배지/라벨
        }

        private static (int size, Color color, bool bold) StyleOf(TextStyle style)
        {
            switch (style)
            {
                case TextStyle.Title: return (72, Accent, true);
                case TextStyle.H1: return (30, TextPrimary, true);
                case TextStyle.H2: return (26, TextPrimary, true);
                case TextStyle.Body: return (22, TextPrimary, false);
                case TextStyle.BodySecondary: return (20, TextSecondary, false);
                case TextStyle.Caption: return (17, TextSecondary, false);
                default: return (22, TextPrimary, false);
            }
        }

        // ── 폰트/색 파서 ──────────────────────────────────────────────────────────────
        private static Font _kor;
        private static bool _korTried;

        /// <summary>번들 한글 폰트(Pretendard) 우선, 없으면 OS 동적 폰트 폴백. 캐시(UiFactory.Kor와 동일 규칙).</summary>
        public static Font Kor()
        {
            if (_kor == null && !_korTried)
            {
                _korTried = true;
                _kor = Resources.Load<Font>("JackpotRun/Fonts/Pretendard-Regular");
                if (_kor != null)
                {
                    _kor.fontNames = new[]
                    {
                        "Pretendard", "Malgun Gothic", "맑은 고딕",
                        "Segoe UI Emoji", "Segoe UI Symbol", "Noto Sans KR", "Roboto", "Arial",
                    };
                }
                else
                {
                    _kor = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
                }
            }
            return _kor;
        }

        public static Color Hex(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.white;
        }

        // ── 레이아웃 보조 ─────────────────────────────────────────────────────────────
        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static LayoutElement SizeHint(Component c, float preferredHeight = -1, float flexibleHeight = -1,
            float preferredWidth = -1, float flexibleWidth = -1, float minHeight = -1, float minWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            if (minWidth >= 0) le.minWidth = minWidth;
            return le;
        }

        // ── S13 §A: 9-slice 늘어남 정리 ──────────────────────────────────────────────────
        // chip_r999(border 128)를 대상보다 작은 pill/칩/배지에 그대로 쓰면 9-slice 모서리 영역이
        // 서로 겹쳐 "늘어난 타원"이 된다(실측 위반: Toast 800×84, PriceButton 150×84, 불운 Pip
        // 24×24, 필터/빌드 칩 등 — ENGINE_PORT_DESIGN.md S13 §A). border 합(반경×2)이 대상 높이를
        // 넘지 않는 후보 중 가장 큰 반경을 골라 항상 안전하게 만든다.
        // 후보는 설계 §A가 명시한 6종(UiSpriteGen이 굽는 w_r9/12/16/18/22 + w_pill_btn)뿐이라
        // 22~63px 구간(border 44~127)엔 대응 파일이 없다 — 그 구간 높이는 w_r22로 수렴한다
        // ("완벽한 반원"이 아니라 "찌그러지지 않는 최대치"가 목표, S13 설계 그대로).
        private static readonly (string file, float radius)[] PillCandidates =
        {
            ("w_r9", R9), ("w_r12", R12), ("w_r16", R16), ("w_r18", R18), ("w_r22", R22), ("w_pill_btn", 64f),
        };

        /// <summary>targetHeight(9-slice가 실제로 적용될 대상의 세로 길이, px)에 맞는 가장 큰 반경의
        /// pill 스프라이트를 돌려준다. 전부 초과하면(targetHeight&lt;18) 가장 작은 후보로 폴백한다.
        /// Editor 빌드 타임 전용(UiSceneBuilder가 씬을 지을 때만 호출) — AssetDatabase로 직접 로드하므로
        /// 플레이어 빌드에서 호출하면 null을 반환한다(현재 모든 호출부가 Editor 코드 — S13 §A 실측).</summary>
        public static Sprite PillSprite(float targetHeight)
        {
#if UNITY_EDITOR
            string best = PillCandidates[0].file;
            for (int i = 0; i < PillCandidates.Length; i++)
            {
                if (PillCandidates[i].radius * 2f > targetHeight) break; // 반경 오름차순 — 이후는 전부 더 크다.
                best = PillCandidates[i].file;
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/JackpotRun/Art/UI/{best}.png");
#else
            return null;
#endif
        }

        // ── 생성 헬퍼 ──────────────────────────────────────────────────────────────────
        /// <summary>단색(+선택적 9-slice 스프라이트) 패널. sprite!=null이면 Image.Type.Sliced로 설정.</summary>
        public static RectTransform Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced; // "Image"만 쓰면 아래 static Image(...) 메서드와 이름이 겹쳐 CS0119
            }
            return rt;
        }

        public static Text Text(Transform parent, string content, int size, Color color, TextAnchor anchor,
            bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Kor();
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = content ?? string.Empty;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>프리셋 스타일로 텍스트를 만든다 — 세부 색/크기가 필요하면 위 Text(...) 오버로드를 쓴다.</summary>
        public static Text Text(Transform parent, string content, TextStyle style, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var (size, color, bold) = StyleOf(style);
            return Text(parent, content, size, color, anchor, bold);
        }

        /// <summary>S13 §B: 카탈로그/심볼 아트 슬롯 — preserveAspect를 항상 true로 고정한다(정사각
        /// 256px 원본이 비정사각 슬롯에서 늘어나는 것을 막는다). 이전엔 sprite!=null일 때만 켰는데,
        /// 이 헬퍼의 모든 호출부가 빌드 시점엔 sprite=null로 만들고 런타임에 값을 채우는 패턴
        /// (아이콘 슬롯은 전부 "빌더가 틀만 짓고 뷰가 채운다")이라 preserveAspect가 항상 꺼진 채로
        /// 굳어 있었다 — 아이콘 슬롯이 정사각이라 눈에 띄는 왜곡은 없었지만 잠재적 위반이었다.</summary>
        public static Image Image(Transform parent, Sprite sprite, Color tint)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>size = 버튼 픽셀 크기. PressFx가 자동으로 붙는다(S7 공통 규칙 "모든 버튼 PressFx").</summary>
        public static Button Button(Transform parent, string label, Vector2 size, Color bg, Color fg,
            UnityAction onClick, Sprite sprite = null)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PressFx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced; // "Image"만 쓰면 위 static Image(...) 메서드와 이름이 겹쳐 CS0119
            }
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            // Fable 육안 검수 수정(2026-07-31): 예전엔 여기서 알파 0.6을 곱하고 PressFx가 또 한 번
            // 알파를 낮춰(0.6×0.45≈0.27) 어두운 배경 위 골드 버튼이 탁한 갈색으로 보였다(pick.css
            // .go:disabled는 opacity:.42 하나만 적용 — 색은 그대로 골드). 이중 감쇠를 없애기 위해
            // 여기서는 완전 불투명을 유지하고, 비활성 페이드는 PressFx의 CanvasGroup 알파 하나로만.
            colors.disabledColor = Color.white;
            btn.colors = colors;

            // S13 §E — PressFx의 fx_btn_press는 "골드 버튼만"(설계 표). 골드 버튼은 전부 배경색으로
            // Accent(=Gold, #ffd23f)를 넘긴다(TitleView/LoginView/MenuView/PickView/RunView 등 8곳
            // 실측 — 별도 플래그 없이 bg 색 비교만으로 정확히 골드 버튼만 걸러진다).
            var pressFx = go.GetComponent<PressFx>();
            if (pressFx != null) pressFx.SetGold(bg == Accent);

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.4f), 18, 40);
            var txt = Text(rt, label, fontSize, fg, TextAnchor.MiddleCenter, true);
            Fill(txt.rectTransform);
            return btn;
        }

        public static RectTransform VGroup(Transform parent, float spacing, RectOffset padding,
            bool controlChildW = true, bool controlChildH = false, bool autoSizeH = false)
        {
            var go = new GameObject("VGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding ?? new RectOffset();
            vlg.childControlWidth = controlChildW;
            vlg.childControlHeight = controlChildH;
            vlg.childForceExpandWidth = controlChildW;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            if (autoSizeH)
            {
                var csf = go.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return rt;
        }

        public static RectTransform HGroup(Transform parent, float spacing, RectOffset padding,
            bool controlChildW = false, bool controlChildH = true, bool autoSizeW = false)
        {
            var go = new GameObject("HGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = padding ?? new RectOffset();
            hlg.childControlWidth = controlChildW;
            hlg.childControlHeight = controlChildH;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = controlChildH;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            if (autoSizeW)
            {
                var csf = go.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return rt;
        }

        /// <summary>ScrollRect + Viewport(RectMask2D) + Content. movementType은 Elastic(S7 공통 규칙 "스크롤은 Elastic").</summary>
        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool vertical = true)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = !vertical;
            sr.vertical = vertical;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.12f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var vrt = (RectTransform)viewportGo.transform;
            vrt.SetParent(rt, false);
            Fill(vrt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var crt = (RectTransform)contentGo.transform;
            crt.SetParent(vrt, false);
            if (vertical)
            {
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 0.5f);
            }
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            sr.viewport = vrt;
            sr.content = crt;
            content = crt;
            return sr;
        }

        public static GridLayoutGroup Grid(RectTransform content, Vector2 cellSize, Vector2 spacing, int constraintCount)
        {
            var glg = content.gameObject.GetComponent<GridLayoutGroup>();
            if (glg == null) glg = content.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = cellSize;
            glg.spacing = spacing;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = constraintCount;
            glg.childAlignment = TextAnchor.UpperLeft;

            var csf = content.gameObject.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return glg;
        }

        /// <summary>선택 글로우용 아웃라인(설계 "outline_r16" 용도) — 기본 비활성 상태로 반환.</summary>
        public static Outline AddGlowOutline(GameObject go, Color color, float distance = 3f)
        {
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.enabled = false;
            return outline;
        }
    }
}
