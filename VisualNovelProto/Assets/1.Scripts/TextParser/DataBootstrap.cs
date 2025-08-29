using UnityEngine;

public sealed class DataBootstrap : MonoBehaviour
{
    public DialogueUI ui;                 // Canvas의 DialogueUI 드래그
    public CollectionsPanel collections;  // PauseMenu 안에 있는 CollectionsPanel 드래그

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
            // GlossaryViewer는 기존처럼 열릴 거고,
            // 캐릭터 뷰어에도 DB를 바인딩해둔다.
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
