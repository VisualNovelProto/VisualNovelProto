using UnityEngine;

public sealed class DataBootstrap : MonoBehaviour
{
    [Header("Scene Bindings")]
    public DialogueUI ui;
    public CollectionsPanel collections;

    [Header("CSV Resources")]
    public string glossaryPath = "StoryText/glossary";
    public string charactersPath = "StoryText/characters";

    void Awake()
    {
        var root = GameRoot.Instance;
        var characterDb = ResolveCharacterDatabase(root);
        var glossaryDb = ResolveGlossaryDatabase(root);

        if (ui != null)
        {
            if (ui.characters == null)
                ui.characters = characterDb;
            if (ui.glossary == null)
                ui.glossary = glossaryDb;
        }

        if (collections != null)
        {
            if (collections.characterViewer != null && characterDb != null)
                collections.characterViewer.Bind(characterDb);

            if (collections.glossaryViewer != null && glossaryDb != null)
                collections.glossaryViewer.gdb = glossaryDb;
        }
    }

    CharacterDatabase ResolveCharacterDatabase(GameRoot root)
    {
        if (root && root.characterDb != null)
            return root.characterDb;

        if (ui != null && ui.characters != null)
            return ui.characters;

        return CharacterDatabase.LoadFromResources(charactersPath);
    }

    GlossaryDatabase ResolveGlossaryDatabase(GameRoot root)
    {
        if (root && root.glossaryDb != null)
            return root.glossaryDb;

        if (ui != null && ui.glossary != null)
            return ui.glossary;

        return GlossaryDatabase.LoadFromResources(glossaryPath);
    }
}
