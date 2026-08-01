using System.IO;
using JackpotRun.UI2;
using UnityEditor;
using UnityEngine;

namespace JackpotRun.EditorTools
{
    // 절차 스프라이트 PNG 생성 — ENGINE_PORT_DESIGN.md S7 "절차 생성 아트" 표 + S8 항목⑤(심볼 표시
    // 근본 수정). UiSceneBuilder가 "JackpotRun/Build UI Scene(s)" 1단계로 이 클래스의
    // GenerateAll(overwrite:false)을 호출한다("① UiSpriteGen 실행(없는 것만)"). 결과 PNG는
    // Assets/JackpotRun/Art/UI/ 아래 커밋 대상 에셋이다.
    //
    // ── 색 적용 방식 ─────────────────────────────────────────────────────────────────
    // UI 크롬(panel_r24/card_r16/card_grad/chip_r999/outline_r16/cell_inset/bar_bg_r12/bar_fill_r12)은
    // 전부 "흰색 베이스"로 굽는다 — 소비 측(UiKit.Panel/UiSceneBuilder)이 Image.color로 원하는 색을
    // 곱하면 그대로 그 색이 나온다(card_grad도 마찬가지: 상단 8% 더 밝은 "흰색" 그라데이션에 임의
    // 틴트를 곱해도 상대적 밝기 비율은 그대로 유지된다). 반면 심볼 타일(sym_<id>)은 심볼마다 고유
    // 색이 정해져 있어(설계 색상표) 해당 색을 그대로 굽는다 — 소비 측은 틴트 없이 그대로 쓴다.
    //
    // ── S8 항목⑤: 심볼 도형 직접 그리기 ─────────────────────────────────────────────
    // 폰트 검증 결과 레거시 uGUI Text는 astral(서로게이트 페어) 이모지를 렌더링하지 못한다
    // (🍒📘💎🪙👑🔥🧲💣🎲🌱🌀🗝 전부 미표시). 이모지 대신 각 심볼 타일 위에 단색/2색 도형을 직접
    // 그려 넣는다(픽셀 SDF/폴리곤 헬퍼는 아래 "픽셀 드로잉" 절 참조). 배경 타일 색상표는 그대로
    // 유지하고, 그 위에 흰색(밝은 배경엔 대비를 위해 어두운색)으로 도형을 얹는다. skull만 예외 —
    // 원래도 암배경(#2A0F14) 자체가 다른 13종과 뚜렷이 구분되고 해골 실루엣을 저해상도 도형으로
    // 만들면 오히려 알아보기 어려워 도형 없이 배경 타일 그대로 둔다("기존" 유지).
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
        private const float Center = CanvasSize / 2f; // 128

        // 심볼 14종 색상표 — ENGINE_PORT_DESIGN.md S7 "절차 생성 아트" 목록 그대로. skull은 원문의
        // "skull #9BA3B4(암배경 #2A0F14)" 표기 중 괄호 안 "암배경"(다크 배경) 값을 타일 배경으로 굽는다.
        //
        // ⚠️ Scripts/UI2/Run/ReelView.cs의 SymbolTintById(런타임, FxId.SpinStop 심볼색 틴트용)가 이
        // 표를 그대로 복제해 쓴다(런타임 코드는 Editor 전용인 이 클래스를 참조할 수 없다) — 색을
        // 바꿀 때 두 표를 함께 갱신할 것.
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
                    WriteSprite("sym_" + id, CreateSymbolTile(CanvasSize, id, ParseHex(hex)), Border(32), overwrite);

