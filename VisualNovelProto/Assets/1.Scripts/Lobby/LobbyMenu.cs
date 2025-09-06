using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Root")]
    [SerializeField] GameObject rootPanel;                 // 비우면 자기 자신 사용
    [SerializeField] bool useTimeScalePause = true;
    [SerializeField] bool closeMenuWhenOpenPanels = true;
    [SerializeField] KeyCode toggleKey = KeyCode.Escape;

    [Header("Buttons")]
    [SerializeField] Button btnLoad;
    [SerializeField] Button btnCollections;
    [SerializeField] Button btnOptions;
    [SerializeField] Button btnExit;

    [Header("Panels")]
    [SerializeField] SaveLoadPanel saveLoadPanel;          // Load 전용
    [SerializeField] OptionsPanel optionPanel;             // 옵션 패널

    [Header("Collections (둘 중 하나만 연결)")]
    [SerializeField] CollectionsPanel collectionsPanel;    // 허브 패널 방식(선택)
    [SerializeField] CharacterViewer characterViewer;      // CharacterSheet 직접 열기(선택)
    [SerializeField] CharacterDatabase characterDb;        // CharacterViewer 사용 시

    bool paused;

    void Awake()
    {
        if (rootPanel == null) rootPanel = gameObject;
        if (rootPanel.activeSelf) rootPanel.SetActive(false);

        if (btnLoad != null) btnLoad.onClick.AddListener(LoadMenu);
        if (btnCollections != null) btnCollections.onClick.AddListener(OpenCollections);
        if (btnOptions != null) btnOptions.onClick.AddListener(OpenOptions);
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);

        // 누락 참조 자동 탐색(있으면 사용)
        if (saveLoadPanel == null) saveLoadPanel = FindObjectOfType<SaveLoadPanel>(true);
        if (optionPanel == null) optionPanel = FindObjectOfType<OptionsPanel>(true);

        // Character DB 자동 참조
        if (characterDb == null)
        {
            var ui = FindObjectOfType<DialogueUI>(true);
            if (ui != null) characterDb = ui.characters;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }

    public void Toggle() { if (paused) Close(); else Open(); }

    void OnDisable()
    {
        if (paused) { paused = false; IsPaused = false; if (useTimeScalePause) Time.timeScale = 1f; }
    }

    void Open()
    {
        paused = true; IsPaused = true;
        if (useTimeScalePause) Time.timeScale = 0f;
        if (rootPanel) rootPanel.SetActive(true);
    }

    void Close()
    {
        paused = false; IsPaused = false;
        if (useTimeScalePause) Time.timeScale = 1f;
        if (rootPanel) rootPanel.SetActive(false);
    }

    void LoadMenu()
    {
        if (saveLoadPanel == null) { Debug.LogWarning("LobbyMenu: saveLoadPanel이 연결되어 있지 않습니다."); return; }
        if (closeMenuWhenOpenPanels) Close();
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
    }

    void OpenCollections()
    {
        // 1) CollectionsPanel이 있으면 그것부터
        if (collectionsPanel != null)
        {
            if (closeMenuWhenOpenPanels) Close();
            collectionsPanel.Open();
            return;
        }
        // 2) 없으면 CharacterViewer(=CharacterSheet) 직접 열기
        if (characterViewer != null)
        {
            if (characterDb == null) { Debug.LogWarning("LobbyMenu: characterDb가 설정되지 않았습니다."); return; }
            if (closeMenuWhenOpenPanels) Close();
            characterViewer.Open(characterDb, -1);
            return;
        }

        Debug.LogWarning("LobbyMenu: Collections 대상이 없습니다. (CollectionsPanel 또는 CharacterViewer 연결 필요)");
    }

    void OpenOptions()
    {
        if (optionPanel == null) { Debug.LogWarning("LobbyMenu: optionPanel이 연결되어 있지 않습니다."); return; }
        if (closeMenuWhenOpenPanels) Close();
        optionPanel.Open();
    }

    void ExitGame()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
