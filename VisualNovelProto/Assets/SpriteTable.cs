using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Sprite Table")]
public class SpriteTable : ScriptableObject
{
    [Serializable]
    public struct Entry { public string key; public Sprite sprite; }

    public Entry[] entries;
    private Dictionary<string, Sprite> map;

    public void Build()
    {
        map = new Dictionary<string, Sprite>();
        if (entries == null) return;
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.key) && e.sprite != null)
                map[e.key] = e.sprite; // 중복 키면 마지막이 유효
    }

    public bool TryGet(string key, out Sprite sprite)
    {
        if (map == null) Build();
        if (map != null && map.TryGetValue(key, out sprite)) return true;
        sprite = null; return false;
    }
}
