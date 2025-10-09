using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenu : MonoBehaviour
{
    // 어디서든 현재 일시정지 상태를 확인할 수 있는 전역 플래그
    public static bool IsPaused { get; private set; }

    [Header("Root")]
    public GameObject rootPanel;              // 전체 오버레이 패널(비활성 시작)
    public bool useTimeScalePause = true;     // 필요 없으면 끄기
    public bool closeMenuWhenOpenPanels = true; // Glossary/Characters 열 때 메뉴 닫기

    [Header("Buttons")]
    public Button btnReturn;
    public Button btnSave;
    public Button btnLoad;
    public Button btnGlossary;                // ★ 새로 추가
    public Button btnCharacters;              // ★ 새로 추가
    public Button btnOptions;
    public Button btnMainMenu;
    public Button btnExit;

    [Header("Panels / Viewers")]
    public OptionsPanel optionPanel;
    public GlossaryViewer glossaryViewer;     // ★ 직접 열기
    public CharacterViewer characterViewer;   // ★ 직접 열기
    public SaveLoadPanel saveLoadPanel;

    [Header("Databases (optional, 비워두면 자동 탐색)")]
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
        // ESC 토글
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
        // 옵션 패널을 따로 쓰면 여기서 SetActive(true)
        if (optionPanel == null)
        {
            Debug.LogWarning("PauseMenu: optionPanel이 연결되어 있지 않습니다.");
            return;
        }
        if (closeMenuWhenOpenPanels)
            Close();

        optionPanel.Open();
    }
    void SaveMenu()
    {
        // 나중에 세이브/로드 UI 연결
        saveLoadPanel.Open(SaveLoadPanel.Mode.Save);
        Debug.Log("Open Load Menu");
    }
    void LoadMenu()
    {
        // 나중에 세이브/로드 UI 연결
        saveLoadPanel.Open(SaveLoadPanel.Mode.Load);
        Debug.Log("Open Load Menu");
    }

    void OpenGlossary()
    {
        // DB가 비어 있으면 DialogueUI에서 자동 참조 가져오기(1회)
        if (glossaryDb == null)
        {
            var ui = FindObjectOfType<DialogueUI>();
            if (ui != null) glossaryDb = ui.glossary;
        }

        if (glossaryViewer == null)
        {
            Debug.LogWarning("PauseMenu: glossaryViewer 가 연결되어 있지 않습니다.");
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
            Debug.LogWarning("PauseMenu: characterViewer 가 연결되어 있지 않습니다.");
            return;
        }

        if (closeMenuWhenOpenPanels) Close();
        characterViewer.Open(characterDb, -1);
    }

    void ReturnToMainMenu()
    {
        Debug.Log("Go to Main Menu");
        // 씬 전환 등
    }

    void ExitGame()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}
