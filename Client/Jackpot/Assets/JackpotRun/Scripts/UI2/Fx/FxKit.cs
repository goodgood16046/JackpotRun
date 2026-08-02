using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 파티클 이펙트 11종 식별자 — ENGINE_PORT_DESIGN.md S7c "파티클 에셋 생성" 표의 id 컬럼과
    // 1:1 대응(Editor/FxPrefabGen.cs가 굽는 Resources/JackpotRun/FX/<snake_case>.prefab 파일명은
    // FileName(FxId)가 변환한다).
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
        /// 고정한다(대상이 정적 HUD 라벨이라는 전제). from/to null이면 null 반환.</summary>
        public ParticleSystem PlayFlyTo(FxId id, RectTransform from, RectTransform to, int count)
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
            StartCoroutine(FlyToRoutine(ps, targetLocalOffset));
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

        private static void ApplyTint(ParticleSystem ps, Color? tint)
        {
            if (!tint.HasValue) return;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(tint.Value);
        }

        /// <summary>설계 "좌표 변환": RectTransformUtility 대신 anchor.TransformPoint(anchor.rect.center)
        /// → FxLayer(this.transform) 로컬로 InverseTransformPoint.</summary>
        private Vector2 ToLocal(RectTransform anchor)
        {
            Vector3 world = anchor.TransformPoint(anchor.rect.center);
            Vector3 local = transform.InverseTransformPoint(world);
            return local;
        }

        // 입자별 remainingLifetime 기준으로 "목표점에 수렴"시킨다: 매 프레임 (경과시간 / 남은수명)만큼
        // 현재 위치→목표 사이를 보간하면, 특정 프레임 수와 무관하게 remainingLifetime이 0에 다가갈수록
        // 정확히 target에 도달한다(고전적인 "시간 비례 추적" 공식).
        private IEnumerator FlyToRoutine(ParticleSystem ps, Vector3 targetLocalOffset)
        {
            if (ps == null) yield break;
            var buffer = new ParticleSystem.Particle[Mathf.Max(8, ps.main.maxParticles)];
            float elapsed = 0f;
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

                for (int i = 0; i < count; i++)
                {
                    float remain = buffer[i].remainingLifetime;
                    if (remain > 0.0001f)
                    {
                        float frac = Mathf.Clamp01(Time.deltaTime / remain);
                        buffer[i].position = Vector3.Lerp(buffer[i].position, targetLocalOffset, frac);
                    }
                    else
                    {
                        buffer[i].position = targetLocalOffset;
                    }
                }
                ps.SetParticles(buffer, count);
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
                default: return null;
            }
        }
    }
}
