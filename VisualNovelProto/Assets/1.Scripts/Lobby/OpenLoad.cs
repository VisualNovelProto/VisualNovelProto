using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenLoad : MonoBehaviour
{
    [SerializeField] private SaveLoadPanel saveLoadPanel; // 인스펙터에서 드래그

    public void Open() // 버튼 OnClick에 연결
    {
        if (!saveLoadPanel) { Debug.LogError("SaveLoadPanel 참조가 비었습니다."); return; }
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
        Debug.Log("[UI] Open Load Menu");
    }
}
