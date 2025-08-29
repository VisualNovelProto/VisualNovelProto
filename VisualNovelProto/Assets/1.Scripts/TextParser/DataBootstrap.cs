using UnityEngine;

public sealed class DataBootstrap : MonoBehaviour
{
    public DialogueUI ui;                 // Canvas�� DialogueUI �巡��
    public CollectionsPanel collections;  // PauseMenu �ȿ� �ִ� CollectionsPanel �巡��

    public string glossaryPath = "StoryText/glossary";   // Resources/<path>.csv
    public string charactersPath = "StoryText/characters"; // Resources/<path>.csv

    void Awake()
    {
        if (ui != null)
        {
            if (ui.glossary == null)
                ui.glossary = GlossaryDatabase.LoadFromResources(glossaryPath);

            if (ui.characters == null)
                ui.characters = CharacterDatabase.LoadFromResources(charactersPath);
        }

        if (collections != null)
        {
            // GlossaryViewer�� ����ó�� ���� �Ű�,
            // ĳ���� ���� DB�� ���ε��صд�.
            if (collections.characterViewer != null)
            {
                var cdb = (ui != null && ui.characters != null)
                          ? ui.characters
                          : CharacterDatabase.LoadFromResources(charactersPath);
                collections.characterViewer.Bind(cdb);
            }
        }
    }
}
