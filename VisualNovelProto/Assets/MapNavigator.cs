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
        var cur = mapManager.GetCurrentNode();
        if (cur != null) mapView.Show(cur.imageKey);
    }
}
