using UnityEngine;

public sealed class ChatLogManager : MonoBehaviour
{
    public static ChatLogManager Instance { get; private set; }

    [System.Serializable]
    public struct LogEntry
    {
        public int nodeId;
        public string speaker;
        public string bodyRich; // ��ũ/�� ���� ��ġ �ؽ�Ʈ
    }

    [Header("Capacity")]
    public int capacity = 256;          // ���� ���� ũ��
    public int defaultExportCount = 50; // ��� �⺻ ǥ�� ���� ��

    LogEntry[] buf;
    int head;   // ���� ���� ��ġ
    int count;  // ���� ��(<= capacity)

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
        // �� �ı��� �� �̱��� ����
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

    /// <summary>�ֱ� n���� outBuf�� �տ�������(������ �� �ֽ�) ä�� ��ȯ. ���� ä�� �� ����.</summary>
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
