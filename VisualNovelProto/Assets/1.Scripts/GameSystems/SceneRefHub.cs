using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SceneRefHub : MonoBehaviour
{
    [Header("Story/Log/UI in this scene")]
    public DialogueUI dialogueUI;
    public DialogueRunner dialogueRunner;
    public PauseMenu pauseMenu;
    public LogViewerList logViewer;
    public EventSystem eventSystem;

    [Header("Optional panels in this scene")]
    public OptionsPanel optionsPanel;
    public CollectionsPanel collectionsPanel;
    public GlossaryViewer glossaryViewer;
    public CharacterViewer characterViewer;

    void Awake()
    {
        PropagateSceneBindings();
    }

    void OnEnable()
    {
        PropagateSceneBindings();
    }

    void OnDisable()
    {
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.runner == dialogueRunner)
            SaveLoadManager.Instance.ApplySceneBindings(null);

        if (TimeLoopManager.Instance != null && TimeLoopManager.Instance.Runner == dialogueRunner)
            TimeLoopManager.Instance.ApplySceneBindings(null);
    }

    void PropagateSceneBindings()
    {
        if (SaveLoadManager.Instance != null)
            SaveLoadManager.Instance.ApplySceneBindings(this);

        if (TimeLoopManager.Instance != null)
            TimeLoopManager.Instance.ApplySceneBindings(this);
    }
}
