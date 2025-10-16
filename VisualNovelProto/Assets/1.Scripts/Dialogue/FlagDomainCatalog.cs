using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Story/Flag Domain Catalog", fileName = "FlagDomainCatalog")]
public sealed class FlagDomainCatalog : ScriptableObject
{
    [Tooltip("When true, any flag id that is not explicitly listed will be treated as persistent.")]
    public bool treatUnlistedAsPersistent = true;

    [Tooltip("Flag ids that should always be saved to the persistent GlobalFlags store.")]
    public int[] persistentFlags = System.Array.Empty<int>();

    [Tooltip("Flag ids that should be limited to the current session and excluded from GlobalFlags.")]
    public int[] sessionOnlyFlags = System.Array.Empty<int>();

    HashSet<int> _persistent;
    HashSet<int> _sessionOnly;

    void OnEnable() => BuildCaches();
    void OnValidate() => BuildCaches();

    void BuildCaches()
    {
        _persistent = BuildSet(persistentFlags);
        _sessionOnly = BuildSet(sessionOnlyFlags);
    }

    static HashSet<int> BuildSet(int[] source)
    {
        var set = new HashSet<int>();
        if (source == null)
            return set;

        for (int i = 0; i < source.Length; i++)
        {
            int id = source[i];
            if (id > 0)
                set.Add(id);
        }
        return set;
    }

    void EnsureCaches()
    {
        if (_persistent == null || _sessionOnly == null)
            BuildCaches();
    }

    public bool IsPersistent(int id)
    {
        if (id <= 0)
            return false;

        EnsureCaches();

        if (_sessionOnly.Contains(id))
            return false;

        if (_persistent.Contains(id))
            return true;

        return treatUnlistedAsPersistent;
    }

    public void CollectPersistent(int[] pool, int offset, int count, List<int> target)
    {
        if (pool == null || target == null || count <= 0)
            return;

        EnsureCaches();

        for (int i = 0; i < count; i++)
        {
            int id = pool[offset + i];
            if (IsPersistent(id))
                target.Add(id);
        }
    }
}
