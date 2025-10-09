using UnityEngine;

public sealed class ChatLogManager : MonoBehaviour
{
    public static ChatLogManager Instance { get; private set; }

    [System.Serializable]
    public struct LogEntry
    {
        public int nodeId;
        public string speaker;
        public string bodyRich; // 링크/색 포함 리치 텍스트
    }

    [Header("Capacity")]
    public int capacity = 256;          // 원형 버퍼 크기
    public int defaultExportCount = 50; // 뷰어 기본 표시 라인 수

    LogEntry[] buf;
    int head;   // 다음 쓰기 위치
    int count;  // 누적 수(<= capacity)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (capacity < 32) capacity = 32;
        buf = new LogEntry[capacity];
        head = 0; count = 0;
    }
    void OnDestroy()
    {
        // ★ 파괴될 때 싱글턴 해제
        if (Instance == this) Instance = null;
    }

    public void Clear() { head = 0; count = 0; }

    public void Push(int nodeId, string speaker, string bodyRich)
    {
        int i = head;
        buf[i].nodeId = nodeId;
        buf[i].speaker = speaker ?? string.Empty;
        buf[i].bodyRich = bodyRich ?? string.Empty;

        head = (head + 1) % capacity;
        if (count < capacity) count++;
    }

    /// <summary>최근 n개를 outBuf에 앞에서부터(오래된 → 최신) 채워 반환. 실제 채운 수 리턴.</summary>
    public int CopyLatest(LogEntry[] outBuf, int n)
    {
        if (outBuf == null || outBuf.Length == 0 || n <= 0) return 0;
        n = Mathf.Min(n, count, outBuf.Length);

        int start = (head - n + capacity) % capacity;
        for (int k = 0; k < n; k++)
        {
            int src = (start + k) % capacity;
            outBuf[k] = buf[src];
        }
        return n;
    }

    public int Count => count;
}
