using System.IO;
using JackpotRun.UI2;
using UnityEditor;
using UnityEngine;

namespace JackpotRun.EditorTools
{
    // 파티클 이펙트 에셋 절차 생성 — ENGINE_PORT_DESIGN.md S7c "파티클 에셋 생성(Editor/FxPrefabGen.cs)".
    // UiSpriteGen과 동일한 스타일(절차 텍스처 굽기 + 임포트 설정 + 결정론적 재실행)을 따르되, 대상이
    // PNG 스프라이트가 아니라 파티클 텍스처/머티리얼/프리팹이라는 점만 다르다.
    //
    // ── 산출물 3종 ───────────────────────────────────────────────────────────────────
    //   1) 텍스처 3종 → Assets/JackpotRun/Art/FX/*.png (dot_soft/star_soft/confetti)
    //   2) 머티리얼 2종 → Assets/JackpotRun/Art/FX/fx_add.mat · fx_alpha.mat
    //      (Shader.Find("Particles/Standard Unlit") 우선, 없으면 레거시 폴백 — 아래 FindParticleShader)
    //   3) 파티클 프리팹 15종(S13 §E에서 UI 발광 4종 추가) → Assets/JackpotRun/Resources/JackpotRun/FX/<id>.prefab
    //      (런타임 FxKit.cs가 Resources.Load<GameObject>로 지연 로드)
    //
    // ── "머티리얼 2종"과 텍스처 3종의 관계(구현 노트) ────────────────────────────────
    // 설계는 fx_add.mat/fx_alpha.mat 딱 2개만 명시한다. 두 머티리얼의 디스크 기본 텍스처는 dot_soft
    // (11종 중 다수가 소프트 도트를 쓰므로)로 굽고, star_soft/confetti가 필요한 프리팹(fx_jackpot의
    // confetti+중앙버스트, fx_clear의 별 낙하, fx_perk_pick)은 CloneWithTexture로 해당 머티리얼을
    // 복제해 텍스처만 바꿔 쓴다 — 이 복제본은 디스크에 별도 .mat로 저장하지 않고(Art/FX에는 여전히
    // 정확히 2개만 남는다) PrefabUtility.SaveAsPrefabAsset이 프리팹 파일 안에 서브 에셋으로 함께
    // 저장한다(런타임 코드 없이 씬/프리팹 단독으로 완결).
    public static class FxPrefabGen
    {
        public const string ArtDir = "Assets/JackpotRun/Art/FX";
        public const string PrefabDir = "Assets/JackpotRun/Resources/JackpotRun/FX";

        private const int DotSize = 64;
        private const int StarSize = 64;
        private const int ConfettiW = 8;
        private const int ConfettiH = 12;

        // 중력 가속도 환산 — ParticleSystem.MainModule.gravityModifier는 Physics.gravity.y(기본
        // -9.81)에 곱해지는 배율이다. 설계가 픽셀/초² 단위로 값을 주므로(예: "중력 400") 9.81로 나눠
        // 배율로 변환한다.
        private const float GravityUnit = 9.81f;

        // sortingOrder — S7c "sortingOrder" 규칙(앰비언트 99 / 일반 150 / 전체화면 250)의 프리팹별
        // 배정. 표가 예시로 명시한 "화면 전체 연출(잭팟/클리어)"에 게임오버(어두운 전면 패널 루프)를
        // 같은 계열로 묶었고, 상시 배경 앰비언트는 메뉴 먼지 하나뿐이라 99는 fx_menu_ambient 전용이다.
        // 나머지 게임플레이 부착형 이펙트는 전부 150(일반 연출) — 표에 개별 sortingOrder 열이 없어
        // 구현 시 정한 매핑이므로 보고 대상.
        private const int OrderAmbient = 99;
        private const int OrderNormal = 150;
        private const int OrderFullscreen = 250;

        private static Texture2D _texDot;
        private static Texture2D _texStar;
        private static Texture2D _texConfetti;
        private static Material _matAdd;
        private static Material _matAlpha;

