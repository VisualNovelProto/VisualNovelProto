using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenu : MonoBehaviour
{
    // ��𼭵� ���� �Ͻ����� ���¸� Ȯ���� �� �ִ� ���� �÷���
    public static bool IsPaused { get; private set; }

    [Header("Root")]
    public GameObject rootPanel;              // ��ü �������� �г�(��Ȱ�� ����)
    public bool useTimeScalePause = true;     // �ʿ� ������ ����
    public bool closeMenuWhenOpenPanels = true; // Glossary/Characters �� �� �޴� �ݱ�

    [Header("Buttons")]
    public Button btnReturn;
    public Button btnSave;
    public Button btnLoad;
    public Button btnGlossary;                // �� ���� �߰�
    public Button btnCharacters;              // �� ���� �߰�
    public Button btnOptions;
    public Button btnMainMenu;
    public Button btnExit;

    [Header("Panels / Viewers")]
    public OptionsPanel optionPanel;
    public GlossaryViewer glossaryViewer;     // �� ���� ����
    public CharacterViewer characterViewer;   // �� ���� ����
    public SaveLoadPanel saveLoadPanel;

    [Header("Databases (optional, ����θ� �ڵ� Ž��)")]
    public GlossaryDatabase glossaryDb;
    public CharacterDatabase characterDb;

    bool paused;

    public void OnClickSaveSlot1()
    {
        SaveLoadManager.Instance?.SaveManual(1);
    }

    public void OnClickLoadSlot1()
    {
        SaveLoadManager.Instance?.LoadManual(1, jumpToNode: true, clearBeforeApply: true);
    }

    void Awake()
    {
        if (rootPanel != null) rootPanel.SetActive(false);

        if (btnReturn != null) btnReturn.onClick.AddListener(Close);
        if (btnSave != null) btnSave.onClick.AddListener(SaveMenu);
        if (btnLoad != null) btnLoad.onClick.AddListener(LoadMenu);
        if (btnGlossary != null) btnGlossary.onClick.AddListener(OpenGlossary);
        if (btnCharacters != null) btnCharacters.onClick.AddListener(OpenCharacters);
        if (btnOptions != null) btnOptions.onClick.AddListener(OpenOptions);
        if (btnMainMenu != null) btnMainMenu.onClick.AddListener(ReturnToMainMenu);
        if (btnExit != null) btnExit.onClick.AddListener(ExitGame);
    }

    public void Toggle()
    {
        // ESC ���
        if (paused) Close();
        else Open();
    }

    void OnDisable()
    {
        if (paused)
        {
            paused = false;
            IsPaused = false;
            if (useTimeScalePause) Time.timeScale = 1f;
        }
    }

    void Open()
    {
        paused = true;
        IsPaused = true;

        if (useTimeScalePause) Time.timeScale = 0f;
        if (rootPanel != null) rootPanel.SetActive(true);
    }

    void Close()
    {
        paused = false;
        IsPaused = false;

        if (useTimeScalePause) Time.timeScale = 1f;
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    void OpenOptions()
    {
        // �ɼ� �г��� ���� ���� ���⼭ SetActive(true)
        if (optionPanel == null)
        {
            Debug.LogWarning("PauseMenu: optionPanel�� ����Ǿ� ���� �ʽ��ϴ�.");
            return;
        }
        if (closeMenuWhenOpenPanels)
            Close();

        optionPanel.Open();
    }
    void SaveMenu()
    {
        // ���߿� ���̺�/�ε� UI ����
        saveLoadPanel.Open(SaveLoadPanel.Mode.Save);
        Debug.Log("Open Load Menu");
    }
    void LoadMenu()
    {
        // ���߿� ���̺�/�ε� UI ����
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
        Debug.Log("Open Load Menu");
    }

    void OpenGlossary()
    {
        // DB�� ��� ������ DialogueUI���� �ڵ� ���� ��������(1ȸ)
        if (glossaryDb == null)
        {
            var ui = FindObjectOfType<DialogueUI>();
            if (ui != null) glossaryDb = ui.glossary;
        }

        if (glossaryViewer == null)
        {
            Debug.LogWarning("PauseMenu: glossaryViewer �� ����Ǿ� ���� �ʽ��ϴ�.");
            return;
        }

        if (closeMenuWhenOpenPanels) Close();
        glossaryViewer.Open(glossaryDb, -1);
    }

    void OpenCharacters()
    {
        if (characterDb == null)
        {
            var ui = FindObjectOfType<DialogueUI>();
            if (ui != null) characterDb = ui.characters;
        }

        if (characterViewer == null)
        {
            Debug.LogWarning("PauseMenu: characterViewer �� ����Ǿ� ���� �ʽ��ϴ�.");
            return;
        }

        if (closeMenuWhenOpenPanels) Close();
        characterViewer.Open(characterDb, -1);
    }

    void ReturnToMainMenu()
    {
        Debug.Log("Go to Main Menu");
        // �� ��ȯ ��
    }

    void ExitGame()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
