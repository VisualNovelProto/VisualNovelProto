using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogItemView : MonoBehaviour
{
    [Header("Text")]
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI bodyText;
    public Graphic zebraBackground;

    [Header("Voice (Optional)")]
    public GameObject voiceButtonRoot;
    public Button voiceButton;

    [Header("Style Defaults")]
    public float speakerFontSize = 28f;
    public float bodyFontSize = 32f;
    public Color speakerColor = Color.white;
    public Color bodyColor = Color.white;

    public void Bind(ChatLogManager.LogEntry entry, bool showNodeId, int zebraIndex,
                     bool showVoiceButton, Action onVoiceClicked)
    {
        if (zebraBackground != null)
            zebraBackground.enabled = (zebraIndex & 1) == 1;

        if (speakerText != null)
        {
            speakerText.fontSize = speakerFontSize;
            speakerText.color = speakerColor;
            if (string.IsNullOrEmpty(entry.speaker))
            {
                speakerText.text = string.Empty;
            }
            else if (showNodeId)
            {
                speakerText.text = $"[{entry.nodeId}] {entry.speaker}";
            }
            else
            {
                speakerText.text = entry.speaker;
            }
        }

        if (bodyText != null)
        {
            bodyText.fontSize = bodyFontSize;
            bodyText.color = bodyColor;
            bodyText.text = entry.bodyRich ?? string.Empty;
        }

        BindVoiceButton(showVoiceButton, onVoiceClicked);
    }

    void BindVoiceButton(bool showVoiceButton, Action onVoiceClicked)
    {
        if (voiceButtonRoot != null)
            voiceButtonRoot.SetActive(showVoiceButton);

        if (voiceButton == null)
            return;

        voiceButton.onClick.RemoveAllListeners();
        bool interactable = showVoiceButton && onVoiceClicked != null;
        voiceButton.interactable = interactable;
        if (interactable)
        {
            voiceButton.onClick.AddListener(() => onVoiceClicked());
        }
    }
}