        [MenuItem("JackpotRun/Generate FX Prefabs")]
        public static void GenerateAllMenuItem()
        {
            GenerateAll(overwrite: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[JackpotRun] FX 프리팹 생성 완료 — " + PrefabDir);
        }

        /// <summary>없는 파일만 생성(overwrite=false)하거나 전부 다시 굽는다(overwrite=true).
        /// UiSceneBuilder가 씬 빌드 파이프라인 한 단계로 호출할 수 있도록 공개.</summary>
        public static void GenerateAll(bool overwrite)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.StartAssetEditing();
            try
            {
                _texDot = WriteTexture("dot_soft", CreateSoftDot(DotSize), overwrite);
                _texStar = WriteTexture("star_soft", CreateSoftStar(StarSize), overwrite);
                _texConfetti = WriteTexture("confetti", CreateConfettiTex(ConfettiW, ConfettiH), overwrite);

                _matAdd = WriteMaterial("fx_add", additive: true, _texDot, overwrite);
                _matAlpha = WriteMaterial("fx_alpha", additive: false, _texDot, overwrite);

                SavePrefab(Build_SpinStop(), overwrite);
                SavePrefab(Build_SetHit(), overwrite);
                SavePrefab(Build_Jackpot(), overwrite);
                SavePrefab(Build_ExpGain(), overwrite);
                SavePrefab(Build_Coin(), overwrite);
                SavePrefab(Build_Clear(), overwrite);
                SavePrefab(Build_Boss(), overwrite);
                SavePrefab(Build_Skull(), overwrite);
                SavePrefab(Build_PerkPick(), overwrite);
                SavePrefab(Build_GameOver(), overwrite);
                SavePrefab(Build_MenuAmbient(), overwrite);

                // S13 §E — UI 발광 파티클 4종(ENGINE_PORT_DESIGN.md S13 §E 표, 새 파일명).
                SavePrefab(Build_UiAura(), overwrite);
                SavePrefab(Build_TitleSpark(), overwrite);
                SavePrefab(Build_BtnPress(), overwrite);
                SavePrefab(Build_CardPick(), overwrite);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        // ── 프리팹 11종(S7c) + UI 발광 4종(S13 §E) ──────────────────────────────────────
        // 트리거/사양 주석은 ENGINE_PORT_DESIGN.md S7c/S13 §E 표를 그대로 옮긴 것.

        // fx_spin_stop — 릴 셀 정지마다. 스파크 8개, 0.25s, 셀 크기 방사, 심볼색(런타임 startColor 주입), size 10~18.
        private static GameObject Build_SpinStop()
        {
            var go = NewRoot("fx_spin_stop", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(120f, 260f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startColor = Color.white; // 런타임 tint(심볼색)로 덮어쓰는 것을 전제로 한 기본값
            main.maxParticles = 16;

            SetBurst(ps, 0f, 8);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 50f; // "셀 크기 방사" 근사(릴 셀 폭의 절반 수준)
            shape.radiusThickness = 0f; // 원 가장자리에서만 방출 → 셀 테두리에서 바깥으로 튀는 느낌

            SizeShrink(ps, 1f, 0.2f);
            FadeOut(ps);
            return go;
        }

        // fx_set_hit — 세트 3/4 성립. 링 확산(shape Circle radius 60, burst 14) + 반짝, 0.4s, 골드.
        private static GameObject Build_SetHit()
        {
            var go = NewRoot("fx_set_hit", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(150f, 320f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startColor = UiKit.TierGold;
            main.maxParticles = 20;

            SetBurst(ps, 0f, 14);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 60f; // 설계 명시값
            shape.radiusThickness = 0f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.1f))); // "반짝"(급성장 후 소멸)

            FadeOut(ps);
            return go;
        }

        // fx_jackpot — 전칸 일치. 컨페티 80개(중력 400, 회전, confetti 텍스처, 1.4s)
        //            + 중앙 방사 버스트 30개(가산, 골드/화이트).
        private static GameObject Build_Jackpot()
        {
            var confettiMat = CloneWithTexture(_matAlpha, _texConfetti, "confetti");
            var go = NewRoot("fx_jackpot", confettiMat, OrderFullscreen);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(250f, 550f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(10f, 16f); // confetti 텍스처(8x12) 비율 근사
            main.startSizeY = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f, 1f);
            main.startColor = new ParticleSystem.MinMaxGradient(UiKit.TierGold, Color.white);
            main.gravityModifier = 400f / GravityUnit; // "중력 400"(px/s²)
            main.maxParticles = 100;

            SetBurst(ps, 0f, 80);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 20f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 콘 기본 +Z를 +Y(위쪽)로 회전 → 위로 솟구쳤다 중력으로 낙하

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-360f, 360f); // "회전"

            FadeOut(ps);

            // 중앙 방사 버스트(가산, 골드/화이트) — 다른 텍스처(star_soft)+블렌드(가산) 조합이라 confetti
            // 시스템과는 별도 ParticleSystem이 필요. ps.Play(true)/Stop(true)가 자식까지 자동 전파한다.
            var burstMat = CloneWithTexture(_matAdd, _texStar, "jackpot_burst");
            var childGo = AddChild(go, "CenterBurst", burstMat, OrderFullscreen);
            var childPs = childGo.GetComponent<ParticleSystem>();

            var cmain = childPs.main;
            cmain.duration = 0.6f;
            cmain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            cmain.startSpeed = new ParticleSystem.MinMaxCurve(300f, 600f);
            cmain.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            cmain.startColor = new ParticleSystem.MinMaxGradient(UiKit.TierGold, Color.white);
            cmain.maxParticles = 40;

            SetBurst(childPs, 0f, 30);

            var cshape = childPs.shape;
            cshape.enabled = true;
            cshape.shapeType = ParticleSystemShapeType.Sphere;
            cshape.radius = 10f;

            SizeShrink(childPs, 1f, 0.2f);
            FadeOut(childPs);

            return go;
        }

        // fx_exp_gain — EXP 바 채움. 바 끝점에서 흐르는 트레일 12개/초, 0.4s, 시안(#34D3C0).
        private static GameObject Build_ExpGain()
        {
            var go = NewRoot("fx_exp_gain", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startColor = UiKit.Good; // #34D3C0
            main.maxParticles = 12;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f; // "12개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 8f;
            shape.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(30f, 70f); // 바 진행 방향(+X)으로 흘러가는 트레일

            FadeInOut(ps);
            return go;
        }

        // fx_coin — 코인 획득. 코인색 도트 3~8개가 HUD 코인 라벨로 날아감(런타임 목표점 지정, 0.5s).
        // FxKit.PlayFlyTo가 shape 없이 원점에서 정확히 Emit(count)하고 GetParticles/SetParticles로
        // 직접 목표점까지 이동시키므로, 이 프리팹은 emission/shape를 모두 비활성해 자연 발생을 막는다.
        private static GameObject Build_Coin()
        {
            var go = NewRoot("fx_coin", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(12f, 18f);
            main.startColor = Hex("#E8B93C"); // UiSpriteGen의 sym_coin과 동일 색
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // FxKit.PlayFlyTo가 Emit(count)로 직접 발사

            var shape = ps.shape;
            shape.enabled = false; // 발사 지점 = 트랜스폼 원점(좌표 정확도 우선, 퍼짐 없음)

            FadeInOut(ps, 0.1f, 0.6f);
            return go;
        }

        // fx_clear — 스테이지 클리어. 별 낙하 40개(상단 라인 emitter, 1.2s) + 배너 뒤 광채 펄스.
        private static GameObject Build_Clear()
        {
            var starMat = CloneWithTexture(_matAdd, _texStar, "clear_star");
            var go = NewRoot("fx_clear", starMat, OrderFullscreen);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 20f);
            main.startColor = new ParticleSystem.MinMaxGradient(UiKit.TierGold, Color.white);
            main.gravityModifier = 150f / GravityUnit; // 완만한 낙하(설계에 수치 없음 — 구현 결정치)
            main.maxParticles = 50;

            SetBurst(ps, 0f, 40);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box; // "상단 라인 emitter"(가로로 얇은 박스)
            shape.scale = new Vector3(900f, 1f, 1f);
            shape.position = new Vector3(0f, 400f, 0f); // 앵커 중심에서 위쪽으로 오프셋

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            FadeOut(ps);

            // 배너 뒤 광채 펄스 — 큰 소프트 도트 1개가 사이즈를 오르내리며 은은히 유지(다른 텍스처라 별도 시스템).
            var glowMat = CloneWithTexture(_matAdd, _texDot, "clear_glow");
            var glowGo = AddChild(go, "GlowPulse", glowMat, OrderFullscreen);
            var gps = glowGo.GetComponent<ParticleSystem>();

            var gmain = gps.main;
            gmain.duration = 1.2f;
            gmain.startLifetime = 1.2f;
            gmain.startSpeed = 0f;
            gmain.startSize = 260f;
            gmain.startColor = new Color(UiKit.TierGold.r, UiKit.TierGold.g, UiKit.TierGold.b, 0.5f);
            gmain.maxParticles = 2;

            SetBurst(gps, 0f, 1);

            var gshape = gps.shape;
            gshape.enabled = false;

            var gsol = gps.sizeOverLifetime;
            gsol.enabled = true;
            gsol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.35f, 1f), new Keyframe(0.65f, 0.7f), new Keyframe(1f, 0.9f))); // 펄스

            FadeInOut(gps, 0.1f, 0.75f);
            return go;
        }

