using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class GlossaryDatabase : ScriptableObject
{
    public const int MaxGlossary = 4096;

    [NonSerialized] public GlossaryEntry[] entries = new GlossaryEntry[MaxGlossary];
    [NonSerialized] public bool[] present = new bool[MaxGlossary];
    [NonSerialized] public int entryCount;

    // 수집 상태 비트셋(세이브 대상)
    [NonSerialized] public GlossarySet owned;

    // Resources/Story/glossary.csv 로드 헬퍼
    public static GlossaryDatabase LoadFromResources(string pathWithoutExt = "StoryText/glossary")
    {
        TextAsset csv = Resources.Load<TextAsset>(pathWithoutExt);

        if (csv == null)
        {
            throw new Exception($"glossary csv not found: Resources/{pathWithoutExt}.csv");
        }

        var db = CreateInstance<GlossaryDatabase>();
        db.LoadFromCsvText(csv.text);
        return db;
    }

    // CSV 형식:
    // id,label,colorHex,desc,thumb,category
    public void LoadFromCsvText(string csvText)
    {
        entryCount = 0;
        owned.Clear();

        for (int i = 0; i < MaxGlossary; i++)
        {
            present[i] = false;
            entries[i] = default;
        }

        using (StringReader r = new StringReader(csvText))
        {
            string line = r.ReadLine(); // header skip
            if (line == null)
            {
                return;
            }

            while ((line = r.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                ParseLine(line,
                    out int id,
                    out string label,
                    out string colorHex,
                    out string desc,
                    out string thumb,
                    out string category
                );

                if ((uint)id >= MaxGlossary)
                {
                    throw new Exception($"glossary id out of range: {id} (0..{MaxGlossary - 1})");
                }
                entries[id] = new GlossaryEntry
                {
                    id = id,
                    label = label,
                    colorHex = NormalizeColor(colorHex),
                    desc = desc,
                    thumb = thumb,
                    category = category
                };
                present[id] = true;

                // entryCount는 최대 사용된 id+1 로 유지(희소 아이디 허용)
                if (id + 1 > entryCount)
                {
                    entryCount = id + 1;
                }
            }
        }
    }

    static void ParseLine(string line, out int id, out string label, out string colorHex, out string desc, out string thumb, out string category)
    {
        string[] slots = new string[6];
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
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    slots[si++] = sb.ToString();
                    sb.Length = 0;
                    if (si >= slots.Length)
                    {
                        break;
                    }
                }
                else if (c == '"')
                {
                    inQuote = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        if (si < slots.Length)
        {
            slots[si] = sb.ToString();
        }

        id = SafeAtoi(slots[0]);
        label = slots[1]?.Trim();
        colorHex = slots[2]?.Trim();
        desc = slots[3] ?? string.Empty;
        thumb = slots[4] ?? string.Empty;
        category = slots[5] ?? string.Empty;
    }

    static int SafeAtoi(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0;
        }
        int i = 0, n = s.Length, sign = 1, v = 0;

        if (s[0] == '-')
        {
            sign = -1; i = 1;
        }
        for (; i < n; i++)
        {
            int d = s[i] - '0';
            if (d < 0 || d > 9)
            {
                break;
            }
            v = v * 10 + d;
        }
        return v * sign;
    }

    static string NormalizeColor(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return "#FFD95A";
        }
        s = s.Trim();
        return s[0] == '#' ? s : ("#" + s);
    }

    public bool Exists(int id)
    {
        return (uint)id < MaxGlossary && present[id];
    }

    public bool TryGetEntry(int id, out GlossaryEntry e)
    {
        if (Exists(id))
        {
            e = entries[id];
            return true;
        }
        e = default;
        return false;
    }
}
