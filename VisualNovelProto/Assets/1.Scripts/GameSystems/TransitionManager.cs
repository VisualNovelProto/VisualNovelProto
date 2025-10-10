using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면(배경) 트랜지션 + 캐릭터(초상) 트랜지션을 한 곳에서 관리.
/// - 동적 생성/코루틴 없음. 고정 크기 풀에서 갱신(Update)만 돌림.
/// - CSV node.transition 예:
///   "fade_out(t=0.4);fade_in(t=0.3,delay=0.4)"
///   "blackout" / "shake(t=0.3,amp=18)"
///   "mask(name=left_to_right; time=0.6; invert=0; soft=0.02; color=#000000)"
/// - 캐릭터 연출은 DialogueUI에서 TransitionManager.PlayActorIn(...) 호출.
/// </summary>
public sealed class TransitionManager : MonoBehaviour
{
    // ===== Mask Wipe =====
    [Header("Mask Wipe")]
    public RawImage maskOverlay;                 // Canvas 전체 RawImage(레이캐스트 OFF)
    public Material maskMaterialTemplate;        // "UI/GrayscaleMaskWipe" 셰이더 머티리얼
    public string maskResourcesFolder = "TransitionMasks";

    Material _maskMat;
    Texture _maskTex;

    struct MaskTask
    {
        public bool active;
        public float t, dur, delay;
        public RawImage overlay;
        public Material mat;
    }
    MaskTask mask; // 동시에 하나만 재생한다고 가정

    public static bool IsPlaying => _activeCount > 0;
    static TransitionManager _i;
    static int _activeCount;
    public static event Action<bool> StateChanged;

    [Header("Screen Targets")]
    public Image fadeOverlay;          // 전체 화면 덮는 검은 이미지(알파 애니메이션)
    public RectTransform shakeTarget;  // 흔들 대상(보통 최상위 Canvas/Panel)

    [Header("Settings")]
    public bool useUnscaledTime = true;
    public float defaultFadeTime = 0.35f;
    public float defaultShakeAmp = 16f;
    public float defaultShakeTime = 0.25f;

    [Header("Camera Motion")]
    public RectTransform cameraTarget;
    public string cameraPathResourcesFolder = "CinematicPaths";

    [Header("Focus & Perspective")]
    public Image focusOverlay;
    public RectTransform focusTarget;
    public float focusFadeTime = 0.35f;
    public float focusDefaultZoom = 1.15f;
    public Color focusTint = new Color(0f, 0f, 0f, 0.4f);

    // ===== Internal: 고정 풀 =====
    const int MaxFade = 16;
    const int MaxShake = 4;
    const int MaxActor = 12;
    const int MaxCamera = 4;

    FadeTask[] fades = new FadeTask[MaxFade];
    ShakeTask[] shakes = new ShakeTask[MaxShake];
    ActorTask[] actors = new ActorTask[MaxActor];
    CameraTask[] cameras = new CameraTask[MaxCamera];
    FocusTask focus;

