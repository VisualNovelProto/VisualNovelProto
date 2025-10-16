using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the pool of knowledge items that can persist between time loops. Knowledge keys are
/// referenced by <see cref="TimeLoopSlotBranch"/> requirements and are unlocked automatically when
/// the player reaches the configured story nodes.
/// </summary>
[CreateAssetMenu(menuName = "Story/Time Loop Knowledge Sheet", fileName = "TimeLoopKnowledge")]
public sealed class TimeLoopKnowledgeSheet : ScriptableObject
{
    [Tooltip("List of knowledge definitions. Keys must be unique.")]
    public TimeLoopKnowledgeEntry[] entries = Array.Empty<TimeLoopKnowledgeEntry>();

    public bool TryGetByKey(string key, out TimeLoopKnowledgeEntry entry)
    {
        if (string.IsNullOrEmpty(key) || entries == null)
        {
            entry = null;
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            var candidate = entries[i];
            if (candidate != null && string.Equals(candidate.key, key, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public IEnumerable<TimeLoopKnowledgeEntry> EnumerateUnlocksForIndexKey(string indexKey)
    {
        if (string.IsNullOrEmpty(indexKey) || entries == null)
            yield break;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.unlockIndexKeys == null)
                continue;

            for (int j = 0; j < entry.unlockIndexKeys.Length; j++)
            {
                if (string.Equals(entry.unlockIndexKeys[j], indexKey, StringComparison.Ordinal))
                {
                    yield return entry;
                    break;
                }
            }
        }
    }
}

[Serializable]
public sealed class TimeLoopKnowledgeEntry
{
    [Tooltip("Unique key referenced by branches and save data.")]
    public string key;

    [Tooltip("Display name used in the UI.")]
    public string displayName;

    [Tooltip("Optional description shown in debug or tools.")]
    [TextArea]
    public string description;

    [Tooltip("Story index keys that will unlock this knowledge when visited.")]
    public string[] unlockIndexKeys = Array.Empty<string>();
}