                // ── S10 — pick.css .jcard 룩 이식용 라운드 반경별 9-slice 추가 (새 파일명, 기존
                // 파일은 건드리지 않는다 — overwrite:false 그대로도 신규 파일이라 항상 생성된다).
                // r7=badge(.b-diff)/jc-lock, r9=jc-eff/jc-unlock 박스, r11=jc-ico/meter 셀, r13=.tab,
                // r15=플랫 라운드(장식용). 값은 pick.css의 border-radius px 그대로(스케일 없음 — S10
                // 설계 지시: "라운드 반경별 9-slice: r7/r9/r11/r13/r15/r999" 이름이 곧 반경).
                WriteSprite("rrect_r7", CreateRoundedRect(CanvasSize, 7, Color.white), Border(7), overwrite);
                WriteSprite("rrect_r9", CreateRoundedRect(CanvasSize, 9, Color.white), Border(9), overwrite);
                WriteSprite("rrect_r11", CreateRoundedRect(CanvasSize, 11, Color.white), Border(11), overwrite);
                WriteSprite("rrect_r13", CreateRoundedRect(CanvasSize, 13, Color.white), Border(13), overwrite);
                WriteSprite("rrect_r15", CreateRoundedRect(CanvasSize, 15, Color.white), Border(15), overwrite);
                // .jcard 배경 — linear-gradient(180deg, panel2, panel)를 그레이스케일 곱연산으로 재현.
                // 소비측이 Image.color = UiKit.Panel2(밝은 쪽, top)로 틴트하면: 아래(bottom)=0.815×Panel2
                // ≈ Panel, 위(top)=1.0×Panel2 그대로 — panel_r24/card_grad와 동일한 "흰색 베이스 굽기" 관례.
                WriteSprite("card_grad_r15", CreateRoundedRectGradient(CanvasSize, 15, 0.815f, 1f), Border(15), overwrite);

