// UiModalGate.cs (기존 클래스 교체/확장)
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

    /// <summary>모달이 열릴 때 반드시 Close 콜백을 함께 등록.</summary>
    public static void Push(System.Action onCancelClose)
    {
        _closers.Push(onCancelClose); // null도 허용(비상용)
    }

    /// <summary>모달이 스스로 닫힐 때 호출(보통 Close() 내부에서 호출)</summary>
    public static void Pop()
    {
        if (_closers.Count > 0) _closers.Pop();
    }

    /// <summary>맨 위 모달을 닫으려고 시도. 닫았으면 true.</summary>
    public static bool TryCloseTop()
    {
        if (_closers.Count == 0) return false;
        var top = _closers.Peek();      // ★중요: Peek만 하고…
        if (top != null) top.Invoke();  // …Close()가 내부에서 Pop()을 호출
        else Pop();                     // 콜백이 없다면 게이트만 내림(비상용)
        return true;
    }
}
