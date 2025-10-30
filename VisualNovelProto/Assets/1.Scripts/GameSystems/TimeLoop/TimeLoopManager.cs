using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates the time loop system by mapping Story.csv index keys to playable destinations.
/// </summary>
public sealed class TimeLoopManager : MonoBehaviour
{
    [Serializable]
    class KnowledgeSaveBox
    {
        public string[] values;
    }

    struct KnowledgeDefinition
    {
        public string key;
        public string displayName;
        public string description;

        public KnowledgeDefinition(string key, string displayName, string description)
        {
            this.key = key;
            this.displayName = displayName ?? string.Empty;
            this.description = description ?? string.Empty;
        }
    }

    [Serializable]
    public struct Destination
    {
        public string indexKey;
        public string displayLabel;
        public string branchLabel;
        public string detailLabel;
        public string rawIndexValue;
        readonly DateTime? _timestamp;

        const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm";

        public Destination(string indexKey, string displayLabel, string branchLabel, string detailLabel, string rawIndexValue, DateTime? timestamp)
        {
            this.indexKey = indexKey ?? string.Empty;
            this.displayLabel = displayLabel ?? string.Empty;
            this.branchLabel = branchLabel ?? string.Empty;
            this.detailLabel = detailLabel ?? string.Empty;
            this.rawIndexValue = rawIndexValue ?? string.Empty;
            _timestamp = timestamp;
        }

        public DateTime? Timestamp => _timestamp;

        public bool TryGetTimestamp(out DateTime timestamp)
        {
            if (_timestamp.HasValue)
            {
                timestamp = DateTime.SpecifyKind(_timestamp.Value, DateTimeKind.Unspecified);
                return true;
            }

            timestamp = default;
            return false;
        }

        public string GetDisplayLabel(string timestampFormat = null)
        {
            if (_timestamp.HasValue)
            {
                string format = string.IsNullOrEmpty(timestampFormat) ? DefaultTimestampFormat : timestampFormat;
                return _timestamp.Value.ToString(format);
            }

            if (!string.IsNullOrEmpty(displayLabel))
                return displayLabel;

            if (!string.IsNullOrEmpty(indexKey))
                return indexKey;

            if (!string.IsNullOrEmpty(rawIndexValue))
                return rawIndexValue;

            return string.Empty;
        }

        public string GetBranchLabel()
        {
            if (!string.IsNullOrEmpty(branchLabel))
                return branchLabel;

            if (!string.IsNullOrEmpty(indexKey))
                return indexKey;

            return string.Empty;
        }

        public string GetDetailLabel()
        {
            if (!string.IsNullOrEmpty(detailLabel))
                return detailLabel;
            return string.Empty;
        }
    }

    const string DefaultKnowledgePrefsKey = "timeloop.knowledge.v1";

    public static TimeLoopManager Instance { get; private set; }

    [Header("References")]
    public DialogueRunner runner;
    public TimeLoopWatchUI watchUI;

    [Header("Runtime Options")]
    [Tooltip("Automatically trigger the initial destination when the scene starts.")]
    public bool startAtInitialIndexOnStart = true;

    [Tooltip("Story CSV index key that should be activated when the scene boots. If empty the first available destination will be used.")]
    public string initialIndexKey;

    [Tooltip("Persist discovered knowledge between sessions using PlayerPrefs.")]
    public bool persistKnowledge = true;

    [Tooltip("PlayerPrefs key used to store discovered knowledge.")]
    public string knowledgePrefsKey = DefaultKnowledgePrefsKey;

    [Tooltip("Automatically find and bind the first wrist watch UI in the scene.")]
    public bool autoBindWatchUI = true;

    public event Action StateChanged;

    readonly HashSet<string> _knowledge = new HashSet<string>(StringComparer.Ordinal);
    readonly Dictionary<string, KnowledgeDefinition> _knowledgeDefinitions = new Dictionary<string, KnowledgeDefinition>(StringComparer.Ordinal);
    readonly List<KnowledgeDefinition> _knowledgeParseBuffer = new List<KnowledgeDefinition>();
    readonly List<Destination> _destinations = new List<Destination>();
    readonly Dictionary<string, int> _destinationIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    int _currentDestinationIndex = -1;
    string _currentDestinationKey;
    int _loopCount;

    DialogueRunner _boundRunner;
    bool _initialLoopPending;

    public IReadOnlyCollection<string> Knowledge => _knowledge;
    public IReadOnlyList<Destination> Destinations => _destinations;
    public int DestinationCount => _destinations.Count;
    public int CurrentDestinationIndex => _currentDestinationIndex;
    public string CurrentDestinationKey => string.IsNullOrEmpty(_currentDestinationKey) && TryGetDestination(_currentDestinationIndex, out var dest)
        ? dest.indexKey
        : _currentDestinationKey;

