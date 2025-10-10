using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "VN/Emotion Style Library")]
public sealed class EmotionStyleLibrary : ScriptableObject
{
    [Serializable]
    public struct EmotionStyle
    {
        public bool overrideBodyFont;
        public TMP_FontAsset bodyFont;
        public bool overrideSpeakerFont;
        public TMP_FontAsset speakerFont;
        public bool overrideBodyTextColor;
        public Color bodyTextColor;
        public bool overrideSpeakerTextColor;
        public Color speakerTextColor;
        public bool overrideBodyPanelSprite;
        public Sprite bodyPanelSprite;
        public bool overrideSpeakerPanelSprite;
        public Sprite speakerPanelSprite;
        public bool overrideBodyPanelColor;
        public Color bodyPanelColor;
        public bool overrideSpeakerPanelColor;
        public Color speakerPanelColor;
    }

    [Serializable]
    struct Entry
    {
        public string key;
        public EmotionStyle style;
    }

    [SerializeField] Entry[] entries;

    Dictionary<string, EmotionStyle> cache;

    void OnEnable()
    {
        cache = null;
    }

    void EnsureCache()
    {
        if (cache != null) return;
        cache = new Dictionary<string, EmotionStyle>(StringComparer.OrdinalIgnoreCase);
        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            string key = entries[i].key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            cache[key.Trim()] = entries[i].style;
        }
    }

    public bool TryGetStyle(string key, out EmotionStyle style)
    {
        EnsureCache();
        if (cache == null)
        {
            style = default;
            return false;
        }
        return cache.TryGetValue(key?.Trim() ?? string.Empty, out style);
    }
}
