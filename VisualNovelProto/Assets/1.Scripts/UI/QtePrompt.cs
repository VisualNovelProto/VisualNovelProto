using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class QtePrompt : MonoBehaviour
{
    public Image fillImage;
    public TMP_Text countdownLabel;
    public RectTransform pulseTarget;
    public float baseScale = 1f;

    Action onTimeout;
    Coroutine countdownCo;

    public void Show(float duration, float pulsePeriod, float pulseStrength, Action timeoutCallback)
    {
        if (pulseTarget != null)
            baseScale = pulseTarget.localScale.x;
        onTimeout = timeoutCallback;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (countdownCo != null) StopCoroutine(countdownCo);
        countdownCo = StartCoroutine(CoCountdown(duration, pulsePeriod, pulseStrength));
    }

    public void Cancel()
    {
        if (countdownCo != null)
        {
            StopCoroutine(countdownCo);
            countdownCo = null;
        }
        if (pulseTarget != null) pulseTarget.localScale = Vector3.one * baseScale;
        onTimeout = null;
    }

    public void HideImmediate()
    {
        Cancel();
        if (fillImage != null) fillImage.fillAmount = 0f;
        if (countdownLabel != null) countdownLabel.text = string.Empty;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    IEnumerator CoCountdown(float duration, float pulsePeriod, float pulseStrength)
    {
        float total = Mathf.Max(0.1f, duration);
        float remaining = total;
        float freq = pulsePeriod > 0f ? (Mathf.PI * 2f) / pulsePeriod : Mathf.PI * 2f;
        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(remaining / total);
            if (fillImage != null) fillImage.fillAmount = normalized;
            if (countdownLabel != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
                countdownLabel.text = seconds.ToString();
            }
            if (pulseTarget != null)
            {
                float phase = Mathf.Sin((total - remaining) * freq) * 0.5f + 0.5f;
                float scale = baseScale * Mathf.Lerp(1f, 1f + pulseStrength, phase);
                pulseTarget.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        Cancel();
        onTimeout?.Invoke();
    }
}
