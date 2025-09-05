using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DDOL(����) ���̺�/�ε� + ���似�̺� �Ŵ���
/// - Manual ���� ����/�ε�
/// - Autosave(�ֱ�/é�� ����)
/// - ���� ��Ÿ(Ÿ�ӽ�����, �÷���Ÿ��, ��/é��/���) ��ȸ
/// - �� ������ �� DialogueRunner/DialogueUI ����ε�
/// </summary>
public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Bindings (�� ����)")]
    public DialogueRunner runner; // �� �ε帶�� ���� ����
    public DialogueUI ui;         // glossary/characters ���ٿ�(�� ����)

    [Header("File Settings")]
    public string manualPrefix = "save_";
    public string autosavePrefix = "auto_";
    public int maxAutosaves = 5;

    [Header("Autosave")]
    public bool enableIntervalAutosave = true;
    public float autosaveIntervalSec = 180f; // 3��
    public bool enableChapterEndAutosave = true;

    [Header("Scan Limits")]
    public int maxFlagsToScan = 2048; // �� ��Ģ: �˳���, Ǯ ����

    [Header("Time Source")]
    public bool useUnscaledTime = true;
    [Header("Log Snapshot")]
    public int logTailCount = 40; // ���̺꿡 �Բ� ������ ��α� �� �� (���� 30~50)

    ChatLogManager.LogEntry[] _tmpLogBuf; // �����Ҵ� �ּ�ȭ�� �ӽ� ����

    // --- ���� ���� (���� ���� �ּ�ȭ) ---
    readonly List<int> tmpFlags = new List<int>(4096);
    readonly List<int> tmpGlossary = new List<int>(1024);
    readonly List<int> tmpCharacters = new List<int>(1024);

    // --- ���� ���� ---
    float _timer;                      // ���似�̺� Ÿ�̸�
    int _lastSavedNodeId = -1;       // ���� ��� �ݺ� ���� ����
    int _lastObservedNodeId = -1;    // ���� ����
    float _playtimeSec;                // ���� �÷���Ÿ��(���� ��)
    DateTime _sessionStart;

    [System.Serializable]
    public struct LogLine
    {
        public int nodeId;          // ����(��� �ٲ�� �������)
        public string speaker;      // ȭ�鿡 ���� ���� ���ڿ�(��ũ/���� ���� OK)
        public string body;         // ȭ�鿡 ���� ���� ���ڿ�
    }


    // --- ���� ������ ���� ---
    [Serializable]
    public struct SaveData
    {
        public int version;               // ���� ���� (����)
        public string timestamp;          // "yyyy-MM-dd HH:mm:ss"
        public string sceneName;          // �� �̸�
        public int nodeId;                // ���� ����
        public int chapterId;             // (������ -1) é�� �ĺ���
        public float playtimeSec;         // ���� �÷���Ÿ��(����)
        public string saveType;           // "Manual" / "Auto" / "ChapterEnd"

        public int[] flags;               // ���� �÷��� id
        public int[] glossary;            // ���� �۷μ��� id
        public int[] characters;          // ���� ĳ���� id
        public LogLine[] logTail;
    }

    // === �����ֱ� ===
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        _sessionStart = DateTime.UtcNow;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // �� ��꿡�� ���� ���۷��� ����
        var hub = FindObjectOfType<SceneRefHub>();
        if (hub != null)
        {
            runner = hub.dialogueRunner;
            ui = hub.dialogueUI;
        }

        // �� �ٲ�� ���似�̺� Ÿ�̸�/���� ���� �ʱ�ȭ
        _timer = 0f;
        _lastObservedNodeId = SafeGetNodeId();
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _playtimeSec += dt;

        // ���� ����
        int nid = SafeGetNodeId();
        if (nid >= 0 && nid != _lastObservedNodeId)
        {
            _lastObservedNodeId = nid;
            _timer = 0f; // ������ ������ Ÿ�̸� ����(ª�� �ð� �� �ߺ� ���� ����)
        }

        if (!enableIntervalAutosave) return;

        // ���� �ð� ���� �� ���� �� ���似�̺�
        _timer += dt;
        if (_timer >= autosaveIntervalSec)
        {
            _timer = 0f;
            TryAutosave("Interval");
        }
    }

    // === �ܺο��� ȣ���� API ===

    /// <summary>�޴�/��ư���� ȣ��: ���� ����</summary>
    public bool SaveManual(int slot)
    {
        return SaveInternal(slot, isAuto: false, tag: "Manual");
    }

    /// <summary>���似�̺�(é�� ���� �� ��). �����ص� ���� ������ ���.</summary>
    public void SaveAutosave(string tag = "Auto")
    {
        TryAutosave(tag);
    }

    /// <summary>é�� ���� �� ȣ��(���丮 �Ŵ���/���� �̺�Ʈ����)</summary>
    public void NotifyChapterEnd(int chapterId = -1)
    {
        if (!enableChapterEndAutosave) return;
        TryAutosave("ChapterEnd", chapterId);
    }

    public bool LoadManual(int slot, bool jumpToNode = true, bool clearBeforeApply = true)
    {
        string path = PathOf(manualPrefix, slot);
        return LoadFromPath(path, jumpToNode, clearBeforeApply);
    }
    public bool LoadAutosaveSlot(int slot, bool jumpToNode = true, bool clearBeforeApply = true)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{autosavePrefix}{slot}.json");
        return LoadFromPath(path, jumpToNode, clearBeforeApply);
    }

    public bool LoadAutosaveLatest(bool jumpToNode = true, bool clearBeforeApply = true)
    {
        // �ֽ� ���似�̺� ����(�����ð� ����) ã�� �ε�
        var files = Directory.GetFiles(Application.persistentDataPath, autosavePrefix + "*.json");
        string best = null; DateTime bestTime = DateTime.MinValue;
        for (int i = 0; i < files.Length; i++)
        {
            var t = File.GetLastWriteTimeUtc(files[i]);
            if (t > bestTime) { bestTime = t; best = files[i]; }
        }
        if (string.IsNullOrEmpty(best)) return false;
        return LoadFromPath(best, jumpToNode, clearBeforeApply);
    }

    /// <summary>���� ��Ÿ ���� �ܼ� ��ȸ(UI ��Ͽ�)</summary>
    public bool TryGetSlotInfo(bool manual, int slot, out SaveData meta)
    {
        string path = PathOf(manual ? manualPrefix : autosavePrefix, slot);
        if (!File.Exists(path)) { meta = default; return false; }
        try
        {
            var json = File.ReadAllText(path);
            meta = JsonUtility.FromJson<SaveData>(json);
            return true;
        }
        catch { meta = default; return false; }
    }

    // === ���� ���� ===

    string PathOf(string prefix, int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"{prefix}{slot}.json");
    }

    bool SaveInternal(int slot, bool isAuto, string tag, int chapterOverride = -1)
    {
        if (runner == null || ui == null)
        {
            Debug.LogWarning("Save: runner/ui not set.");
            return false;
        }
        if (ui.glossary == null || ui.characters == null)
        {
            Debug.LogWarning("Save: DB not bound.");
            return false;
        }

        tmpFlags.Clear(); tmpGlossary.Clear(); tmpCharacters.Clear();

        // 1) ���� ���
        int nodeId = SafeGetNodeId();
        if (nodeId < 0) nodeId = -1;

        // ���� ��� �ݺ� ���� ���� (���似�̺� ���� ����)
        if (isAuto && nodeId >= 0 && nodeId == _lastSavedNodeId)
        {
            // ������ ������ ���� �н�
            return false;
        }

        // 2) �÷��� ����
        for (int i = 0; i < maxFlagsToScan; i++)
            if (runner.HasFlag(i)) tmpFlags.Add(i);

        // 3) �۷μ���/ĳ����
        var gdb = ui.glossary;
        if (gdb != null && gdb.present != null)
        {
            for (int i = 0; i < gdb.present.Length; i++)
                if (gdb.present[i] && gdb.owned.Has(i)) tmpGlossary.Add(i);
        }
        var cdb = ui.characters;
        if (cdb != null && cdb.present != null)
        {
            for (int i = 0; i < cdb.present.Length; i++)
                if (cdb.present[i] && cdb.owned.Has(i)) tmpCharacters.Add(i);
        }

        // 4) ��Ÿ
        var data = new SaveData
        {
            version = 2,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneName = SceneManager.GetActiveScene().name,
            nodeId = nodeId,
            chapterId = chapterOverride >= 0 ? chapterOverride : SafeGetChapterId(),
            playtimeSec = _playtimeSec,
            saveType = tag ?? (isAuto ? "Auto" : "Manual"),
            flags = tmpFlags.ToArray(),
            glossary = tmpGlossary.ToArray(),
            characters = tmpCharacters.ToArray()
        };

        // 4-1) ��α� ������ ����
        var lm = ChatLogManager.Instance;
        if (lm != null && logTailCount > 0)
        {
            int want = Mathf.Min(logTailCount, lm.Count);
            if (want > 0)
            {
                // �ӽ� ���� ũ�� Ȯ��
                if (_tmpLogBuf == null || _tmpLogBuf.Length < want)
                    _tmpLogBuf = new ChatLogManager.LogEntry[want];

                int got = lm.CopyLatest(_tmpLogBuf, want); // �����ȡ��ֽ� ������ ä����
                if (got > 0)
                {
                    data.logTail = new LogLine[got];
                    for (int i = 0; i < got; i++)
                    {
                        data.logTail[i] = new LogLine
                        {
                            nodeId = _tmpLogBuf[i].nodeId,
                            speaker = _tmpLogBuf[i].speaker,
                            body = _tmpLogBuf[i].bodyRich
                        };
                    }
                }
            }
        }

        // 5) ���� ���
        try
        {
            string path = PathOf(isAuto ? autosavePrefix : manualPrefix, slot);
            var json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(path, json);

            _lastSavedNodeId = nodeId;
            // ���似�̺�� ȸ��(�����̼�) ����
            if (isAuto) RotateAutosaves();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
            return false;
        }
    }

    void TryAutosave(string tag, int chapterOverride = -1)
    {
        // ���� ȸ��: auto_0 ~ auto_{maxAutosaves-1}
        // ���� ������ ������ ����� ���
        int slot = FindOldestAutosaveSlot();
        SaveInternal(slot, isAuto: true, tag: tag, chapterOverride: chapterOverride);
    }

    int FindOldestAutosaveSlot()
    {
        DateTime oldest = DateTime.MaxValue; int oldestIdx = 0;
        for (int i = 0; i < Mathf.Max(1, maxAutosaves); i++)
        {
            string path = PathOf(autosavePrefix, i);
            var t = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (t < oldest) { oldest = t; oldestIdx = i; }
        }
        return oldestIdx;
    }

    void RotateAutosaves()
    {
        // ���⼱ ���� ����/�̵� ���� FindOldestAutosaveSlot�� ȸ�� ����
        // (�ʿ��ϸ� �뷮 ����/�ִ� ���� �ϼ� �߰�)
    }

    bool LoadFromPath(string path, bool jumpToNode, bool clearBeforeApply)
    {
        if (runner == null || ui == null) { Debug.LogWarning("Load: runner/ui not set."); return false; }
        if (!File.Exists(path)) { Debug.LogWarning($"Load: file not found: {path}"); return false; }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);

            // 1) �ʱ�ȭ
            if (clearBeforeApply)
            {
                runner.ClearAllFlags();
                if (ui.glossary != null) ui.glossary.owned.Clear();
                if (ui.characters != null) ui.characters.owned.Clear();
            }

            // 2) ����
            if (data.flags != null)
                for (int i = 0; i < data.flags.Length; i++) runner.SetFlag(data.flags[i]);

            if (data.glossary != null && ui.glossary != null)
                for (int i = 0; i < data.glossary.Length; i++) ui.glossary.owned.Set(data.glossary[i]);

            if (data.characters != null && ui.characters != null)
                for (int i = 0; i < data.characters.Length; i++) ui.characters.owned.Set(data.characters[i]);

            _playtimeSec = data.playtimeSec > 0 ? data.playtimeSec : _playtimeSec;
            // 2-1) ��α� ���� (���� ���� ó��)
            if (clearBeforeApply)
            {
                ChatLogManager.Instance?.Clear();
            }
            if (data.logTail != null && data.logTail.Length > 0)
            {
                // ������ ��尡 �������� '������ ��'�� ���ٸ� ������ ���� ����(���� ���� �ߺ� ����)
                int tailLen = data.logTail.Length;
                int end = tailLen;
                if (jumpToNode && data.nodeId >= 0 && tailLen > 0)
                {
                    var last = data.logTail[tailLen - 1];
                    if (last.nodeId == data.nodeId) end = tailLen - 1;
                }

                var lm = ChatLogManager.Instance;
                if (lm != null)
                {
                    for (int i = 0; i < end; i++)
                    {
                        var l = data.logTail[i];
                        lm.Push(l.nodeId, l.speaker, l.body);
                    }
                }
            }
            // 3) ����
            if (jumpToNode && data.nodeId >= 0)
            {
                runner.JumpToNode(data.nodeId);
                _lastObservedNodeId = data.nodeId;
                _lastSavedNodeId = data.nodeId;
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e.Message}");
            return false;
        }
    }

    // === ���� ����� ===
    int SafeGetNodeId() => runner != null ? runner.GetCurrentNodeId() : -1;

    // é�� ID �����ڰ� ������ -1 (�ʿ� �� StoryGameManager ��� ����/���ε�)
    int SafeGetChapterId()
    {
        // 1) ���ʰ� é�� ���� �������̽��� �����ߴٸ� ���
        // 2) �ƴϸ� StoryGameManager ��� ���Ե� ���� é�� ���� �����ϵ��� Ȯ�� ����
        // ���⼭�� �⺻ -1
        return -1;
    }
}
