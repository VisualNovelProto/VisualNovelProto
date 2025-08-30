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
        // 0) ��� ����Ʈ �ʱ�ȭ
        UiModalGate.Reset();

        // 1) TransitionManager ����(���� 1�� ����)
        if (transition == null) transition = FindObjectOfType<TransitionManager>();

        // 2) ������ ����(Glossary/Characters)
        if (ui != null)
        {
            if (ui.glossary == null) ui.glossary = GlossaryDatabase.LoadFromResources(glossaryPath);
            if (ui.characters == null) ui.characters = CharacterDatabase.LoadFromResources(charactersPath);
        }

        // 3) �� DB ���ε�
        if (characterViewer != null && ui != null && ui.characters != null)
            characterViewer.Bind(ui.characters);

        // 4) PauseMenu ��(�޴����� ���� ����)
        if (pauseMenu != null)
        {
            if (pauseMenu.glossaryDb == null && ui != null) pauseMenu.glossaryDb = ui.glossary;
            if (pauseMenu.characterDb == null && ui != null) pauseMenu.characterDb = ui.characters;
            if (pauseMenu.glossaryViewer == null && glossaryViewer != null) pauseMenu.glossaryViewer = glossaryViewer;
            if (pauseMenu.characterViewer == null && characterViewer != null) pauseMenu.characterViewer = characterViewer;
        }

        // 5) ���� �輱(���丮 CSV/���� ���/UI)
        if (runner != null)
        {
            if (runner.csv == null && !string.IsNullOrEmpty(storyPath))
                runner.csv = Resources.Load<TextAsset>(storyPath); // ���丮 CSV�� ���ҽ�����

            if (ui != null) runner.ui = ui; // ����->UI ����
            runner.startNodeId = startNodeId;

            // ���� Awake ���Ŀ��� �����ϰ� UI�� �ڵ鷯 ����α�
            if (ui != null) ui.Bind(runner);
        }
    }
}
