using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class DialogueDatabase : ScriptableObject
{
    [NonSerialized] public DialogueNode[] nodes = Array.Empty<DialogueNode>();
    [NonSerialized] public Choice[] choicesPool = Array.Empty<Choice>();
    [NonSerialized] public int[] flagsPool = Array.Empty<int>();

    [NonSerialized] public int nodeCount;
    [NonSerialized] public int choiceCount;
    [NonSerialized] public int flagRefCount;

    readonly Dictionary<int, int> _nodeIndexById = new Dictionary<int, int>();
    readonly Dictionary<string, int> _indexKeyLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public const string CsvHeaderLegacy =
        "Index,nodeId,rowType,speaker,text,voice,actors,bgm,sfx,cg,transition,advancePolicy,nextNodeId,choiceLabel,choiceGoto,choiceSet,flagsSet,flagsReq";
    public const string CsvHeaderNew =
        "Index,nodeId,rowType,speaker,text,actors,bgm,sfx,cg,transition,advancePolicy,nextNodeId,choiceLabel,choiceGoto,choiceSet,flagsSet,flagsReq";

    struct HeaderInfo
    {
        public bool hasVoice;
        public bool hasLoopKnowledge;
    }

    public static DialogueDatabase LoadFromResources(string path = "StoryText/main")
    {
        TextAsset csv = Resources.Load<TextAsset>(path);
        if (csv == null) throw new Exception($"CSV not found: Resources/{path}.csv");
        var db = CreateInstance<DialogueDatabase>();
        db.LoadFromCsvText(csv.text);
        return db;
    }

    public void LoadFromCsvText(string csvText)
    {
        ResetState();
        if (string.IsNullOrWhiteSpace(csvText))
            return;

        var nodeList = new List<DialogueNode>(1024);
        var choiceList = new List<Choice>(1024);
        var flagList = new List<int>(1024);

        using (StringReader sr = new StringReader(csvText))
        {
            string line = sr.ReadLine();
            if (line == null) throw new Exception("CSV empty");
            HeaderInfo headerInfo = AnalyzeHeader(line);

            int lineNo = 1;
            while ((line = sr.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                ParseCsvLine(line, headerInfo, out CsvFields f);

                if (string.Equals(f.rowType, "Choice", StringComparison.OrdinalIgnoreCase))
                {
                    int parentId = ParseParentId(f.nodeIdText);
                    if (string.IsNullOrWhiteSpace(f.choiceLabel))
                        continue;

                    int parentIdx = GetOrCreateNodeShell(nodeList, parentId);
                    AddChoice(nodeList, choiceList, flagList, parentIdx, f.choiceLabel.Trim(), SafeAtoi(f.choiceGoto), f.choiceSet);
                    continue;
                }

                int nid = SafeAtoi(f.nodeIdText);
                if (nid < 0)
                    throw new Exception($"nodeId must be >= 0 at line {lineNo}: {f.nodeIdText}");

                int index;
                DialogueNode node;
                if (_nodeIndexById.TryGetValue(nid, out index))
                {
                    node = nodeList[index];
                }
                else
                {
                    index = nodeList.Count;
                    node = default;
                    node.nodeId = nid;
                    nodeList.Add(node);
                    _nodeIndexById[nid] = index;
                }

                FillNode(ref node, f, flagList);
                nodeList[index] = node;

                if (!string.IsNullOrEmpty(node.indexKey))
                    _indexKeyLookup[node.indexKey] = node.nodeId;
            }
        }

        nodes = nodeList.ToArray();
        nodeCount = nodes.Length;

        choicesPool = choiceList.ToArray();
        choiceCount = choicesPool.Length;

        flagsPool = flagList.ToArray();
        flagRefCount = flagsPool.Length;
    }

    void ResetState()
    {
        nodes = Array.Empty<DialogueNode>();
        choicesPool = Array.Empty<Choice>();
        flagsPool = Array.Empty<int>();
        nodeCount = choiceCount = flagRefCount = 0;
        _nodeIndexById.Clear();
        _indexKeyLookup.Clear();
    }

    int GetOrCreateNodeShell(List<DialogueNode> nodeList, int nodeId)
    {
        if (nodeId < 0)
            throw new Exception($"Choice references invalid parent id: {nodeId}");

        if (_nodeIndexById.TryGetValue(nodeId, out int existing))
            return existing;

        DialogueNode shell = default;
        shell.nodeId = nodeId;
        shell.indexKey = string.Empty;
        shell.nextNodeId = -1;

        int idx = nodeList.Count;
        nodeList.Add(shell);
        _nodeIndexById[nodeId] = idx;
        return idx;
    }

    void FillNode(ref DialogueNode node, CsvFields f, List<int> flagList)
    {
        node.indexKey = string.IsNullOrEmpty(f.indexKey) ? node.indexKey : f.indexKey;
        node.rowType = f.rowType;
        node.speaker = f.speaker;
        node.text = f.text;
        node.voice = f.voice;

        string spec = f.actors?.Trim();
        if (!string.IsNullOrEmpty(spec))
        {
            bool looksLikeLegacyKey = (spec.IndexOf('@') < 0) && (spec.IndexOf(';') < 0) && (spec.IndexOf(' ') < 0);
            node.actors = looksLikeLegacyKey ? $"{spec}@C(in=fade)" : spec;
        }
        else
        {
            node.actors = null;
        }

        node.bgm = f.bgm;
        node.sfx = f.sfx;
        node.cg = f.cg;
        node.transition = f.transition;
        node.advancePolicy = f.advancePolicy;
        node.loopKnowledge = f.timeLoopKnowledge;
        node.nextNodeId = SafeAtoi(f.nextNodeIdText);

        node.flagsSetOffset = flagList.Count;
        node.flagsSetCount = ParseFlagsField(f.flagsSet, flagList);

        node.flagsReqOffset = flagList.Count;
        node.flagsReqCount = ParseFlagsField(f.flagsReq, flagList);
    }

    void AddChoice(List<DialogueNode> nodeList, List<Choice> choiceList, List<int> flagList,
                   int parentIndex, string label, int gotoId, string choiceSetField)
    {
        var parent = nodeList[parentIndex];
        Choice ch = new Choice
        {
            label = label,
            gotoNodeId = gotoId,
            setOffset = flagList.Count
        };
        ch.setCount = ParseFlagsField(choiceSetField, flagList);

        if (parent.choiceCount == 0)
            parent.choiceOffset = choiceList.Count;
        parent.choiceCount++;

        nodeList[parentIndex] = parent;
        choiceList.Add(ch);
    }

    public bool TryGetNodeById(int nodeId, out DialogueNode node, out int index)
    {
        if (_nodeIndexById.TryGetValue(nodeId, out index))
        {
            if ((uint)index < (uint)nodes.Length)
            {
                node = nodes[index];
                return true;
            }
        }

        index = -1;
        node = default;
        return false;
    }

    public ReadOnlySpan<Choice> GetChoicesOf(ref DialogueNode node)
    {
        if (node.choiceCount <= 0 || choicesPool.Length == 0)
            return ReadOnlySpan<Choice>.Empty;
        return new ReadOnlySpan<Choice>(choicesPool, node.choiceOffset, node.choiceCount);
    }

    public bool TryGetNodeIdByIndexKey(string indexKey, out int nodeId)
    {
        nodeId = -1;
        if (string.IsNullOrEmpty(indexKey))
            return false;

        if (_indexKeyLookup.TryGetValue(indexKey, out nodeId))
            return true;

        return false;
    }

    static HeaderInfo AnalyzeHeader(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new Exception("CSV header missing");

        string trimmed = headerLine.Trim();
        bool hasLoopKnowledge = trimmed.IndexOf(",timeLoopKnowledge", StringComparison.OrdinalIgnoreCase) >= 0;

        int totalColumns = CountCsvColumns(trimmed);
        int baseColumns = hasLoopKnowledge ? totalColumns - 1 : totalColumns;

        HeaderInfo info = default;
        if (baseColumns == 18)
        {
            info.hasVoice = true;
        }
        else if (baseColumns == 17)
        {
            info.hasVoice = false;
        }
        else
        {
            string normalized = trimmed;
            if (hasLoopKnowledge)
            {
                int idx = normalized.LastIndexOf(",timeLoopKnowledge", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    normalized = normalized.Substring(0, idx);
                normalized = normalized.TrimEnd(',');
            }

            if (string.Equals(normalized, CsvHeaderLegacy, StringComparison.OrdinalIgnoreCase))
            {
                info.hasVoice = true;
            }
            else if (string.Equals(normalized, CsvHeaderNew, StringComparison.OrdinalIgnoreCase))
            {
                info.hasVoice = false;
            }
            else
            {
                throw new Exception($"Unexpected CSV header format: {headerLine}");
            }
        }

        info.hasLoopKnowledge = hasLoopKnowledge;
        return info;
    }

    static void ParseCsvLine(string line, HeaderInfo header, out CsvFields f)
    {
        int baseColumnCount = header.hasVoice ? 18 : 17;
        int totalColumns = baseColumnCount + (header.hasLoopKnowledge ? 1 : 0);
        string[] slots = new string[totalColumns];
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
                    if (si < slots.Length) slots[si] = sb.ToString();
                    si++;
                    sb.Length = 0;
                    if (si >= slots.Length) break;
                }
                else if (c == '"') inQuote = true;
                else sb.Append(c);
            }
        }
        if (si < slots.Length) slots[si] = sb.ToString();

        f = new CsvFields
        {
            indexKey = SlotOrDefault(slots, 0),
            nodeIdText = SlotOrDefault(slots, 1),
            rowType = SlotOrDefault(slots, 2),
            speaker = SlotOrDefault(slots, 3),
            text = SlotOrDefault(slots, 4),
            voice = header.hasVoice ? SlotOrDefault(slots, 5) : null,
            actors = SlotOrDefault(slots, header.hasVoice ? 6 : 5),
            bgm = SlotOrDefault(slots, header.hasVoice ? 7 : 6),
            sfx = SlotOrDefault(slots, header.hasVoice ? 8 : 7),
            cg = SlotOrDefault(slots, header.hasVoice ? 9 : 8),
            transition = SlotOrDefault(slots, header.hasVoice ? 10 : 9),
            advancePolicy = SlotOrDefault(slots, header.hasVoice ? 11 : 10),
            nextNodeIdText = SlotOrDefault(slots, header.hasVoice ? 12 : 11),
            choiceLabel = SlotOrDefault(slots, header.hasVoice ? 13 : 12),
            choiceGoto = SlotOrDefault(slots, header.hasVoice ? 14 : 13),
            choiceSet = SlotOrDefault(slots, header.hasVoice ? 15 : 14),
            flagsSet = SlotOrDefault(slots, header.hasVoice ? 16 : 15),
            flagsReq = SlotOrDefault(slots, header.hasVoice ? 17 : 16),
            timeLoopKnowledge = header.hasLoopKnowledge ? SlotOrDefault(slots, baseColumnCount) : null,
        };
    }

    static int CountCsvColumns(string line)
    {
        if (string.IsNullOrEmpty(line)) return 0;

        int count = 1;
        bool inQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"') i++;
                else inQuote = !inQuote;
            }
            else if (c == ',' && !inQuote)
            {
                count++;
            }
        }

        return count;
    }

    static string SlotOrDefault(string[] slots, int index)
        => (index >= 0 && index < slots.Length) ? slots[index] : null;

    struct CsvFields
    {
        public string indexKey;
        public string nodeIdText;
        public string rowType;
        public string speaker;
        public string text;
        public string voice;
        public string actors;
        public string bgm;
        public string sfx;
        public string cg;
        public string transition;
        public string advancePolicy;
        public string nextNodeIdText;
        public string choiceLabel;
        public string choiceGoto;
        public string choiceSet;
        public string flagsSet;
        public string flagsReq;
        public string timeLoopKnowledge;
    }

    static int ParseParentId(string nodeIdText)
    {
        if (string.IsNullOrWhiteSpace(nodeIdText)) return -1;
        int dash = nodeIdText.IndexOf('-');
        string parent = dash >= 0 ? nodeIdText.Substring(0, dash) : nodeIdText;
        return SafeAtoi(parent);
    }

    static int SafeAtoi(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return -1;
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

    static int ParseFlagsField(string field, List<int> pool)
    {
        if (string.IsNullOrWhiteSpace(field)) return 0;
        field = field.Replace(" ", string.Empty);
        int start = 0, added = 0;
        while (start < field.Length)
        {
            int amp = field.IndexOf('&', start);
            string tok = (amp < 0) ? field.Substring(start) : field.Substring(start, amp - start);
            if (tok.Length > 0)
            {
                pool.Add(SafeAtoi(tok));
                added++;
            }
            if (amp < 0) break;
            start = amp + 1;
        }
        return added;
    }
}
