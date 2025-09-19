using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    [Header("Managers (optional wiring)")]
    public SettingsManager settings;
    public AudioManager audioManager;
    public SaveLoadManager saveLoad;
    public DataManager dataManager;

    [Header("Databases (singletons)")]
    public GlossaryDatabase glossaryDb;
    public CharacterDatabase characterDb;

    [Header("Resources Paths")]
    public string glossaryPath = "StoryText/glossary";
    public string charactersPath = "StoryText/characters";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 보장(없으면 AddComponent)
        settings = settings ?? GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
        audioManager = audioManager ?? GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
        saveLoad = saveLoad ?? GetComponent<SaveLoadManager>() ?? gameObject.AddComponent<SaveLoadManager>();
        dataManager = dataManager ?? GetComponent<DataManager>() ?? gameObject.AddComponent<DataManager>();

        // 상호 참조(필요한 것만)
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        if (glossaryDb == null) glossaryDb = GlossaryDatabase.LoadFromResources(glossaryPath);   // CSV 로드(owned 초기화됨)
        if (characterDb == null) characterDb = CharacterDatabase.LoadFromResources(charactersPath);

        // 글로벌 해금 적용(로컬 프로필에서 불러옴)
        GlobalCodex.LoadInto(glossaryDb, characterDb);

        StoryFlags.Bind(GlobalFlags.Has);
        Debug.Log("[GameRoot] StoryFlags bound to GlobalFlags.Has (for lobby etc.)");

        // 설정 로드 & 적용
        settings.Load();     // 파일 없으면 기본값 생성
        settings.ApplyAll(); // 오디오/타이핑/해상도 즉시 반영

        int ownedChars = 0, ownedGloss = 0;
        for (int i = 0; i < characterDb.entryCount; i++) if (characterDb.owned.Has(i)) ownedChars++;
        for (int i = 0; i < glossaryDb.entryCount; i++) if (glossaryDb.owned.Has(i)) ownedGloss++;
        Debug.Log($"[GameRoot] owned: char {ownedChars}/{characterDb.entryCount}, gloss {ownedGloss}/{glossaryDb.entryCount}");
    }
    void Start()
    {
        settings.Load();
        settings.ApplyAll();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 바뀔 때 인게임 오브젝트를 새로 찾고 필요한 참조 주입
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        // (1) 로비/메뉴 씬이면 전역 공개 플래그로 바인딩
        if (saveLoad.runner == null)
        {
            StoryFlags.Bind(GlobalFlags.Has);
            // 전역 소유 상태를 루트 DB에 재적용(혹시 세션 중 더 해금되었을 수 있으므로)
            GlobalCodex.LoadInto(glossaryDb, characterDb);
            Debug.Log("[GameRoot] Bound StoryFlags to GlobalFlags.Has and reloaded GlobalCodex for lobby/menu.");

            audioManager.SetLobbyBgm();
        }

        // 새 씬에서 옵션 즉시 재적용(특히 해상도/캔버스 스케일, 타이핑 등)
        settings.ApplyAll();
        ChatLogManager.Instance.Clear();
        UiModalGate.Reset();
    }
    void OnApplicationQuit() => GlobalCodex.SaveFrom(glossaryDb, characterDb);
}