        // fx_boss — 보스 스테이지 진입. 붉은 잔불 상승 루프(HUD 테두리, 20개/초, 0.6 알파) — 스테이지 동안 유지.
        private static GameObject Build_Boss()
        {
            var go = NewRoot("fx_boss", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = new Color(1f, 0.3f, 0.15f, 0.6f); // 붉은 잔불, 알파 0.6
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 20f; // "20개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box; // "HUD 테두리" — boxThickness=0으로 테두리(껍질)만 방출
            shape.scale = new Vector3(720f, 160f, 0f); // HUD 바 크기 근사(설계에 수치 없음 — 구현 결정치)
            shape.boxThickness = Vector3.zero;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(30f, 60f); // 상승

            FadeInOut(ps);
            return go;
        }

        // fx_skull — 해골 페널티. 검은 연기 퍼프 6개, 0.5s, 알파블렌드, 상승.
        private static GameObject Build_Skull()
        {
            var go = NewRoot("fx_skull", _matAlpha, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(20f, 30f);
            main.startColor = new Color(0.1f, 0.1f, 0.12f, 0.75f); // 검은 연기(알파블렌드라 어두운 색도 정상 표시)
            main.maxParticles = 10;

            SetBurst(ps, 0f, 6);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 12f;
            shape.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(50f, 90f); // 상승

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f))); // 퍼프가 상승하며 퍼짐