    void Awake()
    {
        _i = this;
        for (int i = 0; i < MaxFade; i++) fades[i].active = false;
        for (int i = 0; i < MaxShake; i++) shakes[i].active = false;
        for (int i = 0; i < MaxActor; i++) actors[i].active = false;
        for (int i = 0; i < MaxCamera; i++) cameras[i].active = false;
        mask.active = false;
        focus.active = false;

        if (fadeOverlay != null)
        {
            var c = fadeOverlay.color; c.a = 0f; fadeOverlay.color = c;
            fadeOverlay.gameObject.SetActive(true); // 항상 켜두되 알파 0
        }
        if (maskOverlay != null) maskOverlay.gameObject.SetActive(false);
        if (focusOverlay != null)
        {
            var c = focusOverlay.color; c.a = 0f; focusOverlay.color = c;
            focusOverlay.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        int prevCount = _activeCount;
        int alive = 0;

        // Fade
        for (int i = 0; i < MaxFade; i++)
            if (fades[i].active)
            {
                alive++;
                var tsk = fades[i];
                tsk.t += dt;
                float a = Mathf.Clamp01((tsk.t - tsk.delay) / Mathf.Max(0.0001f, tsk.dur));
                float v = Mathf.Lerp(tsk.from, tsk.to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a)));
                if (tsk.overlay) { var c = tsk.overlay.color; c.a = v; tsk.overlay.color = c; }
                if (tsk.t >= tsk.delay + tsk.dur) tsk.active = false;
                fades[i] = tsk;
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

        // Mask
        if (mask.active)
        {
            alive++;
            mask.t += dt;
            float a = Mathf.Clamp01((mask.t - mask.delay) / Mathf.Max(0.0001f, mask.dur));
            if (mask.mat != null) mask.mat.SetFloat("_Cutoff", a);
            if (mask.t >= mask.delay + mask.dur)
            {
                if (mask.overlay) mask.overlay.gameObject.SetActive(false);
                mask.active = false;
            }
        }

        // Camera Paths
        for (int i = 0; i < MaxCamera; i++)
            if (cameras[i].active)
            {
                alive++;
                var cam = cameras[i];
                cam.t += dt;
                float progress = Mathf.Clamp01((cam.t - cam.delay) / Mathf.Max(0.0001f, cam.dur));
                float eased = EvaluateCurve(cam.curve, progress);
                ApplyCameraTask(ref cam, eased);
                if (cam.t >= cam.delay + cam.dur)
                {
                    ApplyCameraTask(ref cam, 1f);
                    cam.active = false;
                }
                cameras[i] = cam;
            }

        // Focus / Perspective
        if (focus.active)
        {
            alive++;
            focus.t += dt;
            float progress = Mathf.Clamp01((focus.t - focus.delay) / Mathf.Max(0.0001f, focus.dur));
            float eased = Ease(Easing.Smooth, progress);
            ApplyFocusTask(ref focus, eased);
            if (focus.t >= focus.delay + focus.dur)
            {
                ApplyFocusTask(ref focus, 1f);
                focus.active = false;
                if (focusOverlay != null && focusOverlay.color.a <= 0.001f)
                    focusOverlay.gameObject.SetActive(false);
            }
        }

        _activeCount = alive;
        if ((prevCount > 0) != (_activeCount > 0))
            StateChanged?.Invoke(_activeCount > 0);
    }

    // ===== Public API: Background =====