                // ── S12a — ENGINE_PORT_DESIGN.md S12 §1(웹 단독판 토큰 이식). 전부 "w_" 접두 새
                // 파일명(overwrite:false 함정 회피 — 기존 파일은 안 건드린다). 흰색 베이스(w_r9~r22/
                // w_pill/w_gloss)는 소비측이 Image.color로 틴트하고, 나머지(w_reel/w_gold_btn/
                // w_ghost_btn/w_panel_grad/w_expfill/w_aurora/w_vignette)는 style.css 색 그대로 굽는다
                // (소비측은 틴트 없이 흰색으로 사용).
                //
                // ⚠️ 9-slice border는 Sprite 임포트 메타데이터에 고정되어 인스턴스별로 다르게 줄 수
                // 없다 — 파일 하나 = 반경 하나. 이번 슬라이스(S12a, TitleView·MenuView)가 실제로 쓰는
                // 반경을 기준으로 굽고, 다른 반경이 필요한 화면은 별도 파일(w_pill 등)을 대신 쓰거나
                // 아래 주석에 충돌을 남겨 Fable에게 보고한다(설계 §7 "충돌 시 보고").
                WriteSprite("w_r9", CreateRoundedRect(CanvasSize, UiKit.R9, Color.white), Border(UiKit.R9), overwrite);
                WriteSprite("w_r12", CreateRoundedRect(CanvasSize, UiKit.R12, Color.white), Border(UiKit.R12), overwrite);
                WriteSprite("w_r16", CreateRoundedRect(CanvasSize, UiKit.R16, Color.white), Border(UiKit.R16), overwrite);
                WriteSprite("w_r18", CreateRoundedRect(CanvasSize, UiKit.R18, Color.white), Border(UiKit.R18), overwrite);
                WriteSprite("w_r22", CreateRoundedRect(CanvasSize, UiKit.R22, Color.white), Border(UiKit.R22), overwrite);
                // pill(반경 999 → 캔버스 절반 128px border, 인스턴스 높이에 맞춰 자동으로 스타디움
                // 모양이 된다 — 기존 chip_r999와 동일한 공식). 흰색 베이스지만 상단이 살짝 더 밝은
                // 세로 그라데이션으로 구워서, 골드로 틴트했을 때(TitleView .intro-start) 2-스톱
                // 그라데이션 느낌이 함께 난다 — w_gold_btn은 r-lg(16, .bigbtn/.spinbtn 다수결)로 굽고
                // pill 반경이 필요한 유일한 버튼(.intro-start)은 이 w_pill을 금색 틴트로 대신 쓴다
                // (한 PNG=한 반경 제약 때문의 재해석, 보고 대상).
                WriteSprite("w_pill", CreateRoundedRectGradient(CanvasSize, Center, 0.86f, 1f), Border(Center), overwrite);
                // 실제 pill 버튼용(높이 128 기준) — w_pill은 border 128이라 높이 120짜리 버튼에 9-slice로
                // 붙이면 좌우 경계가 겹쳐 타원으로 찌그러진다. 반경/경계를 64로 낮춰 높이 128 버튼에서
                // 위아래 경계 합(128)이 정확히 맞아떨어지게 한 전용 스프라이트.
                WriteSprite("w_pill_btn", CreateRoundedRectGradient(CanvasSize, 64f, 0.86f, 1f), Border(64f), overwrite);
                // 상단 광택 오버레이 — 흰 14%→투명 세로 그라데이션, 텍스처 전체(0~100%)에 굽는다.
                // 소비측이 부모 카드/칩/릴의 "상단 40~50%" 영역 크기로 Type.Simple(비-9slice)로 늘려
                // 붙인다 — uGUI엔 부모의 둥근 모서리에 자동으로 클리핑되는 마스크가 없어(§7 재해석
                // 규칙) 낮은 알파(.14) 그라데이션이라 사각 모서리가 거의 티 나지 않는 것으로 대신한다.
                WriteSprite("w_gloss", CreateTopGloss(CanvasSize), Vector4.zero, overwrite);
                // 릴 셀 배경 165° 그라데이션(상단 #2a3354→48% #1a2038→하단 #10162a). 반경은 이번
                // 슬라이스(TitleView §3, 118×152 타일)가 쓰는 27(14px×1.9)로 굽는다. ⚠️RunView(§5,
                // S12b)는 같은 파일명을 r-xl(18)로 쓰라고 되어 있어 반경이 어긋난다 — S12b 착수 시
                // Fable 판단 필요(보고 대상, 그때 별도 파일로 분리하거나 이 반경을 조정해야 한다).
                WriteSprite("w_reel", CreateGradientRoundedRect(CanvasSize, 27,
                    new[] { (0f, ParseHex("#2a3354")), (0.48f, ParseHex("#1a2038")), (1f, ParseHex("#10162a")) }),
                    Border(27), overwrite);
                // 골드 버튼 세로 그라데(#ffe680→#f59e0b) — r-lg(16, .bigbtn/.spinbtn 다수결. 위 w_pill
                // 주석 참조).
                WriteSprite("w_gold_btn", CreateGradientRoundedRect(CanvasSize, UiKit.R16,
                    new[] { (0f, ParseHex("#ffe680")), (1f, ParseHex("#f59e0b")) }),
                    Border(UiKit.R16), overwrite);
                // 고스트(보조) 버튼 세로 그라데(panel3→panel2) — .bigbtn.ghost 반경 그대로 r-lg(16).
                WriteSprite("w_ghost_btn", CreateGradientRoundedRect(CanvasSize, UiKit.R16,
                    new[] { (0f, ParseHex("#252c46")), (1f, ParseHex("#1c2238")) }),
                    Border(UiKit.R16), overwrite);
                // 패널(.hud) 세로 그라데(panel2→panel) — r-xl(18), MenuView/RunView 공용(반경 충돌 없음).
                WriteSprite("w_panel_grad", CreateGradientRoundedRect(CanvasSize, UiKit.R18,
                    new[] { (0f, ParseHex("#1c2238")), (1f, ParseHex("#161a2c")) }),
                    Border(UiKit.R18), overwrite);
                // EXP 바 채움 가로 그라데(#f59e0b→70% gold→#fff6c0) — r-pill(999→128, expbar-wrap과 동일
                // 공식). S12a는 소비하지 않지만(RunView는 S12b) §1 표는 "전체 생성" 단계라 지금 굽는다.
                WriteSprite("w_expfill", CreateGradientRoundedRect(CanvasSize, Center,
                    new[] { (0f, ParseHex("#f59e0b")), (0.7f, ParseHex("#ffd23f")), (1f, ParseHex("#fff6c0")) },
                    horizontal: true),
                    Border(Center), overwrite);

