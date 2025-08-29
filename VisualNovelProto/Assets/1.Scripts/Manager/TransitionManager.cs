using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면(배경) 트랜지션 + 캐릭터(초상) 트랜지션을 한 곳에서 관리.
/// - 동적 생성/코루틴 없음. 고정 크기 풀에서 갱신(Update)만 돌림.
/// - CSV node.transition 의 스펙 예:
///   "fade_out(t=0.4);fade_in(t=0.3,delay=0.4)"
///   "blackout" / "shake(t=0.3,amp=18)" 등
/// - 캐릭터 연출은 DialogueUI에서 TransitionManager.PlayActorIn(...) 호출.
/// </summary>
public sealed class TransitionManager : MonoBehaviour
{
    public static bool IsPlaying => _activeCount > 0;
    static TransitionManager _i;
    static int _activeCount;

    [Header("Screen Targets")]
    public Image fadeOverlay;          // 전체 화면 덮는 검은 이미지(알파 애니메이션)
    public RectTransform shakeTarget;  // 흔들 대상(보통 최상위 Canvas/Panel)

    [Header("Settings")]
    public bool useUnscaledTime = true;
    public float defaultFadeTime = 0.35f;
    public float defaultShakeAmp = 16f;
    public float defaultShakeTime = 0.25f;

    // ===== Internal: 고정 풀 =====
    const int MaxFade = 16;
    const int MaxShake = 4;
    const int MaxActor = 12;

    FadeTask[] fades = new FadeTask[MaxFade];
    ShakeTask[] shakes = new ShakeTask[MaxShake];
    ActorTask[] actors = new ActorTask[MaxActor];

