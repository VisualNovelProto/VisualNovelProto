// UiModalGate.cs (���� Ŭ���� ��ü/Ȯ��)
using System.Collections.Generic;
using UnityEngine;

public static class UiModalGate
{
    static readonly Stack<System.Action> _closers = new Stack<System.Action>(8);
    public static bool IsOpen => _closers.Count > 0;

    public static void Reset()
    {
        _closers.Clear();
    }

    /// <summary>����� ���� �� �ݵ�� Close �ݹ��� �Բ� ���.</summary>
    public static void Push(System.Action onCancelClose)
    {
        _closers.Push(onCancelClose); // null�� ���(����)
    }

    /// <summary>����� ������ ���� �� ȣ��(���� Close() ���ο��� ȣ��)</summary>
    public static void Pop()
    {
        if (_closers.Count > 0) _closers.Pop();
    }

    /// <summary>�� �� ����� �������� �õ�. �ݾ����� true.</summary>
    public static bool TryCloseTop()
    {
        if (_closers.Count == 0) return false;
        var top = _closers.Peek();      // ���߿�: Peek�� �ϰ�
        if (top != null) top.Invoke();  // ��Close()�� ���ο��� Pop()�� ȣ��
        else Pop();                     // �ݹ��� ���ٸ� ����Ʈ�� ����(����)
        return true;
    }
}
