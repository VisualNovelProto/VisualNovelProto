using System;
using System.Collections.Generic;

// UiModalGate.cs (기존 클래스 교체/확장)
public static class UiModalGate
{
    static readonly Stack<Action> _closers = new Stack<Action>(8);

    public static event Action<bool> StateChanged;

    public static bool IsOpen => _closers.Count > 0;

    public static void Reset()
    {
        bool wasOpen = IsOpen;
        _closers.Clear();
        if (wasOpen)
            NotifyStateChanged();
    }

    /// <summary>모달이 열릴 때 반드시 Close 콜백을 함께 등록.</summary>
    public static void Push(Action onCancelClose)
    {
        bool wasOpen = IsOpen;
        _closers.Push(onCancelClose); // null도 허용(비상용)
        if (!wasOpen)
            NotifyStateChanged();
    }

    /// <summary>모달이 스스로 닫힐 때 호출(보통 Close() 내부에서 호출)</summary>
    public static void Pop()
    {
        if (_closers.Count == 0) return;

        _closers.Pop();
        if (_closers.Count == 0)
            NotifyStateChanged();
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

    static void NotifyStateChanged()
    {
        StateChanged?.Invoke(_closers.Count > 0);
    }
}
