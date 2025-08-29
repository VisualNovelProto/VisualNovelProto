using UnityEngine;

public static class UiModalGate
{
    static int depth;

    public static bool IsOpen => depth > 0;

    public static void Push()
    {
        // 과보호: 비정상 중첩 대비 상한
        if (depth < 100000) depth++;
    }

    public static void Pop()
    {
        if (depth > 0) depth--;
    }

    public static void Reset()
    {
        depth = 0;
    }
}
