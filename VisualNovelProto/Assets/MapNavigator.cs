using UnityEngine;

public class MapNavigator : MonoBehaviour
{
    [SerializeField] private Mapmanager mapManager;
    [SerializeField] private MapView mapView;

    void Start()
    {
        var node = mapManager.GetCurrentNode();
        if (node != null) mapView.Show(node.imageKey);
    }

    public void TryMoveTo(int targetId)
    {
        if (!mapManager.CanMoveTo(targetId))
        {
            Debug.Log($"이동 불가: {mapManager.GetCurrentNode()?.id} -> {targetId}");
            return;
        }
        mapManager.MoveTo(targetId);
        mapView.Show(mapManager.GetCurrentNode().imageKey);
    }

    // 버튼 1개에 연결: 직전 노드로 '되돌이' 금지, 앞으로만 진행. 막다른 곳이면 멈춤.
    public void GoNext()
    {
        var cur = mapManager.GetCurrentNode();
        if (cur == null || cur.neighbors == null || cur.neighbors.Count == 0)
        {
            Debug.Log("다음 이웃 없음");
            return;
        }

        int? prev = mapManager.PeekPrevNodeId();
        int? next = null;

        foreach (var nb in cur.neighbors)
        {
            if (!prev.HasValue || nb != prev.Value)
            {
                next = nb;
                break;
            }
        }

        if (!next.HasValue)
        {
            Debug.Log("앞으로 진행할 이웃이 없음(되돌이만 존재). 정지.");
            return;
        }

        TryMoveTo(next.Value);
    }

    // 선택: 뒤로가기 버튼에 연결
    public void GoPrev()
    {
        if (!mapManager.CanBack())
        {
            Debug.Log("뒤로 갈 기록 없음");
            return;
        }
        mapManager.Back();
        mapView.Show(mapManager.GetCurrentNode().imageKey);
    }
}
