using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenLoad : MonoBehaviour
{
    [SerializeField] private SaveLoadPanel saveLoadPanel; // �ν����Ϳ��� �巡��

    public void Open() // ��ư OnClick�� ����
    {
        if (!saveLoadPanel) { Debug.LogError("SaveLoadPanel ������ ������ϴ�."); return; }
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
        Debug.Log("[UI] Open Load Menu");
    }
}
