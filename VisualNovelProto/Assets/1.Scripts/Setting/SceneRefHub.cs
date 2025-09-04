using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SceneRefHub : MonoBehaviour
{
    [Header("Story/Log/UI in this scene")]
    public DialogueUI dialogueUI;
    public DialogueRunner dialogueRunner;
    public PauseMenu pauseMenu;
    public LogViewerList logViewer;
    public EventSystem eventSystem; // 씬 로컬 권장
    // 필요시: OptionsPanel, CollectionsPanel, GlossaryViewer, CharacterViewer 등도 추가
}
