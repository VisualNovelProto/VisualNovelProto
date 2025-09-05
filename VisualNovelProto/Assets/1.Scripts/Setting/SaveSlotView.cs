using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

public sealed class SaveSlotView : MonoBehaviour
{
    [Header("UI")]
    public Button button;
    public RawImage thumbnail;         // 썸네일(없으면 Image도 OK)
    public TMP_Text titleText;         // "Slot 01" 등
    public TMP_Text metaText;          // 날짜/챕터/플레이타임
    public GameObject emptyBadge;      // "EMPTY" 오버레이

    [HideInInspector] public int slotIndex;
    [HideInInspector] public bool manualSlot = true;
    [HideInInspector] public bool hasData;

    Texture2D _thumb;                  // 메모리 누수 방지 캐시

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

                // 저장된 문자열 "yyyy-MM-dd HH:mm:ss" → DateTime으로 파싱
                DateTime dt;
                if (!DateTime.TryParseExact(m.timestamp, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    dt = DateTime.Now;
                }

                // 2줄: 날짜/시간(한국식) + 플레이타임
                string koDateTime = dt.ToString("yyyy년 M월 d일 HH시 mm분 ss초", new CultureInfo("ko-KR"));
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
            button.interactable = hasData || manual; // 로드 탭에서 빈 슬롯 클릭은 무시
        }
    }
    static string FormatPlayTimeKo(TimeSpan t)
    {
        if (t.TotalSeconds < 1) return "0초";
        var sb = new System.Text.StringBuilder(16);
        sb.Append("플레이 타임 : ");
        if (t.TotalHours >= 1) sb.Append((int)t.TotalHours).Append("시간 ");
        if (t.Minutes > 0) sb.Append(t.Minutes).Append("분 ");
        sb.Append(t.Seconds).Append("초");
        return sb.ToString();
    }
}
