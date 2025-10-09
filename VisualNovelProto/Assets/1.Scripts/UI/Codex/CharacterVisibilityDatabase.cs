using System;
using System.IO;
using System.Text;
using UnityEngine;

public struct CharacterVisibility
{
    public int id;
    public int titleFlag;
    public int descFlag;
    public int abilityNameFlag;
    public int abilityDescFlag;
    public int penaltyNameFlag;
    public int penaltyDescFlag;
    public int groupFlag;
    public int thumbFlag;
}

public sealed class CharacterVisibilityDatabase : ScriptableObject
{
    public const int MaxCharacters = 4096;

    [NonSerialized] public CharacterVisibility[] vis = new CharacterVisibility[MaxCharacters];
    [NonSerialized] public bool[] present = new bool[MaxCharacters];

    public static CharacterVisibilityDatabase LoadFromResources(string path = "StoryText/character_visibility")
    {
        TextAsset csv = Resources.Load<TextAsset>(path);
        if (csv == null) return null; // 없어도 동작
        var db = CreateInstance<CharacterVisibilityDatabase>();
        db.LoadFromCsvText(csv.text);
        return db;
    }

    public void LoadFromCsvText(string text)
    {
        for (int i = 0; i < MaxCharacters; i++) { present[i] = false; vis[i] = default; }

        using (var r = new StringReader(text))
        {
            string line = r.ReadLine(); // header
            while ((line = r.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] s = SplitCsv16(line, 9);

                int id = Atoi(s[0]);
                if ((uint)id >= MaxCharacters) continue;

                vis[id].id = id;
                vis[id].titleFlag = ParseFlag(s[1]);
                vis[id].descFlag = ParseFlag(s[2]);
                vis[id].abilityNameFlag = ParseFlag(s[3]);
                vis[id].abilityDescFlag = ParseFlag(s[4]);
                vis[id].penaltyNameFlag = ParseFlag(s[5]);
                vis[id].penaltyDescFlag = ParseFlag(s[6]);
                vis[id].groupFlag = ParseFlag(s[7]);
                vis[id].thumbFlag = ParseFlag(s[8]);

                present[id] = true;
            }
        }
    }

    public bool TryGet(int id, out CharacterVisibility v)
    {
        if ((uint)id < MaxCharacters && present[id]) { v = vis[id]; return true; }
        v = default; return false;
    }

    // ===== util =====
    static int ParseFlag(string s)
    {
        // 빈칸/0/"null" → 즉시 공개(0)
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();
        if (s == "0") return 0;
        if (string.Equals(s, "null", System.StringComparison.OrdinalIgnoreCase)) return 0;

        return Atoi(s); // 그 외는 실제 플래그 ID
    }

    static int Atoi(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        int sign = 1, i = 0, n = s.Length, v = 0;
        if (s[0] == '-') { sign = -1; i = 1; }
        for (; i < n; i++)
        {
            int d = s[i] - '0';
            if (d < 0 || d > 9) break;
            v = v * 10 + d;
        }
        return v * sign;
    }

    static string[] SplitCsv16(string line, int expect)
    {
        var arr = new string[expect];
        int si = 0; var sb = new StringBuilder(128); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else q = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == ',') { arr[si++] = sb.ToString(); sb.Length = 0; if (si >= expect) break; }
                else if (c == '"') q = true;
                else sb.Append(c);
            }
        }
        if (si < expect) arr[si] = sb.ToString();
        return arr;
    }
}
