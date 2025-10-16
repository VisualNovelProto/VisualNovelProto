#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TimeLoopSheetCsvImporter
{
    const string MenuPath = "Tools/Time Loop/Import Sheet From CSV...";

    [MenuItem(MenuPath, priority = 1000)]
    static void ImportInteractive()
    {
        string csvPath = EditorUtility.OpenFilePanel("Select Time Loop CSV", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(csvPath))
            return;

        string defaultAssetPath = GetDefaultAssetPath();
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Select TimeLoopSheet asset",
            string.IsNullOrEmpty(defaultAssetPath) ? "TimeLoopSheet" : Path.GetFileNameWithoutExtension(defaultAssetPath),
            "asset",
            "Choose the TimeLoopSheet asset to create or overwrite.",
            string.IsNullOrEmpty(defaultAssetPath) ? "Assets" : Path.GetDirectoryName(defaultAssetPath));

        if (string.IsNullOrEmpty(assetPath))
            return;

        ImportFromCsv(csvPath, assetPath);
    }

    static string GetDefaultAssetPath()
    {
        var active = Selection.activeObject as TimeLoopSheet;
        if (active == null)
            return string.Empty;
        return AssetDatabase.GetAssetPath(active);
    }

    public static void ImportFromCsv(string csvPath, string assetPath)
    {
        if (string.IsNullOrEmpty(csvPath))
            throw new ArgumentException("CSV path must be provided", nameof(csvPath));
        if (string.IsNullOrEmpty(assetPath))
            throw new ArgumentException("Asset path must be provided", nameof(assetPath));
        if (!File.Exists(csvPath))
            throw new FileNotFoundException("CSV file not found", csvPath);

        string csvText = File.ReadAllText(csvPath, Encoding.UTF8);
        EnsureDirectory(assetPath);

        var sheet = AssetDatabase.LoadAssetAtPath<TimeLoopSheet>(assetPath);
        bool created = false;
        if (sheet == null)
        {
            sheet = ScriptableObject.CreateInstance<TimeLoopSheet>();
            AssetDatabase.CreateAsset(sheet, assetPath);
            created = true;
        }

        ApplyCsvToSheet(sheet, csvText);
        EditorUtility.SetDirty(sheet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TimeLoopSheetCsvImporter] {(created ? "Created" : "Updated")} {assetPath} from {csvPath}");
    }

    static void EnsureDirectory(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(directory))
            return;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    static void ApplyCsvToSheet(TimeLoopSheet sheet, string csvText)
    {
        var slots = ParseSlots(csvText);
        var slotArray = new TimeLoopSlot[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            var builder = slots[i];
            var slot = new TimeLoopSlot
            {
                label = builder.label ?? string.Empty,
                minuteOfDay = builder.minuteOfDay >= 0 ? builder.minuteOfDay : builder.slotIndex * 30,
                notes = builder.notes ?? string.Empty,
                branches = builder.branches.ToArray()
            };
            slotArray[i] = slot;
        }

        sheet.slots = slotArray;
        EditorUtility.SetDirty(sheet);
    }

    sealed class SlotBuilder
    {
        public int slotIndex;
        public string label;
        public int minuteOfDay = -1;
        public string notes;
        public readonly List<TimeLoopSlotBranch> branches = new List<TimeLoopSlotBranch>();
    }

    static List<SlotBuilder> ParseSlots(string csvText)
    {
        var slotMap = new Dictionary<int, SlotBuilder>();
        int currentLine = 0;
        using (StringReader reader = new StringReader(csvText))
        {
            string header = reader.ReadLine();
            currentLine++;
            if (header == null)
                return new List<SlotBuilder>();

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;
                currentLine++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line);
                if (fields.Count == 0)
                    continue;

                int slotIndex = ParseInt(fields, 0, required: true, defaultValue: 0, lineNumber: currentLine);
                if (!slotMap.TryGetValue(slotIndex, out var builder))
                {
                    builder = new SlotBuilder { slotIndex = slotIndex };
                    slotMap.Add(slotIndex, builder);
                }

                string label = GetField(fields, 1);
                if (!string.IsNullOrEmpty(label))
                    builder.label = label;

                int minute = ParseInt(fields, 2, required: false, defaultValue: builder.minuteOfDay, lineNumber: currentLine);
                if (minute >= 0)
                    builder.minuteOfDay = minute;

                string notes = GetField(fields, 3);
                if (!string.IsNullOrEmpty(notes))
                    builder.notes = notes;

                string branchName = GetField(fields, 4);
                string branchDesc = GetField(fields, 5);
                string storyIndexKey = GetField(fields, 6);
                int explicitNodeId = ParseInt(fields, 7, required: false, defaultValue: -1, lineNumber: currentLine);
                string requirements = GetField(fields, 8);

                if (!string.IsNullOrEmpty(branchName) || !string.IsNullOrEmpty(storyIndexKey) || explicitNodeId >= 0 || !string.IsNullOrEmpty(requirements))
                {
                    var branch = new TimeLoopSlotBranch
                    {
                        branchName = branchName ?? string.Empty,
                        description = branchDesc ?? string.Empty,
                        storyIndexKey = storyIndexKey ?? string.Empty,
                        explicitNodeId = explicitNodeId,
                        requiredKnowledgeKeys = ParseRequirements(requirements)
                    };
                    builder.branches.Add(branch);
                }
            }
        }

        var slots = new List<SlotBuilder>(slotMap.Values);
        slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        return slots;
    }

    static string[] ParseRequirements(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return Array.Empty<string>();

        var parts = field.Split(new[] { '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();
        return parts;
    }

    static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
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
                    fields.Add(sb.ToString());
                    sb.Length = 0;
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

        fields.Add(sb.ToString());
        return fields;
    }

    static string GetField(List<string> fields, int index)
    {
        if (index < 0 || index >= fields.Count)
            return string.Empty;
        return fields[index]?.Trim();
    }

    static int ParseInt(List<string> fields, int index, bool required, int defaultValue, int lineNumber)
    {
        string raw = GetField(fields, index);
        if (string.IsNullOrEmpty(raw))
        {
            if (required)
                throw new FormatException($"Missing required integer at column {index + 1} on line {lineNumber}.");
            return defaultValue;
        }

        if (int.TryParse(raw, out int value))
            return value;

        if (required)
            throw new FormatException($"Failed to parse integer at column {index + 1} on line {lineNumber}: '{raw}'");

        return defaultValue;
    }
}
#endif
