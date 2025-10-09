using System;
using UnityEngine;

[Serializable]
public struct Choice
{
    public string label;
    public int gotoNodeId;

    public int setOffset;
    public int setCount;
}

[Serializable]
public struct DialogueNode
{
    public int nodeId;
    public string indexKey;

    public string rowType;

    public string speaker;
    public string text;
    public string actors;
    public string bgm;
    public string sfx;
    public string cg;
    public string transition;
    public string advancePolicy; //"block" | "fast" | "fastforward" | ""(±âº»)

    public int nextNodeId;

    // flags
    public int flagsSetOffset;
    public int flagsSetCount;
    public int flagsReqOffset;
    public int flagsReqCount;

    // choices
    public int choiceOffset;
    public int choiceCount;

    public bool HasChoices => choiceCount > 0;
}

[Serializable]
public struct FlagSet
{
    public ulong w0, w1, w2, w3, w4, w5, w6, w7,
                 w8, w9, w10, w11, w12, w13, w14, w15;

    static ref ulong Pick(ref FlagSet s, int idx)
    {
        switch (idx)
        {
            case 0: return ref s.w0;
            case 1: return ref s.w1;
            case 2: return ref s.w2;
            case 3: return ref s.w3;
            case 4: return ref s.w4;
            case 5: return ref s.w5;
            case 6: return ref s.w6;
            case 7: return ref s.w7;
            case 8: return ref s.w8;
            case 9: return ref s.w9;
            case 10: return ref s.w10;
            case 11: return ref s.w11;
            case 12: return ref s.w12;
            case 13: return ref s.w13;
            case 14: return ref s.w14;
            case 15: return ref s.w15;
            default: throw new IndexOutOfRangeException();
        }
    }

    public void Clear() { this = default; }

    public void Set(int id)
    {
        int wi = id >> 6; if ((uint)wi >= 16) return;
        int bi = id & 63;
        ref ulong w = ref Pick(ref this, wi);
        w |= (1UL << bi);
    }

    public bool Has(int id)
    {
        int wi = id >> 6; if ((uint)wi >= 16) return false;
        int bi = id & 63;
        ulong w = Pick(ref this, wi);
        return (w & (1UL << bi)) != 0;
    }

    public bool HasAll(int[] pool, int off, int cnt)
    {
        for (int i = 0; i < cnt; i++) if (!Has(pool[off + i])) return false;
        return true;
    }

    public void SetAll(int[] pool, int off, int cnt)
    {
        for (int i = 0; i < cnt; i++) Set(pool[off + i]);
    }
}
