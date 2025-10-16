using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class DialogueDatabase : ScriptableObject
{
    public const int MaxNodes = 100_000;
    public const int MaxChoices = 300_000;
    public const int MaxFlagRefs = 1_000_000;

    [NonSerialized] public DialogueNode[] nodes = new DialogueNode[MaxNodes];
    [NonSerialized] public Choice[] choicesPool = new Choice[MaxChoices];
    [NonSerialized] public int[] flagsPool = new int[MaxFlagRefs];

    [NonSerialized] public int nodeCount;
    [NonSerialized] public int choiceCount;
    [NonSerialized] public int flagRefCount;

    [NonSerialized] public int[] nodeIndexById = CreateIndex();
    static int[] CreateIndex() { var a = new int[MaxNodes]; for (int i = 0; i < a.Length; i++) a[i] = -1; return a; }

    public const string CsvHeaderLegacy =
        "Index,nodeId,rowType,speaker,text,voice,actors,bgm,sfx,cg,transition,advancePolicy,nextNodeId,choiceLabel,choiceGoto,choiceSet,flagsSet,flagsReq";
    public const string CsvHeaderNew =
        "Index,nodeId,rowType,speaker,text,actors,bgm,sfx,cg,transition,advancePolicy,nextNodeId,choiceLabel,choiceGoto,choiceSet,flagsSet,flagsReq";

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
        ResetPools();
        using (StringReader sr = new StringReader(csvText))
        {
            string line = sr.ReadLine(); // header
            if (line == null) throw new Exception("CSV empty");
            bool headerHasVoice = HeaderHasVoiceColumn(line);

            int lineNo = 1;
            while ((line = sr.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseCsvLine(line, headerHasVoice, out CsvFields f);

                if (string.Equals(f.rowType, "Choice", StringComparison.OrdinalIgnoreCase))
                {
                    int parentId = ParseParentId(f.nodeIdText);
                    int gotoId = SafeAtoi(f.choiceGoto);
                    if (string.IsNullOrWhiteSpace(f.choiceLabel)) continue;

                    int parentIdx = nodeIdToIndex(parentId);
                    if (parentIdx < 0) parentIdx = EnsureNodeShell(parentId);

                    AddChoice(parentIdx, f.choiceLabel.Trim(), gotoId, f.choiceSet);
                    continue;
                }

                // Node
                int nid = SafeAtoi(f.nodeIdText);
                if (nid < 0 || nid >= MaxNodes)
                    throw new Exception($"nodeId out of range at line {lineNo}: {f.nodeIdText}");

                if (nodeIndexById[nid] != -1)
                {
                    int idx = nodeIndexById[nid];
                    ref DialogueNode nodeX = ref nodes[idx];
                    FillNode(ref nodeX, f);
                }
                else
                {
                    if (nodeCount >= MaxNodes) throw new Exception("MaxNodes exceeded");
                    ref DialogueNode node = ref nodes[nodeCount];
                    node = default;
                    node.nodeId = nid;
                    node.indexKey = f.indexKey;
                    FillNode(ref node, f);

                    nodeIndexById[nid] = nodeCount;
                    nodeCount++;
                }
            }
        }
    }

    void ResetPools()
    {
        nodeCount = choiceCount = flagRefCount = 0;
        for (int i = 0; i < nodeIndexById.Length; i++) nodeIndexById[i] = -1;
    }

    int nodeIdToIndex(int nodeId) => (nodeId >= 0 && nodeId < nodeIndexById.Length) ? nodeIndexById[nodeId] : -1;

    int EnsureNodeShell(int nodeId)
    {
        int idx = nodeIdToIndex(nodeId);
        if (idx >= 0) return idx;
        if (nodeCount >= MaxNodes) throw new Exception("MaxNodes exceeded");
        ref DialogueNode node = ref nodes[nodeCount];
        node = default;
        node.nodeId = nodeId;
        node.indexKey = string.Empty;
        node.nextNodeId = -1;
        nodeIndexById[nodeId] = nodeCount;
        return nodeCount++;
    }

    void FillNode(ref DialogueNode node, CsvFields f)
    {
        node.indexKey = string.IsNullOrEmpty(f.indexKey) ? node.indexKey : f.indexKey;
        node.rowType = f.rowType;
        node.speaker = f.speaker;
        node.text = f.text;
        node.voice = f.voice;

        string spec = f.actors?.Trim();
        if (!string.IsNullOrEmpty(spec))
        {
            // 구형(단일 키만) 자동 확장
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
        node.transition = f.transition; // transition 매핑
        node.advancePolicy = f.advancePolicy;
        node.nextNodeId = SafeAtoi(f.nextNodeIdText);

        node.flagsSetOffset = flagRefCount;
        node.flagsSetCount = ParseFlagsField(f.flagsSet, flagsPool, ref flagRefCount);

        node.flagsReqOffset = flagRefCount;
        node.flagsReqCount = ParseFlagsField(f.flagsReq, flagsPool, ref flagRefCount);
    }

    void AddChoice(int parentIndex, string label, int gotoId, string choiceSetField)
    {
        if (choiceCount >= MaxChoices) throw new Exception("MaxChoices exceeded");
        ref DialogueNode parent = ref nodes[parentIndex];

        ref Choice ch = ref choicesPool[choiceCount];
        ch.label = label;
        ch.gotoNodeId = gotoId;
        ch.setOffset = flagRefCount;
        ch.setCount = ParseFlagsField(choiceSetField, flagsPool, ref flagRefCount);

        if (parent.choiceCount == 0) parent.choiceOffset = choiceCount;
        parent.choiceCount++;
        choiceCount++;
    }

    // ========= 외부에 노출되는 조회 API =========

    public bool TryGetNodeById(int nodeId, out DialogueNode node, out int index)
    {
        index = -1;
        node = default;
        if ((uint)nodeId >= MaxNodes) return false;

        int idx = nodeIndexById[nodeId];
        if (idx < 0 || idx >= nodeCount) return false;

        index = idx;
        node = nodes[idx];
        return true;
    }

    public ReadOnlySpan<Choice> GetChoicesOf(ref DialogueNode node)
    {
        if (node.choiceCount <= 0) return ReadOnlySpan<Choice>.Empty;
        return new ReadOnlySpan<Choice>(choicesPool, node.choiceOffset, node.choiceCount);
    }

    public bool TryGetNodeIdByIndexKey(string indexKey, out int nodeId)
    {
        nodeId = -1;
        if (string.IsNullOrEmpty(indexKey))
            return false;

        for (int i = 0; i < nodeCount; i++)
        {
            ref DialogueNode node = ref nodes[i];
            if (string.Equals(node.indexKey, indexKey, StringComparison.OrdinalIgnoreCase))
            {
                nodeId = node.nodeId;
                return true;
            }
        }

        return false;
    }

    static bool HeaderHasVoiceColumn(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine)) return true;

        string trimmed = headerLine.Trim();
        if (string.Equals(trimmed, CsvHeaderLegacy, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(trimmed, CsvHeaderNew, StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.IndexOf(",voice", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        int columnCount = CountCsvColumns(trimmed);
        if (columnCount == 18) return true;
        if (columnCount == 17) return false;

        throw new Exception($"Unexpected CSV header format: {headerLine}");
    }

    static void ParseCsvLine(string line, bool headerHasVoice, out CsvFields f)
    {
        string[] slots = new string[headerHasVoice ? 18 : 17];
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
            voice = headerHasVoice ? SlotOrDefault(slots, 5) : null,
            actors = SlotOrDefault(slots, headerHasVoice ? 6 : 5),
            bgm = SlotOrDefault(slots, headerHasVoice ? 7 : 6),
            sfx = SlotOrDefault(slots, headerHasVoice ? 8 : 7),
            cg = SlotOrDefault(slots, headerHasVoice ? 9 : 8),
            transition = SlotOrDefault(slots, headerHasVoice ? 10 : 9),
            advancePolicy = SlotOrDefault(slots, headerHasVoice ? 11 : 10),
            nextNodeIdText = SlotOrDefault(slots, headerHasVoice ? 12 : 11),
            choiceLabel = SlotOrDefault(slots, headerHasVoice ? 13 : 12),
            choiceGoto = SlotOrDefault(slots, headerHasVoice ? 14 : 13),
            choiceSet = SlotOrDefault(slots, headerHasVoice ? 15 : 14),
            flagsSet = SlotOrDefault(slots, headerHasVoice ? 16 : 15),
            flagsReq = SlotOrDefault(slots, headerHasVoice ? 17 : 16),
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

    static int ParseFlagsField(string field, int[] pool, ref int cnt)
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
                if (cnt >= MaxFlagRefs) throw new Exception("MaxFlagRefs exceeded");
                pool[cnt++] = SafeAtoi(tok);
                added++;
            }
            if (amp < 0) break;
            start = amp + 1;
        }
        return added;
    }
}
