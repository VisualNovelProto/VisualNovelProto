using UnityEngine;

public enum CtcIndicatorMode
{
    Corner = 0,
    Inline = 1,
}

[CreateAssetMenu(fileName = "CtcIndicatorConfig", menuName = "Dialogue/CTC Indicator Config", order = 501)]
public sealed class CtcIndicatorConfig : ScriptableObject
{
    [Header("Layout")]
    public CtcIndicatorMode defaultMode = CtcIndicatorMode.Corner;
    [Tooltip("Offset applied to the indicator when using corner mode (anchored to bottom-right by default).")]
    public Vector2 cornerOffset = new Vector2(-48f, 40f);
    [Tooltip("Offset applied when inline mode is active. Values are in local space of the text's rect transform.")]
    public Vector2 inlineOffset = new Vector2(18f, -6f);

    [Header("Timing")]
    [Tooltip("Blink cycle in seconds.")]
    [Range(0.2f, 2f)]
    public float blinkPeriod = 0.85f;
    [Tooltip("Fade duration when switching visibility states.")]
    [Range(0.01f, 0.75f)]
    public float fadeDuration = 0.18f;
    [Tooltip("Minimum delay before the indicator becomes visible once the line is ready (seconds).")]
    [Range(0f, 1f)]
    public float minimumShowDelay = 0.05f;

    [Header("Auto/Skip Mode")]
    [Tooltip("If true, auto/skip modes show an alternative lightweight spinner instead of the main indicator.")]
    public bool enableAutoReplacement = true;
    [Tooltip("Rotation speed for the auto/skip replacement spinner (degrees per second).")]
    public float autoSpinnerSpeed = 160f;

    [Header("Appearance")]
    public Color indicatorColor = Color.white;
    public Color indicatorShadowColor = new Color(0f, 0f, 0f, 0.25f);
    [Tooltip("Alpha applied when the indicator is temporarily blocked.")]
    [Range(0f, 1f)]
    public float blockedAlpha = 0.2f;

    [Header("Input Icons")]
    [Tooltip("Sprite used when the last active device is keyboard.")]
    public Sprite keyboardSprite;
    [Tooltip("Sprite used when the last active device is mouse / pointer.")]
    public Sprite mouseSprite;
    [Tooltip("Sprite used when the last active device is a gamepad.")]
    public Sprite gamepadSprite;
    [Tooltip("Sprite used when the last active device is touch.")]
    public Sprite touchSprite;
}
