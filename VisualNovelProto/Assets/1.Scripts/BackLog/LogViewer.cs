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
    [SerializeField] PanelAnimator animator;
    bool opened;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (openButton != null) { openButton.onClick.RemoveAllListeners(); openButton.onClick.AddListener(Open); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Close); }
        tmp = new ChatLogManager.LogEntry[Mathf.Max(8, linesToShow)];

        if (!animator && panel) animator = panel.GetComponent<PanelAnimator>();
    }

    public void Open()
    {
        if (panel != null)
            panel.SetActive(true);
        if (!opened) { UiModalGate.Push(Close); opened = true; }
        animator?.PlayOpen();
        Rebuild();
    }

    public void Close()
    {
        StartCoroutine(CoClose());
    }
    System.Collections.IEnumerator CoClose()
    {
        if (animator) yield return animator.PlayClose();          // ★ 애니 종료까지 대기
        if (opened) { UiModalGate.Pop(); opened = false; }        // ★ Pop (짝 맞추기)
        if (panel) panel.SetActive(false);
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
