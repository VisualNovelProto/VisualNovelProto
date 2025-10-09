using UnityEngine;

public sealed class GlossaryService : MonoBehaviour
{
    public static GlossaryDatabase Instance { get; private set; }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = GlossaryDatabase.LoadFromResources("StoryText/glossary");
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