            FadeOut(ps);
            return go;
        }

        // fx_perk_pick — 퍼크 선택. 카드 중심에서 티어색 스파클 24개 폭발, 0.6s.
        private static GameObject Build_PerkPick()
        {
            var mat = CloneWithTexture(_matAdd, _texStar, "perk_pick");
            var go = NewRoot("fx_perk_pick", mat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(200f, 350f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 20f);
            main.startColor = Color.white; // 런타임 tint(티어색)로 덮어쓰는 것을 전제로 한 기본값
            main.maxParticles = 30;

            SetBurst(ps, 0f, 24);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 5f;

            SizeShrink(ps, 1f, 0.15f);
            FadeOut(ps);
            return go;
        }

        // fx_gameover — 게임오버 패널. 재 낙하 30개/초 루프, 어두운 회색, 알파 0.4.
        private static GameObject Build_GameOver()
        {
            var go = NewRoot("fx_gameover", _matAlpha, OrderFullscreen);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startColor = new Color(0.45f, 0.45f, 0.48f, 0.4f); // 어두운 회색, 알파 0.4
            main.gravityModifier = 60f / GravityUnit; // 완만한 낙하(설계에 수치 없음 — 구현 결정치)
            main.maxParticles = 70;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 30f; // "30개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box; // 화면 상단 라인
            shape.scale = new Vector3(1080f, 1f, 1f);
            shape.position = new Vector3(0f, 900f, 0f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-60f, 60f);

            FadeInOut(ps);
            return go;
        }

        // fx_menu_ambient — 메뉴 화면 상시. 골드 먼지 상승 6개/초 루프, 알파 0.25, size 6~12.
        private static GameObject Build_MenuAmbient()
        {
            var go = NewRoot("fx_menu_ambient", _matAdd, OrderAmbient);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f); // 설계 명시값
            main.startColor = new Color(UiKit.TierGold.r, UiKit.TierGold.g, UiKit.TierGold.b, 0.25f); // 알파 0.25
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 6f; // "6개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Rectangle; // 메뉴 화면 전체에 걸쳐 상시 발생
            shape.scale = new Vector3(1000f, 1800f, 1f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(15f, 30f); // 상승

            FadeInOut(ps);
            return go;
        }

        // ── S13 §E: UI 발광 파티클 4종(표 사양 그대로, 새 파일명) ───────────────────────

        // fx_ui_aura — 버튼/카드 뒤 은은한 발광. 루프, 6개/초, 크기 40~90, 알파 .12, 매우 느린 상승,
        // 가산합성. 앵커(버튼/카드 RectTransform) 뒤에 깔리는 용도라 앵커 영역 전체에서 방출되도록
        // 원형 방출 반경을 넓게 잡는다(설계에 반경 수치 없음 — 버튼/카드 크기 근사, 구현 결정치).
        private static GameObject Build_UiAura()
        {
            var go = NewRoot("fx_ui_aura", _matAdd, OrderAmbient);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f); // "매우 느린 상승" — 오래 머문다
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(40f, 90f); // 설계 명시값
            main.startColor = new Color(1f, 1f, 1f, 0.12f); // 설계 명시 알파 .12(런타임 tint로 색만 덮어써도 알파는 유지)
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 6f; // "6개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 110f; // 버튼/카드 뒤 전체를 덮는 근사 반경(설계 미명시 — 구현 결정치)
            shape.radiusThickness = 1f;

            // x/z도 y와 같은 TwoConstants 모드로 명시(0~0으로 사실상 무영향) — Unity는 velocityOverLifetime의
            // x/y/z 커브가 서로 다른 모드로 섞이면 "Particle Velocity curves must all be in the same mode"
            // 경고를 낸다(y만 설정하면 x/z가 기본 Constant(0) 모드로 남아 발생).
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.y = new ParticleSystem.MinMaxCurve(4f, 10f); // "매우 느린 상승"
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            FadeInOut(ps);
            return go;
        }

