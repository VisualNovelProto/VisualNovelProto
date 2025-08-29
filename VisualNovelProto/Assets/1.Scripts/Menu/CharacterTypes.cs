using System;
using UnityEngine;

[Serializable]
public struct CharacterEntry
{
    public int id;
    public string name;
    public string colorHex;   // ★ 이름/링크 색상
    public string desc;
    public string thumb;
    public string group;

    // 1인 1능력
    public string abilityName;
    public string abilityDesc;

    // 1인 1페널티
    public string penaltyName;
    public string penaltyDesc;
}

[Serializable]
public struct CharacterSet
{
    const int WordBits = 64;
    const int WordCount = 64; // 4096개까지

    public ulong w0, w1, w2, w3, w4, w5, w6, w7,
                 w8, w9, w10, w11, w12, w13, w14, w15,
                 w16, w17, w18, w19, w20, w21, w22, w23,
                 w24, w25, w26, w27, w28, w29, w30, w31,
                 w32, w33, w34, w35, w36, w37, w38, w39,
                 w40, w41, w42, w43, w44, w45, w46, w47,
                 w48, w49, w50, w51, w52, w53, w54, w55,
                 w56, w57, w58, w59, w60, w61, w62, w63;

    static ref ulong Pick(ref CharacterSet s, int idx)
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
            case 16: return ref s.w16;
            case 17: return ref s.w17;
            case 18: return ref s.w18;
            case 19: return ref s.w19;
            case 20: return ref s.w20;
            case 21: return ref s.w21;
            case 22: return ref s.w22;
            case 23: return ref s.w23;
            case 24: return ref s.w24;
            case 25: return ref s.w25;
            case 26: return ref s.w26;
            case 27: return ref s.w27;
            case 28: return ref s.w28;
            case 29: return ref s.w29;
            case 30: return ref s.w30;
            case 31: return ref s.w31;
            case 32: return ref s.w32;
            case 33: return ref s.w33;
            case 34: return ref s.w34;
            case 35: return ref s.w35;
            case 36: return ref s.w36;
            case 37: return ref s.w37;
            case 38: return ref s.w38;
            case 39: return ref s.w39;
            case 40: return ref s.w40;
            case 41: return ref s.w41;
            case 42: return ref s.w42;
            case 43: return ref s.w43;
            case 44: return ref s.w44;
            case 45: return ref s.w45;
            case 46: return ref s.w46;
            case 47: return ref s.w47;
            case 48: return ref s.w48;
            case 49: return ref s.w49;
            case 50: return ref s.w50;
            case 51: return ref s.w51;
            case 52: return ref s.w52;
            case 53: return ref s.w53;
            case 54: return ref s.w54;
            case 55: return ref s.w55;
            case 56: return ref s.w56;
            case 57: return ref s.w57;
            case 58: return ref s.w58;
            case 59: return ref s.w59;
            case 60: return ref s.w60;
            case 61: return ref s.w61;
            case 62: return ref s.w62;
            case 63: return ref s.w63;
            default: throw new IndexOutOfRangeException();
        }
    }

    public void Clear() { this = default; }

    public void Set(int id)
    {
        int wi = id / WordBits; if ((uint)wi >= WordCount) return;
        int bi = id % WordBits;
        ref ulong w = ref Pick(ref this, wi);
        w |= (1UL << bi);
    }

    public bool Has(int id)
    {
        int wi = id / WordBits; if ((uint)wi >= WordCount) return false;
        int bi = id % WordBits;
        ulong w = Pick(ref this, wi);
        return (w & (1UL << bi)) != 0;
    }
}
