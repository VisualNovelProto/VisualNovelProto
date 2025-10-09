using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogItemView : MonoBehaviour
{
    public Image bg;//줄무늬
    public TextMeshProUGUI speakerText;//인물
    public TextMeshProUGUI bodyText;//대사

    [Header("Style (기본값)")]
    public float speakerFontSize = 28f;
    public float bodyFontSize = 32f;
    public Color speakerColor = Color.white;
    public Color bodyColor = Color.white;

    public void Bind(ChatLogManager.LogEntry e, bool showNodeId, int zebraIndex)
    {
        if (bg) bg.enabled = (zebraIndex & 1) == 1; // 홀짝 줄무늬

        if (speakerText)
        {
            speakerText.fontSize = speakerFontSize;
            speakerText.color = speakerColor;
            speakerText.text = string.IsNullOrEmpty(e.speaker)
                ? ""
                : (showNodeId ? $"[{e.nodeId}] {e.speaker}" : e.speaker);
        }

        if (bodyText)
        {
            bodyText.fontSize = bodyFontSize;
            bodyText.color = bodyColor;
            bodyText.text = e.bodyRich; // 리치 텍스트 유지
        }
    }
}
