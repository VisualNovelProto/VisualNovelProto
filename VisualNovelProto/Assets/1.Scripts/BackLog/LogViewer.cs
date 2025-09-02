using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogViewer : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;           // 루트 패널(SetActive on/off)
    public TextMeshProUGUI content;    // 로그 표시용 텍스트
    public Button openButton;
    public Button closeButton;

    [Header("Options")]
    public int linesToShow = 50;       // 최근 몇 줄
    public bool showNodeId = false;

    // 재사용 버퍼(동적생성 최소화)
    ChatLogManager.LogEntry[] tmp;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (openButton != null) { openButton.onClick.RemoveAllListeners(); openButton.onClick.AddListener(Open); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Close); }
        tmp = new ChatLogManager.LogEntry[Mathf.Max(8, linesToShow)];
    }

    public void Open()
    {
        if (panel != null)
            panel.SetActive(true);
        UiModalGate.Push(Close); // 모달 열림. :contentReference[oaicite:2]{index=2}
        Rebuild();
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
        UiModalGate.Pop();  // 모달 닫힘. :contentReference[oaicite:3]{index=3}
    }

    public void Rebuild()
    {
        if (content == null) return;
        var lm = ChatLogManager.Instance;
        if (lm == null) { content.text = "(No Log)"; return; }

        if (tmp.Length < linesToShow) tmp = new ChatLogManager.LogEntry[linesToShow];

        int n = lm.CopyLatest(tmp, linesToShow);
        var sb = new StringBuilder(4096);

        for (int i = 0; i < n; i++)
        {
            var e = tmp[i];
            // 스피커가 비어 있으면 본문만
            if (!string.IsNullOrEmpty(e.speaker))
            {
                if (showNodeId) sb.Append('[').Append(e.nodeId).Append("] ");
                sb.Append("<b>").Append(e.speaker).Append("</b>\n");
            }
            sb.Append(e.bodyRich).Append("\n\n"); // 링크/색 포함 리치 텍스트 그대로
        }

        content.text = sb.ToString();
        content.ForceMeshUpdate();
    }
}
