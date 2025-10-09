using UnityEngine;
using UnityEngine.UI;

public sealed class CollectionsPanel : MonoBehaviour
{
    public GameObject rootPanel;      // 컬렉션 전체 패널
    public Button tabGlossary;
    public Button tabCharacters;

    public GlossaryViewer glossaryViewer;   // 이미 만들어 둔 뷰어
    public CharacterViewer characterViewer; // 아래 4) 파일

    void Awake()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        if (tabGlossary != null) tabGlossary.onClick.AddListener(ShowGlossary);
        if (tabCharacters != null) tabCharacters.onClick.AddListener(ShowCharacters);

        var root = GameRoot.Instance;
        if (root)
        {
            if (glossaryViewer) glossaryViewer.gdb = root.glossaryDb;
            if (characterViewer) characterViewer.Bind(root.characterDb);
        }
    }
    void OnEnable() { EnsureBoundAndReload(); }
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
            glossaryViewer.Open(glossaryViewer.gdb, -1); // 현재 바인딩된 DB 사용
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
    void EnsureBoundAndReload()
    {
        var root = GameRoot.Instance;
        if (!root)
        {
            Debug.LogWarning("[Collections] GameRoot missing");
            return;
        }

        // (1) 뷰어가 전역 DB를 바라보게 강제
        if (glossaryViewer && glossaryViewer.gdb != root.glossaryDb)
            glossaryViewer.gdb = root.glossaryDb; // GlossaryViewer는 Bind가 없음. 필드 주입

        if (characterViewer && characterViewer.db != root.characterDb)
            characterViewer.Bind(root.characterDb); // CharacterViewer는 Bind 제공

        // (2) 전역 소유 상태 재적용(세션 중 새로 해금됐을 수 있으니 가볍게 동기화)
        GlobalCodex.LoadInto(root.glossaryDb, root.characterDb);

        // (3) 디버그 ? 로비에서 ever 상태가 실제로 잡혔는지 확인
        int ownedChars = 0;
        for (int i = 0; i < root.characterDb.entryCount; i++)
            if (root.characterDb.owned.Has(i)) ownedChars++;
        Debug.Log($"[Collections] bound to GameRoot DB. ownedChars={ownedChars}");
    }
}
