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

    // ¹®ÀÚ/ÃÊ
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
}