    void Awake()
    {
        _i = this;
        // 초기 클리어
        for (int i = 0; i < MaxFade; i++) fades[i].active = false;
        for (int i = 0; i < MaxShake; i++) shakes[i].active = false;
        for (int i = 0; i < MaxActor; i++) actors[i].active = false;

        if (fadeOverlay != null)
        {
            var c = fadeOverlay.color; c.a = 0f; fadeOverlay.color = c;
            fadeOverlay.gameObject.SetActive(true); // 항상 켜두되 알파만 0
        }
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        int alive = 0;

        // Fade
        for (int i = 0; i < MaxFade; i++)
            if (fades[i].active)
            {
                alive++;
                var t = fades[i];
                t.t += dt;
                float a = Mathf.Clamp01((t.t - t.delay) / Mathf.Max(0.0001f, t.dur));
                float v = Mathf.Lerp(t.from, t.to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a)));
                if (t.overlay) { var c = t.overlay.color; c.a = v; t.overlay.color = c; }
                if (t.t >= t.delay + t.dur) t.active = false;
                fades[i] = t;
            }

        // Shake
        for (int i = 0; i < MaxShake; i++)
            if (shakes[i].active)
            {
                alive++;
                var s = shakes[i];
                s.t += dt;
                float a = Mathf.Clamp01((s.t - s.delay) / Mathf.Max(0.0001f, s.dur));
                float k = 1f - a; // 감쇠
                if (s.target)
                {
                    Vector2 ofs = new Vector2(
                        (HashNoise(s.seed + s.frame * 13) - 0.5f) * 2f,
                        (HashNoise(s.seed + s.frame * 29) - 0.5f) * 2f
                    ) * s.amp * k;
                    s.frame++;
                    s.target.anchoredPosition = s.basePos + ofs;
                }
                if (s.t >= s.delay + s.dur)
                {
                    if (s.target) s.target.anchoredPosition = s.basePos;
                    s.active = false;
                }
                shakes[i] = s;
            }

        // Actor
        for (int i = 0; i < MaxActor; i++)
            if (actors[i].active)
            {
                alive++;
                var a = actors[i];
                a.t += dt;
                float x = Mathf.Clamp01((a.t - a.delay) / Mathf.Max(0.0001f, a.dur));
                float e = Ease(a.easing, x);

                if (a.mode == ActorMode.Fade || a.mode == ActorMode.Pop)
                {
                    if (a.img)
                    {
                        var c = a.img.color;
                        c.a = Mathf.Lerp(a.fromA, a.toA, e);
                        a.img.color = c;
                    }
                }
                if (a.mode == ActorMode.Pop)
                {
                    if (a.rt) a.rt.localScale = Vector3.Lerp(a.fromScale, a.toScale, e);
                }
                if (a.mode == ActorMode.Slide)
                {
                    if (a.rt) a.rt.anchoredPosition = Vector2.Lerp(a.fromPos, a.toPos, e);
                }

                if (a.t >= a.delay + a.dur)
                {
                    // 종료 고정
                    if (a.img)
                    {
                        var c = a.img.color; c.a = a.toA; a.img.color = c;
                    }
                    if (a.rt)
                    {
                        if (a.mode == ActorMode.Pop) a.rt.localScale = a.toScale;
                        if (a.mode == ActorMode.Slide) a.rt.anchoredPosition = a.toPos;
                    }
                    a.active = false;
                }
                actors[i] = a;
            }

        _activeCount = alive;
    }

    // ===== Public API: Background =====

    /// <summary>
    /// CSV node.transition 문자열을 파싱해서 여러 트랜지션을 등록.
    /// 예) "fade_out(t=0.4);fade_in(t=0.3,delay=0.4);shake(t=0.25,amp=14)"
    /// </summary>
    public static void Play(string spec)
    {
        if (_i == null || string.IsNullOrWhiteSpace(spec)) return;

        // ';'로 구분된 여러 개 처리
        var parts = spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            ParseAndEnqueue(parts[i].Trim());
    }

    static void ParseAndEnqueue(string token)
    {
        // name(args)
        string name = token;
        string args = null;
        int op = token.IndexOf('(');
        if (op >= 0)
        {
            int cp = token.LastIndexOf(')');
            name = token.Substring(0, op).Trim();
            if (cp > op) args = token.Substring(op + 1, cp - op - 1);
        }

        float t = _i.defaultFadeTime, delay = 0f, amp = _i.defaultShakeAmp;
        if (!string.IsNullOrEmpty(args))
        {
            var kv = args.Split(',');
            for (int k = 0; k < kv.Length; k++)
            {
                var s = kv[k].Trim();
                if (s.StartsWith("t=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(2), out var v)) t = Mathf.Max(0f, v);
                else if (s.StartsWith("time=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(5), out v)) t = Mathf.Max(0f, v);
                else if (s.StartsWith("delay=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(6), out v)) delay = Mathf.Max(0f, v);
                else if (s.StartsWith("amp=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(4), out v)) amp = Mathf.Max(0f, v);
            }
        }

        if (name.Equals("fade_out", StringComparison.OrdinalIgnoreCase)) EnqueueFade(0f, 1f, t, delay);
        else if (name.Equals("fade_in", StringComparison.OrdinalIgnoreCase)) EnqueueFade(1f, 0f, t, delay);
        else if (name.Equals("blackout", StringComparison.OrdinalIgnoreCase)) EnqueueFade(0f, 1f, 0f, 0f);
        else if (name.Equals("clearout", StringComparison.OrdinalIgnoreCase)) EnqueueFade(1f, 0f, 0f, 0f);
        else if (name.Equals("shake", StringComparison.OrdinalIgnoreCase)) EnqueueShake(t, amp, delay);
        // 필요 시 추가: flash, blur 등
    }

    static void EnqueueFade(float from, float to, float dur, float delay)
    {
        if (_i == null || _i.fadeOverlay == null) return;
        for (int i = 0; i < MaxFade; i++)
            if (!_i.fades[i].active)
            {
                var c = _i.fadeOverlay.color; c.a = from; _i.fadeOverlay.color = c;
                _i.fades[i] = new FadeTask
                {
                    active = true,
                    t = 0f,
                    dur = dur,
                    delay = delay,
                    from = from,
                    to = to,
                    overlay = _i.fadeOverlay
                };
                return;
            }
    }

    static void EnqueueShake(float time, float amp, float delay)
    {
        if (_i == null || _i.shakeTarget == null) return;
        for (int i = 0; i < MaxShake; i++)
            if (!_i.shakes[i].active)
            {
                var rt = _i.shakeTarget;
                _i.shakes[i] = new ShakeTask
                {
                    active = true,
                    t = 0f,
                    dur = time,
                    delay = delay,
                    amp = amp,
                    target = rt,
                    basePos = rt.anchoredPosition,
                    seed = UnityEngine.Random.Range(1, int.MaxValue),
                    frame = 0
                };
                return;
            }
    }

    // ===== Public API: Actor(초상) =====

    /// <summary>
    /// 캐릭터 입장 연출을 매니저로 수행. (기존 코루틴 대체)
    /// effect: "fade" | "pop" | "slide"
    /// posHint: 'L' 'C' 'R' → slide 시작 방향 결정. ('X'면 아래에서 올라오도록 처리)
    /// time≤0이면 defaultFadeTime 사용.
    /// </summary>
    public static void PlayActorIn(Image img, char posHint, string effect, float time, bool flipX)
    {
        if (_i == null || img == null) return;

        var rt = img.rectTransform;
        float t = time > 0f ? time : _i.defaultFadeTime;

        ActorMode mode = ActorMode.Fade;
        if (!string.IsNullOrEmpty(effect))
        {
            if (effect.Equals("pop", StringComparison.OrdinalIgnoreCase)) mode = ActorMode.Pop;
            else if (effect.Equals("slide", StringComparison.OrdinalIgnoreCase)) mode = ActorMode.Slide;
        }

        // 시작/끝 상태 구성
        var task = new ActorTask
        {
            active = true,
            img = img,
            rt = rt,
            mode = mode,
            dur = t,
            delay = 0f,
            easing = Easing.Smooth // 기본 이징
        };

        // 공통: flipX는 rt.localScale.x에 반영되어 있다고 가정
        if (mode == ActorMode.Fade)
        {
            task.fromA = 0f; task.toA = 1f;
            task.fromScale = rt.localScale; task.toScale = rt.localScale;
            task.fromPos = rt.anchoredPosition; task.toPos = rt.anchoredPosition;
            // 시작 알파 0으로 보정
            var c = img.color; c.a = 0f; img.color = c;
        }
        else if (mode == ActorMode.Pop)
        {
            float sx = Mathf.Abs(rt.localScale.x);
            if (flipX) sx = -sx;
            task.fromA = 0f; task.toA = 1f;
            task.fromScale = new Vector3(sx * 1.2f, 1.2f, 1f);
            task.toScale = new Vector3(sx, 1f, 1f);
            task.fromPos = task.toPos = Vector2.zero; // 로컬 유지
            var c = img.color; c.a = 0f; img.color = c;
            rt.localScale = task.fromScale;
        }
        else // Slide
        {
            float off = 220f;
            Vector2 offv = Vector2.down * off;
            switch (char.ToUpperInvariant(posHint))
            {
                case 'L': offv = Vector2.left * off; break;
                case 'R': offv = Vector2.right * off; break;
                default: offv = Vector2.down * off; break;
            }
            task.fromA = 1f; task.toA = 1f;
            task.fromScale = rt.localScale; task.toScale = rt.localScale;
            task.toPos = Vector2.zero;
            task.fromPos = task.toPos + offv;
            rt.anchoredPosition = task.fromPos;
        }

        // 빈 슬롯 찾기
        for (int i = 0; i < MaxActor; i++)
            if (!_i.actors[i].active)
            {
                _i.actors[i] = task;
                return;
            }
    }

    /// <summary>모든 진행 중 트랜지션을 정지하고 상태를 복원.</summary>
    public static void StopAllTransitions()
    {
        if (_i == null) return;

        for (int i = 0; i < MaxFade; i++) _i.fades[i].active = false;
        for (int i = 0; i < MaxShake; i++)
        {
            if (_i.shakes[i].active && _i.shakes[i].target)
                _i.shakes[i].target.anchoredPosition = _i.shakes[i].basePos;
            _i.shakes[i].active = false;
        }
        for (int i = 0; i < MaxActor; i++) _i.actors[i].active = false;

        _activeCount = 0;
    }

    // ===== Structs =====
    struct FadeTask
    {
        public bool active;
        public float t, dur, delay;
        public float from, to;
        public Image overlay;
    }

    struct ShakeTask
    {
        public bool active;
        public float t, dur, delay, amp;
        public RectTransform target;
        public Vector2 basePos;
        public int seed, frame;
    }

    enum ActorMode { Fade, Pop, Slide }
    enum Easing { Linear, Smooth }

    struct ActorTask
    {
        public bool active;
        public float t, dur, delay;
        public Easing easing;
        public ActorMode mode;

        public Image img;
        public RectTransform rt;

        public float fromA, toA;
        public Vector3 fromScale, toScale;
        public Vector2 fromPos, toPos;
    }

    // ===== Util =====
    static float Ease(Easing e, float x)
    {
        switch (e)
        {
            case Easing.Smooth: return x * x * (3f - 2f * x); // SmoothStep
            default: return x;
        }
    }

    static float HashNoise(int seed)
    {
        // 0~1 범위의 해시성 노이즈
        unchecked
        {
            uint x = (uint)seed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }
}
