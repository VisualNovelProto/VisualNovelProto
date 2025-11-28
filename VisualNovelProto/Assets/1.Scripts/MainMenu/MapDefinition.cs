using System;
using UnityEngine;

[Serializable]
public class MapNodeDef
{
    public int id;               // OnValidate에서 인덱스로 자동 세팅
    public string title;         // 버튼 라벨용(없으면 imageKey 사용)
    public string imageKey;      // SpriteTable key
    public int[] neighbors;      // 이동 가능한 노드 id들 (엑셀에서 입력)
}

public class MapDefinition : MonoBehaviour
{
    public MapNodeDef[] nodes;
    public int startNodeId = 0;

    void OnValidate()
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Length; i++)
            nodes[i].id = i; // ★ neighbors 순서는 절대 건드리지 않음(엑셀 입력 유지)
    }
}
