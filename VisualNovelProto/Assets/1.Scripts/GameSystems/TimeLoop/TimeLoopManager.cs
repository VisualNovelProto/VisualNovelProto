using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central coordinator for the 30-minute rewind system. Handles knowledge persistence, resolves
/// which branch should be used for a given time slot and asks the <see cref="DialogueRunner"/> to
/// jump to the desired node when the player activates a loop from the wrist watch UI.
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

    const string DefaultKnowledgePrefsKey = "timeloop.knowledge.v1";

    public static TimeLoopManager Instance { get; private set; }

    [Header("References")]
    public DialogueRunner runner;
    public TimeLoopSheet schedule;
    public TimeLoopWatchUI watchUI;

    [Header("Runtime Options")]
    [Tooltip("Automatically trigger the initial slot when the scene starts.")]
    public bool startAtInitialSlotOnStart = true;

    [Tooltip("Initial slot index used when the scene boots.")]
    public int initialSlotIndex = 0;

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
    readonly List<string> _knowledgeNamesBuffer = new List<string>();
    TimeLoopSlotBranch _currentBranch;
    int _currentSlotIndex = -1;
    int _loopCount;

    DialogueRunner _boundRunner;

    public IReadOnlyCollection<string> Knowledge => _knowledge;
    public int CurrentSlotIndex => _currentSlotIndex;
    public TimeLoopSlot CurrentSlot => schedule?.GetSlotOrDefault(_currentSlotIndex);
    public TimeLoopSlotBranch CurrentBranch => _currentBranch;
    public int LoopCount => _loopCount;
    public DialogueRunner Runner => runner;

    public int CurrentMinutes => CurrentSlot?.minuteOfDay ?? 0;

    public bool CanLoopNow => runner != null && !PauseMenu.IsPaused && !TransitionManager.IsPlaying && !UiModalGate.IsOpen;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadKnowledge();
        _knowledgeDefinitions.Clear();
        EnsureScheduleKnowledgePlaceholders();
        EnsureOwnedKnowledgePlaceholders();

        if (autoBindWatchUI && watchUI == null)
            watchUI = FindObjectOfType<TimeLoopWatchUI>(includeInactive: true);

        if (watchUI != null)
            watchUI.Bind(this);
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

    void OnEnable()
    {
        BindRunnerIfNeeded();
    }

    void Start()
    {
        if (startAtInitialSlotOnStart && schedule != null && schedule.SlotCount > 0)
        {
            int index = Mathf.Clamp(initialSlotIndex, 0, schedule.SlotCount - 1);
            TryLoopToSlot(index, countAsLoop: false, force: true);
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
    }

    void UnbindRunner()
    {
        if (_boundRunner != null)
        {
            _boundRunner.NodeEntered -= HandleNodeEntered;
            _boundRunner = null;
        }
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

    public bool TryLoopToSlot(int slotIndex)
    {
        return TryLoopToSlot(slotIndex, countAsLoop: true, force: false);
    }

    public bool TryLoopToSlot(int slotIndex, bool countAsLoop, bool force)
    {
        if (schedule == null)
        {
            Debug.LogWarning("[TimeLoop] Schedule is not assigned.");
            return false;
        }

        if (runner == null)
        {
            Debug.LogWarning("[TimeLoop] DialogueRunner is not assigned.");
            return false;
        }

        if (!force && !CanLoopNow)
            return false;

        if (!schedule.TryGetSlot(slotIndex, out var slot))
            return false;

        var branch = slot.SelectBranch(_knowledge);
        if (branch == null)
        {
            Debug.LogWarning($"[TimeLoop] No branch available for slot {slotIndex} ({slot.GetDisplayLabel()}).");
            return false;
        }

        if (!TryResolveNodeId(branch, out int nodeId))
            return false;

        runner.RestartAtNode(nodeId);

        _currentSlotIndex = slotIndex;
        _currentBranch = branch;
        if (countAsLoop)
            _loopCount++;

        NotifyStateChanged();
        return true;
    }

    public bool TryGetResolvedBranch(int slotIndex, out TimeLoopSlot slot, out TimeLoopSlotBranch branch)
    {
        slot = null;
        branch = null;
        if (schedule == null)
            return false;

        if (!schedule.TryGetSlot(slotIndex, out slot) || slot == null)
            return false;

        branch = slot.SelectBranch(_knowledge);
        return branch != null;
    }

    public TimeLoopSlotBranch GetNextLockedBranch(int slotIndex)
    {
        if (schedule == null)
            return null;

        if (!schedule.TryGetSlot(slotIndex, out var slot) || slot == null)
            return null;

        return slot.FindNextLockedBranch(_knowledge);
    }

    bool TryResolveNodeId(TimeLoopSlotBranch branch, out int nodeId)
    {
        nodeId = -1;
        if (branch == null)
            return false;

        if (branch.explicitNodeId >= 0)
        {
            nodeId = branch.explicitNodeId;
            return true;
        }

        if (runner == null)
            return false;

        if (runner.TryGetNodeIdByIndexKey(branch.storyIndexKey, out nodeId))
            return true;

        Debug.LogWarning($"[TimeLoop] Failed to resolve node for branch '{branch.branchName}' (index key: '{branch.storyIndexKey}').");
        return false;
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

        EnsureScheduleKnowledgePlaceholders();
        EnsureOwnedKnowledgePlaceholders();

        if (watchUI != null)
            watchUI.Refresh();
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

    void EnsureScheduleKnowledgePlaceholders()
    {
        if (schedule?.slots == null)
            return;

        for (int i = 0; i < schedule.slots.Length; i++)
        {
            var slot = schedule.slots[i];
            if (slot?.branches == null)
                continue;

            for (int j = 0; j < slot.branches.Length; j++)
            {
                var branch = slot.branches[j];
                if (branch?.requiredKnowledgeKeys == null)
                    continue;

                for (int k = 0; k < branch.requiredKnowledgeKeys.Length; k++)
                    EnsurePlaceholderDefinition(branch.requiredKnowledgeKeys[k]);
            }
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

    public string BuildRequirementSummary(TimeLoopSlotBranch branch)
    {
        if (branch == null)
            return string.Empty;

        if (!branch.HasRequirements)
            return "기본 타임라인";

        _knowledgeNamesBuffer.Clear();
        if (branch.requiredKnowledgeKeys != null)
        {
            for (int i = 0; i < branch.requiredKnowledgeKeys.Length; i++)
            {
                string key = branch.requiredKnowledgeKeys[i];
                if (string.IsNullOrEmpty(key))
                    continue;
                _knowledgeNamesBuffer.Add(GetKnowledgeDisplayName(key));
            }
        }

        if (_knowledgeNamesBuffer.Count == 0)
            return "기본 타임라인";

        if (_knowledgeNamesBuffer.Count == 1)
            return $"필요 지식: {_knowledgeNamesBuffer[0]}";

        return $"필요 지식: {string.Join(", ", _knowledgeNamesBuffer)}";
    }

    public string BuildMissingRequirementSummary(TimeLoopSlotBranch branch)
    {
        if (branch == null)
            return string.Empty;

        if (!branch.HasRequirements)
            return string.Empty;

        _knowledgeNamesBuffer.Clear();
        foreach (var key in branch.EnumerateMissingRequirements(_knowledge))
        {
            if (string.IsNullOrEmpty(key))
                continue;
            _knowledgeNamesBuffer.Add(GetKnowledgeDisplayName(key));
        }

        if (_knowledgeNamesBuffer.Count == 0)
            return BuildRequirementSummary(branch);

        return $"필요: {string.Join(", ", _knowledgeNamesBuffer)}";
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
