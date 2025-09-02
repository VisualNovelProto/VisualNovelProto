using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogItemView : MonoBehaviour
{
    public Image bg;//�ٹ���
    public TextMeshProUGUI speakerText;//�ι�
    public TextMeshProUGUI bodyText;//���

    [Header("Style (�⺻��)")]
    public float speakerFontSize = 28f;
    public float bodyFontSize = 32f;
    public Color speakerColor = Color.white;
    public Color bodyColor = Color.white;

    public void Bind(ChatLogManager.LogEntry e, bool showNodeId, int zebraIndex)
    {
        if (bg) bg.enabled = (zebraIndex & 1) == 1; // Ȧ¦ �ٹ���

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
            bodyText.text = e.bodyRich; // ��ġ �ؽ�Ʈ ����
        }
    }
}
