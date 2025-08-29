using UnityEngine;

public static class UiModalGate
{
    static int depth;

    public static bool IsOpen => depth > 0;

    public static void Push()
    {
        // ����ȣ: ������ ��ø ��� ����
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
