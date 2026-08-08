using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JackpotRun.Game
{
    // 절차 합성 사운드 — 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 정답지 `public/play/sound.js`).
    // 웹은 Web Audio API로 매 재생마다 오실레이터/노이즈 그래프를 실시간으로 구성해 정확한 샘플
    // 스케줄링(AudioContext.currentTime 기준 tone()/noise())을 한다. Unity는 외부 음원 없이 같은
    // 파형을 오프라인으로 재현한다 — 기동(Bootstrap) 시 sound.js의 16개 sfx 스위치 케이스 +
    // BGM 아르페지오에 쓰이는 음표(PENTA 5 + bass 4)를 AudioClip.Create로 전부 미리 합성해 캐시하고,
    // 재생 시점엔 AudioSource 풀에서 PlayOneShot만 한다(웹의 setTimeout 지연 오차조차 없음 — 스케줄이
    // 아니라 완성된 샘플을 재생하므로 오히려 웹보다 타이밍이 정확하다).
    //
    // WEB_PARITY_DESIGN.md §1-A #17 "SFX 17종"의 정확한 개수 — sound.js의 switch(name) 케이스는
    // tap/select/spin/reel/win/jackpot/coin/buy/clear/perfect/fanfare/perk/bomb/boss/error/gameover
    // 16개뿐이다(전수 확인, grep 결과 이 파일에 정확히 이 16개 case만 존재). 설계 문서의 "17종"은
    // 여기에 BGM 루프 1종을 더한 표기로 보인다(16 SFX + BGM = 17개 사운드 유닛) — 웹 원본을 그대로
    // 옮겼으므로 case 개수를 임의로 늘리지 않았다.
    //
    // select/buy/error 3종은 sound.js에 정의는 있지만 ui.js 전체(및 game.js/engine.js)에서
    // snd.sfx("select"|"buy"|"error")를 호출하는 곳이 실제로는 단 한 곳도 없다(전수 grep 확인 —
    // 웹 자체의 죽은 코드). Unity도 웹과 동일하게 3종의 합성 자체는 그대로 구현·캐시하되(추후 웹이
    // 실제로 호출을 추가하면 바로 쓸 수 있도록), 트리거 배선은 하지 않는다 — §0 "웹 채택이 기본"
    // 원칙대로 웹에 없는 트리거를 새로 만들지 않는다.
    //
    // 합성 근사(웹과 다른 점, WEB_PARITY_DESIGN.md §2에 요약 예정):
    //  ① 오실레이터 파형 — Web Audio 네이티브 오실레이터는 band-limited(안티에일리어싱) 파형을 쓰지만
    //     여기서는 단순 수식파(naive sine/triangle/square/sawtooth)로 근사한다. 고음역에서 미세한
    //     aliasing 차이가 있을 수 있으나 사실상 청감상 무시 가능한 수준(SFX가 대부분 짧고 저음량).
    //  ② 오실레이터 stop 여유분(웹 `o.stop(t+dur+0.03)`의 +0.03s 무음에 가까운 꼬리)은 렌더링하지
    //     않는다 — 그 구간은 게인이 이미 바닥(0.0001)까지 감쇠해 있어 들리는 차이가 없다.
    //  ③ bandpass 필터(noise의 filterFreq)는 Web Audio spec의 "constant 0dB peak gain BPF" RBJ
    //     쿡북 공식을 Q=1(BiquadFilterNode 기본값)로 그대로 구현했다(근사 아님, 정확한 전사).
    //  ④ noise() 버퍼의 난수원은 웹이 Math.random()(비결정)인 반면 여기는 파라미터 기반 고정 시드
    //     (System.Random)를 쓴다 — 기동 시 1회만 합성해 캐시하는 구조상 결정론이 오히려 유리(같은
    //     기기에서 재현 가능한 동일 질감, 매 재생마다 새로 만들지 않음). 청감상 화이트노이즈 텍스처는
    //     동일한 통계적 성질을 가지므로 체감 차이 없음.
    // AudioListener — Editor/UiSceneBuilder.cs의 UICamera 생성부(typeof(Camera)만)는 AudioListener를
    // 붙이지 않는다(전수 확인). 씬에 리스너가 하나도 없으면 AudioSource.Play가 완전히 무음이 되므로
    // (콘솔 경고만 뜨고 아무 것도 들리지 않음), 씬 리빌드에 기대지 않고 이 DontDestroyOnLoad 오브젝트
    // 자신에 리스너를 보장한다(Intro/Play 두 씬 전환에도 유지되는 유일한 리스너).
    public sealed class SoundKit : MonoBehaviour
    {
        public static SoundKit Instance { get; private set; }

        // 웹 slotweb_sound/slotweb_vol(localStorage) 대응 — PlayerPrefs 키(작업 지시 그대로).
        public const string SoundPrefKey = "jackpotrun_sound";
        public const string VolumePrefKey = "jackpotrun_vol";
        private const float DefaultVolume = 0.7f; // 웹 sound.js:3 `let vol = 0.7`

        private const int SampleRate = 44100;
        private const float AttackTime = 0.008f; // 웹 tone()/noise() g.gain.exponentialRampToValueAtTime(..., t+0.008)
        private const float EnvFloor = 0.0001f;  // 웹 g.gain.setValueAtTime(0.0001, t)

        // PENTA(C D E G A) — 웹 sound.js:45. bass — 웹 sound.js:72 `[130.81,130.81,174.61,196.00]`(C C F G).
        private static readonly float[] Penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880f };
        private static readonly float[] BgmBass = { 130.81f, 130.81f, 174.61f, 196.00f };

        private const float BgmTickSeconds = 0.23f; // 웹 setTimeout(tick, 230)
        private const float BgmNoteDur = 0.3f;
        private const float BgmNoteVol = 0.045f;
        private const float BgmBassDur = 0.62f;
        private const float BgmBassVol = 0.05f;

        private const int PoolSize = 8; // 작업 지시 "AudioSource 풀(동시 재생)" — 라운드로빈.

        private bool _enabled = true;
        private float _volume = DefaultVolume;
        private bool _bgmOn;
        private int _bgmStep;
        private Coroutine _bgmRoutine;

        private AudioSource[] _pool;
        private int _poolCursor;

        private Dictionary<string, AudioClip> _sfxClips;
        private AudioClip[] _bgmNoteClips;
        private AudioClip[] _bgmBassClips;

        public static bool Enabled => Instance != null && Instance._enabled;
        public static float Volume => Instance != null ? Instance._volume : DefaultVolume;
        public static bool BgmActive => Instance != null && Instance._bgmOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("SoundKit");
            go.AddComponent<SoundKit>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            _enabled = PlayerPrefs.GetInt(SoundPrefKey, 1) != 0;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));

            BuildAudioSourcePool();
            BuildClipCache();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildAudioSourcePool()
        {
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f; // 2D — UI 사운드는 방향/거리 무관.
                _pool[i] = src;
            }
        }

        // ── 공개 API(호출부는 정적으로 쓴다 — Instance 생존은 Bootstrap이 보장) ────────────────
        public static void Sfx(string name)
        {
            var self = Instance;
            if (self == null || !self._enabled || string.IsNullOrEmpty(name)) return;
            if (self._sfxClips == null || !self._sfxClips.TryGetValue(name, out var clip) || clip == null) return;
            self.PlayOnPool(clip);
        }

        public static void SetEnabled(bool on)
        {
            var self = Instance;
            if (self == null) return;
            self._enabled = on;
            PlayerPrefs.SetInt(SoundPrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (!on) BgmStop(); // 웹 sound.js:16 `setEnabled(on){ enabled=!!on; if(!on) bgmStop(); }`
        }

        // Opus 2차검수 항목3(2026-08-09) — 드래그 중 매 프레임 호출될 수 있어(SettingsSheet
        // Slider.onValueChanged) 여기서는 값 반영 + PlayerPrefs.SetFloat(메모리 캐시)만 하고
        // 디스크 flush(PlayerPrefs.Save)는 하지 않는다 — 값 자체는 즉시 재생/표시에 반영된다.
        public static void SetVolume(float v)
        {
            var self = Instance;
            if (self == null) return;
            self._volume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(VolumePrefKey, self._volume);
        }

        /// <summary>드래그 release/시트 Hide 등 "확정" 시점에 1회만 호출 — SetVolume이 캐시해 둔
        /// 값을 디스크에 실제로 쓴다(항목3, 매 프레임 Save 금지).</summary>
        public static void SaveVolume() => PlayerPrefs.Save();

        // 웹 sound.js:15 `resume()` — 모바일/인앱브라우저의 Web Audio 자동재생 정책 우회(사용자
        // 제스처 안에서 AudioContext를 재개)용이다. Unity AudioSource는 이런 브라우저 전용 제약이
        // 없어(권한 프롬프트 등이 없다면) 대응 개념이 존재하지 않는다 — 호출부에서 의도적으로
        // 생략했다(작업 지시 "모바일 첫 탭 resume은 Unity 불필요").

        public static void BgmStart()
        {
            var self = Instance;
            if (self == null) return;
            self.StartBgmInternal();
        }

        private void StartBgmInternal()
        {
            if (!_enabled || _bgmOn) return; // 웹 sound.js:70 그대로
            _bgmOn = true;
            _bgmStep = 0;
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(BgmLoop());
        }

        public static void BgmStop()
        {
            var self = Instance;
            if (self == null) return;
            self._bgmOn = false;
            if (self._bgmRoutine != null) { self.StopCoroutine(self._bgmRoutine); self._bgmRoutine = null; }
        }

        // 웹 bgmStart() tick() 재귀 setTimeout(230ms)을 코루틴 루프로 재현 — WaitForSecondsRealtime을
        // 쓴다(잭팟 슬로모(ReelView.JackpotSlowMoRoutine) 등 Time.timeScale 조작 중에도 BGM 템포가
        // 웹처럼 실시간을 유지하게 하기 위함 — setTimeout은애초에 timeScale 개념이 없다).
        private IEnumerator BgmLoop()
        {
            while (_bgmOn && _enabled)
            {
                int noteIdx = (_bgmStep * 3) % Penta.Length;
                if (_bgmNoteClips != null && noteIdx < _bgmNoteClips.Length) PlayOnPool(_bgmNoteClips[noteIdx]);
                if (_bgmStep % 4 == 0 && _bgmBassClips != null && _bgmBassClips.Length > 0)
                {
                    int bassIdx = (_bgmStep / 4) % _bgmBassClips.Length;
                    PlayOnPool(_bgmBassClips[bassIdx]);
                }
                _bgmStep++;
                yield return new WaitForSecondsRealtime(BgmTickSeconds);
            }
            _bgmOn = false;
            _bgmRoutine = null;
        }

        private void PlayOnPool(AudioClip clip)
        {
            if (clip == null || _pool == null || _pool.Length == 0) return;
            var src = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            if (src != null) src.PlayOneShot(clip, _volume);
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // ── 클립 캐시 — sound.js switch(name) 16종 + BGM 음표(PENTA 5 + bass 4) 전사 ─────────
        // ══════════════════════════════════════════════════════════════════════════════
        private void BuildClipCache()
        {
            _sfxClips = new Dictionary<string, AudioClip>(16);

            // case "tap": tone(660, 0.05, "triangle", 0.1);
            _sfxClips["tap"] = Bake(b => b.Tone(660f, 0.05f, Wave.Triangle, 0.1f));

            // case "select": tone(587,.07,"triangle",.13); tone(880,.1,"triangle",.12,.05);
            _sfxClips["select"] = Bake(b =>
            {
                b.Tone(587f, 0.07f, Wave.Triangle, 0.13f);
                b.Tone(880f, 0.1f, Wave.Triangle, 0.12f, 0.05f);
            });

            // case "spin": noise(.42,.06,0,900); tone(200,.42,"sawtooth",.05,0,340);
            _sfxClips["spin"] = Bake(b =>
            {
                b.Noise(0.42f, 0.06f, 0f, 900f);
                b.Tone(200f, 0.42f, Wave.Sawtooth, 0.05f, 0f, 340f);
            });

            // case "reel": tone(840,.035,"square",.07);
            _sfxClips["reel"] = Bake(b => b.Tone(840f, 0.035f, Wave.Square, 0.07f));

            // case "win": [0,.08,.16].forEach((d,i)=>tone(PENTA[i+1],.18,"triangle",.15,d));
            _sfxClips["win"] = Bake(b =>
            {
                float[] delays = { 0f, 0.08f, 0.16f };
                for (int i = 0; i < delays.Length; i++) b.Tone(Penta[i + 1], 0.18f, Wave.Triangle, 0.15f, delays[i]);
            });

            // case "jackpot": [523,659,784,1046].forEach((f,i)=>tone(f,.3,"triangle",.2,i*.1)); noise(.5,.08,0,2200);
            _sfxClips["jackpot"] = Bake(b =>
            {
                float[] freqs = { 523f, 659f, 784f, 1046f };
                for (int i = 0; i < freqs.Length; i++) b.Tone(freqs[i], 0.3f, Wave.Triangle, 0.2f, i * 0.1f);
                b.Noise(0.5f, 0.08f, 0f, 2200f);
            });

            // case "coin": tone(988,.06,"square",.11); tone(1318,.1,"square",.11,.05);
            _sfxClips["coin"] = Bake(b =>
            {
                b.Tone(988f, 0.06f, Wave.Square, 0.11f);
                b.Tone(1318f, 0.1f, Wave.Square, 0.11f, 0.05f);
            });

            // case "buy": tone(660,.08,"square",.12); tone(988,.12,"square",.12,.06);
            _sfxClips["buy"] = Bake(b =>
            {
                b.Tone(660f, 0.08f, Wave.Square, 0.12f);
                b.Tone(988f, 0.12f, Wave.Square, 0.12f, 0.06f);
            });

            // case "clear": [523,659,784,1046,1318].forEach((f,i)=>tone(f,.26,"triangle",.17,i*.09));
            _sfxClips["clear"] = Bake(b =>
            {
                float[] freqs = { 523f, 659f, 784f, 1046f, 1318f };
                for (int i = 0; i < freqs.Length; i++) b.Tone(freqs[i], 0.26f, Wave.Triangle, 0.17f, i * 0.09f);
            });

            // case "perfect": tone(1046,.1,"triangle",.2); tone(1568,.2,"triangle",.17,.08);
            //                 tone(2093,.34,"sine",.13,.17); noise(.3,.05,.16,4200);
            _sfxClips["perfect"] = Bake(b =>
            {
                b.Tone(1046f, 0.1f, Wave.Triangle, 0.2f);
                b.Tone(1568f, 0.2f, Wave.Triangle, 0.17f, 0.08f);
                b.Tone(2093f, 0.34f, Wave.Sine, 0.13f, 0.17f);
                b.Noise(0.3f, 0.05f, 0.16f, 4200f);
            });

            // case "fanfare": [523,659,784,1046,1318,1568].forEach((f,i)=>tone(f,.32,"triangle",.18,i*.075));
            //                 tone(130.8,.6,"sawtooth",.08,0); tone(2093,.5,"sine",.12,.5); noise(.55,.08,.46,3200);
            _sfxClips["fanfare"] = Bake(b =>
            {
                float[] freqs = { 523f, 659f, 784f, 1046f, 1318f, 1568f };
                for (int i = 0; i < freqs.Length; i++) b.Tone(freqs[i], 0.32f, Wave.Triangle, 0.18f, i * 0.075f);
                b.Tone(130.8f, 0.6f, Wave.Sawtooth, 0.08f, 0f);
                b.Tone(2093f, 0.5f, Wave.Sine, 0.12f, 0.5f);
                b.Noise(0.55f, 0.08f, 0.46f, 3200f);
            });

            // case "perk": [784,988,1318].forEach((f,i)=>tone(f,.2,"sine",.14,i*.06));
            _sfxClips["perk"] = Bake(b =>
            {
                float[] freqs = { 784f, 988f, 1318f };
                for (int i = 0; i < freqs.Length; i++) b.Tone(freqs[i], 0.2f, Wave.Sine, 0.14f, i * 0.06f);
            });

            // case "bomb": noise(.35,.2,0,220); tone(80,.35,"sawtooth",.11,0,40);
            _sfxClips["bomb"] = Bake(b =>
            {
                b.Noise(0.35f, 0.2f, 0f, 220f);
                b.Tone(80f, 0.35f, Wave.Sawtooth, 0.11f, 0f, 40f);
            });

            // case "boss": tone(110,.5,"sawtooth",.13,0,90); tone(165,.5,"sawtooth",.09,.1);
            _sfxClips["boss"] = Bake(b =>
            {
                b.Tone(110f, 0.5f, Wave.Sawtooth, 0.13f, 0f, 90f);
                b.Tone(165f, 0.5f, Wave.Sawtooth, 0.09f, 0.1f);
            });

            // case "error": tone(170,.16,"sawtooth",.12,0,120);
            _sfxClips["error"] = Bake(b => b.Tone(170f, 0.16f, Wave.Sawtooth, 0.12f, 0f, 120f));

            // case "gameover": [392,330,262,196].forEach((f,i)=>tone(f,.3,"triangle",.14,i*.16));
            _sfxClips["gameover"] = Bake(b =>
            {
                float[] freqs = { 392f, 330f, 262f, 196f };
                for (int i = 0; i < freqs.Length; i++) b.Tone(freqs[i], 0.3f, Wave.Triangle, 0.14f, i * 0.16f);
            });

            // ── BGM 음표 팔레트 — 웹 bgmStart() tick()의 tone(note,.3,"triangle",.045) / bass(.62,"sine",.05) ──
            _bgmNoteClips = new AudioClip[Penta.Length];
            for (int i = 0; i < Penta.Length; i++)
            {
                float note = Penta[i] / 2f; // 웹: `PENTA[(bgmStep*3)%PENTA.length] / 2`
                _bgmNoteClips[i] = Bake(b => b.Tone(note, BgmNoteDur, Wave.Triangle, BgmNoteVol));
            }
            _bgmBassClips = new AudioClip[BgmBass.Length];
            for (int i = 0; i < BgmBass.Length; i++)
            {
                float bass = BgmBass[i];
                _bgmBassClips[i] = Bake(b => b.Tone(bass, BgmBassDur, Wave.Sine, BgmBassVol));
            }
        }

        private static AudioClip Bake(Action<ToneBuilder> configure)
        {
            var builder = new ToneBuilder();
            configure(builder);
            return builder.ToClip();
        }

        private enum Wave { Sine, Triangle, Square, Sawtooth }

        // 웹 tone()/noise()의 레이어 목록을 모아 "when(지연)+dur(길이)"에 맞춰 하나의 오프라인 버퍼로
        // 합성하는 빌더. 웹은 Web Audio 그래프(오실레이터/버퍼소스 각각 별도 노드)로 여러 레이어를
        // 동시 재생하지만, 여기서는 그 결과를 미리 하나의 AudioClip으로 믹스해 둔다(§0 클립 헤더 주석).
        private sealed class ToneBuilder
        {
            private struct Layer
            {
                public bool isNoise;
                public float freq, dur, vol, when, slideTo, filterFreq;
                public Wave type;
            }

            private readonly List<Layer> _layers = new List<Layer>();

            public void Tone(float freq, float dur, Wave type, float vol, float when = 0f, float slideTo = 0f)
                => _layers.Add(new Layer { isNoise = false, freq = freq, dur = dur, type = type, vol = vol, when = when, slideTo = slideTo });

            public void Noise(float dur, float vol, float when = 0f, float filterFreq = 0f)
                => _layers.Add(new Layer { isNoise = true, dur = dur, vol = vol, when = when, filterFreq = filterFreq });

            public AudioClip ToClip()
            {
                float totalDur = 0.02f;
                for (int i = 0; i < _layers.Count; i++) totalDur = Mathf.Max(totalDur, _layers[i].when + _layers[i].dur);
                int totalSamples = Mathf.Max(1, Mathf.CeilToInt(totalDur * SampleRate) + 1);
                var master = new float[totalSamples];

                for (int i = 0; i < _layers.Count; i++)
                {
                    var l = _layers[i];
                    float[] layerSamples = l.isNoise
                        ? RenderNoise(l.dur, l.vol, l.filterFreq, seed: SeedFor(l))
                        : RenderTone(l.freq, l.dur, l.type, l.vol, l.slideTo);
                    int offset = Mathf.RoundToInt(l.when * SampleRate);
                    for (int s = 0; s < layerSamples.Length; s++)
                    {
                        int idx = offset + s;
                        if (idx < 0 || idx >= master.Length) continue;
                        master[idx] += layerSamples[s];
                    }
                }

                // 소프트 클램프 — 여러 레이어가 겹쳐도 [-1,1]을 벗어나지 않게 하는 방어적 안전장치
                // (개별 vol이 0.05~0.2대로 낮게 설계돼 있어 실제로는 거의 도달하지 않는다).
                for (int i = 0; i < master.Length; i++) master[i] = Mathf.Clamp(master[i], -1f, 1f);

                var clip = AudioClip.Create("jackpotrun_sfx", master.Length, 1, SampleRate, false);
                clip.SetData(master, 0);
                return clip;
            }

            private static int SeedFor(Layer l)
                => unchecked((int)(l.dur * 100000f) ^ (int)(l.vol * 100000f) ^ (int)(l.filterFreq * 10f) ^ (int)(l.when * 100000f));

            private static float[] RenderTone(float freq, float dur, Wave type, float vol, float slideTo)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(dur * SampleRate));
                var data = new float[n];
                bool hasSlide = slideTo > 0f && freq > 0f;
                float logRatio = hasSlide ? Mathf.Log(slideTo / freq) : 0f;
                float phase = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float f = hasSlide ? freq * Mathf.Exp(logRatio * (t / dur)) : freq;
                    phase += f / SampleRate;
                    phase -= Mathf.Floor(phase);
                    data[i] = Waveform(type, phase) * ToneEnvelope(t, dur, vol);
                }
                return data;
            }

            private static float[] RenderNoise(float dur, float vol, float filterFreq, int seed)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(dur * SampleRate));
                var raw = new float[n];
                var rng = new System.Random(seed);
                for (int i = 0; i < n; i++)
                    raw[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - (float)i / n); // 웹 noise() 버퍼의 선형 감쇠

                if (filterFreq > 0f) raw = BandpassFilter(raw, filterFreq, SampleRate);

                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    raw[i] *= NoiseEnvelope(t, dur, vol);
                }
                return raw;
            }

            // 웹 tone() 전용 게인 엔벨로프 — g.gain.setValueAtTime(0.0001,t) →
            // exponentialRampToValueAtTime(vol, t+0.008) → exponentialRampToValueAtTime(0.0001, t+dur).
            // Opus 2차검수 항목1(2026-08-09, HIGH) — noise()는 이 어택 구간이 없다(아래 NoiseEnvelope
            // 참조). tone/noise가 같은 함수를 공유하면 spin/jackpot/perfect/fanfare/bomb 5종의 노이즈
            // 레이어가 실제보다 더 느리게 붙는 타격감으로 잘못 합성된다 — 반드시 분리 유지할 것.
            private static float ToneEnvelope(float t, float dur, float vol)
            {
                float peak = Mathf.Max(vol, EnvFloor);
                if (t <= 0f) return EnvFloor;
                if (t < AttackTime)
                {
                    float frac = t / AttackTime;
                    return EnvFloor * Mathf.Pow(peak / EnvFloor, frac);
                }
                if (t < dur)
                {
                    float frac = (t - AttackTime) / Mathf.Max(dur - AttackTime, 0.0001f);
                    return peak * Mathf.Pow(EnvFloor / peak, frac);
                }
                return EnvFloor;
            }

            // 웹 noise() 전용 게인 엔벨로프 — g.gain.setValueAtTime(vol,t)(어택 없이 즉시 최대치) →
            // exponentialRampToValueAtTime(0.0001, t+dur)(전체 dur에 걸쳐 단일 지수 감쇠). tone()의
            // 0.008s 어택 구간이 없다 — sound.js 원문 그대로(§0 헤더 참조, Opus 2차검수 항목1).
            private static float NoiseEnvelope(float t, float dur, float vol)
            {
                float peak = Mathf.Max(vol, EnvFloor);
                if (t >= dur) return EnvFloor;
                float frac = Mathf.Clamp01(t / Mathf.Max(dur, 0.0001f));
                return peak * Mathf.Pow(EnvFloor / peak, frac);
            }

            private static float Waveform(Wave type, float phase01)
            {
                switch (type)
                {
                    case Wave.Square: return phase01 < 0.5f ? 1f : -1f;
                    case Wave.Sawtooth: return 2f * phase01 - 1f;
                    case Wave.Triangle: return phase01 < 0.5f ? (4f * phase01 - 1f) : (3f - 4f * phase01);
                    default: return Mathf.Sin(phase01 * 2f * Mathf.PI); // Sine
                }
            }

            // Web Audio spec BiquadFilterNode "bandpass"(constant 0dB peak gain, Q=1 기본값) — RBJ
            // Audio EQ Cookbook 공식 그대로(근사가 아니라 스펙 전사). Direct Form I.
            private static float[] BandpassFilter(float[] input, float freq, int sampleRate)
            {
                const float q = 1f;
                float w0 = 2f * Mathf.PI * freq / sampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float cosw0 = Mathf.Cos(w0);

                float b0 = alpha, b1 = 0f, b2 = -alpha;
                float a0 = 1f + alpha, a1 = -2f * cosw0, a2 = 1f - alpha;
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

                var output = new float[input.Length];
                float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
                for (int i = 0; i < input.Length; i++)
                {
                    float x0 = input[i];
                    float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    output[i] = y0;
                    x2 = x1; x1 = x0;
                    y2 = y1; y1 = y0;
                }
                return output;
            }
        }
    }
}
