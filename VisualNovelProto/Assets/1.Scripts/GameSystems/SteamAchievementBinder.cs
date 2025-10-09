using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges GlobalFlags to Steam achievements. Whenever a configured flag is earned
/// we unlock the corresponding Steam achievement.
/// </summary>
public sealed class SteamAchievementBinder : MonoBehaviour
{
    [System.Serializable]
    struct Entry
    {
        public int flagId;
        public string achievementId;
    }

    [Tooltip("Automatically checks already unlocked flags on Awake and syncs them to Steam achievements.")]
    [SerializeField] bool syncExistingOnAwake = true;

    [Tooltip("Mapping between story flag ids and Steam achievement identifiers.")]
    [SerializeField] Entry[] entries = System.Array.Empty<Entry>();

    Dictionary<int, string> _map;

    void Awake()
    {
        BuildLookup();

        if (syncExistingOnAwake)
            SyncExistingFlags();
    }

    void OnEnable()
    {
        GlobalFlags.FlagAdded += HandleFlagAdded;
    }

    void OnDisable()
    {
        GlobalFlags.FlagAdded -= HandleFlagAdded;
    }

    void BuildLookup()
    {
        if (_map != null)
            return;

        _map = new Dictionary<int, string>();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            int flagId = entries[i].flagId;
            string achievementId = entries[i].achievementId;
            if (flagId <= 0 || string.IsNullOrEmpty(achievementId))
                continue;

            _map[flagId] = achievementId.Trim();
        }
    }

    void SyncExistingFlags()
    {
        if (_map == null || _map.Count == 0)
            return;

        foreach (var pair in _map)
        {
            if (GlobalFlags.Has(pair.Key))
                SteamIntegrationManager.UnlockAchievementGlobal(pair.Value);
        }
    }

    void HandleFlagAdded(int flagId)
    {
        if (_map == null || _map.Count == 0)
            return;

        if (_map.TryGetValue(flagId, out string achievementId))
            SteamIntegrationManager.UnlockAchievementGlobal(achievementId);
    }
}
