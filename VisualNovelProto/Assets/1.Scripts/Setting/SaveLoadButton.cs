using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveLoadButton : MonoBehaviour
{
    public int slot = 0; // 인스펙터에서 설정

    public void OnClickSave()
    {
        bool ok = SaveLoadManager.Instance != null && SaveLoadManager.Instance.SaveManual(slot);
    }

    public void OnClickLoad()
    {
        SaveLoadManager.Instance?.LoadManual(slot);
    }
}