                // 배경 오로라/비네트 — 256 캔버스가 아니라 1080×1920 전체 화면 텍스처(9-slice 아님,
                // Type.Simple로 늘려 붙인다). body::before의 "inset:-20%"(뷰포트보다 넓게 잡아 애니메이션
                // 중 가장자리가 비지 않게 하는 여백)는 재현하지 않았다 — 대신 애니메이션 배율이 항상
                // ≥1.02이고 이동량이 작아(§7 재해석) 실사용에서 가장자리가 드러나지 않는다.
                WriteFullSprite("w_aurora", CreateAuroraTexture(1080, 1920), overwrite, 2048);
                WriteFullSprite("w_vignette", CreateVignetteTexture(1080, 1920), overwrite, 2048);
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

        // S12a — w_aurora/w_vignette처럼 256 캔버스보다 큰(1080×1920) 전체 화면 텍스처 저장 경로.
        // border 없음(Type.Simple 소비 전제), maxTextureSize를 호출자가 지정한다(WriteSprite는
        // CanvasSize=256 고정이라 이 크기의 텍스처를 그대로 넣으면 임포트 시 다운스케일된다).
        private static void WriteFullSprite(string fileName, Texture2D tex, bool overwrite, int maxTextureSize)
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
            importer.spriteBorder = Vector4.zero;
            importer.maxTextureSize = maxTextureSize;
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

        // ══════════════════════════════════════════════════════════════════════════════
        // S12a — 다중 스톱 그라데이션 + 라운드 사각(w_reel/w_gold_btn/w_ghost_btn/w_panel_grad/
        // w_expfill 공용) · 상단 광택 오버레이 · 오로라/비네트 배경
        // ══════════════════════════════════════════════════════════════════════════════

