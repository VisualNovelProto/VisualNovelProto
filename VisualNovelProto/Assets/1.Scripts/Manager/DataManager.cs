using UnityEngine;

public sealed class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("Resources Paths")]
    public string glossaryPath = "StoryText/glossary";
    public string charactersPath = "StoryText/characters";
    public string characterVisibilityPath = "StoryText/characterVisibility";

    [Header("Caches")]
    public GlossaryDatabase glossary;
    public CharacterDatabase characters;
    public CharacterVisibilityDatabase visibility;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadIfNeeded();
    }

    public void LoadIfNeeded()
    {
        if (glossary == null) glossary = GlossaryDatabase.LoadFromResources(glossaryPath);
        if (characters == null) characters = CharacterDatabase.LoadFromResources(charactersPath);
        if (visibility == null) visibility = CharacterVisibilityDatabase.LoadFromResources(characterVisibilityPath);
    }
}
