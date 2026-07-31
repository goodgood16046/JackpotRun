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
        // ── 팔레트 (S10 — public/jackpotpick/pick.css :root 그대로 이식) ──────────────────
        // 기존 필드명은 유지하고 값만 pick.css 값으로 교체했다(호출부 전부 그대로 동작).
        // pick.css에 없던 색(bg1/panel2/bd/bd2/dim2/amber/pink/green)은 CSS 변수명 그대로 새로 추가.
        public static readonly Color Bg = Hex("#0B0D15"); // --bg0
        public static readonly Color Bg1 = Hex("#11131F"); // --bg1(헤더 그라데이션 등)
        public static readonly Color PanelBg = Hex("#171A27"); // --panel
        public static readonly Color Panel2 = Hex("#1C2030"); // --panel2(카드 그라데이션 상단/밝은 패널)
        public static readonly Color Card = Panel2; // 기존 소비처 호환용 별칭 — panel2와 동일
        // 탭/카드 "활성" 강조색 — pick.css .tab.active{background:linear-gradient(180deg,#2a2238,#211c30)}
        // 두 스톱의 평균(#251f34, 보라 톤)으로 근사(uGUI Image는 단색만 지원 — 그라데이션 재해석).
        // PickView/DexView의 활성 탭·선택 카드 강조에 공용으로 쓰인다(기존 필드명 유지).
        public static readonly Color CardTop = Hex("#251F34");
        public static readonly Color Bd = Hex("#2A3048"); // --bd(기본 테두리)
        public static readonly Color Bd2 = Hex("#394365"); // --bd2(hover/강조 테두리)
        public static readonly Color Accent = Hex("#FFD23F"); // --gold
        public static readonly Color Gold = Accent; // pick.css 이름 그대로 쓰고 싶은 호출부용 별칭
        public static readonly Color Amber = Hex("#F59E0B"); // --amber
        public static readonly Color Pink = Hex("#FF7ADB"); // --pink
        public static readonly Color TextPrimary = Hex("#E9EBF5"); // --txt
        public static readonly Color TextSecondary = Hex("#8B93A7"); // --dim
        public static readonly Color Dim2 = Hex("#69718A"); // --dim2(탭 번호 등 더 흐린 보조색)
        public static readonly Color Good = Hex("#34D3C0"); // --teal(기존 소비처 호환 별칭)
        public static readonly Color Teal = Good;
        public static readonly Color Bad = Hex("#FF6B6B"); // --red(기존 소비처 호환 별칭)
        public static readonly Color Red = Bad;
        public static readonly Color Blue = Hex("#5B8CFF"); // --blue
        public static readonly Color Purple = Hex("#A974FF"); // --purple
        public static readonly Color Green = Hex("#4ADE80"); // --green(장점 프리픽스 등 teal과 별개의 색)
        public static readonly Color LockScrim = new Color(11f / 255f, 13f / 255f, 21f / 255f, 0.62f); // bg0 62%

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

        public static Image Image(Transform parent, Sprite sprite, Color tint)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.preserveAspect = sprite != null;
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