        /// <summary>CSS linear-gradient 스타일의 다중 스톱 그라데이션을 라운드 사각 알파 마스크 위에
        /// 굽는다. stops는 (0=시작 쪽, 1=끝 쪽) 위치의 (pct, color) 목록 — vertical이면 0=상단/1=하단
        /// (CSS 180deg 관용과 동일), horizontal이면 0=좌측/1=우측(CSS 90deg 관용과 동일).</summary>
        private static Texture2D CreateGradientRoundedRect(int size, float radius, (float pct, Color color)[] stops,
            bool horizontal = false)
        {
            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float coverage = Mathf.Clamp01(0.5f - sdf);
                    float pos = horizontal ? (x + 0.5f) / size : 1f - (y + 0.5f) / size;
                    Color c = SampleStops(stops, pos);
                    c.a = coverage;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Color SampleStops((float pct, Color color)[] stops, float t)
        {
            if (stops == null || stops.Length == 0) return Color.white;
            if (t <= stops[0].pct) return stops[0].color;
            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t <= stops[i + 1].pct)
                {
                    float span = stops[i + 1].pct - stops[i].pct;
                    float local = span > 0.0001f ? (t - stops[i].pct) / span : 0f;
                    return Color.Lerp(stops[i].color, stops[i + 1].color, local);
                }
            }
            return stops[stops.Length - 1].color;
        }

        // 상단 광택(--gloss) 오버레이 — 흰 14%→투명 세로 그라데이션, 라운딩 없는 전체 사각(소비측이
        // Type.Simple로 원하는 높이만큼 늘려 쓴다).
        private static Texture2D CreateTopGloss(int size)
        {
            var tex = NewTex(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float fromTop = 1f - y / (float)(size - 1);
                float alpha = 0.14f * (1f - fromTop);
                var c = new Color(1f, 1f, 1f, alpha);
                for (int x = 0; x < size; x++) pixels[y * size + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // body::before 오로라 — 4개 방사 그라데이션(보라 22%/8% .28 · 핑크 82%/4% .22 · 민트 50%/102%
        // .16 · 파랑 50%/40% .10) + 세로 bg1→bg0(60%). CSS radial-gradient(Wx Hy at Xx Yy, color, transparent
        // stop%)를 "중심에서 stop*반경까지 선형 페이드"로 근사(2-스톱 그라데이션과 수학적으로 동일).
        // 페인트 순서는 CSS의 첫 레이어가 맨 위(나중에 그림)라 linear→blue→teal→pink→purple 순.
        private static Texture2D CreateAuroraTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            Color bg1 = ParseHex("#0e1020"), bg0 = ParseHex("#07080f");

            (float wPct, float hPct, float xPct, float yPct, Color color, float stop)[] radials =
            {
                (60f, 50f, 50f, 40f, new Color(91f / 255f, 155f / 255f, 255f / 255f, 0.10f), 0.70f), // blue(맨 아래)
                (50f, 40f, 50f, 102f, new Color(46f / 255f, 230f / 255f, 200f / 255f, 0.16f), 0.60f), // teal
                (42f, 32f, 82f, 4f, new Color(255f / 255f, 110f / 255f, 199f / 255f, 0.22f), 0.60f), // pink
                (40f, 30f, 22f, 8f, new Color(176f / 255f, 123f / 255f, 255f / 255f, 0.28f), 0.60f), // purple(맨 위)
            };

            for (int y = 0; y < h; y++)
            {
                float fromTop = 1f - y / (float)(h - 1);
                float lt = Mathf.Clamp01(fromTop / 0.6f); // 0(상단,bg1)→1(60%지점부터 bg0로 고정)
                Color baseColor = Color.Lerp(bg1, bg0, lt);
                for (int x = 0; x < w; x++)
                {
                    Color c = baseColor;
                    foreach (var r in radials)
                    {
                        float cx = r.xPct / 100f * w;
                        float cyFromTop = r.yPct / 100f * h;
                        float radX = r.wPct / 100f * w;
                        float radY = r.hPct / 100f * h;
                        if (radX <= 0f || radY <= 0f) continue;
                        float dx = (x + 0.5f - cx) / radX;
                        float dyFromTop = (fromTop * h) - cyFromTop;
                        float dy = dyFromTop / radY;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = d < r.stop ? r.color.a * (1f - d / r.stop) : 0f;
                        if (a > 0f) c = Color.Lerp(c, new Color(r.color.r, r.color.g, r.color.b, 1f), a);
                    }
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // body::after 비네트 — radial-gradient(120% 80% at 50% 0%, transparent 55%, rgba(0,0,0,.55)).
        // 상단 중앙은 투명(55% 반경까지), 그 밖은 55%→100% 구간에서 검정 55% 알파까지 선형 증가,
        // 100% 밖은 클램프(마지막 스톱 색 유지) — 실제 알파가 들어간 텍스처(오로라 위에 얹는 용도).
        private static Texture2D CreateVignetteTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            const float radXPct = 120f, radYPct = 80f, stop0 = 0.55f, endAlpha = 0.55f;
            float radX = radXPct / 100f * w;
            float radY = radYPct / 100f * h;
            for (int y = 0; y < h; y++)
            {
                float fromTop = (1f - y / (float)(h - 1)) * h; // 상단 원점 기준 절대 픽셀
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w / 2f) / radX;
                    float dy = fromTop / radY;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (d <= stop0) alpha = 0f;
                    else alpha = endAlpha * Mathf.Clamp01((d - stop0) / (1f - stop0));
                    pixels[y * w + x] = new Color(0f, 0f, 0f, alpha);
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

        // ══════════════════════════════════════════════════════════════════════════════
        // S8 항목⑤ — 심볼 타일(둥근 사각형 배경 + 도형 오버레이) 픽셀 드로잉
        // ══════════════════════════════════════════════════════════════════════════════

        /// <summary>배경 둥근 사각형(반경32, bg색)을 굽고 그 위에 id별 도형을 흰색(밝은 배경은 어두운색)
        /// 으로 얹는다. Blend()가 배경의 알파(둥근 코너 밖=0)를 그대로 유지하므로 도형은 자동으로
        /// 타일 실루엣 안쪽으로 클립된다.</summary>
        private static Texture2D CreateSymbolTile(int size, string id, Color bg)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, 32f);
                    float coverage = Mathf.Clamp01(0.5f - sdf);
                    var c = bg;
                    c.a = bg.a * coverage;
                    px[y * size + x] = c;
                }
            }

            // 배경 밝기(0..1, Rec.601 근사)로 도형 색을 흰/어둠 중 대비가 큰 쪽으로 자동 선택.
            float luminance = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            Color fg = luminance > 0.6f ? new Color(0.11f, 0.13f, 0.22f) : Color.white; // 어두운 남색 vs 흰색

            DrawSymbolShape(px, size, id, fg, bg);

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void DrawSymbolShape(Color32[] px, int size, string id, Color fg, Color bg)
        {
            switch (id)
            {
                case "cherry": DrawCherry(px, size, fg); break;
                case "book": DrawBook(px, size, fg, bg); break;
                case "star": DrawStar(px, size, fg); break;
                case "gem": DrawGem(px, size, fg); break;
                case "crown": DrawCrown(px, size, fg); break;
                case "skull": break; // 설계 지시: "skull=기존" — 배경 타일 그대로 유지, 도형 없음.
                case "coin": DrawCoin(px, size, fg, bg); break;
                case "flame": DrawFlame(px, size, fg); break;
                case "magnet": DrawMagnet(px, size, fg, bg); break;
                case "bomb": DrawBomb(px, size, fg); break;
                case "dice": DrawDice(px, size, fg, bg); break;
                case "seed": DrawSeed(px, size, fg, bg); break;
                case "wild": DrawWild(px, size, fg); break;
                case "key": DrawKey(px, size, fg, bg); break;
                default: break;
            }
        }

        // ── 도형별 그리기(설계 지시 도형 그대로: cherry=원2개+줄기 · book=책 모양 · gem=다이아 ·
        // coin=원+테두리 · crown=사다리꼴+삼각3 · star=별 · flame=물방울 · magnet=U자 · bomb=원+심지 ·
        // dice=사각+점 · seed=잎 · wild=나선 · key=열쇠) ──────────────────────────────────

        private static void DrawCherry(Color32[] px, int size, Color fg)
        {
            PlotLine(px, size, new Vector2(96, 150), new Vector2(130, 208), 9f, fg);
            PlotLine(px, size, new Vector2(160, 150), new Vector2(130, 208), 9f, fg);
            PlotCircle(px, size, 92, 118, 34, fg);
            PlotCircle(px, size, 156, 108, 34, fg);
        }

        private static void DrawBook(Color32[] px, int size, Color fg, Color bg)
        {
            PlotRect(px, size, 66, 62, 190, 194, fg);
            PlotRect(px, size, 122, 62, 134, 194, bg); // 책등(spine) 접힘선
            PlotRect(px, size, 80, 150, 176, 158, bg); // 페이지 줄 1
            PlotRect(px, size, 80, 118, 176, 126, bg); // 페이지 줄 2
        }

        private static void DrawStar(Color32[] px, int size, Color fg)
        {
            var pts = StarPoints(Center, Center, 92f, 38f, 5, -90f);
            PlotPolygon(px, size, pts, fg);
        }

        private static void DrawGem(Color32[] px, int size, Color fg)
        {
            var pts = new[]
            {
                new Vector2(Center, 202f), new Vector2(178f, 118f),
                new Vector2(Center, 54f), new Vector2(78f, 118f),
            };
            PlotPolygon(px, size, pts, fg);
        }

        private static void DrawCrown(Color32[] px, int size, Color fg)
        {
            // 사다리꼴(밴드)
            var band = new[]
            {
                new Vector2(80f, 60f), new Vector2(176f, 60f),
                new Vector2(190f, 100f), new Vector2(66f, 100f),
            };
            PlotPolygon(px, size, band, fg);
            // 삼각 3개(밴드 위로 솟은 첨탑) — y가 클수록 위쪽(텍스처 좌표계).
            PlotPolygon(px, size, new[] { new Vector2(88f, 100f), new Vector2(112f, 100f), new Vector2(100f, 170f) }, fg);
            PlotPolygon(px, size, new[] { new Vector2(116f, 100f), new Vector2(140f, 100f), new Vector2(128f, 196f) }, fg);
            PlotPolygon(px, size, new[] { new Vector2(144f, 100f), new Vector2(168f, 100f), new Vector2(156f, 170f) }, fg);
        }

        private static void DrawCoin(Color32[] px, int size, Color fg, Color bg)
        {
            PlotCircle(px, size, Center, Center, 76f, fg);
            PlotCircle(px, size, Center, Center, 58f, bg);
            PlotCircle(px, size, Center, Center, 32f, fg);
        }

        private static void DrawFlame(Color32[] px, int size, Color fg)
        {
            PlotCircle(px, size, Center, 104f, 56f, fg);
            PlotPolygon(px, size, new[] { new Vector2(Center, 216f), new Vector2(176f, 116f), new Vector2(80f, 116f) }, fg);
        }

        private static void DrawMagnet(Color32[] px, int size, Color fg, Color bg)
        {
            const float ringCx = Center, ringCy = 92f, rOuter = 62f, rInner = 36f;
            PlotCircle(px, size, ringCx, ringCy, rOuter, fg);
            PlotCircle(px, size, ringCx, ringCy, rInner, bg);
            PlotRect(px, size, 0, ringCy, size, size, bg); // 링 윗반쪽을 지워 U자 밑둥만 남긴다.
            PlotRect(px, size, ringCx - rOuter, 90f, ringCx - rInner, 196f, fg); // 왼쪽 다리
            PlotRect(px, size, ringCx + rInner, 90f, ringCx + rOuter, 196f, fg); // 오른쪽 다리
        }

        private static void DrawBomb(Color32[] px, int size, Color fg)
        {
            PlotCircle(px, size, Center, 100f, 64f, fg);
            PlotLine(px, size, new Vector2(168f, 148f), new Vector2(198f, 190f), 10f, fg);
            PlotCircle(px, size, 202f, 200f, 12f, fg);
        }

        private static void DrawDice(Color32[] px, int size, Color fg, Color bg)
        {
            PlotRect(px, size, 68, 68, 188, 188, fg);
            PlotCircle(px, size, 96, 96, 14f, bg);
            PlotCircle(px, size, Center, Center, 14f, bg);
            PlotCircle(px, size, 160, 160, 14f, bg);
        }

        private static void DrawSeed(Color32[] px, int size, Color fg, Color bg)
        {
            var leaf = new[]
            {
                new Vector2(Center, 210f), new Vector2(168f, 176f), new Vector2(180f, 128f),
                new Vector2(Center, 60f),
                new Vector2(76f, 128f), new Vector2(88f, 176f),
            };
            PlotPolygon(px, size, leaf, fg);
            PlotLine(px, size, new Vector2(Center, 200f), new Vector2(Center, 76f), 6f, bg);
        }

        private static void DrawWild(Color32[] px, int size, Color fg)
        {
            const int steps = 140;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = t * Mathf.PI * 2f * 1.6f;
                float radius = 8f + t * 76f;
                float x = Center + radius * Mathf.Cos(angle);
                float y = Center + radius * Mathf.Sin(angle);
                PlotCircle(px, size, x, y, 10f, fg);
            }
        }

        private static void DrawKey(Color32[] px, int size, Color fg, Color bg)
        {
            const float bowCx = 96f, bowCy = 168f;
            PlotCircle(px, size, bowCx, bowCy, 40f, fg);
            PlotCircle(px, size, bowCx, bowCy, 22f, bg);
            PlotRect(px, size, 92f, 60f, 116f, 172f, fg); // 축(shaft)
            PlotRect(px, size, 116f, 60f, 148f, 78f, fg); // 이빨 1
            PlotRect(px, size, 116f, 90f, 140f, 106f, fg); // 이빨 2
        }

        // ── 픽셀 드로잉 원시 도형(안티에일리어싱 포함) ─────────────────────────────────

        private static void PlotCircle(Color32[] px, int size, float cx, float cy, float r, Color fg)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - 2));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - 2));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r + 2));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov > 0f) Blend(px, size, x, y, fg, cov);
                }
            }
        }

        private static void PlotRect(Color32[] px, int size, float x0, float y0, float x1, float y1, Color fg)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(x0 - 2));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(x1 + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(y0 - 2));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(y1 + 2));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float dx = Mathf.Max(x0 - px0, px0 - x1);
                    float dy = Mathf.Max(y0 - py0, py0 - y1);
                    float d = Mathf.Max(dx, dy);
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov > 0f) Blend(px, size, x, y, fg, cov);
                }
            }
        }

        private static void PlotLine(Color32[] px, int size, Vector2 a, Vector2 b, float width, Color fg)
        {
            Vector2 dir = (b - a);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x) * (width / 2f);
            PlotPolygon(px, size, new[] { a + n, b + n, b - n, a - n }, fg);
        }

        // 짝수-홀수 규칙(ray casting) 점-폴리곤 판정 + 2x2 슈퍼샘플 안티에일리어싱.
        private static void PlotPolygon(Color32[] px, int size, Vector2[] pts, Color fg)
        {
            float minXf = float.MaxValue, maxXf = float.MinValue, minYf = float.MaxValue, maxYf = float.MinValue;
            foreach (var p in pts)
            {
                if (p.x < minXf) minXf = p.x;
                if (p.x > maxXf) maxXf = p.x;
                if (p.y < minYf) minYf = p.y;
                if (p.y > maxYf) maxYf = p.y;
            }
            int minX = Mathf.Max(0, Mathf.FloorToInt(minXf));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(maxXf));
            int minY = Mathf.Max(0, Mathf.FloorToInt(minYf));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(maxYf));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float cov = 0f;
                    cov += PointInPolygon(pts, x + 0.25f, y + 0.25f) ? 0.25f : 0f;
                    cov += PointInPolygon(pts, x + 0.75f, y + 0.25f) ? 0.25f : 0f;
                    cov += PointInPolygon(pts, x + 0.25f, y + 0.75f) ? 0.25f : 0f;
                    cov += PointInPolygon(pts, x + 0.75f, y + 0.75f) ? 0.25f : 0f;
                    if (cov > 0f) Blend(px, size, x, y, fg, cov);
                }
            }
        }

        private static bool PointInPolygon(Vector2[] pts, float px, float py)
        {
            bool inside = false;
            int n = pts.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = pts[i], pj = pts[j];
                bool crosses = (pi.y > py) != (pj.y > py);
                if (crosses)
                {
                    float xCross = (pj.x - pi.x) * (py - pi.y) / (pj.y - pi.y) + pi.x;
                    if (px < xCross) inside = !inside;
                }
            }
            return inside;
        }

        // 5각별 등 정다각 별 꼭짓점(바깥/안 반지름 교대) — angleOffsetDeg=-90은 첫 꼭짓점을 위쪽으로.
        private static Vector2[] StarPoints(float cx, float cy, float outerR, float innerR, int points, float angleOffsetDeg)
        {
            var pts = new Vector2[points * 2];
            float step = Mathf.PI / points;
            float start = angleOffsetDeg * Mathf.Deg2Rad;
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? outerR : innerR;
                float a = start + i * step;
                pts[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
            }
            return pts;
        }

        // 배경의 알파(둥근 코너 밖=0)를 보존한 채 RGB만 섞는다 — 그 결과 도형이 타일 실루엣 밖으로
        // 새지 않고 자동으로 클립된다.
        private static void Blend(Color32[] px, int size, int x, int y, Color fg, float coverage)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            int idx = y * size + x;
            Color baseC = px[idx];
            float t = Mathf.Clamp01(coverage) * fg.a;
            Color mixed = Color.Lerp(baseC, fg, t);
            mixed.a = baseC.a;
            px[idx] = mixed;
        }
    }
}