    /// <summary>
    /// CSV node.transition 문자열을 파싱해서 여러 트랜지션을 등록.
    /// 예) "fade_out(t=0.4);fade_in(t=0.3,delay=0.4);shake(t=0.25,amp=14);mask(name=left_to_right)"
    /// </summary>
    public static void Play(string spec)
    {
        if (_i == null || string.IsNullOrWhiteSpace(spec)) return;

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
        string mName = null; bool invert = false; float soft = 0.02f; Color mColor = Color.black;
        string mode = null; string pointsSpec = null; float zoom = 1f; bool hasZoom = false;

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
                else if (s.StartsWith("name=", StringComparison.OrdinalIgnoreCase)) mName = s.Substring(5).Trim();
                else if (s.StartsWith("invert=", StringComparison.OrdinalIgnoreCase)) invert = s.EndsWith("1", StringComparison.OrdinalIgnoreCase) || s.EndsWith("true", StringComparison.OrdinalIgnoreCase);
                else if (s.StartsWith("soft=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(5), out v)) soft = Mathf.Clamp01(v);
                else if (s.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                {
                    var hex = s.Substring(6).Trim();
                    ColorUtility.TryParseHtmlString(hex, out mColor);
                }
                else if (s.StartsWith("mode=", StringComparison.OrdinalIgnoreCase)) mode = s.Substring(5).Trim();
                else if (s.StartsWith("zoom=", StringComparison.OrdinalIgnoreCase) && float.TryParse(s.Substring(5), out v)) { zoom = Mathf.Max(0.01f, v); hasZoom = true; }
                else if (s.StartsWith("points=", StringComparison.OrdinalIgnoreCase)) pointsSpec = s.Substring(7).Trim();
            }
        }

        if (name.Equals("fade_out", StringComparison.OrdinalIgnoreCase)) EnqueueFade(0f, 1f, t, delay);
        else if (name.Equals("fade_in", StringComparison.OrdinalIgnoreCase)) EnqueueFade(1f, 0f, t, delay);
        else if (name.Equals("blackout", StringComparison.OrdinalIgnoreCase)) EnqueueFade(0f, 1f, 0f, 0f);
        else if (name.Equals("clearout", StringComparison.OrdinalIgnoreCase)) EnqueueFade(1f, 0f, 0f, 0f);
        else if (name.Equals("shake", StringComparison.OrdinalIgnoreCase)) EnqueueShake(t, amp, delay);
        else if (name.Equals("mask", StringComparison.OrdinalIgnoreCase)) EnqueueMask(mName, t, delay, invert, soft, mColor);
        else if (name.Equals("camera_path", StringComparison.OrdinalIgnoreCase)) EnqueueCameraPath(mName, pointsSpec, t, delay, hasZoom, zoom, mode);
        else if (name.Equals("focus", StringComparison.OrdinalIgnoreCase)) EnqueueFocus(t, delay, hasZoom ? zoom : _i.focusDefaultZoom, mColor);
        else if (name.Equals("perspective", StringComparison.OrdinalIgnoreCase)) EnqueuePerspective(mode, t, delay, hasZoom ? zoom : _i.focusDefaultZoom, mColor);
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

    static void EnqueueMask(string name, float time, float delay, bool invert, float soft, Color color)
    {
        if (_i == null || _i.maskOverlay == null || _i.maskMaterialTemplate == null || string.IsNullOrEmpty(name)) return;

        // 리소스 로드
        string path = string.IsNullOrEmpty(_i.maskResourcesFolder) ? name : (_i.maskResourcesFolder + "/" + name);
        _i._maskTex = Resources.Load<Texture2D>(path);
        if (_i._maskTex == null) { Debug.LogWarning($"TransitionManager: mask texture not found: Resources/{path}"); return; }

        if (_i._maskMat == null) _i._maskMat = new Material(_i.maskMaterialTemplate);
        _i._maskMat.SetTexture("_MaskTex", _i._maskTex);
        _i._maskMat.SetFloat("_Invert", invert ? 1f : 0f);
        _i._maskMat.SetFloat("_Cutoff", 0f);
        _i._maskMat.SetFloat("_Softness", Mathf.Clamp01(soft));
        _i._maskMat.SetColor("_Color", color);

        _i.maskOverlay.material = _i._maskMat;
        _i.maskOverlay.texture = _i._maskTex;
        _i.maskOverlay.gameObject.SetActive(true);

        _i.mask = new MaskTask
        {
            active = true,
            t = 0f,
            dur = Mathf.Max(0.0001f, time),
            delay = Mathf.Max(0f, delay),
            overlay = _i.maskOverlay,
            mat = _i._maskMat
        };
    }

    static AnimationCurve EvaluateCurve(AnimationCurve curve) => curve != null && curve.length > 0 ? curve : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    static float EvaluateCurve(AnimationCurve curve, float t)
    {
        var c = EvaluateCurve(curve);
        return c.Evaluate(Mathf.Clamp01(t));
    }

    static Vector3[] ParseCameraPoints(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        var tokens = spec.Split('|');
        List<Vector3> pts = new List<Vector3>(tokens.Length);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (string.IsNullOrEmpty(token)) continue;
            var comps = token.Split(',');
            if (comps.Length < 2) continue;
            if (!float.TryParse(comps[0], out float x)) continue;
            if (!float.TryParse(comps[1], out float y)) continue;
            float z = 1f;
            if (comps.Length >= 3 && float.TryParse(comps[2], out float zVal)) z = Mathf.Max(0.01f, zVal);
            pts.Add(new Vector3(x, y, z));
        }
        return pts.Count >= 2 ? pts.ToArray() : null;
    }

    static void EnqueueCameraPath(string name, string inlinePoints, float time, float delay, bool hasZoom, float zoom, string mode)
    {
        if (_i == null || _i.cameraTarget == null) return;

        Vector3[] points = null;
        bool relative = true;
        AnimationCurve curve = null;

        if (!string.IsNullOrEmpty(inlinePoints))
        {
            points = ParseCameraPoints(inlinePoints);
            relative = !string.Equals(mode, "absolute", StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            string path = string.IsNullOrEmpty(_i.cameraPathResourcesFolder) ? name : (_i.cameraPathResourcesFolder + "/" + name);
            var asset = Resources.Load<CinematicCameraPath>(path);
            if (asset != null && asset.nodes != null && asset.nodes.Length >= 2)
            {
                points = new Vector3[asset.nodes.Length];
                for (int i = 0; i < asset.nodes.Length; i++)
                {
                    points[i] = new Vector3(asset.nodes[i].position.x, asset.nodes[i].position.y, asset.nodes[i].zoom <= 0f ? 1f : asset.nodes[i].zoom);
                }
                relative = asset.relative;
                curve = asset.easing;
            }
        }

        if (points == null || points.Length < 2) return;

        for (int i = 0; i < MaxCamera; i++)
            if (!_i.cameras[i].active)
            {
                var task = new CameraTask
                {
                    active = true,
                    t = 0f,
                    dur = time > 0f ? time : _i.defaultFadeTime,
                    delay = Mathf.Max(0f, delay),
                    points = points,
                    curve = curve,
                    relative = relative,
                    target = _i.cameraTarget,
                    basePos = _i.cameraTarget.anchoredPosition,
                    baseScale = _i.cameraTarget.localScale,
                    overrideZoom = hasZoom,
                    targetZoom = zoom
                };
                _i.cameras[i] = task;
                return;
            }
    }

    static void ApplyCameraTask(ref CameraTask task, float eased)
    {
        if (task.target == null || task.points == null || task.points.Length < 2) return;

        float segments = task.points.Length - 1;
        float scaled = Mathf.Clamp01(eased) * segments;
        int segIndex = Mathf.Min(task.points.Length - 2, Mathf.FloorToInt(scaled));
        float segT = Mathf.Clamp01(scaled - segIndex);

        Vector3 start = task.points[segIndex];
        Vector3 end = task.points[segIndex + 1];
        Vector3 pos = Vector3.Lerp(start, end, segT);

        Vector2 targetPos = task.relative ? task.basePos + new Vector2(pos.x, pos.y) : new Vector2(pos.x, pos.y);
        task.target.anchoredPosition = targetPos;

        float zoomStart = start.z <= 0f ? 1f : start.z;
        float zoomEnd = end.z <= 0f ? zoomStart : end.z;
        float zoomFactor = task.overrideZoom ? Mathf.Lerp(1f, task.targetZoom, eased) : Mathf.Lerp(zoomStart, zoomEnd, segT);
        task.target.localScale = task.baseScale * zoomFactor;
    }

    static void EnqueueFocus(float time, float delay, float zoom, Color color)
    {
        if (_i == null) return;

        float dur = time > 0f ? time : _i.focusFadeTime;
        if (_i.focusOverlay != null)
        {
            _i.focusOverlay.gameObject.SetActive(true);
        }

        var target = _i.focusTarget != null ? _i.focusTarget : _i.cameraTarget;
        _i.focus = new FocusTask
        {
            active = true,
            t = 0f,
            dur = dur,
            delay = Mathf.Max(0f, delay),
            fromColor = _i.focusOverlay ? _i.focusOverlay.color : Color.clear,
            toColor = color,
            target = target,
            fromScale = target != null ? target.localScale : Vector3.one,
            toScale = target != null ? target.localScale * zoom : Vector3.one * zoom
        };
    }

    static void EnqueuePerspective(string mode, float time, float delay, float zoom, Color color)
    {
        if (_i == null) return;
        Color targetColor = color.a > 0f ? color : _i.focusTint;
        float targetZoom = zoom;
        if (!string.IsNullOrEmpty(mode) && mode.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            targetColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
            targetZoom = 1f;
        }
        EnqueueFocus(time, delay, targetZoom, targetColor);
    }

    static void ApplyFocusTask(ref FocusTask task, float eased)
    {
        if (_i.focusOverlay != null)
        {
            Color c = Color.Lerp(task.fromColor, task.toColor, eased);
            _i.focusOverlay.color = c;
            if (c.a > 0f) _i.focusOverlay.gameObject.SetActive(true);
        }
        if (task.target != null)
        {
            task.target.localScale = Vector3.Lerp(task.fromScale, task.toScale, eased);
        }
    }

    // ===== Public API: Actor(초상) =====

    /// <summary>
    /// 캐릭터 입장 연출. effect: "fade" | "pop" | "slide"
    /// posHint: 'L','C','R' → slide 시작 방향 추정. ('X'면 아래에서 올라오기)
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

        var task = new ActorTask
        {
            active = true,
            img = img,
            rt = rt,
            mode = mode,
            dur = t,
            delay = 0f,
            easing = Easing.Smooth
        };

        if (mode == ActorMode.Fade)
        {
            task.fromA = 0f; task.toA = 1f;
            task.fromScale = rt.localScale; task.toScale = rt.localScale;
            task.fromPos = rt.anchoredPosition; task.toPos = rt.anchoredPosition;
            var c = img.color; c.a = 0f; img.color = c;
        }
        else if (mode == ActorMode.Pop)
        {
            float sx = Mathf.Abs(rt.localScale.x);
            if (flipX) sx = -sx;
            task.fromA = 0f; task.toA = 1f;
            task.fromScale = new Vector3(sx * 1.2f, 1.2f, 1f);
            task.toScale = new Vector3(sx, 1f, 1f);
            task.fromPos = task.toPos = rt.anchoredPosition;
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
            task.toPos = rt.anchoredPosition;
            task.fromPos = task.toPos + offv;
            rt.anchoredPosition = task.fromPos;
        }

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

        for (int i = 0; i < MaxCamera; i++)
        {
            if (_i.cameras[i].active && _i.cameras[i].target)
            {
                _i.cameras[i].target.anchoredPosition = _i.cameras[i].basePos;
                _i.cameras[i].target.localScale = _i.cameras[i].baseScale;
            }
            _i.cameras[i].active = false;
        }

        if (_i.focus.active)
        {
            if (_i.focusOverlay != null)
            {
                var c = _i.focusOverlay.color; c.a = 0f; _i.focusOverlay.color = c;
                _i.focusOverlay.gameObject.SetActive(false);
            }
            if (_i.focus.target != null)
                _i.focus.target.localScale = _i.focus.fromScale;
            _i.focus.active = false;
        }

        if (_i.mask.active)
        {
            _i.mask.active = false;
            if (_i.mask.overlay) _i.mask.overlay.gameObject.SetActive(false);
        }

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

    struct CameraTask
    {
        public bool active;
        public float t, dur, delay;
        public Vector3[] points;
        public AnimationCurve curve;
        public bool relative;
        public RectTransform target;
        public Vector2 basePos;
        public Vector3 baseScale;
        public bool overrideZoom;
        public float targetZoom;
    }

    struct FocusTask
    {
        public bool active;
        public float t, dur, delay;
        public Color fromColor, toColor;
        public RectTransform target;
        public Vector3 fromScale, toScale;
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
        unchecked
        {
            uint x = (uint)seed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }
}
