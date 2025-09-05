using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

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

    public void Bind(int index, bool manual, SaveLoadManager.SaveData? meta, Texture2D thumb, Action<SaveSlotView> onClick)
    {
        slotIndex = index; manualSlot = manual;
        hasData = meta.HasValue;

        if (titleText) titleText.text = manual ? $"Slot {index:00}" : $"Auto {index:00}";

        if (metaText)
        {
            if (meta.HasValue)
            {
                var m = meta.Value;

                // ����� ���ڿ� "yyyy-MM-dd HH:mm:ss" �� DateTime���� �Ľ�
                DateTime dt;
                if (!DateTime.TryParseExact(m.timestamp, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    dt = DateTime.Now;
                }

                // 2��: ��¥/�ð�(�ѱ���) + �÷���Ÿ��
                string koDateTime = dt.ToString("yyyy�� M�� d�� HH�� mm�� ss��", new CultureInfo("ko-KR"));
                string play = FormatPlayTimeKo(TimeSpan.FromSeconds(Mathf.Max(0f, m.playtimeSec)));
                metaText.text = $"{koDateTime}\n{play}";
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
    static string FormatPlayTimeKo(TimeSpan t)
    {
        if (t.TotalSeconds < 1) return "0��";
        var sb = new System.Text.StringBuilder(16);
        sb.Append("�÷��� Ÿ�� : ");
        if (t.TotalHours >= 1) sb.Append((int)t.TotalHours).Append("�ð� ");
        if (t.Minutes > 0) sb.Append(t.Minutes).Append("�� ");
        sb.Append(t.Seconds).Append("��");
        return sb.ToString();
    }
}
