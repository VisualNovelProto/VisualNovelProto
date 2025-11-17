using System.Collections.Generic;
using UnityEngine;

public class Mapmanager : MonoBehaviour
{
    public class MapNode
    {
        public int id;
        public string imageKey;
        public List<int> neighbors;
    }

    [SerializeField] private MapDefinition definition;

    private readonly Dictionary<int, MapNode> nodeTable = new Dictionary<int, MapNode>();
    private readonly Stack<int> history = new Stack<int>();

    public int CurrentNodeId { get; private set; }

    void Awake()
    {
        BuildGraphFromDefinition(definition);
        CurrentNodeId = Mathf.Clamp(definition.startNodeId, 0, Mathf.Max(0, (definition.nodes?.Length ?? 1) - 1));
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
        history.Push(CurrentNodeId);
        CurrentNodeId = targetId;
        return true;
    }

    public bool CanBack() => history.Count > 0;

    public bool Back()
    {
        if (!CanBack()) return false;
        CurrentNodeId = history.Pop();
        return true;
    }

    public int? PeekPrevNodeId() => history.Count > 0 ? history.Peek() : (int?)null;
}
