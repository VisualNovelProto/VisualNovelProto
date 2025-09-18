using UnityEngine;

public sealed class DataBootstrap : MonoBehaviour
{
    public DialogueUI ui;                 // Canvas의 DialogueUI 드래그
    public CollectionsPanel collections;  // PauseMenu 안에 있는 CollectionsPanel 드래그

    public string glossaryPath = "StoryText/glossary";   // Resources/<path>.csv
    public string charactersPath = "StoryText/characters"; // Resources/<path>.csv

    void Awake()
    {
        var root = GameRoot.Instance;

        if (ui != null)
        {
            if (ui.characters == null)
                ui.characters = root ? root.characterDb : CharacterDatabase.LoadFromResources(charactersPath);
            if (ui.glossary == null)
                ui.glossary = root ? root.glossaryDb : GlossaryDatabase.LoadFromResources(glossaryPath);

        }
        if (collections != null && collections.characterViewer != null)
        {
            var cdb = root ? root.characterDb
                           : (ui != null ? ui.characters : CharacterDatabase.LoadFromResources(charactersPath));
            collections.characterViewer.Bind(cdb);
        }

        if (collections != null && collections.characterViewer != null)
        {
            collections.characterViewer.Bind(root != null ? root.characterDb : ui.characters);
        }
        //if (collections != null && collections.glossaryViewer != null)
        //{
        //    var gdb = root ? root.glossaryDb
        //                   : (ui != null ? ui.glossary : GlossaryDatabase.LoadFromResources(glossaryPath));
        //    collections.glossaryViewer.Bind(gdb);
        //}
    }
}
