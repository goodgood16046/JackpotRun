using System.Collections.Generic;
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
    //   1) 텍스처 7종 → Assets/JackpotRun/Art/FX/*.png
    //      (dot_soft/star_soft/confetti — S7c, p_star4/p_ring/p_shard/p_coin — S15 §B 신규)
    //   2) 머티리얼 2종 → Assets/JackpotRun/Art/FX/fx_add.mat · fx_alpha.mat
    //      (Shader.Find("Particles/Standard Unlit") 우선, 없으면 레거시 폴백 — 아래 FindParticleShader)
    //   3) 파티클 프리팹 23종(S13 §E UI 발광 4종 · S14 §F 연출강화 3종 · S15 §B 파티클 재작업 5종
    //      순차 추가) → Assets/JackpotRun/Resources/JackpotRun/FX/<id>.prefab
    //      (런타임 FxKit.cs가 Resources.Load<GameObject>로 지연 로드)
    //
    // ── "머티리얼 2종"과 텍스처 7종의 관계(구현 노트) ────────────────────────────────
    // 설계는 fx_add.mat/fx_alpha.mat 딱 2개만 명시한다. 두 머티리얼의 디스크 기본 텍스처는 dot_soft
    // (다수 프리팹이 소프트 도트를 쓰므로)로 굽고, 다른 텍스처(star_soft/confetti/p_star4/p_ring/
    // p_shard/p_coin)가 필요한 프리팹은 CloneWithTexture로 해당 머티리얼을 복제해 텍스처만 바꿔 쓴다
    // — 이 복제본은 디스크에 별도 .mat로 저장하지 않고(Art/FX에는 여전히 정확히 2개만 남는다)
    // PrefabUtility.SaveAsPrefabAsset이 프리팹 파일 안에 서브 에셋으로 함께 저장한다(런타임 코드 없이
    // 씬/프리팹 단독으로 완결). p_dot은 설계가 "기존 dot_soft 재사용 가능"이라 명시해 별도 파일 없이
    // _texDot을 그대로 쓴다.
    //
    // ── S15 §B 파티클 전면 재작업(핵심 슬라이스) ─────────────────────────────────────
    // 사용자 피드백 "이펙트가 너무 단조롭다 — 파티클로 제대로 구현하라"에 대한 응답
    // (ENGINE_PORT_DESIGN.md S15 §B 표). 품질 규칙(sizeOverLifetime 0→1→0 · colorOverLifetime 알파+색
    // 그라데이션 · rotationOverLifetime · velocityOverLifetime x/y/z 동일 모드 · 필요 시 TrailModule ·
    // 다단계 버스트 · maxParticles 명시 · Outline/플래시 단독 금지)을 아래 공용 헬퍼
    // (SizeGrowShrink/FadeOutTinted/SetBursts/ZeroVelocityXZ/ConfigureRingWave/ConfigureTrail)로
    // 구현하고, 표의 11개 상황 각각을 레이어드(부모+자식 ParticleSystem) 구성으로 짠다. "위 +"로
    // 표시된 누적 관계(2매치⊂3매치⊂4매치)는 BuildCellBurstFx(withTrail) 하나로 fx_match2/fx_set_hit
    // 둘 다 만들어 코드로도 그 포함 관계를 그대로 드러낸다.
    public static class FxPrefabGen
    {
        public const string ArtDir = "Assets/JackpotRun/Art/FX";
        public const string PrefabDir = "Assets/JackpotRun/Resources/JackpotRun/FX";

        private const int DotSize = 64;
        private const int StarSize = 64;
        private const int ConfettiW = 8;
        private const int ConfettiH = 12;
        private const int Star4Size = 64;
        private const int RingSize = 64;
        private const int ShardSize = 48;
        private const int CoinSize = 64;

        // 중력 — SetGravityPx 참조. main.gravityModifier(월드 공간)는 쓰지 않는다.

        // sortingOrder — S7c "sortingOrder" 규칙(앰비언트 99 / 일반 150 / 전체화면 250)의 프리팹별
        // 배정. 표가 예시로 명시한 "화면 전체 연출(잭팟/클리어)"에 게임오버(어두운 전면 패널 루프)를
        // 같은 계열로 묶었고, 상시 배경 앰비언트는 메뉴 먼지·런 화면 먼지 둘뿐이라 99는 그 둘 전용이다.
        // 나머지 게임플레이 부착형 이펙트는 전부 150(일반 연출) — 표에 개별 sortingOrder 열이 없어
        // 구현 시 정한 매핑이므로 보고 대상.
        private const int OrderAmbient = 99;
        private const int OrderNormal = 150;
        private const int OrderFullscreen = 250;

        // S15 §B — 스파크/파편 계열의 "색 그라데이션"(colorOverLifetime) 기본 중간색: 하얗게 튄
        // 스파크가 식어가는 앰버 톤을 거쳐 사라진다(질감의 "터지는 맛" 담당, FadeOutTinted 참조).
        private static readonly Color EmberMid = new Color(1f, 0.62f, 0.28f);
        private static readonly Color MaroonMid = new Color(0.4f, 0.08f, 0.08f);

        private static Texture2D _texDot;
        private static Texture2D _texStar;
        private static Texture2D _texConfetti;
        private static Texture2D _texStar4;
        private static Texture2D _texRing;
        private static Texture2D _texShard;
        private static Texture2D _texCoin;
        private static Material _matAdd;
        private static Material _matAlpha;
        private static string _matDir = ArtDir + "/mats";
        private static readonly Dictionary<string, Material> _cloneCache = new Dictionary<string, Material>();

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

            // ⚠️ 텍스처 생성은 StartAssetEditing "밖"에서 해야 한다. 배치 편집 구간 안에서는 방금 쓴
            // PNG가 아직 임포트되지 않아 LoadAssetAtPath가 null을 돌려주고, 그 null이 머티리얼에
            // 그대로 들어가 파티클이 텍스처 없는 흰 사각형("텍스처 덩어리")으로 보였다(2026-08-01).
            _texDot = WriteTexture("dot_soft", CreateSoftDot(DotSize), overwrite);
            _texStar = WriteTexture("star_soft", CreateSoftStar(StarSize), overwrite);
            _texConfetti = WriteTexture("confetti", CreateConfettiTex(ConfettiW, ConfettiH), overwrite);

            // S15 §B — 공통 재료 4종(새 파일명, p_dot은 dot_soft 재사용이라 파일 없음).
            _texStar4 = WriteTexture("p_star4", CreateStar4Tex(Star4Size), overwrite);
            _texRing = WriteTexture("p_ring", CreateRingTex(RingSize), overwrite);
            _texShard = WriteTexture("p_shard", CreateShardTex(ShardSize), overwrite);
            _texCoin = WriteTexture("p_coin", CreateCoinTex(CoinSize), overwrite);
            AssetDatabase.Refresh();

            // 임포트 완료 후 다시 로드 — WriteTexture가 배치 중이 아니어도 방어적으로 한 번 더 확인한다.
            _texDot = _texDot != null ? _texDot : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/dot_soft.png");
            _texStar = _texStar != null ? _texStar : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/star_soft.png");
            _texConfetti = _texConfetti != null ? _texConfetti : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/confetti.png");
            _texStar4 = _texStar4 != null ? _texStar4 : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/p_star4.png");
            _texRing = _texRing != null ? _texRing : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/p_ring.png");
            _texShard = _texShard != null ? _texShard : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/p_shard.png");
            _texCoin = _texCoin != null ? _texCoin : AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/p_coin.png");

            // ⚠️ 머티리얼/프리팹 생성도 배치 편집 밖에서 한다. CloneWithTexture가 만드는 파생 머티리얼은
            // .mat 에셋으로 저장돼야 프리팹이 GUID로 참조할 수 있는데, StartAssetEditing 구간 안에서
            // CreateAsset한 에셋은 아직 임포트 전이라 프리팹 저장 시 참조가 끊겼다(머티리얼 = None →
            // 유니티 기본 파티클 머티리얼로 렌더 = 흰 사각형).
            _matDir = $"{ArtDir}/mats";
            Directory.CreateDirectory(_matDir);
            _cloneCache.Clear();

            _matAdd = WriteMaterial("fx_add", additive: true, _texDot, overwrite);
            _matAlpha = WriteMaterial("fx_alpha", additive: false, _texDot, overwrite);

            {
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

                // S14 §F — 연출 강화 신규 파티클 3종(새 파일명).
                SavePrefab(Build_ReelLand(), overwrite);
                SavePrefab(Build_Converge(), overwrite);
                SavePrefab(Build_JackpotRays(), overwrite);

                // S15 §B — 파티클 전면 재작업 신규 5종(새 파일명, ENGINE_PORT_DESIGN.md S15 §B 표).
                SavePrefab(Build_Match2(), overwrite);
                SavePrefab(Build_RisingLight(), overwrite);
                SavePrefab(Build_ConvergeBurst(), overwrite);
                SavePrefab(Build_CoinSpark(), overwrite);
                SavePrefab(Build_RunAmbient(), overwrite);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); // 방금 저장한 프리팹을 다시 임포트 — 캐시된 옛 인스턴스가 남지 않게.
        }

        // ── 프리팹 11종(S7c) + UI 발광 4종(S13 §E) + 연출강화 3종(S14 §F) + 파티클재작업 5종(S15 §B) ──
        // 트리거/사양 주석은 ENGINE_PORT_DESIGN.md 각 절 표를 그대로 옮긴 것.

        // fx_spin_stop — 릴 셀 정지마다(S15 §B 표 "릴 정지" 행). 레이어드:
        //   ① 링 웨이브 1개(자식 RingWave, p_ring, 스케일 0.2→1.4 근사, 0.25s, 알파 .7→0)
        //   ② 스파크 10개 방사(메인, 속도 300~600, 중력 200, 다단계 버스트 0s6+0.05s4)
        // ③ "먼지 6개 하단"은 별도 FxId.ReelLand로 같은 순간 함께 재생된다(ReelView.PlayLandingImpact).
        private static GameObject Build_SpinStop()
        {
            var sparkMat = CloneWithTexture(_matAdd, _texDot, "spin_stop_spark");
            var go = NewRoot("fx_spin_stop", sparkMat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(300f, 600f); // 설계 명시
            main.startSize = new ParticleSystem.MinMaxCurve(9f, 16f);
            main.startColor = Color.white; // 런타임 tint(심볼색)로 덮어쓰는 것을 전제로 한 기본값
            SetGravityPx(ps, 200f); // 설계 명시 "중력200"
            main.maxParticles = 20;

            SetBursts(ps, (0f, 6), (0.05f, 4)); // 설계 "스파크 10개" — 다단계로 겹쳐 "터지는 맛"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 50f; // "셀 크기 방사" 근사(릴 셀 폭의 절반 수준)
            shape.radiusThickness = 0f; // 원 가장자리에서만 방출 → 셀 테두리에서 바깥으로 튀는 느낌

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

            SizeGrowShrink(ps, 1f, 0.15f, 0f);
            FadeOutTinted(ps, EmberMid); // 흰빛 스파크가 앰버로 식으며 소멸(색 그라데이션 실질 적용)

            // 링 웨이브(자식) — "스케일 0.2→1.4"를 셀 지름 약 200px 기준 px로 환산(40→280).
            var ringMat = CloneWithTexture(_matAdd, _texRing, "spin_stop_ring");
            var ringGo = AddChild(go, "RingWave", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 40f, 280f, 0.25f, 0.7f, new[] { (0f, 1) });

            return go;
        }

        // fx_set_hit — 세트 3매치 이상 성립(S15 §B 표 "세트 3매치" = "위(2매치) + 트레일"). 2매치용
        // 레이어(파편12+링1+별4)에 TrailModule만 더한 구성 — BuildCellBurstFx(withTrail:true)가
        // fx_match2와 동일 골격을 공유해 이 "위 +" 누적 관계를 코드로도 드러낸다.
        private static GameObject Build_SetHit() => BuildCellBurstFx("fx_set_hit", withTrail: true);

        // fx_jackpot — 전칸 일치(S15 §B 표 "잭팟" 행). 레이어드:
        //   ⑨ 컨페티 80개(중력 400, 회전, confetti 텍스처, 1.4s) — 기존(S7c) 유지
        //   중앙 방사 버스트(가산, 골드/화이트, 다단계 20+10) — 기존(S7c) 유지, 다단계만 추가
        //   ⑧ 골드 코인비 60개(자식 CoinRain, p_coin, 중력+회전, 1.6s, 다단계 40+20)
        //   ⑪ 링 웨이브 3연발(자식 RingTriple, p_ring, 0.1s 간격)
        // ⑩ "방사 광선 회전"은 별도 FxId.JackpotRays(아래)가 같은 순간 함께 재생된다(ReelView.PostRevealFx).
        // ⑫ "화면 가장자리 상승 광입자"는 별도 FxId.RisingLight를 재해석해 겸용한다(잭팟도 hasSet 경로를
        // 타므로 PostRevealFx가 이미 함께 재생 — Build_RisingLight 주석 참조, 재해석 보고 대상).
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
            SetGravityPx(ps, 400f); // "중력 400"(px/s²)
            main.maxParticles = 100;

            SetBursts(ps, (0f, 80));

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

            SetBursts(childPs, (0f, 20), (0.06f, 10)); // 다단계(설계 품질 규칙)

            var cshape = childPs.shape;
            cshape.enabled = true;
            cshape.shapeType = ParticleSystemShapeType.Sphere;
            cshape.radius = 10f;

            SizeGrowShrink(childPs, 1f, 0.15f, 0f);
            FadeOut(childPs);

            // ⑧ 골드 코인비 60개(자식) — p_coin, 중력+회전, 1.6s, 다단계 40+20.
            var coinRainMat = CloneWithTexture(_matAdd, _texCoin, "jackpot_coinrain");
            var coinGo = AddChild(go, "CoinRain", coinRainMat, OrderFullscreen);
            var coinPs = coinGo.GetComponent<ParticleSystem>();

            var coinMain = coinPs.main;
            coinMain.duration = 1.6f; // 설계 명시
            coinMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
            coinMain.startSpeed = new ParticleSystem.MinMaxCurve(200f, 420f);
            coinMain.startSize = new ParticleSystem.MinMaxCurve(16f, 26f);
            coinMain.startColor = new ParticleSystem.MinMaxGradient(UiKit.TierGold, UiKit.Gold2);
            SetGravityPx(coinPs, 380f); // "중력"(수치 미명시 — confetti 400 근사)
            coinMain.maxParticles = 90;

            SetBursts(coinPs, (0f, 40), (0.15f, 20)); // 설계 "60개" — 다단계

            var coinShape = coinPs.shape;
            coinShape.enabled = true;
            coinShape.shapeType = ParticleSystemShapeType.Cone;
            coinShape.angle = 32f;
            coinShape.radius = 18f;
            coinShape.rotation = new Vector3(-90f, 0f, 0f);

            var coinRot = coinPs.rotationOverLifetime;
            coinRot.enabled = true;
            coinRot.z = new ParticleSystem.MinMaxCurve(-540f, 540f); // "회전"

            var coinSol = coinPs.sizeOverLifetime;
            coinSol.enabled = true;
            coinSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.12f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.4f)));
            FadeInOut(coinPs, 0.05f, 0.75f);

            // ⑪ 링 웨이브 3연발(자식) — 0.1s 간격, 화면을 가로지르는 큰 골드 링.
            var ringTripleMat = CloneWithTexture(_matAdd, _texRing, "jackpot_ringtriple");
            var ringTripleGo = AddChild(go, "RingTriple", ringTripleMat, OrderFullscreen);
            ConfigureRingWave(ringTripleGo.GetComponent<ParticleSystem>(), 60f, 520f, 0.5f, 0.55f,
                new[] { (0f, 1), (0.1f, 1), (0.2f, 1) }, UiKit.TierGold); // 설계 명시 "0.1s 간격" 3연발

            return go;
        }

        // fx_exp_gain — EXP 바 채움. 바 끝점에서 흐르는 트레일 12개/초, 0.4s, 시안(#34D3C0).
        // S15 §B 표 "EXP 획득" — 위 트레일 + 자식 ArriveRing("도착 시 작은 링", startDelay로 채움
        // 애니메이션(HudView.ExpGainDuration=0.3s) 끝 무렵에 맞춤).
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
            // 여기서 축을 맞출 대상은 y/z다. ZeroVelocityXZ를 쓰면 방금 넣은 x를 도로 (0,0)으로
            // 덮어쓰고 y만 Constant로 남아 "Particle Velocity curves must all be in the same mode"
            // 경고가 계속 났다(2026-08-01 콘솔에서 발견 — 트레일 속도도 함께 죽어 있었다).
            ZeroVelocityYZ(ps);

            SizeGrowShrink(ps, 1f, 0.2f, 0.5f);
            FadeInOut(ps);

            // ArriveRing(자식) — "도착 시 작은 링"(설계). startDelay를 채움 애니메이션 길이 근처로
            // 잡아 CountUpRoutine(0.3s)이 끝날 무렵 작게 반짝인다(설계 미명시 정확한 동기화 — 근사).
            var ringMat = CloneWithTexture(_matAdd, _texRing, "exp_ring");
            var ringGo = AddChild(go, "ArriveRing", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 10f, 70f, 0.25f, 0.6f,
                new[] { (0f, 1) }, UiKit.Good, startDelay: 0.26f);

            return go;
        }

        // fx_coin — 코인 획득. S15 §B 표 "코인 획득": p_coin 입자가 HUD로 포물선 비행(중력+회전),
        // 도착 시 스파크4. FxKit.PlayFlyTo가 매 프레임 위치를 직접 덮어써 목표점까지 옮기므로(코인은
        // arcHeight로 포물선을 근사 — 실제 gravityModifier는 무의미해 설정하지 않는다, FxKit.cs
        // FlyToRoutine 주석 참조) 이 프리팹은 emission/shape를 모두 비활성해 자연 발생을 막는다.
        // "도착 시 스파크4"는 FxId.CoinSpark를 PlayFlyTo의 arrivalBurst로 지정해(HudView.PlayCoinFx)
        // 정확한 도착 좌표(HUD 코인 라벨)에서 재생한다 — 별도 자식으로 묶으면 "from" 위치에 고정된
        // 트랜스폼 기준이라 도착 위치에서 재생할 수 없다(회전만은 이 프리팹 자체에서 담당).
        private static GameObject Build_Coin()
        {
            var coinMat = CloneWithTexture(_matAdd, _texCoin, "coin");
            var go = NewRoot("fx_coin", coinMat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.startColor = Hex("#E8B93C"); // UiSpriteGen의 sym_coin과 동일 색
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // FxKit.PlayFlyTo가 Emit(count)로 직접 발사

            var shape = ps.shape;
            shape.enabled = false; // 발사 지점 = 트랜스폼 원점(좌표 정확도 우선, 퍼짐 없음)

            var rot = ps.rotationOverLifetime; // "회전"(설계 명시) — 위치와 달리 정상 시뮬레이션된다.
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(120f, 320f);

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
            SetGravityPx(ps, 150f); // 완만한 낙하(설계에 수치 없음 — 구현 결정치)
            main.maxParticles = 50;

            SetBursts(ps, (0f, 40));

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

            SetBursts(gps, (0f, 1));

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
            ZeroVelocityXZ(ps); // S15 §B 품질 규칙 — x/z를 y와 같은 모드로(경고 제거, 기존 누락 수정)

            FadeInOut(ps);
            return go;
        }

        // fx_skull — 해골 페널티(S15 §B 표: "검은 연기 8 + 붉은 파편 6 하강"). 레이어드:
        //   메인: 검은 연기 8개(다단계 5+3), 회전 추가
        //   자식 RedShards: 붉은 파편 6개, 중력으로 하강, 회전
        // 이 FxId는 S16 §B가 폭탄 폭발 연출에도 재사용한다(BombBurstTint로 틴트 — ApplyTint가 자식까지
        // 전파하므로 RedShards도 함께 주황으로 물든다, 의도된 동작).
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
            main.maxParticles = 12;

            SetBursts(ps, (0f, 5), (0.06f, 3)); // 설계 "연기 8" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 12f;
            shape.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(50f, 90f); // 상승
            ZeroVelocityXZ(ps); // S15 §B 품질 규칙 — 기존 누락 수정

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-40f, 40f); // 연기가 뭉근히 도는 느낌

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f))); // 퍼프가 상승하며 퍼짐(연기 특성상 "0→1→0"이 아니라 "성장" 유지)

            FadeOut(ps);

            // RedShards(자식) — "붉은 파편 6 하강"(설계 명시). 콘을 아래로 돌려(rotation 90,0,0) 하강시키고
            // 중력을 더한다. 틴트가 없을 때(순수 해골 페널티)는 UiKit.Bad(빨강) 고정.
            var shardMat = CloneWithTexture(_matAdd, _texShard, "skull_shard");
            var shardGo = AddChild(go, "RedShards", shardMat, OrderNormal);
            var shardPs = shardGo.GetComponent<ParticleSystem>();

            var smain = shardPs.main;
            smain.duration = 0.45f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(80f, 180f);
            smain.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            smain.startColor = UiKit.Bad; // 붉은 파편(기본값 — 틴트 시 덮어써짐)
            SetGravityPx(shardPs, 300f); // "하강"
            smain.maxParticles = 10;

            SetBursts(shardPs, (0f, 6)); // 설계 명시 "6"

            var sshape = shardPs.shape;
            sshape.enabled = true;
            sshape.shapeType = ParticleSystemShapeType.Cone;
            sshape.angle = 30f;
            sshape.radius = 10f;
            sshape.rotation = new Vector3(90f, 0f, 0f); // 콘을 아래로(-Y) 돌린다

            var srot = shardPs.rotationOverLifetime;
            srot.enabled = true;
            srot.z = new ParticleSystem.MinMaxCurve(-300f, 300f);

            SizeGrowShrink(shardPs, 1f, 0.2f, 0.4f);
            FadeOutTinted(shardPs, MaroonMid);

            return go;
        }

        // fx_perk_pick — 퍽/유물 선택(S15 §B 표 "카드 선택": 티어색 폭발24 + 상승 입자12 + 링1).
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
            main.maxParticles = 34;

            SetBursts(ps, (0f, 16), (0.05f, 8)); // 설계 "폭발 24" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 5f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-200f, 200f);

            SizeGrowShrink(ps, 1f, 0.12f, 0f);
            FadeOut(ps);

            AddRisingChild(go, "Rising", 12); // 설계 "상승 입자 12"

            var ringMat = CloneWithTexture(_matAdd, _texRing, "perk_pick_ring");
            var ringGo = AddChild(go, "Ring", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 30f, 220f, 0.4f, 0.65f, new[] { (0f, 1) }); // 설계 "링1"

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
            SetGravityPx(ps, 60f); // 완만한 낙하(설계에 수치 없음 — 구현 결정치)
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
            ZeroVelocityXZ(ps); // S15 §B 품질 규칙 — 기존 누락 수정

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

        // fx_btn_press — 버튼 누를 때(S15 §B 표: 링 임팩트1 + 스파크8, 짧고 빠름 0.25s). 레이어드:
        //   메인: 스파크 8개(다단계 5+3), 회전+사이즈 커브 추가
        //   자식 RingImpact: 골드 링 1회 팽창
        private static GameObject Build_BtnPress()
        {
            var go = NewRoot("fx_btn_press", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.25f; // 설계 명시값
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(140f, 260f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = UiKit.TierGold; // 설계 명시 "골드"
            main.maxParticles = 12;

            SetBursts(ps, (0f, 5), (0.04f, 3)); // 설계 명시값 "8" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle; // "방사"
            shape.radius = 40f; // 버튼 절반 크기 근사(설계 미명시 — 구현 결정치)
            shape.radiusThickness = 0f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            SizeGrowShrink(ps, 1f, 0.15f, 0f);
            FadeOut(ps);

            var ringMat = CloneWithTexture(_matAdd, _texRing, "btn_press_ring");
            var ringGo = AddChild(go, "RingImpact", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 15f, 110f, 0.2f, 0.65f,
                new[] { (0f, 1) }, UiKit.TierGold); // 설계 "링 임팩트1"

            return go;
        }

        // fx_card_pick — 카드 선택 시(S15 §B 표 "카드 선택": 티어색 폭발24 + 상승 입자12 + 링1).
        private static GameObject Build_CardPick()
        {
            var mat = CloneWithTexture(_matAdd, _texDot, "card_pick");
            var go = NewRoot("fx_card_pick", mat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f; // 설계 명시값
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(180f, 340f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startColor = Color.white; // 런타임 tint(티어색)로 덮어쓰는 것을 전제로 한 기본값(설계 "티어색")
            main.maxParticles = 34;

            SetBursts(ps, (0f, 16), (0.06f, 8)); // 설계 "폭발 24" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere; // 중심에서 진짜 "폭발"(이전엔 원 가장자리 방출)
            shape.radius = 10f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-220f, 220f);

            SizeGrowShrink(ps, 1f, 0.15f, 0f);
            FadeOut(ps);

            AddRisingChild(go, "Rising", 12); // 설계 "상승 입자 12"

            var ringMat = CloneWithTexture(_matAdd, _texRing, "card_pick_ring");
            var ringGo = AddChild(go, "Ring", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 25f, 200f, 0.4f, 0.6f, new[] { (0f, 1) }); // "링1"

            return go;
        }

        // ── S14 §F: 연출 강화 신규 파티클 3종 ────────────────────────────────────────────

        // fx_reel_land — 릴 착지 임팩트(§B). 셀 바닥 쪽에서 먼지 6개, 0.3s, 알파블렌드, 베이지.
        // S15 §B 품질 규칙 — sizeOverLifetime/rotationOverLifetime/약한 중력을 추가했다(개수 6은
        // 설계 그대로 유지).
        private static GameObject Build_ReelLand()
        {
            var go = NewRoot("fx_reel_land", _matAlpha, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(60f, 140f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = new Color(0.85f, 0.78f, 0.6f, 0.6f); // 먼지(베이지)
            SetGravityPx(ps, 40f); // 살짝 가라앉는 먼지(설계 미명시 — 구현 결정치)
            main.maxParticles = 10;

            SetBursts(ps, (0f, 6)); // "먼지 파티클(6개)" 설계 그대로

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 70f;
            shape.position = new Vector3(0f, -90f, 0f); // "바닥" 쪽으로 오프셋(셀 하단 근사)

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            SizeGrowShrink(ps, 1f, 0.2f, 0.5f);
            FadeOut(ps);
            return go;
        }

        // fx_converge — 4매치 성립 시 셀에서 중앙으로 에너지 수렴(§C, S15 §B 표 "4매치 ⑥"). FxKit이
        // from(매치 셀)→to(중앙 셀)로 이동시키는 것을 전제로 emission/shape를 모두 비활성(fx_coin과
        // 동일 패턴). 도착 시 "폭발 링 2연발"(⑦)은 별도 FxId.ConvergeBurst를 PlayFlyTo의 arrivalBurst로
        // 지정해 정확한 도착 좌표(중앙 셀)에서 재생한다(ReelView.PlayConvergeFx).
        private static GameObject Build_Converge()
        {
            var go = NewRoot("fx_converge", _matAdd, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startColor = UiKit.TierGold;
            main.maxParticles = 40; // 셀당 8개 × 최대 4~5칸(설계 "30개") 여유

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // FxKit.PlayFlyTo가 Emit(count)로 직접 발사

            var shape = ps.shape;
            shape.enabled = false;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(180f, 360f); // 회전하며 수렴(품질 규칙)

            SizeGrowShrink(ps, 1f, 0.3f, 0.6f);
            FadeInOut(ps, 0.1f, 0.6f);
            return go;
        }

        // fx_jackpot_rays — 잭팟 전용 회전 광선(S15 §B 표 "잭팟 ⑩" = §C "골드 방사 광선 8줄 회전").
        // w_ray(UiSpriteGen) 텍스처를 파티클에 그대로 물려(별도 uGUI 8-스포크 리그를 새로 짓지 않는다 —
        // 재해석 보고 대상) 8개 버스트 + rotationOverLifetime으로 "회전하는 광선 다발"을 근사한다. 개별
        // 파티클의 초기 방향은 균등 8방향이 아니라 무작위(ParticleSystem 기본 API 한계) — 시각적으로는
        // 여전히 방사형으로 회전하는 광선 다발처럼 보인다.
        private static GameObject Build_JackpotRays()
        {
            var rayTex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiSpriteGen.OutputDir + "/w_ray.png");
            var mat = CloneWithTexture(_matAdd, rayTex, "jackpot_rays");
            var go = NewRoot("fx_jackpot_rays", mat, OrderFullscreen);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(260f, 340f); // 화면을 가로지르는 긴 광선
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // 라디안 — 무작위 초기 방향
            main.startColor = new Color(UiKit.TierGold.r, UiKit.TierGold.g, UiKit.TierGold.b, 0.55f);
            main.maxParticles = 10;

            SetBursts(ps, (0f, 8)); // "8줄"

            var shape = ps.shape;
            shape.enabled = false; // 중심 고정 방출

            var sol = ps.sizeOverLifetime; // S15 §B 품질 규칙 — 등장 시 살짝 자라나는 커브 추가
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.85f)));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(90f, 150f); // "회전"(도/초)

            FadeOut(ps);
            return go;
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // S15 §B — 파티클 전면 재작업 신규 5종
        // ══════════════════════════════════════════════════════════════════════════════

        // fx_match2 — 세트 2매치 전용(표 "세트 2매치" 행: 셀당 파편12 위로 분출 + 링1 + 별4 랜덤 지연).
        // ReelView.TryPairAccent가 심볼색으로 틴트해 재생한다(이전엔 Outline 펄스만 있었다 — 파티클
        // 없이 단독 사용하던 것을 이번 슬라이스에서 없앤 자리).
        private static GameObject Build_Match2() => BuildCellBurstFx("fx_match2", withTrail: false);

        // BuildCellBurstFx — fx_match2/fx_set_hit 공용 골격. "세트 3매치 = 위(2매치) + 트레일" 관계를
        // withTrail 플래그 하나로 코드에 그대로 드러낸다(파일 상단 "S15 §B 파티클 전면 재작업" 주석 참조).
        //   메인 Fragments: p_shard, 심볼색 틴트(런타임), 콘 위로 분출 + 중력, 회전, 다단계 버스트(8+4=12)
        //     withTrail이면 TrailModule 추가("셀 사이를 잇는 트레일" 요구를 파편 자체의 궤적 트레일로
        //     재해석 — 임의의 두 셀을 실제로 잇는 지오메트리는 새 런타임 API가 필요해 범위를 벗어난다,
        //     보고 대상)
        //   자식 Ring: p_ring, 확산 1회("링1")
        //   자식 Sparkle: p_star4, 서로 다른 시각에 1개씩 4번 발사("별 반짝 4개, 랜덤 지연" 근사)
        private static GameObject BuildCellBurstFx(string name, bool withTrail)
        {
            var fragMat = CloneWithTexture(_matAdd, _texShard, name + "_frag");
            var go = NewRoot(name, fragMat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(180f, 320f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = Color.white; // 런타임 tint(심볼색)로 덮어쓰는 것을 전제로 한 기본값
            SetGravityPx(ps, 250f);
            main.maxParticles = 20;

            SetBursts(ps, (0f, 8), (0.05f, 4)); // 설계 "파편 12" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone; // "위로 분출"
            shape.angle = 25f;
            shape.radius = 14f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

            SizeGrowShrink(ps, 1f, 0.2f, 0.3f);
            FadeOutTinted(ps, EmberMid);

            if (withTrail) ConfigureTrail(ps, fragMat, 0.3f); // 설계 명시 "TrailModule, 0.3s"

            var ringMat = CloneWithTexture(_matAdd, _texRing, name + "_ring");
            var ringGo = AddChild(go, "Ring", ringMat, OrderNormal);
            ConfigureRingWave(ringGo.GetComponent<ParticleSystem>(), 30f, 150f, 0.35f, 0.6f, new[] { (0f, 1) });

            var starMat = CloneWithTexture(_matAdd, _texStar4, name + "_sparkle");
            var starGo = AddChild(go, "Sparkle", starMat, OrderNormal);
            var starPs = starGo.GetComponent<ParticleSystem>();

            var smain = starPs.main;
            smain.duration = 0.4f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.32f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(20f, 60f);
            smain.startSize = new ParticleSystem.MinMaxCurve(10f, 16f);
            smain.startColor = Color.white;
            smain.maxParticles = 8;

            // "랜덤 지연" 근사 — 4개를 서로 다른 시각에 1개씩(참 난수 지연은 API 한계로 어려워 스태거로 대체).
            SetBursts(starPs, (0f, 1), (0.06f, 1), (0.13f, 1), (0.2f, 1));

            var sshape = starPs.shape;
            sshape.enabled = true;
            sshape.shapeType = ParticleSystemShapeType.Circle;
            sshape.radius = 40f;
            sshape.radiusThickness = 1f;

            SizeGrowShrink(starPs, 1f, 0.3f, 0f);
            FadeOut(starPs);

            return go;
        }

        // fx_rising_light — S15 §B 표 "세트 3매치 ⑤"(4매치·잭팟도 hasSet 경로로 "위 +" 누적) "화면
        // 하단에서 광입자 20개 상승". 잭팟 전용 "⑫ 화면 가장자리 상승 광입자"는 별도 프리팹을 새로
        // 짓는 대신 이 FxId를 그대로 겸용한다 — 잭팟도 bestSetId==jackpotSym이라 hasSet 경로를 타
        // ReelView.PostRevealFx가 이미 자동으로 함께 재생한다(재해석 보고 대상: "가장자리"와 "하단"의
        // 뉘앙스 차이는 반영하지 못했다).
        private static GameObject Build_RisingLight()
        {
            var mat = CloneWithTexture(_matAdd, _texDot, "rising_light");
            var go = NewRoot("fx_rising_light", mat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startColor = new Color(UiKit.TierGold.r, UiKit.TierGold.g, UiKit.TierGold.b, 0.5f);
            main.maxParticles = 30;

            SetBursts(ps, (0f, 14), (0.1f, 6)); // 설계 "20개" — 다단계

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box; // 화면 하단 라인(fx_clear의 상단 라인과 대칭)
            shape.scale = new Vector3(1000f, 1f, 1f);
            shape.position = new Vector3(0f, -380f, 0f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(60f, 140f); // "상승"
            ZeroVelocityXZ(ps);

            SizeGrowShrink(ps, 1f, 0.25f, 0.4f);
            FadeInOut(ps, 0.1f, 0.6f);
            return go;
        }

        // fx_converge_burst — S15 §B 표 "4매치 ⑦" 중앙 도착 시 "폭발 링 2연발". FxKit.PlayFlyTo의
        // arrivalBurst로 지정되어 수렴 입자 전원이 도착한 프레임에 정확한 도착 좌표(중앙 셀)에서
        // 재생된다(ReelView.PlayConvergeFx).
        private static GameObject Build_ConvergeBurst()
        {
            var mat = CloneWithTexture(_matAdd, _texRing, "converge_burst");
            var go = NewRoot("fx_converge_burst", mat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();
            ConfigureRingWave(ps, 20f, 220f, 0.35f, 0.8f, new[] { (0f, 1), (0.08f, 1) }, UiKit.TierGold); // "2연발"
            return go;
        }

        // fx_coin_spark — S15 §B 표 "코인 획득" "도착 시 스파크4". FxKit.PlayFlyTo의 arrivalBurst로
        // 지정되어 코인 입자가 HUD 코인 라벨에 도착하는 정확한 좌표에서 재생된다(HudView.PlayCoinFx).
        private static GameObject Build_CoinSpark()
        {
            var mat = CloneWithTexture(_matAdd, _texStar4, "coin_spark");
            var go = NewRoot("fx_coin_spark", mat, OrderNormal);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(80f, 160f);
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = UiKit.TierGold;
            main.maxParticles = 8;

            SetBursts(ps, (0f, 4)); // 설계 명시 "4"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 10f;
            shape.radiusThickness = 0f;

            SizeGrowShrink(ps, 1f, 0.2f, 0f);
            FadeOut(ps);
            return go;
        }

        // fx_run_ambient — S15 §B 표 "배경(런 화면)": 아주 은은한 부유 광입자 3개/초(알파 .08). 루프.
        // HudView가 자기 자신이 이미 보유한 runScreenRoot(RunScreen 루트 RectTransform, S14 §G 셰이크
        // 공용 참조와 동일 필드 — 새 앵커 없이 기존 참조 재사용) 기준으로 화면 진입/이탈 시
        // PlayLoop/StopLoop 한다.
        private static GameObject Build_RunAmbient()
        {
            var go = NewRoot("fx_run_ambient", _matAdd, OrderAmbient);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f); // 아주 느긋하게 오래 떠 있는다
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 22f);
            main.startColor = new Color(1f, 1f, 1f, 0.08f); // 설계 명시 알파 .08
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 3f; // 설계 명시 "3개/초"

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Rectangle; // 런 화면 전체(fx_menu_ambient와 동일 관례)
            shape.scale = new Vector3(1000f, 1900f, 1f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(6f, 14f); // 은은한 상승
            ZeroVelocityXZ(ps);

            FadeInOut(ps);
            return go;
        }

        // ── 공용 빌더 헬퍼 ────────────────────────────────────────────────────────────

        private static GameObject NewRoot(string name, Material mat, int sortingOrder)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();
            // 새로 추가된 ParticleSystem이 에디터 씬 뷰 프리뷰로 즉시 재생 중일 수 있다(환경에 따라
            // "Auto-Play" 상태) — 뒤이어 각 빌더가 main.duration 등을 설정하기 전에 확실히 멈춰
            // "Setting the duration while system is still playing is not supported" 경고를 막는다.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None; // 풀 재사용(FxKit이 직접 수명 관리)
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startRotation3D = false;
            main.startSize3D = false;
            // ⚠️ Hierarchy 필수. 설계의 모든 수치(startSize·속도·shape 반지름·중력)는 "캔버스 픽셀"
            // 단위로 적었는데, Local 모드는 부모 스케일을 무시하고 그 값을 월드 단위로 해석한다.
            // Screen Space-Camera 캔버스의 lossyScale은 약 0.005(월드/캔버스px)라, Local이면 크기 8이
            // 8월드 = 화면 높이의 두 배가 넘는 흰 사각형이 된다 — 사용자가 본 "텍스처 덩어리"의 정체.
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

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

        // S15 §B — "상승 입자" 자식(fx_perk_pick/fx_card_pick 공용): 소프트 도트가 위로 떠오르며
        // 등장·소멸한다. 부모의 ApplyTint(런타임)가 자식까지 전파되므로 티어색이 함께 물든다.
        private static void AddRisingChild(GameObject parent, string name, int count)
        {
            var mat = CloneWithTexture(_matAdd, _texDot, parent.name + "_" + name);
            var childGo = AddChild(parent, name, mat, OrderNormal);
            var ps = childGo.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.7f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startColor = Color.white;
            main.maxParticles = Mathf.Max(4, count + 4);

            SetBursts(ps, (0f, count));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 30f;
            shape.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.y = new ParticleSystem.MinMaxCurve(40f, 90f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            SizeGrowShrink(ps, 1f, 0.25f, 0.3f);
            FadeInOut(ps, 0.1f, 0.65f);
        }

        // S15 §B — 링 웨이브 공용 빌더(스케일 fromSize→toSize로 팽창 + 알파 startAlpha→0). bursts를
        // 여러 개(시간이 다른 다중 항목) 주면 그만큼의 "N연발" 웨이브가 된다(각 입자가 자기 lifetime
        // 기준으로 독립적으로 팽창하므로 하나의 시스템으로 다연발을 표현할 수 있다).
        private static void ConfigureRingWave(ParticleSystem ps, float fromSize, float toSize, float lifetime,
            float startAlpha, (float time, int count)[] bursts, Color? color = null, float startDelay = 0f)
        {
            float lastBurstTime = 0f;
            for (int i = 0; i < bursts.Length; i++)
                if (bursts[i].time > lastBurstTime) lastBurstTime = bursts[i].time;

            var main = ps.main;
            main.duration = lastBurstTime + lifetime;
            main.startDelay = startDelay;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = fromSize;
            var c = color ?? Color.white;
            c.a = startAlpha;
            main.startColor = c;
            main.maxParticles = Mathf.Max(4, bursts.Length * 2);

            SetBursts(ps, bursts);

            var shape = ps.shape;
            shape.enabled = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, toSize / Mathf.Max(1f, fromSize))));

            FadeOut(ps); // startColor.a(=startAlpha) → 0
        }

        // S15 §B — TrailModule 부착(설계 품질 규칙 "필요 시 TrailModule", fx_set_hit "트레일 파티클
        // (0.3s)"). 같은 머티리얼을 트레일에도 물려(별도 트레일 전용 에셋 없이) 파편이 지나간 자리에
        // 옅은 잔상을 남긴다.
        private static void ConfigureTrail(ParticleSystem ps, Material mat, float lifetime)
        {
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = lifetime;
            // minVertexDistance는 scalingMode의 영향을 받지 않는 "월드 단위" 값이다. 캔버스
            // lossyScale이 약 0.005라 4를 그대로 두면 새 정점이 사실상 추가되지 않아, 트레일이 첫 정점과
            // 현재 위치를 잇는 화면을 가로지르는 긴 직선으로 그려졌다(2026-08-01 관측). 4 캔버스px
            // 상당인 0.02로 맞춘다.
            trails.minVertexDistance = 0.02f;
            trails.worldSpace = false;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.trailMaterial = mat;
        }

        // S15 §B 품질 규칙 — velocityOverLifetime은 x/y/z가 전부 같은 모드여야 한다(하나만 TwoConstants로
        // 설정하고 나머지를 기본값(Constant)으로 두면 Unity가 "Particle Velocity curves must all be in
        // the same mode" 경고를 낸다). 호출측이 먼저 y(또는 x)를 TwoConstants로 설정한 뒤 이 헬퍼로
        // 나머지 두 축을 (0,0) TwoConstants로 맞춘다.
        // 설계의 "중력 N"은 캔버스 px/s²다. main.gravityModifier는 Physics.gravity(월드 공간 가속)에
        // 곱해지는 값이라 scalingMode=Hierarchy로도 스케일되지 않는다 — 캔버스 lossyScale이 약
        // 0.005라 월드 400은 로컬(=캔버스px) 8만 이상이 돼, 입자가 0.25초 만에 화면 밖 수천 px로
        // 날아가고 그 궤적이 화면을 가로지르는 긴 줄로 보였다(2026-08-01). forceOverLifetime을
        // Local space로 주면 시뮬레이션 공간(=캔버스 단위) 그대로 해석돼 설계 수치가 의도대로 산다.
        private static void SetGravityPx(ParticleSystem ps, float pxPerSecSq)
        {
            var main = ps.main;
            main.gravityModifier = 0f;

            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            // x/y/z는 반드시 같은 모드로(velocityOverLifetime과 동일한 유니티 제약).
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(-pxPerSecSq);
            force.z = new ParticleSystem.MinMaxCurve(0f);
        }

        private static void ZeroVelocityXZ(ParticleSystem ps)
        {
            var vol = ps.velocityOverLifetime;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        // x를 먼저 설정한 경우용 짝(위 ZeroVelocityXZ는 y를 먼저 설정한 경우용).
        private static void ZeroVelocityYZ(ParticleSystem ps)
        {
            var vol = ps.velocityOverLifetime;
            vol.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void SetBursts(ParticleSystem ps, params (float time, int count)[] stages)
        {
            var em = ps.emission;
            em.enabled = true;
            var arr = new ParticleSystem.Burst[stages.Length];
            for (int i = 0; i < stages.Length; i++)
                arr[i] = new ParticleSystem.Burst(stages[i].time, (short)stages[i].count);
            em.SetBursts(arr);
        }

        // S15 §B 품질 규칙 — sizeOverLifetime "0→1→0" 커브 표준형. endValue를 0이 아닌 값으로 주면
        // "0→peak→endValue"(완전히 사라지지 않고 옅게 남는 파편 등)로 쓸 수 있다.
        private static void SizeGrowShrink(ParticleSystem ps, float peak, float growFrac, float endValue)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(Mathf.Clamp01(growFrac), peak), new Keyframe(1f, endValue)));
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

        /// <summary>S15 §B 품질 규칙 "colorOverLifetime(알파 페이드 + 색 그라데이션)" — 알파만이 아니라
        /// 색상 자체가 white→midColor로 실제로 바뀌었다가 사라진다(스타트 컬러/틴트에 곱연산되므로
        /// 스파크가 "하얗게 튀었다가 식어가는" 느낌을 만든다).</summary>
        private static void FadeOutTinted(ParticleSystem ps, Color midColor)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(midColor, 0.4f),
                    new GradientColorKey(midColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
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

        // ══════════════════════════════════════════════════════════════════════════════
        // S15 §B — 공통 파티클 재료 4종 텍스처 생성(설계 명시: p_star4/p_ring/p_shard/p_coin, 전부
        // 가산합성 전제로 흰색 베이스 굽기 — p_dot은 기존 dot_soft 재사용이라 여기 없음).
        // ══════════════════════════════════════════════════════════════════════════════

        // p_star4 — 4각(십자) 스파클. star_soft(부드러운 십자 글로우)보다 더 또렷하고 각진 다이아몬드
        // 형태로 차별화(중심 원형 글로우 + 4방향 다이아몬드 각).
        private static Texture2D CreateStar4Tex(int size)
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
                    float centerGlow = Mathf.Clamp01(1f - distC * 3f);
                    float diamond = Mathf.Clamp01(1f - (Mathf.Abs(dx) + Mathf.Abs(dy)) * 1.15f);
                    float a = Mathf.Clamp01(centerGlow + diamond * 0.9f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // p_ring — 가는 링(도넛). 링 웨이브 계열(fx_spin_stop/fx_jackpot/fx_btn_press 등) 전용.
        private static Texture2D CreateRingTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size / 2f;
            Vector2 c = new Vector2(r, r);
            float ringRadius = r * 0.76f;
            float thickness = r * 0.18f; // "가는" 링
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float d = Mathf.Abs(dist - ringRadius);
                    float a = Mathf.Clamp01(1f - d / thickness);
                    a = a * a * (3f - 2f * a); // smoothstep
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // p_shard — 마름모 파편. 대각 하이라이트(facet)를 얹어 깨진 조각의 반짝임을 흉내낸다.
        private static Texture2D CreateShardTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) / half;
                    float dy = Mathf.Abs(y + 0.5f - half) / half;
                    float d = dx + dy; // 마름모(L1 노름) SDF 근사
                    float a = Mathf.Clamp01((1f - d) / 0.08f);
                    float signedDy = (y + 0.5f - half) / half;
                    float signedDx = (x + 0.5f - half) / half;
                    float facet = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01((signedDy - signedDx) * 0.5f + 0.5f));
                    px[y * size + x] = new Color(facet, facet, facet, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // p_coin — 원+테두리. 알파는 매끈한 원판, RGB 밝기에 테두리(rim) 밴드를 어둡게 넣어 tint 시에도
        // "동전 테두리"가 도드라져 보이게 한다(단일 그레이스케일 텍스처로 embossed 효과 근사).
        private static Texture2D CreateCoinTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size / 2f;
            Vector2 c = new Vector2(r, r);
            const float bandCenter = 0.78f, bandHalfWidth = 0.10f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distNorm = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                    float alpha = Mathf.Clamp01((1f - distNorm) / 0.06f);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    float bandT = Mathf.Clamp01(1f - Mathf.Abs(distNorm - bandCenter) / bandHalfWidth);
                    float brightness = 1f - 0.45f * bandT; // 테두리(rim) 밴드를 어둡게 — "원+테두리"
                    px[y * size + x] = new Color(brightness, brightness, brightness, alpha);
                }
            }
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
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                // 텍스처가 비어 있으면 채워 준다 — 과거 실행에서 텍스처 없이 만들어진 머티리얼이
                // overwrite:false 때문에 계속 남아 파티클이 "흰 네모 덩어리"로 보이던 버그(2026-08-01).
                if (existing != null && defaultTex != null && existing.mainTexture == null)
                {
                    existing.mainTexture = defaultTex;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

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

        // 파생 머티리얼은 반드시 .mat "에셋"으로 저장한다. 메모리 상의 new Material(...)을 프리팹
        // 렌더러에 물린 채 SaveAsPrefabAsset하면 그 참조가 저장되지 않고 None으로 끊긴다 → 유니티가
        // 기본 파티클 머티리얼로 대체해 텍스처 없는 흰 사각형만 보였다(2026-08-01 사용자 리포트).
        private static Material CloneWithTexture(Material baseMat, Texture2D tex, string tag)
        {
            if (baseMat == null) return null;
            if (tex == null) return baseMat;

            string name = baseMat.name + "_" + tag;
            if (_cloneCache.TryGetValue(name, out var cached) && cached != null) return cached;

            string path = $"{_matDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 텍스처/셰이더가 어긋난 과거 산출물은 제자리에서 고쳐 쓴다(GUID 유지 → 프리팹 참조 보존).
                if (existing.shader != baseMat.shader) existing.shader = baseMat.shader;
                existing.CopyPropertiesFromMaterial(baseMat);
                existing.mainTexture = tex;
                EditorUtility.SetDirty(existing);
                _cloneCache[name] = existing;
                return existing;
            }

            var clone = new Material(baseMat) { name = name };
            clone.mainTexture = tex;
            AssetDatabase.CreateAsset(clone, path);
            _cloneCache[name] = clone;
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