    public string CurrentDestinationLabel
    {
        get
        {
            if (TryGetDestination(_currentDestinationIndex, out var dest))
                return dest.GetDisplayLabel();
            if (!string.IsNullOrEmpty(_currentDestinationKey))
                return _currentDestinationKey;
            return string.Empty;
        }
    }

    public int LoopCount => _loopCount;
    public DialogueRunner Runner => runner;

    public bool CanLoopNow => runner != null && !PauseMenu.IsPaused && !TransitionManager.IsPlaying && !UiModalGate.IsOpen;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _initialLoopPending = startAtInitialIndexOnStart;

        LoadKnowledge();
        _knowledgeDefinitions.Clear();
        EnsureOwnedKnowledgePlaceholders();

        if (autoBindWatchUI && watchUI == null)
            watchUI = FindObjectOfType<TimeLoopWatchUI>(includeInactive: true);

        if (watchUI != null)
            watchUI.Bind(this);
    }

    void OnEnable()
    {
        BindRunnerIfNeeded();
    }

    void Start()
    {
        EnsureDestinationsPopulated();

        if (_initialLoopPending)
        {
            _initialLoopPending = !TryTriggerInitialLoop();
            if (_initialLoopPending)
                NotifyStateChanged();
        }
        else
        {
            NotifyStateChanged();
        }
    }

    void OnDisable()
    {
        UnbindRunner();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplySceneBindings(SceneRefHub hub)
    {
        if (hub == null)
        {
            if (runner != null)
            {
                UnbindRunner();
                runner = null;
            }
            return;
        }

        if (hub.dialogueRunner != null)
        {
            runner = hub.dialogueRunner;
            BindRunnerIfNeeded();
        }

        if (hub.collectionsPanel != null && hub.collectionsPanel.characterViewer != null)
        {
            var sourceDb = GameRoot.Instance ? GameRoot.Instance.characterDb : runner?.ui?.characters;
            if (sourceDb != null)
                hub.collectionsPanel.characterViewer.Bind(sourceDb);
        }

        if (autoBindWatchUI)
        {
            var foundWatch = hub.GetComponentInChildren<TimeLoopWatchUI>(includeInactive: true);
            if (foundWatch != null)
            {
                watchUI = foundWatch;
                watchUI.Bind(this);
            }
        }
    }

    void BindRunnerIfNeeded()
    {
        if (runner == null)
            runner = FindObjectOfType<DialogueRunner>();

        if (runner == null || runner == _boundRunner)
            return;

        UnbindRunner();
        _boundRunner = runner;
        _boundRunner.NodeEntered += HandleNodeEntered;
        RefreshKnowledgeDefinitions();
        RefreshDestinationsFromStory();
    }

    void UnbindRunner()
    {
        if (_boundRunner != null)
        {
            _boundRunner.NodeEntered -= HandleNodeEntered;
            _boundRunner = null;
        }

        ClearDestinations();
    }

    void HandleNodeEntered(DialogueNode node)
    {
        if (node.nodeId < 0)
            return;

        bool changed = false;
        ParseKnowledgeField(node.loopKnowledge, _knowledgeParseBuffer);

        for (int i = 0; i < _knowledgeParseBuffer.Count; i++)
        {
            var entry = _knowledgeParseBuffer[i];
            RegisterKnowledgeDefinition(entry);
            if (_knowledge.Add(entry.key))
                changed = true;
        }

        if (changed)
        {
            SaveKnowledge();
            NotifyStateChanged();
        }
    }

    public bool HasKnowledge(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return _knowledge.Contains(key);
    }

    public bool AcquireKnowledge(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        EnsurePlaceholderDefinition(key);

        if (_knowledge.Add(key))
        {
            SaveKnowledge();
            NotifyStateChanged();
            return true;
        }

        return false;
    }

    public bool TryLoopToDestination(int destinationIndex)
    {
        return TryLoopToDestination(destinationIndex, countAsLoop: true, force: false);
    }

    public bool TryLoopToDestination(int destinationIndex, bool countAsLoop, bool force)
    {
        EnsureDestinationsPopulated();

        if (!TryGetDestination(destinationIndex, out var destination))
            return false;

        return TryLoopToIndexKeyInternal(destination.indexKey, destinationIndex, countAsLoop, force);
    }

    public bool TryLoopToIndexKey(string indexKey)
    {
        return TryLoopToIndexKey(indexKey, countAsLoop: true, force: false);
    }

    public bool TryLoopToIndexKey(string indexKey, bool countAsLoop, bool force)
    {
        EnsureDestinationsPopulated();

        if (!string.IsNullOrEmpty(indexKey) && _destinationIndexByKey.TryGetValue(indexKey, out int index))
            return TryLoopToDestination(index, countAsLoop, force);

        return TryLoopToIndexKeyInternal(indexKey, -1, countAsLoop, force);
    }

    bool TryLoopToIndexKeyInternal(string indexKey, int destinationIndex, bool countAsLoop, bool force)
    {
        if (runner == null)
        {
            Debug.LogWarning("[TimeLoop] DialogueRunner is not assigned.");
            return false;
        }

        if (string.IsNullOrEmpty(indexKey))
        {
            Debug.LogWarning("[TimeLoop] Destination key is empty.");
            return false;
        }

        if (!force && !CanLoopNow)
            return false;

        if (!runner.TryGetNodeIdByIndexKey(indexKey, out int nodeId))
        {
            Debug.LogWarning($"[TimeLoop] Failed to resolve destination '{indexKey}'.");
            return false;
        }

        runner.RestartAtNode(nodeId);

        _currentDestinationIndex = destinationIndex;
        _currentDestinationKey = indexKey;
        if (countAsLoop)
            _loopCount++;

        NotifyStateChanged();
        return true;
    }

    public bool TryGetDestination(int index, out Destination destination)
    {
        if ((uint)index < (uint)_destinations.Count)
        {
            destination = _destinations[index];
            return true;
        }

        destination = default;
        return false;
    }

    public bool TryGetDestinationByIndexKey(string indexKey, out Destination destination)
    {
        EnsureDestinationsPopulated();

        if (!string.IsNullOrEmpty(indexKey) && _destinationIndexByKey.TryGetValue(indexKey, out int index))
            return TryGetDestination(index, out destination);

        destination = default;
        return false;
    }

    public string GetKnowledgeDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (_knowledgeDefinitions.TryGetValue(key, out var entry) && !string.IsNullOrEmpty(entry.displayName))
            return entry.displayName;

        return key;
    }

    public string GetKnowledgeDescription(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (_knowledgeDefinitions.TryGetValue(key, out var entry))
            return entry.description;

        return string.Empty;
    }

    void EnsureDestinationsPopulated()
    {
        if (_destinations.Count == 0)
            RefreshDestinationsFromStory();
    }

    void RefreshKnowledgeDefinitions()
    {
        _knowledgeDefinitions.Clear();

        var sourceRunner = runner != null ? runner : _boundRunner;
        var database = sourceRunner != null ? sourceRunner.Database : null;
        if (database != null)
        {
            for (int i = 0; i < database.nodeCount; i++)
            {
                ParseKnowledgeField(database.nodes[i].loopKnowledge, _knowledgeParseBuffer);
                for (int j = 0; j < _knowledgeParseBuffer.Count; j++)
                    RegisterKnowledgeDefinition(_knowledgeParseBuffer[j]);
            }
        }

        EnsureOwnedKnowledgePlaceholders();

        if (watchUI != null)
            watchUI.Refresh();
    }

    void RefreshDestinationsFromStory()
    {
        _destinations.Clear();
        _destinationIndexByKey.Clear();

        var sourceRunner = runner != null ? runner : _boundRunner;
        var database = sourceRunner != null ? sourceRunner.Database : null;
        if (database == null || database.nodeCount == 0)
        {
            _currentDestinationIndex = -1;
            _currentDestinationKey = string.Empty;
            NotifyStateChanged();
            return;
        }

        for (int i = 0; i < database.nodeCount; i++)
        {
            var node = database.nodes[i];
            string key = node.indexKey;
            if (string.IsNullOrEmpty(key))
                continue;

            if (_destinationIndexByKey.ContainsKey(key))
                continue;

            var destination = CreateDestinationFromNode(node);
            if (string.IsNullOrEmpty(destination.indexKey))
                continue;

            _destinationIndexByKey.Add(destination.indexKey, _destinations.Count);
            _destinations.Add(destination);
        }

        if (!string.IsNullOrEmpty(_currentDestinationKey) && _destinationIndexByKey.TryGetValue(_currentDestinationKey, out int idx))
        {
            _currentDestinationIndex = idx;
        }
        else if (_currentDestinationIndex >= _destinations.Count)
        {
            _currentDestinationIndex = _destinations.Count > 0 ? Mathf.Clamp(_currentDestinationIndex, 0, _destinations.Count - 1) : -1;
            if (_currentDestinationIndex >= 0)
                _currentDestinationKey = _destinations[_currentDestinationIndex].indexKey;
            else
                _currentDestinationKey = string.Empty;
        }
        else if (_currentDestinationIndex >= 0 && _currentDestinationIndex < _destinations.Count)
        {
            _currentDestinationKey = _destinations[_currentDestinationIndex].indexKey;
        }

        if (_initialLoopPending)
        {
            _initialLoopPending = !TryTriggerInitialLoop();
            if (!_initialLoopPending)
                return;
        }

        NotifyStateChanged();
    }

    static Destination CreateDestinationFromNode(DialogueNode node)
    {
        DateTime? timestamp = node.TryGetIndexTimestamp(out var parsedTimestamp) ? parsedTimestamp : (DateTime?)null;
        string display = string.IsNullOrEmpty(node.indexDisplayLabel) ? node.indexKey : node.indexDisplayLabel;
        string branch = node.indexBranchLabel ?? string.Empty;
        string detail = node.indexDetailLabel ?? string.Empty;
        string raw = string.IsNullOrEmpty(node.indexRawValue) ? node.indexKey : node.indexRawValue;

        return new Destination(node.indexKey, display, branch, detail, raw, timestamp);
    }

    void ClearDestinations()
    {
        _destinations.Clear();
        _destinationIndexByKey.Clear();
        _currentDestinationIndex = -1;
        _currentDestinationKey = string.Empty;
        NotifyStateChanged();
    }

    bool TryTriggerInitialLoop()
    {
        if (!startAtInitialIndexOnStart || runner == null)
            return false;

        if (!string.IsNullOrEmpty(initialIndexKey))
        {
            int idx = _destinationIndexByKey.TryGetValue(initialIndexKey, out var mapped) ? mapped : -1;
            if (TryLoopToIndexKeyInternal(initialIndexKey, idx, countAsLoop: false, force: true))
                return true;
        }

        if (_destinations.Count > 0)
        {
            var destination = _destinations[0];
            return TryLoopToIndexKeyInternal(destination.indexKey, 0, countAsLoop: false, force: true);
        }

        return false;
    }

    static void ParseKnowledgeField(string field, List<KnowledgeDefinition> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();
        if (string.IsNullOrWhiteSpace(field))
            return;

        string[] entries = field.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string segment = entries[i];
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            string[] parts = segment.Split('|');
            string key = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
                continue;

            string display = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            string description = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            buffer.Add(new KnowledgeDefinition(key, display, description));
        }
    }

    void RegisterKnowledgeDefinition(KnowledgeDefinition entry)
    {
        if (string.IsNullOrEmpty(entry.key))
            return;

        if (_knowledgeDefinitions.TryGetValue(entry.key, out var existing))
        {
            string display = string.IsNullOrEmpty(entry.displayName) ? existing.displayName : entry.displayName;
            string description = string.IsNullOrEmpty(entry.description) ? existing.description : entry.description;
            _knowledgeDefinitions[entry.key] = new KnowledgeDefinition(entry.key,
                string.IsNullOrEmpty(display) ? entry.key : display,
                description);
        }
        else
        {
            string display = string.IsNullOrEmpty(entry.displayName) ? entry.key : entry.displayName;
            _knowledgeDefinitions[entry.key] = new KnowledgeDefinition(entry.key, display, entry.description);
        }
    }

    void EnsureOwnedKnowledgePlaceholders()
    {
        foreach (var key in _knowledge)
            EnsurePlaceholderDefinition(key);
    }

    void EnsurePlaceholderDefinition(string key)
    {
        if (string.IsNullOrEmpty(key) || _knowledgeDefinitions.ContainsKey(key))
            return;
        _knowledgeDefinitions[key] = new KnowledgeDefinition(key, key, string.Empty);
    }

    void NotifyStateChanged()
    {
        StateChanged?.Invoke();
        if (watchUI != null)
            watchUI.Refresh();
    }

    void LoadKnowledge()
    {
        _knowledge.Clear();
        if (!persistKnowledge)
            return;

        string json = PlayerPrefs.GetString(string.IsNullOrEmpty(knowledgePrefsKey) ? DefaultKnowledgePrefsKey : knowledgePrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var box = JsonUtility.FromJson<KnowledgeSaveBox>(json);
            if (box?.values == null)
                return;

            for (int i = 0; i < box.values.Length; i++)
            {
                var key = box.values[i];
                if (!string.IsNullOrEmpty(key))
                    _knowledge.Add(key);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TimeLoop] Failed to load knowledge: {ex.Message}");
        }
    }

    void SaveKnowledge()
    {
        if (!persistKnowledge)
            return;

        var arr = new string[_knowledge.Count];
        _knowledge.CopyTo(arr);
        var box = new KnowledgeSaveBox { values = arr };
        string json = JsonUtility.ToJson(box, false);
        PlayerPrefs.SetString(string.IsNullOrEmpty(knowledgePrefsKey) ? DefaultKnowledgePrefsKey : knowledgePrefsKey, json);
        PlayerPrefs.Save();
    }
}
