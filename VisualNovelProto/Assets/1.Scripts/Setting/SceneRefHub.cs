using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SceneRefHub : MonoBehaviour
{
    [Header("Story/Log/UI in this scene")]
    public DialogueUI dialogueUI;
    public DialogueRunner dialogueRunner;
    public PauseMenu pauseMenu;
    public LogViewerList logViewer;
    public EventSystem eventSystem; // �� ���� ����
    // �ʿ��: OptionsPanel, CollectionsPanel, GlossaryViewer, CharacterViewer � �߰�
}
