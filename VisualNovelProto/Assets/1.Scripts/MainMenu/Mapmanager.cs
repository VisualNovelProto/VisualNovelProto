using System.Collections.Generic;
using UnityEngine;

public class Mapmanager : MonoBehaviour
{
    public class MapNode
    {
        public int id;
        public string title;
        public string imageKey;
        public List<int> neighbors;
    }

    [SerializeField] private MapDefinition definition;

    private readonly Dictionary<int, MapNode> nodeTable = new Dictionary<int, MapNode>();
    public int CurrentNodeId { get; private set; }

    void Awake()
    {
        BuildGraphFromDefinition(definition);
        int max = (definition.nodes?.Length ?? 1) - 1;
        CurrentNodeId = Mathf.Clamp(definition.startNodeId, 0, Mathf.Max(0, max));
    }

    void BuildGraphFromDefinition(MapDefinition def)
    {
        nodeTable.Clear();
        if (def?.nodes == null) return;

        foreach (var d in def.nodes)
        {
            nodeTable[d.id] = new MapNode
            {
                id = d.id,
                title = d.title,
                imageKey = d.imageKey,
                neighbors = new List<int>(d.neighbors ?? new int[0])
            };
        }
    }

    public MapNode GetCurrentNode() => GetNode(CurrentNodeId);

    public MapNode GetNode(int id) => nodeTable.TryGetValue(id, out var n) ? n : null;

    public bool CanMoveTo(int targetId)
    {
        var cur = GetCurrentNode();
        return cur != null && cur.neighbors != null && cur.neighbors.Contains(targetId);
    }

    public bool MoveTo(int targetId)
    {
        if (!CanMoveTo(targetId)) return false;
        CurrentNodeId = targetId;
        return true;
    }
}
