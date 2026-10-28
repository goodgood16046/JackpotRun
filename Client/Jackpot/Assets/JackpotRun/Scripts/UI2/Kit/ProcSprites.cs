using System;
using System.Collections.Generic;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 런타임 코드 생성 스프라이트 — FUN_OVERHAUL_DESIGN F2~F6 연출용(홀드 자물쇠·프레임 발광·비네트·둥근 사각).
    // UiSpriteGen(Editor 전용)이 굽는 w_* 스프라이트는 런타임 코드에서 접근할 수 없고(PillSprite 도 에디터에서만
    // 값이 나온다), 씬을 다시 굽지 않아도(▶ Play 만으로) 동작해야 하므로 필요한 도형을 픽셀 단위로 직접 만든다.
    // 전부 흰색(알파만 가진) 도형이라 사용처에서 Image.color 로 틴트한다. 텍스처/스프라이트는 한 번 만들어 캐시한다
    // (HideAndDontSave — 씬 전환에도 유지되고 저장되지 않는다). 심볼/아이콘이 아니라 UI 도형 전용이다.
    public static class ProcSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>자물쇠(64×64, 흰색). 열쇠구멍은 알파로 뚫려 있다. astral 이모지(🔒) 대신 쓴다.</summary>
        public static Sprite Lock() => Get("lock", () => Make(LockPixels(64), 64, Vector4.zero));

        /// <summary>화면 가장자리만 불투명해지는 비네트(128×128, 흰색 — 풀스크린 Image.Simple 로 늘려 쓴다).</summary>
        public static Sprite Vignette() => Get("vignette", () => Make(VignettePixels(128), 128, Vector4.zero));

        /// <summary>안쪽으로 번지는 프레임 발광(64×64, 9-slice border 26) — 가운데는 완전 투명.</summary>
        public static Sprite FrameGlow() => Get("frameglow", () => Make(FramePixels(64, 26f, 14f), 64, new Vector4(26f, 26f, 26f, 26f)));

        /// <summary>단색 둥근 사각(9-slice, border=radius). radius 는 2~48 로 제한한다.</summary>
        public static Sprite RoundRect(int radius)
        {
            int r = Mathf.Clamp(radius, 2, 48);
            return Get("rr" + r, () => Make(RoundRectPixels(r * 2 + 2, r), r * 2 + 2, new Vector4(r, r, r, r)));
        }

        /// <summary>부드러운 원판(32×32).</summary>
        public static Sprite Disc() => Get("disc", () => Make(DiscPixels(32), 32, Vector4.zero));

        private static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var made = make();
            Cache[key] = made;
            return made;
        }

        private static Sprite Make(Color32[] pixels, int size, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ── 픽셀 생성(UnityEngine 네이티브 호출 없음 — 에디터 밖 검증용으로도 쓸 수 있게 순수 계산만) ────────────
        // 좌표계: 행 0 = 아래(Unity 텍스처 규약). 반환 배열 인덱스 = y*size + x.

        internal static Color32[] LockPixels(int n)
        {
            var px = new Color32[n * n];
            float s = n / 64f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / s, fy = (y + 0.5f) / s;   // 64 기준 좌표
                    float body = -SdRoundBox(fx, fy, 32f, 21f, 20f, 13f, 6f);          // 몸통(안쪽 양수)
                    float dist = Mathf.Sqrt((fx - 32f) * (fx - 32f) + (fy - 34f) * (fy - 34f));
                    float ring = Mathf.Min(15f - dist, dist - 9f);                      // 고리 띠(안쪽 양수)
                    float shackle = Mathf.Min(ring, fy - 30f);                          // 위쪽 반원 + 다리
                    float shape = Mathf.Max(body, shackle);
                    float a = Mathf.Clamp01(0.5f + shape * s);

                    float keyCircle = 4.4f - Mathf.Sqrt((fx - 32f) * (fx - 32f) + (fy - 23.5f) * (fy - 23.5f));
                    float keySlot = -SdRoundBox(fx, fy, 32f, 16.5f, 1.9f, 6.5f, 0.6f);
                    float cut = Mathf.Clamp01(0.5f + Mathf.Max(keyCircle, keySlot) * s);
                    a *= 1f - cut;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return px;
        }

        internal static Color32[] VignettePixels(int n)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;   // 모서리 = 1
                    float t = Mathf.Clamp01((d - 0.42f) / 0.58f);
                    float a = t * t * (3f - 2f * t);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return px;
        }

        internal static Color32[] FramePixels(int n, float reach, float radius)
        {
            var px = new Color32[n * n];
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float depth = -SdRoundBox(x + 0.5f, y + 0.5f, half, half, half, half, radius);   // 가장자리에서 안쪽 거리
                    float inside = Mathf.Clamp01(depth + 0.5f);
                    float line = Mathf.Clamp01(depth) * Mathf.Clamp01(4.2f - depth);                  // 또렷한 바깥 선(~4px)
                    float glow = 0.85f * Mathf.Exp(-Mathf.Max(depth, 0f) / 9f) * Mathf.Clamp01((reach - depth) / 6f);
                    float a = Mathf.Max(line, glow) * inside;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            return px;
        }

        internal static Color32[] RoundRectPixels(int n, float radius)
        {
            var px = new Color32[n * n];
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = Mathf.Clamp01(0.5f - SdRoundBox(x + 0.5f, y + 0.5f, half, half, half, half, radius));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return px;
        }

        internal static Color32[] DiscPixels(int n)
        {
            var px = new Color32[n * n];
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float a = Mathf.Clamp01(0.5f + (c - 1f - Mathf.Sqrt(dx * dx + dy * dy)));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return px;
        }

        // 둥근 상자의 부호 있는 거리(바깥 양수, 안쪽 음수) — (cx,cy)=중심, (hx,hy)=반 크기, r=모서리 반경.
        private static float SdRoundBox(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - (hx - r);
            float qy = Mathf.Abs(py - cy) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
