using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 파티클 이펙트 식별자 — Editor/FxPrefabGen.cs가 굽는 Resources/JackpotRun/FX/<snake_case>.prefab
    // 파일명은 FileName(FxId)가 변환한다. 각 슬라이스(S7c/S13/S14/S15)가 추가한 항목은 아래 그룹
    // 주석으로 구분(ENGINE_PORT_DESIGN.md 해당 절 표 참조).
    public enum FxId
    {
        SpinStop,
        SetHit,
        Jackpot,
        ExpGain,
        Coin,
        Clear,
        Boss,
        Skull,
        PerkPick,
        GameOver,
        MenuAmbient,

        // S13 §E — UI 발광 파티클 4종(ENGINE_PORT_DESIGN.md S13 §E 표).
        UiAura,
        TitleSpark,
        BtnPress,
        CardPick,

        // S14 §F — 연출 강화 신규 파티클 3종.
        ReelLand,
        Converge,
        JackpotRays,

        // S15 §B — 파티클 전면 재작업 신규 5종(ENGINE_PORT_DESIGN.md S15 §B 표).
        Match2,        // 세트 2매치 전용(파편12+링1+별4)
        RisingLight,   // 3매치 이상 "화면 하단/가장자리 상승 광입자"
        ConvergeBurst, // 4매치 수렴 도착 시 "폭발 링 2연발"(PlayFlyTo arrivalBurst)
        CoinSpark,     // 코인 도착 시 스파크(PlayFlyTo arrivalBurst)
        RunAmbient,    // 런 화면 배경 은은한 부유 광입자 루프
    }

    // 런타임 파티클 재생 API — ENGINE_PORT_DESIGN.md S7c "런타임 API(Scripts/UI2/Fx/FxKit.cs)".
    // AppRoot가 캔버스 하위 "FxLayer" GameObject에 이 컴포넌트를 보유한다(이 파일은 자기 자신의
    // GameObject에 CanvasGroup을 스스로 보장하므로, FxLayer 생성 쪽이 CanvasGroup을 미리 붙이지
    // 않아도 Awake에서 안전하게 채워진다 — "파티클이 UI 클릭을 막지 않도록" 요구사항).
    //
    // 프리팹은 Resources.Load로 지연 로드하고(첫 재생 시점), 프리팹별 최대 8개까지 풀링해 재사용한다
    // (stopAction=None으로 구워져 있으므로 재생이 끝나도 GameObject가 파괴되지 않고 풀에 남는다).
    // 프리팹이 존재하지 않으면(FxPrefabGen 미실행 등) 모든 Play* 메서드가 예외 없이 null을 반환한다.
    public sealed class FxKit : MonoBehaviour
    {
        public static FxKit I { get; private set; }

        private const string ResourceDir = "JackpotRun/FX/";
        private const int MaxPerId = 8;

        // PlayFlyTo 코루틴이 목표점에 끝내 수렴하지 못하는 극단적 상황(파티클 lifetime이 비정상적으로
        // 긴 프리팹이 잘못 구워진 경우 등)에 대비한 안전 상한 — 정상 동작(설계상 0.5s)에는 영향 없음.
        private const float FlyToSafetyDuration = 3f;

        private readonly Dictionary<FxId, GameObject> _prefabCache = new Dictionary<FxId, GameObject>();
        private readonly HashSet<FxId> _prefabMissing = new HashSet<FxId>();
        private readonly Dictionary<FxId, List<ParticleSystem>> _pool = new Dictionary<FxId, List<ParticleSystem>>();

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;

            // 설계 "주의": FxLayer CanvasGroup{blocksRaycasts=false, interactable=false}.
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        private void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>anchor 중심(캔버스 로컬 좌표로 변환)에 1회 재생. tint가 있으면 startColor를 덮어쓴다
        /// (예: fx_spin_stop 심볼색, fx_perk_pick 티어색). 프리팹 없음/anchor null이면 null 반환.</summary>
        public ParticleSystem Play(FxId id, RectTransform anchor, Color? tint = null)
        {
            if (anchor == null) return null;
            return PlayAt(id, ToLocal(anchor), tint);
        }

        /// <summary>캔버스(=FxLayer) 로컬 좌표에 직접 1회 재생.</summary>
        public ParticleSystem PlayAt(FxId id, Vector2 canvasLocalPos, Color? tint = null)
        {
            var ps = Acquire(id);
            if (ps == null) return null;
            PositionAt(ps, canvasLocalPos);
            ApplyTint(ps, tint);
            ps.Play(true);
            return ps;
        }

        /// <summary>from 위치에서 count개 입자를 to 위치로 날린다(코인 등). 목표점은 재생 시점 좌표로
        /// 고정한다(대상이 정적 HUD 라벨이라는 전제). S15 §B — arcHeight&gt;0이면 사인 커브로 포물선
        /// 궤적을 근사한다("중력"의 시각적 대역 — FlyToRoutine이 매 프레임 위치를 직접 덮어쓰므로 실제
        /// ParticleSystem 물리 중력은 적용될 수 없다, 아래 FlyToRoutine 주석 참조). arrivalBurst를
        /// 지정하면 입자 전원이 도착한 프레임에 그 FxId를 정확한 도착 좌표에서 1회 재생한다(코인 도착
        /// 스파크, 수렴 도착 폭발 링 등). from/to null이면 null 반환.</summary>
        public ParticleSystem PlayFlyTo(FxId id, RectTransform from, RectTransform to, int count,
            FxId? arrivalBurst = null, float arcHeight = 0f)
        {
            if (from == null || to == null) return null;
            var ps = Acquire(id);
            if (ps == null) return null;

            Vector2 fromLocal = ToLocal(from);
            Vector2 toLocal = ToLocal(to);
            PositionAt(ps, fromLocal);
            ps.Play(true);

            int n = Mathf.Clamp(count, 1, Mathf.Max(1, ps.main.maxParticles));
            ps.Emit(n);

            Vector3 targetLocalOffset = new Vector3(toLocal.x - fromLocal.x, toLocal.y - fromLocal.y, 0f);
            StartCoroutine(FlyToRoutine(ps, targetLocalOffset, arcHeight, arrivalBurst, toLocal));
            return ps;
        }

        /// <summary>루프 이펙트 시작(fx_boss/fx_gameover/fx_menu_ambient) — 반환된 핸들을 StopLoop에
        /// 넘겨야 멈춘다(호출측 책임).</summary>
        public ParticleSystem PlayLoop(FxId id, RectTransform anchor, Color? tint = null)
        {
            if (anchor == null) return null;
            var ps = Acquire(id);
            if (ps == null) return null;
            PositionAt(ps, ToLocal(anchor));
            ApplyTint(ps, tint);
            ps.Play(true);
            return ps;
        }

        public void StopLoop(ParticleSystem handle)
        {
            if (handle == null) return;
            handle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ── 내부: 풀링/좌표 변환 ──────────────────────────────────────────────────────

        private ParticleSystem Acquire(FxId id)
        {
            var prefab = GetPrefab(id);
            if (prefab == null) return null;

            if (!_pool.TryGetValue(id, out var list))
            {
                list = new List<ParticleSystem>();
                _pool[id] = list;
            }

            ParticleSystem free = null;
            for (int i = 0; i < list.Count; i++)
            {
                var inst = list[i];
                if (inst == null) continue;
                if (!inst.IsAlive(true))
                {
                    free = inst;
                    break;
                }
            }

            if (free == null)
            {
                if (list.Count < MaxPerId)
                {
                    var go = Instantiate(prefab, transform);
                    free = go.GetComponent<ParticleSystem>();
                    list.Add(free);
                }
                else
                {
                    // 풀 상한(프리팹당 8) 도달 — 가장 오래된 인스턴스를 재사용한다. 총량 상한을 우선하므로
                    // 그 인스턴스가 재생 중이었다면 연출이 끊길 수 있다(설계 "동시 파티클 총량 상한 ~300").
                    free = list[0];
                    list.RemoveAt(0);
                    list.Add(free);
                }
            }

            if (free == null) return null;
            free.gameObject.SetActive(true);
            free.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            free.Clear(true);
            return free;
        }

        private GameObject GetPrefab(FxId id)
        {
            if (_prefabCache.TryGetValue(id, out var cached)) return cached;
            if (_prefabMissing.Contains(id)) return null;

            var loaded = Resources.Load<GameObject>(ResourceDir + FileName(id));
            if (loaded == null)
            {
                _prefabMissing.Add(id);
                return null;
            }
            _prefabCache[id] = loaded;
            return loaded;
        }

        private static void PositionAt(ParticleSystem ps, Vector2 localPos)
        {
            var t = ps.transform;
            t.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>S15 §B — tint를 루트뿐 아니라 모든 자식 ParticleSystem(레이어드 프리팹의
        /// Ring/Sparkle/Rising 등)에도 함께 적용한다 — 레이어 색이 서로 따로 놀지 않고 하나의 팔레트로
        /// 보이게 한다. tint가 없으면 프리팹이 구운 기본색(예: fx_skull의 검은 연기·붉은 파편처럼 항상
        /// 고정색이어야 하는 레이어)을 그대로 둔다.</summary>
        private static void ApplyTint(ParticleSystem ps, Color? tint)
        {
            if (!tint.HasValue) return;
            var systems = ps.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.startColor = new ParticleSystem.MinMaxGradient(tint.Value);
            }
        }

        /// <summary>설계 "좌표 변환": RectTransformUtility 대신 anchor.TransformPoint(anchor.rect.center)
        /// → FxLayer(this.transform) 로컬로 InverseTransformPoint.</summary>
        private Vector2 ToLocal(RectTransform anchor)
        {
            Vector3 world = anchor.TransformPoint(anchor.rect.center);
            Vector3 local = transform.InverseTransformPoint(world);
            return local;
        }

        // S15 §B — 입자별 startLifetime 대비 경과 비율(t = 1 - remaining/start)로 원점(로컬 0,0,0)→
        // targetLocalOffset을 매 프레임 절대 위치로 직접 계산한다(이전 버전은 "현재 위치 기준
        // remainingLifetime-비례 접근"이었는데, 그 방식 위에 아크 오프셋을 얹으면 매 프레임 이전
        // 프레임의 아크가 다시 입력으로 들어가 피드백이 누적돼 궤적이 뒤틀린다 — t를 절대 기준으로
        // 구해 매 프레임 새로 계산하면 아크와 함께 써도 안정적이다). arcHeight&gt;0이면
        // sin(eased·π)만큼 Y를 더해 포물선을 근사한다("중력"의 시각적 대역 — PlayFlyTo 주석 참조,
        // 이 함수가 위치를 직접 덮어쓰므로 ParticleSystem의 실제 gravityModifier는 무의미하다).
        // 전 입자가 도착(e≥0.97)한 첫 프레임에 arrivalBurst를 정확한 도착 좌표에서 1회 재생한다.
        private IEnumerator FlyToRoutine(ParticleSystem ps, Vector3 targetLocalOffset, float arcHeight,
            FxId? arrivalBurst, Vector2 arrivalLocalPos)
        {
            if (ps == null) yield break;
            var buffer = new ParticleSystem.Particle[Mathf.Max(8, ps.main.maxParticles)];
            float elapsed = 0f;
            bool arrivalFired = false;
            while (ps != null && elapsed < FlyToSafetyDuration)
            {
                elapsed += Time.deltaTime;
                int count = ps.GetParticles(buffer);
                if (count == 0)
                {
                    if (!ps.IsAlive(false)) yield break;
                    yield return null;
                    continue;
                }

                bool allArrived = true;
                for (int i = 0; i < count; i++)
                {
                    float remain = buffer[i].remainingLifetime;
                    float total = buffer[i].startLifetime;
                    float t = total > 0.0001f ? Mathf.Clamp01(1f - remain / total) : 1f;
                    float e = t * t * (3f - 2f * t); // smoothstep
                    Vector3 pos = Vector3.LerpUnclamped(Vector3.zero, targetLocalOffset, e);
                    if (arcHeight > 0f) pos.y += Mathf.Sin(e * Mathf.PI) * arcHeight;
                    buffer[i].position = pos;
                    if (e < 0.97f) allArrived = false;
                }
                ps.SetParticles(buffer, count);

                if (allArrived && !arrivalFired && arrivalBurst.HasValue)
                {
                    arrivalFired = true;
                    PlayAt(arrivalBurst.Value, arrivalLocalPos);
                }
                yield return null;
            }
        }

        private static string FileName(FxId id)
        {
            switch (id)
            {
                case FxId.SpinStop: return "fx_spin_stop";
                case FxId.SetHit: return "fx_set_hit";
                case FxId.Jackpot: return "fx_jackpot";
                case FxId.ExpGain: return "fx_exp_gain";
                case FxId.Coin: return "fx_coin";
                case FxId.Clear: return "fx_clear";
                case FxId.Boss: return "fx_boss";
                case FxId.Skull: return "fx_skull";
                case FxId.PerkPick: return "fx_perk_pick";
                case FxId.GameOver: return "fx_gameover";
                case FxId.MenuAmbient: return "fx_menu_ambient";
                case FxId.UiAura: return "fx_ui_aura";
                case FxId.TitleSpark: return "fx_title_spark";
                case FxId.BtnPress: return "fx_btn_press";
                case FxId.CardPick: return "fx_card_pick";
                case FxId.ReelLand: return "fx_reel_land";
                case FxId.Converge: return "fx_converge";
                case FxId.JackpotRays: return "fx_jackpot_rays";
                case FxId.Match2: return "fx_match2";
                case FxId.RisingLight: return "fx_rising_light";
                case FxId.ConvergeBurst: return "fx_converge_burst";
                case FxId.CoinSpark: return "fx_coin_spark";
                case FxId.RunAmbient: return "fx_run_ambient";
                default: return null;
            }
        }
    }
}
