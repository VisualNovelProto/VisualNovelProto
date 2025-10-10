using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CtcIndicator : MonoBehaviour
{
    [Header("Configuration")]
    public CtcIndicatorConfig config;
    [Tooltip("Automatically apply the configuration during Awake().")]
    public bool applyConfigOnAwake = true;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public RectTransform cornerRoot;
    public RectTransform inlineRoot;
    public TMP_Text inlineSourceText;
    public Image inputIcon;
    public TMP_Text captionLabel;
    public Graphic[] pulseGraphics;
    public Graphic[] shadowGraphics;
    public GameObject autoReplacementRoot;
    public Graphic[] autoReplacementGraphics;

    [Header("Behaviour")]
    public bool autoDetectPlayerInput = true;
    public PlayerInput playerInput;

    struct GraphicCache
    {
        public Graphic graphic;
        public Color baseColor;
    }

    readonly List<GraphicCache> pulseCache = new List<GraphicCache>(8);
    readonly List<GraphicCache> shadowCache = new List<GraphicCache>(4);
    readonly List<GraphicCache> autoReplacementCache = new List<GraphicCache>(4);

    Coroutine fadeRoutine;
    Coroutine blinkRoutine;
    Coroutine delayRoutine;
    Coroutine autoRoutine;

    bool prepared;
    bool typing;
    bool awaitingInput;
    bool hasVisibleCharacters;
    bool blockedByChoices;
    bool blockedByPause;
    bool blockedByModal;
    bool blockedByTransition;
    bool autoMode;
    bool showing;
    bool inlineModeActive;
    string lastControlScheme;

    float pendingDelay;

    CtcIndicatorMode currentMode = CtcIndicatorMode.Corner;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        CacheGraphicColors();

        if (applyConfigOnAwake)
            ApplyConfig();

        if (config != null)
            SetMode(config.defaultMode);
        else
            SetMode(currentMode);

        prepared = true;
        EvaluateVisibility(force: true);
    }

    void OnEnable()
    {
        SubscribeExternalEvents();
        UpdateInputIconImmediate();
        EvaluateVisibility(force: true);
    }

    void OnDisable()
    {
        UnsubscribeExternalEvents();
        StopAllCoroutines();
        fadeRoutine = null;
        blinkRoutine = null;
        delayRoutine = null;
        autoRoutine = null;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        showing = false;
    }

    void SubscribeExternalEvents()
    {
        if (PauseMenu.IsPaused)
            blockedByPause = true;
        PauseMenu.PauseStateChanged += HandlePauseChanged;

        blockedByModal = UiModalGate.IsOpen;
        UiModalGate.StateChanged += HandleModalChanged;

        blockedByTransition = TransitionManager.IsPlaying;
        TransitionManager.StateChanged += HandleTransitionChanged;

        TryRegisterPlayerInput();
    }

    void UnsubscribeExternalEvents()
    {
        PauseMenu.PauseStateChanged -= HandlePauseChanged;
        UiModalGate.StateChanged -= HandleModalChanged;
        TransitionManager.StateChanged -= HandleTransitionChanged;

        if (playerInput != null)
            playerInput.onControlsChanged -= HandleControlsChanged;
    }

    void TryRegisterPlayerInput()
    {
        if (!autoDetectPlayerInput && playerInput == null)
            return;

        if (playerInput == null)
        {
            if (InputRouter.Instance != null && InputRouter.Instance.playerInput != null)
                playerInput = InputRouter.Instance.playerInput;
            else
                playerInput = FindObjectOfType<PlayerInput>();
        }

        if (playerInput != null)
        {
            playerInput.onControlsChanged -= HandleControlsChanged;
            playerInput.onControlsChanged += HandleControlsChanged;
            HandleControlsChanged(playerInput);
        }
    }

    void HandleControlsChanged(PlayerInput source)
    {
        if (source == null)
            return;

        lastControlScheme = source.currentControlScheme;
        UpdateInputIconImmediate();
    }

    void HandlePauseChanged(bool paused)
    {
        blockedByPause = paused;
        EvaluateVisibility();
    }

    void HandleModalChanged(bool open)
    {
        blockedByModal = open;
        EvaluateVisibility();
    }

    void HandleTransitionChanged(bool playing)
    {
        blockedByTransition = playing;
        EvaluateVisibility();
    }

    void CacheGraphicColors()
    {
        pulseCache.Clear();
        if (pulseGraphics != null)
        {
            for (int i = 0; i < pulseGraphics.Length; i++)
            {
                var g = pulseGraphics[i];
                if (g == null) continue;
                pulseCache.Add(new GraphicCache { graphic = g, baseColor = g.color });
            }
        }

        shadowCache.Clear();
        if (shadowGraphics != null)
        {
            for (int i = 0; i < shadowGraphics.Length; i++)
            {
                var g = shadowGraphics[i];
                if (g == null) continue;
                shadowCache.Add(new GraphicCache { graphic = g, baseColor = g.color });
            }
        }

        autoReplacementCache.Clear();
        if (autoReplacementGraphics != null)
        {
            for (int i = 0; i < autoReplacementGraphics.Length; i++)
            {
                var g = autoReplacementGraphics[i];
                if (g == null) continue;
                autoReplacementCache.Add(new GraphicCache { graphic = g, baseColor = g.color });
                g.canvasRenderer.SetAlpha(g.color.a);
            }
        }
    }

    public void ApplyConfig()
    {
        if (config == null)
            return;

        for (int i = 0; i < pulseCache.Count; i++)
        {
            var entry = pulseCache[i];
            if (entry.graphic == null) continue;
            Color c = config.indicatorColor;
            c.a *= entry.baseColor.a;
            entry.graphic.color = c;
            pulseCache[i] = new GraphicCache { graphic = entry.graphic, baseColor = c };
        }

        for (int i = 0; i < shadowCache.Count; i++)
        {
            var entry = shadowCache[i];
            if (entry.graphic == null) continue;
            Color c = config.indicatorShadowColor;
            c.a *= entry.baseColor.a;
            entry.graphic.color = c;
            shadowCache[i] = new GraphicCache { graphic = entry.graphic, baseColor = c };
        }

        for (int i = 0; i < autoReplacementCache.Count; i++)
        {
            var entry = autoReplacementCache[i];
            if (entry.graphic == null) continue;
            Color c = config.indicatorColor;
            c.a *= entry.baseColor.a;
            entry.graphic.color = c;
            autoReplacementCache[i] = new GraphicCache { graphic = entry.graphic, baseColor = c };
        }

        UpdateInputIconImmediate();
        SetMode(config.defaultMode);
    }

    public void SetMode(CtcIndicatorMode mode)
    {
        currentMode = mode;
        inlineModeActive = mode == CtcIndicatorMode.Inline;

        if (cornerRoot != null)
            cornerRoot.gameObject.SetActive(mode == CtcIndicatorMode.Corner);
        if (inlineRoot != null)
            inlineRoot.gameObject.SetActive(mode == CtcIndicatorMode.Inline);

        if (mode == CtcIndicatorMode.Corner && cornerRoot != null && config != null)
            cornerRoot.anchoredPosition = config.cornerOffset;
        if (mode == CtcIndicatorMode.Inline && inlineRoot != null)
            inlineRoot.anchoredPosition3D = Vector3.zero;

        if (autoReplacementRoot != null && autoReplacementRoot.activeSelf)
        {
            if (cornerRoot != null) cornerRoot.gameObject.SetActive(false);
            if (inlineRoot != null) inlineRoot.gameObject.SetActive(false);
        }

        UpdateInlinePosition();
    }

    public void SetInlineSource(TMP_Text source)
    {
        inlineSourceText = source;
        if (inlineModeActive)
            UpdateInlinePosition();
    }

    public void OnLineStarted()
    {
        typing = true;
        awaitingInput = false;
        hasVisibleCharacters = false;
        CancelDelay();
        HideImmediate();
        EvaluateVisibility(force: true);
    }

    public void OnLineContentUpdated(bool hasCharacters)
    {
        hasVisibleCharacters = hasCharacters;
        if (inlineModeActive)
            UpdateInlinePosition();
    }

    public void OnTypingStarted()
    {
        typing = true;
        awaitingInput = false;
        EvaluateVisibility();
    }

    public void OnTypingCompleted()
    {
        typing = false;
        EvaluateVisibility();
    }

    public void OnAwaitingInput()
    {
        awaitingInput = true;
        EvaluateVisibility();
    }

    public void OnAdvanceConsumed()
    {
        awaitingInput = false;
        EvaluateVisibility();
    }

    public void OnChoicesShown()
    {
        blockedByChoices = true;
        awaitingInput = false;
        EvaluateVisibility();
    }

    public void OnChoicesHidden()
    {
        blockedByChoices = false;
        EvaluateVisibility();
    }

    public void OnAutoModeChanged(bool on)
    {
        autoMode = on;
        EvaluateVisibility();
    }

    public void OnPauseStateChanged(bool paused)
    {
        HandlePauseChanged(paused);
    }

    public void OnModalGateChanged(bool open)
    {
        HandleModalChanged(open);
    }

    public void OnTransitionStateChanged(bool playing)
    {
        HandleTransitionChanged(playing);
    }

    void EvaluateVisibility(bool force = false)
    {
        if (!prepared && !force)
            return;

        bool blocked = blockedByChoices || blockedByPause || blockedByModal || blockedByTransition;
        bool shouldShowSpinner = autoMode && config != null && config.enableAutoReplacement && !blocked;
        bool shouldShowIndicator = !shouldShowSpinner && awaitingInput && hasVisibleCharacters && !typing && !blocked;

        if (shouldShowIndicator)
            ShowWithDelay();
        else
            HideIndicator();

        HandleAutoReplacement(shouldShowSpinner);

        float alpha = blocked ? (config != null ? config.blockedAlpha : 0.25f) : 1f;
        ApplyPulseAlpha(alpha);
        ApplyAutoReplacementAlpha(alpha);
    }

    void ShowWithDelay()
    {
        float delay = Mathf.Max(0f, config != null ? config.minimumShowDelay : 0f);
        if (delay <= 0f)
        {
            CancelDelay();
            ShowImmediate();
            return;
        }

        if (showing && delayRoutine == null)
            return;

        if (delayRoutine != null && Mathf.Approximately(pendingDelay, delay))
            return;

        CancelDelay();
        pendingDelay = delay;
        delayRoutine = StartCoroutine(CoDelayShow(delay));
    }

    void CancelDelay()
    {
        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
            delayRoutine = null;
        }
    }

    IEnumerator CoDelayShow(float delay)
    {
        float t = 0f;
        while (t < delay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        delayRoutine = null;
        ShowImmediate();
    }

    void ShowImmediate()
    {
        if (showing)
            return;

        showing = true;
        if (canvasGroup != null)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(CoFade(canvasGroup.alpha, 1f));
        }

        if (blinkRoutine == null)
            blinkRoutine = StartCoroutine(CoBlink());

        if (inlineModeActive)
            UpdateInlinePosition();
    }

    void HideIndicator()
    {
        CancelDelay();
        if (!showing)
            return;

        showing = false;
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
            ApplyPulseAlpha(1f);
        }

        if (canvasGroup != null)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(CoFade(canvasGroup.alpha, 0f));
        }
    }

    void HideImmediate()
    {
        if (canvasGroup != null)
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            canvasGroup.alpha = 0f;
        }
        showing = false;
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
        ApplyPulseAlpha(1f);
        ApplyAutoReplacementAlpha(1f);
    }

    IEnumerator CoFade(float from, float to)
    {
        float duration = config != null ? Mathf.Max(0.01f, config.fadeDuration) : 0.15f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            float value = Mathf.Lerp(from, to, a);
            if (canvasGroup != null)
                canvasGroup.alpha = value;
            yield return null;
        }
        if (canvasGroup != null)
            canvasGroup.alpha = to;
        fadeRoutine = null;
    }

    IEnumerator CoBlink()
    {
        float period = config != null ? Mathf.Clamp(config.blinkPeriod, 0.2f, 2f) : 0.85f;
        float half = Mathf.Max(0.01f, period * 0.5f);
        float t = 0f;
        while (showing)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.PingPong(t, half) / half;
            float alpha = Mathf.Lerp(0.35f, 1f, p);
            ApplyPulseAlpha(alpha);
            if (inlineModeActive)
                UpdateInlinePosition();
            yield return null;
        }
        blinkRoutine = null;
    }

    void ApplyPulseAlpha(float multiplier)
    {
        for (int i = 0; i < pulseCache.Count; i++)
        {
            var entry = pulseCache[i];
            if (entry.graphic == null) continue;
            Color c = entry.baseColor;
            c.a *= multiplier;
            entry.graphic.color = c;
        }
    }

    void ApplyAutoReplacementAlpha(float multiplier)
    {
        for (int i = 0; i < autoReplacementCache.Count; i++)
        {
            var entry = autoReplacementCache[i];
            if (entry.graphic == null) continue;
            Color c = entry.baseColor;
            c.a *= multiplier;
            entry.graphic.color = c;
        }
    }

    void HandleAutoReplacement(bool active)
    {
        if (autoReplacementRoot == null)
            return;

        if (active)
        {
            if (!autoReplacementRoot.activeSelf)
                autoReplacementRoot.SetActive(true);
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            if (cornerRoot != null)
                cornerRoot.gameObject.SetActive(false);
            if (inlineRoot != null)
                inlineRoot.gameObject.SetActive(false);
            if (autoRoutine == null)
                autoRoutine = StartCoroutine(CoSpinAuto());
        }
        else
        {
            if (autoRoutine != null)
            {
                StopCoroutine(autoRoutine);
                autoRoutine = null;
            }
            if (autoReplacementRoot.activeSelf)
            {
                var rt = autoReplacementRoot.GetComponent<RectTransform>();
                if (rt != null)
                    rt.localRotation = Quaternion.identity;
                autoReplacementRoot.SetActive(false);
            }

            SetMode(currentMode);
        }
    }

    IEnumerator CoSpinAuto()
    {
        float speed = config != null ? config.autoSpinnerSpeed : 160f;
        var rt = autoReplacementRoot.GetComponent<RectTransform>();
        while (autoReplacementRoot != null && autoReplacementRoot.activeInHierarchy)
        {
            if (rt != null)
            {
                float delta = -speed * Time.unscaledDeltaTime;
                rt.Rotate(0f, 0f, delta, Space.Self);
            }
            yield return null;
        }
        autoRoutine = null;
    }

    void UpdateInlinePosition()
    {
        if (!inlineModeActive || inlineRoot == null || inlineSourceText == null)
            return;

        if (!inlineSourceText.gameObject.activeInHierarchy)
            return;

        inlineSourceText.ForceMeshUpdate();
        var info = inlineSourceText.textInfo;
        if (info.characterCount == 0)
        {
            inlineRoot.gameObject.SetActive(false);
            return;
        }

        int index = -1;
        for (int i = info.characterCount - 1; i >= 0; i--)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible)
                continue;
            index = i;
            break;
        }

        if (index < 0)
        {
            inlineRoot.gameObject.SetActive(false);
            return;
        }

        inlineRoot.gameObject.SetActive(true);

        var charInfo = info.characterInfo[index];
        Vector3 localPos = (charInfo.topRight + charInfo.bottomRight) * 0.5f;
        Vector3 world = inlineSourceText.transform.TransformPoint(localPos);
        var parentRect = inlineRoot.parent as RectTransform;
        if (parentRect == null)
            parentRect = inlineSourceText.rectTransform;

        var canvas = inlineSourceText.canvas;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2 anchored;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(cam, world),
            cam,
            out anchored);
        Vector2 offset = config != null ? config.inlineOffset : Vector2.zero;
        inlineRoot.anchoredPosition = anchored + offset;
    }

    void UpdateInputIconImmediate()
    {
        if (inputIcon == null)
            return;

        Sprite sprite = null;
        if (config != null)
        {
            if (string.IsNullOrEmpty(lastControlScheme))
            {
                sprite = config.keyboardSprite != null ? config.keyboardSprite : config.mouseSprite;
            }
            else if (lastControlScheme.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sprite = config.gamepadSprite != null ? config.gamepadSprite : config.keyboardSprite;
            }
            else if (lastControlScheme.IndexOf("Touch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sprite = config.touchSprite != null ? config.touchSprite : config.mouseSprite;
            }
            else if (lastControlScheme.IndexOf("Mouse", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sprite = config.mouseSprite != null ? config.mouseSprite : config.keyboardSprite;
            }
            else if (lastControlScheme.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sprite = config.keyboardSprite != null ? config.keyboardSprite : config.mouseSprite;
            }
            else
            {
                sprite = config.keyboardSprite != null ? config.keyboardSprite : config.mouseSprite;
            }
        }

        inputIcon.sprite = sprite;
        inputIcon.enabled = sprite != null;
    }
}
