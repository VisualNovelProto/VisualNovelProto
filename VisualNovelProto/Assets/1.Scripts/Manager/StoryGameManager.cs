using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class StoryGameManager : MonoBehaviour
{
    [Header("Scene References")]
    public DialogueRunner runner;
    public DialogueUI ui;
    public TransitionManager transition;
    public PauseMenu pauseMenu;
    public GlossaryViewer glossaryViewer;
    public CharacterViewer characterViewer;

    [Header("Resources Paths (CSV)")]
    public string storyPath = "StoryText/Story";             // Resources/StoryText/main.csv
    public string glossaryPath = "StoryText/glossary";  // Resources/StoryText/glossary.csv
    public string charactersPath = "StoryText/characters"; // Resources/StoryText/characters.csv

    [Header("Start")]
    public int startNodeId = 0;

    void Awake()
    {
        // 0) 모달 게이트 초기화
        UiModalGate.Reset();

        // 1) TransitionManager 보장(씬에 1개 권장)
        if (transition == null) transition = FindObjectOfType<TransitionManager>();

        // 2) 데이터 주입(Glossary/Characters)
        if (ui != null)
        {
            if (ui.glossary == null) ui.glossary = GlossaryDatabase.LoadFromResources(glossaryPath);
            if (ui.characters == null) ui.characters = CharacterDatabase.LoadFromResources(charactersPath);
        }

        // 3) 뷰어에 DB 바인딩
        if (characterViewer != null && ui != null && ui.characters != null)
            characterViewer.Bind(ui.characters);

        // 4) PauseMenu 훅(메뉴에서 직접 열기)
        if (pauseMenu != null)
        {
            if (pauseMenu.glossaryDb == null && ui != null) pauseMenu.glossaryDb = ui.glossary;
            if (pauseMenu.characterDb == null && ui != null) pauseMenu.characterDb = ui.characters;
            if (pauseMenu.glossaryViewer == null && glossaryViewer != null) pauseMenu.glossaryViewer = glossaryViewer;
            if (pauseMenu.characterViewer == null && characterViewer != null) pauseMenu.characterViewer = characterViewer;
        }

        // 5) 러너 배선(스토리 CSV/시작 노드/UI)
        if (runner != null)
        {
            if (runner.csv == null && !string.IsNullOrEmpty(storyPath))
                runner.csv = Resources.Load<TextAsset>(storyPath); // 스토리 CSV를 리소스에서

            if (ui != null) runner.ui = ui; // 러너->UI 연결
            runner.startNodeId = startNodeId;

            // 러너 Awake 이후에도 안전하게 UI에 핸들러 묶어두기
            if (ui != null) ui.Bind(runner);
        }
    }
}
