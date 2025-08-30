using System;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioBank", menuName = "StoryGame/AudioBank")]
public sealed class AudioBank : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string key;      // ex) "bgm_town_day", "door_open"
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    public Entry[] items = Array.Empty<Entry>();

    public bool TryGet(string key, out AudioClip clip, out float vol)
    {
        for (int i = 0; i < items.Length; i++)
            if (items[i].clip != null && string.Equals(items[i].key, key, StringComparison.Ordinal))
            { clip = items[i].clip; vol = (items[i].volume <= 0f ? 1f : items[i].volume); return true; }
        clip = null; vol = 1f; return false;
    }
}
