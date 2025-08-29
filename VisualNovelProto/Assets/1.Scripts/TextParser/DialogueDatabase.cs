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

    // (참고용) transition 포함 헤더
    public const string CsvHeaderNew =
        "Index,nodeId,rowType,speaker,text,actors,bgm,sfx,cg,transition,advancePolicy,nextNodeId,choiceLabel,choiceGoto,choiceSet,flagsSet,flagsReq";

    public static DialogueDatabase LoadFromResources(string path = "Story/main")
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

            int lineNo = 1;
            while ((line = sr.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseCsvLine(line, out CsvFields f);

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

    // ========= CSV 파싱 =========

    struct CsvFields
    {
        public string indexKey, nodeIdText, rowType;
        public string speaker, text, actors, bgm, sfx, cg, transition, advancePolicy, nextNodeIdText;
        public string choiceLabel, choiceGoto, choiceSet;
        public string flagsSet, flagsReq;
        public string choices; // (구방식 호환용, 현재는 미사용)
    }

    static void ParseCsvLine(string line, out CsvFields f)
    {
        // 16칸(transition 포함)
        string[] slots = new string[17];
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
                if (c == ',') { slots[si++] = sb.ToString(); sb.Length = 0; if (si >= slots.Length) break; }
                else if (c == '"') inQuote = true;
                else sb.Append(c);
            }
        }
        if (si < slots.Length) slots[si] = sb.ToString();

        f = new CsvFields
        {
            indexKey = slots[0],
            nodeIdText = slots[1],
            rowType = slots[2],
            speaker = slots[3],
            text = slots[4],
            actors = slots[5],
            bgm = slots[6],
            sfx = slots[7],
            cg = slots[8],
            transition = slots[9],
            advancePolicy = slots[10],
            nextNodeIdText = slots[11],
            choiceLabel = slots[12],
            choiceGoto = slots[13],
            choiceSet = slots[14],
            flagsSet = slots[15],
            flagsReq = slots[16],
            choices = null
        };
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
