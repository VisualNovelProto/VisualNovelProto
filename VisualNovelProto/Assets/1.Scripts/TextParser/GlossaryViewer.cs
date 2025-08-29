using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GlossaryViewer : MonoBehaviour
{
    [Header("Behavior")]
    public bool autoFocusOwnedOnOpen = true;
    public bool autoFocusOwnedOnPageChange = true;

    [Header("Panel Root")]
    public GameObject rootPanel;          // �г� �ֻ�� ������Ʈ(������ �ڱ� �ڽ� ���)
    public Button closeButton;            // 'Return' ��ư(����)
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Header")]
    public TextMeshProUGUI pageText;     // "03/18"�� ���� ������ ����
    public Image thumbImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;

    [Header("List (Fixed Pool)")]
    public Button[] itemButtons = new Button[18];
    public TextMeshProUGUI[] itemLabels = new TextMeshProUGUI[18];

    [Header("Sprite Bindings")]
    public DialogueUI.SpriteBinding[] thumbBindings;

    int startIndex;
    public GlossaryDatabase gdb;
    bool opened;                          // �ߺ� Push/Pop ����

    void Awake()
    {
        // ���� �� �׻� ��Ȱ��ȭ(�����Ϳ��� ���� �־ ����)
        if (rootPanel != null) rootPanel.SetActive(false);
        else gameObject.SetActive(false);
        SetDetailUnknown();
    }

    void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    void OnDisable()
    {
        // ���� ���� ��Ȳ������ ����Ʈ ����
        if (opened)
        {
            opened = false;
            UiModalGate.Pop();
        }
    }

    // ====== ���� API ======
    public void Open(GlossaryDatabase db, int focusId = -1)
    {
        gdb = db;
        if (rootPanel != null) rootPanel.SetActive(true);
        else gameObject.SetActive(true);

        if (!opened) { UiModalGate.Push(); opened = true; }

        // ��Ŀ�� ������ �������� ������ ���� ���ϱ�
        startIndex = 0;
        if (focusId >= 0)
        {
            int row = focusId / itemButtons.Length;
            startIndex = row * itemButtons.Length;
        }
        Refresh();
        if (focusId >= 0)
        {
            ShowDetail(focusId);
        }
        else
        {
            int autoId = autoFocusOwnedOnOpen ? FindFirstOwnedOnPage() : -1;
            if (autoId >= 0) ShowDetail(autoId);
            else SetDetailUnknown();
        }
    }

    public void Close()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        else gameObject.SetActive(false);

        if (opened) { UiModalGate.Pop(); opened = false; }    // �� ���丮 �Է� ����
    }

    void Update()
    {
        // ESC�� �ݱ�
        if (!opened) return;
        if (Input.GetKeyDown(closeKey)) Close();
    }

    public void NextPage()
    {
        if (gdb == null) return;
        int cap = itemButtons.Length;
        startIndex += cap;
        if (startIndex >= gdb.entryCount) startIndex = 0;
        Refresh();

        if (autoFocusOwnedOnPageChange)
        {
            int autoId = FindFirstOwnedOnPage();
            if (autoId >= 0) { ShowDetail(autoId); return; }
        }
        SetDetailUnknown();
    }
    public void PrevPage()
    {
        if (gdb == null) return;
        int cap = itemButtons.Length;
        startIndex -= cap;
        if (startIndex < 0) startIndex = ((gdb.entryCount - 1) / cap) * cap;
        Refresh();

        if (autoFocusOwnedOnPageChange)
        {
            int autoId = FindFirstOwnedOnPage();
            if (autoId >= 0) { ShowDetail(autoId); return; }
        }
        SetDetailUnknown();
    }

    void Refresh()
    {
        if (gdb == null) return;
        int cap = itemButtons.Length;
        int end = startIndex + cap;
        if (pageText != null) pageText.text = $"{Mathf.Clamp(startIndex + 1, 1, gdb.entryCount):00}/{gdb.entryCount:00}";

        for (int i = 0; i < cap; i++)
        {
            int id = startIndex + i;
            bool active = (id < gdb.entryCount);
            itemButtons[i].gameObject.SetActive(active);
            if (!active) continue;

            int captured = id; // Ŭ����
            itemButtons[i].onClick.RemoveAllListeners();
            itemButtons[i].onClick.AddListener(() => ShowDetail(captured));

            ref GlossaryEntry e = ref gdb.entries[id];
            bool owned = gdb.owned.Has(id);

            if (itemLabels[i] != null)
                itemLabels[i].text = owned ? e.label : "???";
        }
    }

    void ShowDetail(int id)
    {
        if (gdb == null || id < 0 || id >= gdb.entryCount) return;
        ref GlossaryEntry e = ref gdb.entries[id];
        bool owned = gdb.owned.Has(id);

        if (titleText != null) titleText.text = owned ? e.label : "???";
        if (descText != null) descText.text = owned ? e.desc : "�������� ���� �ܾ��Դϴ�.";

        if (thumbImage != null)
        {
            if (owned && !string.IsNullOrEmpty(e.thumb))
                thumbImage.sprite = FindSprite(thumbBindings, e.thumb);
            else
                thumbImage.sprite = null;
        }
    }
    void SetDetailUnknown()
    {
        if (titleText) titleText.text = "???";
        if (descText) descText.text = "���� �رݵ��� �ʾҽ��ϴ�.";
    }

    int FindFirstOwnedOnPage()
    {
        if (gdb == null) return -1;
        int cap = itemButtons.Length;
        for (int i = 0; i < cap; i++)
        {
            int id = startIndex + i;
            if (id >= gdb.entryCount) break;
            if (gdb.owned.Has(id)) return id;
        }
        return -1;
    }

    Sprite FindSprite(DialogueUI.SpriteBinding[] arr, string key)
    {
        if (arr == null) return null;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].sprite != null && arr[i].key == key) return arr[i].sprite;
        return null;
    }
}
