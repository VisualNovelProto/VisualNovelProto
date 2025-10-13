using System;
using UnityEngine;

/// <summary>
/// Stores dialogue history in a circular buffer so that the UI can build a chat log.
/// </summary>
public sealed class ChatLogManager : MonoBehaviour
{
    public static ChatLogManager Instance { get; private set; }

    [Serializable]
    public struct LogEntry
    {
        public int nodeId;
        public string speaker;
        public string bodyRich;
        public string voiceKey;

        public bool HasVoice => !string.IsNullOrEmpty(voiceKey);
    }

    [Header("Capacity")]
    [Tooltip("Number of entries kept in memory. Oldest entries are discarded first.")]
    [Min(32)]
    public int capacity = 256;
    [Tooltip("Default amount exported when a viewer asks for entries.")]
    public int defaultExportCount = 50;

    LogEntry[] buffer;
    int head;   // Next write position
    int count;  // Number of valid entries currently stored (<= capacity)

    public event Action OnLogUpdated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        capacity = Mathf.Max(32, capacity);
        buffer = new LogEntry[capacity];
        head = 0;
        count = 0;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int Count => count;

    public void Clear()
    {
        head = 0;
        count = 0;
        if (buffer != null)
            Array.Clear(buffer, 0, buffer.Length);
        OnLogUpdated?.Invoke();
    }

    /// <summary>
    /// Adds a new entry to the log.
    /// </summary>
    public void Push(int nodeId, string speaker, string bodyRich, string voiceKey = null)
    {
        if (buffer == null || buffer.Length == 0)
            return;

        int index = head;
        buffer[index].nodeId = nodeId;
        buffer[index].speaker = speaker ?? string.Empty;
        buffer[index].bodyRich = bodyRich ?? string.Empty;
        buffer[index].voiceKey = string.IsNullOrEmpty(voiceKey) ? string.Empty : voiceKey;

        head = (head + 1) % capacity;
        if (count < capacity)
            count++;

        OnLogUpdated?.Invoke();
    }

    /// <summary>
    /// Copies the most recent <paramref name="n"/> entries into <paramref name="outBuf"/>.
    /// Entries are written in chronological order (oldest to newest).
    /// Returns the number of entries copied.
    /// </summary>
    public int CopyLatest(LogEntry[] outBuf, int n)
    {
        if (outBuf == null || outBuf.Length == 0 || n <= 0 || count == 0)
            return 0;

        int copyCount = Mathf.Min(n, count, outBuf.Length);
        int start = (head - copyCount + capacity) % capacity;
        for (int i = 0; i < copyCount; i++)
        {
            int src = (start + i) % capacity;
            outBuf[i] = buffer[src];
        }
        return copyCount;
    }

    /// <summary>
    /// Tries to fetch an entry by index relative to the newest entry (0 = newest).
    /// </summary>
    public bool TryGetFromNewest(int indexFromNewest, out LogEntry entry)
    {
        entry = default;
        if (indexFromNewest < 0 || indexFromNewest >= count || buffer == null || buffer.Length == 0)
            return false;

        int idx = (head - 1 - indexFromNewest + capacity) % capacity;
        entry = buffer[idx];
        return true;
    }
}