        // fx_title_spark — 타이틀 릴 주변 반짝임. 루프, 4개/초, 별 텍스처, 크기 8~18, 알파 .5, 위로 천천히.
        private static GameObject Build_TitleSpark()
        {
            var mat = CloneWithTexture(_matAdd, _texStar, "title_spark");
            var go = NewRoot("fx_title_spark", mat, OrderAmbient);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 18f); // 설계 명시값
            main.startColor = new Color(1f, 1f, 1f, 0.5f); // 설계 명시 알파 .5
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 4f; // "4개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Rectangle; // 릴 타일 3개가 늘어선 가로 영역 전체
            shape.scale = new Vector3(420f, 220f, 1f); // 릴 3타일(118×3+gap) 폭 근사(설계 미명시 — 구현 결정치)

            var vol = ps.velocityOverLifetime; // x/z를 y와 같은 모드로 명시(위 fx_ui_aura 주석 참조)
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.y = new ParticleSystem.MinMaxCurve(8f, 18f); // "위로 천천히"
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            FadeInOut(ps);
            return go;
        }

        // fx_btn_press — 버튼 누를 때. 버스트 10, 0.35s, 방사, 골드.
        private static GameObject Build_BtnPress()
        {
            var go = NewRoot("fx_btn_press", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.35f; // 설계 명시값
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(140f, 260f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = UiKit.TierGold; // 설계 명시 "골드"
            main.maxParticles = 14;

            SetBurst(ps, 0f, 10); // 설계 명시값

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle; // "방사"
            shape.radius = 40f; // 버튼 절반 크기 근사(설계 미명시 — 구현 결정치)
            shape.radiusThickness = 0f;

            SizeShrink(ps, 1f, 0.2f);
            FadeOut(ps);
            return go;
        }

        // fx_card_pick — 카드 선택 시. 버스트 18, 0.5s, 티어색, 링 확산.
        private static GameObject Build_CardPick()
        {
            var go = NewRoot("fx_card_pick", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f; // 설계 명시값
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(180f, 340f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startColor = Color.white; // 런타임 tint(티어색)로 덮어쓰는 것을 전제로 한 기본값(설계 "티어색")
            main.maxParticles = 24;

            SetBurst(ps, 0f, 18); // 설계 명시값

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle; // "링 확산" — 가장자리에서만 방출
            shape.radius = 70f; // 카드 폭 절반 근사(설계 미명시 — 구현 결정치)
            shape.radiusThickness = 0f;

            SizeShrink(ps, 1f, 0.15f);
            FadeOut(ps);
            return go;
        }

        // ── 공용 빌더 헬퍼 ────────────────────────────────────────────────────────────

        private static GameObject NewRoot(string name, Material mat, int sortingOrder)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None; // 풀 재사용(FxKit이 직접 수명 관리)
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startRotation3D = false;
            main.startSize3D = false;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // 기본은 무발생 — 각 빌더가 burst 또는 rate로 재설정

            var shape = ps.shape;
            shape.enabled = false; // 기본 비활성 — 각 빌더가 필요 시 켠다

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            // sortingLayerName은 기본("Default") 유지 — 설계 지시대로 건드리지 않는다.
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = mat;
            return go;
        }

        private static GameObject AddChild(GameObject parent, string name, Material mat, int sortingOrder)
        {
            var child = NewRoot(name, mat, sortingOrder);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void SetBurst(ParticleSystem ps, float time, int count)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static void SizeShrink(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, from), new Keyframe(1f, to)));
        }

        /// <summary>버스트형(즉시 최대 알파 → 서서히 소멸) 페이드.</summary>
        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        /// <summary>연속 발생형(서서히 나타났다 서서히 사라짐) 페이드 — 루프/트레일용.</summary>
        private static void FadeInOut(ParticleSystem ps, float inFrac = 0.15f, float outStart = 0.7f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, inFrac),
                    new GradientAlphaKey(1f, outStart),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static void SavePrefab(GameObject go, bool overwrite)
        {
            string path = $"{PrefabDir}/{go.name}.prefab";
            if (!overwrite && File.Exists(path))
            {
                Object.DestroyImmediate(go);
                return;
            }
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ── 텍스처 생성 ──────────────────────────────────────────────────────────────

        private static Texture2D CreateSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size / 2f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep — 부드러운 방사 알파 그라데이션
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // 4각 스파클(중심 광 + 십자 빔) — 절차적 별 모양.
        private static Texture2D CreateSoftStar(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distC = Mathf.Sqrt(dx * dx + dy * dy);
                    float centerGlow = Mathf.Clamp01(1f - distC * 2.6f);
                    float beamH = Mathf.Clamp01(1f - Mathf.Abs(dy) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                    float beamV = Mathf.Clamp01(1f - Mathf.Abs(dx) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                    float a = Mathf.Clamp01(centerGlow + beamH * 0.85f + beamV * 0.85f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // 사각 조각(단색, 불투명) — startColor로 색을 입힌다.
        private static Texture2D CreateConfettiTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D WriteTexture(string fileName, Texture2D tex, bool overwrite)
        {
            string path = $"{ArtDir}/{fileName}.png";
            if (!overwrite && File.Exists(path))
            {
                Object.DestroyImmediate(tex);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default; // 파티클 머티리얼용(Sprite 아님)
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // 작은 텍스처라 압축 아티팩트 방지
                importer.maxTextureSize = 128;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ── 머티리얼 생성 ────────────────────────────────────────────────────────────

        private static Shader FindParticleShader(bool additive)
        {
            var s = Shader.Find("Particles/Standard Unlit");
            if (s != null) return s;
            return Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
        }

        private static Material WriteMaterial(string name, bool additive, Texture2D defaultTex, bool overwrite)
        {
            string path = $"{ArtDir}/{name}.mat";
            if (!overwrite && File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<Material>(path);

            var shader = FindParticleShader(additive);
            if (shader == null)
            {
                Debug.LogWarning($"[JackpotRun] FX 파티클 셰이더를 찾을 수 없습니다({name}) — Sprites/Default로 대체");
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null) return null;

            var mat = new Material(shader) { name = name };
            ConfigureBlend(mat, additive);
            if (defaultTex != null) mat.mainTexture = defaultTex;
            AddAlwaysIncludedShader(shader);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // "Particles/Standard Unlit"는 _SrcBlend/_DstBlend/_ZWrite/_Cull을 머티리얼 프로퍼티로 노출해
        // 코드로 블렌드 모드를 바꿀 수 있다. 레거시 폴백("Legacy Shaders/Particles/Additive|Alpha Blended")은
        // 블렌드가 셰이더 코드에 고정돼 있어 이 프로퍼티들이 아예 없다 — HasProperty 가드로 두 경로 모두 안전.
        private static void ConfigureBlend(Material mat, bool additive)
        {
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)(additive
                    ? UnityEngine.Rendering.BlendMode.One
                    : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
        }

        private static Material CloneWithTexture(Material baseMat, Texture2D tex, string tag)
        {
            if (baseMat == null) return null;
            if (tex == null) return baseMat;
            var clone = new Material(baseMat) { name = baseMat.name + "_" + tag };
            clone.mainTexture = tex;
            return clone;
        }

        // 빌드에서 스트립되지 않도록 GraphicsSettings의 "Always Included Shaders"에 등록.
        private static void AddAlwaysIncludedShader(Shader shader)
        {
            if (shader == null) return;
            var settingsObj = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (settingsObj == null) return;

            var so = new SerializedObject(settingsObj);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;

            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return; // 이미 등록됨
            }

            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
