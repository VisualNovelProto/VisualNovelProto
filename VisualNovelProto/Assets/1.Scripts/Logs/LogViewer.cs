using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Legacy compatibility wrapper that routes button events to the new LogViewerList UI.
/// </summary>
public sealed class LogViewer : MonoBehaviour
{
    [Header("Compatibility")]
    public LogViewerList list;
    public GameObject panel;
    public Button openButton;
    public Button closeButton;

    void Awake()
    {
        EnsureList();

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Open()
    {
        EnsureList();
        list?.Open();
    }

    public void Close()
    {
        EnsureList();
        list?.Close();
    }

    void EnsureList()
    {
        if (list)
            return;

        if (panel)
            list = panel.GetComponent<LogViewerList>();

        if (!list)
            list = FindObjectOfType<LogViewerList>();
    }
}
