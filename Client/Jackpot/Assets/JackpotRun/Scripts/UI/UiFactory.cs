using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>
    /// 코드 전용 uGUI 생성 헬퍼. 프리팹/씬 에셋을 쓰지 않고 런타임에 모든 UI를 만든다.
    /// </summary>
    public static class UiFactory
    {
        private static Font _kor;
        private static bool _korTried;

        /// <summary>번들 한글 폰트(Pretendard) 우선, 없으면 OS 동적 폰트(맑은 고딕) 폴백. 캐시.</summary>
        public static Font Kor()
        {
            if (_kor == null && !_korTried)
            {
                _korTried = true;
                _kor = Resources.Load<Font>("JackpotRun/Fonts/Pretendard-Regular");
                if (_kor != null)
                {
                    // Pretendard에는 이모지 글리프가 없다 — 없는 글리프는 OS 폰트로 폴백시킨다.
                    _kor.fontNames = new[]
                    {
                        "Pretendard", "Malgun Gothic", "맑은 고딕",
                        "Segoe UI Emoji", "Segoe UI Symbol", "Noto Sans KR", "Roboto", "Arial"
                    };
                }
                else
                {
                    _kor = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
                }
            }
            return _kor;
        }

        /// <summary>"#RRGGBB"(또는 "#RRGGBBAA") 문자열을 Color로 변환. 실패 시 흰색.</summary>
        public static Color Hex(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.white;
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>부모 rect에 0..1 anchor로 완전히 늘려붙인다.</summary>
        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static Text Text(Transform parent, string content, int size, Color color, TextAnchor anchor, bool bold = false)
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

        /// <summary>size = 버튼의 픽셀 크기(width,height). 라벨 폰트 크기는 높이에 비례해 자동 산출.</summary>
        public static Button Button(Transform parent, string label, Vector2 size, Color bg, Color fg, UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg;
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            btn.colors = colors;

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.4f), 18, 40);
            var txt = Text(rt, label, fontSize, fg, TextAnchor.MiddleCenter, true);
            Fill(txt.rectTransform);
            return btn;
        }

        /// <summary>세로 레이아웃 그룹. autoSizeH=true면 ContentSizeFitter(PreferredSize)를 붙인다.</summary>
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

        /// <summary>가로 레이아웃 그룹. autoSizeW=true면 ContentSizeFitter(PreferredSize)를 붙인다.</summary>
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

        /// <summary>ScrollRect + Viewport(RectMask2D) + Content 를 만든다. vertical=false면 가로 스크롤.</summary>
        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool vertical = true)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = !vertical;
            sr.vertical = vertical;
            sr.movementType = ScrollRect.MovementType.Clamped;

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

        /// <summary>content에 GridLayoutGroup + ContentSizeFitter(세로 PreferredSize)를 붙인다.</summary>
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

        /// <summary>레이아웃 그룹 자식에 크기 힌트(LayoutElement)를 붙인다. 음수는 무시(미지정).</summary>
        public static LayoutElement SizeHint(Component c, float preferredHeight = -1, float flexibleHeight = -1,
            float preferredWidth = -1, float flexibleWidth = -1, float minHeight = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            return le;
        }
    }
}
