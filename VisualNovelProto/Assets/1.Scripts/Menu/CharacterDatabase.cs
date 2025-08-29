using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class CharacterDatabase : ScriptableObject
{
    public const int MaxCharacters = 4096;

    [NonSerialized] public CharacterEntry[] entries = new CharacterEntry[MaxCharacters];
    [NonSerialized] public bool[] present = new bool[MaxCharacters];
    [NonSerialized] public int entryCount;

    [NonSerialized] public CharacterSet owned;

    public static CharacterDatabase LoadFromResources(string pathWithoutExt = "Story/characters")
    {
        TextAsset csv = Resources.Load<TextAsset>(pathWithoutExt);
        if (csv == null) throw new Exception($"characters csv not found: Resources/{pathWithoutExt}.csv");
        var db = CreateInstance<CharacterDatabase>();
        db.LoadFromCsvText(csv.text);
        return db;
    }

    // CSV 헤더:
    // id,name,colorHex,desc,thumb,group,abilityName,abilityDesc,penaltyName,penaltyDesc
    public void LoadFromCsvText(string csvText)
    {
        entryCount = 0; owned.Clear();
        for (int i = 0; i < MaxCharacters; i++) { present[i] = false; entries[i] = default; }

        using (StringReader r = new StringReader(csvText))
        {
            string line = r.ReadLine(); if (line == null) return; // header

            while ((line = r.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ParseLine(line,
                    out int id, out string name, out string colorHex,
                    out string desc, out string thumb, out string group,
                    out string abilityName, out string abilityDesc,
                    out string penaltyName, out string penaltyDesc);

                if ((uint)id >= MaxCharacters) throw new Exception($"character id out of range: {id}");

                entries[id] = new CharacterEntry
                {
                    id = id,
                    name = name,
                    colorHex = NormalizeColor(colorHex),
                    desc = desc,
                    thumb = thumb,
                    group = group,
                    abilityName = abilityName,
                    abilityDesc = abilityDesc,
                    penaltyName = penaltyName,
                    penaltyDesc = penaltyDesc
                };
                present[id] = true;
                if (id + 1 > entryCount) entryCount = id + 1;
            }
        }
    }

    static void ParseLine(
        string line,
        out int id, out string name, out string colorHex,
        out string desc, out string thumb, out string group,
        out string abilityName, out string abilityDesc,
        out string penaltyName, out string penaltyDesc)
    {
        // 10칸 파싱
        string[] slots = new string[10];
        int si = 0;
        var sb = new StringBuilder(256);
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuote = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == ',')
                {
                    slots[si++] = sb.ToString();
                    sb.Length = 0;
                    if (si >= slots.Length) break;
                }
                else if (c == '"') inQuote = true;
                else sb.Append(c);
            }
        }
        if (si < slots.Length) slots[si] = sb.ToString();

        id = SafeAtoi(slots[0]);
        name = slots[1]?.Trim();
        colorHex = slots[2]?.Trim();
        desc = slots[3] ?? string.Empty;
        thumb = slots[4] ?? string.Empty;
        group = slots[5] ?? string.Empty;
        abilityName = slots[6]?.Trim();
        abilityDesc = slots[7] ?? string.Empty;
        penaltyName = slots[8]?.Trim();
        penaltyDesc = slots[9] ?? string.Empty;
    }

    static int SafeAtoi(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        int sign = 1, i = 0, n = s.Length, val = 0;
        if (s[0] == '-') { sign = -1; i = 1; }
        for (; i < n; i++)
        {
            int d = s[i] - '0';
            if (d < 0 || d > 9) break;
            val = val * 10 + d;
        }
        return val * sign;
    }

    static string NormalizeColor(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "#A8D8FF"; // 기본 캐릭터색
        s = s.Trim();
        return s[0] == '#' ? s : ("#" + s);
    }

    public bool Exists(int id) { return (uint)id < MaxCharacters && present[id]; }

    public bool TryGetEntry(int id, out CharacterEntry e)
    {
        if (Exists(id)) { e = entries[id]; return true; }
        e = default; return false;
    }
}
