using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConfirmDialog : MonoBehaviour
{
    public GameObject root;
    public TMP_Text message;
    public Button btnYes;
    public Button btnNo;

    System.Action _onYes;

    void Awake()
    {
        if (!root) root = gameObject;
        btnYes.onClick.AddListener(() => { _onYes?.Invoke(); Close(); });
        btnNo.onClick.AddListener(Close);
        root.SetActive(false);
    }

    public void Open(string msg, System.Action onYes)
    {
        if (message) message.text = msg;
        _onYes = onYes;
        root.SetActive(true);
        UiModalGate.Push(Close);
        InputRouter.Instance?.SuppressAdvance(0.12f);
    }

    public void Close()
    {
        root.SetActive(false);
        UiModalGate.Pop();
        InputRouter.Instance?.SuppressAdvance(0.12f);
    }
}
