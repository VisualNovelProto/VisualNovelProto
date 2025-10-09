using UnityEngine;

public enum TypingSpeed
{
    Off = 0,
    Slow = 1,
    Normal = 2,
    Fast = 3
}

public static class TypingConfig
{
    const string PrefKey = "TypingSpeed";

    const string PrefKeyEnabled = "typing_enabled";
    const string PrefKeyCps = "typing_cps";
    const string PrefKeyPunc = "typing_punct_delay";

    public static bool enabled = true;
    public static float cps = 60f;
    public static float punctuationDelay = 0.04f;

    // 문자/초
    public static float GetCharsPerSecond(TypingSpeed s)
    {
        switch (s)
        {
            case TypingSpeed.Slow: return 18f;
            case TypingSpeed.Normal: return 35f;
            case TypingSpeed.Fast: return 60f;
            default: return float.PositiveInfinity; // Off
        }
    }

    public static TypingSpeed Load()
    {
        return (TypingSpeed)PlayerPrefs.GetInt(PrefKey, (int)TypingSpeed.Normal);
    }

    public static void Save(TypingSpeed s)
    {
        PlayerPrefs.SetInt(PrefKey, (int)s);
        PlayerPrefs.Save();
    }

    public static void Apply(bool enabled, float cps, float punctuationExtraDelay)
    {
        TypingConfig.enabled = enabled;
        TypingConfig.cps = Mathf.Max(1f, cps);
        TypingConfig.punctuationDelay = Mathf.Clamp(punctuationExtraDelay, 0f, 0.2f);

        // PlayerPrefs에도 반영(서드파티 UI가 직접 참조할 경우 대비)
        PlayerPrefs.SetInt(PrefKeyEnabled, enabled ? 1 : 0);
        PlayerPrefs.SetFloat(PrefKeyCps, TypingConfig.cps);
        PlayerPrefs.SetFloat(PrefKeyPunc, TypingConfig.punctuationDelay);
        PlayerPrefs.Save();
    }
}
