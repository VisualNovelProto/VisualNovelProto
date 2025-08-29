using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GlossaryViewer : MonoBehaviour
{
    [Header("Behavior")]
    public bool autoFocusOwnedOnOpen = true;
    public bool autoFocusOwnedOnPageChange = true;

    [Header("Panel Root")]
    public GameObject rootPanel;          // 패널 최상단 오브젝트(없으면 자기 자신 사용)
    public Button closeButton;            // 'Return' 버튼(선택)
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Header")]
    public TextMeshProUGUI pageText;     // "03/18"와 같은 페이지 정보
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
    bool opened;                          // 중복 Push/Pop 방지

    void Awake()
    {
        // 시작 시 항상 비활성화(에디터에서 켜져 있어도 꺼짐)
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
        // 강제 종료 상황에서도 게이트 복구
        if (opened)
        {
            opened = false;
            UiModalGate.Pop();
        }
    }

    // ====== 공개 API ======
    public void Open(GlossaryDatabase db, int focusId = -1)
    {
        gdb = db;
        if (rootPanel != null) rootPanel.SetActive(true);
        else gameObject.SetActive(true);

        if (!opened) { UiModalGate.Push(); opened = true; }

        // 포커스 아이템 기준으로 페이지 시작 정하기
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

        if (opened) { UiModalGate.Pop(); opened = false; }    // ★ 스토리 입력 해제
    }

    void Update()
    {
        // ESC로 닫기
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

            int captured = id; // 클로저
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
        if (descText != null) descText.text = owned ? e.desc : "수집되지 않은 단어입니다.";

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
        if (descText) descText.text = "아직 해금되지 않았습니다.";
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
