using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogViewer : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;           // ��Ʈ �г�(SetActive on/off)
    public TextMeshProUGUI content;    // �α� ǥ�ÿ� �ؽ�Ʈ
    public Button openButton;
    public Button closeButton;

    [Header("Options")]
    public int linesToShow = 50;       // �ֱ� �� ��
    public bool showNodeId = false;

    // ���� ����(�������� �ּ�ȭ)
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
        UiModalGate.Push(Close); // ��� ����. :contentReference[oaicite:2]{index=2}
        Rebuild();
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
        UiModalGate.Pop();  // ��� ����. :contentReference[oaicite:3]{index=3}
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
            // ����Ŀ�� ��� ������ ������
            if (!string.IsNullOrEmpty(e.speaker))
            {
                if (showNodeId) sb.Append('[').Append(e.nodeId).Append("] ");
                sb.Append("<b>").Append(e.speaker).Append("</b>\n");
            }
            sb.Append(e.bodyRich).Append("\n\n"); // ��ũ/�� ���� ��ġ �ؽ�Ʈ �״��
        }

        content.text = sb.ToString();
        content.ForceMeshUpdate();
    }
}
