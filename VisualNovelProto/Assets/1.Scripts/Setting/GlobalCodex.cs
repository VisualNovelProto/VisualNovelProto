// GlobalCodex.cs
using System.Collections.Generic;
using UnityEngine;

public static class GlobalCodex
{
    const string KeyG = "codex.glossary.v1";
    const string KeyC = "codex.characters.v1";

    [System.Serializable] class OwnedIds { public int[] ids; }

    // ----- Load -----
    public static void LoadInto(GlossaryDatabase gdb, CharacterDatabase cdb)
    {
        if (gdb != null)
        {
            gdb.owned.Clear();
            foreach (var id in LoadIds(KeyG)) if ((uint)id < GlossaryDatabase.MaxGlossary) gdb.owned.Set(id);
        }
        if (cdb != null)
        {
            cdb.owned.Clear();
            foreach (var id in LoadIds(KeyC)) if ((uint)id < CharacterDatabase.MaxCharacters) cdb.owned.Set(id);
        }
    }

    // ----- Save (전체 저장) -----
    public static void SaveFrom(GlossaryDatabase gdb, CharacterDatabase cdb)
    {
        if (gdb != null) SaveIds(KeyG, EnumerateOwned(gdb.entryCount, gdb.owned.Has));
        if (cdb != null) SaveIds(KeyC, EnumerateOwned(cdb.entryCount, cdb.owned.Has));
        PlayerPrefs.Save();
    }

    // ----- 증분 추가(해금 순간에 호출) -----
    public static bool AddGlossary(GlossaryDatabase gdb, int id)
    {
        if (gdb == null || (uint)id >= GlossaryDatabase.MaxGlossary) return false;
        if (gdb.owned.Has(id)) return false;
        gdb.owned.Set(id);
        SaveIds(KeyG, MergeIds(LoadIds(KeyG), id));
        PlayerPrefs.Save();
        return true;
    }

    public static bool AddCharacter(CharacterDatabase cdb, int id)
    {
        if (cdb == null || (uint)id >= CharacterDatabase.MaxCharacters) return false;
        if (cdb.owned.Has(id)) return false;
        cdb.owned.Set(id);
        SaveIds(KeyC, MergeIds(LoadIds(KeyC), id));
        PlayerPrefs.Save();
        return true;
    }

    // ===== 내부 유틸 =====
    static IEnumerable<int> EnumerateOwned(int max, System.Func<int, bool> has)
    {
        var list = new List<int>(256);
        for (int i = 0; i < max; i++) if (has(i)) list.Add(i);
        return list;
    }

    static int[] LoadIds(string key)
    {
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return System.Array.Empty<int>();
        var obj = JsonUtility.FromJson<OwnedIds>(json);
        return obj?.ids ?? System.Array.Empty<int>();
    }

    static void SaveIds(string key, IEnumerable<int> ids)
    {
        var arr = (ids is int[] a) ? a : new List<int>(ids).ToArray();
        var json = JsonUtility.ToJson(new OwnedIds { ids = arr }, false);
        PlayerPrefs.SetString(key, json);
    }

    static int[] MergeIds(int[] oldIds, int add)
    {
        // 중복 방지
        for (int i = 0; i < oldIds.Length; i++) if (oldIds[i] == add) return oldIds;
        var list = new List<int>(oldIds.Length + 1);
        list.AddRange(oldIds);
        list.Add(add);
        return list.ToArray();
    }
}
