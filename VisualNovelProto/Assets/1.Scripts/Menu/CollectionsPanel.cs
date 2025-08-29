using UnityEngine;
using UnityEngine.UI;

public sealed class CollectionsPanel : MonoBehaviour
{
    public GameObject rootPanel;      // �÷��� ��ü �г�
    public Button tabGlossary;
    public Button tabCharacters;

    public GlossaryViewer glossaryViewer;   // �̹� ����� �� ���
    public CharacterViewer characterViewer; // �Ʒ� 4) ����

    void Awake()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        if (tabGlossary != null) tabGlossary.onClick.AddListener(ShowGlossary);
        if (tabCharacters != null) tabCharacters.onClick.AddListener(ShowCharacters);
    }

    public void Open()
    {
        if (rootPanel != null) rootPanel.SetActive(true);
        ShowGlossary();
    }

    public void Close()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        if (glossaryViewer != null) glossaryViewer.Close();
        if (characterViewer != null) characterViewer.Close();
    }

    void ShowGlossary()
    {
        if (glossaryViewer != null)
        {
            glossaryViewer.gameObject.SetActive(true);
            glossaryViewer.Open(glossaryViewer.gdb, -1); // ���� ���ε��� DB ���
        }
        if (characterViewer != null)
            characterViewer.gameObject.SetActive(false);
    }

    void ShowCharacters()
    {
        if (characterViewer != null)
        {
            characterViewer.gameObject.SetActive(true);
            characterViewer.Open(characterViewer.db, -1);
        }
        if (glossaryViewer != null)
            glossaryViewer.gameObject.SetActive(false);
    }
}
