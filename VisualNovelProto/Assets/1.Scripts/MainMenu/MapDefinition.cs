using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MapNodeDef
{
    public int id;
    public string imageKey;
    public int[] neighbors;
}

public class MapDefinition : MonoBehaviour
{
    public MapNodeDef[] nodes;
    public int startNodeId = 0;

    public enum EdgePolicy { Directed, UndirectedOnDemand }
    public EdgePolicy edgePolicy = EdgePolicy.Directed;

    void OnValidate()
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Length; i++)
            nodes[i].id = i; // ★ id만 자동 세팅, neighbors는 손대지 않음
    }

    // 필요할 때 인스펙터에서 수동 호출
    [ContextMenu("Rebuild Undirected Edges Once")]
    public void RebuildUndirectedOnce()
    {
        if (nodes == null || edgePolicy != EdgePolicy.UndirectedOnDemand) return;

        var sets = new List<HashSet<int>>();
        for (int i = 0; i < nodes.Length; i++)
            sets.Add(new HashSet<int>(nodes[i].neighbors ?? Array.Empty<int>()));

        // 양방향 반영(한 번만)
        for (int a = 0; a < nodes.Length; a++)
        {
            foreach (var b in sets[a])
                if (b >= 0 && b < nodes.Length)
                    sets[b].Add(a);
        }

        // 원래 순서 보존 + 새로 생긴 역방향은 뒤에만 추가
        for (int i = 0; i < nodes.Length; i++)
        {
            var original = nodes[i].neighbors ?? Array.Empty<int>();
            var hs = new HashSet<int>(original);
            var appended = new List<int>(original);
            foreach (var nb in sets[i])
                if (!hs.Contains(nb)) appended.Add(nb);
            nodes[i].neighbors = appended.ToArray();
        }
    }
}
