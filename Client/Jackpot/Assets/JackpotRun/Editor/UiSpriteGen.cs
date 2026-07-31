using System.IO;
using UnityEditor;
using UnityEngine;

namespace JackpotRun.EditorTools
{
    // 절차 스프라이트 PNG 생성 — ENGINE_PORT_DESIGN.md S7 "절차 생성 아트" 표. UiSceneBuilder가
    // "JackpotRun/Build UI Scene" 1단계로 이 클래스의 GenerateAll(overwrite:false)을 호출한다
    // ("① UiSpriteGen 실행(없는 것만)"). 결과 PNG는 Assets/JackpotRun/Art/UI/ 아래 커밋 대상 에셋이다.
    //
    // ── 색 적용 방식 ─────────────────────────────────────────────────────────────────
    // UI 크롬(panel_r24/card_r16/card_grad/chip_r999/outline_r16/cell_inset/bar_bg_r12/bar_fill_r12)은
    // 전부 "흰색 베이스"로 굽는다 — 소비 측(UiKit.Panel/UiSceneBuilder)이 Image.color로 원하는 색을
    // 곱하면 그대로 그 색이 나온다(card_grad도 마찬가지: 상단 8% 더 밝은 "흰색" 그라데이션에 임의
    // 틴트를 곱해도 상대적 밝기 비율은 그대로 유지된다). 반면 심볼 타일(sym_<id>)은 심볼마다 고유
    // 색이 정해져 있어(설계 색상표) 해당 색을 그대로 굽는다 — 소비 측은 틴트 없이 그대로 쓴다.
    //
    // ── 9-slice border 규칙 ──────────────────────────────────────────────────────────
    // 각 스프라이트 파일명의 "_rNN" 접미사가 곧 굽는 반경(px, 256px 캔버스 기준)이고, border도 동일
    // 값을 쓴다(반경과 9-slice border가 어긋나면 늘렸을 때 모서리가 잘리거나 평평해진다).
    // chip_r999(피임/스타디움 모양)만 예외 — "999"는 CSS 관용의 "완전히 둥글게"를 뜻하므로 256px
    // 캔버스에서 낼 수 있는 최대 반경(=128, 캔버스 절반)을 쓴다. 심볼 타일은 설계 문서가 명시한
    // "9-slice border 32(타일류만)"을 그대로 따라 반경도 32로 통일했다.
    public static class UiSpriteGen
    {
        public const string OutputDir = "Assets/JackpotRun/Art/UI";
        private const int CanvasSize = 256;

        // 심볼 14종 색상표 — ENGINE_PORT_DESIGN.md S7 "절차 생성 아트" 목록 그대로. skull은 원문의
        // "skull #9BA3B4(암배경 #2A0F14)" 표기 중 괄호 안 "암배경"(다크 배경) 값을 타일 배경으로
        // 굽는다 — 밝은 이모지가 얹힐 배경이 어두워야 대비가 나온다(중앙 이모지 자체는 뷰가 Text로
        // 오버레이하므로 이 PNG에는 포함되지 않는다).
        private static readonly (string id, string hex)[] SymbolColors =
        {
            ("cherry", "#E5484D"), ("book", "#4C8DFF"), ("star", "#F5C518"), ("gem", "#7C5CFF"),
            ("crown", "#FFB300"), ("skull", "#2A0F14"), ("coin", "#E8B93C"), ("flame", "#FF6B35"),
            ("magnet", "#5B8CFF"), ("bomb", "#3A4051"), ("dice", "#E8EAF2"), ("seed", "#4CAF50"),
            ("wild", "#00C2A8"), ("key", "#C9A227"),
        };

        [MenuItem("JackpotRun/Generate UI Sprites")]
        public static void GenerateAllMenuItem()
        {
            GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[JackpotRun] UI 스프라이트 생성 완료 — " + OutputDir);
        }

