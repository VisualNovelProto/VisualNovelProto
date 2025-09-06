using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Root")]
    [SerializeField] GameObject rootPanel;                 // ���� �ڱ� �ڽ� ���
    [SerializeField] bool useTimeScalePause = true;
    [SerializeField] bool closeMenuWhenOpenPanels = true;
    [SerializeField] KeyCode toggleKey = KeyCode.Escape;

    [Header("Buttons")]
    [SerializeField] Button btnLoad;
    [SerializeField] Button btnCollections;
    [SerializeField] Button btnOptions;
    [SerializeField] Button btnExit;

    [Header("Panels")]
    [SerializeField] SaveLoadPanel saveLoadPanel;          // Load ����
    [SerializeField] OptionsPanel optionPanel;             // �ɼ� �г�

    [Header("Collections (�� �� �ϳ��� ����)")]
    [SerializeField] CollectionsPanel collectionsPanel;    // ��� �г� ���(����)
    [SerializeField] CharacterViewer characterViewer;      // CharacterSheet ���� ����(����)
    [SerializeField] CharacterDatabase characterDb;        // CharacterViewer ��� ��

    bool paused;

    void Awake()
    {
        if (rootPanel == null) rootPanel = gameObject;
        if (rootPanel.activeSelf) rootPanel.SetActive(false);

        if (btnLoad != null) btnLoad.onClick.AddListener(LoadMenu);
        if (btnCollections != null) btnCollections.onClick.AddListener(OpenCollections);
        if (btnOptions != null) btnOptions.onClick.AddListener(OpenOptions);
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);

        // ���� ���� �ڵ� Ž��(������ ���)
        if (saveLoadPanel == null) saveLoadPanel = FindObjectOfType<SaveLoadPanel>(true);
        if (optionPanel == null) optionPanel = FindObjectOfType<OptionsPanel>(true);

        // Character DB �ڵ� ����
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
        if (saveLoadPanel == null) { Debug.LogWarning("LobbyMenu: saveLoadPanel�� ����Ǿ� ���� �ʽ��ϴ�."); return; }
        if (closeMenuWhenOpenPanels) Close();
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
    }

    void OpenCollections()
    {
        // 1) CollectionsPanel�� ������ �װͺ���
        if (collectionsPanel != null)
        {
            if (closeMenuWhenOpenPanels) Close();
            collectionsPanel.Open();
            return;
        }
        // 2) ������ CharacterViewer(=CharacterSheet) ���� ����
        if (characterViewer != null)
        {
            if (characterDb == null) { Debug.LogWarning("LobbyMenu: characterDb�� �������� �ʾҽ��ϴ�."); return; }
            if (closeMenuWhenOpenPanels) Close();
            characterViewer.Open(characterDb, -1);
            return;
        }

        Debug.LogWarning("LobbyMenu: Collections ����� �����ϴ�. (CollectionsPanel �Ǵ� CharacterViewer ���� �ʿ�)");
    }

    void OpenOptions()
    {
        if (optionPanel == null) { Debug.LogWarning("LobbyMenu: optionPanel�� ����Ǿ� ���� �ʽ��ϴ�."); return; }
        if (closeMenuWhenOpenPanels) Close();
        optionPanel.Open();
    }

    void ExitGame()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
