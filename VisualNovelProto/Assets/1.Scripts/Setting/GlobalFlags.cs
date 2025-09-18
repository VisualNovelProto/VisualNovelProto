using System.Collections.Generic;
using UnityEngine;

public static class GlobalFlags
{
    const string Key = "flags.ever.v1";
    [System.Serializable] class Box { public int[] ids; }

    static HashSet<int> _set;

    static void Ensure()
    {
        if (_set != null) return;
        var json = PlayerPrefs.GetString(Key, "");
        var arr = string.IsNullOrEmpty(json) ? System.Array.Empty<int>()
                  : (JsonUtility.FromJson<Box>(json)?.ids ?? System.Array.Empty<int>());
        _set = new HashSet<int>(arr);
        Debug.Log($"[GlobalFlags] loaded {arr.Length} ids");
    }

    static void Save()
    {
        var arr = new int[_set.Count]; _set.CopyTo(arr);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Box { ids = arr }, false));
        PlayerPrefs.Save();
        Debug.Log($"[GlobalFlags] saved {_set.Count} ids");
    }

    public static bool Has(int id)
    {
        if (id <= 0) return false;
        Ensure(); return _set.Contains(id);
    }

    public static bool Add(int id)
    {
        if (id <= 0) return false;
        Ensure(); if (_set.Add(id)) { Save(); return true; }
        return false;
    }

    public static void AddRange(int[] pool, int offset, int count)
    {
        if (pool == null || count <= 0) return;
        Ensure(); bool changed = false;
        for (int i = 0; i < count; i++)
        {
            int v = pool[offset + i];
            if (v > 0 && _set.Add(v)) changed = true;
        }
        if (changed) Save();
    }
}
