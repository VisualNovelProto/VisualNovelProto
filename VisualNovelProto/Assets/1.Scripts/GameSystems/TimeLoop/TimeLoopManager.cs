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

    const string DefaultKnowledgePrefsKey = "timeloop.knowledge.v1";

    public static TimeLoopManager Instance { get; private set; }

    [Header("References")]
    public DialogueRunner runner;
    public TimeLoopSheet schedule;
    public TimeLoopKnowledgeSheet knowledgeSheet;
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
    TimeLoopSlotBranch _currentBranch;
    int _currentSlotIndex = -1;
    int _loopCount;

    DialogueRunner _boundRunner;

    public IReadOnlyCollection<string> Knowledge => _knowledge;
    public int CurrentSlotIndex => _currentSlotIndex;
    public TimeLoopSlot CurrentSlot => schedule?.GetSlotOrDefault(_currentSlotIndex);
    public TimeLoopSlotBranch CurrentBranch => _currentBranch;
    public int LoopCount => _loopCount;

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
        if (knowledgeSheet == null || node.nodeId < 0)
            return;

        bool changed = false;
        foreach (var entry in knowledgeSheet.EnumerateUnlocksForIndexKey(node.indexKey))
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;
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
