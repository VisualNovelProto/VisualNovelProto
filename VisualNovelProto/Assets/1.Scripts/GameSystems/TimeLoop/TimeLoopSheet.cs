using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes the schedule of rewindable time slots. Each slot represents a 30 minute
/// checkpoint that the player can jump back to via the wrist watch UI.
/// </summary>
[CreateAssetMenu(menuName = "Story/Time Loop Sheet", fileName = "TimeLoopSheet")]
public sealed class TimeLoopSheet : ScriptableObject
{
    [Tooltip("Chronological list of rewind targets. Each slot should represent a 30 minute chunk.")]
    public TimeLoopSlot[] slots = Array.Empty<TimeLoopSlot>();

    public int SlotCount => slots?.Length ?? 0;

    public bool TryGetSlot(int index, out TimeLoopSlot slot)
    {
        if (slots != null && index >= 0 && index < slots.Length)
        {
            slot = slots[index];
            return slot != null;
        }

        slot = null;
        return false;
    }

    public TimeLoopSlot GetSlotOrDefault(int index)
    {
        if (TryGetSlot(index, out var slot))
            return slot;
        return null;
    }

    public TimeLoopSlot FindSlotByMinutes(int minuteOfDay)
    {
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot != null && slot.minuteOfDay == minuteOfDay)
                return slot;
        }

        return null;
    }
}

[Serializable]
public sealed class TimeLoopSlot
{
    [Tooltip("Display name shown in the watch UI. Falls back to the formatted time when empty.")]
    public string label;

    [Tooltip("Minute of day (0-1439). Should be spaced by 30 minutes for the prototype.")]
    [Range(0, 1439)]
    public int minuteOfDay;

    [Tooltip("Optional notes for designers. Not shown in game.")]
    [TextArea]
    public string notes;

    [Tooltip("Ordered list of branches for this time slot. The first branch whose knowledge requirements are met will be used.")]
    public TimeLoopSlotBranch[] branches = Array.Empty<TimeLoopSlotBranch>();

    public string GetFormattedTime()
    {
        int minutes = Mathf.Clamp(minuteOfDay, 0, 23 * 60 + 59);
        int hour = minutes / 60;
        int minute = minutes % 60;
        return $"{hour:00}:{minute:00}";
    }

    public string GetDisplayLabel()
    {
        if (!string.IsNullOrWhiteSpace(label))
            return label;
        return GetFormattedTime();
    }

    public TimeLoopSlotBranch SelectBranch(IReadOnlyCollection<string> knowledge)
    {
        if (branches == null || branches.Length == 0)
            return null;

        foreach (var branch in branches)
        {
            if (branch == null)
                continue;
            if (branch.IsUnlocked(knowledge))
                return branch;
        }

        return null;
    }

    public TimeLoopSlotBranch FindNextLockedBranch(IReadOnlyCollection<string> knowledge)
    {
        if (branches == null || branches.Length == 0)
            return null;

        foreach (var branch in branches)
        {
            if (branch == null)
                continue;
            if (!branch.IsUnlocked(knowledge))
                return branch;
        }

        return null;
    }
}

[Serializable]
public sealed class TimeLoopSlotBranch
{
    [Tooltip("Friendly name for the branch shown in the UI.")]
    public string branchName;

    [Tooltip("Optional description shown in the UI.")]
    [TextArea]
    public string description;

    [Tooltip("Story node key (Index column from the story CSV) that should be jumped to when this branch is selected.")]
    public string storyIndexKey;

    [Tooltip("Optional explicit node id. Overrides the index key when >= 0.")]
    public int explicitNodeId = -1;

    [Tooltip("Knowledge keys that must be owned to unlock this branch. Leave empty for the default timeline.")]
    public string[] requiredKnowledgeKeys = Array.Empty<string>();

    public bool HasRequirements => requiredKnowledgeKeys != null && requiredKnowledgeKeys.Length > 0;

    public bool IsUnlocked(IReadOnlyCollection<string> knowledge)
    {
        if (!HasRequirements)
            return true;
        if (knowledge == null)
            return false;

        foreach (var key in requiredKnowledgeKeys)
        {
            if (string.IsNullOrEmpty(key))
                continue;

            bool found = false;
            foreach (var owned in knowledge)
            {
                if (string.Equals(owned, key, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    public IEnumerable<string> EnumerateMissingRequirements(IReadOnlyCollection<string> knowledge)
    {
        if (!HasRequirements)
            yield break;

        foreach (var key in requiredKnowledgeKeys)
        {
            if (string.IsNullOrEmpty(key))
                continue;
            if (knowledge == null)
            {
                yield return key;
                continue;
            }

            bool found = false;
            foreach (var owned in knowledge)
            {
                if (string.Equals(owned, key, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                yield return key;
        }
    }

    public string BuildRequirementSummary()
    {
        if (!HasRequirements)
            return "기본 타임라인";

        if (requiredKnowledgeKeys.Length == 1)
            return $"필요 지식: {requiredKnowledgeKeys[0]}";

        return $"필요 지식: {string.Join(", ", requiredKnowledgeKeys)}";
    }
}
