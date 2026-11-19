using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 런타임 아이콘 부착 헬퍼 — FUN_OVERHAUL_DESIGN §3(그래픽). "스프라이트가 있으면 Image, 없으면 기존
    // 이모지/텍스트" 규칙을 빌더가 만든 틀(씬에 구워진 레이아웃)에 아이콘 슬롯이 없는 화면들에도 적용하기
    // 위한 공용 코드다. 씬 리빌드 없이(▶ Play 만으로) 새 그림이 보여야 해서 SerializeField 와이어링 대신
    // 호스트 RectTransform 아래에 이름으로 자식 Image 를 지연 생성한다(있으면 재사용).
    //
    // 아이콘은 항상 preserveAspect + raycastTarget=false(호스트 버튼/카드의 클릭을 가로채지 않는다).
    // sprite 가 null 이면 자식 GameObject 를 비활성화하고 정렬 오프셋도 원래대로 되돌린다 → 그림이 없을 때는
    // 기존 화면과 완전히 동일하게 보인다.
    public static class IconKit
    {
        /// <summary>parent 아래의 name 자식 Image 를 찾는다(없으면 null).</summary>
        public static Image FindImage(RectTransform parent, string name)
        {
            var existing = parent.Find(name);
            return existing != null ? existing.GetComponent<Image>() : null;
        }

        /// <summary>parent 아래의 name 자식 Image 를 찾고, 없으면 만든다.</summary>
        public static Image EnsureImage(RectTransform parent, string name)
        {
            var found = FindImage(parent, name);
            if (found != null) return found;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// host 의 왼쪽 가운데에 size×size 정사각 아이콘을 얹는다(sprite==null 이면 숨김).
        /// shiftTarget(있으면)은 아이콘과 겹치지 않도록 offsetMin.x 를 margin+size+gap 으로 밀고,
        /// 아이콘이 없으면 shiftBaseLeft(원래 왼쪽 오프셋)로 되돌린다.
        /// </summary>
        public static void SetLeftIcon(RectTransform host, string name, Sprite sprite, float size, float margin,
            RectTransform shiftTarget = null, float shiftBaseLeft = 0f, float gap = 6f)
        {
            if (host == null) return;
            // 그림이 없고 이전에 만든 아이콘도 없으면 아무 오브젝트도 만들지 않는다(기존 화면과 완전 동일).
            var img = sprite != null ? EnsureImage(host, name) : FindImage(host, name);
            if (img != null)
            {
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(margin, 0f);
                Apply(img, sprite);
            }

            if (shiftTarget != null)
            {
                var om = shiftTarget.offsetMin;
                om.x = sprite != null ? margin + size + gap : shiftBaseLeft;
                shiftTarget.offsetMin = om;
            }
        }

        /// <summary>
        /// 제목 Text 의 왼쪽 끝(atRight=true 면 오른쪽 끝)에 size×size 아이콘을 얹는다. 제목이 가운데 정렬이면
        /// 왼쪽, 왼쪽 정렬이면 오른쪽에 두면 글자와 겹치지 않는다. 제목 행 높이(LayoutElement.preferredHeight)는
        /// 아이콘이 있으면 size+8 이상으로 키우고, 없으면 baseHeight 로 되돌린다.
        /// </summary>
        public static void SetHeadIcon(Text head, string name, Sprite sprite, float size, float baseHeight,
            bool atRight = false)
        {
            if (head == null) return;
            var host = head.rectTransform;
            var img = sprite != null ? EnsureImage(host, name) : FindImage(host, name);
            if (img != null)
            {
                var rt = img.rectTransform;
                float x = atRight ? 1f : 0f;
                rt.anchorMin = new Vector2(x, 0.5f);
                rt.anchorMax = new Vector2(x, 0.5f);
                rt.pivot = new Vector2(x, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = Vector2.zero;
                Apply(img, sprite);
            }

            var le = head.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = sprite != null ? Mathf.Max(baseHeight, size + 8f) : baseHeight;
        }

        private static void Apply(Image img, Sprite sprite)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.enabled = sprite != null;
            img.gameObject.SetActive(sprite != null);
        }
    }
}
