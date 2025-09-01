using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class CharacterViewer : MonoBehaviour
{
    [Header("Behavior")]
    public bool autoFocusOwnedOnOpen = true;
    public bool autoFocusOwnedOnPageChange = true;

    [Header("Panel Root")]
    public GameObject rootPanel;
    public Button closeButton;
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Header")]
    public TextMeshProUGUI pageText;

    [Header("Detail")]
    public Image thumbImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI abilityNameText;
    public TextMeshProUGUI abilityDescText;
    public TextMeshProUGUI penaltyNameText;
    public TextMeshProUGUI penaltyDescText;

    [Header("List (Fixed Pool)")]
    public Button[] itemButtons = new Button[18];
    public TextMeshProUGUI[] itemLabels = new TextMeshProUGUI[18];

    [Header("Sprite Bindings (optional)")]
    public DialogueUI.SpriteBinding[] thumbBindings;

    [Header("Databases")]
    [HideInInspector] public CharacterDatabase db;
    public CharacterVisibilityDatabase visibilityDb;

    [Header("Paths")]
    public string visibilityDbPath = "StoryText/character_visibility"; // ★ 경로 노출

    int startIndex;
    bool opened;

    void Awake()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        else gameObject.SetActive(false);
        SetDetailUnknown(); // 에디터 텍스트 숨김
    }

    void Update()
    {
        if (!opened) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }

    void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        // ★ 항상 로드 시도(없으면 경고 1회)
        if (visibilityDb == null)
        {
            visibilityDb = CharacterVisibilityDatabase.LoadFromResources(visibilityDbPath);
            if (visibilityDb == null)
                Debug.LogWarning($"CharacterViewer: Resources/{visibilityDbPath}.csv 를 찾지 못해 모든 필드가 즉시 공개됩니다.");
        }
    }

    void OnDisable()
    {
        if (opened) { opened = false; UiModalGate.Pop(); }
    }

    // ========= Public API =========

    public void Bind(CharacterDatabase database)
    {
        db = database;
        // 바인딩 시에도 한 번 로드 시도
        if (visibilityDb == null)
            visibilityDb = CharacterVisibilityDatabase.LoadFromResources(visibilityDbPath);
    }

    public void Open(CharacterDatabase database, int focusId = -1)
    {
        if (database != null) db = database;

        if (rootPanel != null) rootPanel.SetActive(true);
        else gameObject.SetActive(true);

        if (!opened) { UiModalGate.Push(Close); opened = true; }

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

        if (opened) { UiModalGate.Pop(); opened = false; }
    }

    public void NextPage() { if (db == null) return; startIndex = NextStart(startIndex); Refresh(); SetDetailUnknown(); }
    public void PrevPage() { if (db == null) return; startIndex = PrevStart(startIndex); Refresh(); SetDetailUnknown(); }

    // ========= Internal =========

    void Refresh()
    {
        if (db == null) return;

        int cap = itemButtons.Length;
        if (pageText != null)
        {
            int page = Mathf.Clamp((startIndex / cap) + 1, 1, Mathf.Max(1, (db.entryCount + cap - 1) / cap));
            int total = Mathf.Max(1, (db.entryCount + cap - 1) / cap);
            pageText.text = $"{page:00}/{total:00}";
        }

        for (int i = 0; i < cap; i++)
        {
            int id = startIndex + i;
            bool active = (id < db.entryCount);
            if (itemButtons[i] != null) itemButtons[i].gameObject.SetActive(active);
            if (!active) continue;

            int captured = id;
            itemButtons[i].onClick.RemoveAllListeners();
            itemButtons[i].onClick.AddListener(() => ShowDetail(captured));

            bool owned = db.owned.Has(id);
            if (itemLabels[i] != null)
            {
                if (db.TryGetEntry(id, out var e))
                    itemLabels[i].text = owned ? (string.IsNullOrEmpty(e.name) ? "?" : e.name) : "???";
                else
                    itemLabels[i].text = "?";
            }
        }
    }

    void SetDetailUnknown()
    {
        if (titleText) titleText.text = "???";
        if (descText) descText.text = "아직 해금되지 않았습니다.";
        if (abilityNameText) abilityNameText.text = "???";
        if (abilityDescText) abilityDescText.text = "";
        if (penaltyNameText) penaltyNameText.text = "???";
        if (penaltyDescText) penaltyDescText.text = "";
        if (thumbImage) thumbImage.sprite = null;
    }

    void ShowDetail(int id)
    {
        if (db == null || id < 0 || id >= db.entryCount) { SetDetailUnknown(); return; }

        bool owned = db.owned.Has(id);
        if (!db.TryGetEntry(id, out var e)) { SetDetailUnknown(); return; }

        // 가시성 규칙
        CharacterVisibility v = default;
        bool hv = (visibilityDb != null && visibilityDb.TryGet(id, out v));

        // hv==false(행 없음)일 때도 즉시 공개 되도록 0 사용
        int fTitle = hv ? v.titleFlag : 0;
        int fDesc = hv ? v.descFlag : 0;
        int fAbilityName = hv ? v.abilityNameFlag : 0;
        int fAbilityDesc = hv ? v.abilityDescFlag : 0;
        int fPenaltyName = hv ? v.penaltyNameFlag : 0;
        int fPenaltyDesc = hv ? v.penaltyDescFlag : 0;
        int fThumb = hv ? v.thumbFlag : 0;

        // ★ 변경점: "<= 0" 이면 즉시 공개
        bool showTitle = owned && (fTitle <= 0 || StoryFlags.Has(fTitle));
        bool showDesc = owned && (fDesc <= 0 || StoryFlags.Has(fDesc));
        bool showAbilityName = owned && (fAbilityName <= 0 || StoryFlags.Has(fAbilityName));
        bool showAbilityDesc = owned && (fAbilityDesc <= 0 || StoryFlags.Has(fAbilityDesc));
        bool showPenaltyName = owned && (fPenaltyName <= 0 || StoryFlags.Has(fPenaltyName));
        bool showPenaltyDesc = owned && (fPenaltyDesc <= 0 || StoryFlags.Has(fPenaltyDesc));
        bool showThumb = owned && (fThumb <= 0 || StoryFlags.Has(fThumb));

        if (titleText) titleText.text = showTitle ? (e.name ?? "?") : "???";
        if (descText) descText.text = showDesc ? (e.desc ?? "") : "아직 해금되지 않았습니다.";
        if (abilityNameText) abilityNameText.text = showAbilityName ? (e.abilityName ?? "?") : "???";
        if (abilityDescText) abilityDescText.text = showAbilityDesc ? (e.abilityDesc ?? "") : "";
        if (penaltyNameText) penaltyNameText.text = showPenaltyName ? (e.penaltyName ?? "?") : "???";
        if (penaltyDescText) penaltyDescText.text = showPenaltyDesc ? (e.penaltyDesc ?? "") : "";

        if (thumbImage)
        {
            if (showThumb && !string.IsNullOrEmpty(e.thumb))
                thumbImage.sprite = FindSprite(thumbBindings, e.thumb);
            else
                thumbImage.sprite = null;
        }
    }
    int FindFirstOwnedOnPage()
    {
        if (db == null) return -1;
        int cap = itemButtons.Length;
        for (int i = 0; i < cap; i++)
        {
            int id = startIndex + i;
            if (id >= db.entryCount) break;
            if (db.owned.Has(id)) return id;
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

    int NextStart(int s) { int cap = itemButtons.Length; s += cap; if (s >= db.entryCount) s = 0; return s; }
    int PrevStart(int s) { int cap = itemButtons.Length; s -= cap; if (s < 0) s = ((db.entryCount - 1) / cap) * cap; return s; }
}