        /// <summary>없는 파일만 생성(overwrite=false)하거나 전부 다시 굽는다(overwrite=true).</summary>
        public static void GenerateAll(bool overwrite)
        {
            Directory.CreateDirectory(OutputDir);
            AssetDatabase.StartAssetEditing();
            try
            {
                WriteSprite("panel_r24", CreateRoundedRect(CanvasSize, 24, Color.white), Border(24), overwrite);
                WriteSprite("card_r16", CreateRoundedRect(CanvasSize, 16, Color.white), Border(16), overwrite);
                WriteSprite("card_grad", CreateRoundedRectGradient(CanvasSize, 16, 0.92f, 1f), Border(16), overwrite);
                WriteSprite("chip_r999", CreateRoundedRect(CanvasSize, CanvasSize / 2f, Color.white), Border(CanvasSize / 2f), overwrite);
                WriteSprite("outline_r16", CreateRoundedRingOutline(CanvasSize, 16, Color.white, 4f), Border(16), overwrite);
                WriteSprite("cell_inset", CreateRoundedRect(CanvasSize, 16, Color.white, insetShadowPx: 6f, shadowDarken: 0.18f), Border(16), overwrite);
                WriteSprite("bar_bg_r12", CreateRoundedRect(CanvasSize, 12, Color.white), Border(12), overwrite);
                WriteSprite("bar_fill_r12", CreateRoundedRect(CanvasSize, 12, Color.white), Border(12), overwrite);

                foreach (var (id, hex) in SymbolColors)
                    WriteSprite("sym_" + id, CreateRoundedRect(CanvasSize, 32, ParseHex(hex)), Border(32), overwrite);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>이미 생성된 스프라이트를 경로 규칙대로 불러온다(SceneBuilder가 참조 와이어링에 사용).</summary>
        public static Sprite Load(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputDir}/{fileName}.png");
        }

        private static Vector4 Border(float px) => new Vector4(px, px, px, px);

        private static void WriteSprite(string fileName, Texture2D tex, Vector4 border, bool overwrite)
        {
            string path = $"{OutputDir}/{fileName}.png";
            if (!overwrite && File.Exists(path))
            {
                Object.DestroyImmediate(tex);
                return;
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = border;
            importer.maxTextureSize = CanvasSize;
            importer.SaveAndReimport();
        }

        // ── 픽셀 드로잉(둥근 사각형 SDF) ─────────────────────────────────────────────
        // d = length(max(abs(p-c)-(halfSize-r),0)) - r  (표준 rounded-box SDF, d<0=내부, d=0=경계)
        private static float RoundedRectSdf(float px, float py, float w, float h, float radius)
        {
            float dx = Mathf.Max(Mathf.Abs(px - w / 2f) - (w / 2f - radius), 0f);
            float dy = Mathf.Max(Mathf.Abs(py - h / 2f) - (h / 2f - radius), 0f);
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        private static Texture2D NewTex(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

        private static Texture2D CreateRoundedRect(int size, float radius, Color fill,
            float insetShadowPx = 0f, float shadowDarken = 0f)
        {
            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float coverage = Mathf.Clamp01(0.5f - sdf); // ~1px 안티에일리어싱 밴드

                    Color c = fill;
                    if (insetShadowPx > 0f && sdf < 0f)
                    {
                        float innerDist = -sdf; // 경계=0, 안쪽으로 갈수록 증가
                        float shadow = Mathf.Clamp01(1f - innerDist / insetShadowPx);
                        float darken = 1f - shadowDarken * shadow;
                        c = new Color(fill.r * darken, fill.g * darken, fill.b * darken, fill.a);
                    }
                    c.a = fill.a * coverage;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // 세로 방향(y=0 하단 → y=size-1 상단) 밝기 보간 — "상단 밝게 +8%"(bottomBrightness=0.92, topBrightness=1).
        private static Texture2D CreateRoundedRectGradient(int size, float radius, float bottomBrightness, float topBrightness)
        {
            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                float brightness = Mathf.Lerp(bottomBrightness, topBrightness, v);
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float coverage = Mathf.Clamp01(0.5f - sdf);
                    pixels[y * size + x] = new Color(brightness, brightness, brightness, coverage);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // 테두리만 불투명한 링(선택 글로우용 outline_r16) — strokeWidth px 두께.
        private static Texture2D CreateRoundedRingOutline(int size, float radius, Color stroke, float strokeWidth)
        {
            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float outer = Mathf.Clamp01(0.5f - sdf);
                    float inner = Mathf.Clamp01(0.5f - (sdf + strokeWidth));
                    float ring = Mathf.Clamp01(outer - inner);
                    var c = stroke;
                    c.a = stroke.a * ring;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Color ParseHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
