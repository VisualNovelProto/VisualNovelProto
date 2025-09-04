using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotView : MonoBehaviour
{
    [Header("UI")]
    public Button button;
    public RawImage thumbnail;         // �����(������ Image�� OK)
    public TMP_Text titleText;         // "Slot 01" ��
    public TMP_Text metaText;          // ��¥/é��/�÷���Ÿ��
    public GameObject emptyBadge;      // "EMPTY" ��������

    [HideInInspector] public int slotIndex;
    [HideInInspector] public bool manualSlot = true;
    [HideInInspector] public bool hasData;

    Texture2D _thumb;                  // �޸� ���� ���� ĳ��

    void OnDestroy()
    {
        if (_thumb) Destroy(_thumb);
    }

    public void Bind(int index, bool manual, SaveLoadManager.SaveData? meta, Texture2D thumb, System.Action<SaveSlotView> onClick)
    {
        slotIndex = index; manualSlot = manual;
        hasData = meta.HasValue;

        if (titleText) titleText.text = manual ? $"Slot {index:00}" : $"Auto {index:00}";

        if (metaText)
        {
            if (meta.HasValue)
            {
                var m = meta.Value;
                // �ʿ��� �׸� ���� ǥ��
                metaText.text = $"{m.timestamp}\n{m.sceneName}  n:{m.nodeId}  t:{Mathf.RoundToInt(m.playtimeSec / 60f)}m";
            }
            else metaText.text = "-";
        }

        if (emptyBadge) emptyBadge.SetActive(!hasData);

        if (thumbnail)
        {
            if (_thumb) { Destroy(_thumb); _thumb = null; }
            if (thumb)
            {
                _thumb = thumb;
                thumbnail.texture = _thumb;
                thumbnail.enabled = true;
            }
            else
            {
                thumbnail.texture = null;
                thumbnail.enabled = false;
            }
        }

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(this));
            button.interactable = hasData || manual; // �ε� �ǿ��� �� ���� Ŭ���� ����
        }
    }
}
